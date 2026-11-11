using System.IO;
using System.Windows.Media;
using MindAttic.Orchestrator.Sessions;

namespace MindAttic.Orchestrator.Wpf.ViewModels;

/// <summary>
/// Change Color (M.ORCH-T1594): on the right-click menu of each person's tab and each CLI tab. A person's pick is their login's
/// tab colour (<c>tabColor</c> in its <see cref="LoginLook.FileName"/>, the one Windows Terminal tabs use too), which the tabs of
/// their open CLIs follow; a CLI's own pick (<see cref="PaneRequest.CustomColor"/>) is that one tab's. Either drives the Task List
/// (the sidebar's cards): a row being worked by a CLI that has a colour of its own is drawn in it, every other row in its
/// person's colour.
/// </summary>
public sealed partial class MainViewModel
{
    /// <summary>The colour a Task List row is drawn in: the CLI working it (by <paramref name="sessionId"/>, else the session the
    /// agent board attributes <paramref name="ticketId"/> to) when its tab is open here and has a colour, else this person's
    /// <see cref="TabBrush"/>.</summary>
    internal Brush TaskAccent(string? ticketId, string? sessionId)
    {
        var session = sessionId;
        try
        {
            if (session is not { Length: > 0 } && ticketId is { Length: > 0 } id)
                session = (snapshot ?? full)?.Agents?.FirstOrDefault(a => string.Equals(a.TicketId?.Trim(), id.Trim(), StringComparison.OrdinalIgnoreCase))?.SessionId;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException) { OrchestratorLog.For("Dashboard").Warning(ex, "Reading a Task List row's session failed; contained"); }
        return CliAccentOr(session, TabBrush);
    }

    /// <summary>The colour of the CLI tab open here on <paramref name="sessionId"/> (its own Change Color pick, else its login's),
    /// or <paramref name="fallback"/> when no tab here runs that session or it has no colour: what a Task List row for that CLI
    /// is drawn in (M.ORCH-T1594).</summary>
    internal Brush CliAccentOr(string? sessionId, Brush fallback)
    {
        try
        {
            if (sessionId is { Length: > 0 }
                && Root.AllTerminals?.Panes.FirstOrDefault(p => string.Equals(p.SessionId, sessionId, StringComparison.OrdinalIgnoreCase)) is { } pane
                && pane.Request.EffectiveColor is { Length: > 0 })
                return pane.TabBrush;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException) { OrchestratorLog.For("Dashboard").Warning(ex, "Reading a CLI tab's colour failed; contained"); }
        return fallback;
    }

    /// <summary>Every tab's Task List rows read their colour again: after a CLI's Change Color, or its login's colour reaching its
    /// open tabs.</summary>
    internal void RaiseTaskAccents()
    {
        foreach (var t in Root.loginTabs)
        {
            foreach (var row in t.ActiveTab.Tickets) row.RaiseAccent();
            foreach (var row in t.PendingTab.Tickets) row.RaiseAccent();
            foreach (var row in t.LiveActivity) row.RaiseAccent();
        }
    }

    /// <summary>This person's tab colour as "#RRGGBB" (what Change Color shows checked); null when it has none.</summary>
    internal string? TabColorHex => LoginLook.CleanColor(tab.Color);

    /// <summary>Change Color on this person's tab (M.ORCH-T1594): <paramref name="color"/> ("#RRGGBB"; null goes back to the
    /// colour picked from the email) is their login's tab colour from now on, in every folder that's their login. Said on the bus
    /// as a roster change, so every dashboard re-reads its tabs; this one refreshes at once, and the CLIs already open take it
    /// at that pass (<see cref="TerminalsViewModel.RefreshLooksAsync"/>). Never throws.</summary>
    internal async Task ChangeTabColorAsync(string? color)
    {
        if (OwnLogin() is not { } login) return;
        try
        {
            LoginLook.SetTabColor(OwnLoginDirs(), login.Folder, login.Email, color, aggregator.Db);
            AccountsMessage = color is null ? $"{TabTitle} is back to their own colour." : $"{TabTitle}'s colour is now {LoginLook.CleanColor(color)}.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or System.Data.Common.DbException)
        {
            AccountsMessage = $"Couldn't change {TabTitle}'s colour: {ex.Message}";
            return;
        }
        NoteLocalChange();
        await RefreshAsync();
        RaiseTaskAccents();
    }
}
