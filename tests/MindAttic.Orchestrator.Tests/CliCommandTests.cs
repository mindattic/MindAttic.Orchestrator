using MindAttic.Orchestrator.Settings;
using NUnit.Framework;
using Spectre.Console;

namespace MindAttic.Orchestrator.Tests;

/// <summary>The CLI's commands run in-process through Spectre's CommandApp against a temp store, so what a typed command
/// saves (or refuses to save) is checked end to end, never touching the user's own settings.</summary>
[NonParallelizable] // Ctx.StoreDirectory and AnsiConsole.Console are process-wide
public sealed class CliCommandTests
{
    private string dir = "";
    private string? previousStore;
    private IAnsiConsole previousConsole = null!;
    private StringWriter output = null!;

    private JsonStore Store => new(dir);
    private string SettingsFile => Path.Combine(dir, "settings.json");

    [SetUp]
    public void SetUp()
    {
        dir = Path.Combine(Path.GetTempPath(), "orch-cli-" + Guid.NewGuid().ToString("N"));
        previousStore = Ctx.StoreDirectory;
        Ctx.StoreDirectory = dir;
        previousConsole = AnsiConsole.Console;
        output = new StringWriter();
        AnsiConsole.Console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Out = new AnsiConsoleOutput(output),
            Ansi = AnsiSupport.No,
            ColorSystem = ColorSystemSupport.NoColors,
            Interactive = InteractionSupport.No,
        });
    }

    [TearDown]
    public void TearDown()
    {
        Ctx.StoreDirectory = previousStore;
        AnsiConsole.Console = previousConsole;
        if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
    }

    private int Run(params string[] args) => CliApp.Create().Run(args);

    [Test]
    public void A_missing_argument_is_a_runtime_exception_the_entry_point_reports_in_one_line()
    {
        // Program.cs catches parse and runtime exceptions as typing mistakes; anything else is logged as a crash.
        Assert.Throws<Spectre.Console.Cli.CommandRuntimeException>(() => Run("dispatch"));
    }

    [Test]
    public void Dispatch_and_start_take_only_a_provider_the_launcher_knows()
    {
        Assert.That(Ctx.Provider("claude"), Is.EqualTo("Claude"), "any casing, as its own name");
        Assert.That(Ctx.Provider("codx"), Is.Null, "a typo would open a tab that dies at once while the ticket reads as assigned");
        Assert.That(output.ToString(), Does.Contain("No provider \"codx\""));
    }

    [Test]
    public void The_store_directory_is_injectable()
    {
        Assert.That(Ctx.Store().Directory, Is.EqualTo(dir));
    }

    [Test]
    public void Bump_stores_the_ticket_id_upper_cased_and_a_return_to_zero_removes_it()
    {
        Assert.That(Run("bump", "m.orch-t5", "--by", "2"), Is.Zero);
        Assert.That(Store.LoadSettings().Priorities, Is.EqualTo(new Dictionary<string, int> { ["M.ORCH-T5"] = 2 }));
        Assert.That(output.ToString(), Does.Contain("M.ORCH-T5 priority is now +2"));

        Assert.That(Run("bump", "M.Orch-T5", "--by", "-2"), Is.Zero);
        Assert.That(Store.LoadSettings().Priorities, Is.Empty);
    }

    [Test]
    public void Reserve_stores_the_ticket_id_upper_cased_and_0_releases_it()
    {
        Assert.That(Run("reserve", "m.orch-t7", "2M"), Is.Zero);
        Assert.That(Store.LoadSettings().Reservations, Is.EqualTo(new Dictionary<string, long> { ["M.ORCH-T7"] = 2_000_000 }));

        Assert.That(Run("reserve", "m.orch-t7", "0"), Is.Zero);
        Assert.That(Store.LoadSettings().Reservations, Is.Empty);
    }

    [Test]
    public void Reserve_refuses_tokens_it_cannot_read()
    {
        Assert.That(Run("reserve", "m.orch-t7", "lots"), Is.EqualTo(1));
        Assert.That(File.Exists(SettingsFile), Is.False);
    }

    [Test]
    public void Block_stores_both_ids_upper_cased()
    {
        // A workspace with no Workflow database, so block keeps its prerequisites in settings (never the real workspace's).
        var workspace = Path.Combine(dir, "workspace");
        Directory.CreateDirectory(workspace);
        Store.SaveSettings(new OrchestratorSettings { WorkspaceRoot = workspace });

        Assert.That(Run("block", "m.orch-t9", "--by", "m.orch-t8", "--by", "M.Orch-T8"), Is.Zero);
        Assert.That(Store.LoadSettings().BlockedBy["M.ORCH-T9"], Is.EqualTo(new[] { "M.ORCH-T8" }));

        Assert.That(Run("block", "M.ORCH-T9", "--clear"), Is.Zero);
        Assert.That(Store.LoadSettings().BlockedBy, Does.Not.ContainKey("M.ORCH-T9"));
    }

    [Test]
    public void Settings_rejects_an_unknown_provider_and_saves_nothing()
    {
        Assert.That(Run("settings", "--provider", "Clippy"), Is.EqualTo(1));
        Assert.That(output.ToString(), Does.Contain("No provider \"Clippy\""));
        Assert.That(File.Exists(SettingsFile), Is.False);
    }

    [Test]
    public void Settings_stores_a_known_provider_in_its_own_casing()
    {
        Assert.That(Run("settings", "--provider", "codex"), Is.Zero);
        Assert.That(Store.LoadSettings().DefaultProvider, Is.EqualTo("Codex"));
    }

    [TestCase("--max-sessions", "-1")]
    [TestCase("--max-sessions", "0")]
    [TestCase("--groom-days", "0")]
    [TestCase("--stale-hours", "-1")]
    [TestCase("--max-subagents", "-1")]
    [TestCase("--hand-minutes", "-1")]
    public void Settings_rejects_out_of_range_numbers_and_saves_nothing(string option, string value)
    {
        Assert.That(Run("settings", option, value), Is.EqualTo(1));
        Assert.That(File.Exists(SettingsFile), Is.False);
    }

    [TestCase("--repo-sessions", "Prose")]
    [TestCase("--repo-sessions", "Prose=x")]
    [TestCase("--repo-sessions", "Prose=-1")]
    [TestCase("--repo-cap", "Prose=")]
    public void Settings_rejects_a_bad_REPO_equals_N_and_saves_nothing(string option, string value)
    {
        Assert.That(Run("settings", option, value), Is.EqualTo(1));
        Assert.That(output.ToString(), Does.Contain("Use REPO=NUMBER"));
        Assert.That(File.Exists(SettingsFile), Is.False);
    }

    [Test]
    public void A_repo_cap_applies_and_clears_whatever_the_casing_typed()
    {
        Assert.That(Run("settings", "--repo-sessions", "Prose=2", "--repo-cap", "prose=40"), Is.Zero);
        var s = Store.LoadSettings();
        Assert.That((OrchestratorSettings.ForRepo(s.RepoMaxSessions, "Prose", 9), OrchestratorSettings.ForRepo(s.RepoBudgetPercent, "Prose", 0)),
            Is.EqualTo((2, 40)), "prose=40 caps Prose");
        Assert.That(OrchestratorSettings.ForRepo(s.RepoMaxSessions, "Other", 9), Is.EqualTo(9));

        Assert.That(Run("settings", "--repo-sessions", "PROSE=0", "--repo-cap", "Prose=30"), Is.Zero);
        s = Store.LoadSettings();
        Assert.That((s.RepoMaxSessions.Count, s.RepoBudgetPercent.Single()), Is.EqualTo((0, new KeyValuePair<string, int>("Prose", 30))),
            "PROSE=0 cleared Prose; Prose=30 replaced prose");
    }

    [Test]
    public void Settings_escapes_a_bracket_in_saved_values_when_it_prints_them()
    {
        // Unescaped, "[odd" would be read as a markup tag and the command would crash after saving.
        Assert.That(Run("settings", "--repo-sessions", "[odd=3", "--repo-cap", "[cap]=40"), Is.Zero);
        var s = Store.LoadSettings();
        Assert.That(s.RepoMaxSessions["[odd"], Is.EqualTo(3));
        Assert.That(s.RepoBudgetPercent["[cap]"], Is.EqualTo(40));
        Assert.That(output.ToString(), Does.Contain("[odd 3").And.Contain("[cap] 40%"));
    }

    [Test]
    public void Budget_takes_token_counts_as_the_readme_writes_them()
    {
        // Read as 20M, not refused by the option's type before the command runs (the --warn mistake stops it short of saving).
        Assert.That(Run("budget", "--block", "20M", "--weekly", "1.5B", "--warn", "0"), Is.EqualTo(1));
        Assert.That(output.ToString(), Does.Contain("--warn is a percent"));
        Assert.That(Run("budget", "--weekly", "lots"), Is.EqualTo(1));
        Assert.That(output.ToString(), Does.Contain("--weekly: a number like"));
        Assert.That(File.Exists(SettingsFile), Is.False);
    }

    [TestCase("0")]
    [TestCase("101")]
    [TestCase("-5")]
    public void Budget_rejects_warn_outside_1_to_100_and_saves_nothing(string warn)
    {
        Assert.That(Run("budget", "--warn", warn), Is.EqualTo(1));
        Assert.That(output.ToString(), Does.Contain("--warn is a percent from 1 to 100"));
        Assert.That(File.Exists(SettingsFile), Is.False);
    }
}
