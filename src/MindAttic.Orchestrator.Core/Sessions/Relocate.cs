using System.Globalization;
using System.Text.Json.Nodes;

namespace MindAttic.Orchestrator.Sessions;

/// <summary>
/// Dock and Undock (M.ORCH-Tnnn): relocating a Claude CLI's tab between the dashboard's embedded Terminals panel and its own
/// Windows Terminal window, keeping its conversation (<c>claude --resume</c>) rather than starting over. Unlike "Move to
/// account" (<see cref="AccountMove"/>), which restarts a CLI in place (only its login changes), a relocation ends the CLI
/// where it is and reopens it resumed on the <em>other</em> hosting surface entirely — see <see cref="RelocateLocks"/> for
/// the in-flight bookkeeping and <see cref="PaneBroker.InsideVariable"/>/<see cref="SessionControl.ControlPipeName"/> for the
/// two pipes this builds messages and requests for.
/// </summary>
public static class Relocate
{
    /// <summary>The control-pipe message that ends a CLI at its next idle point without restarting it in its current tab
    /// (<c>"reason":"relocate"</c>, a new value of the same <c>restart-resume</c> message <see cref="AccountMove"/> already
    /// sends): the Launcher's <c>host</c> process kills itself once idle-gated exactly as a move does, then simply exits.</summary>
    public static string Message(string session, int cliPid, string? cwd, DateTime requestedAtUtc) =>
        new JsonObject
        {
            ["type"] = "restart-resume",
            ["reason"] = "relocate",
            ["session"] = session,
            ["cliPid"] = cliPid,
            ["cwd"] = cwd,
            ["requestedAt"] = requestedAtUtc.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture),
        }.ToJsonString();

    /// <summary>The <c>host</c> argv that resumes <paramref name="move"/>'s session under its own (unchanged) login: the same
    /// shape <see cref="SessionControl.StartArguments"/> and the Launcher's own agent-host tabs already build, with
    /// <c>--resume</c> added and no <c>--prompt</c> (a relocation never pre-fills one).</summary>
    public static List<string> ResumedHostArguments(RelocateLock move, OrchestratorOptions options, string title)
    {
        List<string> args = [options.LauncherExe, "host", "--path", move.Cwd ?? "", "--title", title, "--provider", "Claude", "--resume", move.SessionId];
        if (move.ConfigDir is { Length: > 0 }) args.AddRange(["--config-dir", move.ConfigDir]);
        else if (move.Email is { Length: > 0 }) args.AddRange(["--account", move.Email]);
        return args;
    }

    /// <summary>Dock's reopen target: <paramref name="move"/>'s session as an embedded Terminals panel tab, on the same
    /// person's page it was already on (<see cref="PaneRequest.ConfigDir"/>/<see cref="PaneRequest.Email"/> carried straight
    /// through, unchanged by the relocation).</summary>
    public static PaneRequest ResumedPaneRequest(RelocateLock move, OrchestratorOptions options, SessionControl control)
    {
        var look = control.Look(move.ConfigDir);
        var title = SessionControl.TabTitle(move.Repo, "Claude", look, control.RepoNames);
        return new PaneRequest(title, move.Cwd ?? options.WorkspaceRoot)
        {
            Command = ResumedHostArguments(move, options, title),
            Repo = move.Repo,
            Provider = "Claude",
            ConfigDir = move.ConfigDir,
            Email = move.Email,
            Initials = move.Initials ?? look.Initials,
            TabColor = move.TabColor ?? look.TabColor,
        };
    }

    /// <summary>Undock's reopen target: <paramref name="move"/>'s session as its own Windows Terminal window, in the same
    /// login's window every standalone tab of theirs gathers in.</summary>
    public static IEnumerable<string> ResumedStartArguments(RelocateLock move, OrchestratorOptions options, SessionControl control) =>
        control.StartArguments(move.Repo, move.Cwd ?? options.WorkspaceRoot, "Claude", prompt: null, move.ConfigDir, move.SessionId);
}
