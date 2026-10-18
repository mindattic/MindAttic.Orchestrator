using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using MindAttic.Orchestrator.Sessions;
using MindAttic.Vault.Credentials;

namespace MindAttic.Orchestrator.Billing;

/// <summary>
/// One open Claude CLI and what it bills, as its context gate recorded (<c>auth</c> and the <c>[Billing]</c> line in its
/// session's state), with the Launcher tab it runs in (null: a plain terminal or an IDE, which only you can restart).
/// </summary>
public sealed record CliBilling(int Pid, string SessionId, string? Cwd, string? Auth, HostSession? Host)
{
    /// <summary>The context gate's <c>[Billing]</c> line from the CLI's start.</summary>
    public string? Line { get; init; }

    /// <summary>For a CLI billing the other mode: <c>requested</c> (its Launcher tab will restart it), <c>refused</c> (its tab
    /// couldn't be asked), <c>pending</c> (not asked yet) or <c>manual</c> (outside the Launcher); null when it bills the mode.</summary>
    public string? Restart { get; init; }

    /// <summary><see cref="BillingModes.Team"/> or <see cref="BillingModes.Api"/>; null when it bills neither (a cloud provider, a
    /// gateway) or the gate hasn't said yet.</summary>
    [JsonIgnore]
    public string? Bills => Auth switch
    {
        ContextCycler.SubscriptionAuth => BillingModes.Team,
        "api-key" => BillingModes.Api,
        _ => null,
    };

    /// <summary>The folder it runs in, by name.</summary>
    [JsonIgnore]
    public string Where => LimitWaker.Where(Cwd);
}

/// <summary>Who's billed (the mode, and the account or masked key behind it) and every open Claude CLI's own billing.</summary>
public sealed record BillingStatus(string Mode, DateTime? SinceUtc, string Headline, IReadOnlyList<CliBilling> Clis)
{
    /// <summary>Why the mode can't be fully in force (no Vault key, no claude.ai login); null when nothing's wrong.</summary>
    public string? Problem { get; init; }

    /// <summary>Open CLIs billing the other mode.</summary>
    [JsonIgnore]
    public IReadOnlyList<CliBilling> Mismatched => [.. Clis.Where(c => c.Restart is not null)];
}

/// <summary>What a switch did: the status after it and what to say, or why nothing was changed.</summary>
public sealed record BillingSwitchResult(BillingStatus Status, IReadOnlyList<string> Lines, string? Error);

/// <summary>
/// The one billing switch for every CLI (M.ORCH-T436). A switch, from the dashboard's toggle, <c>orchestrator billing
/// team|api</c> or a /login to a claude.ai account (the last sign-in decides), does three things:
/// <list type="number">
/// <item>sets Claude Code's choice of the Vault key (<see cref="ClaudeKeyChoice"/>): approved for the API key, rejected for
/// Claude Team, so a CLI started with it bills what the mode says;</item>
/// <item>writes the mode where the Launcher and the context gate read it (<see cref="BillingModeFile"/>);</item>
/// <item>asks the Launcher to restart each open hosted CLI that bills the other thing with <c>claude --resume</c> at its next
/// idle point, never mid-turn (the control pipe contract in docs/ORCHESTRATION.md); a CLI outside the Launcher is flagged
/// for you to restart.</item>
/// </list>
/// The full key is never written anywhere but where Claude Code keeps its last 20 characters, and never shown but masked.
/// </summary>
public sealed class BillingSwitch
{
    /// <summary>The MindAttic Vault keyring's provider whose key the API mode bills.</summary>
    public const string VaultProvider = "Claude";

    /// <summary>How long before a tab that couldn't be asked is asked again (it may have been updated or reopened since).</summary>
    public static readonly TimeSpan RetryAfter = TimeSpan.FromMinutes(5);

    public BillingSwitch(string? sessionContextDir = null)
    {
        SessionContextDir = sessionContextDir ?? Path.Combine(Setup.AgentGate.ClaudeDir, "session-context");
        ModeFile = new BillingModeFile(Path.Combine(SessionContextDir, BillingModeFile.FileName));
    }

    /// <summary>Where the context gate keeps each session's state, and where the mode is written.</summary>
    public string SessionContextDir { get; }

    public BillingModeFile ModeFile { get; }

    /// <summary>Claude Code's own config (<c>~/.claude.json</c>); replaceable for tests, which never touch the real one.</summary>
    public string ConfigPath { get; init; } = ClaudeKeyChoice.DefaultConfigPath;

    /// <summary>Where the context gate records when it set a key aside, so a CLI started on it still reads as billing it.</summary>
    internal string SetAsidePath => Path.Combine(SessionContextDir, Setup.AgentGate.ApiKeySetAsideName);

    /// <summary>The Claude key in the MindAttic Vault keyring (seam).</summary>
    internal Func<string?> VaultKey { get; init; } = () => LlmCredentialStore.Default.GetKey(VaultProvider);

    /// <summary>Whether a CLI process is still running (seam).</summary>
    internal Func<int, bool> IsAlive { get; init; } = ContextCycler.IsClaude;

    /// <summary>The Launcher tab a CLI runs in, if any (seam).</summary>
    internal Func<int, IReadOnlyList<HostSession>, HostSession?> HostOf { get; init; } =
        (pid, hosts) => hosts.FirstOrDefault(h => ProcessTree.IsAncestor(h.ProcessId, pid));

    /// <summary>Sends a restart-resume request to a Launcher tab (seam): true when its control pipe took it.</summary>
    internal Func<HostSession, string, Task<bool>> Send { get; init; } =
        (host, message) => SessionControl.SendAsync(SessionControl.ControlPipeName(host), message, submit: true);

    private readonly Dictionary<string, DateTime> asked = new(StringComparer.Ordinal);
    private readonly HashSet<string> told = new(StringComparer.Ordinal);

    /// <summary>The mode in force: the one written, else the one Claude Code's config amounts to (the Vault key approved is
    /// the API key, anything else Claude Team).</summary>
    public string Mode(out BillingState? state)
    {
        state = ModeFile.Read();
        if (state is not null) return state.Mode;
        return SafeKey() is { } key && Config(ClaudeKeyChoice.Tail(key)).Choice == KeyChoice.Approved ? BillingModes.Api : BillingModes.Team;
    }

    /// <summary>Who's billed and what each open Claude CLI bills; reads only, never asks or changes anything.</summary>
    public BillingStatus Status(IReadOnlyList<HostSession> hosts, DateTime nowUtc)
    {
        var mode = Mode(out var state);
        var key = SafeKey();
        var (login, _) = Config(key is null ? null : ClaudeKeyChoice.Tail(key));
        var (headline, problem) = mode == BillingModes.Api
            ? key is null
                ? ("API KEY: none in the MindAttic Vault", $"There's no {VaultProvider} key in the MindAttic Vault keyring: add one there, or switch back to Claude Team.")
                : ($"API KEY: {ClaudeKeyChoice.Mask(key)}", (string?)null)
            : login is null
                ? ("Claude Team: not signed in", "No claude.ai account is signed in: run /login in a Claude CLI.")
                : (login.Describe(), null);
        var clis = LiveClis(hosts, nowUtc).Select(c => c.Bills is { } bills && bills != mode
            ? c with { Restart = c.Host is null ? "manual" : state is null ? "pending"
                : state.Requested.Contains(c.SessionId) ? "requested" : state.Refused.Contains(c.SessionId) ? "refused" : "pending" }
            : c).ToList();
        return new BillingStatus(mode, state?.SinceUtc, headline, clis) { Problem = problem };
    }

    /// <summary>Switches every CLI to <paramref name="mode"/> (by <paramref name="by"/>: "dashboard", "cli", "login"). Nothing is
    /// changed when the API key is asked for and the Vault has none, or Claude Code's config can't be changed.</summary>
    public BillingSwitchResult Switch(string mode, string by, IReadOnlyList<HostSession> hosts, DateTime nowUtc)
    {
        if (BillingModes.Parse(mode) is not { } to) return new(Status(hosts, nowUtc), [], $"\"{mode}\" isn't a billing mode: use team or api.");
        var key = SafeKey();
        if (to == BillingModes.Api && key is null)
            return new(Status(hosts, nowUtc), [], $"There's no {VaultProvider} key in the MindAttic Vault keyring, so nothing can bill the API key; billing is left as it was.");
        var lines = new List<string>();
        var (login, _) = Config(null);
        var approvedNow = false;
        if (key is not null)
        {
            try
            {
                // Dated first, as the context gate does: a CLI started on the key reads as still on it the moment the file changes.
                // Only a key approved now is set aside: one already rejected, or never answered, was never what a CLI billed, and
                // dating it would make every CLI started before now with it in its environment read as on the key (M.ORCH-T450).
                if (to == BillingModes.Team && ClaudeKeyChoice.Read(ConfigPath, ClaudeKeyChoice.Tail(key)).Choice == KeyChoice.Approved)
                    MarkSetAside(key, nowUtc);
                var changed = ClaudeKeyChoice.Set(ConfigPath, ClaudeKeyChoice.Tail(key), approve: to == BillingModes.Api);
                approvedNow = changed && to == BillingModes.Api;
                lines.Add(to == BillingModes.Api
                    ? $"Claude Code {(changed ? "now uses" : "already used")} the Vault key ({ClaudeKeyChoice.Mask(key)}) in a CLI started with it."
                    : $"Claude Code {(changed ? "now sets aside" : "already set aside")} the Vault key ({ClaudeKeyChoice.Mask(key)}), so the claude.ai login bills.");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
            {
                return new(Status(hosts, nowUtc), [], $"Couldn't set Claude Code's choice of the Vault key in {ConfigPath}: {ex.Message} Billing is left as it was.");
            }
        }
        if (to == BillingModes.Team && login is null) lines.Add("No claude.ai account is signed in: run /login in a Claude CLI so it bills Claude Team.");

        // Switching to the mode already in force keeps its date and the restarts already asked for.
        var was = ModeFile.Read();
        var state = was is { } w && w.Mode == to ? w with { Login = login?.Identity ?? w.Login } : new BillingState(to, nowUtc, login?.Identity, by);
        if (approvedNow) state = state with { KeyApprovedUtc = nowUtc };
        try { ModeFile.Write(state); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new(Status(hosts, nowUtc), lines, $"Couldn't write {ModeFile.Path}: {ex.Message}");
        }
        lines.Insert(0, $"Billing is {BillingModes.Name(to)}{(was?.Mode == to ? " (it already was)" : "")}.");
        lines.AddRange(RequestRestarts(hosts, nowUtc));
        return new(Status(hosts, nowUtc), lines, null);
    }

    /// <summary>
    /// The daemon's part, each refresh: a /login to another claude.ai account than the one signed in when the API key was
    /// chosen switches back to Claude Team (the last sign-in decides); then each open hosted CLI billing the other mode is
    /// asked to restart (once, again after <see cref="RetryAfter"/> if its tab couldn't be asked), and each outside the
    /// Launcher is said once. Nothing happens until a mode has been written: no switch, nothing to enforce.
    /// </summary>
    public IReadOnlyList<string> Tick(IReadOnlyList<HostSession> hosts, DateTime nowUtc)
    {
        if (ModeFile.Read() is not { } state) return [];
        if (state.Mode == BillingModes.Api && Config(null).Login is { } login && login.Identity != state.Login)
        {
            var result = Switch(BillingModes.Team, "login", hosts, nowUtc);
            List<string> said = [$"a /login to {login.Describe()} switches billing back to Claude Team"];
            if (result.Error is { } error) said.Add(error);
            else said.AddRange(result.Lines);
            return said;
        }
        // Claude Code's choice of the Vault key follows the mode: a "yes" or "no" answered in a CLI's prompt since, or a mode
        // the context gate switched on its own (a /login it saw first), is put back in line.
        if (File.Exists(ConfigPath) && SafeKey() is { } key && (enforcedUtc is not { } at || nowUtc - at >= RetryAfter) &&
            Config(ClaudeKeyChoice.Tail(key)).Choice != (state.Mode == BillingModes.Api ? KeyChoice.Approved : KeyChoice.Rejected))
        {
            enforcedUtc = nowUtc;
            var result = Switch(state.Mode, state.By, hosts, nowUtc);
            return result.Error is { } error ? [error] : [.. result.Lines.Skip(1)];
        }
        return RequestRestarts(hosts, nowUtc);
    }

    private DateTime? enforcedUtc;

    /// <summary>Asks the Launcher to restart each hosted CLI billing the other mode, and says each one outside it once.</summary>
    private List<string> RequestRestarts(IReadOnlyList<HostSession> hosts, DateTime nowUtc)
    {
        var lines = new List<string>();
        if (ModeFile.Read() is not { } state) return lines;
        var name = BillingModes.Name(state.Mode);
        var stamp = state.SinceUtc.ToString("O", CultureInfo.InvariantCulture);
        var due = new List<CliBilling>();
        foreach (var c in LiveClis(hosts, nowUtc).Where(c => c.Bills is { } b && b != state.Mode))
        {
            if (c.Host is null)
            {
                if (told.Add(c.SessionId + "@" + stamp))
                    lines.Add($"the CLI in {c.Where} (pid {c.Pid}) bills {BillingModes.Name(c.Bills!)} and isn't in a Launcher tab: close it and reopen it " +
                              $"(claude --resume {c.SessionId}) to bill {name}" + (state.Mode == BillingModes.Api ? " with ANTHROPIC_API_KEY set to the Vault key" : ""));
                continue;
            }
            if (state.Requested.Contains(c.SessionId)) continue;
            if (asked.TryGetValue(c.SessionId + "@" + stamp, out var at) && nowUtc - at < RetryAfter) continue;
            due.Add(c);
        }
        if (due.Count == 0) return lines;
        var sends = due.Select(c => (Cli: c, Sent: SafeSend(c.Host!, RestartResumeMessage(c, state.Mode, nowUtc)))).ToList();
        Task.WaitAll([.. sends.Select(s => s.Sent)]);
        var requested = state.Requested.ToList();
        var refused = state.Refused.ToList();
        foreach (var (c, sent) in sends)
        {
            asked[c.SessionId + "@" + stamp] = nowUtc;
            if (sent.Result)
            {
                requested.Add(c.SessionId);
                refused.Remove(c.SessionId);
                lines.Add($"asked the Launcher to restart the CLI in {c.Where} (pid {c.Pid}) with claude --resume at its next idle point, so it bills {name}");
            }
            else if (!refused.Contains(c.SessionId))
            {
                refused.Add(c.SessionId);
                lines.Add($"couldn't ask the Launcher tab running the CLI in {c.Where} (pid {c.Pid}) to restart it (no control pipe: a Launcher from before " +
                          $"M.LNCH-T38?): close it and reopen it (claude --resume {c.SessionId}) to bill {name}");
            }
        }
        try { ModeFile.Write(state with { Requested = requested, Refused = refused }); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        return lines;
    }

    /// <summary>The restart-resume request a Launcher tab's control pipe takes (docs/ORCHESTRATION.md, "Billing"): one JSON
    /// object on one line.</summary>
    public static string RestartResumeMessage(CliBilling cli, string mode, DateTime nowUtc) => JsonSerializer.Serialize(new JsonObject
    {
        ["type"] = "restart-resume",
        ["reason"] = "billing",
        ["billing"] = mode,
        ["session"] = cli.SessionId,
        ["cliPid"] = cli.Pid,
        ["cwd"] = cli.Cwd,
        ["requestedAt"] = nowUtc.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture),
    });

    private async Task<bool> SafeSend(HostSession host, string message)
    {
        try { return await Send(host, message).ConfigureAwait(false); }
        catch (Exception ex) when (ex is IOException or TimeoutException or UnauthorizedAccessException or OperationCanceledException) { return false; }
    }

    /// <summary>
    /// The newest session of every running Claude CLI, from the context gate's state files written in the last day, with what
    /// it bills and the Launcher tab it's in.
    /// </summary>
    internal IReadOnlyList<CliBilling> LiveClis(IReadOnlyList<HostSession> hosts, DateTime nowUtc)
    {
        if (!Directory.Exists(SessionContextDir)) return [];
        var newest = new Dictionary<int, (DateTime At, CliBilling Cli)>();
        foreach (var file in Directory.EnumerateFiles(SessionContextDir, "*.state.json"))
        {
            try
            {
                var written = File.GetLastWriteTimeUtc(file);
                if (nowUtc - written > TimeSpan.FromDays(1)) continue;
                if (JsonNode.Parse(File.ReadAllText(file)) is not JsonObject o) continue;
                if (o["pid"] is not JsonValue p || !p.TryGetValue<int>(out var pid)) continue;
                var session = Path.GetFileName(file)[..^".state.json".Length];
                string? Text(string n) => o[n] is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;
                var cli = new CliBilling(pid, session, Text("cwd"), Text("auth"), null) { Line = Text("billing") };
                if (!newest.TryGetValue(pid, out var seen) || written > seen.At) newest[pid] = (written, cli);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException or FormatException) { }
        }
        return [.. newest.Values.Select(v => v.Cli).Where(c => IsAlive(c.Pid))
            .Select(c => hosts.Count == 0 ? c : c with { Host = HostOf(c.Pid, hosts) })
            .OrderBy(c => c.Cwd, StringComparer.OrdinalIgnoreCase).ThenBy(c => c.Pid)];
    }

    /// <summary>Records when the key was set aside, as the context gate does: by a hash of its last 20 characters, never the key.</summary>
    private void MarkSetAside(string key, DateTime nowUtc)
    {
        var marks = File.Exists(SetAsidePath) && JsonNode.Parse(File.ReadAllText(SetAsidePath)) is JsonObject o ? o : new JsonObject();
        marks[KeyMark(key)] = (nowUtc - DateTime.UnixEpoch).TotalSeconds;
        Directory.CreateDirectory(SessionContextDir);
        var tmp = $"{SetAsidePath}.{Environment.ProcessId}.tmp";
        File.WriteAllText(tmp, marks.ToJsonString());
        File.Move(tmp, SetAsidePath, overwrite: true);
    }

    /// <summary>The context gate's <c>key_mark</c>: the first 16 hex digits of the SHA-256 of the key's last 20 characters.</summary>
    internal static string KeyMark(string key) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(ClaudeKeyChoice.Tail(key))))[..16];

    private string? SafeKey()
    {
        try { return VaultKey() is { Length: > 0 } k ? k.Trim() : null; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException) { return null; }
    }

    private (string Path, DateTime Stamp, string? Tail, ClaudeLogin? Login, KeyChoice Choice)? config;

    /// <summary>The config's login and key choice, read again only when the file changed (it's large and rewritten often).</summary>
    private (ClaudeLogin? Login, KeyChoice Choice) Config(string? tail)
    {
        var stamp = File.Exists(ConfigPath) ? File.GetLastWriteTimeUtc(ConfigPath) : default;
        if (config is { } c && c.Path == ConfigPath && c.Stamp == stamp && (tail is null || c.Tail == tail)) return (c.Login, tail is null ? KeyChoice.None : c.Choice);
        var (login, choice) = ClaudeKeyChoice.Read(ConfigPath, tail);
        if (tail is not null || config is null) config = (ConfigPath, stamp, tail, login, choice);
        return (login, choice);
    }
}
