namespace MindAttic.Orchestrator;

/// <summary>Where the orchestrator looks for things, and how it classifies activity.</summary>
public sealed record OrchestratorOptions
{
    public string WorkspaceRoot { get; init; } = MindAttic.Workflow.Workspace.DefaultRoot;

    /// <summary>Claude Code's per-project transcript store (one folder per working directory).</summary>
    public string ClaudeProjectsDir { get; init; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "projects");

    /// <summary>The deployed Launcher (see MindAttic.Launcher/deploy.ps1).</summary>
    public string LauncherExe { get; init; } = @"C:\Apps\MindAttic.Launcher\MindAttic.Launcher.exe";

    /// <summary>A transcript written this recently means the agent is mid-turn.</summary>
    public TimeSpan WorkingWindow { get; init; } = TimeSpan.FromMinutes(2);

    /// <summary>A transcript written this recently (but not within <see cref="WorkingWindow"/>) means
    /// the session is still open and most likely waiting on the user.</summary>
    public TimeSpan WaitingWindow { get; init; } = TimeSpan.FromMinutes(30);
}
