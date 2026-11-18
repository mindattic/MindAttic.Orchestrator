namespace MindAttic.Orchestrator.Sessions;

/// <summary>
/// The short name a workspace CLI tab is given for what it is doing (M.ORCH-T1817). A CLI opened on a repo is named for that
/// repo and keeps the name; one opened at the workspace root ("General", or the workspace folder's own name, "MindAttic")
/// says nothing about its work, so the dashboard renames it from what the Orchestrator already knows: the ticket it is
/// working, else its latest activity. No model is asked: the name is cut from text that is already there.
/// </summary>
public static class AutoTabName
{
    /// <summary>Words that carry no meaning in a tab's few characters.</summary>
    private static readonly HashSet<string> Filler = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "an", "the", "of", "to", "for", "and", "or", "in", "on", "at", "by", "with", "from", "into", "its", "it", "is", "are",
        "be", "that", "this", "as", "so", "then", "each", "every", "all", "own",
    };

    /// <summary>The most words a name takes, however short they are.</summary>
    public const int MaxWords = 3;

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
    /// has one) and last seen doing <paramref name="doing"/>: the ticket's name shortened, else its ID, else the activity
    /// shortened; null when none of them says anything, which leaves the tab as it is. Never longer than
    /// <paramref name="maxChars"/> (at least 8).
    /// </summary>
    public static string? NameFor(string? ticketId, string? ticketName, string? doing, int maxChars)
    {
        var max = Math.Max(8, maxChars);
        if (Shorten(ticketName, max) is { } named) return named;
        if (ticketId?.Trim() is { Length: > 0 } id) return id.Length <= max ? id : id[..max];
        return Shorten(doing, max);
    }

    /// <summary><paramref name="text"/>'s first meaningful words, up to <see cref="MaxWords"/> of them and
    /// <paramref name="maxChars"/> characters, cut only between words; a single word longer than that is cut to fit. Null for
    /// nothing usable.</summary>
    public static string? Shorten(string? text, int maxChars)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        // The first line, up to its first sentence or clause break: what follows is detail.
        var line = text.Trim().Split('\n', 2)[0];
        var stop = line.IndexOfAny([':', ';', '(', '.', ',']);
        if (stop > 3) line = line[..stop];
        var words = line.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries)
            .Select(w => w.Trim('"', '\'', '`', '*', '[', ']', '-'))
            .Where(w => w.Length > 0 && !Filler.Contains(w))
            .ToList();
        if (words.Count == 0) return null;
        var name = "";
        foreach (var word in words.Take(MaxWords))
        {
            var next = name.Length == 0 ? word : name + " " + word;
            if (next.Length > maxChars) break;
            name = next;
        }
        if (name.Length == 0) name = words[0][..Math.Min(words[0].Length, maxChars)];
        return char.ToUpperInvariant(name[0]) + name[1..];
    }

    private static string Folder(string path)
    {
        try { return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path)); }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException) { return path.TrimEnd('\\', '/'); }
    }
}
