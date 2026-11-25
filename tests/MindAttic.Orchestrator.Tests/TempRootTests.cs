using MindAttic.Orchestrator.Health;
using MindAttic.Orchestrator.Settings;
using MindAttic.Orchestrator.Term;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>M.ORCH-T2219: the temp-root decision at start-up and the <c>temp:root</c> health check, as pure functions.</summary>
[TestFixture]
public sealed class TempRootTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 25, 12, 0, 0, TimeSpan.Zero);

    [Test]
    public void The_setting_defaults_to_R_Temp() =>
        Assert.That((new OrchestratorSettings().TempRoot, TempRoot.Normalize("  "), TempRoot.Normalize(null)), Is.EqualTo((@"R:\Temp", @"R:\Temp", @"R:\Temp")));

    [TestCase(@"R:\Temp", @"R:\Temp", true)]
    [TestCase(@"r:\temp\", @"R:\Temp", true)]
    [TestCase(@"R:\Temp\claude\x", @"R:\Temp", true)]
    [TestCase(@"R:\Temporary", @"R:\Temp", false)]
    [TestCase(@"C:\Users\ryan\AppData\Local\Temp", @"R:\Temp", false)]
    [TestCase("", @"R:\Temp", false)]
    [TestCase(null, @"R:\Temp", false)]
    public void A_path_is_under_the_root_only_when_it_is_the_root_or_a_folder_in_it(string? path, string root, bool under) =>
        Assert.That(TempRoot.IsUnder(path, root), Is.EqualTo(under));

    [Test]
    public void Temp_already_under_the_root_sets_nothing_and_says_nothing()
    {
        var plan = TempRoot.Plan(@"R:\Temp", @"R:\Temp\x", @"R:\Temp", rootExists: true);
        Assert.That(plan.Set, Is.Empty);
        Assert.That(plan.Warning, Is.Null);
    }

    [Test]
    public void Temp_on_C_is_moved_to_the_root_with_tmp_and_the_dotnet_bundle_folder()
    {
        var plan = TempRoot.Plan(@"C:\Users\ryan\AppData\Local\Temp", @"R:\Temp", @"R:\Temp", rootExists: true);
        Assert.That(plan.Set.ToDictionary(p => p.Key, p => p.Value), Is.EqualTo(new Dictionary<string, string>
        {
            ["TEMP"] = @"R:\Temp",
            ["TMP"] = @"R:\Temp",
            [TempRoot.BundleVariable] = @"R:\Temp\.net",
        }));
        Assert.That(plan.Warning, Does.Contain(@"C:\Users\ryan\AppData\Local\Temp").And.Contain(@"R:\Temp"));
    }

    [Test]
    public void An_unset_tmp_is_moved_too() =>
        Assert.That(TempRoot.Plan(@"R:\Temp", null, @"R:\Temp", rootExists: true).Set.Select(p => p.Key), Does.Contain("TMP"));

    [Test]
    public void A_missing_root_leaves_temp_alone_but_warns()
    {
        var plan = TempRoot.Plan(@"C:\Temp", @"C:\Temp", @"Q:\Nowhere", rootExists: false);
        Assert.That(plan.Set, Is.Empty);
        Assert.That(plan.Warning, Does.Contain(@"Q:\Nowhere"));
    }

    [Test]
    public void The_health_check_warns_for_a_scope_whose_temp_is_off_the_root()
    {
        var off = TempRootCheck.Grade("system", @"%SystemRoot%\TEMP", null, @"R:\Temp", Now);
        Assert.That((off.CheckId, off.ComponentId, off.Grade, off.ObservedState), Is.EqualTo(("temp:root", "system", HealthGrade.Degraded, @"%SystemRoot%\TEMP")));
        Assert.That(TempRootCheck.Grade("user", @"R:\Temp", null, @"R:\Temp", Now).Grade, Is.EqualTo(HealthGrade.Healthy));
    }

    [Test]
    public void An_unreadable_scope_is_degraded_and_an_unset_one_inherits()
    {
        var denied = TempRootCheck.Grade("system", null, "Requested registry access is not allowed.", @"R:\Temp", Now);
        Assert.That((denied.Grade, denied.ObservedState), Is.EqualTo((HealthGrade.Degraded, "unreadable")));
        Assert.That(TempRootCheck.Grade("user", null, null, @"R:\Temp", Now).Grade, Is.EqualTo(HealthGrade.Healthy));
    }
}
