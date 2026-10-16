using System.Text.Json;

namespace MindAttic.Orchestrator.Activity;

/// <summary>A tool call in a few plain words, for "what is it doing right now": "Editing Daemon.cs", "Running dotnet test".</summary>
public static class ToolCallText
{
    public const int MaxLength = 70;

    public static string Describe(string name, string inputJson)
    {
        JsonElement input = default;
        try { if (inputJson.Length > 0) input = JsonDocument.Parse(inputJson).RootElement.Clone(); }
        catch (JsonException) { }

        string? Str(string key) => input.ValueKind == JsonValueKind.Object && input.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
        string File() => (Str("file_path") ?? Str("notebook_path") ?? Str("path")) is { Length: > 0 } p ? Path.GetFileName(p.TrimEnd('/', '\\')) : "a file";

        var text = name switch
        {
            // A command's description is already a phrase ("Run the tests"); a bare command gets a verb.
            "Bash" or "PowerShell" => Str("description") is { Length: > 0 } d ? FirstLine(d) : "Running " + FirstLine(Str("command") ?? "a command"),
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
        return Trim(text);
    }

    /// <summary>The first line of a prompt or command, trimmed to <see cref="MaxLength"/>.</summary>
    public static string FirstLine(string text)
    {
        var line = text.Trim().Split('\n', 2)[0].Trim();
        return Trim(line);
    }

    private static string Trim(string s) => s.Length <= MaxLength ? s : s[..(MaxLength - 1)].TrimEnd() + "…";
}
