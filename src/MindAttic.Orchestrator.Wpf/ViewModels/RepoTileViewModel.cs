using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Media;
using MindAttic.Components.Wpf.Mvvm;
using MindAttic.Orchestrator.Sessions;
using MindAttic.Tickets.Log;

namespace MindAttic.Orchestrator.Wpf.ViewModels;

/// <summary>A repo's card in the Repos view: its state, open tickets, today's tokens, and the hosted tabs, sessions and
/// tickets its detail lists.</summary>
public sealed class RepoTileViewModel(string name) : ObservableObject
{
    private RepoStatus? status;
    private DateTime nowUtc;
    private bool isSelected;

    /// <summary>The repo's folder name, the card's sync key.</summary>
    public string Name { get; } = name;
    /// <summary>The repo's status from the latest snapshot; null before the first one.</summary>
    public RepoStatus? Status => status;
    /// <summary>The repo's folder, empty when it has none (or no status yet).</summary>
    public string Path => status?.Path ?? "";
    /// <summary>The repo has a folder to open and start a session in.</summary>
    public bool HasFolder => Path.Length > 0;
    /// <summary>Working, waiting or dormant; dormant with no status yet.</summary>
    public RepoState State => status?.State ?? RepoState.Dormant;
    /// <summary>How many of the repo's tickets are open.</summary>
    public int OpenTicketCount => status?.OpenTickets.Count ?? 0;
    /// <summary>The repo's tokens today, all kinds; 0 with no status yet.</summary>
    public long TokensToday => status?.UsageToday.Total ?? 0;

    /// <summary>The card's colour for its state, and for whether it has open tickets.</summary>
    public Brush Accent => Palette.For(State, OpenTicketCount > 0);

    /// <summary>The card's state in words: "Working", "Needs you", "Has tickets" or "Quiet".</summary>
    public string StateLabel => State switch
    {
        RepoState.Working => "Working",
        RepoState.Waiting => "Needs you",
        _ => OpenTicketCount > 0 ? "Has tickets" : "Quiet",
    };

    /// <summary>One line that says what you'd want to know before clicking.</summary>
    public string Subtitle
    {
        get
        {
            if (status is null) return "";
            var parts = new List<string>();
            if (OpenTicketCount > 0) parts.Add(OpenTicketCount == 1 ? "1 open ticket" : $"{OpenTicketCount} open tickets");
            if (status.Hosts.Count > 0)
                parts.Add(string.Join(", ", status.Hosts.GroupBy(h => h.Provider).Select(g => g.Count() == 1 ? $"{g.Key} tab" : $"{g.Count()} {g.Key} tabs")));
            if (status.LastActivityUtc is { } last) parts.Add(Formatting.Ago(last, nowUtc));
            return parts.Count == 0 ? "No activity on record" : string.Join(" · ", parts);
        }
    }

    /// <summary>Today's tokens formatted ("1.2M"), empty at none.</summary>
    public string TokensText => TokensToday == 0 ? "" : Formatting.Tokens(TokensToday);

    /// <summary>The detail pane's headline: what this repo is doing and for how long.</summary>
    public string Headline
    {
        get
        {
            var since = status?.LastActivityUtc is { } last ? Formatting.Span(nowUtc - last) : null;
            return State switch
            {
                RepoState.Working => "An agent is working right now.",
                RepoState.Waiting when since is not null && since != "now" => $"Waiting on you for {since}.",
                RepoState.Waiting => "A session is open and waiting on you.",
                // Under a minute Span says "now", which read "now ago" (M.ORCH-T463).
                _ when since == "now" => "Quiet. Last agent activity just now.",
                _ when since is not null => $"Quiet. Last agent activity {since} ago.",
                _ => "No agent activity on record.",
            };
        }
    }

    /// <summary>The card is the one selected: its detail shows.</summary>
    public bool IsSelected { get => isSelected; set => Set(ref isSelected, value); }

    /// <summary>The repo's hosted CLI tabs, keyed by pipe name.</summary>
    public ObservableCollection<HostRowViewModel> Hosts { get; } = [];
    /// <summary>The repo's eight most recent transcript sessions.</summary>
    public ObservableCollection<SessionRowViewModel> Sessions { get; } = [];
    /// <summary>The repo's open tickets.</summary>
    public ObservableCollection<TicketRowViewModel> Tickets { get; } = [];

    /// <summary>Today's uncached input tokens, formatted.</summary>
    public string UsageInput => Formatting.Tokens(status?.UsageToday.Input ?? 0);
    /// <summary>Today's cache-write tokens, formatted.</summary>
    public string UsageCacheWrite => Formatting.Tokens(status?.UsageToday.CacheCreation ?? 0);
    /// <summary>Today's cache-read tokens, formatted.</summary>
    public string UsageCacheRead => Formatting.Tokens(status?.UsageToday.CacheRead ?? 0);
    /// <summary>Today's output tokens, formatted.</summary>
    public string UsageOutput => Formatting.Tokens(status?.UsageToday.Output ?? 0);
    /// <summary>The repo used tokens today: the usage breakdown shows.</summary>
    public bool HasUsage => TokensToday > 0;

    /// <summary><paramref name="search"/> is blank, or found (any case) in the repo's name or an open ticket's id or
    /// summary.</summary>
    public bool Matches(string search) =>
        string.IsNullOrWhiteSpace(search) ||
        Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
        (status?.OpenTickets.Any(t => t.Id.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                                       t.Summary.Contains(search, StringComparison.OrdinalIgnoreCase)) ?? false);

    /// <summary>Takes the repo's latest status: syncs its hosts, sessions and tickets in place, then raises every
    /// property.</summary>
    public void Update(RepoStatus next, DateTime snapshotUtc)
    {
        status = next;
        nowUtc = snapshotUtc;

        Hosts.SyncBy(next.Hosts, h => h.PipeName, r => r.PipeName, h => new HostRowViewModel(h), (r, h) => r.Update(h));
        Sessions.SyncBy(next.Sessions.Take(8), s => s.SessionId, r => r.SessionId,
            s => new SessionRowViewModel(s, snapshotUtc), (r, s) => r.Update(s, snapshotUtc));
        Tickets.SyncBy(next.OpenTickets, t => t.Id, r => r.Id, t => new TicketRowViewModel(t), (r, t) => r.Update(t));

        // Everything derived from the status changes together.
        Raise(string.Empty);
    }

    /// <summary>The status unchanged, only the snapshot's time moved (M.ORCH-T2195): just what reads it is raised.</summary>
    public void Tick(DateTime snapshotUtc)
    {
        if (nowUtc == snapshotUtc) return;
        nowUtc = snapshotUtc;
        foreach (var session in Sessions) session.Tick(snapshotUtc);
        Raise(nameof(Subtitle));
        Raise(nameof(Headline));
    }
}

/// <summary>One hosted CLI tab in a repo's detail: its process and login, and a box to type a line into it.</summary>
public sealed class HostRowViewModel : ObservableObject
{
    private HostSession host;
    /// <summary>The text typed into the row's box, sent by <see cref="SendCommand"/>.</summary>
    private string draft = "";
    /// <summary>What the last send or "Go to tab" said under the row.</summary>
    private string feedback = "";

    /// <summary>A row for <paramref name="host"/>, with Send wired to type its draft into the tab.</summary>
    public HostRowViewModel(HostSession host)
    {
        this.host = host;
        SendCommand = new RelayCommand(async () => await SendAsync(), () => !Sending && !IsMoving && !string.IsNullOrWhiteSpace(Draft));
    }

    /// <summary>Its CLI is moving to another login (M.ORCH-T637): Send and Go to tab are off until the move lands.</summary>
    public bool IsMoving => PendingMoves.ForHost(host.ProcessId) is not null;
    /// <summary>Not held by a move: Send and Go to tab are on.</summary>
    public bool IsUnlocked => !IsMoving;
    /// <summary>What the move holding it says (M.ORCH-T637), empty when it isn't moving.</summary>
    public string MovingText => PendingMoves.ForHost(host.ProcessId)?.Text ?? "";
    /// <summary>Re-reads <see cref="IsMoving"/>, <see cref="IsUnlocked"/> and <see cref="MovingText"/> after a move starts
    /// or lands.</summary>
    public void RaiseMoving() { Raise(nameof(IsMoving)); Raise(nameof(IsUnlocked)); Raise(nameof(MovingText)); }

    /// <summary>The tab's named pipe, the row's sync key and where Send types.</summary>
    public string PipeName => host.PipeName;
    /// <summary>The tab itself, for "Go to tab" (M.ORCH-T619).</summary>
    public HostSession Host => host;
    /// <summary>"Claude tab": the row's heading, its provider.</summary>
    public string Title => $"{host.Provider} tab";
    /// <summary>"PID 1234 · a@b.com": the tab's process and the Claude login its CLI bills (M.ORCH-T606), when known.</summary>
    public string Detail => host.Login is { } l ? $"PID {host.ProcessId} · {l}" : $"PID {host.ProcessId}";
    /// <summary>The email behind the login's name (M.ORCH-T614); null when the login isn't known.</summary>
    public string? DetailTip => host.LoginEmail is { } e ? $"Bills the Claude login {e}" : null;
    /// <summary>The text typed into the row's box, sent by <see cref="SendCommand"/>; cleared once sent.</summary>
    public string Draft { get => draft; set => Set(ref draft, value); }
    /// <summary>"Sent.", or why the send or "Go to tab" didn't land; empty until one is tried.</summary>
    public string Feedback { get => feedback; private set => Set(ref feedback, value); }
    /// <summary>Types <see cref="Draft"/> into the tab and presses Enter; off while sending, moving or with nothing
    /// typed.</summary>
    public ICommand SendCommand { get; }

    /// <summary>Says under the row why "Go to tab" didn't land on the tab itself.</summary>
    internal void ShowFeedback(string note) => Feedback = note;

    /// <summary>Takes the tab's latest record and re-reads its detail and tip (the pipe name, its key, doesn't
    /// change).</summary>
    public void Update(HostSession next)
    {
        host = next;
        Raise(nameof(Detail));
        Raise(nameof(DetailTip));
    }

    /// <summary>Set while a send is under way: Enter follows the text a second later, and a second click in that time
    /// would type the text into the tab again.</summary>
    internal bool Sending { get; private set; }

    internal async Task SendAsync()
    {
        if (Sending) return;
        Sending = true;
        try
        {
            var text = Draft;
            var ok = await SessionControl.SubmitAsync(host.PipeName, text);
            if (ok) Draft = "";
            Feedback = ok ? "Sent." : "Couldn't reach that tab; it may have closed.";
        }
        finally
        {
            Sending = false;
            CommandManager.InvalidateRequerySuggested();
        }
    }
}

/// <summary>One transcript session in a repo's detail: its id, model, last write and tokens today.</summary>
public sealed class SessionRowViewModel(TranscriptSession session, DateTime nowUtc) : ObservableObject
{
    private TranscriptSession session = session;
    private DateTime nowUtc = nowUtc;

    /// <summary>The session's full id.</summary>
    public string SessionId => session.SessionId;
    /// <summary>The session id's first eight characters, as the row shows it.</summary>
    public string ShortId => session.SessionId.Length > 8 ? session.SessionId[..8] : session.SessionId;
    /// <summary>The model it last ran on, empty when unknown.</summary>
    public string Model => session.Model ?? "";
    /// <summary>How long ago its transcript was last written ("3m ago"), as of the snapshot.</summary>
    public string LastWrite => Formatting.Ago(session.LastWriteUtc, nowUtc);
    /// <summary>Its tokens today formatted, empty at none.</summary>
    public string Tokens => session.UsageToday.Total == 0 ? "" : Formatting.Tokens(session.UsageToday.Total);

    /// <summary>Takes the session's latest record and the snapshot's time, and raises every property.</summary>
    public void Update(TranscriptSession next, DateTime snapshotUtc)
    {
        session = next;
        nowUtc = snapshotUtc;
        Raise(string.Empty);
    }

    /// <summary>Only the snapshot's time moved (M.ORCH-T2195): its "ago" is raised.</summary>
    public void Tick(DateTime snapshotUtc)
    {
        nowUtc = snapshotUtc;
        Raise(nameof(LastWrite));
    }
}

/// <summary>One open ticket in a repo's detail, which "Use as prompt" turns into the start prompt.</summary>
public sealed class TicketRowViewModel(Ticket ticket) : ObservableObject
{
    private Ticket ticket = ticket;

    /// <summary>The ticket's id.</summary>
    public string Id => ticket.Id;
    /// <summary>The ticket's type, as the tickets database names it.</summary>
    public string Type => ticket.Type;
    /// <summary>The ticket's one-line title.</summary>
    public string Summary => ticket.Summary;
    /// <summary>The ticket's date, as the tickets database gives it.</summary>
    public string Date => ticket.Date;

    /// <summary>Held by a CLI moving to another login (M.ORCH-T637): "Use as prompt" is off until the move lands.</summary>
    public bool IsMoving => PendingMoves.ForTicket(Id) is not null;
    /// <summary>Not held by a move: "Use as prompt" is on.</summary>
    public bool IsUnlocked => !IsMoving;
    /// <summary>What the move holding it says (M.ORCH-T637), empty when it isn't moving.</summary>
    public string MovingText => PendingMoves.ForTicket(Id)?.Text ?? "";
    /// <summary>Re-reads <see cref="IsMoving"/>, <see cref="IsUnlocked"/> and <see cref="MovingText"/> after a move starts
    /// or lands.</summary>
    public void RaiseMoving() { Raise(nameof(IsMoving)); Raise(nameof(IsUnlocked)); Raise(nameof(MovingText)); }

    /// <summary>Takes the ticket's latest record and raises every property.</summary>
    public void Update(Ticket next)
    {
        ticket = next;
        Raise(string.Empty);
    }
}
