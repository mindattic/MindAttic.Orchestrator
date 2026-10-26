using System.Text.Json;
using System.Text.Json.Serialization;
using MindAttic.Orchestrator.Budget;
using MindAttic.Orchestrator.Reminders;
using MindAttic.Orchestrator.Scheduling;
using MindAttic.Orchestrator.Sessions;
using MindAttic.Orchestrator.Settings;

namespace MindAttic.Orchestrator.Status;

/// <summary>
/// What the dashboard used to learn only by rereading the store, pushed on the status bus instead (M.ORCH-T891): the member roster,
/// the orgs, the settings, the global pause and the pending reminders, each its own kind. Each message carries the whole of its
/// kind as it stands right after the change (read back from the store, at full precision), so the latest one is the truth and a
/// subscriber that missed one loses nothing at the next. Published by whichever process changed it, the moment the change commits
/// (<see cref="StoreChanges"/>): the dashboard and the <c>orchestrator</c> CLI through the daemon's inbox, the daemon straight to
/// its hub. The daemon keeps the latest of each and hands them to every subscriber as it connects, after the state. Every payload
/// names its <see cref="StatePayload.Store"/> (the database it was read from) so a subscriber applies only its own store's.
/// Best effort and never throws: with no daemon nothing is sent, and a dashboard that isn't connected polls the store as before.
/// </summary>
public static class StateBus
{
    private static readonly Serilog.ILogger log = OrchestratorLog.For("StateBus");

    /// <summary>The organization's members (<see cref="MembersPayload"/>).</summary>
    public const string Members = "members";

    /// <summary>The orgs and who's in each (<see cref="OrgsPayload"/>).</summary>
    public const string Orgs = "orgs";

    /// <summary>The orchestrator's settings (<see cref="SettingsPayload"/>).</summary>
    public const string Settings = "settings";

    /// <summary>The global pause (<see cref="PausePayload"/>).</summary>
    public const string Pause = "pause";

    /// <summary>The reminders not sent yet (<see cref="RemindersPayload"/>).</summary>
    public const string Reminders = "reminders";

    /// <summary>The version of each of these kinds' payloads.</summary>
    public const int Version = 1;

    /// <summary>Every kind of these.</summary>
    public static IReadOnlyList<string> Kinds { get; } = [Members, Orgs, Settings, Pause, Reminders];

    public static bool IsStateKind(string? kind) => kind is Members or Orgs or Settings or Pause or Reminders;

    /// <summary>The store <paramref name="db"/> is, as payloads name it: its full path.</summary>
    public static string StoreOf(OrchestratorDb db) => db.Path;

    /// <summary>Whether two payloads' stores are the same database (paths compared whole, ignoring case); false for a missing one.</summary>
    public static bool SameStore(string? a, string? b)
    {
        if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b)) return false;
        try { return string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase); }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException or System.Security.SecurityException) { return false; }
    }

    /// <summary>The store's <c>store</c> field from any of these kinds' envelopes; null when it has none.</summary>
    public static string? StoreOf(StatusEnvelope envelope)
    {
        try
        {
            return envelope.Payload.ValueKind == JsonValueKind.Object
                && envelope.Payload.TryGetProperty("store", out var s) && s.ValueKind == JsonValueKind.String ? s.GetString() : null;
        }
        catch (Exception ex) when (ex is InvalidOperationException or ObjectDisposedException) { return null; }
    }

    private static readonly object gate = new();
    private static readonly Dictionary<string, (string Kind, StoreChange Change)> waiting = new(StringComparer.OrdinalIgnoreCase);
    private static bool draining, inBackground;

    /// <summary>
    /// Turns publishing on for this process: every change its store writes is pushed from now on. <paramref name="background"/>
    /// sends off the writer's thread (the dashboard: a write on its UI thread never waits on the pipe); otherwise as the write
    /// returns (a CLI command, which exits straight after; the daemon, whose hub is in its own process).
    /// </summary>
    public static void Enable(bool background)
    {
        inBackground = background;
        StoreChanges.Changed = OnChanged;
    }

    /// <summary>Turns publishing off (tests).</summary>
    internal static void Disable() => StoreChanges.Changed = null;

    /// <summary>The kind a change is, or null when it's none of these.</summary>
    internal static string? KindOf(StoreChange change) => change.Store is null
        ? change.Name switch { StoreChanges.Members => Members, StoreChanges.Orgs => Orgs, _ => null }
        : change.Name switch { JsonStore.SettingsName => Settings, GlobalPause.StateName => Pause, ReminderBook.Name => Reminders, _ => null };

    private static void OnChanged(StoreChange change)
    {
        if (KindOf(change) is not { } kind) return;
        if (!inBackground)
        {
            Publish(kind, change.Db, change.Store);
            return;
        }
        // One sender at a time, in order, each kind read once however many writes came meanwhile: the read is the newest.
        lock (gate)
        {
            waiting[kind + "|" + change.Db.Path] = (kind, change);
            if (draining) return;
            draining = true;
        }
        _ = Task.Run(Drain);
    }

    private static void Drain()
    {
        while (true)
        {
            List<(string Kind, StoreChange Change)> batch;
            lock (gate)
            {
                if (waiting.Count == 0) { draining = false; return; }
                batch = [.. waiting.Values];
                waiting.Clear();
            }
            foreach (var (kind, change) in batch) Publish(kind, change.Db, change.Store);
        }
    }

    /// <summary>Publishes each of these kinds as <paramref name="store"/> holds them now: the daemon at its start, so a dashboard
    /// connecting gets them all, changed while it was down or not. Never throws.</summary>
    public static void PublishAll(JsonStore store)
    {
        foreach (var kind in Kinds) Publish(kind, store.Db, store);
    }

    /// <summary>Reads <paramref name="kind"/> from the store and publishes it; whether it reached the daemon. One that can't be
    /// read now is skipped (never sent as empty). Never throws.</summary>
    public static bool Publish(string kind, OrchestratorDb db, JsonStore? store)
    {
        try
        {
            object? payload = Read(kind, db, store);
            return payload is not null && AppBusClient.Publish(kind, payload, Version);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            log.Debug(ex, "Couldn't publish {Kind:l}", kind);
            return false;
        }
    }

    /// <summary><paramref name="kind"/>'s payload as the store holds it now; null when it can't be read.</summary>
    internal static object? Read(string kind, OrchestratorDb db, JsonStore? store)
    {
        var at = DateTime.UtcNow;
        var name = StoreOf(db);
        try
        {
            switch (kind)
            {
                case Members:
                    return new MembersPayload { Store = name, AtUtc = at, Members = MemberRoster.Load(db) };
                case Orgs:
                    var orgs = db.LoadOrgs();
                    return new OrgsPayload { Store = name, AtUtc = at, Orgs = orgs.Orgs, Members = orgs.Members };
                case Settings:
                    return store is not null && store.TryLoadSettings(out var settings)
                        ? new SettingsPayload { Store = name, AtUtc = at, Settings = settings } : null;
                case Pause:
                    return store is not null && store.TryLoad<PauseState>(GlobalPause.StateName, out var pause)
                        ? new PausePayload { Store = name, AtUtc = at, Paused = pause.Paused, SinceUtc = pause.SinceUtc, By = pause.By } : null;
                case Reminders:
                    return store is not null && store.TryLoad<List<Reminder>>(ReminderBook.Name, out var all)
                        ? new RemindersPayload { Store = name, AtUtc = at, Pending = [.. all.Where(r => r.Pending).OrderBy(r => r.DueUtc)] } : null;
                default:
                    return null;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Data.Common.DbException or InvalidDataException or JsonException or InvalidOperationException)
        {
            log.Debug(ex, "Couldn't read {Kind:l} to publish it", kind);
            return null;
        }
    }
}

/// <summary>What every <see cref="StateBus"/> payload carries: the store it was read from, and when.</summary>
public abstract record StatePayload
{
    /// <summary>The database it was read from (<see cref="StateBus.StoreOf(OrchestratorDb)"/>): a subscriber applies only its own's.</summary>
    [JsonPropertyName("store")] public string Store { get; init; } = "";

    /// <summary>When it was read back, right after the change.</summary>
    [JsonPropertyName("atUtc")] public DateTime AtUtc { get; init; }
}

/// <summary>The <see cref="StateBus.Members"/> payload: every member, by email.</summary>
public sealed record MembersPayload : StatePayload
{
    [JsonPropertyName("members")] public IReadOnlyList<Member> Members { get; init; } = [];
}

/// <summary>The <see cref="StateBus.Orgs"/> payload: every org (the default one first) and every login placed in one.</summary>
public sealed record OrgsPayload : StatePayload
{
    [JsonPropertyName("orgs")] public IReadOnlyList<Organization> Orgs { get; init; } = [];
    [JsonPropertyName("members")] public IReadOnlyList<OrgMember> Members { get; init; } = [];

    /// <summary>The orgs as the store reads them.</summary>
    public OrgDirectory ToDirectory() => new(Orgs, Members);
}

/// <summary>The <see cref="StateBus.Settings"/> payload: the whole of the settings.</summary>
public sealed record SettingsPayload : StatePayload
{
    [JsonPropertyName("settings")] public OrchestratorSettings Settings { get; init; } = new();
}

/// <summary>The <see cref="StateBus.Pause"/> payload: the global pause, on or off, since when and who turned it on.</summary>
public sealed record PausePayload : StatePayload
{
    [JsonPropertyName("paused")] public bool Paused { get; init; }
    [JsonPropertyName("sinceUtc")] public DateTime? SinceUtc { get; init; }
    [JsonPropertyName("by")] public string? By { get; init; }
}

/// <summary>The <see cref="StateBus.Reminders"/> payload: every reminder not sent yet, soonest first.</summary>
public sealed record RemindersPayload : StatePayload
{
    [JsonPropertyName("pending")] public IReadOnlyList<Reminder> Pending { get; init; } = [];
}
