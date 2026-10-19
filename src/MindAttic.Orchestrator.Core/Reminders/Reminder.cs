using MindAttic.Orchestrator.Settings;
using System.Globalization;

namespace MindAttic.Orchestrator.Reminders;

/// <summary>
/// One reminder (M.ORCH-T477): <see cref="Text"/> texted through Psst at <see cref="DueUtc"/> to <see cref="To"/> (a Psst
/// contact or a US number; null is the owner, Psst's own configured number). <see cref="SentUtc"/> is set once it's sent;
/// a failed send is counted and tried again at the daemon's next tick.
/// </summary>
public sealed record Reminder
{
    public string Id { get; init; } = "";
    public string Text { get; init; } = "";
    public DateTime DueUtc { get; init; }
    public string? To { get; init; }
    public DateTime CreatedUtc { get; init; }
    public DateTime? SentUtc { get; init; }
    public int Failures { get; init; }
    public string? LastError { get; init; }

    public bool Pending => SentUtc is null;

    /// <summary>Who it goes to, as said back: the contact or number, or "you" for the owner.</summary>
    public string ToLabel => To ?? "you";

    /// <summary>The text Psst sends: prefixed, so a reminder reads as one on the phone and never starts like a psst flag.</summary>
    public string Message => $"Reminder: {Text}";
}

/// <summary>
/// The reminders the daemon owns, in one file (<c>reminders.json</c> in the orchestrator's store) rather than a Task
/// Scheduler task each, as <c>psst sms --schedule</c> makes: so they're listed and cancelled in one place, and a reminder
/// whose time passed while the machine slept or the daemon was off is sent at the daemon's next tick.
/// </summary>
public static class ReminderBook
{
    public const string Name = "reminders";

    /// <summary>How long a sent reminder stays in the file before the daemon drops it.</summary>
    public static readonly TimeSpan KeepSent = TimeSpan.FromDays(30);

    public static string PathOf(JsonStore store) => Path.Combine(store.Directory, Name + ".json");

    /// <summary>Saves a new reminder (under the store's lock, so one added while the daemon marks another sent isn't lost).</summary>
    public static Reminder Add(JsonStore store, string text, DateTime dueUtc, string? to, DateTime nowUtc)
    {
        Reminder added = null!;
        store.Update<List<Reminder>>(Name, list =>
        {
            string id;
            do id = Guid.NewGuid().ToString("N")[..6];
            while (list.Any(r => r.Id.Equals(id, StringComparison.OrdinalIgnoreCase)));
            added = new Reminder
            {
                Id = id,
                Text = text.Trim(),
                DueUtc = dueUtc,
                To = string.IsNullOrWhiteSpace(to) ? null : to.Trim(),
                CreatedUtc = nowUtc,
            };
            list.Add(added);
        });
        return added;
    }

    /// <summary>Removes the pending reminder <paramref name="id"/> (any case); false when there's none.</summary>
    public static bool Cancel(JsonStore store, string id)
    {
        var found = false;
        store.Update<List<Reminder>>(Name, list =>
            found = list.RemoveAll(r => r.Pending && r.Id.Equals(id.Trim(), StringComparison.OrdinalIgnoreCase)) > 0);
        return found;
    }

    /// <summary>The reminders not sent yet, soonest first; none when the file is missing or can't be read.</summary>
    public static IReadOnlyList<Reminder> Pending(JsonStore store) =>
        [.. store.Load<List<Reminder>>(Name).Where(r => r.Pending).OrderBy(r => r.DueUtc)];

    /// <summary>"9:00 AM" today, "Tue 9:00 AM" this week, "Tue Nov 3 9:00 AM" past that, and "overdue since ..." once it's past.</summary>
    public static string When(Reminder r, DateTime nowUtc) =>
        r.DueUtc <= nowUtc ? $"overdue since {Formatting.ResetTime(r.DueUtc, nowUtc)}"
        : r.DueUtc - nowUtc > TimeSpan.FromDays(6) ? r.DueUtc.ToLocalTime().ToString("ddd MMM d h:mm tt", CultureInfo.InvariantCulture)
        : Formatting.ResetTime(r.DueUtc, nowUtc);

    /// <summary>One line for the dashboard and the list: when, to whom, what, and why it hasn't gone if a send failed.</summary>
    public static string Describe(Reminder r, DateTime nowUtc) =>
        $"{When(r, nowUtc)} → {r.ToLabel}: {r.Text}" +
        (r.Failures > 0 ? $" (not sent: {r.LastError ?? "failed"}; tried {r.Failures}×)" : "");
}
