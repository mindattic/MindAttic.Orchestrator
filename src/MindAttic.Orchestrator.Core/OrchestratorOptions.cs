namespace MindAttic.Orchestrator;

/// <summary>Where the orchestrator looks for things, and how it classifies activity.</summary>
public sealed record OrchestratorOptions
{
    /// <summary>The defaults with your saved settings applied (the workspace picked on first run).</summary>
    public static OrchestratorOptions Load(Settings.JsonStore? store = null)
    {
        var s = (store ?? new Settings.JsonStore()).LoadSettings();
        var options = s.WorkspaceRoot is { Length: > 0 } root
            ? new OrchestratorOptions { WorkspaceRoot = root, ExtraWorkspaceRoots = DefaultExtraWorkspaceRoots }
            : new OrchestratorOptions { ExtraWorkspaceRoots = DefaultExtraWorkspaceRoots };
        return WithLauncher(options, s);
    }

    /// <summary><paramref name="options"/> with the Launcher the settings name (blank keeps the default) and its run copies on.
    /// Left at the default on a PC with no Launcher there (no R: drive), the release installer's fallbacks are tried next.</summary>
    private static OrchestratorOptions WithLauncher(OrchestratorOptions options, Settings.OrchestratorSettings? s)
    {
        var exe = s?.LauncherExe is { Length: > 0 } set ? set.Trim() : options.LauncherExe;
        if (string.Equals(exe, Settings.OrchestratorSettings.DefaultLauncherExe, StringComparison.OrdinalIgnoreCase) && !File.Exists(exe))
            exe = InstalledLauncherFallbacks().FirstOrDefault(File.Exists) ?? exe;
        return options with { LauncherExe = exe, LauncherRunCopies = true };
    }

    /// <summary>Where release.ps1's installer puts the Launcher when the default's drive is missing (M.ORCH-T2285): under C:\Apps,
    /// else per-user Programs.</summary>
    private static IEnumerable<string> InstalledLauncherFallbacks()
    {
        const string relative = @"MindAttic.Launcher\Release\MindAttic.Launcher.exe";
        yield return Path.Combine(@"C:\Apps", relative);
        yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", relative);
    }

    /// <summary>Like <see cref="Load"/>, but false when <c>settings.json</c> is there and can't be read (a typo from a hand
    /// edit, held past the retries): its defaults aren't the workspace you picked, so nothing should switch to them.</summary>
    public static bool TryLoad(Settings.JsonStore store, out OrchestratorOptions options)
    {
        var read = store.TryLoad<Settings.OrchestratorSettings>("settings", out var s);
        options = WithLauncher((read && s.WorkspaceRoot is { Length: > 0 } root ? new OrchestratorOptions { WorkspaceRoot = root } : new OrchestratorOptions())
            with
        { ExtraWorkspaceRoots = DefaultExtraWorkspaceRoots }, read ? s : null);
        return read;
    }

    /// <summary>Other workspace roots whose repos become tiles too (M.ORCH-T1752), selectable and CLI-openable just like
    /// <see cref="WorkspaceRoot"/>'s: a repo there just won't have any tickets/budget history (no ticket database, no
    /// USER_STORIES.md) unless it's brought into one. Set by <see cref="Load"/>/<see cref="TryLoad"/>, not defaulted on the
    /// record itself, so a test building its own <see cref="OrchestratorOptions"/> around a scratch <see cref="WorkspaceRoot"/>
    /// doesn't also pick up whatever's really sitting in <see cref="DefaultExtraWorkspaceRoots"/> on this machine.</summary>
    public static readonly IReadOnlyList<string> DefaultExtraWorkspaceRoots = [@"D:\Projects\GunGreenEyes"];

    /// <summary>The folder whose repos are watched; the tickets workspace's default root unless set.</summary>
    public string WorkspaceRoot { get; init; } = MindAttic.Tickets.Workspace.DefaultRoot;

    /// <summary>See <see cref="DefaultExtraWorkspaceRoots"/>. Empty unless <see cref="Load"/>/<see cref="TryLoad"/> set it.</summary>
    public IReadOnlyList<string> ExtraWorkspaceRoots { get; init; } = [];

    /// <summary>Claude Code's per-project transcript store (one folder per working directory).</summary>
    public string ClaudeProjectsDir { get; init; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "projects");

    /// <summary>What the transcript reader has already parsed, so a new process only reads what's new.
    /// Machine-local (it's megabytes and rebuildable), unlike settings. Null turns it off.</summary>
    public string? TranscriptCache { get; init; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MindAttic", "MindAttic.Orchestrator", "transcripts.cache.json");

    /// <summary>The deployed Launcher (see MindAttic.Launcher/run.ps1): its Release build under R:\MindAttic\Apps since
    /// M.LNCH-T145. <see cref="Load"/>/<see cref="TryLoad"/> take it from the Vault setting
    /// (<see cref="Settings.OrchestratorSettings.LauncherExe"/>, M.ORCH-T2285), which defaults to the same path.</summary>
    public string LauncherExe { get; init; } = Settings.OrchestratorSettings.DefaultLauncherExe;

    /// <summary>Start every Launcher from a snapshot of <see cref="LauncherExe"/>'s folder (<see cref="Sessions.LauncherRunCopy"/>,
    /// M.ORCH-T2285), so the Launcher's own redeploy, which stops whatever runs from its deploy folder, leaves open tabs alone.
    /// On for the live app (<see cref="Load"/>/<see cref="TryLoad"/>); off on the bare record, so a test's stub exe is started
    /// where it is.</summary>
    public bool LauncherRunCopies { get; init; }

    /// <summary>The deployed Tickets Desktop (see MindAttic.Tickets/run.ps1), where a ticket id the dashboard lists opens (M.ORCH-T500).</summary>
    public string TicketsDesktopExe { get; init; } = @"R:\MindAttic\Apps\MindAttic.Tickets\Release\MindAttic.Tickets.Desktop.exe";

    /// <summary>A transcript written this recently means the agent is mid-turn.</summary>
    public TimeSpan WorkingWindow { get; init; } = TimeSpan.FromMinutes(2);

    /// <summary>A transcript written this recently (but not within <see cref="WorkingWindow"/>) means
    /// the session is still open and most likely waiting on the user.</summary>
    public TimeSpan WaitingWindow { get; init; } = TimeSpan.FromMinutes(30);
}
