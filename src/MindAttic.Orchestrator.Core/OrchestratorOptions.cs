namespace MindAttic.Orchestrator;

/// <summary>Where the orchestrator looks for things, and how it classifies activity.</summary>
public sealed record OrchestratorOptions
{
    /// <summary>The defaults with your saved settings applied (the workspace picked on first run).</summary>
    public static OrchestratorOptions Load(Settings.JsonStore? store = null) =>
        (store ?? new Settings.JsonStore()).LoadSettings().WorkspaceRoot is { Length: > 0 } root
            ? new OrchestratorOptions { WorkspaceRoot = root }
            : new OrchestratorOptions();

    /// <summary>Like <see cref="Load"/>, but false when <c>settings.json</c> is there and can't be read (a typo from a hand
    /// edit, held past the retries): its defaults aren't the workspace you picked, so nothing should switch to them.</summary>
    public static bool TryLoad(Settings.JsonStore store, out OrchestratorOptions options)
    {
        var read = store.TryLoad<Settings.OrchestratorSettings>("settings", out var s);
        options = read && s.WorkspaceRoot is { Length: > 0 } root ? new OrchestratorOptions { WorkspaceRoot = root } : new OrchestratorOptions();
        return read;
    }

    public string WorkspaceRoot { get; init; } = MindAttic.Tickets.Workspace.DefaultRoot;

    /// <summary>Claude Code's per-project transcript store (one folder per working directory).</summary>
    public string ClaudeProjectsDir { get; init; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "projects");

    /// <summary>What the transcript reader has already parsed, so a new process only reads what's new.
    /// Machine-local (it's megabytes and rebuildable), unlike settings. Null turns it off.</summary>
    public string? TranscriptCache { get; init; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MindAttic", "MindAttic.Orchestrator", "transcripts.cache.json");

    /// <summary>The deployed Launcher (see MindAttic.Launcher/deploy.ps1).</summary>
    public string LauncherExe { get; init; } = @"C:\Apps\MindAttic.Launcher\MindAttic.Launcher.exe";

    /// <summary>The deployed Tickets Desktop (see MindAttic.Tickets/deploy.ps1), where a ticket id the dashboard lists opens (M.ORCH-T500).</summary>
    public string TicketsDesktopExe { get; init; } = @"C:\Apps\MindAttic.Tickets.Desktop\MindAttic.Tickets.Desktop.exe";

    /// <summary>A transcript written this recently means the agent is mid-turn.</summary>
    public TimeSpan WorkingWindow { get; init; } = TimeSpan.FromMinutes(2);

    /// <summary>A transcript written this recently (but not within <see cref="WorkingWindow"/>) means
    /// the session is still open and most likely waiting on the user.</summary>
    public TimeSpan WaitingWindow { get; init; } = TimeSpan.FromMinutes(30);
}
