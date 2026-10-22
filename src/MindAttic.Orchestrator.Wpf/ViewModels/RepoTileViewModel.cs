using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Media;
using MindAttic.Components.Wpf.Mvvm;
using MindAttic.Orchestrator.Sessions;
using MindAttic.Tickets.Log;

namespace MindAttic.Orchestrator.Wpf.ViewModels;

public sealed class RepoTileViewModel(string name) : ObservableObject
{
    private RepoStatus? status;
    private DateTime nowUtc;
    private bool isSelected;

    public string Name { get; } = name;
    public RepoStatus? Status => status;
    public string Path => status?.Path ?? "";
    public bool HasFolder => Path.Length > 0;
    public RepoState State => status?.State ?? RepoState.Dormant;
    public int OpenTicketCount => status?.OpenTickets.Count ?? 0;
    public long TokensToday => status?.UsageToday.Total ?? 0;

    public Brush Accent => Palette.For(State, OpenTicketCount > 0);

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

    public bool IsSelected { get => isSelected; set => Set(ref isSelected, value); }

    public ObservableCollection<HostRowViewModel> Hosts { get; } = [];
    public ObservableCollection<SessionRowViewModel> Sessions { get; } = [];
    public ObservableCollection<TicketRowViewModel> Tickets { get; } = [];

    public string UsageInput => Formatting.Tokens(status?.UsageToday.Input ?? 0);
    public string UsageCacheWrite => Formatting.Tokens(status?.UsageToday.CacheCreation ?? 0);
    public string UsageCacheRead => Formatting.Tokens(status?.UsageToday.CacheRead ?? 0);
    public string UsageOutput => Formatting.Tokens(status?.UsageToday.Output ?? 0);
    public bool HasUsage => TokensToday > 0;

    public bool Matches(string search) =>
        string.IsNullOrWhiteSpace(search) ||
        Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
        (status?.OpenTickets.Any(t => t.Id.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                                       t.Summary.Contains(search, StringComparison.OrdinalIgnoreCase)) ?? false);

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
}

public sealed class HostRowViewModel : ObservableObject
{
    private HostSession host;
    private string draft = "";
    private string feedback = "";

    public HostRowViewModel(HostSession host)
    {
        this.host = host;
        SendCommand = new RelayCommand(async () => await SendAsync(), () => !Sending && !IsMoving && !string.IsNullOrWhiteSpace(Draft));
    }

    /// <summary>Its CLI is moving to another login (M.ORCH-T637): Send and Go to tab are off until the move lands.</summary>
    public bool IsMoving => PendingMoves.ForHost(host.ProcessId) is not null;
    public bool IsUnlocked => !IsMoving;
    public string MovingText => PendingMoves.ForHost(host.ProcessId)?.Text ?? "";
    public void RaiseMoving() { Raise(nameof(IsMoving)); Raise(nameof(IsUnlocked)); Raise(nameof(MovingText)); }

    public string PipeName => host.PipeName;
    /// <summary>The tab itself, for "Go to tab" (M.ORCH-T619).</summary>
    public HostSession Host => host;
    public string Title => $"{host.Provider} tab";
    /// <summary>"PID 1234 · a@b.com": the tab's process and the Claude login its CLI bills (M.ORCH-T606), when known.</summary>
    public string Detail => host.Login is { } l ? $"PID {host.ProcessId} · {l}" : $"PID {host.ProcessId}";
    /// <summary>The email behind the login's name (M.ORCH-T614); null when the login isn't known.</summary>
    public string? DetailTip => host.LoginEmail is { } e ? $"Bills the Claude login {e}" : null;
    public string Draft { get => draft; set => Set(ref draft, value); }
    public string Feedback { get => feedback; private set => Set(ref feedback, value); }
    public ICommand SendCommand { get; }

    /// <summary>Says under the row why "Go to tab" didn't land on the tab itself.</summary>
    internal void ShowFeedback(string note) => Feedback = note;

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

public sealed class SessionRowViewModel(TranscriptSession session, DateTime nowUtc) : ObservableObject
{
    private TranscriptSession session = session;
    private DateTime nowUtc = nowUtc;

    public string SessionId => session.SessionId;
    public string ShortId => session.SessionId.Length > 8 ? session.SessionId[..8] : session.SessionId;
    public string Model => session.Model ?? "";
    public string LastWrite => Formatting.Ago(session.LastWriteUtc, nowUtc);
    public string Tokens => session.UsageToday.Total == 0 ? "" : Formatting.Tokens(session.UsageToday.Total);

    public void Update(TranscriptSession next, DateTime snapshotUtc)
    {
        session = next;
        nowUtc = snapshotUtc;
        Raise(string.Empty);
    }
}

public sealed class TicketRowViewModel(Ticket ticket) : ObservableObject
{
    private Ticket ticket = ticket;

    public string Id => ticket.Id;
    public string Type => ticket.Type;
    public string Summary => ticket.Summary;
    public string Date => ticket.Date;

    /// <summary>Held by a CLI moving to another login (M.ORCH-T637): "Use as prompt" is off until the move lands.</summary>
    public bool IsMoving => PendingMoves.ForTicket(Id) is not null;
    public bool IsUnlocked => !IsMoving;
    public string MovingText => PendingMoves.ForTicket(Id)?.Text ?? "";
    public void RaiseMoving() { Raise(nameof(IsMoving)); Raise(nameof(IsUnlocked)); Raise(nameof(MovingText)); }

    public void Update(Ticket next)
    {
        ticket = next;
        Raise(string.Empty);
    }
}
