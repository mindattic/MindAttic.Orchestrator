using System.Text.RegularExpressions;

namespace MindAttic.Orchestrator.Setup;

/// <summary>
/// <c>MindAttic.Orchestrator.Desktop.exe --start &lt;repo&gt;</c> (M.ORCH-T767): the Tickets desktop's Repos tab Start link (M.TIX-T135)
/// asks the dashboard to start a CLI in a repo, as its own Start does. Only a plain repo name gets through here; the
/// dashboard then starts it only if the workspace has a repo of that name, and the CLI's folder and title come from the
/// workspace, never from this text.
/// </summary>
public static partial class StartRequest
{
    public const string Arg = "--start";

    /// <summary>The repo <paramref name="args"/> asks to start, or null when there's no <c>--start</c>, it names nothing, or what it
    /// names can't be a repo's folder name. <paramref name="refused"/> is that text, for saying why nothing started.</summary>
    public static string? Repo(IReadOnlyList<string> args, out string? refused)
    {
        refused = null;
        for (var i = 0; i < args.Count; i++)
        {
            if (!string.Equals(args[i], Arg, StringComparison.OrdinalIgnoreCase)) continue;
            var name = i + 1 < args.Count ? args[i + 1].Trim() : "";
            if (IsRepoName(name)) return name;
            refused = name;
            return null;
        }
        return null;
    }

    public static string? Repo(IReadOnlyList<string> args) => Repo(args, out _);

    /// <summary>Whether <paramref name="args"/> carry a <c>--start</c> at all, valid or not.</summary>
    public static bool Asked(IReadOnlyList<string> args) => args.Any(a => string.Equals(a, Arg, StringComparison.OrdinalIgnoreCase));

    /// <summary>What a later launch hands the running dashboard: <c>--start &lt;repo&gt;</c>, or nothing (it only comes forward).</summary>
    public static IReadOnlyList<string> HandOff(IReadOnlyList<string> args) => Repo(args) is { } repo ? [Arg, repo] : [];

    /// <summary>A repo's folder name: letters, digits, dots, dashes and underscores, not starting with a dot. No path,
    /// quote, space or shell character can get through.</summary>
    public static bool IsRepoName(string? name) => name is { Length: > 0 and <= 128 } && RepoName().IsMatch(name);

    [GeneratedRegex(@"^[A-Za-z0-9_-][A-Za-z0-9._-]*$")]
    private static partial Regex RepoName();
}
