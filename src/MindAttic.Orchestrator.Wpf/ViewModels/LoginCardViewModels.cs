using System.Collections.ObjectModel;
using System.Windows.Media;
using MindAttic.Components.Wpf.Mvvm;
using MindAttic.Orchestrator.Activity;
using MindAttic.Orchestrator.Budget;
using MindAttic.Orchestrator.Sessions;

namespace MindAttic.Orchestrator.Wpf.ViewModels;

/// <summary>One live CLI on a login, as a row inside its card (M.ORCH-T603): its repo and state.</summary>
public sealed class LoginCliViewModel(string sessionId) : ObservableObject
{
    private LoginCli cli = null!;

    public string SessionId { get; } = sessionId;
    public string Repo => cli.Repo;
    public AgentState State => cli.State;

    /// <summary>The per-CLI login it runs on (null: the global login) and its claude.exe, for its "Move to" menu (M.ORCH-T607).</summary>
    public string? OwnLogin => cli.OwnLogin;
    public int? CliPid => cli.CliPid;

    /// <summary>The state in words, as the agent board says it.</summary>
    public string StateLabel => cli.State switch
    {
        AgentState.Working => "Working",
        AgentState.HeldByPace => "Held by pace",
        _ => "Needs you",
    };

    public Brush StateBrush => cli.State == AgentState.Waiting ? Palette.Waiting : Palette.Working;
    public string ShortId => SessionId.Length > 8 ? SessionId[..8] : SessionId;

    public void Update(LoginCli next)
    {
        cli = next;
        Raise(nameof(Repo)); Raise(nameof(State)); Raise(nameof(OwnLogin)); Raise(nameof(CliPid)); Raise(nameof(StateLabel)); Raise(nameof(StateBrush));
    }
}

/// <summary>
/// One Claude login's card in the Accounts view (M.ORCH-T603): the global login or a per-CLI one, who's signed in to it and on
/// what plan, its 5-hour and weekly use with their resets, the pace the daemon writes for it, and every live CLI on it.
/// </summary>
public sealed class LoginCardViewModel(string key) : ObservableObject
{
    private LoginCard card = null!;
    private DateTime nowUtc;

    /// <summary>"" for the global login, else the per-CLI login's email.</summary>
    public string Key { get; } = key;
    public LoginCard Card => card;
    public bool IsGlobal => card.IsGlobal;
    public string? Email => card.Email;
    public bool SignedIn => card.SignedIn;

    public string Title => card.Email ?? MainViewModel.GlobalLogin;

    public string Kind => IsGlobal ? "Every CLI not started on a login of its own (~/.claude)" : "A per-CLI login (~/.claude-accounts)";

    /// <summary>Who's signed in: "a@b.com", "Signed in" while its email isn't known, or "Not signed in".</summary>
    public string SignedInText => !SignedIn ? "Not signed in" : card.SignedInAs ?? "Signed in";

    /// <summary>Signed in as someone other than the folder is named for: its CLIs spend that account's tokens.</summary>
    public string Mismatch => SignedIn && !IsGlobal && card.SignedInAs is { } who && !who.Equals(card.Email, StringComparison.OrdinalIgnoreCase)
        ? $"Signed in as {who}, not {card.Email}: its CLIs spend {who}'s tokens." : "";

    public string Plan => card.Plan ?? "";
    public Brush StateBrush => SignedIn ? Palette.Working : Palette.Over;
    public string StateGlyph => SignedIn ? "" : ""; // check mark / warning

    public string FiveHourUsed => AccountRoster.Percent(card.FiveHourPercent);
    public string FiveHourResets => card.FiveHourResetUtc is { } r ? "resets " + Formatting.ResetInHours(r, nowUtc) : "";
    public string WeekUsed => AccountRoster.Percent(card.WeekPercent);
    /// <summary>"stops at 95% · resets 4/26 9:00am (3.2 days)": the login's weekly stop line (M.ORCH-T609, the weekly reserve's;
    /// "stopped at" once its week reached it), then the week's reset.</summary>
    public string WeekResets => (card.AtWeeklyStop ? "stopped at " : "stops at ") + card.WeeklyStopPercent + "%" +
        (card.WeekResetUtc is { } r ? " · resets " + Formatting.ResetInDays(r, nowUtc) : "");

    /// <summary>"Pace open · subagents 2 of 6 · written 1m ago", or that the daemon hasn't paced it yet.</summary>
    public string PaceText => PaceLine(card.Pace, nowUtc);

    internal static string PaceLine(LoginPace? pace, DateTime nowUtc)
    {
        if (pace is null) return "Not paced yet: the daemon paces a login once it's signed in.";
        var parts = new List<string> { "Pace " + (pace.Pace ?? "unpaced") };
        if (pace.Subagents is { } s) parts.Add($"subagents {s}");
        if (pace.AtUtc is { } at) parts.Add($"written {Formatting.Ago(at, nowUtc)}");
        return string.Join(" · ", parts);
    }

    /// <summary>A per-CLI login nobody's signed in to offers Sign in; the global login signs in with /login in any CLI.</summary>
    public bool CanSignIn => !SignedIn && !IsGlobal;
    public string SignInHint => !SignedIn && IsGlobal ? "Run /login in a Claude CLI to sign the global login in." : "";

    public ObservableCollection<LoginCliViewModel> Clis { get; } = [];
    public bool HasClis => Clis.Count > 0;
    public string ClisHeader => Clis.Count switch
    {
        0 => "No CLI running on it",
        1 => "1 CLI running on it",
        var n => $"{n} CLIs running on it",
    };

    public void Update(LoginCard next, DateTime now)
    {
        card = next;
        nowUtc = now;
        Clis.SyncBy([.. next.Clis], c => c.SessionId, v => v.SessionId,
            c => { var v = new LoginCliViewModel(c.SessionId); v.Update(c); return v; },
            (v, c) => v.Update(c));
        Raise(string.Empty);
    }
}
