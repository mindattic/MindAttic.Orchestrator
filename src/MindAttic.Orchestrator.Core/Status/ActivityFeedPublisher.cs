using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using MindAttic.Orchestrator.Settings;

namespace MindAttic.Orchestrator.Status;

/// <summary>
/// The daemon's activity publisher (M.ORCH-T962; docs/spikes/activity-feed.md section 5): watches the hooks' per-session activity
/// records (<c>~/.claude/session-context/activity/&lt;session&gt;.json</c>, written by the context gate for Codex and Gemini since
/// M.ORCH-T964) and puts each changed one on the bus as the <see cref="AppBus.Activity"/> kind through the hub's per-session
/// coalescing (<see cref="AppBusHub.OfferActivity"/>). Cheap: a <see cref="FileSystemWatcher"/> says which file changed, only that
/// one is read, and a slow rescan (the ActivityRescanSeconds Vault setting) catches a change the watcher missed and ends the
/// sessions gone silent with their CLI gone (the ActivityEndedMinutes setting). The settings are read again at each rescan, so a
/// change in Vault is live within one. Nothing here throws into its caller.
/// </summary>
public sealed class ActivityFeedPublisher(AppBusHub hub, string folder)
{
    private static readonly Serilog.ILogger log = OrchestratorLog.For("ActivityFeed");

    /// <summary>The records' folder: <c>~/.claude/session-context/activity</c>.</summary>
    public static string DefaultFolder { get; } =
        Path.Combine(Setup.AgentGate.ClaudeDir, "session-context", Setup.AgentGate.ActivityDirName);

    /// <summary>The settings, read at each rescan; null keeps the defaults.</summary>
    public Func<OrchestratorSettings>? Settings { get; init; }

    /// <summary>Whether a process runs; tests swap it.</summary>
    internal Func<int, bool> Alive { get; init; } = ProcessAlive;

    /// <summary>The clock; tests swap it.</summary>
    internal Func<DateTime> Now { get; init; } = () => DateTime.UtcNow;

    private readonly ConcurrentDictionary<string, byte> dirty = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DateTime> written = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim changed = new(0);
    private TimeSpan rescanEvery = TimeSpan.FromSeconds(OrchestratorSettings.DefaultActivityRescanSeconds);
    private TimeSpan endedAfter = TimeSpan.FromMinutes(OrchestratorSettings.DefaultActivityEndedMinutes);

    /// <summary>Watches and publishes until <paramref name="ct"/> ends.</summary>
    public async Task RunAsync(CancellationToken ct)
    {
        FileSystemWatcher? watcher = null;
        try
        {
            LoadSettings();
            Scan(Now(), starting: true);
            var rescanAt = Now() + rescanEvery;
            while (!ct.IsCancellationRequested)
            {
                watcher ??= TryWatch();
                var now = Now();
                var wait = rescanAt - now;
                if (hub.Activity.NextDue(now) is { } due && due < wait) wait = due;
                if (wait < TimeSpan.Zero) wait = TimeSpan.Zero;
                try { await changed.WaitAsync(wait, ct).ConfigureAwait(false); }
                catch (OperationCanceledException) { return; }
                now = Now();
                try
                {
                    ReadDirty(now);
                    if (now >= rescanAt)
                    {
                        LoadSettings();
                        Scan(now, starting: false);
                        hub.ExpireActivity(now, endedAfter, Alive);
                        rescanAt = now + rescanEvery;
                    }
                    hub.FlushActivity(now);
                }
                catch (Exception ex) when (ex is not OutOfMemoryException)
                {
                    log.Warning(ex, "The activity feed's pass failed; trying again");
                }
            }
        }
        finally
        {
            watcher?.Dispose();
        }
    }

    /// <summary>Takes the coalescing gap, rescan and end times from the settings; a failed read keeps the last ones.</summary>
    private void LoadSettings()
    {
        if (Settings is null) return;
        try
        {
            var s = Settings();
            hub.Activity.Every = TimeSpan.FromSeconds(Math.Clamp(s.ActivityCoalesceSeconds, 0, 60));
            rescanEvery = TimeSpan.FromSeconds(Math.Clamp(s.ActivityRescanSeconds, 1, 3600));
            endedAfter = TimeSpan.FromMinutes(Math.Clamp(s.ActivityEndedMinutes, 0, 1440));
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            log.Debug(ex, "Couldn't read the activity feed's settings; keeping the last ones");
        }
    }

    private FileSystemWatcher? TryWatch()
    {
        try
        {
            Directory.CreateDirectory(folder);
            var w = new FileSystemWatcher(folder, "*.json") { NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite, InternalBufferSize = 16384 };
            w.Changed += (_, e) => MarkDirty(e.FullPath);
            w.Created += (_, e) => MarkDirty(e.FullPath);
            // The hook writes a temp file and replaces the record with it: the record's name is the rename's new one.
            w.Renamed += (_, e) => MarkDirty(e.FullPath);
            // A lost buffer: the next rescan reads whatever changed.
            w.Error += (_, e) => log.Debug(e.GetException(), "The activity folder's watcher lost changes; the rescan will catch them");
            w.EnableRaisingEvents = true;
            return w;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or PlatformNotSupportedException)
        {
            log.Debug(ex, "Couldn't watch the activity folder {Folder:l}; reading it at each rescan only", folder);
            return null;
        }
    }

    /// <summary>Notes <paramref name="path"/> as changed and wakes the loop. Never throws.</summary>
    internal void MarkDirty(string path)
    {
        if (!path.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) return;
        dirty[path] = 0;
        try { if (changed.CurrentCount == 0) changed.Release(); }
        catch (Exception ex) when (ex is ObjectDisposedException or SemaphoreFullException) { }
    }

    private void ReadDirty(DateTime now)
    {
        foreach (var path in dirty.Keys)
        {
            dirty.TryRemove(path, out _);
            Take(path, now, starting: false);
        }
    }

    /// <summary>Reads each record written since it was last read (all of them at the start).</summary>
    internal void Scan(DateTime now, bool starting)
    {
        if (!Directory.Exists(folder)) return;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in Directory.EnumerateFiles(folder, "*.json"))
        {
            seen.Add(path);
            DateTime at;
            try { at = File.GetLastWriteTimeUtc(path); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { continue; }
            if (written.TryGetValue(path, out var was) && was == at) continue;
            Take(path, now, starting);
        }
        foreach (var gone in written.Keys.Where(p => !seen.Contains(p)).ToList()) written.Remove(gone);
    }

    /// <summary>Reads one record and offers it. At the start, a record older than the end time whose CLI is gone is history, not
    /// news, and isn't sent.</summary>
    private void Take(string path, DateTime now, bool starting)
    {
        try
        {
            written[path] = File.GetLastWriteTimeUtc(path);
            if (Parse(File.ReadAllText(path)) is not { } parsed) return;
            var (activity, pid) = parsed;
            if (starting && now - (activity.AtUtc ?? written[path]) >= endedAfter && !(pid is { } p && Alive(p))) return;
            hub.OfferActivity(activity, now, pid);
        }
        // Gone, or being replaced this instant: the replace raises its own change.
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    /// <summary>One activity record (docs/spikes/activity-feed.md section 5) read as its payload and its CLI's pid; null when it
    /// isn't one. Only the payload's own fields are taken: anything else the record holds is never published.</summary>
    internal static (ActivityPayload Activity, int? Pid)? Parse(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var r = doc.RootElement;
            if (r.ValueKind != JsonValueKind.Object || Str(r, "session") is not { } session || Str(r, "state") is not { } state) return null;
            var activity = new ActivityPayload
            {
                Session = session, Provider = Str(r, "provider"), Repo = Str(r, "repo"), Login = Str(r, "login"), State = state,
                Tool = Str(r, "tool"), Target = Str(r, "target"), Verb = Str(r, "verb"), Ticket = Str(r, "ticket"),
                Subagents = Long(r, "subagents") is { } n and > 0 and < int.MaxValue ? (int)n : null,
                Seq = Long(r, "seq"),
                AtUtc = Str(r, "atUtc") is { } at && DateTime.TryParse(at, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var t) ? t : null,
            };
            int? pid = Long(r, "pid") is { } p and > 0 and < int.MaxValue ? (int)p : null;
            return (activity, pid);
        }
        catch (JsonException) { return null; }

        static string? Str(JsonElement r, string name) =>
            r.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String && v.GetString() is { Length: > 0 } s ? s : null;
        static long? Long(JsonElement r, string name) =>
            r.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt64(out var n) ? n : null;
    }

    private static bool ProcessAlive(int pid)
    {
        try
        {
            using var p = System.Diagnostics.Process.GetProcessById(pid);
            return !p.HasExited;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException) { return false; }
    }
}
