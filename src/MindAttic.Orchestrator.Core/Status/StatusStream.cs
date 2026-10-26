using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Channels;
using MindAttic.Orchestrator.Budget;

namespace MindAttic.Orchestrator.Status;

/// <summary>
/// One login's reading of Claude's meter as it landed (M.ORCH-T854): its 5-hour and weekly percent used, their resets, and
/// whether its login is signed in. Percents are carried at full double precision; only the dashboard rounds them, for display.
/// </summary>
public sealed record MeterReading
{
    /// <summary>The per-CLI login's email; null for the global login (~/.claude).</summary>
    public string? Email { get; init; }

    /// <summary>The account the reading was tagged with (the meter probe tags its own); null from a statusline.</summary>
    public string? Account { get; init; }

    public double? FiveHourPercent { get; init; }
    public DateTime? FiveHourResetUtc { get; init; }
    public double? WeekPercent { get; init; }
    public DateTime? WeekResetUtc { get; init; }

    /// <summary>When Claude's meter said it.</summary>
    public DateTime ReadUtc { get; init; }

    /// <summary>Whether the login is signed in here; null when the writer doesn't know.</summary>
    public bool? SignedIn { get; init; }

    /// <summary>Who learned it: "probe" (the meter probe asked Claude), "statusline" (a CLI's statusline wrote it), "state".</summary>
    public string Source { get; init; } = "";

    /// <summary>The key a login's readings are kept under: its email, case-insensitively; "" for the global login.</summary>
    [JsonIgnore]
    public string Key => KeyOf(Email);

    public static string KeyOf(string? email) => email?.Trim().ToLowerInvariant() ?? "";

    /// <summary><paramref name="usage"/> as a reading of <paramref name="email"/>'s login; null when it carries neither window.</summary>
    public static MeterReading? Of(string? email, ReportedUsage? usage, bool? signedIn, string source) =>
        usage is null || (usage.FiveHour is null && usage.Week is null) ? null : new MeterReading
        {
            Email = email,
            Account = usage.Account,
            FiveHourPercent = usage.FiveHour?.Percent,
            FiveHourResetUtc = usage.FiveHour?.ResetUtc,
            WeekPercent = usage.Week?.Percent,
            WeekResetUtc = usage.Week?.ResetUtc,
            ReadUtc = usage.AtUtc,
            SignedIn = signedIn,
            Source = source,
        };

    /// <summary>
    /// <paramref name="next"/> laid over <paramref name="was"/>, the same login's: usage never falls within a window, so a lower
    /// percent for the same window (an idle CLI's statusline restamping its last reply's percent with the current time) keeps the
    /// higher one; a new window takes the new reading whatever it says. An older reading changes nothing but the signed-in state.
    /// </summary>
    public static MeterReading Merge(MeterReading? was, MeterReading next)
    {
        if (was is null) return next;
        if (next.ReadUtc < was.ReadUtc) return next.SignedIn is { } s && s != was.SignedIn ? was with { SignedIn = s } : was;
        var (five, fiveReset) = Window(was.FiveHourPercent, was.FiveHourResetUtc, next.FiveHourPercent, next.FiveHourResetUtc);
        var (week, weekReset) = Window(was.WeekPercent, was.WeekResetUtc, next.WeekPercent, next.WeekResetUtc);
        return next with
        {
            FiveHourPercent = five, FiveHourResetUtc = fiveReset, WeekPercent = week, WeekResetUtc = weekReset,
            SignedIn = next.SignedIn ?? was.SignedIn, Account = next.Account ?? was.Account,
        };

        static (double?, DateTime?) Window(double? wasPercent, DateTime? wasReset, double? percent, DateTime? reset)
        {
            if (percent is null) return (wasPercent, wasReset);
            if (wasPercent is { } p && SameWindow(wasReset, reset) && p > percent) return (p, reset ?? wasReset);
            return (percent, reset ?? wasReset);
        }
    }

    /// <summary>Whether two resets name the same window: the meter's and the statusline's come back a few seconds apart.</summary>
    public static bool SameWindow(DateTime? a, DateTime? b) => a is { } x && b is { } y && Math.Abs((x - y).TotalMinutes) < 2;

    /// <summary>Whether <paramref name="next"/> says anything <paramref name="was"/> didn't (so it's worth sending).</summary>
    public static bool Differs(MeterReading? was, MeterReading next) =>
        was is null || was.FiveHourPercent != next.FiveHourPercent || was.FiveHourResetUtc != next.FiveHourResetUtc ||
        was.WeekPercent != next.WeekPercent || was.WeekResetUtc != next.WeekResetUtc || was.SignedIn != next.SignedIn ||
        was.ReadUtc != next.ReadUtc;
}

/// <summary>
/// The payload of the stream's own status kinds (M.ORCH-T854): <see cref="State"/>, <see cref="Usage"/>, <see cref="Snapshot"/> and
/// <see cref="Heartbeat"/>, each version <see cref="Version"/>. <see cref="Kind"/>, <see cref="AtUtc"/> and <see cref="Pid"/> are the
/// envelope's (M.ORCH-T872), filled in as it's read (<see cref="From"/>).
/// </summary>
public sealed record StatusMessage
{
    /// <summary>The full current state, sent first on every connection so a dashboard is never stale.</summary>
    public const string State = "state";

    /// <summary>One or more logins' readings just landed.</summary>
    public const string Usage = "usage";

    /// <summary>The status lease holder published a new snapshot (<see cref="SnapshotFile"/>) whose figures weren't pushed: read it now.</summary>
    public const string Snapshot = "snapshot";

    /// <summary>Nothing new; says the writer is alive, so a hung one is noticed.</summary>
    public const string Heartbeat = "heartbeat";

    public const int Version = 1;

    [JsonIgnore] public string Kind { get; init; } = "";
    [JsonIgnore] public DateTime AtUtc { get; init; }

    /// <summary>The writing process.</summary>
    [JsonIgnore] public int Pid { get; init; }

    public IReadOnlyList<MeterReading> Readings { get; init; } = [];

    /// <summary>When the newest snapshot the writer built was taken; null while it has built none (another process holds the lease).</summary>
    public DateTime? SnapshotTakenUtc { get; init; }

    /// <summary>This message in its envelope.</summary>
    public StatusEnvelope ToEnvelope() => StatusEnvelope.Of(Kind, this, Version) with
    {
        AtUtc = AtUtc == default ? DateTime.UtcNow : AtUtc, Pid = Pid == 0 ? Environment.ProcessId : Pid,
    };

    /// <summary><paramref name="payload"/>, read from <paramref name="envelope"/>, with the envelope's kind, time and writer.</summary>
    public static StatusMessage From(StatusEnvelope envelope, StatusMessage payload) =>
        payload with { Kind = envelope.Kind, AtUtc = envelope.AtUtc, Pid = envelope.Pid };

    /// <summary>Whether <paramref name="kind"/> is one of these.</summary>
    public static bool IsStatusKind(string kind) => kind is State or Usage or Snapshot or Heartbeat;
}

/// <summary>
/// The status stream (M.ORCH-T854): a long-lived, one-way named pipe from the daemon to every open dashboard, one JSON message a
/// line (as <see cref="Sessions.PaneBroker"/>'s pipe carries its requests). Readable only by this Windows user.
/// </summary>
public static class StatusStream
{
    /// <summary>The daemon's pipe; one per Windows user, as each has their own daemon and dashboard.</summary>
    public static string PipeName { get; } = "mindattic-orchestrator-status-" +
        new string(Environment.UserName.Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray());

    /// <summary>How often the server says it's alive while nothing else is sent.</summary>
    public static readonly TimeSpan HeartbeatEvery = TimeSpan.FromSeconds(20);

    /// <summary>A stream silent this long is taken as dead (its writer hung) and dropped for a new connection.</summary>
    public static readonly TimeSpan SilentFor = TimeSpan.FromSeconds(65);

    /// <summary>The daemon's inbox (M.ORCH-T872): writers (the hooks, the statusline) connect, write a line or two, and go.</summary>
    public static string InboxPipeName { get; } = PipeName.Replace("-status-", "-status-in-", StringComparison.Ordinal);

    /// <summary>The longest line read or relayed: a payload past it is dropped, never buffered whole.</summary>
    public const int MaxLine = 4 * 1024 * 1024;

    /// <summary>How payloads are written: as the published snapshot is (NaN and infinities named, read-only sets read back),
    /// names read case-insensitively, nulls left out. Doubles round-trip exactly.</summary>
    public static readonly JsonSerializerOptions PayloadJson = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
        Converters = { new SnapshotFile.ReadOnlySetConverter() },
    };

    private static readonly JsonSerializerOptions EnvelopeJson = new() { PropertyNameCaseInsensitive = true };

    public static string Serialize(StatusEnvelope envelope) => JsonSerializer.Serialize(envelope, EnvelopeJson);

    public static string Serialize(StatusMessage message) => Serialize(message.ToEnvelope());

    /// <summary>One line read back; null when it isn't an envelope. Throws <see cref="JsonException"/> on one that isn't JSON.</summary>
    public static StatusEnvelope? Deserialize(string line) =>
        JsonSerializer.Deserialize<StatusEnvelope>(line, EnvelopeJson) is { Kind.Length: > 0 } e ? e : null;

    /// <summary>A status kind's message read back from <paramref name="line"/>; null when it's another kind.</summary>
    public static StatusMessage? DeserializeMessage(string line) =>
        Deserialize(line) is { } e && StatusMessage.IsStatusKind(e.Kind) && e.PayloadAs<StatusMessage>() is { } m ? StatusMessage.From(e, m) : null;
}

/// <summary>
/// The daemon's end of the status stream (M.ORCH-T854). Keeps every login's latest reading, the newest snapshot's time and its
/// figures, sends them whole to each dashboard as it connects, then each change the moment it's published: a meter probe's
/// reading (<see cref="PublishReadings"/>), a statusline's (the usage files it watches, looked at the moment a writer says it
/// wrote one on the inbox, <see cref="UsageWritten"/>, and every <see cref="WatchEvery"/> for a writer that doesn't say), a new
/// snapshot's figures (<see cref="PublishFigures"/>), and any other kind (<see cref="Publish"/>). Nothing here throws into its
/// caller: a failure is logged and the stream carries on.
/// </summary>
public sealed class StatusStreamServer : IDisposable
{
    private readonly Serilog.ILogger log = OrchestratorLog.For("StatusStream");
    private readonly object gate = new();
    private readonly Dictionary<string, MeterReading> readings = new(StringComparer.Ordinal);
    private readonly List<Channel<string>> clients = [];
    private readonly CancellationTokenSource stop = new();
    // The latest message of each retained kind (the figures, M.ORCH-T872), sent after the state to each dashboard as it connects.
    private readonly Dictionary<string, string> retained = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim written = new(0);
    private DateTime? snapshotTakenUtc;
    private int instances;

    /// <summary>How often the usage files are looked at with no writer saying it wrote one (M.ORCH-T872: was every second; a
    /// statusline or hook that says so on the inbox is read at once).</summary>
    public TimeSpan WatchEvery { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>A writer said it wrote a usage file (the inbox's <c>usage</c> kind): look at them now. Never throws.</summary>
    public void UsageWritten()
    {
        try { if (written.CurrentCount == 0) written.Release(); }
        catch (Exception ex) when (ex is ObjectDisposedException or SemaphoreFullException) { }
    }

    /// <summary>The pipe served; <see cref="StatusStream.PipeName"/> by default.</summary>
    public string PipeName { get; init; } = StatusStream.PipeName;

    /// <summary>The usage files to watch, each with its login's email (null for the global login's) and whether it's signed in:
    /// a statusline or a per-CLI login's probe writing one is a reading landing. Read again every <see cref="FilesEvery"/>.</summary>
    public Func<IReadOnlyList<WatchedUsage>>? Files { get; init; }

    /// <summary>Reads a watched file's reading; tests swap it.</summary>
    internal Func<WatchedUsage, DateTime, ReportedUsage?> ReadUsage { get; init; } = (w, now) => w.Email is null
        ? ReportedUsage.Load(now)
        : ReportedUsage.Load(now, w.Path);

    /// <summary>Called (off the lock) whenever a published reading changed the state: the daemon brings its next snapshot
    /// forward, so the pace and budget built from the reading follow it within seconds.</summary>
    public Action? ReadingsChanged { get; init; }

    /// <summary>How often the list of watched files is read again (a login signed in or out).</summary>
    internal TimeSpan FilesEvery { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>One usage file the server watches.</summary>
    public sealed record WatchedUsage(string? Email, string Path, bool? SignedIn);

    /// <summary>Starts serving and watching, on the thread pool, until disposed.</summary>
    public void Start()
    {
        _ = Task.Run(() => ServeAsync(stop.Token));
        _ = Task.Run(() => WatchAsync(stop.Token));
        _ = Task.Run(() => BeatAsync(stop.Token));
    }

    /// <summary>The full current state, as a connecting dashboard gets it first.</summary>
    public StatusMessage Current()
    {
        lock (gate) return CurrentLocked();
    }

    private StatusMessage CurrentLocked() => new()
    {
        Kind = StatusMessage.State, AtUtc = DateTime.UtcNow, Pid = Environment.ProcessId,
        Readings = [.. readings.Values], SnapshotTakenUtc = snapshotTakenUtc,
    };

    /// <summary>Takes each reading into the state (<see cref="MeterReading.Merge"/>) and sends the ones that changed it.</summary>
    public void PublishReadings(IEnumerable<MeterReading?> landed)
    {
        try
        {
            List<MeterReading> changed = [];
            lock (gate)
            {
                foreach (var r in landed)
                {
                    if (r is null) continue;
                    var was = readings.GetValueOrDefault(r.Key);
                    var merged = MeterReading.Merge(was, r);
                    if (!MeterReading.Differs(was, merged)) continue;
                    readings[r.Key] = merged;
                    changed.Add(merged);
                }
                if (changed.Count == 0) return;
                BroadcastLocked(new StatusMessage { Kind = StatusMessage.Usage, AtUtc = DateTime.UtcNow, Pid = Environment.ProcessId, Readings = changed, SnapshotTakenUtc = snapshotTakenUtc });
            }
            ReadingsChanged?.Invoke();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            log.Warning(ex, "Couldn't publish a reading on the status stream");
        }
    }

    /// <summary>Says a new snapshot taken at <paramref name="takenUtc"/> was published, for every dashboard to read now. The
    /// daemon sends its figures instead (<see cref="PublishFigures"/>); this is for a snapshot whose figures aren't to hand.</summary>
    public void PublishSnapshot(DateTime takenUtc)
    {
        try
        {
            lock (gate)
            {
                if (snapshotTakenUtc is { } was && was >= takenUtc) return;
                snapshotTakenUtc = takenUtc;
                BroadcastLocked(new StatusMessage { Kind = StatusMessage.Snapshot, AtUtc = DateTime.UtcNow, Pid = Environment.ProcessId, SnapshotTakenUtc = takenUtc });
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            log.Warning(ex, "Couldn't publish a snapshot on the status stream");
        }
    }

    /// <summary>
    /// Pushes <paramref name="snapshot"/>'s figures (M.ORCH-T872): every dashboard lays them over what it shows at once, and reads
    /// the published file only when the rest of it moved. Kept, so a dashboard connecting later gets them straight after the state.
    /// An older snapshot than the last one sent is dropped.
    /// </summary>
    public void PublishFigures(Snapshot snapshot)
    {
        try
        {
            lock (gate)
                if (snapshotTakenUtc is { } was && was >= snapshot.TakenUtc) return;
            // Off the lock: the digest serializes the snapshot.
            var line = StatusStream.Serialize(StatusEnvelope.Of(SnapshotFigures.Kind, SnapshotFigures.Of(snapshot), SnapshotFigures.Version));
            lock (gate)
            {
                if (snapshotTakenUtc is { } was && was >= snapshot.TakenUtc) return;
                snapshotTakenUtc = snapshot.TakenUtc;
                retained[SnapshotFigures.Kind] = line;
                BroadcastLocked(line);
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            log.Warning(ex, "Couldn't publish a snapshot's figures on the status stream");
        }
    }

    /// <summary>
    /// Sends <paramref name="envelope"/> to every dashboard: the way any other kind goes out (M.ORCH-T879's ticket events among
    /// them). With <paramref name="retain"/>, the latest of its kind is kept and sent to each dashboard as it connects, after the
    /// state. Never throws.
    /// </summary>
    public void Publish(StatusEnvelope envelope, bool retain = false)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(envelope.Kind)) return;
            var line = StatusStream.Serialize(envelope);
            if (line.Length > StatusStream.MaxLine) { log.Warning("A {Kind:l} status message of {Length} characters was too long to send", envelope.Kind, line.Length); return; }
            lock (gate)
            {
                if (retain) retained[envelope.Kind] = line;
                BroadcastLocked(line);
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            log.Warning(ex, "Couldn't publish a {Kind:l} message on the status stream", envelope.Kind);
        }
    }

    /// <summary>The latest message of <paramref name="kind"/> kept for connecting dashboards; null when none is.</summary>
    public string? Retained(string kind)
    {
        lock (gate) return retained.GetValueOrDefault(kind);
    }

    /// <summary>How many dashboards are connected now.</summary>
    public int Clients
    {
        get { lock (gate) return clients.Count; }
    }

    private void BroadcastLocked(StatusMessage message) => BroadcastLocked(StatusStream.Serialize(message));

    private void BroadcastLocked(string line)
    {
        // Bounded and dropping the oldest: a dashboard that stops reading can't make the daemon hold its messages forever.
        foreach (var c in clients) c.Writer.TryWrite(line);
    }

    private async Task ServeAsync(CancellationToken ct)
    {
        var warned = false;
        while (!ct.IsCancellationRequested)
        {
            NamedPipeServerStream? pipe = null;
            try
            {
                // The first instance claims the name, so a second daemon (an `orchestrator daemon` beside the logon one) doesn't
                // split the dashboards between two streams; it serves once the first is gone.
                var first = Interlocked.CompareExchange(ref instances, 0, 0) == 0 ? PipeOptions.FirstPipeInstance : PipeOptions.None;
                pipe = new NamedPipeServerStream(PipeName, PipeDirection.Out, NamedPipeServerStream.MaxAllowedServerInstances,
                    PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly | first);
                Interlocked.Increment(ref instances);
                warned = false;
                try { await pipe.WaitForConnectionAsync(ct).ConfigureAwait(false); }
                catch
                {
                    Interlocked.Decrement(ref instances);
                    throw;
                }
            }
            catch (OperationCanceledException) { pipe?.Dispose(); return; }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                pipe?.Dispose();
                if (!warned) log.Information("The status stream's pipe is taken or couldn't listen ({Message:l}); retrying every 10s", ex.Message);
                warned = true;
                try { await Task.Delay(TimeSpan.FromSeconds(10), ct).ConfigureAwait(false); } catch (OperationCanceledException) { return; }
                continue;
            }
            _ = Task.Run(() => FeedAsync(pipe, ct), ct);
        }
    }

    /// <summary>Feeds one dashboard: the full state first, then every message from then on, until it goes away.</summary>
    private async Task FeedAsync(NamedPipeServerStream pipe, CancellationToken ct)
    {
        // A figures message runs to tens of KB: 128 lines is half an hour of ticks behind before the oldest go.
        var channel = Channel.CreateBounded<string>(new BoundedChannelOptions(128) { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true });
        lock (gate)
        {
            // Under the lock, so nothing published between the state and the subscription is lost or sent out of order.
            channel.Writer.TryWrite(StatusStream.Serialize(CurrentLocked()));
            foreach (var line in retained.Values) channel.Writer.TryWrite(line);
            clients.Add(channel);
        }
        try
        {
            await using (pipe.ConfigureAwait(false))
            {
                using var writer = new StreamWriter(pipe, new UTF8Encoding(false), leaveOpen: true);
                await foreach (var line in channel.Reader.ReadAllAsync(ct).ConfigureAwait(false))
                {
                    await writer.WriteLineAsync(line.AsMemory(), ct).ConfigureAwait(false);
                    await writer.FlushAsync(ct).ConfigureAwait(false);
                }
            }
        }
        // The dashboard closed (or the daemon is stopping): its pipe is gone, nothing to say.
        catch (Exception ex) when (ex is IOException or OperationCanceledException or ObjectDisposedException) { }
        catch (Exception ex) when (ex is not OutOfMemoryException) { log.Warning(ex, "A status stream connection failed"); }
        finally
        {
            lock (gate) clients.Remove(channel);
            Interlocked.Decrement(ref instances);
        }
    }

    private async Task BeatAsync(CancellationToken ct)
    {
        try
        {
            using var timer = new PeriodicTimer(StatusStream.HeartbeatEvery);
            while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
                lock (gate)
                    BroadcastLocked(new StatusMessage { Kind = StatusMessage.Heartbeat, AtUtc = DateTime.UtcNow, Pid = Environment.ProcessId, SnapshotTakenUtc = snapshotTakenUtc });
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) when (ex is not OutOfMemoryException) { log.Warning(ex, "The status stream's heartbeat stopped"); }
    }

    /// <summary>Each watched usage file whose write time moved is read and its reading published: at once when a writer says
    /// on the inbox that it wrote one (<see cref="UsageWritten"/>), and every <see cref="WatchEvery"/> for one that doesn't.</summary>
    private async Task WatchAsync(CancellationToken ct)
    {
        if (Files is null) return;
        IReadOnlyList<WatchedUsage> files = [];
        var listedUtc = DateTime.MinValue;
        var stamps = new Dictionary<string, (DateTime, bool?)>(StringComparer.OrdinalIgnoreCase);
        string? lastError = null;
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var now = DateTime.UtcNow;
                if (now - listedUtc >= FilesEvery)
                {
                    files = Files();
                    listedUtc = now;
                }
                List<MeterReading?> landed = [];
                foreach (var f in files)
                {
                    var stamp = (File.Exists(f.Path) ? File.GetLastWriteTimeUtc(f.Path) : default, f.SignedIn);
                    if (stamps.TryGetValue(f.Path, out var was) && was == stamp) continue;
                    stamps[f.Path] = stamp;
                    landed.Add(MeterReading.Of(f.Email, ReadUsage(f, now), f.SignedIn, "statusline")
                        ?? (f.SignedIn is null ? null : new MeterReading { Email = f.Email, SignedIn = f.SignedIn, Source = "state" }));
                }
                if (landed.Count > 0) PublishReadings(landed);
                lastError = null;
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                // Said once while it keeps failing the same way, not every second.
                if (ex.Message != lastError) log.Warning(ex, "Watching the usage files for the status stream failed; retrying");
                lastError = ex.Message;
            }
            try { await written.WaitAsync(WatchEvery, ct).ConfigureAwait(false); } catch (OperationCanceledException) { return; }
        }
    }

    public void Dispose()
    {
        stop.Cancel();
        lock (gate)
            foreach (var c in clients) c.Writer.TryComplete();
    }
}

/// <summary>
/// A subscriber's end of the status stream (M.ORCH-T854): connects, hands every envelope to <c>onEnvelope</c> (a
/// <see cref="StatusBus"/>'s <see cref="StatusBus.Dispatch"/>, marshalled to the caller's UI thread), and when the daemon goes
/// away (restarted, upgraded, hung) connects again on its own, getting the full state again first. Every failure is contained
/// and logged; nothing it does can throw into the subscriber. Any number of subscribers may run at once, each its own app.
/// </summary>
public static class StatusStreamClient
{
    private static readonly Serilog.ILogger log = OrchestratorLog.For("StatusStream");

    /// <summary>The stream's own status kinds only (M.ORCH-T854's shape): every other kind is skipped.</summary>
    public static Task RunAsync(Action<StatusMessage> onMessage, Action<bool> connected, CancellationToken ct, string? pipeName = null) =>
        RunAsync((StatusEnvelope e) =>
        {
            if (StatusMessage.IsStatusKind(e.Kind) && e.PayloadAs<StatusMessage>() is { } m) onMessage(StatusMessage.From(e, m));
        }, connected, ct, pipeName);

    /// <summary>Runs until <paramref name="ct"/> ends. <paramref name="connected"/> is told each time the stream comes up or goes
    /// down; both callbacks run on the stream's thread, so the caller marshals to its UI thread.</summary>
    public static async Task RunAsync(Action<StatusEnvelope> onEnvelope, Action<bool> connected, CancellationToken ct, string? pipeName = null)
    {
        var wait = TimeSpan.FromSeconds(1);
        string? lastError = null;
        while (!ct.IsCancellationRequested)
        {
            var up = false;
            try
            {
                using var pipe = new NamedPipeClientStream(".", pipeName ?? StatusStream.PipeName, PipeDirection.In, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await pipe.ConnectAsync(2000, ct).ConfigureAwait(false);
                up = true;
                wait = TimeSpan.FromSeconds(1);
                if (lastError is not null) log.Information("The status stream is connected again");
                lastError = null;
                Notify(() => connected(true));
                using var reader = new StreamReader(pipe, new UTF8Encoding(false));
                while (!ct.IsCancellationRequested)
                {
                    // Bounded by the heartbeat: a daemon that hung with its pipe open is dropped and connected to again.
                    using var silent = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    silent.CancelAfter(StatusStream.SilentFor);
                    string? line;
                    try { line = await reader.ReadLineAsync(silent.Token).ConfigureAwait(false); }
                    catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                    {
                        log.Warning("The status stream went silent for {Seconds}s; connecting again", StatusStream.SilentFor.TotalSeconds);
                        break;
                    }
                    if (line is null) break; // the daemon closed it: stopped or restarting
                    if (line.Length == 0) continue;
                    StatusEnvelope? envelope;
                    try { envelope = StatusStream.Deserialize(line); }
                    catch (JsonException ex)
                    {
                        log.Warning("An unreadable status stream message was skipped: {Message:l}", ex.Message);
                        continue;
                    }
                    if (envelope is not null) Notify(() => onEnvelope(envelope));
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                // No daemon yet (a timeout) is the usual case and not worth a line; anything else is said once while it repeats.
                if (ex is not TimeoutException && ex.Message != lastError)
                    log.Warning(ex, "The status stream failed; connecting again");
                lastError ??= ex.Message;
            }
            if (up) Notify(() => connected(false));
            try { await Task.Delay(wait, ct).ConfigureAwait(false); } catch (OperationCanceledException) { return; }
            wait = TimeSpan.FromSeconds(Math.Min(10, wait.TotalSeconds * 2));
        }

        static void Notify(Action action)
        {
            try { action(); }
            catch (Exception ex) when (ex is not OutOfMemoryException) { log.Error(ex, "Applying a status stream message failed"); }
        }
    }
}
