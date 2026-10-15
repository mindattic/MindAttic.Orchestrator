using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using MindAttic.Orchestrator.Settings;
using MindAttic.Orchestrator.Setup;

namespace MindAttic.Orchestrator.Wpf;

/// <summary>
/// One exe, three jobs. <c>--daemon</c>: the headless always-on loop the logon task runs.
/// <c>--uninstall</c>: remove the task and shortcuts. Otherwise the dashboard, which first makes
/// sure it's installed and set up: dropped anywhere and double-clicked, it installs itself and hands
/// over to the installed copy; the installed copy repairs its logon task and shortcuts if they're
/// missing or stale, and asks where the workspace is if it can't find one.
/// </summary>
public partial class App : Application
{
    private Mutex? daemonMutex;
    private Tray? tray;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        if (e.Args.Contains(Installer.DaemonArg, StringComparer.OrdinalIgnoreCase))
        {
            RunDaemon();
            return;
        }

        OrchestratorLog.Start("Dashboard");
        var log = OrchestratorLog.For("Dashboard");
        DispatcherUnhandledException += (_, args) => log.Fatal(args.Exception, "Dashboard crashed on the UI thread");
        AppDomain.CurrentDomain.UnhandledException += (_, args) => log.Fatal(args.ExceptionObject as Exception, "Dashboard crashed");
        TaskScheduler.UnobservedTaskException += (_, args) => log.Error(args.Exception, "Unobserved task exception");

        var installer = new Installer();
        if (e.Args.Contains("--uninstall", StringComparer.OrdinalIgnoreCase))
        {
            installer.Uninstall();
            log.Information("Uninstalled: removed the logon task and shortcuts");
            MessageBox.Show($"The background daemon and the shortcuts are gone.\n\nTo finish, delete {installer.InstallDir}.",
                "MindAttic Orchestrator", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        var self = Environment.ProcessPath!;

        // What a double-click does, without the window: deploy.ps1 runs this after publishing.
        if (e.Args.Contains("--install", StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                if (!installer.IsInstalled(self)) installer.Install(self);
                log.Information("Installed {Exe:l}", installer.InstalledExe);
                if (EnsureWorkspace(log)) RepairSetup(installer, installer.InstalledExe, log);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                log.Error(ex, "Install failed");
                Console.Error.WriteLine($"Install failed: {ex.Message}");
            }
            Shutdown();
            return;
        }

        var dev = Installer.IsDevBuild(self);
        if (!dev && !installer.IsInstalled(self))
        {
            if (TryInstallAndHandOver(installer, self, log)) { Shutdown(); return; }
        }

        if (!EnsureWorkspace(log)) { Shutdown(); return; }

        if (!dev) RepairSetup(installer, self, log);

        ShutdownMode = ShutdownMode.OnMainWindowClose;
        MainWindow = new MainWindow();
        MainWindow.Show();
        tray = new Tray(MainWindow, (ViewModels.MainViewModel)MainWindow.DataContext);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        tray?.Dispose();
        OrchestratorLog.Stop();
        daemonMutex?.Dispose();
        base.OnExit(e);
    }

    /// <summary>Headless: no window, ever. The mutex keeps a second daemon from starting and tells the dashboard one is up.</summary>
    private void RunDaemon()
    {
        OrchestratorLog.Start("Daemon");
        daemonMutex = new Mutex(initiallyOwned: true, Installer.DaemonMutex, out var first);
        if (!first)
        {
            OrchestratorLog.For("Daemon").Information("A daemon is already running; this one exits");
            Shutdown();
            return;
        }
        new Thread(() => new Daemon(new JsonStore()).Run(TimeSpan.FromSeconds(15), CancellationToken.None))
        {
            IsBackground = true, Name = "Daemon",
        }.Start();
    }

    /// <summary>Running from a download folder (or an older copy): install there and start the installed copy instead.</summary>
    private static bool TryInstallAndHandOver(Installer installer, string self, Serilog.ILogger log)
    {
        try
        {
            installer.Install(self);
            log.Information("Installed {From:l} to {To:l}", self, installer.InstalledExe);
            Process.Start(new ProcessStartInfo(installer.InstalledExe) { UseShellExecute = true, WorkingDirectory = installer.InstallDir });
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            log.Error(ex, "Installing to {Dir:l} failed; running from {Self:l}", installer.InstallDir, self);
            MessageBox.Show($"Couldn't install to {installer.InstallDir}: {ex.Message}\n\nRunning from here instead; the background daemon will point at this copy.",
                "MindAttic Orchestrator", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
    }

    /// <summary>The logon daemon and the shortcuts, checked every start and fixed only when missing or pointing elsewhere.</summary>
    private static void RepairSetup(Installer installer, string self, Serilog.ILogger log)
    {
        try
        {
            foreach (var did in new[] { installer.EnsureDaemon(self), Installer.EnsureShortcuts(self), AgentGate.Ensure(contextPercent: new MindAttic.Orchestrator.Settings.JsonStore().LoadSettings().ContextCyclePercent), AgentGate.EnsureOtherProviders() })
                if (did is not null) log.Information("Setup: {Did:l}", did);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or COMException)
        {
            log.Error(ex, "Setup couldn't finish");
            MessageBox.Show($"Setup couldn't finish: {ex.Message}\n\nThe dashboard works; the background daemon (budget file, pacing, autopilot) may not run until this is fixed.",
                "MindAttic Orchestrator", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    /// <summary>
    /// The one question setup can't answer itself, asked only when the workspace folder is missing or
    /// empty: build one here (clone everything from GitHub), or point at one that already exists.
    /// </summary>
    private static bool EnsureWorkspace(Serilog.ILogger log)
    {
        var store = new JsonStore();
        var root = OrchestratorOptions.Load(store).WorkspaceRoot;
        if (!WorkspaceBootstrap.IsEmpty(root)) return true;

        var choice = MessageBox.Show(
            "There's no MindAttic workspace on this machine yet.\n\n" +
            "Yes: build one. Pick an empty folder and every repo is cloned into it from GitHub.\n" +
            "No: point at a workspace folder that's already here.",
            "MindAttic Orchestrator", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
        if (choice == MessageBoxResult.Cancel) return false;

        var build = choice == MessageBoxResult.Yes;
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = build ? "MindAttic Orchestrator: choose an empty folder for the workspace" : "MindAttic Orchestrator: choose the folder that holds all your repos",
        };
        if (dialog.ShowDialog() != true) return false;
        if (build && !BuildWorkspaceWindow.Run(dialog.FolderName, log)) return false;
        store.SaveSettings(store.LoadSettings() with { WorkspaceRoot = dialog.FolderName });
        log.Information("Workspace set to {Root:l}", dialog.FolderName);
        return true;
    }
}
