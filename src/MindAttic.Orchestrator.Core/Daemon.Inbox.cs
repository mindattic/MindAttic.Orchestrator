using MindAttic.Orchestrator.Scheduling;
using MindAttic.Orchestrator.Settings;
using MindAttic.Orchestrator.Status;
using Serilog.Events;

namespace MindAttic.Orchestrator;

/// <summary>The owner inbox's alert (M.ORCH-T1689): what newly waits on the owner is texted through Psst, once per item.</summary>
public sealed partial class Daemon
{
    private OwnerInboxAlerts? inboxAlerts;
    private DateTime inboxLookedUtc;

    /// <summary>How often the tick looks for new inbox items: two state rows are read per look, so not on every tick.</summary>
    internal static readonly TimeSpan InboxEvery = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Looks at the owner inbox (<see cref="OwnerInbox"/>) built from this tick's snapshot and plan, and texts
    /// <see cref="OrchestratorSettings.PsstAlertTo"/> one Psst message for the items that are new since the last look, each item
    /// once and never again (<see cref="OwnerInboxAlerts"/>). Off with <see cref="OrchestratorSettings.InboxAlerts"/>: new items
    /// are still noted as seen, so turning it on later texts no backlog. Skipped while a repo's tickets couldn't be read, when
    /// the list is partial. Returns the send, null when nothing was sent.
    /// </summary>
    internal Task? ReportInbox(Snapshot snapshot, SchedulePlan plan, OrchestratorSettings s, DateTime nowUtc)
    {
        if (nowUtc - inboxLookedUtc < InboxEvery || snapshot.UnknownRepos.Count > 0) return null;
        inboxLookedUtc = nowUtc;
        inboxAlerts ??= new OwnerInboxAlerts(store);
        var fresh = inboxAlerts.Fresh(OwnerInbox.Build(OwnerInbox.Read(store, snapshot, plan, s, nowUtc)), nowUtc);
        if (fresh.Count == 0) return null;
        var what = fresh.Count == 1 ? $"{fresh[0].Id} waits on you in the inbox" : $"{fresh.Count} new items wait on you in the inbox";
        Say(LogEventLevel.Information, $"inbox: new, waiting on the owner: {string.Join(", ", fresh.Select(i => $"{i.Kind} {i.Id}"))}");
        if (!s.InboxAlerts || s.PsstAlertTo is not { Length: > 0 } to || string.IsNullOrWhiteSpace(to)) return null;
        psstAlerts ??= new Reminders.PsstAlerts(new Reminders.PsstClient(Runner), (level, line) => Say(level, line));
        return psstAlerts.Send($"inbox|{fresh[0].Key}|{fresh.Count}", to, OwnerInboxAlerts.Text(fresh), what);
    }
}
