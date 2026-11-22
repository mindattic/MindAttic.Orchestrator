using MindAttic.Orchestrator.Sessions;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>M.ORCH-T2026: a Launcher project's Vault deep link, its roster index's tabAlias row (the Launcher's ProjectSetupMenu.SettingFor).</summary>
public sealed class LauncherProjectSettingsTests
{
    [Test]
    public void A_project_deep_links_to_its_raw_roster_index_by_name_or_folder_and_a_missing_one_to_nothing()
    {
        var file = Path.Combine(Path.GetTempPath(), $"launcher-settings-{Guid.NewGuid():N}.json");
        File.WriteAllText(file, """{ "projects": [ { "name": "Zeta", "path": "D:\\z" }, { "name": "Beta", "path": "D:\\b", "tabAlias": "B" } ] }""");
        try
        {
            Assert.That(LauncherProjectSettings.SettingFor("beta", settingsPath: file),
                Is.EqualTo("MindAttic:Settings:MindAttic.Launcher:projects:1:tabAlias"), "the raw array index, not the sorted roster's");
            Assert.That(LauncherProjectSettings.SettingFor("Other", @"D:\Z\", file),
                Is.EqualTo("MindAttic:Settings:MindAttic.Launcher:projects:0:tabAlias"), "no name match falls back to the folder");
            Assert.That(LauncherProjectSettings.SettingFor("Gone", settingsPath: file), Is.Null);
            Assert.That(LauncherProjectSettings.SettingFor("Beta", settingsPath: file + ".missing"), Is.Null);
        }
        finally { File.Delete(file); }
    }
}
