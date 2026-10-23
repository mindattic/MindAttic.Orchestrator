using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using MindAttic.Orchestrator.Billing;
using MindAttic.Orchestrator.Budget;

namespace MindAttic.Orchestrator.Sessions;

/// <summary>A login a CLI can be moved to: a per-CLI login's email, or the global login (<see cref="Email"/> null).</summary>
public sealed record MoveTarget(string? Email, string ConfigDir)
{
    public bool IsGlobal => Email is null;

    /// <summary>The menu's words for it: the email, or "Fallback login".</summary>
    public string Label => Email ?? AccountMove.GlobalLabel;
}

/// <summary>What a move did: whether the Launcher tab took the request, and what to say.</summary>
public sealed record AccountMoveResult(bool Ok, string Message);

/// <summary>
/// Move to account (M.ORCH-T607): moves a running Claude CLI's ownership to another signed-in login without losing its session.
/// The orchestrator sends the CLI's Launcher tab the restart-resume request (docs/ORCHESTRATION.md, "The Launcher's control
/// pipe") with the login's folder as <c>configDir</c> and no <c>billing</c>, so the tab ends the CLI at its next idle point and
/// starts <c>claude --resume &lt;session&gt;</c> under that <c>CLAUDE_CONFIG_DIR</c> (<c>""</c>: the global login), keeping it
/// for every later restart (M.LNCH-T52). A CLI outside the Launcher can't be moved: only you can restart it.
/// </summary>
public sealed class AccountMove(AccountDirs? dirs = null, BillingSwitch? billing = null)
{
    public const string GlobalLabel = "Fallback login";

    public AccountDirs Dirs { get; } = dirs ?? new AccountDirs();

    /// <summary>The billing switch: what each CLI bills, and the Vault key a failover bills (M.ORCH-T664).</summary>
    public BillingSwitch Billing { get; } = billing ?? new BillingSwitch();

    /// <summary>Sends the request to a Launcher tab's control pipe (seam): true when it took it.</summary>
    internal Func<HostSession, string, Task<bool>> Send { get; init; } =
        (host, message) => SessionControl.SendAsync(SessionControl.ControlPipeName(host), message, submit: true);

    /// <summary>Whether the global login (<c>~/.claude.json</c>) has a Claude login signed in (seam).</summary>
    internal Func<bool> GlobalSignedIn { get; init; } = () => false;

    /// <summary>Each live CLI's session and the per-CLI login folder it runs under (seam: the context gate's record).</summary>
    internal Func<IReadOnlyDictionary<string, string>> OnOwnLogin { get; init; } = () => new ContextCycler().OnOwnLogin();

    /// <summary>The live CLIs, with their Launcher tabs (seam).</summary>
    internal Func<IReadOnlyList<HostSession>, DateTime, IReadOnlyList<CliBilling>>? LiveOverride { get; init; }

    /// <summary>The moves in flight (M.ORCH-T637): a move locks its CLI and the tickets it holds until it lands. Null moves
    /// without locking (tests that only check the request).</summary>
    public MoveLocks? Locks { get; init; }

    /// <summary>Stops the CLI's running subagents at once (seam): its turn interrupted with Esc typed into its tab, so the
    /// Launcher's restart, which waits for an idle CLI with no subagent running, comes now rather than when they finish.</summary>
    internal Func<HostSession, Task<bool>> Interrupt { get; init; } =
        host => SessionControl.SendAsync(host.PipeName, "\u001b", submit: false);

    /// <summary>A login's name as the dashboard shows it (seam), from its folder ("" the global login).</summary>
    internal Func<string, string> NameOf { get; init; } = configDir => LoginLook.ForConfigDir(configDir.Length == 0 ? null : configDir).Alias;

    /// <summary>The real thing: the global login read from <see cref="AccountDirs.HomeConfigPath"/>, and moves locked in
    /// <paramref name="store"/>'s database (the live one by default).</summary>
    public static AccountMove Default(AccountDirs? dirs = null, BillingSwitch? billing = null, Settings.JsonStore? store = null)
    {
        var d = dirs ?? new AccountDirs();
        return new AccountMove(d, billing) { GlobalSignedIn = () => IsSignedIn(d.HomeConfigPath), Locks = new MoveLocks(store ?? new Settings.JsonStore()) };
    }

    private static bool IsSignedIn(string configPath)
    {
        try { return ClaudeAccount.Current(configPath) is { } a && a != ClaudeAccount.ApiKey; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { return false; }
    }

    /// <summary>
    /// The logins a CLI on <paramref name="currentEmail"/> (null: the global login) can be moved to: every other signed-in one,
    /// the global login first when it's signed in, then each per-CLI login by email.
    /// </summary>
    public static IReadOnlyList<MoveTarget> Targets(IEnumerable<AccountDir> signedIn, bool globalSignedIn, string? currentEmail)
    {
        var targets = new List<MoveTarget>();
        if (globalSignedIn && currentEmail is not null) targets.Add(new MoveTarget(null, ""));
        targets.AddRange(signedIn
            .Where(d => !string.Equals(d.Email, currentEmail, StringComparison.OrdinalIgnoreCase))
            .OrderBy(d => d.Email, StringComparer.Ordinal)
            .Select(d => new MoveTarget(d.Email, d.Directory)));
        return targets;
    }

    /// <summary>The targets for a CLI on <paramref name="currentEmail"/>, as this machine's logins are now.</summary>
    public IReadOnlyList<MoveTarget> TargetsFor(string? currentEmail) => Targets(Dirs.SignedIn(), GlobalSignedIn(), currentEmail);

    /// <summary>The restart-resume request that moves <paramref name="cli"/> to <paramref name="configDir"/> ("" the global login):
    /// one JSON object on one line, with no <c>billing</c>, so the Launcher keeps the billing mode and only moves the login.</summary>
    public static string Message(CliBilling cli, string configDir, DateTime nowUtc) => JsonSerializer.Serialize(new JsonObject
    {
        ["type"] = "restart-resume",
        ["reason"] = "account",
        ["configDir"] = configDir,
        ["session"] = cli.SessionId,
        ["cliPid"] = cli.Pid,
        ["cwd"] = cli.Cwd,
        ["requestedAt"] = nowUtc.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture),
    });

    /// <summary>
    /// The restart-resume request of an emergency failover (M.ORCH-T664): <c>"reason":"failover"</c> with the <c>billing</c> this
    /// one CLI is to bill (<c>api</c> onto the Vault key, <c>team</c> back to its login) whatever <c>billing.json</c>'s mode says,
    /// and the login it runs under (<paramref name="configDir"/>, "" the global one). <paramref name="now"/> asks for it at once
    /// rather than at the next idle point: the CLI's turn already ended at a limit error.
    /// </summary>
    public static string FailoverMessage(CliBilling cli, string configDir, string billing, bool now, DateTime nowUtc) => JsonSerializer.Serialize(new JsonObject
    {
        ["type"] = "restart-resume",
        ["reason"] = "failover",
        ["billing"] = billing,
        ["configDir"] = configDir,
        ["now"] = now,
        ["session"] = cli.SessionId,
        ["cliPid"] = cli.Pid,
        ["cwd"] = cli.Cwd,
        ["requestedAt"] = nowUtc.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture),
    });

    /// <summary>Asks <paramref name="cli"/>'s Launcher tab to restart it billing <paramref name="billing"/> on its own
    /// (<see cref="FailoverMessage"/>): true when the tab took the request.</summary>
    public async Task<bool> FailoverAsync(CliBilling cli, string configDir, string billing, bool now, DateTime nowUtc)
    {
        if (cli.Host is null) return false;
        try { return await Send(cli.Host, FailoverMessage(cli, configDir, billing, now, nowUtc)).ConfigureAwait(false); }
        catch (Exception ex) when (ex is IOException or TimeoutException or UnauthorizedAccessException or OperationCanceledException) { return false; }
    }

    /// <summary>A login's name as the dashboard shows it, from its folder ("" the global login); <paramref name="fallback"/> when
    /// it can't be read.</summary>
    public string NameFor(string configDir, string fallback)
    {
        try { return NameOf(configDir) is { Length: > 0 } n ? n : fallback; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or JsonException) { return fallback; }
    }

    /// <summary>Every live Claude CLI, with its Launcher tab.</summary>
    public IReadOnlyList<CliBilling> Live(IReadOnlyList<HostSession> hosts, DateTime nowUtc) =>
        LiveOverride?.Invoke(hosts, nowUtc) ?? Billing.LiveClis(hosts, nowUtc);

    /// <summary>
    /// The live CLIs <paramref name="repoOrPid"/> names: a claude.exe process id, a session id, or a repo (the folder it runs
    /// in, by name or full path, case-insensitive).
    /// </summary>
    public static IReadOnlyList<CliBilling> Find(IReadOnlyList<CliBilling> live, string repoOrPid)
    {
        var key = repoOrPid.Trim();
        if (int.TryParse(key, NumberStyles.None, CultureInfo.InvariantCulture, out var pid))
            return [.. live.Where(c => c.Pid == pid)];
        if (live.Where(c => string.Equals(c.SessionId, key, StringComparison.OrdinalIgnoreCase)).ToList() is { Count: > 0 } bySession)
            return bySession;
        var full = key.TrimEnd('\\', '/');
        return [.. live.Where(c => string.Equals(c.Where, key, StringComparison.OrdinalIgnoreCase) ||
            (c.Cwd is { } cwd && string.Equals(cwd.TrimEnd('\\', '/'), full, StringComparison.OrdinalIgnoreCase)))];
    }

    /// <summary>The per-CLI login <paramref name="cli"/> runs on now (its email), or null for the global login.</summary>
    public string? CurrentLogin(CliBilling cli) =>
        OnOwnLogin().TryGetValue(cli.SessionId, out var dir) ? Dirs.Of(dir)?.Email : null;

    /// <summary>
    /// Moves <paramref name="cli"/> to <paramref name="email"/>'s per-CLI login (null: the global login): refused when it's
    /// already there, the login isn't signed in, or the CLI isn't in a Launcher tab; otherwise the login's folder is brought up
    /// to date (its settings follow <c>~/.claude</c>'s) and the tab is asked to restart the CLI on it at its next idle point.
    /// </summary>
    /// <remarks>With <see cref="Locks"/>, the CLI and the tickets it holds (its claims, and <paramref name="holding"/>) are
    /// locked until it lands, and its running subagents are stopped at once (M.ORCH-T637); <paramref name="repo"/> names its
    /// repo for the lock.</remarks>
    public async Task<AccountMoveResult> MoveAsync(CliBilling cli, string? email, DateTime nowUtc, IReadOnlyCollection<string>? holding = null, string? repo = null)
    {
        string? normalized = null;
        if (email is not null && (normalized = AccountDirs.Normalize(email)) is null)
            return new(false, $"\"{email}\" isn't an email address: a per-CLI login is named by the account's whole email.");
        var to = normalized ?? GlobalLabel;
        var current = CurrentLogin(cli);
        if (string.Equals(current, normalized, StringComparison.OrdinalIgnoreCase))
            return new(false, $"The CLI in {cli.Where} (pid {cli.Pid}) is already on {(current ?? GlobalLabel.ToLowerInvariant())}.");
        string configDir;
        if (normalized is null)
        {
            if (!GlobalSignedIn()) return new(false, "No claude.ai account is signed in to the global login: run /login in a Claude CLI on it first.");
            configDir = "";
        }
        else
        {
            var dir = Dirs.For(normalized);
            if (!dir.SignedIn)
                return new(false, $"{normalized} isn't signed in: sign it in first (orchestrator login --account {normalized}, or Sign in on its card).");
            try { Dirs.Prepare(dir); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return new(false, $"Couldn't prepare {normalized}'s login ({ex.Message}), so the CLI in {cli.Where} wasn't moved.");
            }
            configDir = dir.Directory;
        }
        if (cli.Host is null)
            return new(false, $"The CLI in {cli.Where} (pid {cli.Pid}) isn't in a Launcher tab, so only you can move it: close it and reopen it on {to} " +
                              $"({(normalized is null ? "" : $"{AccountDirs.EnvVar}={configDir} ")}claude --resume {cli.SessionId}).");
        // Locked from now until it lands (M.ORCH-T637): the CLI and every ticket it or its subagents hold, so nothing else
        // starts, claims or hands them out meanwhile; refused when it's already on its way somewhere.
        MoveLock? locked = null;
        if (Locks is { } locks)
        {
            string name;
            try { name = NameOf(configDir); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or JsonException) { name = to; }
            List<string> tickets;
            try { tickets = MoveLocks.ClaimsOf(locks.Store.LoadAssignments(), cli.Pid, cli.SessionId); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException) { tickets = []; }
            foreach (var t in holding ?? []) if (!tickets.Contains(t, StringComparer.OrdinalIgnoreCase)) tickets.Add(t);
            locked = new MoveLock
            {
                SessionId = cli.SessionId, Pid = cli.Pid, HostPid = cli.Host.ProcessId, HostPipe = cli.Host.PipeName,
                Repo = repo ?? cli.Where, Cwd = cli.Cwd, FromEmail = current, ToEmail = normalized, ToName = name,
                StartedUtc = nowUtc, Tickets = tickets,
            };
            try
            {
                if (!locks.TryBegin(locked, out var already))
                    return new(false, $"The CLI in {cli.Where} (pid {cli.Pid}) is already moving to {already!.ToName}: wait for that move to land.");
            }
            catch (IOException ex) { return new(false, $"Couldn't lock the CLI in {cli.Where} for the move ({ex.Message}), so it wasn't moved."); }
        }
        bool sent;
        try { sent = await Send(cli.Host, Message(cli, configDir, nowUtc)).ConfigureAwait(false); }
        catch (Exception ex) when (ex is IOException or TimeoutException or UnauthorizedAccessException or OperationCanceledException) { sent = false; }
        if (!sent)
        {
            // Nothing was asked of the tab: its old owner keeps everything, unlocked at once.
            if (locked is not null)
                try { Locks!.End(locked.SessionId); }
                catch (IOException) { }
            return new(false, $"Couldn't ask the Launcher tab running the CLI in {cli.Where} (pid {cli.Pid}) to move it (no control pipe: a Launcher from before " +
                              $"M.LNCH-T52?): close it and reopen it on {to} (claude --resume {cli.SessionId}).");
        }
        // Its subagents move with it: stopped now (their tickets stay claimed, their worktrees kept), handed back once it lands.
        var stopped = "";
        if (locked is { Tickets.Count: > 0 })
        {
            bool interrupted;
            try { interrupted = await Interrupt(cli.Host).ConfigureAwait(false); }
            catch (Exception ex) when (ex is IOException or TimeoutException or UnauthorizedAccessException or OperationCanceledException) { interrupted = false; }
            if (interrupted)
                try { Locks!.Put(locked with { Interrupted = true }); }
                catch (IOException) { }
            stopped = interrupted
                ? $" Its subagents were stopped; {string.Join(", ", locked.Tickets)} stay claimed with their worktrees and are handed back to it to start again once it's there."
                : $" Its subagents couldn't be stopped (typing into its tab failed), so it moves once they finish; {string.Join(", ", locked.Tickets)} stay locked until then.";
        }
        return new(true, $"Asked the Launcher to move the CLI in {cli.Where} (pid {cli.Pid}) from {current ?? GlobalLabel.ToLowerInvariant()} to {(normalized ?? GlobalLabel.ToLowerInvariant())}: " +
                         "it restarts with claude --resume at its next idle point, keeping its session." + stopped);
    }
}
