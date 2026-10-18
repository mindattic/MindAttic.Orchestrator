using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MindAttic.Orchestrator.Billing;

/// <summary>The claude.ai login in <c>~/.claude.json</c> (<c>oauthAccount</c>, no <c>primaryApiKey</c>).</summary>
public sealed record ClaudeLogin(string AccountUuid, string? OrganizationUuid, string? Email, string? OrganizationName, string? Plan)
{
    /// <summary>The login as <see cref="Budget.ClaudeAccount"/> keys it: account and organization.</summary>
    public string Identity => OrganizationUuid is { } org ? $"{AccountUuid}/{org}" : AccountUuid;

    /// <summary>"Claude Team: pdftransferjr@mindattic.com (MindAttic)", as the context gate's [Billing] line names it.</summary>
    public string Describe() => $"{Plan ?? "Claude"}: {Email ?? AccountUuid}" + (OrganizationName is { } o ? $" ({o})" : "");
}

/// <summary>What Claude Code was told about a key (its last 20 characters in <c>customApiKeyResponses</c>).</summary>
public enum KeyChoice { None, Approved, Rejected }

/// <summary>
/// Claude Code's own record of which <c>ANTHROPIC_API_KEY</c> it may use: the last 20 characters of each key it was told
/// yes to (<c>customApiKeyResponses.approved</c>) or no to (<c>.rejected</c>) in <c>~/.claude.json</c>. An approved key
/// outranks the claude.ai login in a CLI started with it; a rejected one leaves the login in charge (M.ORCH-T434). The
/// billing switch (M.ORCH-T436) moves the Vault key's entry between the two lists.
/// <para>That file is Claude Code's, large and rewritten often, and holds keys that differ only in case (so PowerShell's
/// ConvertFrom-Json can't read it). Only the <c>customApiKeyResponses</c> value's text is changed; every other byte is kept.
/// The old file is kept beside it (<see cref="BackupSuffix"/>) and the new one is put in place whole, re-read just before
/// so a write Claude Code made meanwhile is never lost.</para>
/// </summary>
public static class ClaudeKeyChoice
{
    /// <summary>How many of a key's last characters Claude Code keeps.</summary>
    public const int TailLength = 20;

    /// <summary>The copy of <c>~/.claude.json</c> as it was before the last change made here.</summary>
    public const string BackupSuffix = ".billing-backup";

    public static string DefaultConfigPath { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude.json");

    /// <summary>The part of a key Claude Code records.</summary>
    public static string Tail(string key) => key.Length <= TailLength ? key : key[^TailLength..];

    /// <summary>A key as it may be shown: <c>sk-ant-...WXYZ</c>, never more than its last four characters.</summary>
    public static string Mask(string key) =>
        (key.StartsWith("sk-ant-", StringComparison.Ordinal) ? "sk-ant-" : "") + "..." + (key.Length > 8 ? key[^4..] : "");

    /// <summary>The claude.ai login in <paramref name="configPath"/>, and what it says about the key ending in <paramref name="tail"/>;
    /// (null, None) when the file is missing or isn't a JSON object.</summary>
    public static (ClaudeLogin? Login, KeyChoice Choice) Read(string configPath, string? tail)
    {
        try
        {
            if (!File.Exists(configPath)) return (null, KeyChoice.None);
            using var stream = new FileStream(configPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var doc = JsonDocument.Parse(stream);
            return Read(doc.RootElement, tail);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return (null, KeyChoice.None);
        }
    }

    internal static (ClaudeLogin? Login, KeyChoice Choice) Read(JsonElement root, string? tail)
    {
        if (root.ValueKind != JsonValueKind.Object) return (null, KeyChoice.None);
        ClaudeLogin? login = null;
        // Last of each name, as JSON.parse keeps (the file holds names that differ only in case, never the same twice).
        JsonElement? oauth = null, responses = null;
        var console = false;
        foreach (var p in root.EnumerateObject())
        {
            if (p.NameEquals("oauthAccount")) oauth = p.Value;
            else if (p.NameEquals("customApiKeyResponses")) responses = p.Value;
            else if (p.NameEquals("primaryApiKey")) console = p.Value.ValueKind == JsonValueKind.String && p.Value.GetString() is { Length: > 0 };
        }
        if (!console && oauth is { ValueKind: JsonValueKind.Object } o && Text(o, "accountUuid") is { } id)
            login = new ClaudeLogin(id, Text(o, "organizationUuid"), Text(o, "emailAddress"), Text(o, "organizationName"), PlanName(Text(o, "organizationType")));
        var choice = KeyChoice.None;
        if (tail is not null && responses is { ValueKind: JsonValueKind.Object } r)
        {
            if (Listed(r, "approved", tail)) choice = KeyChoice.Approved;
            else if (Listed(r, "rejected", tail)) choice = KeyChoice.Rejected;
        }
        return (login, choice);
    }

    /// <summary>The plan an <c>organizationType</c> names, as the context gate's <c>plan_name</c>: "claude_team" is Claude Team.</summary>
    internal static string? PlanName(string? kind) =>
        kind is { Length: > 7 } k && k.StartsWith("claude_", StringComparison.Ordinal)
            ? "Claude " + string.Join(' ', k[7..].Split('_', StringSplitOptions.RemoveEmptyEntries).Select(w => char.ToUpperInvariant(w[0]) + w[1..].ToLowerInvariant()))
            : null;

    private static string? Text(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String && v.GetString() is { Length: > 0 } s ? s : null;

    private static bool Listed(JsonElement responses, string list, string tail) =>
        responses.TryGetProperty(list, out var l) && l.ValueKind == JsonValueKind.Array &&
        l.EnumerateArray().Any(x => x.ValueKind == JsonValueKind.String && x.GetString() == tail);

    /// <summary>
    /// Puts the key ending in <paramref name="tail"/> in <c>customApiKeyResponses.approved</c> (<paramref name="approve"/>) or
    /// <c>.rejected</c>, out of the other list; true when the file changed, false when it already said so. A missing file is
    /// left missing (false); one that isn't a JSON object, or whose <c>customApiKeyResponses</c> isn't one, is left as it is
    /// and throws <see cref="InvalidDataException"/>.
    /// </summary>
    public static bool Set(string configPath, string tail, bool approve)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            if (!File.Exists(configPath)) return false;
            var raw = ReadBytes(configPath);
            var bom = raw.AsSpan().StartsWith(Utf8Bom);
            var text = Encoding.UTF8.GetString(raw, bom ? 3 : 0, raw.Length - (bom ? 3 : 0));
            if (Flip(text, tail, approve) is not { } next) return false;
            var tmp = $"{configPath}.{Environment.ProcessId}.tmp";
            try
            {
                var body = Encoding.UTF8.GetBytes(next);
                File.WriteAllBytes(tmp, bom ? [.. Utf8Bom, .. body] : body);
                if (!ReadBytes(configPath).AsSpan().SequenceEqual(raw)) continue; // Claude Code wrote it meanwhile: start over from its new text
                File.WriteAllBytes(configPath + BackupSuffix, raw);
                for (var i = 0; ; i++)
                {
                    try { File.Move(tmp, configPath, overwrite: true); return true; }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException && i < 20) { Thread.Sleep(50); }
                }
            }
            finally
            {
                try { if (File.Exists(tmp)) File.Delete(tmp); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            }
        }
        throw new IOException($"{configPath} kept changing while the key's choice was being saved; it's left as Claude Code last wrote it.");
    }

    private static ReadOnlySpan<byte> Utf8Bom => [0xEF, 0xBB, 0xBF];

    private static byte[] ReadBytes(string path)
    {
        for (var i = 0; ; i++)
        {
            try
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var copy = new MemoryStream();
                stream.CopyTo(copy);
                return copy.ToArray();
            }
            catch (IOException) when (i < 20) { Thread.Sleep(50); }
        }
    }

    /// <summary>
    /// <paramref name="text"/> with the tail moved to the list asked for, only the <c>customApiKeyResponses</c> value's text
    /// changed (added as the last member when there's none), laid out as the text it replaces was; null when nothing changes.
    /// </summary>
    internal static string? Flip(string text, string tail, bool approve)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        var reader = new Utf8JsonReader(bytes, new JsonReaderOptions { CommentHandling = JsonCommentHandling.Skip });
        (int Start, int End)? span = null;
        int? lastEnd = null, rootEnd = null;
        try
        {
            if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject) throw new InvalidDataException("~/.claude.json isn't a JSON object.");
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject && reader.CurrentDepth == 0) { rootEnd = (int)reader.TokenStartIndex; break; }
                if (reader.TokenType != JsonTokenType.PropertyName || reader.CurrentDepth != 1) continue;
                var mine = reader.ValueTextEquals("customApiKeyResponses");
                reader.Read();
                var start = (int)reader.TokenStartIndex;
                reader.Skip();
                var end = (int)reader.BytesConsumed;
                if (mine) span = (start, end);
                lastEnd = end;
            }
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("~/.claude.json isn't JSON it can read: " + ex.Message, ex);
        }
        if (rootEnd is null) throw new InvalidDataException("~/.claude.json isn't a whole JSON object.");

        if (span is { } s)
        {
            if (JsonNode.Parse(bytes.AsSpan(s.Start, s.End - s.Start)) is not JsonObject value)
                throw new InvalidDataException("customApiKeyResponses in ~/.claude.json isn't an object; it's left as it is.");
            if (!Move(value, tail, approve)) return null;
            var at = Encoding.UTF8.GetCharCount(bytes, 0, s.Start);
            var until = Encoding.UTF8.GetCharCount(bytes, 0, s.End);
            return text[..at] + DumpedLike(value, text, at, until) + text[until..];
        }

        // None yet: added as the last member, indented as the members before it.
        var fresh = new JsonObject();
        Move(fresh, tail, approve);
        var close = Encoding.UTF8.GetCharCount(bytes, 0, rootEnd.Value);
        var newline = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var indent = System.Text.RegularExpressions.Regex.Match(text, @"\{\r?\n([ \t]+)\S") is { Success: true } m ? m.Groups[1].Value : "";
        var member = $"\"customApiKeyResponses\": {Dumped(fresh, indent, indent.Length > 0 ? indent : "  ", newline, compact: indent.Length == 0)}";
        if (lastEnd is { } last)
        {
            var after = Encoding.UTF8.GetCharCount(bytes, 0, last);
            return text[..after] + "," + (indent.Length > 0 ? newline + indent : " ") + member + text[after..];
        }
        return text[..close] + (indent.Length > 0 ? newline + indent + member + newline : member) + text[close..];
    }

    /// <summary>Moves the tail into the list asked for and out of the other; false when it was already only there.</summary>
    private static bool Move(JsonObject value, string tail, bool approve)
    {
        var (into, outOf) = approve ? ("approved", "rejected") : ("rejected", "approved");
        var target = value[into] as JsonArray;
        var other = value[outOf] as JsonArray;
        bool Has(JsonArray? a) => a is not null && a.Any(x => x is JsonValue v && v.TryGetValue<string>(out var t) && t == tail);
        if (Has(target) && !Has(other)) return false;
        if (target is null) value[into] = target = [];
        if (!Has(target)) target.Add(tail);
        if (other is null) value[outOf] = other = [];
        foreach (var gone in other.Where(x => x is JsonValue v && v.TryGetValue<string>(out var t) && t == tail).ToList()) other.Remove(gone);
        return true;
    }

    /// <summary><paramref name="value"/> laid out as the text it replaces was: Claude Code's indent at that depth and its
    /// line endings, or on one line when it was.</summary>
    private static string DumpedLike(JsonObject value, string text, int start, int end)
    {
        var old = text[start..end];
        if (!old.Contains('\n')) return Dumped(value, "", "  ", "\n", compact: true);
        var newline = old.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var line = text[(text.LastIndexOf('\n', Math.Max(0, start - 1)) + 1)..start];
        var indent = line[..(line.Length - line.TrimStart(' ', '\t').Length)];
        var inner = System.Text.RegularExpressions.Regex.Match(old, @"\n([ \t]*)\S");
        var step = inner.Success && inner.Groups[1].Value.Length > indent.Length ? inner.Groups[1].Value[indent.Length..] : "  ";
        return Dumped(value, indent, step, newline, compact: false);
    }

    private static string Dumped(JsonObject value, string indent, string step, string newline, bool compact)
    {
        var options = new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, WriteIndented = !compact };
        if (!compact)
        {
            options.IndentCharacter = step.Length > 0 && step[0] == '\t' ? '\t' : ' ';
            options.IndentSize = Math.Max(1, step.Length);
            options.NewLine = newline;
        }
        var lines = value.ToJsonString(options).Split(newline);
        return lines[0] + string.Concat(lines.Skip(1).Select(l => newline + indent + l));
    }
}
