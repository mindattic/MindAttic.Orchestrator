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
/// asked to "remind me tomorrow" runs it too.
/// </summary>
internal sealed class RemindCommand : Command<RemindCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "[TEXT]")]
        [Description("What to be reminded of.")]
        public string? Text { get; init; }

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
        [Description("With --list, write them as JSON.")]
        public bool Json { get; init; }
    }

    /// <summary>Psst, as the command checks it; a test's stands in for the real one.</summary>
    internal static Func<PsstClient> Psst { get; set; } = () => new PsstClient(ProcessRunner.Default);

    /// <summary>Psst's contact book; tests point it at their own.</summary>
    internal static string? ContactsPath { get; set; }

    public override int Execute(CommandContext context, Settings settings)
    {
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
