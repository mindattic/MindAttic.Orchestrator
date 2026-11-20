namespace MindAttic.Orchestrator.Sessions;

/// <summary>
/// The short name a workspace CLI tab is given for what it is doing (M.ORCH-T1817). A CLI opened on a repo is named for that
/// repo and keeps the name; one opened at the workspace root ("General", or the workspace folder's own name, "MindAttic")
/// says nothing about its work, so the dashboard renames it from what the Orchestrator already knows: the ticket it is
/// working, else its latest activity. No model is asked: the name is cut from text that is already there.
/// </summary>
public static class AutoTabName
{
    /// <summary>Words that carry no meaning in a tab's few characters ("Using" alone named tabs before M.ORCH-T1878).</summary>
    private static readonly HashSet<string> Filler = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "an", "the", "of", "to", "for", "and", "or", "in", "on", "at", "by", "with", "from", "into", "its", "it", "is", "are",
        "be", "that", "this", "as", "so", "then", "each", "every", "all", "own", "using", "mcp", "tool",
    };

    /// <summary>How many words every name has (M.ORCH-T1878): always exactly this many, so a tab says what it's doing.</summary>
    public const int Words = 3;

    /// <summary>The shortest a word is cut to when three don't fit the character limit.</summary>
    private const int MinWordChars = 3;

    /// <summary>Whether <paramref name="request"/> is a CLI opened at the workspace root rather than on a repo: it runs in
    /// <paramref name="workspaceRoot"/> itself and is not a Launcher menu. Decided by the folder, never by the tab's name.</summary>
    public static bool IsWorkspaceTab(PaneRequest request, string? workspaceRoot)
    {
        if (request.IsMenu || string.IsNullOrWhiteSpace(workspaceRoot) || string.IsNullOrWhiteSpace(request.Directory)) return false;
        return string.Equals(Folder(request.Directory), Folder(workspaceRoot), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Whether the tab may be auto-named: a workspace tab nobody has renamed by hand (no name of its own, or one this
    /// rule gave it earlier).</summary>
    public static bool IsEligible(PaneRequest request, string? workspaceRoot) =>
        IsWorkspaceTab(request, workspaceRoot) && (request.CustomName is not { Length: > 0 } || request.AutoNamed);

    /// <summary>
    /// The name for a CLI working <paramref name="ticketId"/> (whose friendly name is <paramref name="ticketName"/>, when it
    /// has one) and last seen doing <paramref name="doing"/>: always exactly <see cref="Words"/> words (M.ORCH-T1878), taken
    /// first from the ticket's name, then its activity, then the ticket's ID; null when together they don't have three, which
    /// leaves the tab as it is. Never longer than <paramref name="maxChars"/> (at least 14): the longest words are cut down
    /// rather than any word dropped.
    /// </summary>
    public static string? NameFor(string? ticketId, string? ticketName, string? doing, int maxChars)
    {
        var words = new List<string>();
        foreach (var source in new[] { ticketName, doing, ticketId })
            foreach (var word in WordsOf(source))
                if (words.Count < Words && !words.Contains(word, StringComparer.OrdinalIgnoreCase)) words.Add(word);
        return words.Count < Words ? null : Fit(words, maxChars);
    }

    /// <summary><paramref name="text"/>'s first <see cref="Words"/> meaningful words, fitted to <paramref name="maxChars"/>;
    /// null when it has fewer than three.</summary>
    public static string? Shorten(string? text, int maxChars) =>
        WordsOf(text).Take(Words).ToList() is { Count: Words } words ? Fit(words, maxChars) : null;

    /// <summary><paramref name="text"/>'s meaningful words in order: its first line up to a sentence or clause break, filler
    /// dropped, a tool or code name split into its parts (<c>mcp__rea__list_strings</c> is "rea list strings") and a path
    /// cut to its file name.</summary>
    private static IEnumerable<string> WordsOf(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) yield break;
        var line = text.Trim().Split('\n', 2)[0];
        // A colon breaks only as punctuation (": "), never inside a path like D:\.
        var stops = new[] { line.IndexOf(": ", StringComparison.Ordinal), line.TrimEnd().EndsWith(':') ? line.TrimEnd().Length - 1 : -1, line.IndexOfAny([';', '(', ',']) };
        var stop = stops.Where(i => i > 3).DefaultIfEmpty(-1).Min();
        if (stop > 3) line = line[..stop];
        foreach (var raw in line.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries))
        {
            var token = raw.Trim('"', '\'', '`', '*', '[', ']', '-', '.');
            var slash = token.LastIndexOfAny(['\\', '/']);
            if (slash >= 0 && slash < token.Length - 1) token = token[(slash + 1)..];
            foreach (var part in token.Split(['_'], StringSplitOptions.RemoveEmptyEntries))
            {
                var word = part.Trim('.', '-');
                if (word.Length > 0 && !Filler.Contains(word)) yield return word;
            }
        }
    }

    /// <summary><paramref name="words"/> joined, capitalized, the longest cut a letter at a time (to <see cref="MinWordChars"/>)
    /// until the name fits <paramref name="maxChars"/>.</summary>
    private static string Fit(List<string> words, int maxChars)
    {
        var max = Math.Max(14, maxChars);
        var fitted = words.ToList();
        while (fitted.Sum(w => w.Length) + fitted.Count - 1 > max && fitted.MaxBy(w => w.Length) is { Length: > MinWordChars } longest)
        {
            var i = fitted.IndexOf(longest);
            fitted[i] = longest[..^1];
        }
        var name = string.Join(' ', fitted);
        return char.ToUpperInvariant(name[0]) + name[1..];
    }

    private static string Folder(string path)
    {
        try { return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path)); }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException) { return path.TrimEnd('\\', '/'); }
    }
}
