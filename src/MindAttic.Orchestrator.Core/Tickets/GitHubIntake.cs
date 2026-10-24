using EngineIntake = MindAttic.Tickets.Repos.GitHubIntake;

namespace MindAttic.Orchestrator.Tickets;

/// <summary>
/// The daemon's hourly GitHub intake (open issues labelled <c>orchestrator</c> and CI runs that failed on main in the last day, filed
/// as tickets once each). What it finds and how it files it is the MindAttic.Tickets engine's (<see cref="EngineIntake"/>, taken over
/// in M.TIX-T134; <c>tickets ticket intake</c> and Tickets Desktop's intake button run the same); the Orchestrator only schedules it
/// (<see cref="Settings.OrchestratorSettings.GitHubIntake"/>), the one runner, and runs <c>gh</c> through its own process runner so a
/// stalled call is killed inside the daemon's tick (M.ORCH-T762).
/// </summary>
public static class GitHubIntake
{
    /// <summary>How long one <c>gh</c> call may run inside the daemon's tick before it's killed.</summary>
    internal static readonly TimeSpan GhTimeout = TimeSpan.FromMinutes(1);

    /// <summary>Runs <c>gh</c> through <paramref name="runner"/>: a call past <paramref name="timeout"/> is killed and reads as a failure.</summary>
    internal static (int Code, string Output) Gh(IProcessRunner runner, TimeSpan timeout, params string[] args)
    {
        try
        {
            // It runs inside the daemon's tick: a network stall mustn't stop pacing.
            var r = runner.Run("gh", args, timeout);
            if (r.TimedOut)
            {
                OrchestratorLog.For("GitHubIntake").Warning("gh {Args} took over {Seconds:0}s; stopped it", string.Join(' ', args), timeout.TotalSeconds);
                return (-1, $"gh took over {timeout.TotalSeconds:0}s; stopped it");
            }
            return (r.ExitCode, r.ExitCode == 0 ? r.Output : r.Error);
        }
        catch (System.ComponentModel.Win32Exception) { return (-1, "the GitHub CLI (gh) isn't installed"); }
    }

    /// <summary>Files new GitHub issues and CI failures as tickets in every workspace repo with a GitHub origin, through the
    /// engine: into the ticket database when it holds the repo, else the repo's own USER_STORIES.md.</summary>
    public static IEnumerable<(string Repo, MindAttic.Tickets.Log.Ticket Ticket)> Run(string workspaceRoot, bool dryRun = false,
        Func<string[], (int Code, string Output)>? gh = null)
    {
        gh ??= args => Gh(ProcessRunner.Default, GhTimeout, args);
        var remotes = Setup.WorkspaceBootstrap.Export(workspaceRoot).Repos.ToDictionary(m => m.Name, m => m.Remote, StringComparer.OrdinalIgnoreCase);
        using var db = MindAttic.Tickets.Data.TicketsDb.OpenIfMigrated(workspaceRoot);
        // Listed before the database is closed: the engine files lazily.
        return [.. EngineIntake.Intake(new MindAttic.Tickets.Workspace(workspaceRoot), db, dryRun, gh, remote: r => remotes.GetValueOrDefault(r.Name))];
    }
}
