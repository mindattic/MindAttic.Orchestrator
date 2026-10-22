using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using MindAttic.Components.Wpf.Controls;
using MindAttic.Orchestrator.Settings;
using MindAttic.Orchestrator.Setup;

namespace MindAttic.Orchestrator.Wpf;

/// <summary>
/// One exe, three jobs. <c>--daemon</c>: the headless always-on loop Scheduler's Orchestrator.Daemon job
/// starts at logon (M.WD-T5; this exe registers no task of its own, M.ORCH-T475).
/// <c>--uninstall</c>: remove the shortcuts and hooks. Otherwise the dashboard, which first makes
/// sure it's installed and set up: dropped anywhere and double-clicked, it installs itself and hands
/// over to the installed copy; the installed copy starts the daemon if it isn't running, repairs its
/// shortcuts if they're missing or stale, and asks where the workspace is if it can't find one.
/// </summary>
public partial class App : Application
{
    // WCAG 1.4.13: a tooltip stays up until the pointer or focus leaves or Esc is pressed, rather than vanishing after 5s.
    static App() => MaToolTips.KeepOpen();

    private Mutex? daemonMutex;
    private EventWaitHandle? exitSignal;
    private SingleInstance? instance;
    private IDisposable? highContrast;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        if (e.Args.Contains(Installer.DaemonArg, StringComparer.OrdinalIgnoreCase))
        {
            RunDaemon();
            return;
        }

        // Windows' high-contrast theme swaps the system colours into every theme brush, live (--high-contrast forces it, to
        // check the colours without changing Windows). The dashboard's state colours are the theme's MaGood, MaWarn, MaLink,
        // MaBad and MaMuted (ViewModels/Palette.cs), so it registers no keys of its own.
        highContrast = MaHighContrast.Track(this, force: e.Args.Contains("--high-contrast", StringComparer.OrdinalIgnoreCase));

        OrchestratorLog.Start("Dashboard");
        var log = OrchestratorLog.For("Dashboard");
        DispatcherUnhandledException += (_, args) => log.Fatal(args.Exception, "Dashboard crashed on the UI thread");
        AppDomain.CurrentDomain.UnhandledException += (_, args) => log.Fatal(args.ExceptionObject as Exception, "Dashboard crashed");
        TaskScheduler.UnobservedTaskException += (_, args) => log.Error(args.Exception, "Unobserved task exception");

        var installer = new Installer();
        if (e.Args.Contains("--uninstall", StringComparer.OrdinalIgnoreCase))
        {
            var hooks = installer.Uninstall();
            log.Information("Uninstalled: removed the shortcuts (and any legacy logon task); hooks: {Hooks:l}", hooks ?? "none registered");
            MaMessageBox.Show($"The background daemon, the shortcuts and the CLI hooks are gone.\n\nTo finish, delete {installer.InstallDir} and remove Scheduler's Orchestrator.Daemon job, which starts the daemon at logon.",
                "MindAttic Orchestrator", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        var self = Environment.ProcessPath!;

        // What a double-click does, without the window: deploy.ps1 runs this after publishing. The one way a
        // dev or worktree build installs; it still won't replace a newer installed exe unless --force is passed too.
        if (e.Args.Contains("--install", StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                if (!installer.IsInstalled(self)) installer.Install(self, force: e.Args.Contains("--force", StringComparer.OrdinalIgnoreCase));
                log.Information("Installed {Exe:l}", installer.InstalledExe);
                if (EnsureWorkspace(log)) RepairSetup(installer, installer.InstalledExe, log);
                // The install stopped the daemon; with no workspace picked (the prompt cancelled) it still runs again from the
                // new build, or pacing and the hooks' holds stay off until the next logon (M.ORCH-T370).
                else if (!Installer.DaemonRunning())
                    try { installer.EnsureDaemon(installer.InstalledExe); }
                    catch (InvalidOperationException ex) { log.Error(ex, "Couldn't restart the daemon after the install"); }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                log.Error(ex, "Install failed");
                Console.Error.WriteLine($"Install failed: {ex.Message}");
                Shutdown(1); // deploy.ps1 reads the exit code: the old copy still being there isn't an install
                return;
            }
            Shutdown();
            return;
        }

        // A dev, worktree or scratch build runs as itself and leaves the install alone (M.ORCH-T225): only a
        // downloaded copy installs on a double-click, and never over a newer installed exe.
        var dev = Installer.IsDevBuild(self);
        if (!dev && !installer.IsInstalled(self))
        {
            if (installer.WhyNotReplace(self) is { } why)
            {
                log.Information("Not installing {Self:l}: {Why:l}; starting the installed copy instead", self, why);
                if (TryHandOver(installer, log)) { Shutdown(); return; }
                dev = true; // couldn't start it: run from here, without repointing the daemon and shortcuts at this older copy
            }
            else if (TryInstallAndHandOver(installer, self, log)) { Shutdown(); return; }
        }

        // One dashboard (M.ORCH-T497): a later launch brings this one forward and exits.
        instance = SingleInstance.Claim(SingleInstance.DashboardName, TimeSpan.FromSeconds(5));
        if (instance is null)
        {
            log.Information("A dashboard is already running; asked it to show itself, and this one exits");
            Shutdown();
            return;
        }
        // Holding the mutex, any other dashboard of this exe is from an older build, maybe hidden in the tray (M.ORCH-T501).
        var retired = DashboardWindow.RetireHiddenCopies(self);
        if (retired > 0) log.Information("Asked {Count} dashboard(s) an older build left hidden in the tray to exit", retired);

        if (!EnsureWorkspace(log)) { Shutdown(); return; }

        if (!dev) RepairSetup(installer, self, log);

        // Closing the window quits: there's no tray to hide in (M.ORCH-T501).
        ShutdownMode = ShutdownMode.OnMainWindowClose;
        var main = new MainWindow();
        MainWindow = main;
        // This dashboard holds the terminal panes CLIs open in when Settings says Orchestrator (M.ORCH-T618).
        main.HostPanes();
        MainWindow.Show();
        DashboardWindow.Track(MainWindow);
        instance.OnShowRequested(() => Dispatcher.BeginInvoke(() => { if (MainWindow is { } window) DashboardWindow.Restore(window); }));
        // An upgrade asks this dashboard to exit here (it may be minimized, or behind other windows).
        exitSignal = new EventWaitHandle(false, EventResetMode.ManualReset, Installer.DashboardExitEvent(Environment.ProcessId));
        ThreadPool.RegisterWaitForSingleObject(exitSignal, (_, _) => Dispatcher.BeginInvoke(() => Shutdown()), null, Timeout.Infinite, executeOnlyOnce: true);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        OrchestratorLog.Stop();
        daemonMutex?.Dispose();
        exitSignal?.Dispose();
        instance?.Dispose();
        highContrast?.Dispose();
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
        ThreadPool.QueueUserWorkItem(_ =>
        {
            var (folders, bytes) = ExtractionPruner.Prune(ExtractionPruner.BaseDir(), Path.GetFileNameWithoutExtension(Environment.ProcessPath!));
            if (folders > 0)
                OrchestratorLog.For("Daemon").Information("Deleted {Folders} old builds' unpacked files ({MB:N0} MB) from {Dir:l}",
                    folders, bytes / 1048576.0, ExtractionPruner.BaseDir());
        });
    }

    /// <summary>Running from a download folder over an older install (or none): install there and start the installed copy instead.</summary>
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
            MaMessageBox.Show($"Couldn't install to {installer.InstallDir}: {ex.Message}\n\nRunning from here instead; the background daemon will point at this copy.",
                "MindAttic Orchestrator", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
    }

    /// <summary>An older copy run from elsewhere: start the newer installed copy instead of replacing it.</summary>
    private static bool TryHandOver(Installer installer, Serilog.ILogger log)
    {
        try
        {
            Process.Start(new ProcessStartInfo(installer.InstalledExe) { UseShellExecute = true, WorkingDirectory = installer.InstallDir });
            return true;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            log.Error(ex, "Starting the installed {Exe:l} failed; running from here", installer.InstalledExe);
            return false;
        }
    }

    /// <summary>The daemon (started if it isn't running; Scheduler starts it at logon) and the shortcuts, checked every start and fixed only when missing or pointing elsewhere.</summary>
    private static void RepairSetup(Installer installer, string self, Serilog.ILogger log)
    {
        try
        {
            var gate = new MindAttic.Orchestrator.Settings.JsonStore().LoadSettings();
            foreach (var did in new[] { installer.EnsureDaemon(self), Installer.EnsureShortcuts(self), AgentGate.Ensure(contextPercent: gate.ContextCyclePercent, keepGoing: gate.KeepGoing), AgentGate.EnsureOtherProviders() })
                if (did is not null) log.Information("Setup: {Did:l}", did);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or COMException)
        {
            log.Error(ex, "Setup couldn't finish");
            MaMessageBox.Show($"Setup couldn't finish: {ex.Message}\n\nThe dashboard works; the background daemon (budget file, pacing, autopilot) may not run until this is fixed.",
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

        var choice = MaMessageBox.Show(
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
        // Not LoadSettings: a settings.json that can't be read would be saved over with defaults, every budget, priority,
        // approval and reservation lost; and a save that fails is said, not a crash at startup (M.ORCH-T337).
        try { store.SaveSettings(store.LoadSettingsToChange() with { WorkspaceRoot = dialog.FolderName }); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            log.Error(ex, "Couldn't save the workspace folder");
            MaMessageBox.Show($"Couldn't save the workspace folder: {ex.Message}", "MindAttic Orchestrator", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
        log.Information("Workspace set to {Root:l}", dialog.FolderName);
        return true;
    }
}
