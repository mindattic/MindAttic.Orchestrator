using System.Text.Json.Nodes;
using MindAttic.Orchestrator.Settings;
using MindAttic.Vault.Settings;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>
/// Global default, app override (M.ORCH-T1328, M.VALT-T77): the orchestrator's alert recipient (<c>psstAlertTo</c>) and
/// workspace root in Vault are optional overrides of Vault Global's <c>alertRecipient</c> and <c>workspaceRoot</c>. Blank follows
/// Global, set overrides it, and the database row the hooks read gets the resolved value. Every test has its own scratch
/// database, Vault folder and Global (handed to the store, never the user's real one).
/// </summary>
public sealed class GlobalFallbackTests
{
    private string dir = "";
    private string storeDir = "";
    private JsonSettingsStore<OrchestratorVaultSettings> vault = null!;
    private MindAtticGlobalSettings global = new();

    [SetUp]
    public void SetUp()
    {
        dir = Path.Combine(Path.GetTempPath(), "orch-global-" + Guid.NewGuid().ToString("N"));
        storeDir = Path.Combine(dir, "store");
        vault = new JsonSettingsStore<OrchestratorVaultSettings>(Path.Combine(dir, "vault", OrchestratorVaultSettings.AppId));
        global = new MindAtticGlobalSettings();
    }

    [TearDown]
    public void TearDown()
    {
        try { if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    /// <summary>The store as the orchestrator runs it, with this test's Global.</summary>
    private JsonStore Store() => new(storeDir, vault: vault, global: () => global);

    /// <summary>The row alone, as the hooks read it.</summary>
    private JsonStore Row() => new(storeDir);

    /// <summary>The store after a first read, with the Vault file in place.</summary>
    private JsonStore Started()
    {
        var store = Store();
        store.LoadSettings();
        Assume.That(vault.Exists(), Is.True);
        return store;
    }

    // ── Alert recipient ──────────────────────────────────────────────────

    [Test]
    public void A_blank_alert_recipient_follows_Global()
    {
        global = new MindAtticGlobalSettings { AlertRecipient = "jordan" };
        var store = Started();
        vault.Update(v => v.PsstAlertTo = "");
        Assert.That(store.LoadSettings().PsstAlertTo, Is.EqualTo("jordan"));
        Assert.That(vault.Load().PsstAlertTo, Is.Empty, "the Vault keeps no copy: it still follows Global");

        // A Global edit is followed at once, with nothing changed in the orchestrator's own file.
        global = new MindAtticGlobalSettings { AlertRecipient = "920-555-0101" };
        Assert.That(store.LoadSettings().PsstAlertTo, Is.EqualTo("920-555-0101"));
        Assert.That(vault.Load().PsstAlertTo, Is.Empty);
    }

    [Test]
    public void A_set_alert_recipient_overrides_Global()
    {
        global = new MindAtticGlobalSettings { AlertRecipient = "jordan" };
        var store = Started();
        vault.Update(v => v.PsstAlertTo = "920-555-0101");
        Assert.That(store.LoadSettings().PsstAlertTo, Is.EqualTo("920-555-0101"));
        Assert.That(store.SyncVaultSettings(), Is.True);
        Assert.That(vault.Load().PsstAlertTo, Is.EqualTo("920-555-0101"));
    }

    [Test]
    public void None_texts_nobody_whatever_Global_says()
    {
        global = new MindAtticGlobalSettings { AlertRecipient = "jordan" };
        var store = Started();
        vault.Update(v => v.PsstAlertTo = OrchestratorVaultSettings.NoAlerts);
        Assert.That(store.LoadSettings().PsstAlertTo, Is.Empty);
    }

    [Test]
    public void Blank_with_a_blank_Global_texts_the_default_recipient_as_before()
    {
        var store = Started();
        vault.Update(v => v.PsstAlertTo = "");
        Assert.That(store.LoadSettings().PsstAlertTo, Is.EqualTo(OrchestratorSettings.DefaultPsstAlertTo));
    }

    [Test]
    public void The_hooks_row_gets_the_resolved_alert_recipient_and_follows_a_Global_edit()
    {
        global = new MindAtticGlobalSettings { AlertRecipient = "jordan" };
        var store = Started();
        vault.Update(v => v.PsstAlertTo = "");
        Assert.That(store.SyncVaultSettings(), Is.True);
        Assert.That(Row().LoadSettings().PsstAlertTo, Is.EqualTo("jordan"), "the row holds the resolved recipient, not a blank");
        Assert.That(RowValue("PsstAlertTo"), Is.EqualTo("\"jordan\""), "as the hooks read it through sqlite3");
        Assert.That(store.SyncVaultSettings(), Is.False, "in sync now");

        // Only Global changes: the daemon's sync still brings the row along, and the Vault file keeps following Global.
        global = new MindAtticGlobalSettings { AlertRecipient = "920-555-0101" };
        Assert.That(store.SyncVaultSettings(), Is.True);
        Assert.That(Row().LoadSettings().PsstAlertTo, Is.EqualTo("920-555-0101"));
        Assert.That(store.SyncVaultSettings(), Is.False);
        Assert.That(vault.Load().PsstAlertTo, Is.Empty, "a Global edit never freezes into an override");
        Assert.That(store.LoadSettings().PsstAlertTo, Is.EqualTo("920-555-0101"));
    }

    [Test]
    public void A_row_from_before_carries_its_recipient_over_unchanged()
    {
        // Before M.ORCH-T1328: the row held the recipient itself, and so did the Vault file.
        Row().SaveSettings(new OrchestratorSettings { PsstAlertTo = "920-555-0101" });
        global = new MindAtticGlobalSettings { AlertRecipient = "jordan" };
        Assert.That(Store().LoadSettings().PsstAlertTo, Is.EqualTo("920-555-0101"), "an explicit recipient is the override");
        Assert.That(vault.Load().PsstAlertTo, Is.EqualTo("920-555-0101"));
    }

    [Test]
    public void The_default_recipient_from_before_follows_Global()
    {
        // "ryan", the old default, imports as "follow Global" (whose own fallback is that same default).
        Row().SaveSettings(new OrchestratorSettings());
        global = new MindAtticGlobalSettings { AlertRecipient = "ryan" };
        var store = Store();
        Assert.That(store.LoadSettings().PsstAlertTo, Is.EqualTo("ryan"));
        Assert.That(vault.Load().PsstAlertTo, Is.Empty);
    }

    [Test]
    public void The_CLIs_alert_to_sets_clears_and_stops_the_override()
    {
        global = new MindAtticGlobalSettings { AlertRecipient = "jordan" };
        var store = Started();
        store.SaveSettings(OrchestratorVaultSettings.WithPsstAlertTo(store.LoadSettingsToChange(), "920-555-0101", store.LoadGlobal()));
        Assert.That((store.LoadSettings().PsstAlertTo, vault.Load().PsstAlertTo), Is.EqualTo(("920-555-0101", "920-555-0101")));

        store.SaveSettings(OrchestratorVaultSettings.WithPsstAlertTo(store.LoadSettingsToChange(), "", store.LoadGlobal()));
        Assert.That((store.LoadSettings().PsstAlertTo, vault.Load().PsstAlertTo), Is.EqualTo(("jordan", "")), "back to following Global");
        Assert.That(Row().LoadSettings().PsstAlertTo, Is.EqualTo("jordan"));

        store.SaveSettings(OrchestratorVaultSettings.WithPsstAlertTo(store.LoadSettingsToChange(), "none", store.LoadGlobal()));
        Assert.That((store.LoadSettings().PsstAlertTo, vault.Load().PsstAlertTo), Is.EqualTo(("", "none")));
    }

    // ── Workspace root ───────────────────────────────────────────────────

    [Test]
    public void A_blank_workspace_root_follows_Global()
    {
        global = new MindAtticGlobalSettings { WorkspaceRoot = @"D:\Projects\MindAttic" };
        var store = Started();
        vault.Update(v => v.WorkspaceRoot = null);
        Assert.That(store.LoadSettings().WorkspaceRoot, Is.EqualTo(@"D:\Projects\MindAttic"));
        Assert.That(OrchestratorOptions.Load(store).WorkspaceRoot, Is.EqualTo(@"D:\Projects\MindAttic"), "the daemon's and the CLI's workspace");
        vault.Update(v => v.WorkspaceRoot = "  ");
        Assert.That(store.LoadSettings().WorkspaceRoot, Is.EqualTo(@"D:\Projects\MindAttic"), "whitespace is blank");
    }

    [Test]
    public void A_set_workspace_root_overrides_Global()
    {
        global = new MindAtticGlobalSettings { WorkspaceRoot = @"D:\Projects\MindAttic" };
        var store = Started();
        vault.Update(v => v.WorkspaceRoot = @"E:\Work");
        Assert.That(store.LoadSettings().WorkspaceRoot, Is.EqualTo(@"E:\Work"));
        Assert.That(OrchestratorOptions.Load(store).WorkspaceRoot, Is.EqualTo(@"E:\Work"));
    }

    [Test]
    public void Both_blank_is_the_Tickets_default_workspace_as_before()
    {
        var store = Started();
        vault.Update(v => v.WorkspaceRoot = null);
        Assert.That(store.LoadSettings().WorkspaceRoot, Is.Null);
        Assert.That(OrchestratorOptions.Load(store).WorkspaceRoot, Is.EqualTo(new OrchestratorOptions().WorkspaceRoot));
    }

    [Test]
    public void The_hooks_row_gets_the_resolved_workspace_root()
    {
        global = new MindAtticGlobalSettings { WorkspaceRoot = @"D:\Projects\MindAttic" };
        var store = Started();
        vault.Update(v => v.WorkspaceRoot = null);
        Assert.That(store.SyncVaultSettings(), Is.True);
        Assert.That(Row().LoadSettings().WorkspaceRoot, Is.EqualTo(@"D:\Projects\MindAttic"));
        Assert.That(RowValue("WorkspaceRoot"), Is.EqualTo(JsonValue.Create(@"D:\Projects\MindAttic").ToJsonString()), "what AgentGate's hook reads");
        Assert.That(store.SyncVaultSettings(), Is.False);
        Assert.That(vault.Load().WorkspaceRoot, Is.Null, "still following Global");

        global = new MindAtticGlobalSettings { WorkspaceRoot = @"F:\Repos" };
        Assert.That(store.SyncVaultSettings(), Is.True);
        Assert.That(Row().LoadSettings().WorkspaceRoot, Is.EqualTo(@"F:\Repos"));
        Assert.That(vault.Load().WorkspaceRoot, Is.Null);
    }

    [Test]
    public void The_dashboards_workspace_picker_saves_an_override()
    {
        global = new MindAtticGlobalSettings { WorkspaceRoot = @"D:\Projects\MindAttic" };
        var store = Started();
        store.SaveSettings(OrchestratorVaultSettings.WithWorkspaceRoot(store.LoadSettingsToChange(), @"E:\Picked", store.LoadGlobal()));
        Assert.That((store.LoadSettings().WorkspaceRoot, vault.Load().WorkspaceRoot), Is.EqualTo((@"E:\Picked", @"E:\Picked")));
        Assert.That(Row().LoadSettings().WorkspaceRoot, Is.EqualTo(@"E:\Picked"));
    }

    // ── The mapping ──────────────────────────────────────────────────────

    [TestCase("", "jordan", "jordan")]
    [TestCase("920-555-0101", "jordan", "920-555-0101")]
    [TestCase("none", "jordan", "")]
    [TestCase("", "", OrchestratorSettings.DefaultPsstAlertTo)]
    [TestCase("ryan", "jordan", "ryan")]
    public void Every_alert_value_round_trips_through_the_row_as_its_own_override(string vaultValue, string globalValue, string resolved)
    {
        var g = new MindAtticGlobalSettings { AlertRecipient = globalValue };
        var s = OrchestratorVaultSettings.WithPsstAlertTo(new OrchestratorSettings(), vaultValue, g);
        Assert.That(s.PsstAlertTo, Is.EqualTo(resolved));
        Assert.That(OrchestratorVaultSettings.From(s).PsstAlertTo, Is.EqualTo(vaultValue), "the Vault value is never the resolved one");
        var back = System.Text.Json.JsonSerializer.Deserialize<OrchestratorSettings>(System.Text.Json.JsonSerializer.Serialize(s))!;
        Assert.That(OrchestratorVaultSettings.From(back).PsstAlertTo, Is.EqualTo(vaultValue), "and survives the row");
    }

    [TestCase(null, @"D:\Projects\MindAttic", @"D:\Projects\MindAttic")]
    [TestCase(@"E:\Work", @"D:\Projects\MindAttic", @"E:\Work")]
    [TestCase(null, "", null)]
    [TestCase(@"E:\Work", "", @"E:\Work")]
    public void Every_workspace_value_round_trips_through_the_row_as_its_own_override(string? vaultValue, string globalValue, string? resolved)
    {
        var g = new MindAtticGlobalSettings { WorkspaceRoot = globalValue };
        var s = OrchestratorVaultSettings.WithWorkspaceRoot(new OrchestratorSettings(), vaultValue, g);
        Assert.That(s.WorkspaceRoot, Is.EqualTo(resolved));
        Assert.That(OrchestratorVaultSettings.From(s).WorkspaceRoot, Is.EqualTo(vaultValue));
        var back = System.Text.Json.JsonSerializer.Deserialize<OrchestratorSettings>(System.Text.Json.JsonSerializer.Serialize(s))!;
        Assert.That(OrchestratorVaultSettings.From(back).WorkspaceRoot, Is.EqualTo(vaultValue));
    }

    [Test]
    public void A_scratch_store_reads_no_Global()
    {
        Assert.That(Row().LoadGlobal(), Is.Null);
        Assert.That(Store().LoadGlobal(), Is.SameAs(global));
    }

    /// <summary>One setting's value in the database row, exactly as stored.</summary>
    private string? RowValue(string name)
    {
        using var c = Row().Db.Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT value FROM settings WHERE name = @name;";
        var p = cmd.CreateParameter();
        p.ParameterName = "@name";
        p.Value = name;
        cmd.Parameters.Add(p);
        return cmd.ExecuteScalar() as string;
    }
}
