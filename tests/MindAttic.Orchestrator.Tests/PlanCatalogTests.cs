using MindAttic.Orchestrator.Settings;
using MindAttic.Orchestrator.Status;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>The saved plans the dashboard's Plans button lists (M.ORCH-T1826): what <see cref="PlanCatalog"/> reads from the
/// markdown files under <c>docs\plans</c> in the workspace root and each known repo, and what it does with a folder or a file
/// it can't read.</summary>
public sealed class PlanCatalogTests
{
    private static readonly OrchestratorSettings Defaults = new();
    private string root = "";

    [SetUp]
    public void SetUp() => root = Path.Combine(Path.GetTempPath(), "plan-catalog-" + Guid.NewGuid().ToString("N"), "Workspace");

    [TearDown]
    public void TearDown()
    {
        var dir = Path.GetDirectoryName(root)!;
        try { if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    /// <summary>Saves <paramref name="text"/> as <paramref name="name"/> in <paramref name="under"/>'s plans folder, last
    /// written <paramref name="minutesAgo"/> minutes ago; returns its path.</summary>
    private static string Save(string under, string name, string text, int minutesAgo = 0, string folder = @"docs\plans")
    {
        var dir = Path.Combine(under, folder);
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, name);
        File.WriteAllText(path, text);
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddMinutes(-minutesAgo));
        return path;
    }

    private string Repo(string name)
    {
        var path = Path.Combine(root, name);
        Directory.CreateDirectory(path);
        return path;
    }

    // ── Parsing ───────────────────────────────────────────────────────────

    [Test]
    public void The_title_is_the_first_top_heading_the_status_its_own_line_and_the_summary_the_first_other_paragraph()
    {
        var (title, summary, status) = PlanCatalog.Parse(
            "# Plan: a cloud workspace\n\nStatus: proposal, nothing started.\n\nGoal: everything reachable\nfrom a browser.\n\n## Later\n\nMore.\n",
            "cloud-workspace-plan", 200);
        Assert.Multiple(() =>
        {
            Assert.That(title, Is.EqualTo("Plan: a cloud workspace"));
            Assert.That(status, Is.EqualTo("proposal, nothing started."));
            Assert.That(summary, Is.EqualTo("Goal: everything reachable from a browser."), "a wrapped paragraph reads as one line");
        });
    }

    [Test]
    public void With_no_top_heading_the_title_is_the_file_name_and_with_no_status_line_the_status_is_null()
    {
        var (title, summary, status) = PlanCatalog.Parse("## Only a section\n\nAn idea worth keeping.\n", "half-an-idea", 200);
        Assert.Multiple(() =>
        {
            Assert.That(title, Is.EqualTo("half-an-idea"));
            Assert.That(status, Is.Null);
            Assert.That(summary, Is.EqualTo("An idea worth keeping."));
        });
    }

    [Test]
    public void An_empty_file_is_its_file_name_with_no_summary_and_no_status()
    {
        var (title, summary, status) = PlanCatalog.Parse("", "blank", 200);
        Assert.Multiple(() =>
        {
            Assert.That(title, Is.EqualTo("blank"));
            Assert.That(summary, Is.Empty);
            Assert.That(status, Is.Null);
        });
    }

    [Test]
    public void The_summary_skips_headings_rules_code_blocks_and_the_status()
    {
        var (title, summary, status) = PlanCatalog.Parse(
            "Status: draft\n# Title\n\n---\n\n```\n# not a title\ncode\n```\n\n### Sub\nThe real first paragraph.\n", "x", 200);
        Assert.Multiple(() =>
        {
            Assert.That(title, Is.EqualTo("Title"));
            Assert.That(status, Is.EqualTo("draft"));
            Assert.That(summary, Is.EqualTo("The real first paragraph."));
        });
    }

    [Test]
    public void A_status_line_ends_the_paragraph_before_it_and_takes_its_own_wrapped_lines()
    {
        var (_, summary, status) = PlanCatalog.Parse("# T\n\nAn idea.\nStatus: parked\nuntil the hub ships.\n\nNext.\n", "x", 200);
        Assert.Multiple(() =>
        {
            Assert.That(summary, Is.EqualTo("An idea."));
            Assert.That(status, Is.EqualTo("parked until the hub ships."));
        });
    }

    [TestCase("Status: draft", "draft")]
    [TestCase("status:   draft  ", "draft")]
    [TestCase("**Status:** draft", "draft")]
    [TestCase("**Status**: draft", "draft")]
    [TestCase("Status:", "")]
    public void A_status_line_starts_with_the_word_in_any_case_or_emphasis(string line, string expected) =>
        Assert.That(PlanCatalog.StatusOf(line), Is.EqualTo(expected));

    [TestCase("Statuses: none")]
    [TestCase("The status: draft")]
    [TestCase("Status draft")]
    public void Any_other_line_is_not_a_status(string line) => Assert.That(PlanCatalog.StatusOf(line), Is.Null);

    [Test]
    public void A_front_matter_block_gives_the_status_but_never_the_title_or_summary()
    {
        var (title, summary, status) = PlanCatalog.Parse("---\ntitle: ignored\nstatus: idea\n---\n# Real title\n\nBody.\n", "x", 200);
        Assert.Multiple(() =>
        {
            Assert.That(title, Is.EqualTo("Real title"));
            Assert.That(status, Is.EqualTo("idea"));
            Assert.That(summary, Is.EqualTo("Body."));
        });
    }

    [Test]
    public void A_long_summary_is_cut_at_a_word_to_the_settings_length_and_ends_with_an_ellipsis()
    {
        var clipped = PlanCatalog.Clip("one two three four five six seven", 18);
        Assert.Multiple(() =>
        {
            Assert.That(clipped, Is.EqualTo("one two three four…"));
            Assert.That(PlanCatalog.Clip("one two three fourteen five", 18), Is.EqualTo("one two three…"), "never mid-word");
            Assert.That(PlanCatalog.Clip("short", 18), Is.EqualTo("short"));
            Assert.That(PlanCatalog.Clip("  spaced \t out\n text ", 50), Is.EqualTo("spaced out text"));
        });
    }

    // ── Reading the folders ───────────────────────────────────────────────

    [Test]
    public void A_workspace_with_no_plans_folder_anywhere_lists_nothing()
    {
        Directory.CreateDirectory(root);
        var plans = PlanCatalog.Read(root, [("Alpha", Repo("Alpha")), ("Gone", Path.Combine(root, "never-cloned"))], Defaults);
        Assert.That(plans, Is.Empty);
    }

    [Test]
    public void A_workspace_root_that_does_not_exist_lists_nothing_and_does_not_throw() =>
        Assert.That(PlanCatalog.Read(Path.Combine(root, "missing"), [], Defaults), Is.Empty);

    [Test]
    public void Plans_come_from_the_root_and_every_repo_named_for_where_they_were_saved_newest_first()
    {
        var alpha = Repo("Alpha");
        var old = Save(root, "old.md", "# Old root plan\n\nFirst.\n", minutesAgo: 60);
        var newest = Save(alpha, "newest.md", "# Newest\n\nStatus: idea\n\nSecond.\n", minutesAgo: 1);
        var middle = Save(Repo("Beta"), "middle.md", "No heading here.\n", minutesAgo: 30);
        Save(alpha, "notes.txt", "# Not markdown\n");

        var plans = PlanCatalog.Read(root, [("Alpha", alpha), ("Beta", Path.Combine(root, "Beta"))], Defaults);

        Assert.Multiple(() =>
        {
            Assert.That(plans.Select(p => p.Path), Is.EqualTo(new[] { newest, middle, old }));
            Assert.That(plans.Select(p => p.Repo), Is.EqualTo(new[] { "Alpha", "Beta", "Workspace" }), "the root's plans carry the workspace folder's own name");
            Assert.That(plans.Select(p => p.Title), Is.EqualTo(new[] { "Newest", "middle", "Old root plan" }));
            Assert.That(plans[0].Status, Is.EqualTo("idea"));
            Assert.That(plans[0].Summary, Is.EqualTo("Second."));
            Assert.That(plans[1].Status, Is.Null);
            Assert.That(plans[0].LastWriteTimeUtc, Is.GreaterThan(plans[1].LastWriteTimeUtc));
        });
    }

    [Test]
    public void The_workspace_root_listed_again_as_a_repo_lists_its_plans_once()
    {
        Save(root, "once.md", "# Once\n");
        Assert.That(PlanCatalog.Read(root, [("Workspace", root), ("Again", root + Path.DirectorySeparatorChar)], Defaults), Has.Count.EqualTo(1));
    }

    [Test]
    public void The_plans_folder_is_the_setting_and_a_blank_one_is_the_default()
    {
        Save(root, "idea.md", "# In ideas\n", folder: "ideas");
        Save(root, "plan.md", "# In docs plans\n");
        Assert.Multiple(() =>
        {
            Assert.That(PlanCatalog.Read(root, [], Defaults with { PlansFolder = @"\ideas\" }).Select(p => p.Title), Is.EqualTo(new[] { "In ideas" }));
            Assert.That(PlanCatalog.Read(root, [], Defaults with { PlansFolder = "  " }).Select(p => p.Title), Is.EqualTo(new[] { "In docs plans" }));
        });
    }

    [Test]
    public void The_summary_length_is_the_setting()
    {
        Save(root, "long.md", "# Long\n\n" + string.Join(' ', Enumerable.Repeat("word", 200)) + "\n");
        var summary = PlanCatalog.Read(root, [], Defaults with { PlanSummaryMaxChars = 40 }).Single().Summary;
        Assert.Multiple(() =>
        {
            Assert.That(summary.Length, Is.LessThanOrEqualTo(41), "40 characters and the ellipsis");
            Assert.That(summary, Does.EndWith("…"));
        });
    }

    [Test]
    public void Only_the_head_of_a_very_large_file_is_read()
    {
        // The title sits in the first KB; a status line 2 MB in is never reached.
        var path = Save(root, "huge.md", "# Huge plan\n\nIts opening line.\n\n" + new string('x', 2 * 1024 * 1024) + "\n\nStatus: far away\n");
        var plan = PlanCatalog.Read(root, [], Defaults with { PlanHeadKb = 1 }).Single();
        Assert.Multiple(() =>
        {
            Assert.That(plan.Title, Is.EqualTo("Huge plan"));
            Assert.That(plan.Summary, Is.EqualTo("Its opening line."));
            Assert.That(plan.Status, Is.Null);
            Assert.That(PlanCatalog.ReadHead(path, 1024), Has.Length.LessThanOrEqualTo(1024));
        });
    }

    [Test]
    public void A_file_that_cannot_be_read_is_still_listed_under_its_file_name()
    {
        var path = Save(root, "locked-plan.md", "# Its real title\n\nBody.\n");
        using (new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            var plan = PlanCatalog.Read(root, [], Defaults).Single();
            Assert.Multiple(() =>
            {
                Assert.That(plan.Title, Is.EqualTo("locked-plan"));
                Assert.That(plan.Summary, Is.Empty);
                Assert.That(plan.Path, Is.EqualTo(path));
            });
        }
    }

    [Test]
    public void Out_of_range_settings_are_brought_within_bounds()
    {
        Assert.Multiple(() =>
        {
            Assert.That(PlanCatalog.HeadBytesOf(Defaults with { PlanHeadKb = 0 }), Is.EqualTo(OrchestratorSettings.MinPlanHeadKb * 1024));
            Assert.That(PlanCatalog.HeadBytesOf(Defaults with { PlanHeadKb = int.MaxValue }), Is.EqualTo(OrchestratorSettings.MaxPlanHeadKb * 1024));
            Assert.That(PlanCatalog.SummaryCharsOf(Defaults with { PlanSummaryMaxChars = -5 }), Is.EqualTo(OrchestratorSettings.MinPlanSummaryMaxChars));
            Assert.That(PlanCatalog.FolderOf(Defaults), Is.EqualTo(@"docs\plans"));
        });
    }

    [Test]
    public void An_edited_plan_has_a_new_key_so_the_open_list_refreshes()
    {
        var at = new DateTime(2026, 11, 18, 12, 0, 0, DateTimeKind.Utc);
        var plan = new PlanEntry("T", "R", "", null, @"C:\w\docs\plans\t.md", at);
        Assert.That((plan with { LastWriteTimeUtc = at.AddSeconds(1) }).Key, Is.Not.EqualTo(plan.Key));
    }

    // ── The dashboard's texts ─────────────────────────────────────────────

    [Test]
    public void The_plans_button_says_how_many_plans_there_are_and_a_row_says_where_and_when()
    {
        var now = new DateTime(2026, 11, 18, 12, 0, 0, DateTimeKind.Utc);
        var row = new MindAttic.Orchestrator.Wpf.ViewModels.PlanRowViewModel(
            new PlanEntry("Cloud workspace", "MindAttic", "Goal: a browser.", "proposal", @"C:\w\docs\plans\cloud.md", now.AddHours(-2)), now);
        var bare = new MindAttic.Orchestrator.Wpf.ViewModels.PlanRowViewModel(new PlanEntry("bare", "Alpha", "", null, @"C:\w\Alpha\docs\plans\bare.md", now), now);
        Assert.Multiple(() =>
        {
            Assert.That(MindAttic.Orchestrator.Wpf.ViewModels.MainViewModel.PlansLine(0), Is.EqualTo("Plans"));
            Assert.That(MindAttic.Orchestrator.Wpf.ViewModels.MainViewModel.PlansLine(1), Is.EqualTo("Plans: 1 saved plan"));
            Assert.That(MindAttic.Orchestrator.Wpf.ViewModels.MainViewModel.PlansLine(4), Is.EqualTo("Plans: 4 saved plans"));
            Assert.That(MindAttic.Orchestrator.Wpf.ViewModels.MainViewModel.PlansEmptyLine,
                Is.EqualTo(@"No plans yet. Save a markdown file under docs\plans in the workspace or any repo."));
            Assert.That(row.Where, Is.EqualTo("MindAttic · 2h ago"));
            Assert.That(row.HasStatus, Is.True);
            Assert.That(row.HasSummary, Is.True);
            Assert.That(bare.HasStatus, Is.False);
            Assert.That(bare.HasSummary, Is.False);
            Assert.That(bare.Where, Is.EqualTo("Alpha · now"));
        });
    }
}
