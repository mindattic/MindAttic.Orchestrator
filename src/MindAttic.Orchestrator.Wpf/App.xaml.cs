using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using MindAttic.Components.Wpf.Controls;
using MindAttic.Orchestrator.Sessions;
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

    /// <summary>What --install exits with when it refused because a Claude CLI is still working (M.ORCH-T873); deploy.ps1 reads it.</summary>
    public const int BusyRefusedExit = 3;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        // Started by the old name (Scheduler's logon job, the Launcher, Tickets Desktop's links): handed to the new-named exe
        // beside it before anything else, so this copy takes no mutex or pipe and writes no hooks (M.ORCH-T999).
        if (Environment.ProcessPath is { } started && Installer.ForwardTarget(started) is { } forward)
        {
            Shutdown(Forward(forward, e.Args));
            return;
        }

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
        // Handled = true contains a bad tab close or binding error to that one operation: left unhandled, WPF terminates
        // the process after this event and every open terminal dies with it (M.ORCH-T859).
        DispatcherUnhandledException += (_, args) =>
        {
            log.Error(args.Exception, "Dashboard hit an unhandled exception on the UI thread; contained, not crashing the window or its terminals");
            args.Handled = true;
            // Contained, but the session-restore file is brought up to date anyway, in case it wasn't the last (M.ORCH-T862).
            SessionRestore.Crash("ui-exception");
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            log.Fatal(args.ExceptionObject as Exception, "Dashboard crashed");
            SessionRestore.Crash("crash");
        };
        // A last write if the process ends without a clean exit having written one (skipped after it).
        AppDomain.CurrentDomain.ProcessExit += (_, _) => SessionRestore.Crash("process-exit");
        TaskScheduler.UnobservedTaskException += (_, args) => log.Error(args.Exception, "Unobserved task exception");

        // Dark unless light was chosen (M.ORCH-T678): the saved choice, before any window (the first-run prompts and the
        // workspace builder too) opens. High contrast still wins over either.
        ThemeChoice.Apply(Resources, new JsonStore());

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
            // Installing stops the running dashboard, and every CLI its Terminals panels host goes with it: refused while any Claude
            // CLI is still working or can't be read, unless --ignore-busy (M.ORCH-T873). deploy.ps1 reads the exit code.
            if (!installer.IsInstalled(self) && !e.Args.Contains("--ignore-busy", StringComparer.OrdinalIgnoreCase))
            {
                var busy = BusyClis.Scan();
                if (!busy.AllIdle)
                {
                    var lines = BusyClis.Describe(busy);
                    log.Warning("Install refused: {Count} Claude CLI(s) still working or unknown: {Clis:l}", lines.Count, string.Join(" | ", lines));
                    Console.Error.WriteLine("Install refused: these Claude CLIs are still working (or their state can't be read); --ignore-busy installs anyway.");
                    foreach (var line in lines) Console.Error.WriteLine("  " + line);
                    Shutdown(BusyRefusedExit);
                    return;
                }
            }
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
                if (TryHandOver(installer, log, DashboardRequest.HandOff(e.Args))) { Shutdown(); return; }
                dev = true; // couldn't start it: run from here, without repointing the daemon and shortcuts at this older copy
            }
            else if (TryInstallAndHandOver(installer, self, log, DashboardRequest.HandOff(e.Args))) { Shutdown(); return; }
        }

        // One dashboard (M.ORCH-T497): a later launch brings this one forward and exits, handing it --start <repo> (M.ORCH-T767)
        // and --show <email|session|ticket> (M.ORCH-T763) down the same pipe (DashboardRequest.HandOff keeps StartRequest.HandOff's).
        var startRepo = StartRequest.Repo(e.Args, out var refused);
        if (refused is not null) log.Warning("--start {Repo}: not a repo name; nothing started", refused);
        var showTarget = DashboardRequest.ShowTarget(e.Args);
        instance = SingleInstance.Claim(SingleInstance.DashboardName, TimeSpan.FromSeconds(5), DashboardRequest.HandOff(e.Args));
        if (instance is null)
        {
            log.Information(startRepo is null
                ? "A dashboard is already running; asked it to show itself, and this one exits"
                : "A dashboard is already running; asked it to show itself and start a CLI in {Repo:l}, and this one exits", startRepo);
            Shutdown();
            return;
        }
        // Holding the mutex, any other dashboard of this exe is from an older build, maybe hidden in the tray (M.ORCH-T501).
        var retired = DashboardWindow.RetireHiddenCopies(self);
        if (retired > 0) log.Information("Asked {Count} dashboard(s) an older build left hidden in the tray to exit", retired);

        if (!EnsureWorkspace(log)) { Shutdown(); return; }

        if (!dev) RepairSetup(installer, self, log);

        // The last run's session-restore file read and rotated aside before this run writes its own (M.ORCH-T862).
        SessionRestore.Begin();
        dashboard = true;
        // Windows logging off or restarting ends every terminal too: its tabs are offered back at the next start.
        SessionEnding += (_, _) => SessionRestore.Exit(clean: false, "windows-session-ending");

        // Closing the window quits: there's no tray to hide in (M.ORCH-T501).
        ShutdownMode = ShutdownMode.OnMainWindowClose;
        var main = new MainWindow();
        MainWindow = main;
        // This dashboard holds the Terminals panels' terminals, and takes the ones the Launcher sends it (M.ORCH-T810).
        main.HostTerminals();
        // Every reading and snapshot pushed from the daemon the moment it lands, rather than polled (M.ORCH-T854).
        main.ListenForStatus();
        MainWindow.Show();
        DashboardWindow.Track(MainWindow);
        instance.OnShowRequested(args => Dispatcher.BeginInvoke(() =>
        {
            if (MainWindow is not { } window) return;
            DashboardWindow.Restore(window);
            if (window is MainWindow main && StartRequest.Repo(args) is { } repo) _ = main.StartRepoAsync(repo);
            if (window is MainWindow shown && DashboardRequest.ShowTarget(args) is { } target) _ = shown.ShowAsync(target);
        }));
        if (startRepo is not null) _ = ((MainWindow)MainWindow).StartRepoAsync(startRepo);
        if (showTarget is not null) _ = ((MainWindow)MainWindow).ShowAsync(showTarget);
        // An upgrade asks this dashboard to exit here (it may be minimized, or behind other windows).
        exitSignal = new EventWaitHandle(false, EventResetMode.ManualReset, Installer.DashboardExitEvent(Environment.ProcessId));
        // An upgrade restarting it isn't a clean exit: its tabs are offered back when the new build starts (M.ORCH-T862).
        ThreadPool.RegisterWaitForSingleObject(exitSignal, (_, _) =>
        {
            try { Dispatcher.BeginInvoke(() => { SessionRestore.Exit(clean: false, "upgrade"); Shutdown(); }); }
            catch (Exception ex) { log.Error(ex, "Exit signal failed"); }
        }, null, Timeout.Infinite, executeOnlyOnce: true);
    }

    /// <summary>This process is the dashboard (not the daemon or an install run): it writes the session-restore file.</summary>
    private bool dashboard;

    protected override void OnExit(ExitEventArgs e)
    {
        // Closed on its own: written once more, marked clean, so the next start offers nothing (M.ORCH-T862).
        if (dashboard) SessionRestore.Exit(clean: true, "exit");
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
        new Thread(() =>
        {
            try { new Daemon(new JsonStore()).Run(TimeSpan.FromSeconds(15), CancellationToken.None); }
            catch (Exception ex) { OrchestratorLog.For("Daemon").Error(ex, "The daemon loop died"); }
        })
        {
            IsBackground = true, Name = "Daemon",
        }.Start();
        ThreadPool.QueueUserWorkItem(_ =>
        {
            try
            {
                // The old-named copy (M.ORCH-T999) unpacks under its own name each time it forwards a launch.
                var (folders, bytes) = (0, 0L);
                foreach (var app in new[] { Path.GetFileNameWithoutExtension(Environment.ProcessPath!), Path.GetFileNameWithoutExtension(Installer.LegacyExeName) }.Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    var (f, b) = ExtractionPruner.Prune(ExtractionPruner.BaseDir(), app);
                    folders += f;
                    bytes += b;
                }
                if (folders > 0)
                    OrchestratorLog.For("Daemon").Information("Deleted {Folders} old builds' unpacked files ({MB:N0} MB) from {Dir:l}",
                        folders, bytes / 1048576.0, ExtractionPruner.BaseDir());
            }
            catch (Exception ex) { OrchestratorLog.For("Daemon").Error(ex, "Pruning old builds' files failed"); }
        });
    }

    /// <summary>Starts <paramref name="exe"/> with <paramref name="args"/> and returns this process's exit code. A daemon launch waits
    /// for it, so a logon job watching this process sees the daemon's lifetime and exit code, not a forwarder that ended at once.</summary>
    private static int Forward(string exe, string[] args)
    {
        var daemon = args.Contains(Installer.DaemonArg, StringComparer.OrdinalIgnoreCase);
        // The caller's working directory is kept (M.ORCH-T1044): a Launcher or Tickets link's relative path arguments resolve against
        // it, not the install folder. The daemon still runs from its own folder, as DaemonStartInfo starts it.
        var psi = new ProcessStartInfo(exe)
        {
            UseShellExecute = false, CreateNoWindow = daemon,
            WorkingDirectory = daemon ? Path.GetDirectoryName(exe)! : Environment.CurrentDirectory,
        };
        foreach (var a in args) psi.ArgumentList.Add(a);
        try
        {
            using var p = Process.Start(psi);
            if (p is null) return 1;
            if (!daemon) return 0;
            p.WaitForExit();
            return p.ExitCode;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException) { return 1; }
    }

    /// <summary>Running from a download folder over an older install (or none): install there and start the installed copy instead.</summary>
    private static bool TryInstallAndHandOver(Installer installer, string self, Serilog.ILogger log, IReadOnlyList<string> args)
    {
        // A double-click install restarts the running dashboard too (M.ORCH-T873): asked first while any CLI is still working.
        var busy = BusyClis.Scan();
        if (!busy.AllIdle)
        {
            var lines = BusyClis.Describe(busy);
            log.Warning("Install would cut off {Count} Claude CLI(s) still working or unknown: {Clis:l}", lines.Count, string.Join(" | ", lines));
            var go = MaMessageBox.Show("These Claude CLIs are still working, or their state can't be read:\n\n" + string.Join("\n", lines.Take(12)) +
                (lines.Count > 12 ? $"\n...and {lines.Count - 12} more" : "") +
                "\n\nInstalling restarts the dashboard, and every CLI in its Terminals panels loses its state.\n\nYes: install anyway. No: open the installed copy and install later.",
                "MindAttic Orchestrator", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
            if (go != MessageBoxResult.Yes)
            {
                log.Information("Install declined while CLIs were working; starting the installed copy instead");
                return installer.InstalledExe is { } exe && File.Exists(exe) && TryHandOver(installer, log, args);
            }
        }
        try
        {
            installer.Install(self);
            log.Information("Installed {From:l} to {To:l}", self, installer.InstalledExe);
            Process.Start(InstalledCopy(installer, args));
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

    /// <summary>The installed copy, passed on what this launch hands over (<c>--start &lt;repo&gt;</c>, already checked to be a plain
    /// repo name), each argument quoted on its own.</summary>
    private static ProcessStartInfo InstalledCopy(Installer installer, IReadOnlyList<string> args)
    {
        var psi = new ProcessStartInfo(installer.InstalledExe) { UseShellExecute = true, WorkingDirectory = installer.InstallDir };
        foreach (var a in args) psi.ArgumentList.Add(a);
        return psi;
    }

    /// <summary>An older copy run from elsewhere: start the newer installed copy instead of replacing it.</summary>
    private static bool TryHandOver(Installer installer, Serilog.ILogger log, IReadOnlyList<string> args)
    {
        try
        {
            Process.Start(InstalledCopy(installer, args));
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
            MaMessageBox.Show($"Setup couldn't finish: {ex.Message}\n\nThe dashboard works; the background daemon (budget file, pacing, allocate) may not run until this is fixed.",
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
