using MindAttic.Orchestrator.Settings;
using MindAttic.Vault.Settings;
using MindAttic.Vault.Settings.Manifest;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>
/// M.ORCH-T2175: the sidebar panels' order and collapsed set (<see cref="OrchestratorSettings.SidebarPanelOrder"/>,
/// <see cref="OrchestratorSettings.SidebarCollapsedPanels"/>) are saved through the settings store the way the theme switch
/// saves (<see cref="JsonStore.LoadSettingsToChange"/> then <see cref="JsonStore.SaveSettings"/>), reach the Vault file and the
/// row, and read back unchanged; they stay out of the Settings manifest.
/// </summary>
public sealed class SidebarPanelSettingsTests
{
    private string dir = "";
    private JsonSettingsStore<OrchestratorVaultSettings> vault = null!;

    [SetUp]
    public void SetUp()
    {
        dir = Path.Combine(Path.GetTempPath(), "orch-sidebar-" + Guid.NewGuid().ToString("N"));
        vault = new JsonSettingsStore<OrchestratorVaultSettings>(Path.Combine(dir, "vault", OrchestratorVaultSettings.AppId));
    }

    [TearDown]
    public void TearDown()
    {
        try { if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    private JsonStore Store() => new(Path.Combine(dir, "store"), vault: vault);

    [Test]
    public void Both_lists_default_to_empty()
    {
        var s = Store().LoadSettings();
        Assert.That(s.SidebarPanelOrder, Is.Empty, "empty is the declared order");
        Assert.That(s.SidebarCollapsedPanels, Is.Empty, "empty is every panel expanded");
        Assert.That(new OrchestratorVaultSettings().ApplyTo(new OrchestratorSettings()).SidebarPanelOrder, Is.Empty);
    }

    [Test]
    public void Both_lists_survive_a_save_and_a_reload()
    {
        var store = Store();
        store.SaveSettings(store.LoadSettingsToChange() with
        {
            SidebarPanelOrder = ["tickets", "users", "orgs", "host"],
            SidebarCollapsedPanels = ["host", "tickets.pending"],
        });

        var reloaded = Store().LoadSettings();
        Assert.That(reloaded.SidebarPanelOrder, Is.EqualTo(new[] { "tickets", "users", "orgs", "host" }));
        Assert.That(reloaded.SidebarCollapsedPanels, Is.EqualTo(new[] { "host", "tickets.pending" }));

        var v = vault.Load();
        Assert.That(v.SidebarPanelOrder, Is.EqualTo(new[] { "tickets", "users", "orgs", "host" }), "mirrored into the Vault file");
        Assert.That(v.SidebarCollapsedPanels, Is.EqualTo(new[] { "host", "tickets.pending" }));
        Assert.That(new JsonStore(Path.Combine(dir, "store")).LoadSettings().SidebarCollapsedPanels,
            Is.EqualTo(new[] { "host", "tickets.pending" }), "and the row");
    }

    [Test]
    public void Neither_is_shown_in_the_Settings_form()
    {
        var names = SettingsManifestBuilder.Build<OrchestratorVaultSettings>().AllFields().Select(f => f.Name).ToList();
        Assert.That(names, Has.None.Contains("sidebarPanelOrder").IgnoreCase);
        Assert.That(names, Has.None.Contains("sidebarCollapsedPanels").IgnoreCase);
    }
}
