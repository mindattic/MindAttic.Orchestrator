using System.Text.RegularExpressions;

namespace MindAttic.Orchestrator.Setup;

/// <summary>
/// <c>MindAttic.Orchestrator.Desktop.exe --show &lt;target&gt;</c> (M.ORCH-T763): Tickets Desktop's deep link (its OrchestratorLink) opens
/// the Orchestrator on a person (a login's email), a session (its id, or the start of it) or a ticket's live work (its id).
/// A later launch hands it to the dashboard already running down the same single-instance pipe <c>--start</c> uses
/// (<see cref="SingleInstance"/>, M.ORCH-T767); the first launch keeps it. Only a well-formed target gets through: an email, or
/// letters, digits, dots, dashes and underscores; never an option or anything with a space.
/// </summary>
public static partial class DashboardRequest
{
    /// <summary>The option naming what the dashboard should bring forward.</summary>
    public const string ShowArg = "--show";

    /// <summary>The target after <c>--show</c>, or null when there's none or it isn't a well-formed target.</summary>
    public static string? ShowTarget(IReadOnlyList<string> args)
    {
        for (var i = 0; i < args.Count; i++)
        {
            if (!string.Equals(args[i], ShowArg, StringComparison.OrdinalIgnoreCase)) continue;
            var target = i + 1 < args.Count ? args[i + 1].Trim() : "";
            return IsTarget(target) ? target : null;
        }
        return null;
    }

    /// <summary>Whether <paramref name="target"/> can be shown: an email, a ticket id or a session id (as Tickets Desktop checks it).</summary>
    public static bool IsTarget(string? target) => target is { Length: > 0 and <= 256 } && Target().IsMatch(target);

    [GeneratedRegex(@"^(?:[^@\s-][^@\s]*@[^@\s]+\.[^@\s]+|[A-Za-z0-9][A-Za-z0-9._-]{0,127})$", RegexOptions.CultureInvariant)]
    private static partial Regex Target();

    /// <summary><c>--quickload-all &lt;nonce&gt;</c> (M.ORCH-T2070): a CLI's <c>/quickload all</c> (<c>orchestrator quickload-all</c>)
    /// asks the running dashboard to run Quick Load All, unconfirmed (it was typed on purpose), and to write what it resumed to
    /// <see cref="Sessions.QuickLoadAllReport"/> under that nonce, so the CLI can wait for it and report it.</summary>
    public const string QuickLoadAllArg = "--quickload-all";

    /// <summary>The nonce after <c>--quickload-all</c>, or null when there's no such argument or its nonce isn't well formed
    /// (<see cref="Sessions.QuickLoadAllReport.IsNonce"/>).</summary>
    public static string? QuickLoadAllNonce(IReadOnlyList<string> args)
    {
        for (var i = 0; i < args.Count; i++)
        {
            if (!string.Equals(args[i], QuickLoadAllArg, StringComparison.OrdinalIgnoreCase)) continue;
            var nonce = i + 1 < args.Count ? args[i + 1].Trim() : "";
            return Sessions.QuickLoadAllReport.IsNonce(nonce) ? nonce : null;
        }
        return null;
    }

    /// <summary>What a later launch hands the running dashboard: its <c>--start &lt;repo&gt;</c> (<see cref="StartRequest.HandOff"/>),
    /// its <c>--show &lt;target&gt;</c> and its <c>--quickload-all &lt;nonce&gt;</c>, each only when well formed; nothing when it only
    /// comes forward.</summary>
    public static IReadOnlyList<string> HandOff(IReadOnlyList<string> args) =>
        [.. StartRequest.HandOff(args), .. ShowTarget(args) is { } target ? new[] { ShowArg, target } : [],
         .. QuickLoadAllNonce(args) is { } nonce ? new[] { QuickLoadAllArg, nonce } : []];

    /// <summary>What kind of thing <paramref name="target"/> names: a login (an email), a ticket (a ticket id), else a session.</summary>
    public static ShowKind KindOf(string target) =>
        target.Contains('@') ? ShowKind.Person
        : Tickets.TicketsDesktop.IsTicketId(target) ? ShowKind.Ticket
        : ShowKind.Session;
}

/// <summary>What a <c>--show</c> target names.</summary>
public enum ShowKind
{
    /// <summary>A member or login.</summary>
    Person,
    /// <summary>A CLI session.</summary>
    Session,
    /// <summary>A ticket.</summary>
    Ticket,
}
