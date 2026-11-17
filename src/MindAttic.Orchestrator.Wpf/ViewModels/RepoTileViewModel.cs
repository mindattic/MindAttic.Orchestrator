using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Media;
using MindAttic.Components.Wpf.Mvvm;
using MindAttic.Orchestrator.Sessions;
using MindAttic.Tickets.Log;

namespace MindAttic.Orchestrator.Wpf.ViewModels;

/// <summary>The repo tile view model type.</summary>
public sealed class RepoTileViewModel(string name) : ObservableObject
{
    private RepoStatus? status;
    private DateTime nowUtc;
    private bool isSelected;

    /// <summary>The name.</summary>
    public string Name { get; } = name;
    /// <summary>The status.</summary>
    public RepoStatus? Status => status;
    /// <summary>The path.</summary>
    public string Path => status?.Path ?? "";
    /// <summary>Whether folder.</summary>
    public bool HasFolder => Path.Length > 0;
    /// <summary>The state.</summary>
    public RepoState State => status?.State ?? RepoState.Dormant;
    /// <summary>The open ticket count.</summary>
    public int OpenTicketCount => status?.OpenTickets.Count ?? 0;
    /// <summary>The tokens today.</summary>
    public long TokensToday => status?.UsageToday.Total ?? 0;

    /// <summary>The accent.</summary>
    public Brush Accent => Palette.For(State, OpenTicketCount > 0);

    /// <summary>The state label.</summary>
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

    /// <summary>The tokens text.</summary>
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

    /// <summary>Whether selected.</summary>
    public bool IsSelected { get => isSelected; set => Set(ref isSelected, value); }

    /// <summary>The hosts.</summary>
    public ObservableCollection<HostRowViewModel> Hosts { get; } = [];
    /// <summary>The sessions.</summary>
    public ObservableCollection<SessionRowViewModel> Sessions { get; } = [];
    /// <summary>The tickets.</summary>
    public ObservableCollection<TicketRowViewModel> Tickets { get; } = [];

    /// <summary>The usage input.</summary>
    public string UsageInput => Formatting.Tokens(status?.UsageToday.Input ?? 0);
    /// <summary>The usage cache write.</summary>
    public string UsageCacheWrite => Formatting.Tokens(status?.UsageToday.CacheCreation ?? 0);
    /// <summary>The usage cache read.</summary>
    public string UsageCacheRead => Formatting.Tokens(status?.UsageToday.CacheRead ?? 0);
    /// <summary>The usage output.</summary>
    public string UsageOutput => Formatting.Tokens(status?.UsageToday.Output ?? 0);
    /// <summary>Whether usage.</summary>
    public bool HasUsage => TokensToday > 0;

    /// <summary>Matches.</summary>
    public bool Matches(string search) =>
        string.IsNullOrWhiteSpace(search) ||
        Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
        (status?.OpenTickets.Any(t => t.Id.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                                       t.Summary.Contains(search, StringComparison.OrdinalIgnoreCase)) ?? false);

    /// <summary>Updates.</summary>
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

/// <summary>The host row view model type.</summary>
public sealed class HostRowViewModel : ObservableObject
{
    private HostSession host;
    /// <summary>The draft.</summary>
    private string draft = "";
    /// <summary>The feedback.</summary>
    private string feedback = "";

    /// <summary>Creates the host row view model.</summary>
    public HostRowViewModel(HostSession host)
    {
        this.host = host;
        SendCommand = new RelayCommand(async () => await SendAsync(), () => !Sending && !IsMoving && !string.IsNullOrWhiteSpace(Draft));
    }

    /// <summary>Its CLI is moving to another login (M.ORCH-T637): Send and Go to tab are off until the move lands.</summary>
    public bool IsMoving => PendingMoves.ForHost(host.ProcessId) is not null;
    /// <summary>Whether unlocked.</summary>
    public bool IsUnlocked => !IsMoving;
    /// <summary>The moving text.</summary>
    public string MovingText => PendingMoves.ForHost(host.ProcessId)?.Text ?? "";
    /// <summary>Raises the moving.</summary>
    public void RaiseMoving() { Raise(nameof(IsMoving)); Raise(nameof(IsUnlocked)); Raise(nameof(MovingText)); }

    /// <summary>The pipe name.</summary>
    public string PipeName => host.PipeName;
    /// <summary>The tab itself, for "Go to tab" (M.ORCH-T619).</summary>
    public HostSession Host => host;
    /// <summary>The title.</summary>
    public string Title => $"{host.Provider} tab";
    /// <summary>"PID 1234 · a@b.com": the tab's process and the Claude login its CLI bills (M.ORCH-T606), when known.</summary>
    public string Detail => host.Login is { } l ? $"PID {host.ProcessId} · {l}" : $"PID {host.ProcessId}";
    /// <summary>The email behind the login's name (M.ORCH-T614); null when the login isn't known.</summary>
    public string? DetailTip => host.LoginEmail is { } e ? $"Bills the Claude login {e}" : null;
    /// <summary>The draft.</summary>
    public string Draft { get => draft; set => Set(ref draft, value); }
    /// <summary>The feedback.</summary>
    public string Feedback { get => feedback; private set => Set(ref feedback, value); }
    /// <summary>The send command.</summary>
    public ICommand SendCommand { get; }

    /// <summary>Says under the row why "Go to tab" didn't land on the tab itself.</summary>
    internal void ShowFeedback(string note) => Feedback = note;

    /// <summary>Updates.</summary>
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

/// <summary>The session row view model type.</summary>
public sealed class SessionRowViewModel(TranscriptSession session, DateTime nowUtc) : ObservableObject
{
    private TranscriptSession session = session;
    private DateTime nowUtc = nowUtc;

    /// <summary>The session id.</summary>
    public string SessionId => session.SessionId;
    /// <summary>The short id.</summary>
    public string ShortId => session.SessionId.Length > 8 ? session.SessionId[..8] : session.SessionId;
    /// <summary>The model.</summary>
    public string Model => session.Model ?? "";
    /// <summary>The last write.</summary>
    public string LastWrite => Formatting.Ago(session.LastWriteUtc, nowUtc);
    /// <summary>The tokens.</summary>
    public string Tokens => session.UsageToday.Total == 0 ? "" : Formatting.Tokens(session.UsageToday.Total);

    /// <summary>Updates.</summary>
    public void Update(TranscriptSession next, DateTime snapshotUtc)
    {
        session = next;
        nowUtc = snapshotUtc;
        Raise(string.Empty);
    }
}

/// <summary>The ticket row view model type.</summary>
public sealed class TicketRowViewModel(Ticket ticket) : ObservableObject
{
    private Ticket ticket = ticket;

    /// <summary>The id.</summary>
    public string Id => ticket.Id;
    /// <summary>The type.</summary>
    public string Type => ticket.Type;
    /// <summary>The summary.</summary>
    public string Summary => ticket.Summary;
    /// <summary>The date.</summary>
    public string Date => ticket.Date;

    /// <summary>Held by a CLI moving to another login (M.ORCH-T637): "Use as prompt" is off until the move lands.</summary>
    public bool IsMoving => PendingMoves.ForTicket(Id) is not null;
    /// <summary>Whether unlocked.</summary>
    public bool IsUnlocked => !IsMoving;
    /// <summary>The moving text.</summary>
    public string MovingText => PendingMoves.ForTicket(Id)?.Text ?? "";
    /// <summary>Raises the moving.</summary>
    public void RaiseMoving() { Raise(nameof(IsMoving)); Raise(nameof(IsUnlocked)); Raise(nameof(MovingText)); }

    /// <summary>Updates.</summary>
    public void Update(Ticket next)
    {
        ticket = next;
        Raise(string.Empty);
    }
}
