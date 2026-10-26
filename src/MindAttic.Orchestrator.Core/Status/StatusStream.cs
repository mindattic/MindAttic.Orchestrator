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

/// <summary>One line of the status stream (M.ORCH-T854).</summary>
public sealed record StatusMessage
{
    /// <summary>The full current state, sent first on every connection so a dashboard is never stale.</summary>
    public const string State = "state";

    /// <summary>One or more logins' readings just landed.</summary>
    public const string Usage = "usage";

    /// <summary>The status lease holder published a new snapshot (<see cref="SnapshotFile"/>): read it now.</summary>
    public const string Snapshot = "snapshot";

    /// <summary>Nothing new; says the writer is alive, so a hung one is noticed.</summary>
    public const string Heartbeat = "heartbeat";

    public string Kind { get; init; } = "";
    public DateTime AtUtc { get; init; }

    /// <summary>The writing process.</summary>
    public int Pid { get; init; }

    public IReadOnlyList<MeterReading> Readings { get; init; } = [];

    /// <summary>When the newest snapshot the writer built was taken; null while it has built none (another process holds the lease).</summary>
    public DateTime? SnapshotTakenUtc { get; init; }
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

    private static readonly JsonSerializerOptions Json = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true,
    };

    public static string Serialize(StatusMessage message) => JsonSerializer.Serialize(message, Json);

    public static StatusMessage? Deserialize(string line) => JsonSerializer.Deserialize<StatusMessage>(line, Json);
}

/// <summary>
/// The daemon's end of the status stream (M.ORCH-T854). Keeps every login's latest reading and the newest snapshot's time, sends
/// them whole to each dashboard as it connects, then each change the moment it's published: a meter probe's reading
/// (<see cref="PublishReadings"/>), a statusline's (the usage files it watches, checked every second), a new snapshot
/// (<see cref="PublishSnapshot"/>). Nothing here throws into its caller: a failure is logged and the stream carries on.
/// </summary>
public sealed class StatusStreamServer : IDisposable
{
    private readonly Serilog.ILogger log = OrchestratorLog.For("StatusStream");
    private readonly object gate = new();
    private readonly Dictionary<string, MeterReading> readings = new(StringComparer.Ordinal);
    private readonly List<Channel<string>> clients = [];
    private readonly CancellationTokenSource stop = new();
    private DateTime? snapshotTakenUtc;
    private int instances;

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

    /// <summary>Says a new snapshot taken at <paramref name="takenUtc"/> was published, for every dashboard to read now.</summary>
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

    /// <summary>How many dashboards are connected now.</summary>
    public int Clients
    {
        get { lock (gate) return clients.Count; }
    }

    private void BroadcastLocked(StatusMessage message)
    {
        if (clients.Count == 0) return;
        var line = StatusStream.Serialize(message);
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
        var channel = Channel.CreateBounded<string>(new BoundedChannelOptions(512) { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true });
        lock (gate)
        {
            // Under the lock, so nothing published between the state and the subscription is lost or sent out of order.
            channel.Writer.TryWrite(StatusStream.Serialize(CurrentLocked()));
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

    /// <summary>Every second, each watched usage file whose write time moved is read and its reading published: a statusline
    /// (or a per-CLI login's probe) writing one is on every dashboard within the second.</summary>
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
            try { await Task.Delay(TimeSpan.FromSeconds(1), ct).ConfigureAwait(false); } catch (OperationCanceledException) { return; }
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
/// A dashboard's end of the status stream (M.ORCH-T854): connects, hands every message to <c>onMessage</c>, and when the daemon
/// goes away (restarted, upgraded, hung) connects again on its own, getting the full state again first. Every failure is
/// contained and logged; nothing it does can throw into the dashboard.
/// </summary>
public static class StatusStreamClient
{
    private static readonly Serilog.ILogger log = OrchestratorLog.For("StatusStream");

    /// <summary>Runs until <paramref name="ct"/> ends. <paramref name="connected"/> is told each time the stream comes up or goes
    /// down; both callbacks run on the stream's thread, so the caller marshals to its UI thread.</summary>
    public static async Task RunAsync(Action<StatusMessage> onMessage, Action<bool> connected, CancellationToken ct, string? pipeName = null)
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
                    StatusMessage? message;
                    try { message = StatusStream.Deserialize(line); }
                    catch (JsonException ex)
                    {
                        log.Warning("An unreadable status stream message was skipped: {Message:l}", ex.Message);
                        continue;
                    }
                    if (message is not null) Notify(() => onMessage(message));
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
