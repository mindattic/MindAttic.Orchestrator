using System.ComponentModel;
using MindAttic.Orchestrator;
using MindAttic.Orchestrator.Scheduling;
using MindAttic.Orchestrator.Settings;
using Spectre.Console;
using Spectre.Console.Cli;

internal sealed class BlockCommand : Command<BlockCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<TICKET>")]
        public string Ticket { get; init; } = "";

        [CommandOption("--by <TICKET>")]
        [Description("The ticket it waits for (repeatable).")]
        public string[] By { get; init; } = [];

        [CommandOption("--clear")]
        public bool Clear { get; init; }
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        var store = new JsonStore();
        // Once migrated, prerequisites are 'requires' links in the Workflow database, where every tool sees them.
        using (var db = MindAttic.Workflow.Data.WorkflowDb.OpenIfMigrated(OrchestratorOptions.Load(store).WorkspaceRoot))
            if (db is not null)
            {
                if (settings.Clear)
                    foreach (var to in db.Requires(settings.Ticket)) db.RemoveLink(settings.Ticket, to, "requires");
                foreach (var prerequisite in settings.By) db.AddLink(settings.Ticket, prerequisite, "requires", "orchestrator block");
                var now = db.Requires(settings.Ticket);
                AnsiConsole.MarkupLine(now.Count > 0 ? $"{settings.Ticket} requires {string.Join(", ", now)}." : $"{settings.Ticket} requires nothing.");
                return 0;
            }
        var s = store.LoadSettings();
        var map = new Dictionary<string, List<string>>(s.BlockedBy, StringComparer.Ordinal);
        if (settings.Clear) map.Remove(settings.Ticket);
        else map[settings.Ticket] = [.. (map.GetValueOrDefault(settings.Ticket) ?? []).Concat(settings.By).Distinct(StringComparer.Ordinal)];
        store.SaveSettings(s with { BlockedBy = map });
        AnsiConsole.MarkupLine(map.GetValueOrDefault(settings.Ticket) is { Count: > 0 } by
            ? $"{settings.Ticket} waits for {string.Join(", ", by)}."
            : $"{settings.Ticket} waits for nothing set by hand (a \"blocked by\" in its summary still counts).");
        return 0;
    }
}

internal sealed class ReserveCommand : Command<ReserveCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<TICKET>")]
        public string Ticket { get; init; } = "";

        [CommandArgument(1, "<TOKENS>")]
        [Description("Uncached tokens to set aside, like 2M; 0 releases the reservation.")]
        public string Tokens { get; init; } = "";
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        if (!TokenText.TryParse(settings.Tokens, out var tokens)) { AnsiConsole.MarkupLine("[red]Tokens: a number like 2M, 500k or 0.[/]"); return 1; }
        var store = new JsonStore();
        var s = store.LoadSettings();
        var map = new Dictionary<string, long>(s.Reservations, StringComparer.Ordinal);
        if (tokens <= 0) map.Remove(settings.Ticket); else map[settings.Ticket] = tokens;
        store.SaveSettings(s with { Reservations = map });
        AnsiConsole.MarkupLine(tokens > 0
            ? $"{settings.Ticket} has {Formatting.Tokens(tokens)} reserved: it's proposed first, and everything else paces against what's left after it."
            : $"{settings.Ticket}'s reservation is released.");
        return 0;
    }
}

/// <summary>What /autopilot would type into each CLI that has it on, without typing anything.</summary>
internal sealed class DryRunCommand : Command
{
    public override int Execute(CommandContext context)
    {
        var (_, store, snapshot, plan) = Ctx.Load();
        var s = store.LoadSettings();
        var cycler = new MindAttic.Orchestrator.Sessions.ContextCycler();
        var autopilot = new Autopilot(store, cycler);
        var lines = autopilot.Tick(plan, snapshot, s with { Autopilot = true }, DateTime.UtcNow, dryRun: true);

        if (!autopilot.On) { AnsiConsole.MarkupLine("[grey]/autopilot is off; type /autopilot in any CLI (optionally -repo=a,b).[/]"); return 0; }
        foreach (var (repo, reason) in autopilot.LeftAlone) AnsiConsole.MarkupLine($"[grey]Leaving {Markup.Escape(repo)} alone: {Markup.Escape(reason)}.[/]");
        if (autopilot.Repos.Count == 0) { AnsiConsole.MarkupLine("[grey]/autopilot is on, but no Claude CLI is open in a repo it may work (or -repo named none).[/]"); return 0; }
        AnsiConsole.MarkupLine($"/autopilot is on for {Markup.Escape(string.Join(", ", autopilot.Repos))}.");
        if (!s.Autopilot) AnsiConsole.MarkupLine("[yellow]Autopilot is paused (--autopilot true resumes); this is what it would do.[/]");
        if (autopilot.Paused is { } why) AnsiConsole.MarkupLine($"[yellow]Holding: {Markup.Escape(why)}[/]");
        foreach (var line in lines) AnsiConsole.MarkupLine("  " + Markup.Escape(line));
        AnsiConsole.MarkupLine("[grey]Each starts as a background agent (each in its own worktree, as many at once as the pace has room for, worked out every refresh). Nothing was started.[/]");
        return 0;
    }
}

/// <summary>File GitHub issues labelled "orchestrator" and today's CI failures on main as tickets, once.</summary>
internal sealed class IntakeCommand : Command<IntakeCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("--dry-run")]
        [Description("Show what would be filed without writing any USER_STORIES.md.")]
        public bool DryRun { get; init; }
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        var filed = Daemon.Intake(OrchestratorOptions.Load().WorkspaceRoot, settings.DryRun).ToList();
        foreach (var (repo, t) in filed)
            AnsiConsole.MarkupLine($"{(settings.DryRun ? "would file" : "filed")} [bold]{t.Id}[/] in {Markup.Escape(repo)} ({t.Type}): {Markup.Escape(t.Summary)}");
        AnsiConsole.MarkupLine(filed.Count == 0 ? "[grey]Nothing new on GitHub (issues labelled 'orchestrator', CI failures on main in the last day).[/]"
            : $"[grey]{filed.Count} ticket{(filed.Count == 1 ? "" : "s")}{(settings.DryRun ? " would be filed; nothing was written" : " filed")}.[/]");
        return 0;
    }
}

internal static class TokenText
{
    /// <summary>20000000, 20M, 1.5B, 500k.</summary>
    public static bool TryParse(string text, out long value)
    {
        value = 0;
        var t = text.Trim().Replace(",", "").Replace("_", "");
        if (t.Length == 0) return false;
        var scale = char.ToUpperInvariant(t[^1]) switch { 'K' => 1e3, 'M' => 1e6, 'B' => 1e9, _ => 1 };
        if (scale != 1) t = t[..^1];
        if (!double.TryParse(t, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var n) || !double.IsFinite(n) || n < 0) return false;
        value = (long)(n * scale);
        return true;
    }
}
