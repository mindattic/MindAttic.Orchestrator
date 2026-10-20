using System.Diagnostics;
using System.Text.RegularExpressions;

namespace MindAttic.Orchestrator.Tickets;

/// <summary>
/// Opens a ticket in Tickets Desktop, where it's viewed, approved, prioritized and closed (M.ORCH-T500): every ticket id
/// the dashboard lists links there. Starts <c>MindAttic.Tickets.Desktop.exe --root &lt;workspace&gt; --ticket &lt;ID&gt;</c>;
/// Tickets Desktop keeps one window per user, so a second start brings the open one forward with the ticket.
/// </summary>
public static partial class TicketsDesktop
{
    /// <summary>A ticket id as the tickets CLI files them (M.ORCH-T500, AUT-T1): anything else never reaches a command line.</summary>
    [GeneratedRegex(@"^[A-Z][A-Z0-9]*(\.[A-Z0-9]+)*-T\d+$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TicketId();

    public static bool IsTicketId(string? id) => id is { Length: > 0 and <= 64 } && TicketId().IsMatch(id);

    /// <summary>What Tickets Desktop is started with to open <paramref name="ticketId"/> in <paramref name="workspaceRoot"/>.</summary>
    internal static IReadOnlyList<string> Arguments(string ticketId, string workspaceRoot) =>
        ["--root", workspaceRoot, "--ticket", ticketId.ToUpperInvariant()];

    /// <summary>Opens <paramref name="ticketId"/> in Tickets Desktop; returns why it couldn't, or null once it's started.</summary>
    public static string? Open(string? ticketId, OrchestratorOptions options)
    {
        if (!IsTicketId(ticketId)) return $"Not a ticket id: {ticketId}";
        if (!File.Exists(options.TicketsDesktopExe))
            return $"Tickets Desktop not found at {options.TicketsDesktopExe} (run MindAttic.Tickets/deploy.ps1).";

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
