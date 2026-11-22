using System.Globalization;
using System.Text.Json;
using MindAttic.Orchestrator;
using MindAttic.Orchestrator.Reminders;
using MindAttic.Orchestrator.Sessions;
using MindAttic.Orchestrator.Settings;
using Spectre.Console;

/// <summary>
/// The reminders that nag (M.ORCH-T1933, on <see cref="NagBook"/> from M.ORCH-T1932): <c>orchestrator remind add --text
/// "&lt;text&gt;" --every 2h [--until-ticket &lt;ID&gt;]</c>, <c>remind list</c> and <c>remind dismiss &lt;id&gt;</c>. Unlike
/// <c>orchestrator remind "&lt;text&gt;" --at</c> (one text at one time), a nag comes back on a backoff until it's dismissed or
/// its ticket closes; the daemon delivers them (M.ORCH-T1934). The verbs are <see cref="RemindCommand"/>'s first word rather than
/// subcommands of a <c>remind</c> branch: Spectre doesn't hand a branch's default command a positional, so a branch would break
/// <c>remind "&lt;text&gt;" --at</c>.
/// </summary>
internal static class NagCli
{
    /// <summary>The words that make <c>remind</c> one of these verbs (when no <c>--at</c> is given).</summary>
    public const string AddVerb = "add", ListVerb = "list", DismissVerb = "dismiss";

    /// <summary>The database the nags live in; tests point it at their own.</summary>
    internal static Func<OrchestratorDb> Db { get; set; } = () => new OrchestratorDb(OrchestratorDb.LivePath);

    /// <summary>A ticket's status by id; null when the tickets store can't be read or has no such ticket. Tests stand in their own.</summary>
    internal static Func<string, string?> StatusOf { get; set; } = id =>
    {
        try
        {
            var root = OrchestratorOptions.Load(Ctx.Store()).WorkspaceRoot;
            using var db = MindAttic.Tickets.Data.TicketsDb.OpenIfMigrated(root);
            return MindAttic.Orchestrator.Tickets.TicketQueue.Find(db, root, id)?.Ticket.Status;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or System.Data.Common.DbException)
        {
            return null;
        }
    };

    private static NagBook Book() => new(Db());

    /// <summary><c>remind add</c>: adds a nag saying <paramref name="text"/>, first due one <paramref name="every"/> from now.</summary>
    public static int Add(string? text, string? every, string? untilTicket, double? backoff, string? maxEvery)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            AnsiConsole.MarkupLine("[red]What should it say? orchestrator remind add --text \"price the data vendors\" --every 1d --until-ticket IP-T603[/]");
            return 1;
        }
        if (every is null)
        {
            AnsiConsole.MarkupLine("[red]How often? Add --every, like --every 2h, --every 1d or --every 30m.[/]");
            return 1;
        }
        if (TermGrants.ParseDuration(every) is not { } gap)
        {
            AnsiConsole.MarkupLine($"[red]Couldn't read --every \"{Markup.Escape(every)}\"; try \"30m\", \"2h\", \"1d\" or \"1h30m\".[/]");
            return 1;
        }
        TimeSpan? cap = null;
        if (maxEvery is not null)
        {
            if (TermGrants.ParseDuration(maxEvery) is not { } max)
            {
                AnsiConsole.MarkupLine($"[red]Couldn't read --max-every \"{Markup.Escape(maxEvery)}\"; try \"12h\", \"3d\" or \"1w\".[/]");
                return 1;
            }
            cap = max;
        }
        var growth = backoff ?? NagSchedule.DefaultBackoff;
        if (NagSchedule.Problem(gap, growth, cap) is { } problem)
        {
            AnsiConsole.MarkupLine($"[red]{Markup.Escape(problem)}.[/]");
            return 1;
        }

        string? ticket = null;
        if (!string.IsNullOrWhiteSpace(untilTicket))
        {
            ticket = untilTicket.Trim().ToUpperInvariant();
            var status = StatusOf(ticket);
            if (status is not null && NagSchedule.Done.Contains(status))
            {
                AnsiConsole.MarkupLine($"[red]{Markup.Escape(ticket)} is already {Markup.Escape(status)}, so it would end before it nags.[/]");
                return 1;
            }
            // Saved anyway: the tickets store may only be unreadable now. A nag errs toward nagging, so a mistyped id just
            // means it runs until dismissed.
            if (status is null)
                AnsiConsole.MarkupLine($"[yellow]Note: couldn't find {Markup.Escape(ticket)}, so its closing may never end this; dismiss it by hand if so.[/]");
        }

        var now = DateTime.UtcNow;
        var nag = Book().Add(text, gap, now, growth, cap, ticket);
        AnsiConsole.MarkupLine($"Reminder [bold]{nag.Id}[/]: {Markup.Escape(nag.Message)}");
        AnsiConsole.MarkupLine($"[grey]First nag {Markup.Escape(When(nag.NextUtc, now))}, then {Markup.Escape(Cadence(nag))}, " +
                               (nag.UntilTicket is { } t ? $"until {Markup.Escape(t)} closes or " : "until ") +
                               $"orchestrator remind dismiss {nag.Id}.[/]");
        return 0;
    }

    /// <summary><c>remind list</c>: the nags still active (<paramref name="all"/>: the ended ones too).</summary>
    public static int List(bool all, bool json)
    {
        var book = Book();
        var nags = all ? book.All() : book.Active();
        if (json)
        {
            Console.WriteLine(JsonSerializer.Serialize(nags, new JsonSerializerOptions { WriteIndented = true }));
            return 0;
        }
        if (nags.Count == 0)
        {
            AnsiConsole.MarkupLine($"[grey]{(all ? "No reminders" : "No reminders nagging")}. Add one: orchestrator remind add --text \"<text>\" --every 1d [[--until-ticket <ID>]][/]");
            return 0;
        }
        var now = DateTime.UtcNow;
        var table = new Table().Border(TableBorder.Rounded).AddColumns("Id", "Next", "Repeats", "Until", "Nags", "Reminder");
        foreach (var n in nags)
            table.AddRow(
                n.Id,
                n.Active
                    ? Markup.Escape(When(n.NextUtc, now))
                    : $"[grey]ended {Markup.Escape(n.EndedUtc!.Value.ToLocalTime().ToString("MMM d h:mm tt", CultureInfo.CurrentCulture))}: {Markup.Escape(n.EndedWhy ?? "")}[/]",
                Markup.Escape(Cadence(n)),
                Markup.Escape(n.UntilTicket is { } t ? $"{t} closes" : "dismissed"),
                n.Nags.ToString(CultureInfo.InvariantCulture),
                Markup.Escape(n.Message));
        AnsiConsole.Write(table);
        return 0;
    }

    /// <summary><c>remind dismiss &lt;id&gt;</c>: ends a nag for good.</summary>
    public static int Dismiss(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            AnsiConsole.MarkupLine("[red]Which one? orchestrator remind dismiss <id> (orchestrator remind list shows them).[/]");
            return 1;
        }
        id = id.Trim();
        var book = Book();
        if (book.Dismiss(id, DateTime.UtcNow) is { } ended)
        {
            AnsiConsole.MarkupLine($"Dismissed reminder {ended.Id}: {Markup.Escape(ended.Message)}");
            return 0;
        }
        if (book.Find(id) is { } gone)
        {
            AnsiConsole.MarkupLine($"Reminder {gone.Id} already ended ({Markup.Escape(gone.EndedWhy ?? "")}).");
            return 0;
        }
        AnsiConsole.MarkupLine($"[red]No reminder {Markup.Escape(id)}; orchestrator remind list shows them.[/]");
        return 1;
    }

    /// <summary>A gap said briefly: "45m", "2h", "1d 6h".</summary>
    internal static string Short(TimeSpan span)
    {
        if (span.TotalMinutes < 60) return $"{Math.Max(1, Math.Round(span.TotalMinutes)):0}m";
        if (span.TotalHours < 24) return span.Minutes == 0 ? $"{(int)span.TotalHours}h" : $"{(int)span.TotalHours}h{span.Minutes}m";
        return span.Hours == 0 ? $"{(int)span.TotalDays}d" : $"{(int)span.TotalDays}d {span.Hours}h";
    }

    /// <summary>A time in local terms, and how far off it is from <paramref name="nowUtc"/>.</summary>
    internal static string When(DateTime utc, DateTime nowUtc)
    {
        var local = utc.ToLocalTime().ToString("ddd MMM d h:mm tt", CultureInfo.CurrentCulture);
        var gap = utc - nowUtc;
        return gap > TimeSpan.Zero ? $"{local} (in {Short(gap)})" : $"{local} (due)";
    }

    /// <summary>How a nag repeats: "every 2h, doubling up to 7d".</summary>
    internal static string Cadence(Nag n)
    {
        var cap = n.MaxEvery ?? NagSchedule.LongestGap;
        if (n.Backoff <= 1 || n.Every >= cap) return $"every {Short(n.Every)}";
        var growth = n.Backoff == 2 ? "doubling" : $"×{n.Backoff.ToString("0.##", CultureInfo.InvariantCulture)} each time";
        return $"every {Short(n.Every)}, {growth} up to {Short(cap)}";
    }
}
