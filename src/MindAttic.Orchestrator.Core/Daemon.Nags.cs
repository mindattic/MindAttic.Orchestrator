using System.Data.Common;
using MindAttic.Orchestrator.Reminders;
using MindAttic.Orchestrator.Settings;
using Serilog.Events;

namespace MindAttic.Orchestrator;

/// <summary>The delivery of reminders that nag (M.ORCH-T1934, on <see cref="NagBook"/> from M.ORCH-T1932).</summary>
public sealed partial class Daemon
{
    private NagBook? nagBook;
    private DateTime nagsLookedUtc;

    /// <summary>How often the tick looks for nags due: the shortest gap a nag takes is a minute (<see cref="NagSchedule.ShortestEvery"/>).</summary>
    internal static readonly TimeSpan NagsEvery = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Delivers every nag due at <paramref name="nowUtc"/>: ends each whose ticket has closed (<see cref="NagBook.Due"/>), marks
    /// each due one nagged (<see cref="NagBook.MarkNagged"/>, which sets its next time on the backoff and is what the dashboard
    /// toasts from, <see cref="NagBook.NaggedSince"/>), pushes it as an alert, and, with <see cref="OrchestratorSettings.NagAlerts"/>
    /// on, texts it to <see cref="OrchestratorSettings.PsstAlertTo"/> through Psst on its own thread. Returns the nags delivered.
    /// </summary>
    internal IReadOnlyList<Nag> ReportNags(OrchestratorSettings s, string workspaceRoot, DateTime nowUtc)
    {
        if (nowUtc - nagsLookedUtc < NagsEvery) return [];
        nagsLookedUtc = nowUtc;
        nagBook ??= new NagBook(store.Db);
        var due = nagBook.Due(nowUtc, id => TicketStatus(workspaceRoot, id),
            ended => Say(LogEventLevel.Information, $"reminder {ended.Id} ended ({ended.EndedWhy}): {ended.Message}"));
        var delivered = new List<Nag>();
        foreach (var nag in due)
        {
            // Marked first, on the row as it is now: one dismissed meanwhile (the CLI) isn't sent.
            if (nagBook.MarkNagged(nag.Id, nowUtc) is not { } saved) continue;
            delivered.Add(saved);
            Say(LogEventLevel.Information, $"reminder {saved.Id} nagged (#{saved.Nags}, next {Formatting.ResetTime(saved.NextUtc, nowUtc)}): {saved.Message}");
            Alert("Reminder", NagSchedule.Text(saved));
            if (!s.NagAlerts || s.PsstAlertTo is not { Length: > 0 } to || string.IsNullOrWhiteSpace(to)) continue;
            psstAlerts ??= new PsstAlerts(new PsstClient(Runner), (level, line) => Say(level, line));
            psstAlerts.Send($"nag|{saved.Id}|{saved.Nags}", to, $"Reminder: {NagSchedule.Text(saved)}", $"reminder {saved.Id} is due");
        }
        return delivered;
    }

    /// <summary>A ticket's status by id; null when the tickets store can't be read or has no such ticket (the nag then keeps nagging).</summary>
    private static string? TicketStatus(string workspaceRoot, string id)
    {
        try
        {
            using var db = MindAttic.Tickets.Data.TicketsDb.OpenIfMigrated(workspaceRoot);
            return Tickets.TicketQueue.Find(db, workspaceRoot, id)?.Ticket.Status;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or DbException)
        {
            return null;
        }
    }
}
