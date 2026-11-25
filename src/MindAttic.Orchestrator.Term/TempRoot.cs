namespace MindAttic.Orchestrator.Term;

/// <summary>What <see cref="TempRoot.Plan"/> decided: the variables to set on this process (none when TEMP and TMP are already
/// under the root, or the root is missing) and the warning to log, if any.</summary>
public sealed record TempPlan(IReadOnlyList<KeyValuePair<string, string>> Set, string? Warning);

/// <summary>
/// Every temp file on this machine goes under one temp root (M.ORCH-T2219; <c>OrchestratorSettings.TempRoot</c>, default
/// <see cref="Default"/>). The daemon, the dashboard and each TermHost call <see cref="Enforce"/> as they start: when TEMP or TMP
/// isn't under the root they are moved there for this process, so every child it spawns (claude CLIs, git, dotnet, python hooks)
/// inherits it. A root that doesn't exist (its drive missing) leaves TEMP alone. Lives in Term so TermHost, which never loads Core,
/// shares it.
/// </summary>
public static class TempRoot
{
    /// <summary>The temp root when the setting is empty.</summary>
    public const string Default = @"R:\Temp";

    /// <summary>The variable the daemon and dashboard pass their configured root on in, so a TermHost they start uses the same one.</summary>
    public const string RootVariable = "MINDATTIC_TEMP_ROOT";

    /// <summary>Where single-file .NET apps unpack, kept under the root like the rest of temp.</summary>
    public const string BundleVariable = "DOTNET_BUNDLE_EXTRACT_BASE_DIR";

    /// <summary><paramref name="root"/> trimmed, or <see cref="Default"/> when it is empty.</summary>
    public static string Normalize(string? root) => string.IsNullOrWhiteSpace(root) ? Default : root.Trim();

    /// <summary>Whether <paramref name="path"/> (environment variables expanded) is <paramref name="root"/> or a folder under it,
    /// case-insensitively. An empty or unparseable path isn't.</summary>
    public static bool IsUnder(string? path, string root)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        try
        {
            var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Environment.ExpandEnvironmentVariables(path.Trim())));
            var top = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Normalize(root)));
            return full.Equals(top, StringComparison.OrdinalIgnoreCase)
                || full.StartsWith(top + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException) { return false; }
    }

    /// <summary>The decision, apart from the process: with TEMP <paramref name="temp"/> and TMP <paramref name="tmp"/>, set TEMP,
    /// TMP and <see cref="BundleVariable"/> to <paramref name="root"/> unless both are already under it; nothing when the root
    /// doesn't exist (<paramref name="rootExists"/>), only a warning.</summary>
    public static TempPlan Plan(string? temp, string? tmp, string? root, bool rootExists)
    {
        var top = Normalize(root);
        if (IsUnder(temp, top) && IsUnder(tmp, top)) return new([], null);
        if (!rootExists) return new([], $"The temp root {top} doesn't exist, so TEMP stays {temp ?? "(unset)"}");
        return new(
        [
            new("TEMP", top),
            new("TMP", top),
            new(BundleVariable, Path.Combine(top, ".net")),
        ], $"TEMP was {temp ?? "(unset)"} and TMP {tmp ?? "(unset)"}, not under the temp root {top}; both set to {top} for this process and its children");
    }

    /// <summary>Applies <see cref="Plan"/> to this process for <paramref name="root"/> (empty: <see cref="RootVariable"/>, else
    /// <see cref="Default"/>) and passes the root on in <see cref="RootVariable"/>; the warning to log, or null. Never throws.</summary>
    public static string? Enforce(string? root = null)
    {
        try
        {
            var top = Normalize(string.IsNullOrWhiteSpace(root) ? Environment.GetEnvironmentVariable(RootVariable) : root);
            var plan = Plan(Environment.GetEnvironmentVariable("TEMP"), Environment.GetEnvironmentVariable("TMP"), top, Directory.Exists(top));
            foreach (var (key, value) in plan.Set) Environment.SetEnvironmentVariable(key, value);
            Environment.SetEnvironmentVariable(RootVariable, top);
            return plan.Warning;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException) { return $"Checking the temp root failed: {ex.Message}"; }
    }
}
