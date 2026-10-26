using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MindAttic.Orchestrator.Sessions;

/// <summary>One terminal tab of the dashboard as it stood (M.ORCH-T862): what opened it, the Claude session its CLI was in,
/// the login it was on by then (it may have moved) and whether it was its person's chosen tab.</summary>
public sealed record RestoreTab
{
    public required PaneRequest Request { get; init; }

    /// <summary>The Claude session its CLI was in, when known: it reopens with <c>claude --resume</c> on it.</summary>
    public string? SessionId { get; init; }

    /// <summary>The login its CLI was on last (its email), null for the global login.</summary>
    public string? Login { get; init; }

    /// <summary>It was the tab its person's page showed.</summary>
    public bool Chosen { get; init; }

    /// <summary>The TermHost session its CLI ran in (M.ORCH-T928), null when it ran unhosted (in the dashboard's own process). While
    /// that host lives the dashboard reattaches the tab itself, so a restore never offers it.</summary>
    public string? HostId { get; init; }
}

/// <summary>Every terminal tab open in the dashboard, and who and what was on screen (M.ORCH-T862): rewritten whenever that
/// changes, so a crash or a kill (which can run no code) still leaves what to reopen.</summary>
public sealed record RestoreState
{
    public int Version { get; init; } = 1;

    public DateTime SavedUtc { get; init; }

    /// <summary>The dashboard exited on its own (closed): nothing to offer at the next start.</summary>
    public bool Clean { get; init; }

    /// <summary>Why it was last written: "change", "exit", "crash", "upgrade"...</summary>
    public string? Reason { get; init; }

    public int Pid { get; init; }

    /// <summary>The person shown: their login tab's key (the email, "" for the global login); null when unknown.</summary>
    public string? SelectedPerson { get; init; }

    /// <summary>The tab on screen, an index into <see cref="Tabs"/>; null for none.</summary>
    public int? SelectedTab { get; init; }

    public IReadOnlyList<RestoreTab> Tabs { get; init; } = [];
}

/// <summary>
/// The dashboard's session-restore file (M.ORCH-T862), <c>%APPDATA%\MindAttic\MindAttic.Orchestrator\session-restore\session.json</c>:
/// written atomically (a temp file, then moved over it). Each start rotates the last run's file to <c>session.1.json</c> (and
/// so on, <see cref="Keep"/> kept), never deleting one on read, and offers to restore it when it wasn't marked clean.
/// Nothing here throws: a restore file that can't be read or written is logged and skipped.
/// </summary>
public sealed class SessionRestoreStore(string? directory = null)
{
    public const string FileName = "session.json";

    /// <summary>How many earlier runs' files are kept beside the current one.</summary>
    public const int Keep = 5;

    public string Directory { get; } = directory ?? Path.Combine(Settings.JsonStore.LiveDirectory, "session-restore");

    public string CurrentPath => Path.Combine(Directory, FileName);

    /// <summary>The <paramref name="n"/>th run back's file, <c>session.n.json</c>.</summary>
    public string RotatedPath(int n) => Path.Combine(Directory, $"session.{n}.json");

    private static readonly JsonSerializerOptions Json = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    private readonly Lock gate = new();

    public static string Serialize(RestoreState state) => JsonSerializer.Serialize(state, Json);

    public static RestoreState? Deserialize(string json) => JsonSerializer.Deserialize<RestoreState>(json, Json);

    /// <summary>Writes <paramref name="state"/> as the current file: to a temp file first, then moved over it, so a crash
    /// mid-write leaves the last whole one. False (logged) when it couldn't be written.</summary>
    public bool Save(RestoreState state)
    {
        lock (gate)
        {
            string? temp = null;
            try
            {
                System.IO.Directory.CreateDirectory(Directory);
                temp = Path.Combine(Directory, $"{FileName}.{Environment.ProcessId}.tmp");
                File.WriteAllText(temp, Serialize(state), new UTF8Encoding(false));
                File.Move(temp, CurrentPath, overwrite: true);
                return true;
            }
            catch (Exception ex)
            {
                try { if (temp is not null && File.Exists(temp)) File.Delete(temp); } catch (Exception) { }
                try { OrchestratorLog.For("Restore").Error(ex, "Couldn't write the session-restore file {Path:l}", CurrentPath); } catch (Exception) { }
                return false;
            }
        }
    }

    /// <summary>The current file (the last run's, at a start before <see cref="Rotate"/>); null when there's none or it can't be read.</summary>
    public RestoreState? Load() => Load(CurrentPath);

    public static RestoreState? Load(string path)
    {
        try { return File.Exists(path) ? Deserialize(File.ReadAllText(path)) : null; }
        catch (Exception ex)
        {
            try { OrchestratorLog.For("Restore").Warning(ex, "Couldn't read the session-restore file {Path:l}", path); } catch (Exception) { }
            return null;
        }
    }

    /// <summary>Moves the current file to <c>session.1.json</c>, each older one up a number, the one past <see cref="Keep"/>
    /// dropped: so this run writes a fresh current file and the last few runs' stay. Returns the rotated file's path, or null.</summary>
    public string? Rotate()
    {
        lock (gate)
        {
            try
            {
                if (!File.Exists(CurrentPath)) return null;
                if (File.Exists(RotatedPath(Keep))) File.Delete(RotatedPath(Keep));
                for (var n = Keep - 1; n >= 1; n--)
                    if (File.Exists(RotatedPath(n))) File.Move(RotatedPath(n), RotatedPath(n + 1), overwrite: true);
                File.Move(CurrentPath, RotatedPath(1), overwrite: true);
                return RotatedPath(1);
            }
            catch (Exception ex)
            {
                try { OrchestratorLog.For("Restore").Warning(ex, "Couldn't rotate the session-restore files in {Dir:l}", Directory); } catch (Exception) { }
                return null;
            }
        }
    }

    /// <summary>Whether <paramref name="state"/> is one to offer: it wasn't a clean exit and had a tab open.</summary>
    public static bool ShouldOffer(RestoreState? state) => state is { Clean: false, Tabs.Count: > 0 };

    /// <summary>
    /// <paramref name="state"/> without the tabs whose TermHost still lives (M.ORCH-T928): the dashboard reattaches those, so
    /// offering them for <c>--resume</c> would run each CLI twice. What stays died with its host or was never hosted (the fallback
    /// path). <see cref="RestoreState.SelectedTab"/> follows its tab, or goes when that tab was dropped.
    /// <paramref name="hostAlive"/> says whether a session id has a live host (default: <see cref="Term.TermSessions.Get"/>).
    /// </summary>
    public static RestoreState Unhosted(RestoreState state, Func<string, bool>? hostAlive = null)
    {
        hostAlive ??= id => Term.TermSessions.Get(id) is not null;
        var kept = new List<RestoreTab>();
        int? selected = null;
        for (var i = 0; i < state.Tabs.Count; i++)
        {
            var tab = state.Tabs[i];
            if (tab.HostId is { Length: > 0 } id && hostAlive(id)) continue;
            if (state.SelectedTab == i) selected = kept.Count;
            kept.Add(tab);
        }
        return kept.Count == state.Tabs.Count ? state : state with { Tabs = kept, SelectedTab = selected };
    }

    /// <summary>
    /// The request that reopens <paramref name="tab"/>: a Launcher menu pane as it was; a Claude CLI on the login it was on
    /// last, with <c>--resume &lt;session&gt;</c> (as <c>orchestrator move</c>'s Launcher restart does), or fresh in its
    /// folder when its session isn't known. The original <c>--prompt</c> is dropped either way, so a reopened CLI never
    /// sends its first prompt again. <paramref name="configDirOf"/> gives a login's <c>CLAUDE_CONFIG_DIR</c> (null: the global
    /// login's), for a CLI moved to another login since it opened; <paramref name="lookOf"/> its badge and color.
    /// </summary>
    public static PaneRequest ReopenRequest(RestoreTab tab, Func<string?, string>? configDirOf = null, Func<string?, LoginLook?>? lookOf = null)
    {
        var request = tab.Request;
        if (request.IsMenu) return request;
        // A tab built from its TermHost's record (TermPaneFile.FromRecord: a Launcher-started CLI) has only its whole command
        // line (M.ORCH-T1029): split it into its arguments, so a Launcher host command drops its --prompt and resumes like any other.
        if (request.Command is not { Count: > 0 } && request.CommandLine is { Length: > 0 } line && SplitArgs(line) is { Count: > 1 } split)
            request = request with { Command = split, CommandLine = null };
        if (request.Command is not { Count: > 0 } command) return tab.Request;
        var args = command.ToList();
        var isHost = args.Count > 1 && args.Skip(1).Any(a => a.Equals("host", StringComparison.OrdinalIgnoreCase));
        if (!isHost) return tab.Request;
        RemoveOption(args, "--prompt");
        RemoveOption(args, "--resume");
        var claude = string.Equals(request.Provider, "Claude", StringComparison.OrdinalIgnoreCase);

        // Moved to another login since it opened ("Move to", the rebalancer): it reopens on the one it was on last.
        var moved = claude && configDirOf is not null &&
                    !string.Equals(AccountDirs.Normalize(tab.Login), request.Login, StringComparison.OrdinalIgnoreCase);
        if (moved)
        {
            var login = AccountDirs.Normalize(tab.Login);
            var dir = configDirOf!(login);
            var at = args.FindIndex(a => a.Equals("--config-dir", StringComparison.OrdinalIgnoreCase));
            if (at >= 0 && at + 1 < args.Count) args[at + 1] = dir;
            else { args.Add("--config-dir"); args.Add(dir); }
            var look = lookOf?.Invoke(login is null ? null : dir);
            var title = request.Title;
            if (look is not null && request.Initials is { Length: > 0 } old && title.StartsWith(old + ":", StringComparison.Ordinal))
                title = look.Initials + title[old.Length..];
            request = request with
            {
                Title = title,
                Email = login,
                ConfigDir = login is null ? null : dir,
                Initials = look?.Initials ?? request.Initials,
                TabColor = look?.TabColor ?? request.TabColor,
            };
        }

        if (claude && tab.SessionId is { Length: > 0 } session) args.AddRange(["--resume", session]);
        return request with { Command = args };
    }

    /// <summary>A Windows command line's arguments; empty when it can't be split. Never throws.</summary>
    private static IReadOnlyList<string> SplitArgs(string commandLine)
    {
        try { return HostSessionScanner.SplitCommandLine(commandLine); }
        catch (Exception ex) when (ex is not OutOfMemoryException) { return []; }
    }

    /// <summary>Whether <paramref name="tab"/> reopens with <c>--resume</c> (<see cref="ReopenRequest"/>): a tab whose command
    /// isn't a Launcher host command reopens as it was started, so its session isn't resumed.</summary>
    public static bool Resumes(RestoreTab tab)
    {
        try { return ReopenRequest(tab).Command?.Contains("--resume", StringComparer.OrdinalIgnoreCase) == true; }
        catch (Exception ex) when (ex is not OutOfMemoryException) { return false; }
    }

    /// <summary>Removes every <paramref name="name"/> and the value after it.</summary>
    internal static void RemoveOption(List<string> args, string name)
    {
        for (var i = args.Count - 1; i >= 1; i--)
        {
            if (!args[i].Equals(name, StringComparison.OrdinalIgnoreCase)) continue;
            args.RemoveRange(i, i + 1 < args.Count ? 2 : 1);
        }
    }

    /// <summary>One line per tab, for the restore prompt and the log.</summary>
    public static IReadOnlyList<string> Describe(RestoreState state) =>
    [
        .. state.Tabs.Select(t => t.Request.IsMenu
            ? $"{t.Request.Title}: the Launcher menu"
            : $"{t.Request.Title} in {t.Request.Directory}: " + (t.SessionId is not { Length: > 0 } s ? "no session known, opens fresh"
                : Resumes(t) ? $"resumes session {s}" : $"reopens as it was started; session {s} can't be resumed from its command")),
    ];
}
