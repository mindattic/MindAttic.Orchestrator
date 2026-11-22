namespace MindAttic.Orchestrator.Sessions;

/// <summary>
/// The save-state slots (M.ORCH-T1869): Quick Save All's package of every CLI tab, as a <see cref="RestoreState"/> whose tabs
/// carry their handoff code, /allocate and /crossrepo, in <c>%APPDATA%\MindAttic\MindAttic.Orchestrator\save-states</c>. Each
/// save is kept as <c>save-&lt;utc&gt;.json</c> (the last <see cref="Keep"/>); the newest is what Quick Load All restores, unless
/// the session-restore autosave (written on every change, and on a crash or restart) is newer. Nothing here throws.
/// </summary>
public sealed class SaveStateStore(string? directory = null)
{
    /// <summary>How many saves are kept.</summary>
    public const int Keep = 10;

    /// <summary>The reason a Quick Save All package is written with.</summary>
    public const string SaveAllReason = "save-all";

    /// <summary>The directory.</summary>
    public string Directory { get; } = directory ?? Path.Combine(Settings.JsonStore.LiveDirectory, "save-states");

    /// <summary>Writes <paramref name="state"/> as a new save, dropping the oldest past <see cref="Keep"/>; its path, or null.</summary>
    public string? Save(RestoreState state)
    {
        try
        {
            System.IO.Directory.CreateDirectory(Directory);
            var path = Path.Combine(Directory, $"save-{state.SavedUtc:yyyyMMdd-HHmmss-fff}.json");
            var temp = path + ".tmp";
            File.WriteAllText(temp, SessionRestoreStore.Serialize(state), new System.Text.UTF8Encoding(false));
            File.Move(temp, path, overwrite: true);
            foreach (var old in Saves().Skip(Keep))
                try { File.Delete(old); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            return path;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            try { OrchestratorLog.For("SaveState").Error(ex, "Couldn't write a save state in {Dir:l}", Directory); } catch (Exception) { }
            return null;
        }
    }

    /// <summary>Every save, newest first.</summary>
    public IReadOnlyList<string> Saves()
    {
        try
        {
            return System.IO.Directory.Exists(Directory)
                ? [.. System.IO.Directory.EnumerateFiles(Directory, "save-*.json").OrderByDescending(Path.GetFileName, StringComparer.Ordinal)]
                : [];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return []; }
    }

    /// <summary>The newest save, or null.</summary>
    public RestoreState? Latest() => Saves().Select(SessionRestoreStore.Load).FirstOrDefault(s => s is not null);

    /// <summary>What a Quick Load restores: the newer of the last save and <paramref name="autosave"/> (the session-restore
    /// state), so a crash or restart after a save still comes back as it was at the end. Null when neither has a tab.</summary>
    public static RestoreState? Pick(RestoreState? save, RestoreState? autosave)
    {
        var options = new[] { save, autosave }.Where(s => s is { Tabs.Count: > 0 }).Select(s => s!).ToList();
        return options.Count == 0 ? null : options.MaxBy(s => s.SavedUtc);
    }

    /// <summary>The file holding when a save state was last restored (M.ORCH-T2069).</summary>
    public string LastLoadPath => Path.Combine(Directory, "last-load.txt");

    /// <summary>The <see cref="RestoreState.SavedUtc"/> of the state last restored (by Quick Load All or on start), or null.</summary>
    public DateTime? LastLoadedUtc()
    {
        try
        {
            return File.Exists(LastLoadPath) && DateTime.TryParse(File.ReadAllText(LastLoadPath).Trim(), System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal, out var when) ? when : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return null; }
    }

    /// <summary>Records that <paramref name="state"/> was restored, so the next start doesn't restore it again.</summary>
    public void MarkLoaded(RestoreState state)
    {
        try
        {
            System.IO.Directory.CreateDirectory(Directory);
            File.WriteAllText(LastLoadPath, state.SavedUtc.ToUniversalTime().ToString("o", System.Globalization.CultureInfo.InvariantCulture));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    /// <summary>Whether <paramref name="state"/> is worth restoring on start (M.ORCH-T2069): it has tabs and was saved after
    /// the state last restored (<paramref name="lastLoadedUtc"/>; none yet counts as older).</summary>
    public static bool IsNewer(RestoreState? state, DateTime? lastLoadedUtc) =>
        state is { Tabs.Count: > 0 } && (lastLoadedUtc is not { } last || state.SavedUtc.ToUniversalTime() > last);

    /// <summary>What turns <paramref name="tab"/>'s /allocate back on, typed into its CLI; null when it was off. Its own repo
    /// alone is bare <c>/allocate</c>; "*" is <c>/allocate all</c>; else <c>/allocate -repo=A,B</c>.</summary>
    public static string? AllocateCommand(RestoreTab tab)
    {
        if (tab.Allocate is not { Count: > 0 } repos) return null;
        if (repos.Any(r => r is "*" or "all")) return "/allocate all";
        var own = Path.GetFileName(tab.Request.Directory.TrimEnd('\\', '/'));
        return repos.Count == 1 && string.Equals(repos[0], own, StringComparison.OrdinalIgnoreCase) ? "/allocate" : $"/allocate -repo={string.Join(',', repos)}";
    }

    /// <summary>What turns <paramref name="tab"/>'s /crossrepo back on; null when it had none.</summary>
    public static string? CrossrepoCommand(RestoreTab tab) =>
        tab.Crossrepo is { Count: > 0 } repos ? "/crossrepo " + (repos.Any(r => r is "*" or "all") ? "all" : string.Join(',', repos)) : null;

    /// <summary>The first thing typed into a restored CLI: <c>/quickload &lt;code&gt;</c> on its handoff, else a plain "carry on".</summary>
    public static string ResumePrompt(RestoreTab tab) => tab.HandoffCode is { Length: > 0 } code
        ? $"/quickload {code}"
        : "This CLI was restored from a save state (claude --resume). Continue exactly where you left off; if your work was already finished, say so in one line and stop.";
}
