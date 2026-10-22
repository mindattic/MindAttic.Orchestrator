using System.Globalization;
using System.Text.Json;
using MindAttic.Orchestrator.Activity;
using MindAttic.Orchestrator.Budget;

namespace MindAttic.Orchestrator.Sessions;

/// <summary>The pace the daemon last wrote for a login's CLIs (its <c>rl-status.json</c>): the pace key ("stop" at the
/// wall), its subagents running and allowed, and when it was written. Null fields weren't written.</summary>
public sealed record LoginPace(string? Pace, string? SubagentsRunning, string? SubagentAllowance, DateTime? AtUtc)
{
    /// <summary>"2 of 6" while there's an allowance; null when unpaced.</summary>
    public string? Subagents => SubagentAllowance is { } allowed ? $"{SubagentsRunning ?? "0"} of {allowed}" : null;

    /// <summary>The login's weekly stop line the daemon wrote (M.ORCH-T609, the weekly reserve's); null from a daemon before it.</summary>
    public int? WeeklyStopPercent { get; init; }

    /// <summary>The status at <paramref name="path"/>; null when there's none or it can't be read.</summary>
    public static LoginPace? Read(string path)
    {
        try
        {
            if (!File.Exists(path)) return null;
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var doc = JsonDocument.Parse(stream);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return null;
            string? Text(string name) => root.TryGetProperty(name, out var v) ? v.ValueKind switch
            {
                JsonValueKind.String => v.GetString(),
                JsonValueKind.Number => v.GetDouble().ToString("0.#", CultureInfo.InvariantCulture),
                JsonValueKind.True => "true",
                _ => null,
            } : null;
            var at = Text("timestamp") is { } t && DateTime.TryParse(t, CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var stamp) ? stamp : (DateTime?)null;
            return new LoginPace(Text("pace") ?? (Text("stop") is not null ? "stop" : null), Text("subagents_running"), Text("subagent_allowance"), at)
            {
                WeeklyStopPercent = root.TryGetProperty("weekly_stop_percent", out var line) && line.ValueKind == JsonValueKind.Number &&
                    line.TryGetInt32(out var stop) ? stop : null,
            };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { return null; }
    }
}

/// <summary>A live Claude CLI (a repo's tab, or an autopilot agent) and the login it runs on.</summary>
public sealed record LoginCli(string SessionId, string Repo, AgentState State)
{
    /// <summary>The per-CLI login it runs on (its email), null on the global login: what its "Move to" menu leaves out (M.ORCH-T607).</summary>
    public string? OwnLogin { get; init; }

    /// <summary>Its claude.exe process id; null when not known.</summary>
    public int? CliPid { get; init; }
}

/// <summary>
/// One Claude login on this machine (M.ORCH-T603): the global <c>~/.claude</c> one every CLI uses unless started on its own, or
/// a per-CLI one under <c>~/.claude-accounts</c> (<see cref="AccountDirs"/>). Who's signed in to it, its plan, Claude's 5-hour
/// and weekly percent for it with their resets, the pace the daemon writes for its CLIs, and the live CLIs running on it.
/// </summary>
/// <param name="Email">The per-CLI login's folder name; null for the global login.</param>
/// <param name="SignedInAs">The email Claude Code has for whoever is signed in; null when nobody is, or it isn't known yet.</param>
public sealed record LoginCard(
    string? Email,
    string Directory,
    bool SignedIn,
    string? SignedInAs,
    string? Plan,
    double? FiveHourPercent,
    DateTime? FiveHourResetUtc,
    double? WeekPercent,
    DateTime? WeekResetUtc,
    LoginPace? Pace,
    IReadOnlyList<LoginCli> Clis)
{
    public bool IsGlobal => Email is null;

    /// <summary>Where this login's CLIs stop in its week (M.ORCH-T609): the line the daemon wrote for it, else the weekly
    /// reserve's default.</summary>
    public int WeeklyStopPercent => Pace?.WeeklyStopPercent
        ?? Settings.OrchestratorSettings.WeeklyStopFor(Settings.OrchestratorSettings.DefaultWeeklyReservePercent);

    /// <summary>Its week is at or past <see cref="WeeklyStopPercent"/>: its CLIs are stopped until the week resets.</summary>
    public bool AtWeeklyStop => WeekPercent is { } week && week >= WeeklyStopPercent;
}

/// <summary>
/// Builds the dashboard's Accounts view and the per-CLI half of <c>orchestrator accounts</c> (M.ORCH-T603): one
/// <see cref="LoginCard"/> per Claude login, the global one first, then each per-CLI login by email, with every live CLI put on
/// the login its session runs under (<see cref="ContextCycler.OnOwnLogin"/>; a session with no config folder of its own is on
/// the global login).
/// </summary>
public static class LoginBoard
{
    /// <summary>A per-CLI login's card, without its CLIs: its sign-in and plan from its <c>.claude.json</c>, its meter from its
    /// <c>claude-usage.json</c> (a window whose reset has passed shows nothing), its pace from its <c>rl-status.json</c>.</summary>
    public static LoginCard Own(AccountDir dir, DateTime nowUtc)
    {
        AccountProfile? profile = null;
        try { profile = AccountProfile.Read(dir.ConfigPath); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { }
        var usage = ReportedUsage.Load(nowUtc, dir.UsagePath);
        var five = ReportedUsage.Usable(usage?.FiveHour, nowUtc);
        var week = usage?.Week is { } w && w.ResetUtc > nowUtc ? w : null;
        return new LoginCard(dir.Email, dir.Directory, dir.SignedIn, profile?.Email, profile?.Plan,
            five?.Percent, five?.ResetUtc, week?.Percent, week?.ResetUtc, LoginPace.Read(dir.StatusPath), []);
    }

    /// <summary>The global login's card: who's signed in (<paramref name="current"/>, the roster's row for it), its meter as the
    /// roster last read it, and the pace at <paramref name="statusPath"/>.</summary>
    public static LoginCard Global(string directory, AccountRow? current, bool signedIn, string statusPath, DateTime nowUtc) =>
        new(null, directory, signedIn, current?.Email, current?.Plan,
            current?.FiveHourUsedAt(nowUtc), current?.FiveHourResetAt(nowUtc),
            current?.WeekUsedAt(nowUtc), current?.WeekResetAt(nowUtc), LoginPace.Read(statusPath), []);

    /// <summary>
    /// Every login's card: <paramref name="global"/> first, then each per-CLI login <paramref name="dirs"/> has, with the live
    /// <paramref name="agents"/> on each by <paramref name="onOwnLogin"/> (session → config folder). A CLI on a folder that's
    /// since gone gets a card of its own, so no running CLI goes unshown.
    /// </summary>
    public static IReadOnlyList<LoginCard> Build(LoginCard global, AccountDirs dirs, IReadOnlyList<AgentBoardRow> agents,
        IReadOnlyDictionary<string, string> onOwnLogin, DateTime nowUtc)
    {
        var cards = new List<LoginCard> { global };
        cards.AddRange(dirs.All().Select(d => Own(d, nowUtc)));
        var clis = new Dictionary<string, List<LoginCli>>(StringComparer.OrdinalIgnoreCase);
        foreach (var agent in agents)
        {
            var key = onOwnLogin.TryGetValue(agent.SessionId, out var configDir) && dirs.Of(configDir) is { } own ? own.Email : "";
            if (key.Length > 0 && cards.All(c => !string.Equals(c.Email, key, StringComparison.OrdinalIgnoreCase)))
                cards.Add(Own(dirs.For(key), nowUtc));
            if (!clis.TryGetValue(key, out var list)) clis[key] = list = [];
            list.Add(new LoginCli(agent.SessionId, agent.Repo, agent.State) { OwnLogin = key.Length > 0 ? key : null, CliPid = agent.CliPid });
        }
        return [.. cards.Select(c => clis.TryGetValue(c.Email ?? "", out var mine)
            ? c with { Clis = [.. mine.OrderBy(x => x.Repo, StringComparer.OrdinalIgnoreCase).ThenBy(x => x.SessionId, StringComparer.Ordinal)] }
            : c)];
    }
}
