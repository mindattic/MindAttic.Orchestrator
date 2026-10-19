using MindAttic.Orchestrator.Reminders;

namespace MindAttic.Orchestrator.Wpf.ViewModels;

/// <summary>The pending reminders (M.ORCH-T477), read from the file the daemon sends them from, shown in the Queue view.</summary>
public sealed partial class MainViewModel
{
    private IReadOnlyList<string> reminders = [];

    public IReadOnlyList<string> Reminders { get => reminders; private set => Set(ref reminders, value); }
    public bool HasReminders => Reminders.Count > 0;
    public string RemindersTitle => Reminders.Count == 1 ? "1 reminder pending" : $"{Reminders.Count} reminders pending";

    private void ApplyReminders(DateTime nowUtc)
    {
        var lines = ReminderLines(ReminderBook.Pending(store), nowUtc);
        if (lines.SequenceEqual(Reminders)) return;
        Reminders = lines;
        Raise(nameof(HasReminders));
        Raise(nameof(RemindersTitle));
    }

    internal static IReadOnlyList<string> ReminderLines(IEnumerable<Reminder> pending, DateTime nowUtc) =>
        [.. pending.Select(r => $"{r.Id}  {ReminderBook.Describe(r, nowUtc)}")];
}
