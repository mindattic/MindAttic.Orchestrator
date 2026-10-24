using MindAttic.Orchestrator.Settings;
using MindAttic.Tickets.Log;

namespace MindAttic.Orchestrator.Scheduling;

/// <summary>
/// The model a ticket's subagent runs on (M.ORCH-T563), as <c>orchestrator next --claim</c> prints it beside the prompt so
/// <c>/allocate</c> starts that subagent on it: <see cref="Sonnet"/> for fixes, failed-test tickets and tightly specified
/// changes with a clear definition of done; <see cref="Opus"/> for features, design work, changes across many files and
/// anything that must be right the first time. An override in settings (<see cref="OrchestratorSettings.ModelOverrides"/>,
/// by ticket, then by repo) wins. A Sonnet ticket whose diff is big or touches risky files gets one Opus review of the diff
/// before it lands (<see cref="DiffReview"/>).
/// </summary>
public static class ModelChoice
{
    /// <summary>The Agent tool's model names, which is how the CLI is told which one to start.</summary>
    public const string Sonnet = "sonnet", Opus = "opus";

    /// <summary>The full model ids, for a headless <c>claude --model</c>.</summary>
    public const string SonnetId = "claude-sonnet-5-5", OpusId = "claude-opus-5-5";

    /// <summary>The model chosen and, in a few words, why.</summary>
    public sealed record Choice(string Model, string Why)
    {
        public bool IsSonnet => Model == Sonnet;
    }

    /// <summary>What an override may say: a model, or <c>auto</c> (no override).</summary>
    public const string Auto = "auto";

    /// <summary>Types that are tightly specified by nature: a fix, a test, a chore, docs.</summary>
    private static readonly string[] SonnetTypes = ["fix", "bug", "test", "tests", "chore", "docs", "doc"];

    // Said in a summary, these mark work Sonnet is right for whatever the type: a test that fails, a red build.
    private static readonly string[] FailedTest = ["failing test", "failed test", "test fails", "tests fail", "test failure", "test failed", "tests failed",
        "ci fail", "ci run fail", "build fails", "build failed", "red build", "broken build"];

    // Said in a summary, these mark work that must be right the first time, or reaches across many files: Opus.
    private static readonly string[] OpusSignals = ["design", "architect", "rewrite", "overhaul", "migrat", "security", "data loss", "irreversible",
        "right the first time", "must be right", "across many files", "across every", "across all", "every repo", "all repos", "many files",
        "cross-repo"];

    /// <summary>A typed or saved model ("Sonnet 5.5", "claude-opus-5-5", "opus", "auto") as <see cref="Sonnet"/>, <see cref="Opus"/>
    /// or <see cref="Auto"/>; null when it's none of them.</summary>
    public static string? Parse(string? text)
    {
        var t = text?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(t)) return null;
        if (t is Auto or "none" or "default") return Auto;
        return Family(t) is { } family and (Sonnet or Opus) ? family : null;
    }

    /// <summary>The family of a model id as transcripts name it (<c>claude-sonnet-5-5</c> → <c>sonnet</c>); null for none.</summary>
    public static string? Family(string? modelId)
    {
        if (string.IsNullOrWhiteSpace(modelId)) return null;
        var m = modelId.ToLowerInvariant();
        return m.Contains("opus") ? Opus : m.Contains("sonnet") ? Sonnet : m.Contains("haiku") ? "haiku" : m.Contains("fable") ? "fable" : null;
    }

    /// <summary>The model for <paramref name="ticket"/> of <paramref name="repo"/>.</summary>
    public static Choice For(Ticket ticket, string repo, OrchestratorSettings settings)
    {
        if (Override(settings, ticket.Id, repo) is { } over) return over;
        var summary = ticket.Summary.ToLowerInvariant();
        var type = ticket.Type.ToLowerInvariant();
        if (FailedTest.FirstOrDefault(summary.Contains) is not null) return new(Sonnet, "a failed test");
        if (OpusSignals.FirstOrDefault(summary.Contains) is { } signal)
            return new(Opus, $"its summary says \"{signal}\": it must be right the first time");
        if (SonnetTypes.Contains(type)) return new(Sonnet, type == "fix" || type == "bug" ? "a fix" : $"a {type} with a clear definition of done");
        return new(Opus, type == "feature" ? "a feature" : $"a {type}");
    }

    /// <summary>The override in settings for the ticket, else for its repo (a name or a wildcard like <c>GTFO.*</c>); null when none.</summary>
    internal static Choice? Override(OrchestratorSettings settings, string ticketId, string repo)
    {
        foreach (var (key, value) in settings.ModelOverrides.Where(kv => kv.Key.Equals(ticketId, StringComparison.OrdinalIgnoreCase)))
            if (Parse(value) is { } m and not Auto) return new(m, $"your override for {key}");
        // The exact repo first, then a wildcard naming it.
        foreach (var (key, value) in settings.ModelOverrides.OrderBy(kv => kv.Key.Contains('*') || kv.Key.Contains('?')))
            if (Allocate.Named([key], repo) && Parse(value) is { } m and not Auto) return new(m, $"your override for {key}");
        return null;
    }

    /// <summary>The line <c>orchestrator next</c> prints for a handout's model.</summary>
    public static string Line(Choice choice) =>
        $"model: {choice.Model} ({(choice.IsSonnet ? SonnetId : OpusId)}): {choice.Why}";
}
