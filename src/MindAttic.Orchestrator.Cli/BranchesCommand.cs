using System.ComponentModel;
using System.Text.Json;
using MindAttic.Orchestrator;
using MindAttic.Orchestrator.Scheduling;
using Spectre.Console;
using Spectre.Console.Cli;

/// <summary><c>orchestrator branches [--ready] [--returned] [--json]</c>: the remote host's work branches, their builds
/// (M.ORCH-T1501) and their local test runs (M.ORCH-T1637). A branch built clean at a head not yet tested is ready for the user's
/// local test run; <c>--ready --json</c> is the queue as data, for the local fetch job. A branch whose local run failed is back
/// with the work unit that produced it (<c>--returned</c>).</summary>
internal sealed class BranchesCommand : Command<BranchesCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("--ready")]
        [Description("Only the branches built clean at a head not tested yet: the queue for a local test run.")]
        public bool Ready { get; init; }

        [CommandOption("--returned")]
        [Description("Only the branches whose local test run failed at their head: back with the work unit that produced them.")]
        public bool Returned { get; init; }

        [CommandOption("--json")]
        [Description("Print it as JSON.")]
        public bool Json { get; init; }
    }

    internal static readonly JsonSerializerOptions Indented = new() { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public override int Execute(CommandContext context, Settings settings)
    {
        var store = Ctx.Store();
        var state = BranchBuilds.Load(store);
        var tests = BranchBuilds.LoadTests(store);
        if (settings.Returned)
        {
            var returned = BranchBuilds.ReturnedToWork(state, tests);
            if (settings.Json)
            {
                Console.WriteLine(JsonSerializer.Serialize(returned, Indented));
                return 0;
            }
            if (returned.Count == 0) AnsiConsole.MarkupLine("[grey]No work branch is back with its work unit after a failed local test run.[/]");
            foreach (var r in returned)
                AnsiConsole.MarkupLine($"{Markup.Escape(r.Repo)} [aqua]{Markup.Escape(r.Branch)}[/] at {Markup.Escape(Short(r.Head))}: [red]failed its local test run[/]" +
                    $", back with {Markup.Escape(r.Ticket ?? "its work unit")}" + (r.Rounds > 1 ? $" (failed run {r.Rounds})" : "") +
                    (r.Note is { Length: > 0 } note ? $"\n  {Markup.Escape(note)}" : ""));
            return 0;
        }
        if (settings.Ready)
        {
            var ready = BranchBuilds.ReadyToTest(state, tests);
            if (settings.Json)
            {
                Console.WriteLine(JsonSerializer.Serialize(ready, Indented));
                return 0;
            }
            if (ready.Count == 0) AnsiConsole.MarkupLine("[grey]No work branch is built and waiting for a local test run.[/]");
            foreach (var r in ready)
                AnsiConsole.MarkupLine($"{Markup.Escape(r.Repo)} [aqua]{Markup.Escape(r.Branch)}[/] at {Markup.Escape(Short(r.Head))}" +
                    (r.BuiltUtc is { } at ? $", built {at.ToLocalTime():yyyy-MM-dd HH:mm}" : ""));
            return 0;
        }
        if (settings.Json)
        {
            Console.WriteLine(JsonSerializer.Serialize(new { builds = state, tests }, Indented));
            return 0;
        }
        if (!store.TryLoadSettings(out var s) || !s.RemoteBranchBuilds)
            AnsiConsole.MarkupLine("[grey]Work-branch builds are off (the Vault setting \"Build work branches (remote host)\"): work lands on main as always.[/]");
        if (state.Count == 0 || state.All(r => r.Value.Count == 0))
        {
            AnsiConsole.MarkupLine("[grey]No work branches (origin/work/*) seen yet.[/]");
            return 0;
        }
        foreach (var (repo, branches) in state.OrderBy(r => r.Key, StringComparer.OrdinalIgnoreCase))
            foreach (var (branch, b) in branches.OrderBy(b => b.Key, StringComparer.Ordinal))
            {
                var builtAtHead = string.Equals(b.Built, b.Head, StringComparison.OrdinalIgnoreCase);
                var test = tests.TryGetValue(repo, out var t) && t.TryGetValue(branch, out var tb)
                    && string.Equals(tb.Head, b.Head, StringComparison.OrdinalIgnoreCase) ? tb : null;
                var what = b.Building is not null ? "[yellow]building[/]"
                    : !builtAtHead ? "[grey]waiting to build[/]"
                    : !b.Ok ? "[red]fails to build[/]"
                    : test is { Ok: true } ? "[green]passed its local test run, waiting for the merge[/]"
                    : test is { Ok: false } ? $"[red]failed its local test run[/], back with {Markup.Escape(test.ReturnedTo ?? "its work unit")}" +
                        (test.Rounds > 1 ? $" (failed run {test.Rounds})" : "")
                    : "[green]built, ready to test[/]";
                var note = test is { Ok: false, Note.Length: > 0 } ? test.Note : builtAtHead && test is null ? b.Note : null;
                AnsiConsole.MarkupLine($"{Markup.Escape(repo)} [aqua]{Markup.Escape(branch)}[/] at {Markup.Escape(Short(b.Head))}: {what}" +
                    (note is { Length: > 0 } ? $" ({Markup.Escape(note)})" : ""));
            }
        return 0;
    }

    internal static string Short(string sha) => sha.Length > 7 ? sha[..7] : sha;
}

/// <summary><c>orchestrator branches report &lt;repo&gt; &lt;branch&gt; &lt;head&gt; --pass|--fail [--note "..."]</c> (M.ORCH-T1637): records
/// the user's local test run of a work branch at a head. A pass signs that head off for the merge; a fail sends the branch back
/// to the work unit that produced it (its ticket gets a note with the failures and is handed out again), which keeps iterating
/// on the same branch. No other branch is held by either. A result for a head the branch has moved past is dropped.</summary>
internal sealed class BranchesReportCommand : Command<BranchesReportCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<repo>")]
        [Description("The repo the branch is in, as the host names it.")]
        public string Repo { get; init; } = "";

        [CommandArgument(1, "<branch>")]
        [Description("The work branch (work/<ticket>), without origin/.")]
        public string Branch { get; init; } = "";

        [CommandArgument(2, "<head>")]
        [Description("The commit the run tested (its full hash, or at least 7 characters of it).")]
        public string Head { get; init; } = "";

        [CommandOption("--pass")]
        [Description("The run passed.")]
        public bool Pass { get; init; }

        [CommandOption("--fail")]
        [Description("The run failed.")]
        public bool Fail { get; init; }

        [CommandOption("--note <TEXT>")]
        [Description("What failed (the failing tests and their first lines), passed on to the work unit.")]
        public string? Note { get; init; }

        [CommandOption("--json")]
        [Description("Print the outcome as JSON.")]
        public bool Json { get; init; }

        public override ValidationResult Validate() =>
            Pass == Fail ? ValidationResult.Error("Say how the run went: --pass or --fail (one of them).") : ValidationResult.Success();
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        var branch = settings.Branch.StartsWith("origin/", StringComparison.Ordinal) ? settings.Branch["origin/".Length..] : settings.Branch;
        var (report, warning) = Record(settings.Repo, branch, settings.Head, settings.Pass, settings.Note);
        if (settings.Json)
        {
            Console.WriteLine(JsonSerializer.Serialize(new { outcome = report.Outcome.ToString(), text = report.Text, ticket = report.Ticket, head = report.Head, warning },
                BranchesCommand.Indented));
        }
        else
        {
            var color = report.Outcome switch
            {
                BranchBuilds.ReportOutcome.Passed => "green",
                BranchBuilds.ReportOutcome.SentBack => "yellow",
                BranchBuilds.ReportOutcome.Stale => "grey",
                _ => "red",
            };
            AnsiConsole.MarkupLine($"[{color}]{Markup.Escape(report.Text)}[/]");
            if (warning is not null) AnsiConsole.MarkupLine($"[yellow]{Markup.Escape(warning)}[/]");
        }
        return report.Outcome is BranchBuilds.ReportOutcome.Unknown or BranchBuilds.ReportOutcome.Unreadable ? 1 : 0;
    }

    /// <summary>Records a local run in this machine's store (the host's) and, when it sends the branch back, tells its ticket: the
    /// one path every report takes, <c>branches report</c>'s and <c>branches test</c>'s alike (M.ORCH-T1635). The warning is what
    /// couldn't be done about the ticket, or null.</summary>
    internal static (BranchBuilds.Report Report, string? Warning) Record(string repo, string branch, string head, bool pass, string? note)
    {
        var report = BranchBuilds.RecordTest(Ctx.Store(), repo, branch, head, pass, note, DateTime.UtcNow);
        string? warning = null;
        if (report.Outcome == BranchBuilds.ReportOutcome.SentBack)
        {
            var tests = BranchBuilds.LoadTests(Ctx.Store());
            var returned = BranchBuilds.ReturnedToWork(BranchBuilds.Load(Ctx.Store()), tests)
                .FirstOrDefault(r => string.Equals(r.Repo, repo, StringComparison.OrdinalIgnoreCase) && r.Branch == branch);
            if (returned is not null) warning = BranchBuilds.SendBack(OrchestratorOptions.Load().WorkspaceRoot, returned);
        }
        return (report, warning);
    }
}
