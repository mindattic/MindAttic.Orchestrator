using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using MindAttic.Orchestrator.Budget;

namespace MindAttic.Orchestrator.Sessions;

/// <summary>
/// One Claude login of its own (M.ORCH-T559): a config folder a CLI runs under through <c>CLAUDE_CONFIG_DIR</c>, so it spends
/// <see cref="Email"/>'s token pool while every other CLI keeps the global login. Its sign-in (<c>.claude.json</c>,
/// <c>.credentials.json</c>), Claude's meter for it (<c>claude-usage.json</c>, written by its own statusline), its pace
/// (<c>rl-status.json</c>, written by the daemon) and its subagents' marks are all here, apart from every other account's.
/// </summary>
public sealed record AccountDir(string Email, string Directory)
{
    /// <summary>The config path.</summary>
    public string ConfigPath => Path.Combine(Directory, ".claude.json");
    /// <summary>The credentials path.</summary>
    public string CredentialsPath => Path.Combine(Directory, ".credentials.json");
    /// <summary>The settings path.</summary>
    public string SettingsPath => Path.Combine(Directory, "settings.json");

    /// <summary>The account's pace, as the daemon writes it for the hooks of the CLIs on this login.</summary>
    public string StatusPath => Path.Combine(Directory, "rl-status.json");

    /// <summary>Claude's own 5-hour and weekly percent for this account (its statusline's, or the daemon's meter probe's).</summary>
    public string UsagePath => Path.Combine(Directory, "claude-usage.json");

    /// <summary>Where this account's subagents are marked (<c>subagents</c>, slots and wants), as the hooks' SUBAGENT_MARKS.</summary>
    public string MarksDirectory => Path.Combine(Directory, "session-context");

    /// <summary>A week of Claude's readings for this account, which size its 5-hour limit and grade its ledger windows.</summary>
    public string SamplesPath => Path.Combine(Directory, "usage-samples.json");

    /// <summary>This account's own limit hits, read from its sessions' transcripts (M.ORCH-T774): kept across ticks and
    /// restarts so a wall its sessions reported keeps grading its ledger windows "hit the 5-hour limit", not "Claude reported
    /// 100%", even once the transcript line that reported it has scrolled out of the reader's window.</summary>
    public string LimitsPath => Path.Combine(Directory, "limits.json");

    /// <summary>The statusline path.</summary>
    public string StatuslinePath => Path.Combine(Directory, AccountDirs.StatuslineName);

    /// <summary>The script <c>claude auth login</c> opens this account's sign-in through (<c>BROWSER</c>, M.ORCH-T610).</summary>
    public string LoginOpenerPath => Path.Combine(Directory, "open-login.cmd");

    /// <summary>The account's own browser profile its sign-in opens in, apart from whoever the default browser is signed in as.</summary>
    public string BrowserProfileDirectory => Path.Combine(Directory, "login-browser");

    /// <summary>The account signed in here, keyed as <see cref="ClaudeAccount.Current"/> keys it; null when nobody is (yet).</summary>
    public string? Account
    {
        get
        {
            try { return ClaudeAccount.Current(ConfigPath); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { return null; }
        }
    }

    /// <summary>Whether a Claude login (not an API key) is signed in here.</summary>
    public bool SignedIn => Account is { } a && a != ClaudeAccount.ApiKey;

    /// <summary>The email Claude Code has for the login here; null when signed out or unreadable.</summary>
    public string? SignedInEmail
    {
        get
        {
            try { return AccountProfile.Read(ConfigPath)?.Email; }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { return null; }
        }
    }

    /// <summary>The other member this folder is signed in as (M.ORCH-T1896): <see cref="SignedInEmail"/> when it isn't the folder's
    /// own <see cref="Email"/>, else null. Its meter (<see cref="UsagePath"/>) then reads that member's 5-hour and week, not this
    /// one's, and a CLI on it spends that member's allowance: its usage is "borrowed", never shown or paced as this login's.</summary>
    public string? BorrowedFrom => SignedIn && SignedInEmail is { } signed && !signed.Equals(Email, StringComparison.OrdinalIgnoreCase) ? signed : null;

    /// <summary>The plan of the login here as its sign-in names it ("Ultra", "Team · Max"); null when signed out, unreadable or
    /// unnamed (M.ORCH-T1751).</summary>
    public string? SignedInPlan
    {
        get
        {
            try { return AccountProfile.Read(ConfigPath)?.Plan; }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { return null; }
        }
    }
}

/// <summary>
/// The per-CLI Claude logins (M.ORCH-T559), one <see cref="AccountDir"/> per email under <see cref="Root"/>. A CLI started with
/// <c>--account &lt;email&gt;</c> runs under that folder (<see cref="EnvVar"/>); without it a CLI uses the global login
/// (<c>~/.claude</c>) exactly as before. <see cref="Prepare"/> makes the folder a working Claude home: what's yours and not the
/// account's (commands, skills, agents, hooks, plugins, transcripts) is linked to <c>~/.claude</c>, the settings are
/// <c>~/.claude</c>'s with the account's own statusline, and <c>.claude.json</c> starts as yours without the sign-in.
/// </summary>
public sealed class AccountDirs(string? root = null, string? claudeDir = null, string? homeConfigPath = null)
{
    /// <summary>What tells Claude Code where its config lives.</summary>
    public const string EnvVar = "CLAUDE_CONFIG_DIR";

    /// <summary>The statusline name constant.</summary>
    public const string StatuslineName = "statusline.ps1";

    private static string UserProfile => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    /// <summary>Where every account's folder is kept: <c>%USERPROFILE%\.claude-accounts</c>.</summary>
    public string Root { get; } = root ?? Path.Combine(UserProfile, ".claude-accounts");

    /// <summary>The global login's folder, <c>~/.claude</c>, where the shared parts are linked to.</summary>
    public string ClaudeDir { get; } = claudeDir ?? Setup.AgentGate.ClaudeDir;

    /// <summary>The global login's <c>~/.claude.json</c>, which a new account's starts from.</summary>
    public string HomeConfigPath { get; } = homeConfigPath ?? Path.Combine(UserProfile, ".claude.json");

    /// <summary>The parts of <c>~/.claude</c> every login shares, linked (a junction) rather than copied. <c>projects</c> is the
    /// transcripts: shared, so the dashboard, tickets and agent board see every CLI's work whichever account it spends.</summary>
    public static readonly IReadOnlyList<string> SharedDirectories = ["commands", "skills", "agents", "hooks", "plugins", "projects"];

    /// <summary>Copied each time it's prepared (a file link needs elevation on Windows).</summary>
    public static readonly IReadOnlyList<string> SharedFiles = ["CLAUDE.md"];

    /// <summary>A hook that only reads the global login's pace: a CLI on its own login gets its account's from the context gate instead.</summary>
    internal const string GlobalOnlyHook = "rl-check.py";

    /// <summary>The email as its folder is named (trimmed, lower case); null when it isn't one, or can't name a folder.</summary>
    public static string? Normalize(string? email)
    {
        var e = email?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(e)) return null;
        var at = e.IndexOf('@');
        if (at <= 0 || at != e.LastIndexOf('@') || at == e.Length - 1) return null;
        if (e.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || e.Contains(' ') || e.EndsWith('.')) return null;
        return e;
    }

    /// <summary>The folder for <paramref name="email"/> (made or not); throws <see cref="ArgumentException"/> when it isn't an email.</summary>
    public AccountDir For(string email) =>
        Normalize(email) is { } e ? new AccountDir(e, Path.Combine(Root, e)) : throw new ArgumentException($"\"{email}\" isn't an email address.", nameof(email));

    /// <summary>Every account folder there is, signed in or not, by email.</summary>
    public IReadOnlyList<AccountDir> All()
    {
        if (!System.IO.Directory.Exists(Root)) return [];
        return [.. System.IO.Directory.EnumerateDirectories(Root)
            .Select(Path.GetFileName)
            .OfType<string>()
            .Where(n => Normalize(n) == n)
            .Order(StringComparer.Ordinal)
            .Select(n => new AccountDir(n, Path.Combine(Root, n)))];
    }

    /// <summary>The accounts a CLI can run on now: a Claude login is signed in to in its folder, as the folder's own member. A
    /// folder signed in as another member (<see cref="AccountDir.BorrowedFrom"/>, M.ORCH-T1896) is left out, so no pick, move or
    /// rebalance puts work on it: it would spend that other member's allowance. A disabled member's (<see cref="Disabled"/>,
    /// M.ORCH-T1952) is left out the same way: it's never picked for work. So is a locked one's (<see cref="Locked"/>,
    /// M.ORCH-T2108): no pick, auto-pick or rebalance move ever lands on a locked login.</summary>
    public IReadOnlyList<AccountDir> SignedIn()
    {
        var off = DisabledNow();
        var locked = LockedNow();
        return [.. All().Where(a => a.SignedIn && a.BorrowedFrom is null && !off.Contains(a.Email) && !locked.Contains(a.Email))];
    }

    /// <summary>The disabled members' emails (M.ORCH-T1952, <see cref="Budget.MemberRoster.Disabled"/>): the live database's for
    /// the real <c>~/.claude-accounts</c>, none for a folder given (a test's), unless set.</summary>
    public Func<IReadOnlySet<string>>? Disabled { get; init; } = root is null ? Budget.MemberRoster.LiveDisabled : null;

    /// <summary><see cref="Disabled"/> read now; none when unset.</summary>
    public IReadOnlySet<string> DisabledNow() => Disabled?.Invoke() ?? EmptySet;

    /// <summary>The locked logins' emails (M.ORCH-T2108, <see cref="Budget.LoginLocks.LiveLocked"/>): the live database's for the
    /// real <c>~/.claude-accounts</c>, none for a folder given (a test's), unless set.</summary>
    public Func<IReadOnlySet<string>>? Locked { get; init; } = root is null ? Budget.LoginLocks.LiveLocked : null;

    /// <summary><see cref="Locked"/> read now; none when unset.</summary>
    public IReadOnlySet<string> LockedNow() => Locked?.Invoke() ?? EmptySet;

    private static readonly IReadOnlySet<string> EmptySet = new HashSet<string>();

    /// <summary>Whether the global login (<see cref="HomeConfigPath"/>) is signed in to a Claude account, not the API key.</summary>
    public bool GlobalSignedIn
    {
        get
        {
            try { return ClaudeAccount.Current(HomeConfigPath) is { } a && a != ClaudeAccount.ApiKey; }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { return false; }
        }
    }

    /// <summary>
    /// Whether the global login is signed out (M.ORCH-T1821): <see cref="HomeConfigPath"/> holds no Claude login and no API
    /// key, as <see cref="SignOutGlobal"/> leaves it. Unlike <see cref="GlobalSignedIn"/>, billing on the API key is not
    /// signed out (the global tab stays usable then), and a read that fails counts as signed in, so a torn write never grays
    /// the global login's tab. Read again only when the file's write time or length changed: the dashboard asks every tick.
    /// </summary>
    public bool GlobalSignedOut
    {
        get
        {
            try
            {
                var info = new FileInfo(HomeConfigPath);
                var stamp = info.Exists ? (info.LastWriteTimeUtc, info.Length) : (default(DateTime), -1L);
                lock (GlobalSignedOutLock)
                    if (globalSignedOutRead is { } cached && cached.Path == HomeConfigPath && cached.Stamp == stamp) return cached.SignedOut;
                var signedOut = ClaudeAccount.Current(HomeConfigPath) is null;
                lock (GlobalSignedOutLock) globalSignedOutRead = (HomeConfigPath, stamp, signedOut);
                return signedOut;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { return false; }
        }
    }

    private static readonly object GlobalSignedOutLock = new();
    private static (string Path, (DateTime, long) Stamp, bool SignedOut)? globalSignedOutRead;

    /// <summary>Whether any Claude account is signed in here at all, the global login or one of its own (M.ORCH-T1545), locked
    /// or disabled ones included: locking every login isn't a first run.</summary>
    public bool AnySignedIn => GlobalSignedIn || All().Any(a => a.SignedIn && a.BorrowedFrom is null);

    /// <summary>The account folder <paramref name="configDir"/> (a <c>CLAUDE_CONFIG_DIR</c>) is; null when it's unset, the global
    /// <c>~/.claude</c>, or anything that isn't one of these.</summary>
    public AccountDir? Of(string? configDir)
    {
        if (string.IsNullOrWhiteSpace(configDir)) return null;
        string full, parent;
        try
        {
            full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(configDir));
            parent = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Root));
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException) { return null; }
        if (!string.Equals(Path.GetDirectoryName(full), parent, StringComparison.OrdinalIgnoreCase)) return null;
        return Normalize(Path.GetFileName(full)) is { } e ? new AccountDir(e, Path.Combine(Root, e)) : null;
    }

    /// <summary>The account this process runs on (its own <see cref="EnvVar"/>); null on the global login.</summary>
    public AccountDir? Current() => Of(Environment.GetEnvironmentVariable(EnvVar));

    /// <summary>
    /// Makes (or brings up to date) <paramref name="account"/>'s folder as a Claude home, and returns what it did for anything
    /// the user should hear about. Safe to run every time a CLI starts on it: nothing of the account's own is replaced.
    /// </summary>
    public IReadOnlyList<string> Prepare(AccountDir account)
    {
        var lines = new List<string>();
        System.IO.Directory.CreateDirectory(account.Directory);
        System.IO.Directory.CreateDirectory(account.MarksDirectory);
        foreach (var name in SharedDirectories)
            Link(Path.Combine(account.Directory, name), Path.Combine(ClaudeDir, name), create: name == "projects", lines);
        foreach (var name in SharedFiles)
        {
            var from = Path.Combine(ClaudeDir, name);
            var to = Path.Combine(account.Directory, name);
            try { if (File.Exists(from) && (!File.Exists(to) || File.GetLastWriteTimeUtc(from) != File.GetLastWriteTimeUtc(to))) File.Copy(from, to, overwrite: true); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { lines.Add($"couldn't copy {name}: {ex.Message}"); }
        }
        var shared = WriteSettings(account);
        WriteIfChanged(account.StatuslinePath, StatuslineScript(account, shared));
        if (!File.Exists(account.ConfigPath) && SeedConfig() is { } seed)
        {
            WriteIfChanged(account.ConfigPath, seed);
            lines.Add($"{account.Email}'s folder starts from your ~/.claude.json (trusted folders, settings) without its sign-in.");
        }
        return lines;
    }

    /// <summary>Writes the account's settings.json: <c>~/.claude</c>'s (its hooks, permissions, plugins), with the statusline
    /// that records this account's meter in its own folder and without hooks that read only the global login's pace. Returns
    /// the shared statusline's command, for the account's to hand on to.</summary>
    private string? WriteSettings(AccountDir account)
    {
        JsonObject settings;
        try
        {
            var path = Path.Combine(ClaudeDir, "settings.json");
            settings = File.Exists(path) && JsonNode.Parse(File.ReadAllText(path)) is JsonObject o ? o : new JsonObject();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { settings = new JsonObject(); }
        var statusLine = settings["statusLine"] as JsonObject;
        var shared = statusLine?["command"] is JsonValue c && c.TryGetValue<string>(out var command) ? command : null;
        var own = new JsonObject
        {
            ["type"] = "command",
            ["command"] = $"powershell -NoProfile -ExecutionPolicy Bypass -File \"{account.StatuslinePath.Replace('\\', '/')}\"",
        };
        if (statusLine?["refreshInterval"] is JsonNode refresh) own["refreshInterval"] = refresh.DeepClone();
        settings["statusLine"] = own;
        if (settings["hooks"] is JsonObject hooks)
            foreach (var (_, groups) in hooks)
                if (groups is JsonArray list)
                    foreach (var group in list.OfType<JsonObject>())
                        if (group["hooks"] is JsonArray inner)
                            foreach (var hook in inner.OfType<JsonObject>().Where(IsGlobalOnly).ToList())
                                inner.Remove(hook);
        ApplyOverrides(settings, account);
        WriteIfChanged(account.SettingsPath, settings.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        return shared;

        static bool IsGlobalOnly(JsonObject hook) =>
            hook["command"] is JsonValue v && v.TryGetValue<string>(out var text) && text.Contains(GlobalOnlyHook, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The file in a login's folder whose top-level keys replace the derived settings' (M.ORCH-T1860).</summary>
    public const string OverridesName = "settings-overrides.json";

    /// <summary>
    /// Lays <paramref name="account"/>'s own <see cref="OverridesName"/> over the settings derived from <c>~/.claude</c>
    /// (M.ORCH-T1860): each top-level key there replaces the derived one, and a null removes it. It lets one login differ from
    /// the rest, e.g. Jordan's turning off Remote Control so the CLIs started on it never reach his claude.ai/code list. An
    /// unreadable file is ignored, leaving the derived settings as they are.
    /// </summary>
    private static void ApplyOverrides(JsonObject settings, AccountDir account)
    {
        var path = Path.Combine(account.Directory, OverridesName);
        JsonObject overrides;
        try
        {
            if (!File.Exists(path) || JsonNode.Parse(File.ReadAllText(path)) is not JsonObject o) return;
            overrides = o;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { return; }
        foreach (var (key, value) in overrides)
            if (value is null) settings.Remove(key);
            else settings[key] = value.DeepClone();
    }

    /// <summary>The keys of a <c>.claude.json</c> that say who's signed in.</summary>
    private static readonly string[] SignInKeys = ["oauthAccount", "primaryApiKey", "customApiKeyResponses", "userID"];

    /// <summary>
    /// Signs <paramref name="account"/>'s folder out (M.ORCH-T610): its token (<c>.credentials.json</c>) goes, and its
    /// <c>.claude.json</c> forgets who was signed in, keeping everything else. Used when its sign-in landed on another account
    /// than the folder's, so no CLI started on it spends that other account's tokens; also what the dashboard's own "Log out"
    /// menu item runs for a per-CLI login (M.ORCH-T1821). Returns why it couldn't, or null.
    /// </summary>
    public static string? SignOut(AccountDir account)
    {
        try
        {
            if (File.Exists(account.CredentialsPath)) File.Delete(account.CredentialsPath);
            if (File.Exists(account.ConfigPath) && JsonNode.Parse(File.ReadAllText(account.ConfigPath)) is JsonObject config)
            {
                foreach (var key in SignInKeys) config.Remove(key);
                WriteIfChanged(account.ConfigPath, config.ToJsonString());
            }
            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { return ex.Message; }
    }

    /// <summary>
    /// As <see cref="SignOut(AccountDir)"/>, for the global login instead of a per-CLI one: its token is
    /// <paramref name="claudeDir"/>'s own <c>.credentials.json</c> (<c>~/.claude</c>), but its sign-in is
    /// <paramref name="homeConfigPath"/>'s (<c>~/.claude.json</c>, at the profile root, not under <c>~/.claude</c>) - a
    /// mismatched layout that is why the global login isn't one <see cref="AccountDir"/> to begin with. Returns why it
    /// couldn't, or null.
    /// </summary>
    public static string? SignOutGlobal(string claudeDir, string homeConfigPath)
    {
        try
        {
            var credentials = Path.Combine(claudeDir, ".credentials.json");
            if (File.Exists(credentials)) File.Delete(credentials);
            if (File.Exists(homeConfigPath) && JsonNode.Parse(File.ReadAllText(homeConfigPath)) is JsonObject config)
            {
                foreach (var key in SignInKeys) config.Remove(key);
                WriteIfChanged(homeConfigPath, config.ToJsonString());
            }
            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { return ex.Message; }
    }

    /// <summary><c>~/.claude.json</c> without who's signed in, so the account's folder keeps your trusted folders and settings but
    /// signs in on its own; null when there's none to start from.</summary>
    private string? SeedConfig()
    {
        try
        {
            if (!File.Exists(HomeConfigPath) || JsonNode.Parse(File.ReadAllText(HomeConfigPath)) is not JsonObject config) return null;
            foreach (var key in SignInKeys) config.Remove(key);
            return config.ToJsonString();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { return null; }
    }

    /// <summary>
    /// The account's statusline: records Claude's 5-hour and weekly percent for this account in its own
    /// <c>claude-usage.json</c> (only ever up within a window, as the shared statusline keeps <c>~/.claude</c>'s), then hands
    /// the rest of the status to the shared statusline without <c>rate_limits</c>, which would otherwise be written as the
    /// global login's meter, and tags the line with the account.
    /// </summary>
    internal static string StatuslineScript(AccountDir account, string? sharedCommand) => $$"""
        # Written by MindAttic.Orchestrator for {{account.Email}}'s own Claude login (M.ORCH-T559); edits are overwritten.
        $raw = [Console]::In.ReadToEnd()
        try { $j = $raw | ConvertFrom-Json } catch { $j = $null }
        $file = {{Ps(account.UsagePath)}}
        if ($j -and $j.rate_limits) {
          try {
            $new = $j.rate_limits.five_hour
            $stale = $false
            if ($new -and $null -ne $new.resets_at -and [double]$new.resets_at -le [DateTimeOffset]::UtcNow.ToUnixTimeSeconds()) { $stale = $true }
            if (-not $stale -and $new -and $null -ne $new.used_percentage -and (Test-Path -LiteralPath $file)) {
              try {
                $old = (Get-Content -LiteralPath $file -Raw | ConvertFrom-Json).rate_limits.five_hour
                $stale = $old -and "$($old.resets_at)" -eq "$($new.resets_at)" -and [double]$new.used_percentage -le [double]$old.used_percentage
              } catch { }
            }
            if (-not $stale) {
              $usage = [ordered]@{ rate_limits = $j.rate_limits; at = [DateTime]::UtcNow.ToString('yyyy-MM-ddTHH:mm:ssZ') } | ConvertTo-Json -Depth 5 -Compress
              [IO.File]::WriteAllText("$file.$PID.tmp", $usage)
              Move-Item -LiteralPath "$file.$PID.tmp" -Destination $file -Force
              # Said on the daemon's status inbox at once (M.ORCH-T892), so dashboards get the reading now rather than at its
              # slow look at the file. Best effort: anything that fails is skipped within ~50ms.
              try {
                $line = [ordered]@{
                  kind = {{Ps(Status.StatusInbox.UsageWritten)}}; v = 1; at = [DateTime]::UtcNow.ToString('yyyy-MM-ddTHH:mm:ssZ'); pid = $PID
                  payload = [ordered]@{ path = $file; email = {{Ps(account.Email)}} }
                } | ConvertTo-Json -Depth 3 -Compress
                $pipe = New-Object IO.Pipes.NamedPipeClientStream('.', {{Ps(Status.StatusStream.InboxPipeName)}}, [IO.Pipes.PipeDirection]::Out)
                try {
                  $pipe.Connect(50)
                  $bytes = (New-Object Text.UTF8Encoding($false)).GetBytes($line + "`n")
                  $pipe.Write($bytes, 0, $bytes.Length)
                  $pipe.Flush()
                } finally { $pipe.Dispose() }
              } catch { }
            }
          } catch { }
          try { $j.PSObject.Properties.Remove('rate_limits'); $raw = $j | ConvertTo-Json -Depth 20 -Compress } catch { }
        }
        $shared = {{Ps(sharedCommand ?? "")}}
        $out = ''
        if ($shared) {
          try {
            $psi = New-Object Diagnostics.ProcessStartInfo 'cmd.exe', ('/d /s /c "' + $shared + '"')
            $psi.UseShellExecute = $false
            $psi.RedirectStandardInput = $true
            $psi.RedirectStandardOutput = $true
            $p = [Diagnostics.Process]::Start($psi)
            $p.StandardInput.Write($raw)
            $p.StandardInput.Close()
            $out = $p.StandardOutput.ReadToEnd().TrimEnd()
            [void]$p.WaitForExit(10000)
          } catch { }
        }
        # The login's name (M.ORCH-T614), read each time so a rename shows at once: its mindattic-account.json's alias while
        # that's for this email, else the email's part before the @.
        $name = {{Ps(LoginLook.DefaultAlias(account.Email) ?? account.Email)}}
        try {
          $look = Get-Content -LiteralPath {{Ps(Path.Combine(account.Directory, LoginLook.FileName))}} -Raw -ErrorAction Stop | ConvertFrom-Json
          $for = "$($look.email)".Trim()
          if ((-not $for -or $for -eq {{Ps(account.Email)}}) -and "$($look.alias)".Trim()) { $name = "$($look.alias)".Trim() }
        } catch { }
        $tag = "$([char]27)[2m[$name]$([char]27)[0m"
        if ($out) {
          $lines = $out -split "`r?`n"
          $lines[0] = $lines[0] + '  ' + $tag
          $lines | ForEach-Object { Write-Output $_ }
        } else { Write-Output $tag }
        """.Replace("\r\n", "\n");

    /// <summary>A PowerShell single-quoted string of <paramref name="text"/>.</summary>
    internal static string Ps(string text)
    {
        // PowerShell treats U+2018-U+201B as single-quote delimiters too; doubling any of them stays inside the string.
        var sb = new StringBuilder("'");
        foreach (var c in text)
        {
            sb.Append(c);
            if (c == (char)0x27 || (c >= (char)0x2018 && c <= (char)0x201B)) sb.Append(c);
        }
        return sb.Append((char)0x27).ToString();
    }

    private static void WriteIfChanged(string path, string text)
    {
        if (File.Exists(path) && File.ReadAllText(path) == text) return;
        // A temp of its own: two Prepare calls at once (the rebalancer's move and the dashboard) shared one (bughunt 2026-10-27).
        var temp = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllText(temp, text, new UTF8Encoding(false));
            File.Move(temp, path, overwrite: true);
        }
        finally
        {
            try { File.Delete(temp); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
    }

    /// <summary>Links <paramref name="link"/> to <paramref name="target"/> with a junction (no elevation needed). Left alone when
    /// it's already a link, or a folder of the account's own with something in it (said, since it then isn't shared).</summary>
    private static void Link(string link, string target, bool create, List<string> lines)
    {
        if (!System.IO.Directory.Exists(target))
        {
            if (!create) return;
            System.IO.Directory.CreateDirectory(target);
        }
        var info = new DirectoryInfo(link);
        if (info.Exists)
        {
            if (info.Attributes.HasFlag(FileAttributes.ReparsePoint)) return;
            if (info.EnumerateFileSystemInfos().Any())
            {
                lines.Add($"{link} is a folder of its own, so it isn't shared with {target}.");
                return;
            }
            info.Delete();
        }
        var psi = new ProcessStartInfo("cmd.exe") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        if (!TryMklinkArguments(link, target, out var cmdLine))
        {
            lines.Add($"couldn't link {link} to {target}: the path has a character cmd.exe can't take safely.");
            return;
        }
        psi.Arguments = cmdLine;
        try
        {
            using var p = Process.Start(psi)!;
            var error = p.StandardError.ReadToEnd();
            p.StandardOutput.ReadToEnd();
            p.WaitForExit();
            if (p.ExitCode != 0) lines.Add($"couldn't link {link} to {target}: {error.Trim()}");
        }
        catch (System.ComponentModel.Win32Exception ex) { lines.Add($"couldn't link {link} to {target}: {ex.Message}"); }
    }

    /// <summary>The cmd.exe command line for <c>mklink /J</c>, with both paths quoted so &amp; ^ | ( ) in a folder name stay data. False
    /// when a path holds a quote, % or ! (cmd expands those even inside quotes).</summary>
    internal static bool TryMklinkArguments(string link, string target, out string arguments)
    {
        arguments = "";
        foreach (var c in link + target) if (c is '"' or '%' or '!' or '\r' or '\n') return false;
        arguments = $"/d /c \"mklink /J \"{link}\" \"{target}\"\"";
        return true;
    }
}
