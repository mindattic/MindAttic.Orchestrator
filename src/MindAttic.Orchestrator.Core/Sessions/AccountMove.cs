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

    /// <summary>The menu's words for it: the email, or "Global login".</summary>
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
    public const string GlobalLabel = "Global login";

    public AccountDirs Dirs { get; } = dirs ?? new AccountDirs();

    private readonly BillingSwitch billing = billing ?? new BillingSwitch();

    /// <summary>Sends the request to a Launcher tab's control pipe (seam): true when it took it.</summary>
    internal Func<HostSession, string, Task<bool>> Send { get; init; } =
        (host, message) => SessionControl.SendAsync(SessionControl.ControlPipeName(host), message, submit: true);

    /// <summary>Whether the global login (<c>~/.claude.json</c>) has a Claude login signed in (seam).</summary>
    internal Func<bool> GlobalSignedIn { get; init; } = () => false;

    /// <summary>Each live CLI's session and the per-CLI login folder it runs under (seam: the context gate's record).</summary>
    internal Func<IReadOnlyDictionary<string, string>> OnOwnLogin { get; init; } = () => new ContextCycler().OnOwnLogin();

    /// <summary>The live CLIs, with their Launcher tabs (seam).</summary>
    internal Func<IReadOnlyList<HostSession>, DateTime, IReadOnlyList<CliBilling>>? LiveOverride { get; init; }

    /// <summary>The real thing: the global login read from <see cref="AccountDirs.HomeConfigPath"/>.</summary>
    public static AccountMove Default(AccountDirs? dirs = null, BillingSwitch? billing = null)
    {
        var d = dirs ?? new AccountDirs();
        return new AccountMove(d, billing) { GlobalSignedIn = () => IsSignedIn(d.HomeConfigPath) };
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

    /// <summary>Every live Claude CLI, with its Launcher tab.</summary>
    public IReadOnlyList<CliBilling> Live(IReadOnlyList<HostSession> hosts, DateTime nowUtc) =>
        LiveOverride?.Invoke(hosts, nowUtc) ?? billing.LiveClis(hosts, nowUtc);

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
    public async Task<AccountMoveResult> MoveAsync(CliBilling cli, string? email, DateTime nowUtc)
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
        bool sent;
        try { sent = await Send(cli.Host, Message(cli, configDir, nowUtc)).ConfigureAwait(false); }
        catch (Exception ex) when (ex is IOException or TimeoutException or UnauthorizedAccessException or OperationCanceledException) { sent = false; }
        return sent
            ? new(true, $"Asked the Launcher to move the CLI in {cli.Where} (pid {cli.Pid}) from {current ?? GlobalLabel.ToLowerInvariant()} to {(normalized ?? GlobalLabel.ToLowerInvariant())}: " +
                        "it restarts with claude --resume at its next idle point, keeping its session.")
            : new(false, $"Couldn't ask the Launcher tab running the CLI in {cli.Where} (pid {cli.Pid}) to move it (no control pipe: a Launcher from before " +
                         $"M.LNCH-T52?): close it and reopen it on {to} (claude --resume {cli.SessionId}).");
    }
}
