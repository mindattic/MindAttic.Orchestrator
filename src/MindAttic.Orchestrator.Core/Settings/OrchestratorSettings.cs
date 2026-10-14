using System.Text.Json;
using System.Text.Json.Serialization;

namespace MindAttic.Orchestrator.Settings;

/// <summary>What you tell the orchestrator. Everything has a working default.</summary>
public sealed record OrchestratorSettings
{
    /// <summary>Uncached tokens (input + cache write + output) allowed per 5-hour usage block.
    /// Null measures blocks against your own peak block in the last 7 days instead.</summary>
    public long? BlockBudget { get; init; }

    /// <summary>Uncached tokens allowed per week. Null means no weekly line.</summary>
    public long? WeeklyBudget { get; init; }

    /// <summary>When the week resets, as "Monday 09:00" local time (see <see cref="Budget.WeeklyReset"/>).
    /// Null means the week is a rolling 7 days.</summary>
    public string? WeeklyReset { get; init; }

    /// <summary>Percent of a budget at which the dashboard warns and the scheduler stops proposing.</summary>
    public int WarnPercent { get; init; } = 80;

    /// <summary>Sessions the scheduler will fill up to (working + waiting both occupy a slot).</summary>
    public int MaxConcurrentSessions { get; init; } = 4;

    /// <summary>Off by default: proposals wait for a click. On: the scheduler opens tabs itself
    /// (prompt pre-filled, never submitted) when a slot is free and the budget allows.</summary>
    public bool AutoDispatch { get; init; }

    public string DefaultProvider { get; init; } = "Claude";

    /// <summary>Worktrees for concurrent tickets live here, outside the workspace root so they never
    /// show up as repos of their own.</summary>
    public string WorktreeRoot { get; init; } = @"D:\Projects\MindAttic.worktrees";

    /// <summary>Manual priority per ticket ID; higher runs sooner. Missing means 0.</summary>
    public Dictionary<string, int> Priorities { get; init; } = new(StringComparer.Ordinal);
}

/// <summary>A ticket the orchestrator has handed to a session.</summary>
public sealed record Assignment(string TicketId, string Repo, string Directory, string Provider, DateTime StartedUtc, bool Worktree);

/// <summary>Small JSON files under <c>%APPDATA%\MindAttic\MindAttic.Orchestrator</c>, written atomically.</summary>
public sealed class JsonStore(string? directory = null)
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public string Directory { get; } = directory ?? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MindAttic", "MindAttic.Orchestrator");

    public T Load<T>(string name) where T : new()
    {
        var path = PathOf(name);
        if (!File.Exists(path)) return new T();
        try { return JsonSerializer.Deserialize<T>(File.ReadAllText(path), Options) ?? new T(); }
        catch (JsonException) { return new T(); }
        catch (IOException) { return new T(); }
    }

    public void Save<T>(string name, T value)
    {
        System.IO.Directory.CreateDirectory(Directory);
        var path = PathOf(name);
        var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(value, Options));
        File.Move(temp, path, overwrite: true);
    }

    public OrchestratorSettings LoadSettings() => Load<OrchestratorSettings>("settings");
    public void SaveSettings(OrchestratorSettings s) => Save("settings", s);
    public List<Assignment> LoadAssignments() => Load<List<Assignment>>("assignments");
    public void SaveAssignments(List<Assignment> a) => Save("assignments", a);

    private string PathOf(string name) => Path.Combine(Directory, name + ".json");
}
