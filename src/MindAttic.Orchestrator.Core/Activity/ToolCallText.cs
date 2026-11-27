using System.Text;
using System.Text.Json;
using MindAttic.Orchestrator.Term;

namespace MindAttic.Orchestrator.Activity;

/// <summary>A tool call in a few plain words, for "what is it doing right now": "Editing Daemon.cs", "Running dotnet".
/// Never a raw tool input (M.ORCH-T963): a shell command shows its description or else its program name only, never its
/// arguments, and every text goes through <see cref="SecretShapes.MaskActivity"/>, the shapes the gateway's masker and the
/// context gate's activity records share.</summary>
public static class ToolCallText
{
    /// <summary>The longest a described call runs, in characters; longer is cut with an ellipsis.</summary>
    public const int MaxLength = 70;

    /// <summary>Tool <paramref name="name"/>'s call with JSON input <paramref name="inputJson"/> in a few plain words, secrets masked.</summary>
    public static string Describe(string name, string inputJson)
    {
        JsonElement input = default;
        try { if (inputJson.Length > 0) input = JsonDocument.Parse(inputJson).RootElement.Clone(); }
        catch (JsonException) { }

        string? Str(string key) => input.ValueKind == JsonValueKind.Object && input.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
        string File() => (Str("file_path") ?? Str("notebook_path") ?? Str("path")) is { Length: > 0 } p ? Path.GetFileName(p.TrimEnd('/', '\\')) : "a file";

        var text = name switch
        {
            // A command's description is already a phrase ("Run the tests"); a bare command gets a verb and its program name
            // only: arguments carry tokens (curl -H "Authorization: ...", --api-key, env assignments, connection strings).
            "Bash" or "PowerShell" => Str("description") is { Length: > 0 } d ? FirstLine(d) : "Running " + (Program(Str("command")) ?? "a command"),
            "Edit" or "MultiEdit" or "NotebookEdit" => "Editing " + File(),
            "Write" => "Writing " + File(),
            "Read" => "Reading " + File(),
            "Grep" => $"Searching for '{Str("pattern")}'",
            "Glob" => $"Finding {Str("pattern")}",
            "Agent" or "Task" => "Starting a subagent: " + FirstLine(Str("description") ?? Str("subagent_type") ?? "work"),
            "WebFetch" => "Reading " + (Uri.TryCreate(Str("url"), UriKind.Absolute, out var u) ? u.Host : "a web page"),
            "WebSearch" => $"Searching the web for '{Str("query")}'",
            "Skill" => "Using /" + Str("skill"),
            "TodoWrite" or "TaskCreate" or "TaskUpdate" => "Updating its task list",
            _ when name.StartsWith("mcp__", StringComparison.Ordinal) => "Using " + name.Split("__").Last(),
            _ => "Using " + name,
        };
        return Trim(SecretShapes.MaskActivity(text));
    }

    /// <summary>The first line of a prompt or description, masked (<see cref="SecretShapes.MaskActivity"/>) and trimmed to
    /// <see cref="MaxLength"/>.</summary>
    public static string FirstLine(string text)
    {
        var line = text.Trim().Split('\n', 2)[0].Trim();
        return Trim(SecretShapes.MaskActivity(line));
    }

    /// <summary>The program a command line runs, its base name only and never an argument: the first word that isn't an
    /// environment assignment (<c>FOO=bar</c>), a call operator (<c>&amp;</c>) or a <c>cd x &amp;&amp;</c> prefix. Null when
    /// there is none.</summary>
    public static string? Program(string? command)
    {
        if (string.IsNullOrWhiteSpace(command)) return null;
        var words = Tokenize(command.Trim().Split('\n', 2)[0]);
        for (var i = 0; i < words.Count; i++)
        {
            var w = words[i].Trim('"', '\'', '(', ')', ';').TrimStart('&');
            if (w.Length == 0 || w.Contains('=')) continue;
            var name = w.Replace('\\', '/').TrimEnd('/');
            name = name[(name.LastIndexOf('/') + 1)..];
            if (name is "" or "sudo" or "env" or "time" or "nohup") continue;
            // `cd somewhere && dotnet build` runs dotnet.
            if (name is "cd" or "Set-Location" or "pushd")
            {
                while (i + 1 < words.Count)
                {
                    var next = words[++i];
                    if (next is "&&" or ";" or "||" || next.EndsWith(';')) break;
                }
                continue;
            }
            return Trim(SecretShapes.MaskActivity(name.Length <= 40 ? name : name[..39] + "…"));
        }
        return null;
    }

    /// <summary>Splits a command line on whitespace, but not inside a single- or double-quoted span (so <c>"C:\Program
    /// Files\dotnet\dotnet.exe"</c> stays one word); the quotes themselves are dropped.</summary>
    private static List<string> Tokenize(string line)
    {
        var words = new List<string>();
        var sb = new StringBuilder();
        char? quote = null;
        foreach (var c in line)
        {
            if (quote is { } q)
            {
                if (c == q) quote = null;
                else sb.Append(c);
                continue;
            }
            if (c is '"' or '\'') { quote = c; continue; }
            if (c is ' ' or '\t')
            {
                if (sb.Length > 0) { words.Add(sb.ToString()); sb.Clear(); }
                continue;
            }
            sb.Append(c);
        }
        if (sb.Length > 0) words.Add(sb.ToString());
        return words;
    }

    private static string Trim(string s) => s.Length <= MaxLength ? s : s[..(MaxLength - 1)].TrimEnd() + "…";
}
