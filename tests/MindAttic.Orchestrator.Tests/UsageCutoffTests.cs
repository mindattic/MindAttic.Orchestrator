using MindAttic.Orchestrator.Budget;
using MindAttic.Orchestrator.Scheduling;
using MindAttic.Orchestrator.Settings;
using NUnit.Framework;
using Spectre.Console;

namespace MindAttic.Orchestrator.Tests;

/// <summary>M.ORCH-T707: per-user usage cutoffs, 90% of the week and 98% of the 5-hour window by default, changed only by
/// <c>org limit</c>; no 75% line.</summary>
public sealed class UsageCutoffTests
{
    private const string Erin = "ohleo199@mindattic.com";
    private readonly TempDirs temps = new();

    [TearDown]
    public void TearDown() => temps.DeleteAll();

    [Test]
    public void Everyone_defaults_to_90_and_98_and_a_set_cutoff_applies_to_that_login_only()
    {
        var settings = new OrchestratorSettings();
        var store = new JsonStore(temps.New());
        Assert.That((UsageCutoffs.WeeklyFor(null, Erin), UsageCutoffs.FiveHourFor(null, Erin)), Is.EqualTo((90, 98)));

        UsageCutoffs.Set(store, Erin, "Erin", 80, null);
        var cutoffs = UsageCutoffs.Load(store);
        var erin = UsageCutoffs.Apply(cutoffs, Erin, settings);
        var other = UsageCutoffs.Apply(cutoffs, "honey@mindattic.com", settings);
        Assert.That((erin.WeeklyStopPercent, erin.PaceTargetPercent), Is.EqualTo((80, 98)));
        Assert.That((other.WeeklyStopPercent, other.PaceTargetPercent), Is.EqualTo((90, 98)));
        // The warn line keeps the same 10-point gap to Erin's own (lowered) weekly cutoff that the org-wide default
        // keeps to its own (90 - 80 = 10); her untouched 5-hour cutoff keeps the org-wide 80 (bughunt 2026-11-01).
        Assert.That((erin.WeeklyWarnPercent, erin.FiveHourWarnPercent), Is.EqualTo((70, 80)));
        Assert.That((other.WeeklyWarnPercent, other.FiveHourWarnPercent), Is.EqualTo((80, 80)));

        UsageCutoffs.Set(store, Erin, "Erin", null, 70);
        Assert.That(UsageCutoffs.Load(store).Single(), Has.Property("WeeklyPercent").EqualTo(80).And.Property("FiveHourPercent").EqualTo(70));

        UsageCutoffs.Reset(store, Erin, "Erin");
        Assert.That(UsageCutoffs.Load(store), Is.Empty);
    }

    [Test]
    public void An_explicit_cutoff_equal_to_the_defaults_is_kept_and_a_5h_only_set_leaves_weekly_on_the_global_dial()
    {
        // bughunt 2026-11-02: Set dropped an explicit 90 (equal to the constant) so the member still followed a moved global
        // dial, and a --5h-only set pinned weekly to 90 instead of leaving it on the global dial.
        var settings = new OrchestratorSettings { WeeklyReservePercent = 30 }; // global weekly target 70
        var store = new JsonStore(temps.New());

        UsageCutoffs.Set(store, Erin, "Erin", UsageCutoffs.DefaultWeeklyPercent, null);
        Assert.That(UsageCutoffs.Apply(UsageCutoffs.Load(store), Erin, settings).WeeklyStopPercent, Is.EqualTo(90));

        UsageCutoffs.Reset(store, Erin, "Erin");
        UsageCutoffs.Set(store, Erin, "Erin", null, 70);
        var applied = UsageCutoffs.Apply(UsageCutoffs.Load(store), Erin, settings);
        Assert.That((applied.WeeklyStopPercent, applied.PaceTargetPercent), Is.EqualTo((settings.WeeklyStopPercent, 70)));
    }

    [Test]
    public void Raising_a_cutoff_past_the_org_wide_warn_line_raises_its_own_warn_line_too()
    {
        // bughunt 2026-11-01: a member who raised their weekly cutoff to 98 (org limit --weekly 98) so they'd keep
        // working close to it still warned, and while unpaced was held, at the org-wide 80% line as if they hadn't.
        const string waffle = "waffle@mindattic.com";
        var settings = new OrchestratorSettings();
        var store = new JsonStore(temps.New());
        UsageCutoffs.Set(store, waffle, "Waffle", 98, null);
        var applied = UsageCutoffs.Apply(UsageCutoffs.Load(store), waffle, settings);
        Assert.That((applied.WeeklyStopPercent, applied.WeeklyWarnPercent), Is.EqualTo((98, 88)));

        // 85% is past the org-wide 80% line but short of this member's own 88% line: no warning. Their own line still warns.
        var budget = BudgetCalculator.Compute([], applied, DateTime.UtcNow, DateTime.UtcNow) with { WeeklyPercent = 85 };
        Assert.That(budget.Level, Is.EqualTo(BudgetLevel.Ok), "85% used of a 98% cutoff is still plenty of room");
        Assert.That((budget with { WeeklyPercent = 93 }).Level, Is.EqualTo(BudgetLevel.Warn), "93% is past their own 88% line");
    }

    [Test, NonParallelizable] // Ctx.StoreDirectory, AnsiConsole.Console and the org overrides are process-wide
    public void Org_limit_lists_sets_and_resets_a_members_cutoffs()
    {
        var previousStore = Ctx.StoreDirectory;
        var storeDir = temps.New();
        Ctx.StoreDirectory = storeDir;
        var store = new JsonStore(storeDir);
        OrgHelpCommand.PeopleOverride = () => [new(Erin, "Erin", [], []), new("honey@mindattic.com", "Honey", [], [])];
        try
        {
            var said = Cli(out var exit, "org", "limit");
            Assert.That(exit, Is.Zero, said);
            Assert.That(said, Does.Contain("Erin").And.Contain("90% of the week, 98% of the 5-hour window (default)"));

            said = Cli(out exit, "org", "limit", "erin", "--weekly", "80", "--5h", "95");
            Assert.That(exit, Is.Zero, said);
            Assert.That(UsageCutoffs.WeeklyFor(UsageCutoffs.Load(store), Erin), Is.EqualTo(80));
            Assert.That(UsageCutoffs.FiveHourFor(UsageCutoffs.Load(store), Erin), Is.EqualTo(95));
            Assert.That(Cli(out _, "org", "limit"), Does.Contain("80% of the week, 95% of the 5-hour window (set)"));

            said = Cli(out exit, "org", "limit", "erin", "--weekly", "20");
            Assert.That(exit, Is.EqualTo(1));
            Assert.That(said, Does.Contain("--weekly is 50 to 100"));

            said = Cli(out exit, "org", "limit", "Honey");
            Assert.That(exit, Is.EqualTo(1), "nothing to set");

            said = Cli(out exit, "org", "limit", "E", "--reset");
            Assert.That(exit, Is.Zero, said);
            Assert.That(UsageCutoffs.Load(store), Is.Empty);
        }
        finally
        {
            Ctx.StoreDirectory = previousStore;
            OrgHelpCommand.PeopleOverride = null;
        }
    }

    private static string Cli(out int exit, params string[] args)
    {
        var output = new StringWriter();
        var previous = AnsiConsole.Console;
        AnsiConsole.Console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Out = new AnsiConsoleOutput(output), Ansi = AnsiSupport.No, ColorSystem = ColorSystemSupport.NoColors, Interactive = InteractionSupport.No,
        });
        AnsiConsole.Console.Profile.Width = 240;
        try { exit = CliApp.Create().Run(args); }
        finally { AnsiConsole.Console = previous; }
        return output.ToString();
    }
}
