using MindAttic.Orchestrator;
using MindAttic.Orchestrator.Sessions;
using MindAttic.Orchestrator.Term;

/// <summary>The CLIs-per-login limit of <c>orchestrator start</c> (M.ORCH-T1736, the setting MaxClisPerLogin).</summary>
internal static class LoginClis
{
    /// <summary>What a start at the limit did instead: <see cref="Sent"/> when its prompt went to a CLI the login already runs.</summary>
    internal sealed record Outcome(bool Sent, string Line);

    /// <summary>The hosted sessions whose CLI runs on <paramref name="email"/>'s own login: running, and not a Launcher menu.</summary>
    internal static List<TermSessionRecord> On(IEnumerable<TermSessionRecord> sessions, string email) =>
        [.. sessions.Where(s => !s.IsMenu && !s.Exited && string.Equals(s.Login, email, StringComparison.OrdinalIgnoreCase)).OrderBy(s => s.StartedUtc)];

    /// <summary>The CLI of <paramref name="open"/> a prompt for <paramref name="repo"/> can go to: one in that repo, else one at the
    /// workspace root, which may work any repo; null when every one works another repo only.</summary>
    internal static TermSessionRecord? TargetFor(IReadOnlyList<TermSessionRecord> open, string repo, string repoPath, string workspaceRoot) =>
        open.FirstOrDefault(s => InRepo(s, repo, repoPath)) ?? open.FirstOrDefault(s => SamePath(s.Cwd, workspaceRoot));

    private static bool InRepo(TermSessionRecord s, string repo, string repoPath) =>
        string.Equals(s.Repo, repo, StringComparison.OrdinalIgnoreCase) || SamePath(s.Cwd, repoPath);

    private static bool SamePath(string? a, string? b) => a is { Length: > 0 } && b is { Length: > 0 }
        && string.Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(a)), Path.TrimEndingDirectorySeparator(Path.GetFullPath(b)), StringComparison.OrdinalIgnoreCase);

    /// <summary>A prompt as one typed line: a line break typed into a CLI would submit it early.</summary>
    internal static string OneLine(string prompt) =>
        string.Join(' ', prompt.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).Select(l => l.Trim()));

    /// <summary>Null when <paramref name="email"/>'s login runs fewer than <paramref name="cap"/> CLIs, so the start goes ahead.
    /// At the limit: a start with a prompt types it into that login's CLI in the repo or at the workspace root; otherwise nothing
    /// is started and the line says which CLIs it runs.</summary>
    internal static Outcome? AtLimit(OrchestratorOptions options, string email, int cap, string repo, string repoPath, string? prompt)
    {
        var open = On(TermSessions.Enumerate(), email);
        if (open.Count < cap) return null;
        var runs = $"{email} already runs {open.Count} CLI(s) ({string.Join(", ", open.Select(s => s.Repo ?? s.Title ?? s.Id))}), the CLIs-per-login limit";
        if (prompt is not { Length: > 0 })
            return new(false, $"{runs}: not started. --new opens one anyway.");
        if (TargetFor(open, repo, repoPath, options.WorkspaceRoot) is not { } target)
            return new(false, $"{runs}, and none of them is in {repo} or at the workspace root to take the prompt: not started. --new opens one anyway.");
        var name = target.Repo ?? target.Title ?? target.Id;
        var parents = ProcessTree.ParentMap();
        var host = new HostSessionScanner(options).Scan().FirstOrDefault(h => ProcessTree.IsAncestor(target.HostPid, h.ProcessId, parents));
        if (host is null)
            return new(false, $"{runs}, and its CLI in {name} has no input pipe to take the prompt: not started. --new opens one anyway.");
        var text = InRepo(target, repo, repoPath) ? prompt : $"For the repo {repo} ({repoPath}): {prompt}";
        return SessionControl.SubmitAsync(host.PipeName, OneLine(text)).GetAwaiter().GetResult()
            ? new(true, $"{runs}: the prompt was sent to its CLI in {name} instead of opening another.")
            : new(false, $"{runs}, and the prompt couldn't be typed into its CLI in {name}: not started. --new opens one anyway.");
    }
}
