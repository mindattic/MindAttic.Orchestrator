using MindAttic.Orchestrator.Settings;
using Serilog.Events;

namespace MindAttic.Orchestrator.Reminders;

/// <summary>
/// The daemon's side of reminders (M.ORCH-T477). Each tick, every reminder due (its time passed, including while the
/// machine slept or the daemon was off) is texted through Psst and marked sent; one Psst couldn't send is counted and tried
/// again next tick. When Psst isn't installed or can't send, or a reminder for the owner finds no owner's number, the
/// reminders wait and you're alerted once, not every tick; sent ones are dropped after <see cref="ReminderBook.KeepSent"/>.
/// </summary>
public sealed class ReminderSender(JsonStore store, PsstClient psst, Action<LogEventLevel, string> say, Action<string, string, bool> alert)
{
    /// <summary>The lease one daemon holds to send them: two daemons would text each reminder twice.</summary>
    public const string LeaseName = "reminders.lock";

    /// <summary>A reminder sent this long after its time is said to be late (the machine was asleep, the daemon off).</summary>
    internal static readonly TimeSpan Late = TimeSpan.FromMinutes(5);

    private string? problem;          // what's keeping due reminders from being sent, as last said
    private bool unreadable;
    private readonly HashSet<string> alertedFailures = new(StringComparer.OrdinalIgnoreCase);
    private DateTime prunedUtc;
    private DateTime heldUntilUtc;    // after psst hung: the tick isn't held up by it again until then

    /// <summary>After <c>psst</c> times out (an SMTP or network stall), how long before it's run again: each try holds the
    /// daemon's tick for up to <see cref="PsstClient.SendTimeout"/>, and the gates go stale past a few minutes of it.</summary>
    internal static readonly TimeSpan AfterTimeout = TimeSpan.FromMinutes(10);

    public void Tick(DateTime nowUtc)
    {
        if (!store.TryLoad<List<Reminder>>(ReminderBook.Name, out var all))
        {
            if (!unreadable) say(LogEventLevel.Warning, $"reminders: {ReminderBook.PathOf(store)} couldn't be read; none are sent until it can be");
            unreadable = true;
            return;
        }
        unreadable = false;

        var due = all.Where(r => r.Pending && r.DueUtc <= nowUtc).OrderBy(r => r.DueUtc).ToList();
        if (due.Count == 0)
        {
            Clear();
            PruneIfDue(all, nowUtc);
            return;
        }

        if (nowUtc < heldUntilUtc) return;
        var status = psst.Probe();
        if (status.TimedOut) heldUntilUtc = nowUtc + AfterTimeout;
        if (!status.Installed || !status.Configured)
        {
            Report(status.Problem ?? "Psst can't send texts", due.Count);
            return;
        }

        var done = new Dictionary<string, Reminder>(StringComparer.OrdinalIgnoreCase);
        var ownerless = 0;
        foreach (var r in due)
        {
            if ((r.To ?? status.OwnerNumber) is not { } to) { ownerless++; continue; }
            var (sent, detail, timedOut) = psst.Send(to, r.Message);
            if (sent)
            {
                done[r.Id] = r with { SentUtc = nowUtc, LastError = null };
                alertedFailures.Remove(r.Id);
                say(LogEventLevel.Information, $"reminder {r.Id} texted to {r.ToLabel}: {r.Text}" +
                    (nowUtc - r.DueUtc > Late ? $" (due {Formatting.ResetTime(r.DueUtc, nowUtc)}; sent late, the machine was asleep or the daemon off)" : ""));
            }
            else
            {
                done[r.Id] = r with { Failures = r.Failures + 1, LastError = detail };
                // Hung (it may even have gone): the rest would hang as well, so none is tried again for a while.
                if (timedOut) heldUntilUtc = nowUtc + AfterTimeout;
                var again = timedOut ? $"in {AfterTimeout.TotalMinutes:0} minutes" : "next tick";
                say(LogEventLevel.Warning, $"reminder {r.Id}: Psst couldn't text it to {r.ToLabel} ({detail}); trying again {again}");
                if (alertedFailures.Add(r.Id))
                    alert("Reminder not sent", $"Psst couldn't text your reminder to {r.ToLabel} ({detail}): {r.Text}. Trying again until it goes.", false);
                if (timedOut) break;
            }
        }
        if (ownerless > 0)
            Report("Psst has no number of its own ('to' in its settings), so a reminder without --to has nowhere to go", ownerless);
        else Clear();

        if (done.Count > 0)
            try
            {
                store.Update<List<Reminder>>(ReminderBook.Name, list =>
                {
                    // By id, on the file as it is now: one added or cancelled meanwhile (the CLI) is kept as it is.
                    for (var i = 0; i < list.Count; i++)
                        if (done.TryGetValue(list[i].Id, out var now)) list[i] = now;
                    Prune(list, nowUtc);
                });
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
            {
                // Sent but not marked: said, since the next tick would text it again.
                say(LogEventLevel.Warning, $"reminders: couldn't save which were sent ({ex.Message}); they may be texted again");
            }
    }

    private void Report(string why, int count)
    {
        if (why == problem) return;
        problem = why;
        var line = $"{count} reminder{(count == 1 ? " is" : "s are")} due but can't be texted: {why}. {(count == 1 ? "It's" : "They're")} sent once that's fixed.";
        say(LogEventLevel.Warning, $"reminders: {line}");
        alert("Reminder can't be sent", line, true);
    }

    private void Clear()
    {
        if (problem is null) return;
        problem = null;
        say(LogEventLevel.Information, "reminders: Psst can send them again");
    }

    /// <summary>Once a day with nothing to send, sent reminders past <see cref="ReminderBook.KeepSent"/> are dropped.</summary>
    private void PruneIfDue(List<Reminder> all, DateTime nowUtc)
    {
        if (nowUtc - prunedUtc < TimeSpan.FromDays(1)) return;
        prunedUtc = nowUtc;
        if (!all.Any(r => Old(r, nowUtc))) return;
        try { store.Update<List<Reminder>>(ReminderBook.Name, list => Prune(list, nowUtc)); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException) { }
    }

    private static void Prune(List<Reminder> list, DateTime nowUtc) => list.RemoveAll(r => Old(r, nowUtc));

    private static bool Old(Reminder r, DateTime nowUtc) => r.SentUtc is { } sent && nowUtc - sent > ReminderBook.KeepSent;
}
