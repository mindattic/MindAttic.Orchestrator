using System.ComponentModel;
using System.Text.Json;
using MindAttic.Orchestrator;
using MindAttic.Orchestrator.Reminders;
using MindAttic.Orchestrator.Settings;
using Spectre.Console;
using Spectre.Console.Cli;

/// <summary>
/// <c>orchestrator remind "&lt;text&gt;" --at &lt;time&gt; [--to &lt;contact&gt;]</c> (M.ORCH-T477): saves a reminder the daemon
/// texts through Psst when it's due; <c>--list</c> shows the pending ones and <c>--cancel &lt;id&gt;</c> drops one. A CLI agent
/// asked to "remind me tomorrow" runs it too. With no <c>--at</c>, a first word of <c>add</c>, <c>list</c> or <c>dismiss</c> works
/// the reminders that nag instead (M.ORCH-T1933, <see cref="NagCli"/>).
/// </summary>
internal sealed class RemindCommand : Command<RemindCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "[TEXT]")]
        [Description("What to be reminded of; or add, list, dismiss for a reminder that nags.")]
        public string? Text { get; init; }

        [CommandArgument(1, "[MORE]")]
        [Description("After add: what it says (or give --text). After dismiss: the reminder's id.")]
        public string? More { get; init; }

        [CommandOption("--text <TEXT>")]
        [Description("With add: what the reminder that nags says.")]
        public string? Says { get; init; }

        [CommandOption("--every <GAP>")]
        [Description("With add: how long until the first nag, and the gap the backoff grows from: \"30m\", \"2h\", \"1d\" (at least 1 minute).")]
        public string? Every { get; init; }

        [CommandOption("--until-ticket <ID>")]
        [Description("With add: a ticket whose closing (or won't-do) ends it, e.g. IP-T603; without one it nags until dismissed.")]
        public string? UntilTicket { get; init; }

        [CommandOption("--backoff <N>")]
        [Description("With add: how much longer each gap is than the last (default 2: doubling; 1: the same every time).")]
        public double? Backoff { get; init; }

        [CommandOption("--max-every <GAP>")]
        [Description("With add: the longest a gap grows to (default 7d).")]
        public string? MaxEvery { get; init; }

        [CommandOption("--all")]
        [Description("With list: the ended reminders that nagged too.")]
        public bool All { get; init; }

        [CommandOption("--at <TIME>")]
        [Description("When, local time: \"9am\", \"tomorrow\", \"tomorrow 8am\", \"fri 5pm\", \"tonight\", \"in 2h\", \"30m\", \"2026-10-20 09:00\".")]
        public string? At { get; init; }

        [CommandOption("--to <CONTACT>")]
        [Description("A Psst contact or a US number; the default is you (Psst's own number).")]
        public string? To { get; init; }

        [CommandOption("--list")]
        [Description("List the reminders not sent yet.")]
        public bool List { get; init; }

        [CommandOption("--cancel <ID>")]
        [Description("Cancel a pending reminder.")]
        public string? Cancel { get; init; }

        [CommandOption("--json")]
        [Description("With --list or list, write them as JSON.")]
        public bool Json { get; init; }
    }

    /// <summary>Psst, as the command checks it; a test's stands in for the real one.</summary>
    internal static Func<PsstClient> Psst { get; set; } = () => new PsstClient(ProcessRunner.Default);

    /// <summary>Psst's contact book; tests point it at their own.</summary>
    internal static string? ContactsPath { get; set; }

    public override int Execute(CommandContext context, Settings settings)
    {
        // The reminders that nag (M.ORCH-T1933): a verb as the first word, unless --at makes it a one-shot reminder saying it.
        if (settings.At is null && settings.Cancel is null && !settings.List && settings.Text?.Trim().ToLowerInvariant() is { } verb)
            switch (verb)
            {
                case NagCli.AddVerb:
                    return NagCli.Add(string.IsNullOrWhiteSpace(settings.Says) ? settings.More : settings.Says, settings.Every,
                        settings.UntilTicket, settings.Backoff, settings.MaxEvery);
                case NagCli.ListVerb:
                    return NagCli.List(settings.All, settings.Json);
                case NagCli.DismissVerb:
                    return NagCli.Dismiss(settings.More);
            }
        if (settings.Says is not null || settings.Every is not null || settings.UntilTicket is not null || settings.MaxEvery is not null || settings.Backoff is not null)
        {
            AnsiConsole.MarkupLine("[red]--text, --every, --until-ticket, --backoff and --max-every go with add: orchestrator remind add --text \"<text>\" --every 1d[/]");
            return 1;
        }
        if (settings.More is not null)
        {
            AnsiConsole.MarkupLine("[red]Put the reminder's text in quotes: orchestrator remind \"call the dentist\" --at \"tomorrow 9am\"[/]");
            return 1;
        }
        var store = Ctx.Store();
        var now = DateTime.UtcNow;
        if (settings.Cancel is { } id)
        {
            if (ReminderBook.Cancel(store, id)) { AnsiConsole.MarkupLine($"Cancelled reminder {Markup.Escape(id.Trim())}."); return 0; }
            AnsiConsole.MarkupLine($"[red]No pending reminder {Markup.Escape(id.Trim())}; orchestrator remind --list shows them.[/]");
            return 1;
        }
        if (settings.List || string.IsNullOrWhiteSpace(settings.Text))
        {
            if (!settings.List && settings.At is not null)
            {
                AnsiConsole.MarkupLine("[red]What should the reminder say? orchestrator remind \"call the dentist\" --at \"tomorrow 9am\"[/]");
                return 1;
            }
            return List(store, now, settings.Json);
        }

        if (settings.At is null)
        {
            AnsiConsole.MarkupLine("[red]When? Add --at, like --at \"tomorrow 9am\", --at 5pm or --at \"in 2h\".[/]");
            return 1;
        }
        if (!ReminderTime.TryParseUtc(settings.At, now, TimeZoneInfo.Local, out var dueUtc, out var dueLocal))
        {
            AnsiConsole.MarkupLine($"[red]Couldn't read --at \"{Markup.Escape(settings.At)}\"; try \"9am\", \"tomorrow\", \"fri 5pm\", \"in 2h\" or \"2026-10-20 09:00\".[/]");
            return 1;
        }
        if (dueUtc < now - TimeSpan.FromMinutes(1))
        {
            AnsiConsole.MarkupLine($"[red]{dueLocal:ddd MMM d h:mm tt} has already passed.[/]");
            return 1;
        }
        if (settings.To is { } to && !PsstClient.Addressable(to, ContactsPath))
        {
            AnsiConsole.MarkupLine($"[red]\"{Markup.Escape(to)}\" isn't a Psst contact or a US number, so Psst would refuse it. Add it with: psst contacts add <name> <phone>[/]");
            return 1;
        }

        var r = ReminderBook.Add(store, settings.Text, dueUtc, settings.To, now);
        AnsiConsole.MarkupLine($"Reminder [bold]{r.Id}[/]: {Markup.Escape(ReminderBook.Describe(r, now))}");
        AnsiConsole.MarkupLine("[grey]The daemon texts it through Psst when it's due (or at its first tick after, if the machine's asleep then). " +
                               $"orchestrator remind --cancel {r.Id} drops it.[/]");
        // Said now rather than found out at the reminder's time, but saved either way: the daemon sends it once Psst is fixed.
        var psst = Psst().Probe();
        if (psst.Problem is { } problem)
            AnsiConsole.MarkupLine($"[yellow]Note: {Markup.Escape(problem)}; it's sent once that's fixed.[/]");
        else if (r.To is null && psst.OwnerNumber is null)
            AnsiConsole.MarkupLine("[yellow]Note: Psst has no number of its own ('to' in its settings), so give --to or set it before this is due.[/]");
        return 0;
    }

    private static int List(JsonStore store, DateTime now, bool json)
    {
        var pending = ReminderBook.Pending(store);
        if (json)
        {
            Console.WriteLine(JsonSerializer.Serialize(pending, new JsonSerializerOptions { WriteIndented = true }));
            return 0;
        }
        if (pending.Count == 0)
        {
            AnsiConsole.MarkupLine("[grey]No reminders pending. Add one: orchestrator remind \"<text>\" --at \"tomorrow 9am\"[/]");
            return 0;
        }
        var table = new Table().Border(TableBorder.Rounded).AddColumns("Id", "When", "To", "Reminder", "");
        foreach (var r in pending)
            table.AddRow(
                r.Id,
                Markup.Escape(ReminderBook.When(r, now)),
                Markup.Escape(r.ToLabel),
                Markup.Escape(r.Text),
                r.Failures > 0 ? $"[red]not sent ({r.Failures}×): {Markup.Escape(r.LastError ?? "")}[/]" : "");
        AnsiConsole.Write(table);
        return 0;
    }
}
