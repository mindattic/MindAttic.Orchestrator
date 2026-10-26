using System.Diagnostics;
using MindAttic.Orchestrator.Scheduling;
using MindAttic.Orchestrator.Sessions;
using MindAttic.Orchestrator.Status;

namespace MindAttic.Orchestrator.Wpf.ViewModels;

/// <summary>
/// The app bus in the view models (M.ORCH-T879): which apps listen (<see cref="AppPresences"/>), a ticket id opening in Tickets
/// Desktop over the bus when it's listening, and Tickets Desktop's "show who is working X" selecting that person and CLI tab.
/// </summary>
public sealed partial class MainViewModel
{
    /// <summary>The apps heard listening on the bus; on the global login's tab.</summary>
    private readonly AppPresences presences = new();

    /// <summary>The apps heard listening on the bus.</summary>
    internal AppPresences Presences => Root.presences;

    /// <summary>
    /// Opens <paramref name="ticketId"/> in Tickets Desktop: over the bus when it's listening for <see cref="AppBus.OpenTicket"/>
    /// (no new process, its window brought forward), else by starting it with <c>--ticket</c> as before (it starts if it isn't
    /// running and hands the ticket to the one that is). The send runs off the UI thread. Never throws.
    /// </summary>
    private void OpenInTickets(string? ticketId)
    {
        try
        {
            if (!MindAttic.Orchestrator.Tickets.TicketsDesktop.IsTicketId(ticketId) || Presences.Listening(AppBus.TicketsDesktop, AppBus.OpenTicket) is not { } tickets)
            {
                StartTickets(ticketId);
                return;
            }
            var id = ticketId!.ToUpperInvariant();
            // It may only take the foreground with leave from the app in front, which this click makes us.
            TicketsApp.AllowForeground(tickets.Pid);
            Task.Run(() => AppBusClient.Publish(AppBus.OpenTicket, new OpenTicketRequest { Id = id, From = AppBus.OrchestratorDesktop, To = AppBus.TicketsDesktop }))
                .ContinueWith(sent =>
                {
                    try
                    {
                        if (sent.Status == TaskStatus.RanToCompletion && sent.Result) return;
                        StartTickets(id);
                    }
                    catch (Exception ex) { Trace.TraceWarning($"Opening {id} in Tickets Desktop failed: {ex.Message}"); }
                }, CancellationToken.None, TaskContinuationOptions.None,
                SynchronizationContext.Current is null ? TaskScheduler.Default : TaskScheduler.FromCurrentSynchronizationContext());
        }
        catch (Exception ex)
        {
            Trace.TraceWarning($"Opening {ticketId} in Tickets Desktop over the bus failed: {ex.Message}");
            StartTickets(ticketId);
        }
    }

    private void StartTickets(string? ticketId)
    {
        if (MindAttic.Orchestrator.Tickets.TicketsDesktop.Open(ticketId, aggregator.Options) is { } error)
            Toasts.Insert(0, new ToastViewModel("Couldn't open it in Tickets Desktop", error, Palette.Over, null));
    }

    /// <summary>
    /// "Show who is working X" (<see cref="AppBus.ShowWorker"/>, from Tickets Desktop): the CLI tab in the session working it is
    /// shown; failing that, the person working it is selected. Who works it is the request's, else the plan's claim on it.
    /// Returns what was shown, or why nothing could be, for the status line: the shown tab's, the one the toolbar binds and
    /// announces, after any switch this makes (M.ORCH-T1022). Never throws.
    /// </summary>
    internal string ShowWorker(ShowWorkerRequest request)
    {
        if (!IsGlobalTab) return Root.ShowWorker(request);
        try
        {
            var id = request.Id.Trim();
            var claim = plan?.Queue.FirstOrDefault(q => string.Equals(q.Id, id, StringComparison.OrdinalIgnoreCase))?.Assignment;
            var login = AccountDirs.Normalize(request.Login ?? claim?.Account);
            var session = request.Session ?? claim?.CliSession;
            SyncTerminals();
            if (session is { Length: > 0 } && AllTerminals.Panes.FirstOrDefault(p => !p.Ended && string.Equals(p.SessionId, session, StringComparison.OrdinalIgnoreCase)) is { } pane)
            {
                ShowPane(pane);
                return Shown.StatusMessage = $"Showing the CLI working {id}.";
            }
            if (login is not null && loginTabs.FirstOrDefault(t => TicketPins.Same(t.PersonEmail, login)) is { } person)
            {
                if (person.OrgId != selectedOrgId) SelectOrg(person.OrgId);
                Select(person);
                return Shown.StatusMessage = $"{id} is worked on {person.LoginName}'s login; its CLI isn't open in this window.";
            }
            return Shown.StatusMessage = login is null && session is null ? $"Nobody is working {id} right now." : $"{id} is worked on a login with no tab here ({login ?? session}).";
        }
        catch (Exception ex)
        {
            OrchestratorLog.For("TicketBus").Error(ex, "Showing who works {Ticket:l} failed; contained", request.Id);
            return "";
        }
    }
}
