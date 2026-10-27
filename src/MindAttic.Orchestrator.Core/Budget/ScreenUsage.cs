using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using MindAttic.Orchestrator.Sessions;

namespace MindAttic.Orchestrator.Budget;

/// <summary>
/// Claude's usage figures read off a hosted CLI's screen (M.ORCH-T1118): Claude Code's <c>/usage</c> shows the running 5-hour
/// window ("Current session ... N% used / Resets 2:30pm (America/Chicago)") and the week ("Current week (all models) ... N% used
/// / Resets Oct 29, 4pm (America/Chicago)"). Every hosted CLI's output already streams to the dashboard through its TermHost's
/// relay, so a figure the CLI shows is written as its login's fresh reading the moment it's drawn, rather than at the
/// statusline's next write or the meter probe's next ask (Amy's tab read wk 86% while her /usage showed 87%).
/// </summary>
public static class ScreenUsage
{
    /// <summary>What the readings this writes are tagged with in <c>claude-usage.json</c>'s <c>source</c> and on the status stream.</summary>
    public const string Source = "screen";

    /// <summary>Slack past a window's length a reset may lie ahead: the screen's reset is to the minute, in local time.</summary>
    private static readonly TimeSpan Slack = TimeSpan.FromHours(1);

    private static readonly TimeSpan WeekLength = TimeSpan.FromDays(7);

    // CSI (cursor moves, erases, colors), OSC (titles, links) and the rest of the two-byte escapes.
    private static readonly Regex Sgr = new(@"\x1b\[[0-9;:]*m", RegexOptions.Compiled);
    private static readonly Regex Csi = new(@"\x1b\[[0-?]*[ -/]*[@-~]", RegexOptions.Compiled);
    private static readonly Regex Osc = new(@"\x1b\][^\x07\x1b]*(?:\x07|\x1b\\)?", RegexOptions.Compiled);
    private static readonly Regex Esc = new(@"\x1b(?:[()*+][0-9A-Za-z]|[@-Z\\-_=>78])", RegexOptions.Compiled);

    // A block: its header, its percent used, then its reset and the IANA zone it's said in. Bounded gaps, so a header whose
    // figures aren't drawn yet can't borrow the next block's.
    private const string Figures = @")(?<before>[^%]{0,160}?)(?<pct>\d{1,3}(?:\.\d+)?)\s*%\s*used(?:(?<between>[\s\S]{0,80}?)Resets\s+(?<reset>[^()\r\n]{1,40}?)\s*\((?<zone>[A-Za-z_]+(?:/[A-Za-z_+\-]+){0,2})\))?";
    private static readonly Regex Session = new(@"(?<head>Current\s+session" + Figures, RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex Week = new(@"(?<head>Current\s+week\s*\(\s*all\s+models\s*\)" + Figures, RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex Header = new(@"Current\s+(?:session|week)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex Reset = new(
        @"^(?:(?<mon>[A-Za-z]{3,9})\.?\s+(?<day>\d{1,2})(?:,?\s+(?<year>\d{4}))?(?:\s*,|\s+at)?\s+)?(?<h>\d{1,2})(?::(?<m>\d{2}))?\s*(?<ap>am|pm)?$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary><paramref name="text"/> without its terminal escapes: colors go, every other control sequence (a cursor move, an
    /// erase) becomes a space, so words the renderer placed apart stay apart.</summary>
    public static string Strip(string text)
    {
        if (text.IndexOf('\u001b') < 0) return text;
        text = Osc.Replace(text, " ");
        text = Sgr.Replace(text, "");
        text = Csi.Replace(text, " ");
        return Esc.Replace(text, " ");
    }

    /// <summary>
    /// The latest "Current session" and "Current week (all models)" figures in <paramref name="text"/> (escapes and all), each
    /// with its reset as UTC; null when neither block is there whole. A per-model week ("Current week (Fable)") is left out. A
    /// window whose reset is missing, unreadable, already past or further ahead than the window is long is dropped: what it says
    /// is from another time (a stale screen) or misread.
    /// </summary>
    public static ReportedUsage? Parse(string text, DateTime nowUtc)
    {
        if (string.IsNullOrEmpty(text)) return null;
        var plain = Strip(text);
        if (plain.IndexOf("used", StringComparison.OrdinalIgnoreCase) < 0) return null;
        var five = Last(Session, plain, nowUtc, BudgetCalculator.BlockLength);
        var week = Last(Week, plain, nowUtc, WeekLength);
        return five is null && week is null ? null : new ReportedUsage(five, week, nowUtc);
    }

    private static ReportedWindow? Last(Regex block, string plain, DateTime nowUtc, TimeSpan length)
    {
        Match? last = null;
        for (var m = block.Match(plain); m.Success; m = m.NextMatch()) last = m;
        if (last is null || !last.Groups["reset"].Success) return null;
        // Another block's header inside this one's figures: they're that block's, not this one's.
        if (Header.IsMatch(last.Groups["before"].Value) || Header.IsMatch(last.Groups["between"].Value)) return null;
        if (!double.TryParse(last.Groups["pct"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var percent) || percent > 100) return null;
        if (ResetUtc(last.Groups["reset"].Value, last.Groups["zone"].Value, nowUtc) is not { } reset) return null;
        return reset > nowUtc && reset - nowUtc <= length + Slack ? new ReportedWindow(percent, reset) : null;
    }

    /// <summary>
    /// A reset as the screen says it ("2:30pm", "Oct 29, 4pm", "Oct 29 at 4pm"), in <paramref name="zone"/> (an IANA id), as UTC;
    /// null when it can't be read. A time alone is its next occurrence; a date without a year is the year that puts it no more
    /// than a day behind <paramref name="nowUtc"/>.
    /// </summary>
    public static DateTime? ResetUtc(string text, string zone, DateTime nowUtc)
    {
        try
        {
            var m = Reset.Match(Regex.Replace(text.Trim(), @"\s+", " "));
            if (!m.Success || Zone(zone) is not { } tz) return null;
            var hour = int.Parse(m.Groups["h"].Value, CultureInfo.InvariantCulture);
            var minute = m.Groups["m"].Success ? int.Parse(m.Groups["m"].Value, CultureInfo.InvariantCulture) : 0;
            if (m.Groups["ap"].Success)
            {
                if (hour is < 1 or > 12) return null;
                var pm = m.Groups["ap"].Value.Equals("pm", StringComparison.OrdinalIgnoreCase);
                hour = hour % 12 + (pm ? 12 : 0);
            }
            if (hour > 23 || minute > 59) return null;
            var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc), tz);
            if (!m.Groups["mon"].Success)
            {
                var at = local.Date.AddHours(hour).AddMinutes(minute);
                if (at <= local) at = at.AddDays(1);
                return ToUtc(at, tz);
            }
            if (Month(m.Groups["mon"].Value) is not { } month) return null;
            var day = int.Parse(m.Groups["day"].Value, CultureInfo.InvariantCulture);
            var year = m.Groups["year"].Success ? int.Parse(m.Groups["year"].Value, CultureInfo.InvariantCulture) : local.Year;
            if (day < 1 || day > DateTime.DaysInMonth(year, month)) return null;
            var when = new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Unspecified);
            if (!m.Groups["year"].Success && when < local.AddDays(-1) && day <= DateTime.DaysInMonth(year + 1, month)) when = when.AddYears(1);
            return ToUtc(when, tz);
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException or OverflowException) { return null; }
    }

    private static DateTime? ToUtc(DateTime local, TimeZoneInfo tz) =>
        tz.IsInvalidTime(local) ? null : TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), tz);

    private static int? Month(string name)
    {
        var n = name.Length >= 3 ? name[..3] : name;
        for (var i = 0; i < 12; i++)
            if (CultureInfo.InvariantCulture.DateTimeFormat.AbbreviatedMonthNames[i].Equals(n, StringComparison.OrdinalIgnoreCase)) return i + 1;
        return null;
    }

    /// <summary>The zone an IANA id names (Windows reads them on .NET 6+), or its Windows id's; null when neither is known.</summary>
    internal static TimeZoneInfo? Zone(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        if (id.Equals("UTC", StringComparison.OrdinalIgnoreCase)) return TimeZoneInfo.Utc;
        try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException) { }
        try { return TimeZoneInfo.TryConvertIanaIdToWindowsId(id, out var win) ? TimeZoneInfo.FindSystemTimeZoneById(win) : null; }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException) { return null; }
    }

    /// <summary>
    /// What to write for <paramref name="screen"/> over <paramref name="was"/> (the login's reading now, which must be
    /// <paramref name="account"/>'s: one tagged with another account's counts as none); null when the screen says nothing new.
    /// The screen never replaces a newer reading: per window, a running one already there is kept unless the screen shows the
    /// same window higher (usage never falls within a window, and the screen's whole percent is coarser than the meter's); a
    /// window the screen doesn't show, or shows differently from one still running, keeps what's there.
    /// </summary>
    public static ReportedUsage? Merge(ReportedUsage? was, ReportedUsage screen, string? account, DateTime nowUtc)
    {
        if (was?.Account is { } tag && tag != account) was = null;
        var (five, fiveNew) = Window(ReportedUsage.Usable(was?.FiveHour, nowUtc), ReportedUsage.Usable(screen.FiveHour, nowUtc));
        var (week, weekNew) = Window(ReportedUsage.Usable(was?.Week, nowUtc), ReportedUsage.Usable(screen.Week, nowUtc));
        return fiveNew || weekNew ? new ReportedUsage(five, week, nowUtc) { Account = account } : null;

        static (ReportedWindow?, bool) Window(ReportedWindow? old, ReportedWindow? seen)
        {
            if (seen is null) return (old, false);
            if (old is null) return (seen, true);
            // Same window, higher: the screen's percent on the meter's exact reset.
            if (Status.MeterReading.SameWindow(old.ResetUtc, seen.ResetUtc) && seen.Percent > old.Percent) return (old with { Percent = seen.Percent }, true);
            return (old, false);
        }
    }

    /// <summary>Raised as each screen reading is written (on the writer's thread): the login's email (null for the global login)
    /// and the reading as written. The dashboard lays it over the login's tab at once, as a meter probe's.</summary>
    public static event Action<string?, ReportedUsage>? Landed;

    /// <summary>
    /// Writes <paramref name="screen"/> as the reading of the login a pane runs on, where it says something new
    /// (<see cref="Merge"/>), tagged with the account signed in there and <see cref="Source"/>, through the meter probe's own
    /// writer: a per-CLI login's (<paramref name="email"/>) to its <c>claude-usage.json</c>; the global login's (no email, no
    /// config folder) to the probe's row, as its probe does. Then tells the daemon (it publishes the reading and rebuilds the
    /// pace and stops now) and raises <see cref="Landed"/>. Returns what was written; null when nothing was (an unknown login, an
    /// API key, nothing new, or a failure, which is logged). Never throws. Without <paramref name="tell"/> (tests) only writes.
    /// </summary>
    public static ReportedUsage? Record(string? email, string? configDir, ReportedUsage screen, DateTime nowUtc, AccountDirs? dirs = null, bool tell = true)
    {
        try
        {
            string path;
            string? account;
            Settings.OrchestratorDb? db = null;
            if (AccountDirs.Normalize(email) is { } e)
            {
                var dir = (dirs ?? new AccountDirs()).For(e);
                if (!Directory.Exists(dir.Directory)) return null;
                (path, account) = (dir.UsagePath, dir.Account);
            }
            else if (string.IsNullOrWhiteSpace(email) && string.IsNullOrWhiteSpace(configDir))
            {
                // The global login, as its meter probe reads it (StatusAggregator): its account from ~/.claude.json, its reading in the row.
                (path, account, db) = (ReportedUsage.DefaultPath, ClaudeAccount.Current(new ClaudeAccount().ConfigPath), ReportedUsage.DefaultDb);
                email = null;
            }
            else return null;
            if (account is null || account == ClaudeAccount.ApiKey) return null;
            var was = ReportedUsage.Load(nowUtc, path, db);
            if (Merge(was, screen, account, nowUtc) is not { } reading) return null;
            var written = ClaudeMeterProbe.Write(path, reading, nowUtc, account, db, Source);
            Say(Serilog.Events.LogEventLevel.Information,
                $"screen usage [{email ?? "global login"}]: 5-hour {written.FiveHour?.Percent:0.#}%, week {written.Week?.Percent:0.#}%");
            if (!tell) return written;
            Tell(path, email);
            try { Landed?.Invoke(email, written); }
            catch (Exception ex) when (ex is not OutOfMemoryException) { Say(Serilog.Events.LogEventLevel.Warning, $"screen usage: telling the dashboard failed: {ex.Message}"); }
            return written;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Say(Serilog.Events.LogEventLevel.Warning, $"screen usage [{email ?? "global login"}]: not written: {ex.GetType().Name}: {ex.Message}");
            return null;
        }
    }

    /// <summary>The daemon's inbox: the usage file was written (it reads it and publishes it to every dashboard) and a recount is
    /// due (the global login's row has no file to watch, so its pace follows from the snapshot this brings forward).</summary>
    private static void Tell(string path, string? email)
    {
        Status.StatusInbox.TrySend(Status.StatusEnvelope.Of(Status.StatusInbox.UsageWritten, new { path, email }));
        Status.StatusInbox.TrySend(Status.StatusEnvelope.Of(Status.StatusInbox.Poke, new { why = "screen usage" }));
    }

    private static void Say(Serilog.Events.LogEventLevel level, string line)
    {
        try { OrchestratorLog.For("ScreenUsage").Write(level, "{Line:l}", line); }
        catch (Exception) { }
    }
}

/// <summary>
/// One pane's watch for usage figures on its screen (M.ORCH-T1118). <see cref="Feed"/> is called with each chunk of output as it
/// is decoded (on the relay's reader thread) and only appends to a small rolling tail; at most every <see cref="Every"/> the tail
/// is parsed on the thread pool, so a figure split across chunks is still read whole and the renderer is never slowed. Each new
/// reading goes to <c>found</c> once. Nothing here throws to its caller.
/// </summary>
public sealed class ScreenUsageScanner(Action<ReportedUsage> found, Func<DateTime>? clock = null) : IDisposable
{
    /// <summary>The most text kept: a whole /usage screen, its escapes included, with room to spare.</summary>
    public const int TailChars = 24 * 1024;

    /// <summary>How often, at most, the tail is parsed while output arrives.</summary>
    public static readonly TimeSpan Every = TimeSpan.FromMilliseconds(400);

    private readonly object gate = new();
    private readonly StringBuilder tail = new();
    private readonly Func<DateTime> now = clock ?? (() => DateTime.UtcNow);
    private Timer? timer;
    private bool armed, disposed;
    private int partial;
    private ReportedUsage? last;

    /// <summary>Takes a chunk of output. Cheap: an append, and a timer armed when none is.</summary>
    public void Feed(string chunk)
    {
        if (string.IsNullOrEmpty(chunk)) return;
        lock (gate)
        {
            if (disposed) return;
            tail.Append(chunk);
            if (tail.Length > TailChars) tail.Remove(0, tail.Length - TailChars);
            if (armed) return;
            armed = true;
            timer ??= new Timer(_ => Tick(), null, Timeout.Infinite, Timeout.Infinite);
            timer.Change(Every, Timeout.InfiniteTimeSpan);
        }
    }

    private void Tick()
    {
        try { Scan(); }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            try { OrchestratorLog.For("ScreenUsage").Debug("A screen scan failed: {Message:l}", ex.Message); } catch (Exception) { }
        }
    }

    /// <summary>Parses the tail now (the timer's work; tests call it directly). A session block with no week after it yet is
    /// given two more looks for the week to be drawn; a whole reading clears the tail, so the next screen is read on its own.</summary>
    internal void Scan()
    {
        string text;
        lock (gate)
        {
            armed = false;
            if (disposed) return;
            text = tail.ToString();
        }
        if (text.IndexOf("Current", StringComparison.OrdinalIgnoreCase) < 0) return;
        if (ScreenUsage.Parse(text, now()) is not { } reading) return;
        lock (gate)
        {
            if (reading.Week is null && partial++ < 2)
            {
                // The week may be in the next chunk: look again shortly.
                armed = true;
                timer?.Change(Every, Timeout.InfiniteTimeSpan);
                return;
            }
            partial = 0;
            tail.Clear();
            if (last is not null && last.FiveHour == reading.FiveHour && last.Week == reading.Week) return;
            last = reading;
        }
        found(reading);
    }

    public void Dispose()
    {
        lock (gate)
        {
            disposed = true;
            timer?.Dispose();
            timer = null;
            tail.Clear();
        }
    }
}
