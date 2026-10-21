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
    public string ConfigPath => Path.Combine(Directory, ".claude.json");
    public string CredentialsPath => Path.Combine(Directory, ".credentials.json");
    public string SettingsPath => Path.Combine(Directory, "settings.json");

    /// <summary>The account's pace, as the daemon writes it for the hooks of the CLIs on this login.</summary>
    public string StatusPath => Path.Combine(Directory, "rl-status.json");

    /// <summary>Claude's own 5-hour and weekly percent for this account (its statusline's, or the daemon's meter probe's).</summary>
    public string UsagePath => Path.Combine(Directory, "claude-usage.json");

    /// <summary>Where this account's subagents are marked (<c>subagents</c>, slots and wants), as the hooks' SUBAGENT_MARKS.</summary>
    public string MarksDirectory => Path.Combine(Directory, "session-context");

    /// <summary>A week of Claude's readings for this account, which size its 5-hour limit and grade its ledger windows.</summary>
    public string SamplesPath => Path.Combine(Directory, "usage-samples.json");

    public string StatuslinePath => Path.Combine(Directory, AccountDirs.StatuslineName);

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

    /// <summary>The accounts a CLI can run on now: a Claude login is signed in to in its folder.</summary>
    public IReadOnlyList<AccountDir> SignedIn() => [.. All().Where(a => a.SignedIn)];

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
        WriteIfChanged(account.SettingsPath, settings.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        return shared;

        static bool IsGlobalOnly(JsonObject hook) =>
            hook["command"] is JsonValue v && v.TryGetValue<string>(out var text) && text.Contains(GlobalOnlyHook, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary><c>~/.claude.json</c> without who's signed in, so the account's folder keeps your trusted folders and settings but
    /// signs in on its own; null when there's none to start from.</summary>
    private string? SeedConfig()
    {
        try
        {
            if (!File.Exists(HomeConfigPath) || JsonNode.Parse(File.ReadAllText(HomeConfigPath)) is not JsonObject config) return null;
            foreach (var key in new[] { "oauthAccount", "primaryApiKey", "customApiKeyResponses", "userID" }) config.Remove(key);
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
        $tag = "$([char]27)[2m[{{account.Email}}]$([char]27)[0m"
        if ($out) {
          $lines = $out -split "`r?`n"
          $lines[0] = $lines[0] + '  ' + $tag
          $lines | ForEach-Object { Write-Output $_ }
        } else { Write-Output $tag }
        """.Replace("\r\n", "\n");

    /// <summary>A PowerShell single-quoted string of <paramref name="text"/>.</summary>
    internal static string Ps(string text) => "'" + text.Replace("'", "''") + "'";

    private static void WriteIfChanged(string path, string text)
    {
        if (File.Exists(path) && File.ReadAllText(path) == text) return;
        var temp = path + ".tmp";
        File.WriteAllText(temp, text, new UTF8Encoding(false));
        File.Move(temp, path, overwrite: true);
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
        foreach (var a in new[] { "/d", "/c", "mklink", "/J", link, target }) psi.ArgumentList.Add(a);
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
}
