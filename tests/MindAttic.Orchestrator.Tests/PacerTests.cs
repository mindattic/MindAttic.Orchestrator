using System.Text.Json;
using MindAttic.Orchestrator.Budget;
using MindAttic.Orchestrator.Scheduling;
using MindAttic.Orchestrator.Settings;
using MindAttic.Workflow.Tickets;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

public sealed class PacerTests
{
    private static readonly DateTime Day = new(2026, 10, 13, 0, 0, 0, DateTimeKind.Utc);

    private static UsageEvent E(double hour, long output) => new(Day.AddHours(hour), new TokenUsage(0, 0, 0, output));

    private static (BudgetStatus Budget, OrchestratorSettings Settings) Budget(double nowHour, OrchestratorSettings settings, params UsageEvent[] events) =>
        (BudgetCalculator.Compute(events, settings, Day.AddHours(nowHour), Day, TimeZoneInfo.Utc), settings);

    private static Pace Pace(double nowHour, int active, OrchestratorSettings settings, params UsageEvent[] events)
    {
        var (b, s) = Budget(nowHour, settings, events);
        return Pacer.Compute(b, active, null, s, Day.AddHours(nowHour));
    }

    [Test]
    public void Under_pace_every_slot_is_open()
    {
        // 500 used of 9,900 (99% of 10k) with 4.5h left: 2,000/h fits; 4 sessions burn 1,000/h together.
        var p = Pace(10.5, 4, new OrchestratorSettings { BlockBudget = 10_000 }, E(10, 500));

        Assert.That(p.Mode, Is.EqualTo(PaceMode.Open));
        Assert.That(p.SessionCap, Is.EqualTo(4));
        Assert.That(p.Advise, Is.False);
    }

    [Test]
    public void Burning_twice_the_sustainable_rate_halves_the_sessions()
    {
        // 2,000 used in the first 30 minutes (4,000/h); 9,000 left over 4.5h fits 2,000/h. Four sessions at 1,000/h each: two fit.
        var p = Pace(10.5, 4, new OrchestratorSettings { BlockBudget = 11_000, PaceTargetPercent = 100 }, E(10, 1000), E(10.4, 1000));

        Assert.That(p.SustainablePerHour, Is.EqualTo(2000).Within(1));
        Assert.That(p.RecentPerHour, Is.EqualTo(4000).Within(1));
        Assert.That((p.Mode, p.SessionCap), Is.EqualTo((PaceMode.Throttled, 2)));
        Assert.That(p.Advise, Is.True, "running agents are asked to conserve while they outrun the window");
        Assert.That(p.Governing, Is.EqualTo("5-hour"));
    }

    [Test]
    public void Near_the_reset_with_budget_left_it_opens_back_up()
    {
        // Half the block left with 30 minutes to go: spend it rather than leave it on the table.
        var p = Pace(14.5, 1, new OrchestratorSettings { BlockBudget = 10_000, PaceTargetPercent = 100 }, E(10, 4000), E(14 + 10.0 / 60, 1000));

        Assert.That(p.SustainablePerHour, Is.EqualTo(10_000).Within(1));
        Assert.That((p.Mode, p.SessionCap), Is.EqualTo((PaceMode.Open, 4)));
    }

    [Test]
    public void At_the_limit_nothing_starts()
    {
        var p = Pace(10.5, 1, new OrchestratorSettings { BlockBudget = 10_000 }, E(10, 10_000));

        Assert.That((p.Mode, p.SessionCap), Is.EqualTo((PaceMode.Over, 0)));
        Assert.That(p.AgentNote, Does.Contain("stop at a clean point"));
    }

    [Test]
    public void Past_the_target_but_under_the_limit_it_coasts()
    {
        var p = Pace(12, 1, new OrchestratorSettings { BlockBudget = 10_000 }, E(10, 9950));

        Assert.That((p.Mode, p.SessionCap), Is.EqualTo((PaceMode.Coast, 0)));
        Assert.That(p.Advise, Is.True);
    }

    [Test]
    public void The_weekly_window_is_never_paced()
    {
        // Week resets Wednesday 00:00 (13.5h away) with 9,000 of 100k left: pacing it would allow 667/h.
        var settings = new OrchestratorSettings { BlockBudget = 1_000_000, WeeklyBudget = 100_000, WeeklyReset = "Wed 00:00", PaceTargetPercent = 100 };
        var p = Pace(10.5, 1, settings, E(-24, 90_000), E(10, 1000));

        Assert.That(p.Governing, Is.EqualTo("5-hour"));
        Assert.That((p.Mode, p.AgentDelaySeconds), Is.EqualTo((PaceMode.Open, 0.0)), "the block has room, so nothing is held for the week");
    }

    [Test]
    public void Without_a_limit_or_with_pacing_off_it_stands_aside()
    {
        Assert.That(Pace(10.5, 1, new OrchestratorSettings(), E(10, 500)).Mode, Is.EqualTo(PaceMode.Unpaced), "no budget and nothing learned");
        Assert.That(Pace(10.5, 1, new OrchestratorSettings { BlockBudget = 10_000, Pacing = false }, E(10, 500)).Mode, Is.EqualTo(PaceMode.Unpaced));
    }

    [Test]
    public void With_nothing_running_the_usual_session_cost_decides()
    {
        var (b, s) = Budget(10.5, new OrchestratorSettings { BlockBudget = 10_000, PaceTargetPercent = 100 }, E(9, 500));

        Assert.That(Pacer.Compute(b, 0, 2_000, s, Day.AddHours(10.5)).SessionCap, Is.EqualTo(1), "9,500 over the 3.5h left fits one 2,000/h session");
        Assert.That(Pacer.Compute(b, 0, null, s, Day.AddHours(10.5)).SessionCap, Is.EqualTo(1), "no history: start one and measure");
    }

    [Test]
    public void Usual_session_cost_is_the_median_active_ten_minutes()
    {
        IReadOnlyList<UsageEvent> a = [E(10, 100), E(10.5, 300)];
        IReadOnlyList<UsageEvent> b = [E(11, 200), E(1, 999_999)];

        Assert.That(Pacer.TypicalPerSession([a, b], Day.AddHours(5)), Is.EqualTo(1200), "600, 1,200 and 1,800 an hour; the 1:00 burst is before the window");
    }

    // â”€â”€ Holding running agents â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Test]
    public void Outrunning_the_window_holds_running_agents_and_fitting_it_runs_them_free()
    {
        var over = Pace(10.5, 4, new OrchestratorSettings { BlockBudget = 11_000, PaceTargetPercent = 100 }, E(10, 1000), E(10.4, 1000));
        Assert.That(over.AgentDelaySeconds, Is.EqualTo(40), "the last 10 minutes drew 6,000/h, three times what fits: a 20s step gets 40s more");
        Assert.That(over.AgentNote, Does.Contain("holding each tool call about 40s"));

        var fits = Pace(10.5, 4, new OrchestratorSettings { BlockBudget = 10_000 }, E(10, 500));
        Assert.That((fits.AgentDelaySeconds, fits.Advise), Is.EqualTo((0.0, false)), "on pace: nobody is held or told to conserve");
    }

    [Test]
    public void The_hold_judges_what_agents_would_burn_unheld_so_it_neither_flaps_nor_lingers()
    {
        var s = new OrchestratorSettings();
        Assert.That(Pacer.AgentDelay(2000, 2000, 20, s), Is.EqualTo(20),
            "the hold brought the rate down to what fits; releasing it would put it straight back at twice that");
        Assert.That(Pacer.AgentDelay(1000, 2000, 20, s), Is.Zero, "unheld they'd burn 2,000/h, which fits: release at once");
        Assert.That(Pacer.AgentDelay(100_000, 2000, 0, s), Is.EqualTo(300), "capped at the max");
        Assert.That(Pacer.AgentDelay(100, 0, 0, s), Is.EqualTo(300), "out of room before the reset: slowest pace, still running");
        Assert.That(Pacer.AgentDelay(0, 0, 0, s), Is.Zero, "nothing running, nothing to hold");
        Assert.That(Pacer.AgentDelay(4000, 2000, 0, s with { ThrottleAgents = false }), Is.Zero);
    }

    [Test]
    public void A_held_session_is_costed_at_what_it_will_draw_once_let_go()
    {
        var (b, s) = Budget(10.5, new OrchestratorSettings { BlockBudget = 11_000, PaceTargetPercent = 100 }, E(10, 1000), E(10.4, 1000));

        Assert.That(Pacer.Compute(b, 4, null, s, Day.AddHours(10.5)).PerSessionPerHour, Is.EqualTo(1000).Within(1));
        var held = Pacer.Compute(b, 4, null, s, Day.AddHours(10.5), recentDelaySeconds: 20);
        Assert.That(held.PerSessionPerHour, Is.EqualTo(2000).Within(1), "held 20s per 20s step: it burned half what it will");
        Assert.That(held.SessionCap, Is.EqualTo(1), "so one session fits 2,000/h, not two");
    }

    [Test]
    public void Coasting_only_closes_new_sessions_it_doesnt_slow_the_running_ones()
    {
        var coast = new Pace(PaceMode.Coast, 0, 2000, 1500, 3000, "5-hour", Day, "Coasting: not even one more session fits.");
        Assert.That(coast.Advise, Is.False);
    }

    [Test]
    public void The_status_file_carries_the_hold_and_how_long_it_has_been_in_force()
    {
        var path = Path.Combine(Path.GetTempPath(), "rl-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var (b, s) = Budget(10.5, new OrchestratorSettings { BlockBudget = 11_000, PaceTargetPercent = 100 }, E(10, 1000), E(10.4, 1000));
            var writer = new RateLimitStatusWriter(path);
            writer.Write(b, Pacer.Compute(b, 4, null, s, Day.AddHours(10.5)));

            using (var doc = JsonDocument.Parse(File.ReadAllText(path)))
                Assert.That(doc.RootElement.GetProperty("pace_delay_seconds").GetDouble(), Is.EqualTo(40));
            var now = DateTime.UtcNow;
            Assert.That(writer.AgentDelay(now), Is.EqualTo(0).Within(0.1), "just set: the recent rate hasn't felt it yet");
            Assert.That(writer.AgentDelay(now.AddMinutes(2)), Is.EqualTo(40 * (1 - Math.Exp(-2.0 / 10))).Within(0.1));
            Assert.That(writer.AgentDelay(now.AddHours(2)), Is.LessThan(0.1), "nobody refreshed it, so the gate stopped holding long ago");
        }
        finally { File.Delete(path); }
    }

    [Test]
    public void The_subagent_allowance_grows_with_the_spare_rate_and_drains_when_over()
    {
        Assert.That(Pacer.SubagentAllowance(2000, 1000, 250, 2, 16), Is.EqualTo(6), "1,000/h spare pays for 4 more at 250/h each");
        Assert.That(Pacer.SubagentAllowance(2000, 3000, 500, 4, 16), Is.EqualTo(2), "1,000/h over: two fewer, so the excess drains as they finish");
        Assert.That(Pacer.SubagentAllowance(2000, 9000, 500, 4, 16), Is.EqualTo(1), "never below one while anything's left: a held spawn waits");
        Assert.That(Pacer.SubagentAllowance(2000, 0, 1, 0, 16), Is.EqualTo(16), "lots of room: the ceiling caps it");
        Assert.That(Pacer.SubagentAllowance(0, 0, 500, 3, 16), Is.EqualTo(0), "the window is at its target: none new");
        Assert.That(Pacer.SubagentAllowance(2000, 1000, null, 3, 16), Is.EqualTo(5), "no per-agent history: two more while there's room");
        Assert.That(Pacer.SubagentAllowance(2000, 1000, 250, 2, 0), Is.EqualTo(0), "--max-subagents 0 holds every new one");
    }

    [Test]
    public void Agents_are_told_the_allowance_and_the_gate_reads_it_from_the_status_file()
    {
        var path = Path.Combine(Path.GetTempPath(), "rl-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var (b, s) = Budget(10.5, new OrchestratorSettings { BlockBudget = 10_000 }, E(10, 500));
            var pace = Pacer.Compute(b, 1, 500, s, Day.AddHours(10.5), subagents: 3);
            Assert.That(pace.SubagentAllowance, Is.GreaterThan(3).And.LessThanOrEqualTo(16));
            Assert.That(pace.SubagentNote, Does.Contain("3 running now").And.Contains("parallel subagents"), "room: it says to fan out");

            new RateLimitStatusWriter(path).Write(b, pace);
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            Assert.That(doc.RootElement.GetProperty("subagent_allowance").GetInt32(), Is.EqualTo(pace.SubagentAllowance));
            Assert.That(doc.RootElement.GetProperty("subagent_note").GetString(), Is.EqualTo(pace.SubagentNote));

            var over = Pace(10.5, 1, new OrchestratorSettings { BlockBudget = 1_000 }, E(10, 2_000)) with { Subagents = 2 };
            Assert.That(over.SubagentAllowance, Is.EqualTo(0));
            Assert.That(over.SubagentNote, Does.StartWith("Subagents: none new"));
            Assert.That(new Pace(PaceMode.Unpaced, 4, 0, 0, null, null, null, "off").SubagentNote, Is.Empty, "unpaced: nothing held, nothing said");
        }
        finally { File.Delete(path); }
    }

    [Test]
    public void The_pace_gate_refuses_a_search_of_a_whole_drive_and_lets_a_folder_search_through()
    {
        // Found live: two orphaned `find /` runs held 660k handles and froze the desktop.
        var dir = Path.Combine(Path.GetTempPath(), "claude-" + Guid.NewGuid().ToString("N"));
        try
        {
            MindAttic.Orchestrator.Setup.AgentGate.Ensure(dir);
            string Gate(string tool, string command)
            {
                var psi = new System.Diagnostics.ProcessStartInfo("python", $"-I \"{Path.Combine(dir, "pace-gate.py")}\"")
                    { RedirectStandardInput = true, RedirectStandardOutput = true, UseShellExecute = false };
                psi.Environment["USERPROFILE"] = dir; // no status file there: nothing to hold for
                using var p = System.Diagnostics.Process.Start(psi)!;
                p.StandardInput.Write(JsonSerializer.Serialize(new { tool_name = tool, tool_input = new { command } }));
                p.StandardInput.Close();
                var output = p.StandardOutput.ReadToEnd();
                Assert.That(p.WaitForExit(20_000), Is.True, command);
                return output;
            }

            foreach (var c in new[] { "find / -iname \"*kimi*\"", "\"C:/Program Files/Git/usr/bin/find.exe\" / -iname x", "rg foo /", "cd x && find C:\\ -name y" })
                Assert.That(Gate("Bash", c), Does.Contain("\"deny\""), c);
            Assert.That(Gate("PowerShell", "Get-ChildItem C:\\ -Recurse -Filter x"), Does.Contain("\"deny\""));
            foreach (var c in new[] { "find . -name x", "find /d/Projects/MindAttic/X -name y", "ls /", "echo find the root /" })
                Assert.That(Gate("Bash", c), Is.Empty, c);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Test]
    public void Subagents_are_counted_by_their_start_and_stop_marks()
    {
        var dir = Path.Combine(Path.GetTempPath(), "claude-" + Guid.NewGuid().ToString("N"));
        try
        {
            Assert.That(MindAttic.Orchestrator.Setup.AgentGate.Ensure(dir), Is.Not.Null);
            var hooks = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(Path.Combine(dir, "settings.json")))!["hooks"]!;
            foreach (var e in new[] { "SubagentStart", "SubagentStop" })
                Assert.That(hooks[e]!.ToJsonString(), Does.Contain("context-gate.py"), e);
            Assert.That(File.ReadAllText(Path.Combine(dir, "pace-gate.py")), Does.Contain("subagent_allowance"));

            var cycler = new MindAttic.Orchestrator.Sessions.ContextCycler(Path.Combine(dir, "session-context"));
            Directory.CreateDirectory(cycler.SubagentDirectory);
            File.WriteAllText(Path.Combine(cycler.SubagentDirectory, "a1"), Environment.ProcessId.ToString()); // not a Claude CLI: closed
            File.WriteAllText(Path.Combine(cycler.SubagentDirectory, "a2"), ""); // no pid: trusted while young
            var old = Path.Combine(cycler.SubagentDirectory, "a3");
            File.WriteAllText(old, "");
            File.SetLastWriteTimeUtc(old, DateTime.UtcNow.AddHours(-7)); // its stop never came
            Assert.That(cycler.Subagents(), Is.EqualTo(1));
            cycler.PruneMarks();
            Assert.That(Directory.GetFiles(cycler.SubagentDirectory).Select(Path.GetFileName), Is.EqualTo(new[] { "a2" }));
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Test]
    public void The_pace_gate_hook_is_installed_once_beside_the_users_own_hooks()
    {
        var dir = Path.Combine(Path.GetTempPath(), "claude-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var settings = Path.Combine(dir, "settings.json");
            File.WriteAllText(settings, """{ "model": "opus", "hooks": { "PreToolUse": [ { "matcher": "Bash", "hooks": [ { "type": "command", "command": "mine.ps1" } ] } ] } }""");

            Assert.That(MindAttic.Orchestrator.Setup.AgentGate.Ensure(dir), Is.Not.Null);
            Assert.That(MindAttic.Orchestrator.Setup.AgentGate.Ensure(dir), Is.Null, "already in place: nothing rewritten");

            var root = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(settings))!;
            var pre = root["hooks"]!["PreToolUse"]!.AsArray();
            Assert.That(root["model"]!.GetValue<string>(), Is.EqualTo("opus"));
            Assert.That(pre, Has.Count.EqualTo(2), "the user's hook kept, the gate added once");
            Assert.That(pre[1]!["hooks"]![0]!["command"]!.GetValue<string>(), Does.Contain("pace-gate.py"));
            Assert.That(File.ReadAllText(Path.Combine(dir, "pace-gate.py")), Does.Contain("pace_delay_seconds"));
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Test]
    public void The_pace_hold_is_wired_into_Codex_Gemini_and_Kimis_own_hooks_too()
    {
        var claudeDir = Path.Combine(Path.GetTempPath(), "claude-" + Guid.NewGuid().ToString("N"));
        var codexDir = Path.Combine(Path.GetTempPath(), "codex-" + Guid.NewGuid().ToString("N"));
        var geminiDir = Path.Combine(Path.GetTempPath(), "gemini-" + Guid.NewGuid().ToString("N"));
        var kimiConfig = Path.Combine(Path.GetTempPath(), "kimi-" + Guid.NewGuid().ToString("N"), "config.toml");
        try
        {
            // Nothing to wire in before pace-gate.py itself exists.
            Assert.That(MindAttic.Orchestrator.Setup.AgentGate.EnsureOtherProviders(claudeDir, codexDir, geminiDir, kimiConfig), Is.Null);

            Directory.CreateDirectory(Path.GetDirectoryName(kimiConfig)!);
            File.WriteAllText(kimiConfig, "default_model = \"kimi-code/k3\"\n");
            Assert.That(MindAttic.Orchestrator.Setup.AgentGate.Ensure(claudeDir), Is.Not.Null);
            Assert.That(MindAttic.Orchestrator.Setup.AgentGate.EnsureOtherProviders(claudeDir, codexDir, geminiDir, kimiConfig), Is.Not.Null);
            Assert.That(MindAttic.Orchestrator.Setup.AgentGate.EnsureOtherProviders(claudeDir, codexDir, geminiDir, kimiConfig), Is.Null, "already in place: nothing rewritten");

            var codexHook = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(Path.Combine(codexDir, "hooks.json")))!["hooks"]!["PreToolUse"]![0]!;
            Assert.That(codexHook["hooks"]![0]!["command"]!.GetValue<string>(), Does.Contain("pace-gate.py"));

            var geminiHook = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(Path.Combine(geminiDir, "settings.json")))!["hooks"]!["BeforeTool"]![0]!;
            Assert.That(geminiHook["hooks"]![0]!["timeout"]!.GetValue<int>(), Is.EqualTo(MindAttic.Orchestrator.Setup.AgentGate.HookTimeoutSeconds * 1000), "Gemini's timeout is milliseconds");

            var kimiText = File.ReadAllText(kimiConfig);
            Assert.That(kimiText, Does.Contain("default_model"), "the user's own config survives");
            Assert.That(kimiText, Does.Contain("[[hooks]]").And.Contain("event = \"PreToolUse\"").And.Contain("pace-gate.py"));
        }
        finally
        {
            foreach (var d in new[] { claudeDir, codexDir, geminiDir, Path.GetDirectoryName(kimiConfig)! })
                if (Directory.Exists(d)) Directory.Delete(d, recursive: true);
        }
    }

    // â”€â”€ Scheduler and status file â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    private static Ticket T(string id) => new(id, "2026-10-01", "feature", "open", $"summary of {id}", "");

    private static RepoStatus Repo(string name, RepoState state, params Ticket[] open) =>
        new(name, $@"D:\W\{name}", state, [], [], open, null, default);

    private static readonly RepoStatus[] FourRepos =
    [
        Repo("A", RepoState.Working), Repo("B", RepoState.Dormant, T("B-T1")), Repo("C", RepoState.Dormant, T("C-T1")), Repo("D", RepoState.Dormant, T("D-T1")),
    ];

    [Test]
    public void The_scheduler_fills_only_what_the_pace_allows()
    {
        var pace = new Pace(PaceMode.Throttled, 2, 2000, 4000, 1000, "5-hour", Day, "Pacing: capped at 2 of 4 sessions.");

        var plan = Scheduler.Plan(new Snapshot(Day, FourRepos, []) { Pace = pace }, new OrchestratorSettings(), []);

        Assert.That(plan.Cap, Is.EqualTo(2));
        Assert.That(plan.Proposals, Has.Count.EqualTo(1), "one session is working, so one more fits");
        Assert.That(plan.Hold, Is.Null);
    }

    [Test]
    public void Paced_the_warn_line_warns_but_doesnt_hold()
    {
        var (warn, s) = Budget(10.5, new OrchestratorSettings { BlockBudget = 10_000 }, E(10, 8500));
        Assume.That(warn.Level, Is.EqualTo(BudgetLevel.Warn));
        var open = new Pace(PaceMode.Open, 4, 2000, 100, 100, "5-hour", Day, "On pace.");

        var paced = Scheduler.Plan(new Snapshot(Day, FourRepos, []) { Budget = warn, Pace = open }, s, []);
        var unpaced = Scheduler.Plan(new Snapshot(Day, FourRepos, []) { Budget = warn }, s, []);

        Assert.That(paced.Proposals, Is.Not.Empty);
        Assert.That(unpaced.Proposals, Is.Empty, "with pacing off the warn line still holds");
    }

    [Test]
    public void Coasting_or_over_holds_and_says_why()
    {
        var coast = new Pace(PaceMode.Coast, 0, 0, 3000, 3000, "5-hour", Day, "Coasting: the 5-hour window is at its 95% target.");

        var plan = Scheduler.Plan(new Snapshot(Day, FourRepos, []) { Pace = coast }, new OrchestratorSettings(), []);

        Assert.That(plan.Proposals, Is.Empty);
        Assert.That(plan.Hold, Does.Contain("Coasting"));
    }

    [Test]
    public void The_status_file_tells_running_agents_to_conserve()
    {
        var path = Path.Combine(Path.GetTempPath(), "rl-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var (b, s) = Budget(10.5, new OrchestratorSettings { BlockBudget = 11_000, PaceTargetPercent = 100 }, E(10, 1000), E(10.4, 1000));
            var pace = Pacer.Compute(b, 4, null, s, Day.AddHours(10.5));
            new RateLimitStatusWriter(path).Write(b, pace);

            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            Assert.That(doc.RootElement.GetProperty("pace").GetString(), Is.EqualTo("throttled"));
            Assert.That(doc.RootElement.GetProperty("pace_note").GetString(), Does.Contain("Keep working normally").And.Contains("subagent allowance"),
                "never told to skip subagents; the allowance decides how many");
        }
        finally { File.Delete(path); }
    }
}
