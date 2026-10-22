using System.Collections.ObjectModel;
using System.Windows.Media;
using MindAttic.Components.Wpf.Mvvm;
using MindAttic.Orchestrator.Activity;

namespace MindAttic.Orchestrator.Wpf.ViewModels;

/// <summary>One live Claude CLI or autopilot background agent, with its running subagents nested inside it.</summary>
public sealed class AgentRowViewModel(string sessionId) : ObservableObject
{
    private AgentBoardRow row = null!;
    private DateTime nowUtc;
    private string? ticketTitle;

    public string SessionId { get; } = sessionId;
    public string Repo => row.Repo;
    public string? TicketId => row.TicketId;
    public bool HasTicket => TicketId is { Length: > 0 };
    public AgentState State => row.State;

    /// <summary>The Launcher tab its CLI runs in, for "Go to tab" (M.ORCH-T619); null for a headless agent.</summary>
    public HostSession? Host => row.Host;
    public bool HasHost => row.Host is not null;

    /// <summary>The per-CLI login it runs on (null: the global login) and its claude.exe, for its "Move to" menu (M.ORCH-T607).</summary>
    public string? OwnLogin => row.OwnLogin;
    public int? CliPid => row.CliPid;

    /// <summary>"M.ORCH-T84: Agent board …" when the ticket's title is known, else just the id, else what it's for.</summary>
    public string Headline => TicketId is not { Length: > 0 } ? "No ticket: working on what you typed"
        : ticketTitle is { Length: > 0 } ? $"{TicketId}: {ticketTitle}" : TicketId;

    public string Elapsed => Formatting.Span(nowUtc - row.StartedUtc);
    public string Tokens => row.Tokens.Total == 0 ? "" : Formatting.Tokens(row.Tokens.Total) + " tokens today";
    public bool IsAutopilot => row.IsAutopilot;
    public string Kind => IsAutopilot ? "Autopilot agent" : "Claude CLI";

    /// <summary>The state in words; colour only repeats it (WCAG 1.4.1).</summary>
    public string StateLabel => row.State switch
    {
        AgentState.Working => "Working",
        AgentState.HeldByPace => "Held by pace",
        _ => "Needs you",
    };

    /// <summary>A Segoe MDL2 glyph per state, so the state reads without colour: play, pause, person.</summary>
    public string StateGlyph => row.State switch
    {
        AgentState.Working => "",
        AgentState.HeldByPace => "",
        _ => "",
    };

    public Brush StateBrush => row.State switch
    {
        AgentState.Working => Palette.Working,
        _ => Palette.Waiting,
    };

    /// <summary>Its latest tool call and how long ago: "Editing Daemon.cs · 12s ago"; while waiting, what it stopped on.</summary>
    public string Doing => row.Doing is not { } d ? ""
        : row.State == AgentState.Waiting ? $"Stopped after: {d}" : $"Now: {d} · {Formatting.Ago(row.DoingAtUtc, nowUtc)}";

    /// <summary>What its CLI bills (M.ORCH-T436): "Claude Team", or "API key: the Launcher restarts it…" while it bills the other thing.</summary>
    public string Billing => row.Billing ?? "";

    /// <summary>The Claude login its CLI bills (M.ORCH-T606): "Login: a@b.com", or the global login's "(global)"; empty when unknown.</summary>
    public string Login => row.Login is { } l ? $"Login: {l}" : "";

    /// <summary>"Claude CLI · Claude Team · 3.2M tokens today", without a dangling separator for a part not known yet.</summary>
    public string Meta => string.Join(" · ", new[] { Kind, Billing, Tokens }.Where(p => p.Length > 0));

    /// <summary>The tickets its CLI claimed for its subagents (M.ORCH-T532): "Claimed: M.ORCH-T1, M.ORCH-T2"; empty when none.</summary>
    public string Claims => row.Claims.Count == 0 ? "" : "Claimed: " + string.Join(", ", row.Claims);

    /// <summary>Why it's held (the pace's note); empty otherwise.</summary>
    public string HoldNote => row.State == AgentState.HeldByPace ? row.StateDetail : "";

    public ObservableCollection<SubagentRowViewModel> Subagents { get; } = [];
    public bool HasSubagents => Subagents.Count > 0;
    public string SubagentsHeader => Subagents.Count switch { 0 => "No subagents running", 1 => "1 subagent", var n => $"{n} subagents" };

    /// <summary>The whole card in one sentence, for a screen reader.</summary>
    public string AccessibleName =>
        $"{Repo}, {Kind}, {StateLabel}, {Headline}. {Doing}. {(Billing.Length > 0 ? $"Bills {Billing}. " : "")}{(Login.Length > 0 ? Login + ". " : "")}{(Claims.Length > 0 ? Claims + ". " : "")}{SubagentsHeader}.";

    public void Update(AgentBoardRow next, string? title, DateTime snapshotUtc)
    {
        row = next;
        ticketTitle = title;
        nowUtc = snapshotUtc;
        Subagents.SyncBy(next.Subagents, s => s.AgentId, r => r.AgentId,
            s => new SubagentRowViewModel(s, snapshotUtc), (r, s) => r.Update(s, snapshotUtc));
        Raise(string.Empty);
    }
}

/// <summary>One subagent running inside an <see cref="AgentRowViewModel"/>: what it was asked, what it's doing, how long, what it's spent.</summary>
public sealed class SubagentRowViewModel(SubagentActivity subagent, DateTime nowUtc) : ObservableObject
{
    private SubagentActivity subagent = subagent;
    private DateTime nowUtc = nowUtc;

    public string AgentId => subagent.AgentId;
    public string Type => subagent.Type;
    public string Task => subagent.Task ?? "";
    public string Doing => "Now: " + (subagent.Doing ?? "starting");
    public string Meta => Tokens.Length == 0 ? Elapsed : $"{Elapsed} · {Tokens}";
    public string Elapsed => Formatting.Span(nowUtc - subagent.StartedUtc);
    public string Tokens => subagent.Usage.Total == 0 ? "" : Formatting.Tokens(subagent.Usage.Total);
    public string AccessibleName => $"{Type} subagent, {Elapsed}: {Task}. Now: {Doing}.";

    public void Update(SubagentActivity next, DateTime snapshotUtc)
    {
        subagent = next;
        nowUtc = snapshotUtc;
        Raise(string.Empty);
    }
}
