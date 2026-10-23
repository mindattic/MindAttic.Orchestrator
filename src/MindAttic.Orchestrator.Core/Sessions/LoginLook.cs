using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using MindAttic.Orchestrator.Budget;

namespace MindAttic.Orchestrator.Sessions;

/// <summary>
/// How a Claude login's terminal tabs look (M.ORCH-T616): the Windows Terminal window they gather in, the initials badge
/// that starts their titles ("RD: Repo [Claude]") and their tab color. Read from <see cref="FileName"/> in the login's
/// folder (<c>alias</c>, <c>initials</c>, <c>tabColor</c>); without one, the email's part before the @ names the window and
/// its first two letters are the initials.
/// <para>The login's name (M.ORCH-T614): <see cref="Alias"/>, what the dashboard, the Accounts view, the agent cards and the
/// account's status line call it ("Jordan" for pdftransferjr@mindattic.com), defaulting to the email's part before the @.
/// The file belongs to an email, not a folder: it keeps the <c>email</c> it was written for, and once the folder's login is
/// someone else's (the global <c>~/.claude</c> after a <c>/login</c> as another account) what it says is ignored. A file
/// with no <c>email</c> is trusted only in a per-CLI login's folder, which is named for its email.</para>
/// </summary>
public sealed record LoginLook(string Window, string Initials, string TabColor)
{
    public const string FileName = "mindattic-account.json";

    /// <summary>The login's name as people read it ("Jordan", "Amy Reed"); <see cref="Window"/> is it made safe for <c>wt -w</c>.</summary>
    public string Alias { get; init; } = Window;

    private static readonly string[] Palette = ["#2E7D32", "#1565C0", "#AD1457", "#EF6C00", "#6A1B9A", "#00838F", "#C62828", "#558B2F"];

    /// <summary>The look of the login a tab runs under: <paramref name="configDir"/>'s per-CLI login, or the global one (null).</summary>
    public static LoginLook ForConfigDir(string? configDir)
    {
        if (configDir is null)
        {
            string? email = null;
            try { email = AccountProfile.Read(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude.json"))?.Email; }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { }
            return For(Setup.AgentGate.ClaudeDir, email);
        }
        return For(configDir, AccountDirs.Normalize(Path.GetFileName(Path.TrimEndingDirectorySeparator(configDir))));
    }

    /// <summary>The look kept in <paramref name="folder"/>, filled in from <paramref name="email"/> where it says nothing.</summary>
    public static LoginLook For(string folder, string? email) => Read(folder, email, out _);

    /// <summary>The file's own <c>initials</c> for <paramref name="email"/> (M.ORCH-T633, the Rename dialog's Prefix), as
    /// given; null when the badge is derived from the name instead.</summary>
    public static string? InitialsOverride(string folder, string? email)
    {
        Read(folder, email, out var initials);
        return initials;
    }

    private static LoginLook Read(string folder, string? email, out string? initials)
    {
        string? alias = null, color = null;
        initials = null;
        try
        {
            var path = Path.Combine(folder, FileName);
            if (File.Exists(path))
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(path));
                if (doc.RootElement.ValueKind == JsonValueKind.Object && Belongs(Text(doc.RootElement, "email"), folder, email))
                {
                    alias = Text(doc.RootElement, "alias");
                    initials = Text(doc.RootElement, "initials");
                    color = Text(doc.RootElement, "tabColor");
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { }

        var local = DefaultAlias(email);
        var name = alias ?? local ?? "claude";
        var badge = Clean(initials, 3) ?? Clean(Words(alias), 3) ?? Clean(alias, 2) ?? Clean(local, 2) ?? "CL";
        var tab = color is { Length: 7 } c && c[0] == '#' && c[1..].All(Uri.IsHexDigit) ? c.ToUpperInvariant() : Palette[Hash(email ?? folder) % Palette.Length];
        return new LoginLook(WindowName(name), badge, tab) { Alias = name };

        // "Amy Reed" -> "AR"; one word ("Jordan") gives its own first two letters instead.
        static string? Words(string? text) =>
            text?.Split(' ', StringSplitOptions.RemoveEmptyEntries) is { Length: > 1 } words ? string.Concat(words.Select(w => w[0])) : null;

        static string? Clean(string? text, int max) =>
            text is null ? null : new string(text.Where(char.IsLetterOrDigit).Take(max).ToArray()).ToUpperInvariant() is { Length: > 0 } s ? s : null;
    }

    /// <summary>What a login is called until it's given a name: its email's part before the @; null without an email.</summary>
    public static string? DefaultAlias(string? email) => email is { } e && e.IndexOf('@') is > 0 and var at ? e[..at] : null;

    /// <summary>Whether a file stamped <paramref name="stored"/> is for <paramref name="email"/>, the login in
    /// <paramref name="folder"/> now: the same email, or no email in the per-CLI login's folder named for it.</summary>
    private static bool Belongs(string? stored, string folder, string? email)
    {
        if (email is null) return false;
        if (stored is not null) return string.Equals(stored, email, StringComparison.OrdinalIgnoreCase);
        return string.Equals(AccountDirs.Normalize(Path.GetFileName(Path.TrimEndingDirectorySeparator(folder))), email, StringComparison.OrdinalIgnoreCase);
    }

    private static string? Text(JsonElement e, string key) =>
        e.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String && v.GetString() is { } s && s.Trim().Length > 0 ? s.Trim() : null;

    /// <summary>
    /// Names the login in <paramref name="folder"/>, signed in as <paramref name="email"/>, <paramref name="alias"/> (blank goes
    /// back to the email's default) in its <see cref="FileName"/>: the file is stamped with the email and keeps anything else it
    /// holds, but drops another email's look, and the initials when the name changes (they follow the name). Returns the name.
    /// <para><paramref name="initials"/> (M.ORCH-T633): null leaves them as just said; given, it's the badge from now on
    /// (blank: derived from the name again).</para>
    /// </summary>
    public static string SetAlias(string folder, string email, string? alias, string? initials = null)
    {
        var e = email.Trim().ToLowerInvariant();
        var path = Path.Combine(folder, FileName);
        JsonObject file;
        try { file = File.Exists(path) && JsonNode.Parse(File.ReadAllText(path)) is JsonObject o ? o : []; }
        catch (JsonException) { file = []; }
        var stored = file["email"] is JsonValue sv && sv.TryGetValue<string>(out var se) && se.Trim().Length > 0 ? se.Trim() : null;
        var was = For(folder, e).Alias;
        var name = alias?.Trim() is { Length: > 0 } a ? a : null;
        if (!Belongs(stored, folder, e))
        {
            file.Remove("alias"); file.Remove("initials"); file.Remove("tabColor");
        }
        else if (!string.Equals(name ?? DefaultAlias(e), was, StringComparison.Ordinal))
            file.Remove("initials");
        file["email"] = e;
        if (name is null) file.Remove("alias"); else file["alias"] = name;
        if (initials is not null)
        {
            if (initials.Trim() is { Length: > 0 } i) file["initials"] = i; else file.Remove("initials");
        }
        Directory.CreateDirectory(folder);
        var temp = path + ".tmp";
        File.WriteAllText(temp, file.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));
        File.Move(temp, path, overwrite: true);
        return For(folder, e).Alias;
    }

    /// <summary>
    /// Names <paramref name="email"/>'s logins <paramref name="alias"/> (blank: back to the default) in each folder that's its
    /// login: its per-CLI one under <see cref="AccountDirs.Root"/> when it has one, and the global login when that's signed in
    /// as it. Returns the folders written; none when the email has no login on this machine.
    /// </summary>
    public static IReadOnlyList<string> SetAlias(AccountDirs dirs, string email, string? alias, string? initials = null)
    {
        var e = AccountDirs.Normalize(email) ?? throw new ArgumentException($"\"{email}\" isn't an email address.", nameof(email));
        var folders = new List<string>();
        var own = dirs.For(e);
        if (Directory.Exists(own.Directory)) folders.Add(own.Directory);
        if (GlobalEmail(dirs) is { } g && string.Equals(g, e, StringComparison.OrdinalIgnoreCase)) folders.Add(dirs.ClaudeDir);
        foreach (var folder in folders) SetAlias(folder, e, alias, initials);
        return folders;
    }

    /// <summary>
    /// The one rename the Accounts view and <c>orchestrator accounts alias</c> share (M.ORCH-T698): names <paramref name="email"/>'s
    /// logins <paramref name="alias"/> (blank: back to the default) with the badge <paramref name="initials"/> (null leaves it) in
    /// every folder that's its login and in <paramref name="folder"/>, and, when <paramref name="db"/> holds the email as a member,
    /// keeps the member's name in step (blank clears it, back to the alias). Returns the folders written.
    /// </summary>
    public static IReadOnlyList<string> Rename(AccountDirs dirs, string folder, string email, string? alias, string? initials = null,
        Settings.OrchestratorDb? db = null)
    {
        var written = SetAlias(dirs, email, alias, initials).ToList();
        if (!written.Any(f => string.Equals(Path.GetFullPath(f), Path.GetFullPath(folder), StringComparison.OrdinalIgnoreCase)))
        {
            SetAlias(folder, email, alias, initials);
            written.Add(folder);
        }
        if (db is not null && MemberRoster.Find(db, email) is not null)
            MemberRoster.Set(db, email, alias?.Trim() ?? "", null, null, null, DateTime.UtcNow);
        return written;
    }

    /// <summary>The email signed in to the global login (<see cref="AccountDirs.HomeConfigPath"/>); null when unknown.</summary>
    public static string? GlobalEmail(AccountDirs dirs)
    {
        try { return AccountProfile.Read(dirs.HomeConfigPath)?.Email; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { return null; }
    }

    /// <summary>A name <c>wt -w</c> takes as a window's name: a number is a window id, and "new"/"last" mean something else.</summary>
    internal static string WindowName(string name)
    {
        var safe = new string(name.Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' or '.' ? c : '-').ToArray()).Trim('-');
        if (safe.Length == 0 || safe.All(char.IsDigit) || safe.Equals("new", StringComparison.OrdinalIgnoreCase) || safe.Equals("last", StringComparison.OrdinalIgnoreCase))
            safe = "claude-" + safe;
        return safe.TrimEnd('-');
    }

    // FNV-1a: string.GetHashCode differs per process, and a login's color must not.
    private static int Hash(string text)
    {
        var h = 2166136261u;
        foreach (var ch in text.ToLowerInvariant()) { h ^= ch; h *= 16777619u; }
        return (int)(h % int.MaxValue);
    }
}
