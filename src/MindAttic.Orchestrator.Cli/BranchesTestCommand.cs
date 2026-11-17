using System.ComponentModel;
using System.Text.Json;
using MindAttic.Orchestrator;
using MindAttic.Orchestrator.Scheduling;
using MindAttic.Orchestrator.Sessions;
using MindAttic.Orchestrator.Settings;
using MindAttic.Orchestrator.Setup;
using Spectre.Console;
using Spectre.Console.Cli;

/// <summary>
/// <c>orchestrator branches test [&lt;branch&gt;...] [--repo &lt;name&gt;] [--list] [--json]</c> (M.ORCH-T1635): the user's on-demand
/// local test run of the host's queued work branches. Takes the queue (the branches built clean at a head not tested yet), runs
/// each chosen branch's full suite at that head in a detached worktree of its own (<see cref="BranchTestRun"/>: this checkout
/// stays on main), one branch at a time, and reports pass or fail per branch head back to the host through the one reporting
/// path (<see cref="BranchesReportCommand.Record"/>): in this machine's store when it is the host (or standalone), else through
/// the host's gateway, which runs <c>orchestrator branches report</c> there. A pass signs that head off for the merge; a fail
/// sends the branch back to its work unit. A run that couldn't complete tested nothing and isn't reported.
/// </summary>
internal sealed class BranchesTestCommand : AsyncCommand<BranchesTestCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "[branches]")]
        [Description("The queued branches to run (work/<name>, <name>, or the ticket it names); every queued branch when none.")]
        public string[] Branches { get; init; } = [];

        [CommandOption("--repo <NAME>")]
        [Description("Only the queued branches of this repo.")]
        public string? Repo { get; init; }

        [CommandOption("--list")]
        [Description("Only list the branches that would run; nothing is run or reported.")]
        public bool List { get; init; }

        [CommandOption("--timeout <MINUTES>")]
        [Description("How long one branch's suite may run (the main-line suite's limit by default).")]
        public double? TimeoutMinutes { get; init; }

        [CommandOption("--json")]
        [Description("Print the results as JSON.")]
        public bool Json { get; init; }
    }

    private static readonly JsonSerializerOptions Read = new() { PropertyNameCaseInsensitive = true };

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        var store = Ctx.Store();
        var s = store.LoadSettings();
        GatewayClient? host = null;
        try
        {
            if (MachineTopology.Current.IsClient)
            {
                if (GatewayRemotes.Find(s.Gateway, MachineRole.HostRemoteName) is not { } remote)
                {
                    AnsiConsole.MarkupLine("[red]This machine is a client but isn't connected to a host (orchestrator machine connect).[/]");
                    return 1;
                }
                host = new GatewayClient(remote);
            }

            var (queue, problem) = await Queue(store, host);
            if (queue is null)
            {
                AnsiConsole.MarkupLine($"[red]Couldn't read the host's queue of work branches: {Markup.Escape(problem ?? "unknown")}[/]");
                return 1;
            }
            var chosen = Choose(queue, settings.Repo, settings.Branches, out var unmatched);
            foreach (var name in unmatched)
                AnsiConsole.MarkupLine($"[yellow]{Markup.Escape(name)} isn't in the queue (built clean at a head not tested yet); skipped.[/]");
            if (chosen.Count == 0)
            {
                if (settings.Json) Console.WriteLine("[]");
                else AnsiConsole.MarkupLine("[grey]No queued work branch to run: none is built and waiting for a local test run.[/]");
                return unmatched.Count > 0 ? 1 : 0;
            }
            if (settings.List)
            {
                if (settings.Json) Console.WriteLine(JsonSerializer.Serialize(chosen, BranchesCommand.Indented));
                else foreach (var r in chosen) AnsiConsole.MarkupLine($"{Markup.Escape(r.Repo)} [aqua]{Markup.Escape(r.Branch)}[/] at {Markup.Escape(BranchesCommand.Short(r.Head))}");
                return 0;
            }

            var workspace = OrchestratorOptions.Load().WorkspaceRoot;
            var timeout = MainlineTests.TimeoutOf(s);
            var runner = new BranchTestRun
            {
                Timeout = OrchestratorSettings.MinutesSpan(settings.TimeoutMinutes ?? double.NaN, timeout),
                LockRetries = (int)Math.Clamp(double.IsFinite(s.MainTestsLockRetries) ? s.MainTestsLockRetries : 2, 0, 10),
                LockRetryDelay = OrchestratorSettings.SecondsSpan(s.MainTestsLockRetrySeconds, 1),
            };
            var results = new List<object>();
            var allPassed = true;
            var memoryWait = TimeSpan.FromMinutes(double.IsFinite(s.BranchTestsMemoryWaitMinutes) ? Math.Clamp(s.BranchTestsMemoryWaitMinutes, 0, 1440) : 0);
            foreach (var r in chosen)
            {
                // Like the daemon's suite runs: none starts while the memory guard holds; wait for RAM, else skip (nothing reported).
                if (await WaitForMemory(() => Ctx.Settings, memoryWait, settings.Json) is { } held)
                {
                    allPassed = false;
                    results.Add(new { repo = r.Repo, branch = r.Branch, head = r.Head, completed = false, ok = false, passed = 0, failed = 0, note = held, reported = (string?)null, warning = (string?)null });
                    if (!settings.Json) AnsiConsole.MarkupLine($"{Markup.Escape(r.Repo)}'s [aqua]{Markup.Escape(r.Branch)}[/]: [yellow]didn't run, nothing reported:[/] {Markup.Escape(held)}");
                    continue;
                }
                if (!settings.Json)
                    AnsiConsole.MarkupLine($"Running {Markup.Escape(r.Repo)}'s [aqua]{Markup.Escape(r.Branch)}[/] at {Markup.Escape(BranchesCommand.Short(r.Head))}…");
                var result = runner.Run(r, Path.Combine(workspace, r.Repo), s.WorktreeRoot);
                string? reported = null, warning = null;
                var reportFailed = false;
                if (result.Completed)
                    (reported, warning, reportFailed) = await Report(host, result);
                allPassed &= result.Completed && result.Ok && !reportFailed;
                results.Add(new { repo = r.Repo, branch = r.Branch, head = r.Head, completed = result.Completed, ok = result.Ok, passed = result.Passed, failed = result.Failed, note = result.Note, reported, warning });
                if (settings.Json) continue;
                var head = Markup.Escape(BranchesCommand.Short(r.Head));
                if (!result.Completed)
                    AnsiConsole.MarkupLine($"  [yellow]didn't complete, nothing reported:[/] {Markup.Escape(result.Note ?? "")}");
                else if (result.Ok)
                    AnsiConsole.MarkupLine($"  [green]passed[/] at {head}" + (result.Passed > 0 ? $" ({result.Passed} tests)" : "") + (result.Note is { } n ? $" ({Markup.Escape(n)})" : ""));
                else
                    AnsiConsole.MarkupLine($"  [red]failed[/] at {head} ({result.Failed} failing):\n  {Markup.Escape(result.Note ?? "").Replace("\n", "\n  ")}");
                if (reported is not null) AnsiConsole.MarkupLine($"  [{(reportFailed ? "red" : "grey")}]host: {Markup.Escape(reported.Trim())}[/]");
                if (warning is not null) AnsiConsole.MarkupLine($"  [yellow]{Markup.Escape(warning)}[/]");
            }
            if (settings.Json) Console.WriteLine(JsonSerializer.Serialize(results, BranchesCommand.Indented));
            return allPassed ? 0 : 1;
        }
        finally
        {
            host?.Dispose();
        }
    }

    /// <summary>Waits up to <paramref name="max"/> for the memory guard to let new work start; null once it does, else why it still holds.</summary>
    private static async Task<string?> WaitForMemory(Func<OrchestratorSettings> settings, TimeSpan max, bool quiet)
    {
        var until = DateTime.UtcNow + max;
        var said = false;
        while (true)
        {
            var held = MindAttic.Orchestrator.Activity.MemoryGuard.StartHeld(settings());
            if (held is null) return null;
            if (DateTime.UtcNow >= until) return held;
            if (!said && !quiet) AnsiConsole.MarkupLine($"[grey]Waiting for RAM before the next run: {Markup.Escape(held)}[/]");
            said = true;
            await Task.Delay(TimeSpan.FromSeconds(10));
        }
    }

    /// <summary>The host's queue: this machine's store when it holds the builds' record, else the host's gateway.</summary>
    private static async Task<(IReadOnlyList<BranchBuilds.Ready>? Queue, string? Problem)> Queue(JsonStore store, GatewayClient? host)
    {
        if (host is null) return (BranchBuilds.ReadyToTest(BranchBuilds.Load(store), BranchBuilds.LoadTests(store)), null);
        var (code, output) = await host.RunBranchesAsync(["branches", "--ready", "--json"], CancellationToken.None);
        if (code != 0) return (null, output.Trim());
        try { return (JsonSerializer.Deserialize<List<BranchBuilds.Ready>>(output, Read) ?? [], null); }
        catch (JsonException) { return (null, "its answer wasn't the queue: " + Clip(output)); }
    }

    /// <summary>Reports one completed run to the host; the host's line, a warning about the ticket, and whether it wasn't taken.</summary>
    private static async Task<(string? Reported, string? Warning, bool Failed)> Report(GatewayClient? host, BranchTestRun.Result result)
    {
        if (host is null)
        {
            var (report, warning) = BranchesReportCommand.Record(result.Repo, result.Branch, result.Head, result.Ok, result.Note);
            return (report.Text, warning, report.Outcome is BranchBuilds.ReportOutcome.Unknown or BranchBuilds.ReportOutcome.Unreadable);
        }
        List<string> args = ["branches", "report", result.Repo, result.Branch, result.Head, result.Ok ? "--pass" : "--fail"];
        if (!string.IsNullOrWhiteSpace(result.Note)) args.AddRange(["--note", Clip(result.Note, 30_000)]);
        var (code, output) = await host.RunBranchesAsync(args, CancellationToken.None);
        return (output, null, code != 0);
    }

    /// <summary>The queued branches the user named (all when none), in queue order; <paramref name="unmatched"/> the names that
    /// matched none. A name matches <c>work/&lt;name&gt;</c>, <c>&lt;name&gt;</c>, or the ticket the branch names.</summary>
    internal static List<BranchBuilds.Ready> Choose(IReadOnlyList<BranchBuilds.Ready> queue, string? repo, IReadOnlyList<string> names, out List<string> unmatched)
    {
        var inRepo = queue.Where(r => repo is null || string.Equals(r.Repo, repo, StringComparison.OrdinalIgnoreCase)).ToList();
        unmatched = [];
        if (names.Count == 0) return inRepo;
        var chosen = new List<BranchBuilds.Ready>();
        foreach (var name in names)
        {
            var bare = name.StartsWith("origin/", StringComparison.Ordinal) ? name["origin/".Length..] : name;
            var hits = inRepo.Where(r => r.Branch == bare || r.Branch == BranchBuilds.Prefix + bare
                || string.Equals(BranchBuilds.WorkUnitOf(r.Branch), bare, StringComparison.OrdinalIgnoreCase)).ToList();
            if (hits.Count == 0) unmatched.Add(name);
            chosen.AddRange(hits.Where(h => !chosen.Contains(h)));
        }
        return [.. inRepo.Where(chosen.Contains)];
    }

    private static string Clip(string text, int max = 300) => text.Length > max ? text[..max] : text;
}
