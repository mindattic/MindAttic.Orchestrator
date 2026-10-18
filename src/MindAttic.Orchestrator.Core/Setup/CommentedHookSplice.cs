using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace MindAttic.Orchestrator.Setup;

/// <summary>
/// Wires a hook entry into a JSON hook file that holds comments (Gemini's settings.json allows them) by editing its
/// text in place, so the user's comments and layout survive: a parse-and-rewrite drops every comment (M.ORCH-T442).
/// It only inserts members and swaps the values of a stale entry's command and timeout; any other shape, or a result
/// that doesn't come out equal to <see cref="AgentGate.AddHook"/>'s own merge, is null, and the file is left alone.
/// </summary>
internal static class CommentedHookSplice
{
    private static readonly JsonReaderOptions Allow = new() { CommentHandling = JsonCommentHandling.Allow };
    private static readonly JsonDocumentOptions Skip = new() { AllowDuplicateProperties = false, CommentHandling = JsonCommentHandling.Skip };

    /// <summary>True when <paramref name="text"/> holds a // or /* */ comment.</summary>
    internal static bool HasComments(string text)
    {
        try
        {
            var r = new Utf8JsonReader(Encoding.UTF8.GetBytes(text), Allow);
            while (r.Read()) if (r.TokenType == JsonTokenType.Comment) return true;
        }
        catch (JsonException) { }
        return false;
    }

    /// <summary><paramref name="text"/> with the hook entry spliced in, or null when that can't be done reliably;
    /// <paramref name="expected"/> is the parsed file after <see cref="AgentGate.AddHook"/>, which the result must equal.</summary>
    internal static string? Splice(string text, JsonObject expected, string hookEvent, string scriptName, string command, int timeout)
    {
        try
        {
            var bytes = Encoding.UTF8.GetBytes(text);
            var reader = new Utf8JsonReader(bytes, Allow);
            if (Next(ref reader) != JsonTokenType.StartObject) return null;
            var top = Read(ref reader);
            var edits = new Splicer(bytes, text.Contains("\r\n") ? "\r\n" : "\n", IndentUnit(text));

            var entry = new JsonObject
            {
                ["matcher"] = "",
                ["hooks"] = new JsonArray(new JsonObject { ["type"] = "command", ["command"] = command, ["timeout"] = timeout }),
            };
            switch (top["hooks"])
            {
                case null:
                    edits.Insert(top, "\"hooks\": " + Indented(new JsonObject { [hookEvent] = new JsonArray(entry) }));
                    break;
                case { Kind: JsonTokenType.StartObject } hooks:
                    switch (hooks[hookEvent])
                    {
                        case null: edits.Insert(hooks, JsonValue.Create(hookEvent).ToJsonString() + ": " + Indented(new JsonArray(entry))); break;
                        case { Kind: JsonTokenType.StartArray } entries:
                            var existing = entries.Members.Select(m => m.Value).Where(m => m.Kind == JsonTokenType.StartObject)
                                .SelectMany(m => m["hooks"] is { Kind: JsonTokenType.StartArray } hs ? hs.Members.Select(h => h.Value) : [])
                                .FirstOrDefault(h => h.Kind == JsonTokenType.StartObject && h["command"] is { Kind: JsonTokenType.String, Text: { } c }
                                    && c.Contains(scriptName, StringComparison.OrdinalIgnoreCase));
                            if (existing is null) { edits.Insert(entries, Indented(entry)); break; }
                            edits.Replace(existing["command"]!, JsonValue.Create(command).ToJsonString());
                            var number = timeout.ToString(CultureInfo.InvariantCulture);
                            if (existing["timeout"] is { } t) edits.Replace(t, number);
                            else edits.Insert(existing, "\"timeout\": " + number);
                            break;
                        default: return null;
                    }
                    break;
                default: return null;
            }

            var result = edits.Apply();
            // The splice has to read back as exactly what the plain merge would have written, comments aside.
            return JsonNode.Parse(result, documentOptions: Skip) is JsonObject back && JsonNode.DeepEquals(back, expected) ? result : null;
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or ArgumentException) { return null; }
    }

    /// <summary><paramref name="text"/> with every hook whose command runs one of <paramref name="scripts"/> cut out of it
    /// (and a matcher group, event or <c>hooks</c> object that leaves empty), the rest of the text as it was; null when that
    /// can't be done reliably. <paramref name="expected"/> is the parsed file after <see cref="AgentGateRemoval"/>'s own
    /// removal, which the result must equal (M.ORCH-T442).</summary>
    internal static string? Unsplice(string text, JsonObject expected, IReadOnlyList<string> scripts)
    {
        try
        {
            var bytes = Encoding.UTF8.GetBytes(text);
            var reader = new Utf8JsonReader(bytes, Allow);
            if (Next(ref reader) != JsonTokenType.StartObject) return null;
            var top = Read(ref reader);
            if (top["hooks"] is not { Kind: JsonTokenType.StartObject } hooks) return null;
            var edits = new Splicer(bytes, text.Contains("\r\n") ? "\r\n" : "\n", IndentUnit(text));

            bool Ours(Node hook) => hook.Kind == JsonTokenType.StartObject && hook["command"] is { Kind: JsonTokenType.String, Text: { } c }
                && scripts.Any(s => c.Contains(s, StringComparison.OrdinalIgnoreCase));

            var goneEvents = new HashSet<int>();
            for (var e = 0; e < hooks.Members.Count; e++)
            {
                if (hooks.Members[e].Value is not { Kind: JsonTokenType.StartArray } groups) continue;
                var goneGroups = new HashSet<int>();
                var here = false;
                for (var g = 0; g < groups.Members.Count; g++)
                {
                    if (groups.Members[g].Value is not { Kind: JsonTokenType.StartObject } group || group["hooks"] is not { Kind: JsonTokenType.StartArray } inner) continue;
                    var ours = Enumerable.Range(0, inner.Members.Count).Where(h => Ours(inner.Members[h].Value)).ToHashSet();
                    if (ours.Count == 0) continue;
                    here = true;
                    if (ours.Count == inner.Members.Count) goneGroups.Add(g);
                    else edits.Remove(inner, ours);
                }
                if (here && goneGroups.Count == groups.Members.Count) goneEvents.Add(e);
                else if (goneGroups.Count > 0) edits.Remove(groups, goneGroups);
            }
            if (goneEvents.Count == hooks.Members.Count && hooks.Members.Count > 0)
                edits.Remove(top, new HashSet<int> { top.Members.FindIndex(m => m.Name == "hooks") });
            else if (goneEvents.Count > 0) edits.Remove(hooks, goneEvents);

            var result = edits.Apply();
            return JsonNode.Parse(result, documentOptions: Skip) is JsonObject back && JsonNode.DeepEquals(back, expected) ? result : null;
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or ArgumentException) { return null; }
    }

    /// <summary>The hand-wiring instructions when <see cref="Splice"/> gave up.</summary>
    internal static string ByHand(string fileName, string hookEvent, string scriptName, string command, int timeout)
    {
        var entry = new JsonObject
        {
            ["matcher"] = "",
            ["hooks"] = new JsonArray(new JsonObject { ["type"] = "command", ["command"] = command, ["timeout"] = timeout }),
        };
        return $"left {fileName} untouched: it has comments, and {hookEvent} can't be wired in here without losing them, so add it by hand: " +
            $"under \"hooks\" -> \"{hookEvent}\" (a list), add this entry (or put it in place of one already running {scriptName}): {entry.ToJsonString()}";
    }

    private static string Indented(JsonNode node) => node.ToJsonString(new JsonSerializerOptions { WriteIndented = true });

    /// <summary>The file's own indent step (the first indented line's leading whitespace), else two spaces.</summary>
    private static string IndentUnit(string text) =>
        Regex.Match(text, @"^([ \t]+)\S", RegexOptions.Multiline) is { Success: true } m ? m.Groups[1].Value : "  ";

    private static JsonTokenType Next(ref Utf8JsonReader r)
    {
        do { if (!r.Read()) throw new JsonException("unexpected end"); } while (r.TokenType == JsonTokenType.Comment);
        return r.TokenType;
    }

    /// <summary>The value the reader stands on, with byte offsets: <c>Start</c> at its first byte, <c>End</c> just past its last.</summary>
    private static Node Read(ref Utf8JsonReader r)
    {
        var node = new Node { Kind = r.TokenType, Start = (int)r.TokenStartIndex };
        if (r.TokenType is JsonTokenType.StartObject or JsonTokenType.StartArray)
        {
            var close = r.TokenType == JsonTokenType.StartObject ? JsonTokenType.EndObject : JsonTokenType.EndArray;
            while (Next(ref r) != close)
            {
                var nameStart = (int)r.TokenStartIndex;
                string? name = null;
                if (r.TokenType == JsonTokenType.PropertyName) { name = r.GetString(); Next(ref r); }
                node.Members.Add((name, nameStart, Read(ref r)));
            }
            node.End = (int)r.TokenStartIndex + 1;
        }
        else
        {
            if (r.TokenType == JsonTokenType.String) node.Text = r.GetString();
            node.End = (int)r.TokenStartIndex + r.ValueSpan.Length + (r.TokenType == JsonTokenType.String ? 2 : 0);
        }
        return node;
    }

    private sealed class Node
    {
        public JsonTokenType Kind;
        public int Start, End;
        public string? Text;
        public readonly List<(string? Name, int NameStart, Node Value)> Members = [];
        public Node? this[string name] => Members.FirstOrDefault(m => m.Name == name).Value;
    }

    private sealed class Splicer(byte[] bytes, string newline, string unit)
    {
        private readonly List<(int At, int Remove, string Insert)> edits = [];

        public void Replace(Node value, string with) => edits.Add((value.Start, value.End - value.Start, with));

        /// <summary>Adds <paramref name="member"/> as the last member of <paramref name="container"/>, on its own line at the
        /// indent its siblings use.</summary>
        public void Insert(Node container, string member)
        {
            var (outer, _) = Indent(container.Start);
            var close = container.End - 1;
            var (closeIndent, closeOwnLine) = Indent(close);
            if (container.Members.Count == 0)
            {
                var inner = outer + unit;
                edits.Add((container.Start + 1, 0, newline + inner + Reindent(member, inner) + (closeOwnLine ? "" : newline + outer)));
                return;
            }
            var last = container.Members[^1];
            var (lastIndent, lastOwnLine) = Indent(last.NameStart);
            var indent = lastOwnLine ? lastIndent : outer + unit;
            edits.Add((last.Value.End, 0, ","));
            if (closeOwnLine) edits.Add((close - closeIndent.Length, 0, indent + Reindent(member, indent) + newline));
            else edits.Add((close, 0, newline + indent + Reindent(member, indent) + newline + outer));
        }

        /// <summary>Cuts the members of <paramref name="container"/> at <paramref name="gone"/> out, each with its comma, and
        /// with its whole line (and a comment trailing it there) when it stood on a line of its own.</summary>
        public void Remove(Node container, IReadOnlySet<int> gone)
        {
            if (gone.Count == 0) return;
            for (var i = 0; i < container.Members.Count; i++)
            {
                if (!gone.Contains(i)) continue;
                var (_, nameStart, value) = container.Members[i];
                var (indent, ownLine) = Indent(nameStart);
                var start = ownLine ? nameStart - indent.Length : nameStart;
                var comma = CommaAfter(value.End);
                var end = comma >= 0 ? comma + 1 : value.End;
                if (ownLine)
                {
                    var j = end;
                    while (j < bytes.Length && bytes[j] is (byte)' ' or (byte)'\t') j++;
                    if (j + 1 < bytes.Length && bytes[j] == (byte)'/' && bytes[j + 1] == (byte)'/')
                        while (j < bytes.Length && bytes[j] != (byte)'\n' && bytes[j] != (byte)'\r') j++;
                    if (j < bytes.Length && bytes[j] == (byte)'\r') j++;
                    if (j < bytes.Length && bytes[j] == (byte)'\n') end = j + 1;
                }
                edits.Add((start, end - start, ""));
            }
            // With the last member gone, the one now last mustn't keep its comma.
            if (gone.Contains(container.Members.Count - 1) && gone.Count < container.Members.Count)
            {
                var kept = container.Members[Enumerable.Range(0, container.Members.Count).Last(i => !gone.Contains(i))].Value;
                if (CommaAfter(kept.End) is var comma and >= 0) edits.Add((comma, 1, ""));
            }
        }

        /// <summary>Where the comma after a value ending at <paramref name="at"/> is (past whitespace and comments), or -1.</summary>
        private int CommaAfter(int at)
        {
            var i = at;
            while (i < bytes.Length)
            {
                if (bytes[i] is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n') i++;
                else if (bytes[i] == (byte)'/' && i + 1 < bytes.Length && bytes[i + 1] == (byte)'/')
                    while (i < bytes.Length && bytes[i] != (byte)'\n') i++;
                else if (bytes[i] == (byte)'/' && i + 1 < bytes.Length && bytes[i + 1] == (byte)'*')
                {
                    var close = bytes.AsSpan(i + 2).IndexOf("*/"u8);
                    if (close < 0) return -1;
                    i += close + 4;
                }
                else return bytes[i] == (byte)',' ? i : -1;
            }
            return -1;
        }

        /// <summary>The whitespace before <paramref name="at"/> on its line, and whether nothing else precedes it there.</summary>
        private (string Indent, bool OwnLine) Indent(int at)
        {
            var i = at;
            while (i > 0 && bytes[i - 1] is (byte)' ' or (byte)'\t') i--;
            var ownLine = i == 0 || bytes[i - 1] == (byte)'\n';
            return (Encoding.UTF8.GetString(bytes, i, at - i), ownLine);
        }

        /// <summary>Two-space-indented JSON re-indented under <paramref name="indent"/> with the file's own step and line ending.</summary>
        private string Reindent(string json, string indent) =>
            string.Join(newline, json.Split('\n').Select((line, n) =>
            {
                line = line.TrimEnd('\r');
                if (n == 0) return line;
                var spaces = line.Length - line.TrimStart(' ').Length;
                return indent + string.Concat(Enumerable.Repeat(unit, spaces / 2)) + line[spaces..];
            }));

        public string Apply()
        {
            var output = bytes.ToList();
            foreach (var (at, remove, insert) in edits.Select((e, n) => (e, n)).OrderByDescending(x => x.e.At).ThenByDescending(x => x.n).Select(x => x.e))
            {
                output.RemoveRange(at, remove);
                output.InsertRange(at, Encoding.UTF8.GetBytes(insert));
            }
            return Encoding.UTF8.GetString(output.ToArray());
        }
    }
}
