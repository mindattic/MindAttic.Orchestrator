namespace MindAttic.Orchestrator;

/// <summary>
/// Keeps this process's PATH whole (M.ORCH-T1314). A start that didn't come from the user's own session, such as the installer
/// launching the app at the end of setup, hands over an environment with only the machine PATH: no <c>.dotnet\tools</c>
/// (<c>tickets</c>), npm or <c>.local\bin</c> (<c>claude</c>). The daemon then can't sync tickets, and every TermHost, Launcher and CLI
/// it starts inherits the stripped PATH. At startup the machine and user PATH from the registry are merged in, so the app runs with
/// the PATH a fresh logon would give it, whoever started it.
/// </summary>
public static class ProcessPath
{
    /// <summary>Merges the registry's machine and user PATH into this process's PATH; returns how many entries were added.</summary>
    public static int Refresh()
    {
        try
        {
            var current = Environment.GetEnvironmentVariable("PATH") ?? "";
            var merged = Merge(current,
                Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Machine),
                Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User));
            // Entries the merge brought in that the current PATH lacks: not a count difference, which duplicates already in the
            // current PATH (dropped by the merge) would cancel out, leaving the stripped PATH in place.
            var had = Split(current).Select(e => Key(Environment.ExpandEnvironmentVariables(e))).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var added = Split(merged).Count(e => !had.Contains(Key(e)));
            if (added > 0) Environment.SetEnvironmentVariable("PATH", merged);
            return Math.Max(0, added);
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            return 0; // keep what we have
        }
    }

    /// <summary>
    /// <paramref name="current"/>'s entries first, in order, then the machine's and the user's that it lacks (environment variables
    /// expanded, as Windows does at logon). Duplicates are dropped, compared case-insensitively and ignoring a trailing slash.
    /// </summary>
    public static string Merge(string? current, string? machine, string? user)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<string>();
        foreach (var e in Split(current).Concat(Split(machine)).Concat(Split(user)))
        {
            var expanded = Environment.ExpandEnvironmentVariables(e);
            if (seen.Add(Key(expanded))) result.Add(expanded);
        }
        return string.Join(';', result);
    }

    private static List<string> Split(string? path) =>
        (path ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

    private static string Key(string entry) => entry.TrimEnd('\\', '/');
}
