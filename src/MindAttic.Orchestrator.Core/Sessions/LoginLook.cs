using System.Text.Json;
using MindAttic.Orchestrator.Budget;

namespace MindAttic.Orchestrator.Sessions;

/// <summary>
/// How a Claude login's terminal tabs look (M.ORCH-T616): the Windows Terminal window they gather in, the initials badge
/// that starts their titles ("[RD] Repo [Claude]") and their tab color. Read from <see cref="FileName"/> in the login's
/// folder (<c>alias</c>, <c>initials</c>, <c>tabColor</c>); without one, the email's part before the @ names the window and
/// its first two letters are the initials.
/// </summary>
public sealed record LoginLook(string Window, string Initials, string TabColor)
{
    public const string FileName = "mindattic-account.json";

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
    public static LoginLook For(string folder, string? email)
    {
        string? alias = null, initials = null, color = null;
        try
        {
            var path = Path.Combine(folder, FileName);
            if (File.Exists(path))
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(path));
                if (doc.RootElement.ValueKind == JsonValueKind.Object)
                {
                    alias = Text(doc.RootElement, "alias");
                    initials = Text(doc.RootElement, "initials");
                    color = Text(doc.RootElement, "tabColor");
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { }

        var local = email is { } e && e.IndexOf('@') is > 0 and var at ? e[..at] : null;
        var name = alias ?? local ?? "claude";
        var badge = Clean(initials, 3) ?? Clean(Words(alias), 3) ?? Clean(local, 2) ?? "CL";
        var tab = color is { Length: 7 } c && c[0] == '#' && c[1..].All(Uri.IsHexDigit) ? c.ToUpperInvariant() : Palette[Hash(email ?? folder) % Palette.Length];
        return new LoginLook(WindowName(name), badge, tab);

        static string? Text(JsonElement e, string key) =>
            e.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String && v.GetString() is { } s && s.Trim().Length > 0 ? s.Trim() : null;

        // "Amy Reed" -> "AR"; one word says nothing beyond its own first letters.
        static string? Words(string? text) =>
            text?.Split(' ', StringSplitOptions.RemoveEmptyEntries) is { Length: > 1 } words ? string.Concat(words.Select(w => w[0])) : null;

        static string? Clean(string? text, int max) =>
            text is null ? null : new string(text.Where(char.IsLetterOrDigit).Take(max).ToArray()).ToUpperInvariant() is { Length: > 0 } s ? s : null;
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
