namespace MindAttic.Orchestrator;

/// <summary>Where the orchestrator looks for things, and how it classifies activity.</summary>
public sealed record OrchestratorOptions
{
    /// <summary>The defaults with your saved settings applied (the workspace picked on first run).</summary>
    public static OrchestratorOptions Load(Settings.JsonStore? store = null) =>
        (store ?? new Settings.JsonStore()).LoadSettings().WorkspaceRoot is { Length: > 0 } root
            ? new OrchestratorOptions { WorkspaceRoot = root }
            : new OrchestratorOptions();

    public string WorkspaceRoot { get; init; } = MindAttic.Workflow.Workspace.DefaultRoot;

    /// <summary>Claude Code's per-project transcript store (one folder per working directory).</summary>
    public string ClaudeProjectsDir { get; init; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "projects");

    /// <summary>What the transcript reader has already parsed, so a new process only reads what's new.
    /// Machine-local (it's megabytes and rebuildable), unlike settings. Null turns it off.</summary>
    public string? TranscriptCache { get; init; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MindAttic", "MindAttic.Orchestrator", "transcripts.cache.json");

    /// <summary>The deployed Launcher (see MindAttic.Launcher/deploy.ps1).</summary>
    public string LauncherExe { get; init; } = @"C:\Apps\MindAttic.Launcher\MindAttic.Launcher.exe";

    /// <summary>The deployed Workflow Desktop (see MindAttic.Workflow/deploy.ps1), where a ticket id the dashboard lists opens (M.ORCH-T500).</summary>
    public string WorkflowDesktopExe { get; init; } = @"C:\Apps\MindAttic.Workflow.Desktop\MindAttic.Workflow.Desktop.exe";

    /// <summary>A transcript written this recently means the agent is mid-turn.</summary>
    public TimeSpan WorkingWindow { get; init; } = TimeSpan.FromMinutes(2);

    /// <summary>A transcript written this recently (but not within <see cref="WorkingWindow"/>) means
    /// the session is still open and most likely waiting on the user.</summary>
    public TimeSpan WaitingWindow { get; init; } = TimeSpan.FromMinutes(30);
}
