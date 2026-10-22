namespace MindAttic.Orchestrator.Sessions;

/// <summary>One tab of a Windows Terminal window, as UI Automation reads it: the window's handle and the tab's name.</summary>
public sealed record TerminalTab(long Window, string Name);

/// <summary>
/// Which Windows Terminal tab is a Launcher host's (M.ORCH-T619). Rule: the host's own WindowsTerminal.exe ancestor gives
/// the windows (one process hosts them all, so the pid alone doesn't pick a window), and among their tabs the one whose
/// name, without the status glyph the Launcher and Claude Code put in front, is the host's <c>--title</c>; failing that, the
/// one glyph-prefixed tab no other host's title claims; failing that, the window alone when only one can be it.
/// </summary>
public static class TerminalTabPicker
{
    /// <summary>The title a host's tab carries: its <c>--title</c>, else "folder [Provider]" as the Orchestrator's own tabs read.</summary>
    public static string? ExpectedTitle(HostSession host)
    {
        if (!string.IsNullOrWhiteSpace(host.Title)) return host.Title.Trim();
        var folder = host.WorkingDirectory is { Length: > 0 } dir ? Path.GetFileName(dir.TrimEnd('\\', '/')) : null;
        return string.IsNullOrEmpty(folder) ? null : $"{folder} [{host.Provider}]";
    }

    /// <summary>A tab's name without the leading status glyph and spaces ("▶  REA.Bloodlines [Claude]" → "REA.Bloodlines [Claude]").</summary>
    public static string Normalize(string name)
    {
        var i = 0;
        // Letters, digits and the brackets an Orchestrator title opens with ("[JR] Repo [Claude]") start the real title.
        while (i < name.Length && !char.IsLetterOrDigit(name[i]) && name[i] != '[') i++;
        return name[i..].Trim();
    }

    /// <summary>The host's tab among <paramref name="tabs"/>, or null when none (or more than one) can be told to be it.</summary>
    public static TerminalTab? PickTab(IReadOnlyList<TerminalTab> tabs, string? title, IEnumerable<string?> otherTitles)
    {
        if (!string.IsNullOrWhiteSpace(title))
        {
            var exact = tabs.Where(t => Normalize(t.Name).Equals(title, StringComparison.OrdinalIgnoreCase)).ToList();
            if (exact.Count > 0) return exact.Count == 1 ? exact[0] : null;
            var containing = tabs.Where(t => t.Name.Contains(title, StringComparison.OrdinalIgnoreCase)).ToList();
            if (containing.Count > 0) return containing.Count == 1 ? containing[0] : null;
        }

        // Claude Code may have retitled it: the one tab carrying a status glyph that no other live host's title claims.
        var others = otherTitles.Where(o => !string.IsNullOrWhiteSpace(o)).Select(o => o!.Trim()).ToList();
        var unclaimed = tabs.Where(t => t.Name.Length > 0 && !char.IsLetterOrDigit(t.Name[0]) && t.Name[0] != '[')
            .Where(t => !others.Any(o => t.Name.Contains(o, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        return unclaimed.Count == 1 ? unclaimed[0] : null;
    }

    /// <summary>
    /// The window to bring up when no tab could be picked: the one holding every tab titled like the host (two tabs on one
    /// repo), else the terminal's only window; null when it could be any of several.
    /// </summary>
    public static long? PickWindow(IReadOnlyList<TerminalTab> tabs, IReadOnlyList<long> windows, string? title)
    {
        if (!string.IsNullOrWhiteSpace(title))
        {
            var holding = tabs.Where(t => Normalize(t.Name).Equals(title, StringComparison.OrdinalIgnoreCase)
                                          || t.Name.Contains(title, StringComparison.OrdinalIgnoreCase))
                .Select(t => t.Window).Distinct().ToList();
            if (holding.Count == 1) return holding[0];
        }
        return windows.Count == 1 ? windows[0] : null;
    }
}
