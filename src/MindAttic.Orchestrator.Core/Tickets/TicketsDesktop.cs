using System.Diagnostics;
using System.Text.RegularExpressions;

namespace MindAttic.Orchestrator.Tickets;

/// <summary>
/// Opens a ticket in Tickets Desktop, where it's viewed, approved, prioritized, assigned, blocked and closed (M.ORCH-T500,
/// M.ORCH-T762: the Orchestrator does none of that itself): every ticket id and title the dashboard shows links there. Starts <c>MindAttic.Tickets.Desktop.exe --root &lt;workspace&gt; --ticket &lt;ID&gt;</c>;
/// Tickets Desktop keeps one window per user, so a second start brings the open one forward with the ticket.
/// </summary>
public static partial class TicketsDesktop
{
    /// <summary>A ticket id as the tickets CLI files them (M.ORCH-T500, AUT-T1): anything else never reaches a command line.</summary>
    [GeneratedRegex(@"^[A-Z][A-Z0-9]*(\.[A-Z0-9]+)*-T\d+$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TicketId();

    public static bool IsTicketId(string? id) => id is { Length: > 0 and <= 64 } && TicketId().IsMatch(id);

    /// <summary>A ticket id inside running text ("M.ORCH-T5 waits for M.TIX-T2"): upper case, as tickets are filed, whole words.</summary>
    [GeneratedRegex(@"(?<![A-Za-z0-9.\-])[A-Z][A-Z0-9]*(?:\.[A-Z0-9]+)*-T\d+(?![A-Za-z0-9])", RegexOptions.CultureInvariant)]
    private static partial Regex TicketIdInText();

    /// <summary>One piece of a line split by <see cref="Split"/>: plain text, or a ticket id (<see cref="TicketId"/> set) that the
    /// dashboard shows as a link into Tickets Desktop.</summary>
    public sealed record Segment(string Text, string? TicketId);

    /// <summary><paramref name="text"/> cut into plain pieces and ticket ids, in order, so every ticket id a message, alert or
    /// status line names can open the ticket in Tickets Desktop (M.ORCH-T762). Empty text gives no pieces.</summary>
    public static IReadOnlyList<Segment> Split(string? text)
    {
        if (string.IsNullOrEmpty(text)) return [];
        var parts = new List<Segment>();
        var at = 0;
        foreach (Match m in TicketIdInText().Matches(text))
        {
            if (m.Index > at) parts.Add(new(text[at..m.Index], null));
            parts.Add(new(m.Value, m.Value));
            at = m.Index + m.Length;
        }
        if (at < text.Length) parts.Add(new(text[at..], null));
        return parts;
    }

    /// <summary>The ticket <paramref name="text"/> is about when it names exactly one (an alert about one ticket opens it); else null.</summary>
    public static string? OnlyTicketIn(string? text) =>
        Split(text).Select(s => s.TicketId).OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase).ToList() is [var one] ? one : null;

    /// <summary>What Tickets Desktop is started with to open <paramref name="ticketId"/> in <paramref name="workspaceRoot"/>.</summary>
    internal static IReadOnlyList<string> Arguments(string ticketId, string workspaceRoot) =>
        ["--root", workspaceRoot, "--ticket", ticketId.ToUpperInvariant()];

    /// <summary>Opens <paramref name="ticketId"/> in Tickets Desktop; returns why it couldn't, or null once it's started.</summary>
    public static string? Open(string? ticketId, OrchestratorOptions options)
    {
        if (!IsTicketId(ticketId)) return $"Not a ticket id: {ticketId}";
        if (!File.Exists(options.TicketsDesktopExe))
            return $"Tickets Desktop not found at {options.TicketsDesktopExe} (run MindAttic.Tickets/run.ps1).";

        var psi = new ProcessStartInfo(options.TicketsDesktopExe) { UseShellExecute = false };
        foreach (var a in Arguments(ticketId!, options.WorkspaceRoot)) psi.ArgumentList.Add(a);
        try
        {
            using var _ = Process.Start(psi);
            return null;
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            return $"Couldn't start Tickets Desktop: {ex.Message}";
        }
    }
}
