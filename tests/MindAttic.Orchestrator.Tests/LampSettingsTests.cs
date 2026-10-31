using System.Text.Json.Nodes;
using MindAttic.Orchestrator.Billing;
using MindAttic.Orchestrator.Settings;
using MindAttic.Vault.Credentials;
using MindAttic.Vault.Launch;
using MindAttic.Vault.Settings;
using MindAttic.Vault.Settings.Manifest;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>
/// LAMP (M.ORCH-T1308): the orchestrator's app-level settings live in MindAttic Vault (<see cref="OrchestratorVaultSettings"/>,
/// <c>%LOCALAPPDATA%\MindAttic\Orchestrator\settings.json</c>), overlaid on the database's settings row, which the hooks keep
/// reading. One test case per setting proves it end to end: today's row value is imported into the Vault once, the Vault's value
/// is the one the app reads, and an edit in Vault.Desktop reaches the row the hooks read. Every test here has its own scratch
/// database (<see cref="OrchestratorDbPerTestAttribute"/>) and Vault folder; <c>VaultIsolation</c> keeps the real AppData out.
/// </summary>
public sealed class LampSettingsTests
{
    private const string Prefix = "MindAttic:Settings:Orchestrator:";

    private string dir = "";
    private string storeDir = "";
    private JsonSettingsStore<OrchestratorVaultSettings> vault = null!;
    private IVaultDesktopLauncher launcherBefore = null!;

    [SetUp]
    public void SetUp()
    {
        dir = Path.Combine(Path.GetTempPath(), "orch-lamp-" + Guid.NewGuid().ToString("N"));
        storeDir = Path.Combine(dir, "store");
        vault = new JsonSettingsStore<OrchestratorVaultSettings>(Path.Combine(dir, "vault", OrchestratorVaultSettings.AppId));
        launcherBefore = VaultSettings.Launcher;
    }

    [TearDown]
    public void TearDown()
    {
        VaultSettings.Launcher = launcherBefore;
        try { if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    /// <summary>The store as the orchestrator runs it: the Vault overlaid on the row.</summary>
    private JsonStore Store() => new(storeDir, vault: vault);

    /// <summary>The row alone, as the store was before LAMP and as the hooks read it.</summary>
    private JsonStore Row() => new(storeDir);

    // ── One test per setting ──────────────────────────────────────────────

    /// <summary>One setting: its manifest name; how today's row is given value <see cref="Old"/>; how Vault.Desktop sets
    /// <see cref="New"/>; and what the app reads it as.</summary>
    public sealed record SettingCase(string Name, Func<OrchestratorSettings, OrchestratorSettings> PutOld, object? Old,
        Action<OrchestratorVaultSettings> PutNew, object? New, Func<OrchestratorSettings, object?> Read)
    {
        public override string ToString() => Name;
    }

    private static SettingCase Case(string name, Func<OrchestratorSettings, OrchestratorSettings> putOld, object? old,
        Action<OrchestratorVaultSettings> putNew, object? @new, Func<OrchestratorSettings, object?> read) => new(name, putOld, old, putNew, @new, read);

    public static IEnumerable<TestCaseData> Cases() => All.Select(c => new TestCaseData(c).SetName("Setting_" + c.Name.Replace(':', '_')));

    public static readonly IReadOnlyList<SettingCase> All =
    [
        // Budget
        Case("blockBudget", s => s with { BlockBudget = 20_000_000 }, 20_000_000L, v => v.BlockBudget = 35_000_000, 35_000_000L, s => s.BlockBudget),
        Case("weeklyBudget", s => s with { WeeklyBudget = 150_000_000 }, 150_000_000L, v => v.WeeklyBudget = 90_000_000, 90_000_000L, s => s.WeeklyBudget),
        Case("weeklyReset", s => s with { WeeklyReset = "Monday 09:00" }, "Monday 09:00", v => v.WeeklyReset = "Tuesday 17:00", "Tuesday 17:00", s => s.WeeklyReset),
        Case("warnPercent", s => s with { WarnPercent = 70 }, 70, v => v.WarnPercent = 85, 85, s => s.WarnPercent),
        Case("weeklyReservePercent", s => s with { WeeklyReservePercent = 5 }, 5, v => v.WeeklyReservePercent = 20, 20, s => s.WeeklyReservePercent),
        // Pacing
        Case("pacing", s => s with { Pacing = false }, false, v => v.Pacing = true, true, s => s.Pacing),
        Case("paceTargetPercent", s => s with { PaceTargetPercent = 90 }, 90, v => v.PaceTargetPercent = 95, 95, s => s.PaceTargetPercent),
        Case("exemptCommands", s => s with { ExemptCommands = ["/pacing", "/deploy", "/run"] }, new[] { "/pacing", "/deploy", "/run" },
            v => v.ExemptCommands = ["/commit"], new[] { "/commit" }, s => s.ExemptCommands),
        Case("reportFreshMinutes", s => s with { ReportFreshMinutes = 5 }, 5, v => v.ReportFreshMinutes = 9, 9, s => s.ReportFreshMinutes),
        Case("providerCapPercent", s => s with { ProviderCapPercent = 80 }, 80, v => v.ProviderCapPercent = 60, 60, s => s.ProviderCapPercent),
        Case("weeklyAwareLinePercent", s => s with { WeeklyAwareLinePercent = 70 }, 70, v => v.WeeklyAwareLinePercent = 60, 60, s => s.WeeklyAwareLinePercent),
        Case("paceCurveK", s => s with { PaceCurveK = 0.1 }, 0.1, v => v.PaceCurveK = 0.8, 0.8, s => s.PaceCurveK),
        Case("maxAgentDelaySeconds", s => s with { MaxAgentDelaySeconds = 120 }, 120, v => v.MaxAgentDelaySeconds = 30, 30, s => s.MaxAgentDelaySeconds),
        Case("agentAbort", s => s with { AgentAbort = false }, false, v => v.AgentAbort = true, true, s => s.AgentAbort),
        Case("agentAbortAfterMinutes", s => s with { AgentAbortAfterMinutes = 7 }, 7, v => v.AgentAbortAfterMinutes = 12, 12, s => s.AgentAbortAfterMinutes),
        // Scheduler
        Case("allocate", s => s with { Allocate = false }, false, v => v.Allocate = true, true, s => s.Allocate),
        Case("maxConcurrentSessions", s => s with { MaxConcurrentSessions = 6 }, 6, v => v.MaxConcurrentSessions = 9, 9, s => s.MaxConcurrentSessions),
        Case("maxSessionsPerRepo", s => s with { MaxSessionsPerRepo = 3 }, 3, v => v.MaxSessionsPerRepo = 1, 1, s => s.MaxSessionsPerRepo),
        Case("maxSubagents", s => s with { MaxSubagents = 8 }, 8, v => v.MaxSubagents = 24, 24, s => s.MaxSubagents),
        Case("defaultProvider", s => s with { DefaultProvider = "Gemini" }, "Gemini", v => v.DefaultProvider = "Kimi", "Kimi", s => s.DefaultProvider),
        Case("keepGoing", s => s with { KeepGoing = false }, false, v => v.KeepGoing = true, true, s => s.KeepGoing),
        Case("resumeAfterReset", s => s with { ResumeAfterReset = false }, false, v => v.ResumeAfterReset = true, true, s => s.ResumeAfterReset),
        Case("quietHours", s => s with { QuietHours = ["22:00-07:00"] }, new[] { "22:00-07:00" }, v => v.QuietHours = ["12:00-13:00", "23:00-06:00"],
            new[] { "12:00-13:00", "23:00-06:00" }, s => s.QuietHours),
        Case("allocateSkip", s => s with { AllocateSkip = ["Prose"] }, new[] { "Prose" }, v => v.AllocateSkip = ["GTFO.EZ"], new[] { "GTFO.EZ" }, s => s.AllocateSkip),
        Case("allocateHandMinutes", s => s with { AllocateHandMinutes = 20 }, 20, v => v.AllocateHandMinutes = 0, 0, s => s.AllocateHandMinutes),
        Case("staleAfterHours", s => s with { StaleAfterHours = 24 }, 24, v => v.StaleAfterHours = 6, 6, s => s.StaleAfterHours),
        Case("contextCyclePercent", s => s with { ContextCyclePercent = 70 }, 70, v => v.ContextCyclePercent = 90, 90, s => s.ContextCyclePercent),
        Case("orphanRedispatches", s => s with { OrphanRedispatches = 0 }, 0, v => v.OrphanRedispatches = 5, 5, s => s.OrphanRedispatches),
        Case("orphanQuietMinutes", s => s with { OrphanQuietMinutes = 30 }, 30, v => v.OrphanQuietMinutes = 45, 45, s => s.OrphanQuietMinutes),
        // Logins
        Case("autoLogin", s => s with { AutoLogin = false }, false, v => v.AutoLogin = true, true, s => s.AutoLogin),
        Case("rebalanceLogins", s => s with { RebalanceLogins = false }, false, v => v.RebalanceLogins = true, true, s => s.RebalanceLogins),
        // Reviews
        Case("reviewDiffLines", s => s with { ReviewDiffLines = -1 }, -1, v => v.ReviewDiffLines = 50, 50, s => s.ReviewDiffLines),
        Case("riskyFiles", s => s with { RiskyFiles = ["*run.ps1"] }, new[] { "*run.ps1" }, v => v.RiskyFiles = ["*/hooks/*", "*Pacer.cs"],
            new[] { "*/hooks/*", "*Pacer.cs" }, s => s.RiskyFiles),
        // Alerts
        Case("psstAlertTo", s => s with { PsstAlertTo = "" }, "", v => v.PsstAlertTo = "920 555 0101", "920 555 0101", s => s.PsstAlertTo),
        Case("notifyUrl", s => s with { NotifyUrl = "https://ntfy.sh/old-topic" }, "https://ntfy.sh/old-topic", v => v.NotifyUrl = "https://ntfy.sh/new-topic",
            "https://ntfy.sh/new-topic", s => s.NotifyUrl),
        Case("diskWarnGb", s => s with { DiskWarnGb = 30 }, 30d, v => v.DiskWarnGb = 0, 0d, s => s.DiskWarnGb),
        Case("diskCriticalGb", s => s with { DiskCriticalGb = 2 }, 2d, v => v.DiskCriticalGb = 8.5, 8.5, s => s.DiskCriticalGb),
        Case("auditEveryHours", s => s with { AuditEveryHours = 0 }, 0, v => v.AuditEveryHours = 6, 6, s => s.AuditEveryHours),
        Case("groomAfterDays", s => s with { GroomAfterDays = 14 }, 14, v => v.GroomAfterDays = 60, 60, s => s.GroomAfterDays),
        Case("gitHubIntake", s => s with { GitHubIntake = true }, true, v => v.GitHubIntake = false, false, s => s.GitHubIntake),
        // Dashboard
        Case("theme", s => s with { Theme = "Light" }, "Light", v => v.Theme = "Dark", "Dark", s => s.Theme),
        Case("symbolTabLabels", s => s with { SymbolTabLabels = false }, false, v => v.SymbolTabLabels = true, true, s => s.SymbolTabLabels),
        Case("tabBlinkTokens", s => s with { TabBlinkTokens = 5000 }, 5000, v => v.TabBlinkTokens = 250, 250, s => s.TabBlinkTokens),
        Case("excludeLauncherFromTeamWall", s => s with { ExcludeLauncherFromTeamWall = true }, true, v => v.ExcludeLauncherFromTeamWall = false, false,
            s => s.ExcludeLauncherFromTeamWall),
        // Folders
        Case("workspaceRoot", s => s with { WorkspaceRoot = @"E:\Work" }, @"E:\Work", v => v.WorkspaceRoot = @"F:\Repos", @"F:\Repos", s => s.WorkspaceRoot),
        Case("worktreeRoot", s => s with { WorktreeRoot = @"E:\wt" }, @"E:\wt", v => v.WorktreeRoot = @"F:\wt", @"F:\wt", s => s.WorktreeRoot),
        // Gateway
        Case("gateway:enabled", s => s with { Gateway = s.Gateway with { Enabled = true } }, true, v => v.Gateway.Enabled = false, false, s => s.Gateway.Enabled),
        Case("gateway:bind", s => s with { Gateway = s.Gateway with { Bind = "100.64.0.1" } }, "100.64.0.1", v => v.Gateway.Bind = "0.0.0.0", "0.0.0.0", s => s.Gateway.Bind),
        Case("gateway:port", s => s with { Gateway = s.Gateway with { Port = 8443 } }, 8443, v => v.Gateway.Port = 9443, 9443, s => s.Gateway.Port),
        Case("gateway:certPath", s => s with { Gateway = s.Gateway with { CertPath = @"C:\certs\old.pfx" } }, @"C:\certs\old.pfx",
            v => v.Gateway.CertPath = @"C:\certs\new.pfx", @"C:\certs\new.pfx", s => s.Gateway.CertPath),
        Case("gateway:certThumbprint", s => s with { Gateway = s.Gateway with { CertThumbprint = "AB12" } }, "AB12", v => v.Gateway.CertThumbprint = "CD34", "CD34",
            s => s.Gateway.CertThumbprint),
        Case("gateway:devCertNames", s => s with { Gateway = s.Gateway with { DevCertNames = ["box.tail.ts.net"] } }, new[] { "box.tail.ts.net" },
            v => v.Gateway.DevCertNames = ["100.64.0.9"], new[] { "100.64.0.9" }, s => s.Gateway.DevCertNames),
        Case("gateway:owner", s => s with { Gateway = s.Gateway with { Owner = "ryan" } }, "ryan", v => v.Gateway.Owner = "jordan", "jordan", s => s.Gateway.Owner),
        Case("gateway:tokenMinutes", s => s with { Gateway = s.Gateway with { TokenMinutes = 30 } }, 30, v => v.Gateway.TokenMinutes = 5, 5, s => s.Gateway.TokenMinutes),
    ];

    [TestCaseSource(nameof(Cases))]
    public void Each_setting_is_imported_once_then_read_from_the_Vault_and_mirrored_to_the_hooks_row(SettingCase c)
    {
        // Today: the value lives in the database row, written before LAMP.
        var row = Row();
        row.SaveSettings(c.PutOld(row.LoadSettings()));
        Assume.That(c.Read(row.LoadSettings()), Is.EqualTo(c.Old), "the row holds today's value");
        Assert.That(vault.Exists(), Is.False);

        // First read under LAMP imports it into the Vault, and it is the value in force: no change for the user.
        var store = Store();
        Assert.That(c.Read(store.LoadSettings()), Is.EqualTo(c.Old), "today's value still in force after the import");
        Assert.That(vault.Exists(), Is.True, "imported into the Vault file");
        Assert.That(c.Read(vault.Load().ApplyTo(new OrchestratorSettings())), Is.EqualTo(c.Old), "the Vault file holds today's value");
        Assert.That(JsonNode.Parse(File.ReadAllText(vault.FilePath))!.AsObject().ContainsKey(c.Name.Split(':')[0]), Is.True,
            "stored under its manifest name");

        // An edit in MindAttic.Vault.Desktop: the Vault's value is the one the app reads, at once.
        vault.Update(c.PutNew);
        Assert.That(c.Read(store.LoadSettings()), Is.EqualTo(c.New), "the Vault's value is what the app uses");
        Assert.That(c.Read(Store().LoadSettings()), Is.EqualTo(c.New), "a new process reads it too");

        // The daemon's sync copies it into the row the hooks read (stdlib sqlite3), and the status bus pushes it.
        Assert.That(store.SyncVaultSettings(), Is.True);
        Assert.That(c.Read(Row().LoadSettings()), Is.EqualTo(c.New), "the hooks' row has the Vault's value");
        Assert.That(store.SyncVaultSettings(), Is.False, "in sync now: nothing more is written");

        // The manifest Vault.Desktop renders names it.
        var names = SettingsManifestBuilder.Build<OrchestratorVaultSettings>().AllFields().Select(f => f.Name).ToList();
        Assert.That(names, Does.Contain(Prefix + c.Name));
    }

    [Test]
    public void EverySettingHasItsOwnTest()
    {
        var leaves = SettingsManifestBuilder.Build<OrchestratorVaultSettings>().AllFields()
            .Where(f => f.Kind != SettingKind.Object).Select(f => f.Name[Prefix.Length..]).OrderBy(n => n, StringComparer.Ordinal).ToList();
        var tested = All.Select(c => c.Name).OrderBy(n => n, StringComparer.Ordinal).ToList();
        Assert.That(tested, Is.Unique);
        Assert.That(tested, Is.EqualTo(leaves), "one case per Vault setting, no more, no fewer");
    }

    // ── Manifest ──────────────────────────────────────────────────────────

    [Test]
    public void The_manifest_builds_with_every_field_labeled_grouped_and_no_secrets()
    {
        var manifest = SettingsManifestBuilder.Build<OrchestratorVaultSettings>();
        Assert.That((manifest.AppId, manifest.DisplayName, manifest.Scope), Is.EqualTo(("Orchestrator", "MindAttic Orchestrator", SettingsManifest.LocalScope)),
            "per machine: the daemon, its folders, ports and database are this machine's");
        Assert.That(manifest.ConfigurationPrefix, Is.EqualTo("MindAttic:Settings:Orchestrator"));
        foreach (var field in manifest.AllFields())
        {
            Assert.That(field.Label, Is.Not.Empty, field.Name);
            Assert.That(field.Group, Is.Not.EqualTo(SettingsManifest.DefaultGroup), field.Name + " sits in a named group");
            Assert.That(field.Kind, Is.Not.EqualTo(SettingKind.Unknown), field.Name + " has an editor");
            Assert.That(field.Name, Does.Not.Contain("key").IgnoreCase.And.Not.Contain("password").IgnoreCase.And.Not.Contain("secret").IgnoreCase, "no secrets");
        }
        Assert.That(manifest.Credentials.Select(c => c.ProviderId), Is.EqualTo(new[] { "claude" }), "Claude's key, from the shared named keys");
        Assert.That(manifest.AllFields().Select(f => f.Group).Distinct(),
            Is.EquivalentTo(new[] { "Budget", "Pacing", "Scheduler", "Logins", "Reviews", "Alerts", "Dashboard", "Folders", "Gateway" }));
    }

    [Test]
    public void The_manifest_is_written_beside_the_Vault_file_on_the_first_read()
    {
        Store().LoadSettings();
        var manifest = SettingsManifestFile.TryRead(Path.Combine(vault.Directory, SettingsManifest.FileName));
        Assert.That(manifest, Is.Not.Null);
        Assert.That(manifest!.AppId, Is.EqualTo(OrchestratorVaultSettings.AppId));
    }

    [Test]
    public void The_manifest_command_imports_todays_settings_writes_the_manifest_and_exits()
    {
        var row = Row();
        row.SaveSettings(row.LoadSettings() with { MaxConcurrentSessions = 6, AutoLogin = false, PaceCurveK = 0.1 });
        var output = new StringWriter();
        Assert.That(OrchestratorSettingsManifest.TryRun(["--write-settings-manifest"], output, out var exit, Store()), Is.True);
        Assert.That(exit, Is.Zero, output.ToString());
        Assert.That(output.ToString(), Does.Contain(SettingsManifest.FileName));
        var imported = vault.Load();
        Assert.That((imported.MaxConcurrentSessions, imported.AutoLogin, imported.PaceCurveK), Is.EqualTo((6, false, 0.1)), "today's values, not defaults");
        Assert.That(File.Exists(Path.Combine(vault.Directory, SettingsManifest.FileName)), Is.True);

        Assert.That(OrchestratorSettingsManifest.TryRun(["status"], output, out _, Store()), Is.False, "anything else runs the app");
        Assert.That(OrchestratorSettingsManifest.TryRun(["--write-settings-manifest"], output, out exit, Row()), Is.True);
        Assert.That(exit, Is.EqualTo(1), "a scratch store has no Vault to write");
    }

    [Test]
    public void The_manifest_command_seeds_nothing_when_the_settings_row_cant_be_read()
    {
        Row().SaveSettings(new OrchestratorSettings());
        using (var c = Row().Db.Open())
        using (var cmd = c.CreateCommand())
        {
            cmd.CommandText = "UPDATE settings SET value = '{not json' WHERE name = 'WarnPercent';";
            cmd.ExecuteNonQuery();
        }
        Assert.That(OrchestratorSettingsManifest.TryRun(["--write-settings-manifest"], new StringWriter(), out var exit, Store()), Is.True);
        Assert.That(exit, Is.EqualTo(1));
        Assert.That(vault.Exists(), Is.False, "defaults never stand in for the real values");
    }

    // ── Defaults and import ───────────────────────────────────────────────

    [Test]
    public void LoadOrSeed_on_an_empty_folder_seeds_exactly_todays_defaults()
    {
        var seeded = vault.LoadOrSeed();
        Assert.That(seeded.SameAs(OrchestratorVaultSettings.From(new OrchestratorSettings())), Is.True,
            "the Vault's first-run defaults are the orchestrator's own");
        Assert.That(System.Text.Json.JsonSerializer.Serialize(seeded.ApplyTo(new OrchestratorSettings())),
            Is.EqualTo(System.Text.Json.JsonSerializer.Serialize(new OrchestratorSettings())));
    }

    [Test]
    public void A_fresh_install_reads_the_same_defaults_as_before()
    {
        var before = System.Text.Json.JsonSerializer.Serialize(Row().LoadSettings());
        Assert.That(System.Text.Json.JsonSerializer.Serialize(Store().LoadSettings()), Is.EqualTo(before));
        Assert.That(vault.Exists(), Is.True, "seeded");
    }

    [Test]
    public void Todays_real_settings_import_unchanged()
    {
        // The shape of this machine's row on 2026-10-31 (no secrets in it): every value the app read before is the value after.
        var today = new OrchestratorSettings
        {
            AutoLogin = false, RebalanceLogins = false, MaxConcurrentSessions = 6, OrphanRedispatches = 0, PaceCurveK = 0.1,
            ExemptCommands = ["/pacing", "/deploy", "/run"], Theme = "Dark", WorktreeRoot = @"D:\Projects\MindAttic.worktrees",
        };
        Row().SaveSettings(today);
        var after = Store().LoadSettings();
        Assert.That(System.Text.Json.JsonSerializer.Serialize(after), Is.EqualTo(System.Text.Json.JsonSerializer.Serialize(Row().LoadSettings())));
        Assert.That(System.Text.Json.JsonSerializer.Serialize(after), Is.EqualTo(System.Text.Json.JsonSerializer.Serialize(today)));
    }

    [Test]
    public void The_import_happens_once_and_never_overwrites_the_Vault()
    {
        Row().SaveSettings(new OrchestratorSettings { WarnPercent = 70 });
        Store().LoadSettings();
        vault.Update(v => v.WarnPercent = 85);
        Assert.That(Store().LoadSettings().WarnPercent, Is.EqualTo(85), "a second start doesn't import over the Vault");
        Assert.That(vault.Load().WarnPercent, Is.EqualTo(85));
    }

    [Test]
    public void The_old_row_is_left_as_it_was_by_the_import()
    {
        var row = Row();
        row.SaveSettings(new OrchestratorSettings { WarnPercent = 70, Reservations = new() { ["X-T1"] = 5 } });
        var before = System.Text.Json.JsonSerializer.Serialize(row.LoadSettings());
        Store().LoadSettings();
        Assert.That(System.Text.Json.JsonSerializer.Serialize(Row().LoadSettings()), Is.EqualTo(before), "the rollback");
    }

    [Test]
    public void What_the_Vault_doesnt_own_stays_in_the_row_through_every_sync()
    {
        var remote = new GatewayRemote { Name = "jordan", Url = "https://100.64.0.2:7443" };
        var row = Row();
        row.SaveSettings(new OrchestratorSettings
        {
            Reservations = new() { ["X-T1"] = 5 }, RepoOrgs = new() { ["Prose"] = "org-2" }, RepoMaxSessions = new() { ["Prose"] = 3 },
            RepoBudgetPercent = new() { ["Prose"] = 40 }, ModelOverrides = new() { ["GTFO.*"] = "sonnet" }, ApiCreditUsd = 25,
            Gateway = new GatewaySettings { Remotes = [remote] },
        });
        var store = Store();
        store.LoadSettings();
        vault.Update(v => { v.MaxConcurrentSessions = 9; v.Gateway.Port = 9443; });
        Assert.That(store.SyncVaultSettings(), Is.True);
        foreach (var s in new[] { store.LoadSettings(), Row().LoadSettings() })
        {
            Assert.That((s.MaxConcurrentSessions, s.Gateway.Port), Is.EqualTo((9, 9443)));
            Assert.That(s.Reservations["X-T1"], Is.EqualTo(5));
            Assert.That((s.RepoOrgs["Prose"], s.RepoMaxSessions["Prose"], s.RepoBudgetPercent["Prose"], s.ModelOverrides["GTFO.*"]),
                Is.EqualTo(("org-2", 3, 40, "sonnet")));
            Assert.That(s.ApiCreditUsd, Is.EqualTo(25));
            Assert.That(s.Gateway.Remotes.Single().Name, Is.EqualTo("jordan"), "the teammates (sealed tokens) never go to the Vault");
        }
        Assert.That(File.ReadAllText(vault.FilePath), Does.Not.Contain("remotes").IgnoreCase.And.Not.Contain("reservations").IgnoreCase);
    }

    // ── Writes, merges and live apply ─────────────────────────────────────

    [Test]
    public void A_change_the_orchestrator_makes_itself_is_written_to_the_Vault_too()
    {
        // orchestrator settings --max-sessions 8, /allocate off, the sun/moon switch: they save the settings through the store.
        var store = Store();
        store.SaveSettings(store.LoadSettingsToChange() with { MaxConcurrentSessions = 8, Allocate = false, Theme = "Light" });
        var v = vault.Load();
        Assert.That((v.MaxConcurrentSessions, v.Allocate, v.Theme), Is.EqualTo((8, false, "Light")));
        Assert.That(Row().LoadSettings().MaxConcurrentSessions, Is.EqualTo(8), "and the hooks' row");
        Assert.That(store.SyncVaultSettings(), Is.False, "already in agreement");

        // Update (a transaction) too: the change is made to the Vault's values.
        vault.Update(x => x.WarnPercent = 66);
        store.Update<OrchestratorSettings>(JsonStore.SettingsName, s => s.Approved = null);
        Assert.That((Row().LoadSettings().WarnPercent, vault.Load().WarnPercent), Is.EqualTo((66, 66)), "an Update never puts the stale row back");
    }

    [Test]
    public void A_write_that_reached_only_the_row_still_lands_and_a_Vault_edit_still_wins()
    {
        var store = Store();
        store.SaveSettings(store.LoadSettingsToChange() with { WarnPercent = 70, MaxSubagents = 10 });
        // A copy from before LAMP (still running while the new one starts) changes the row only.
        Row().SaveSettings(Row().LoadSettings() with { MaxSubagents = 4 });
        // Meanwhile Vault.Desktop changes another field.
        vault.Update(v => v.WarnPercent = 90);
        var merged = store.LoadSettings();
        Assert.That((merged.MaxSubagents, merged.WarnPercent), Is.EqualTo((4, 90)), "each side's change kept");
        Assert.That(store.SyncVaultSettings(), Is.True);
        Assert.That((vault.Load().MaxSubagents, Row().LoadSettings().WarnPercent), Is.EqualTo((4, 90)), "both now agree");

        // Both change the same field: the Vault's edit wins.
        Row().SaveSettings(Row().LoadSettings() with { MaxSubagents = 2 });
        vault.Update(v => v.MaxSubagents = 12);
        Assert.That(store.LoadSettings().MaxSubagents, Is.EqualTo(12));
    }

    [Test]
    public void An_unreadable_Vault_file_leaves_the_rows_values_in_force_never_the_defaults()
    {
        Row().SaveSettings(new OrchestratorSettings { MaxConcurrentSessions = 6, Allocate = false });
        Store().LoadSettings();
        File.WriteAllText(vault.FilePath, "{ \"maxConcurrentSessions\": 9, oops");
        var s = Store().LoadSettings();
        Assert.That((s.MaxConcurrentSessions, s.Allocate), Is.EqualTo((6, false)));
        Assert.That(Store().SyncVaultSettings(), Is.False, "nothing is synced from a file that can't be read");
        Assert.That(File.ReadAllText(vault.FilePath), Does.Contain("oops"), "left for you to fix");
    }

    [Test]
    public void A_settings_row_that_cant_be_read_is_never_overlaid_or_imported()
    {
        Row().SaveSettings(new OrchestratorSettings());
        using (var c = Row().Db.Open())
        using (var cmd = c.CreateCommand())
        {
            cmd.CommandText = "UPDATE settings SET value = '{not json' WHERE name = 'WarnPercent';";
            cmd.ExecuteNonQuery();
        }
        Assert.That(Store().TryLoadSettings(out _), Is.False, "said, as before LAMP");
        Assert.That(vault.Exists(), Is.False);
        Assert.That(Store().SyncVaultSettings(), Is.False);
    }

    [Test]
    public void A_scratch_store_has_no_Vault_and_reads_its_row_exactly_as_before()
    {
        var row = Row();
        Assert.That(row.Vault, Is.Null);
        row.SaveSettings(new OrchestratorSettings { WarnPercent = 61 });
        Assert.That(row.LoadSettings().WarnPercent, Is.EqualTo(61));
        Assert.That(row.SyncVaultSettings(), Is.False);
        Assert.That(row.WatchVault(() => { }), Is.Null);
        Assert.That(Directory.Exists(vault.Directory), Is.False);
    }

    [Test]
    public void The_live_store_uses_the_Vault_app_folder_under_the_local_root()
    {
        var path = JsonSettingsStore<OrchestratorVaultSettings>.ForSettingsApp().FilePath;
        Assert.That(path, Is.EqualTo(Path.Combine(VaultIsolation.LocalRoot, "Orchestrator", "settings.json")), "Local scope, the test's temp root");
        Assert.That(path, Does.Not.StartWith(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MindAttic")),
            "never the real AppData in a test");
    }

    [Test]
    public void A_save_in_Vault_Desktop_is_noticed_and_applied_live()
    {
        var store = Store();
        store.LoadSettings();
        using var applied = new ManualResetEventSlim();
        using var watch = store.WatchVault(() => { if (store.SyncVaultSettings()) applied.Set(); });
        Assert.That(watch, Is.Not.Null);
        vault.Update(v => v.TabBlinkTokens = 4321);
        Assert.That(applied.Wait(TimeSpan.FromSeconds(10)), Is.True, "the watcher synced it");
        Assert.That(Row().LoadSettings().TabBlinkTokens, Is.EqualTo(4321), "in the row the hooks and every dashboard read");
    }

    // ── What consumes them ────────────────────────────────────────────────

    [Test]
    public void The_workspace_root_from_the_Vault_is_the_daemons_workspace()
    {
        var store = Store();
        store.LoadSettings();
        vault.Update(v => v.WorkspaceRoot = @"E:\Elsewhere");
        Assert.That(OrchestratorOptions.Load(store).WorkspaceRoot, Is.EqualTo(@"E:\Elsewhere"));
        vault.Update(v => v.WorkspaceRoot = null);
        Assert.That(OrchestratorOptions.Load(store).WorkspaceRoot, Is.EqualTo(new OrchestratorOptions().WorkspaceRoot), "empty is the default workspace");
    }

    [Test]
    public void The_theme_from_the_Vault_is_the_dashboards_theme()
    {
        var store = Store();
        store.LoadSettings();
        vault.Update(v => v.Theme = "Light");
        Assert.That(MindAttic.Orchestrator.Wpf.ThemeChoice.Saved(store), Is.EqualTo(MindAttic.Components.Wpf.Controls.MaThemeMode.Light));
        vault.Update(v => v.Theme = null);
        Assert.That(MindAttic.Orchestrator.Wpf.ThemeChoice.Saved(store), Is.EqualTo(MindAttic.Components.Wpf.Controls.MaThemeMode.Dark));
        // The sun/moon switch saves through the store, into the Vault.
        Assert.That(MindAttic.Orchestrator.Wpf.ThemeChoice.Save(store, MindAttic.Components.Wpf.Controls.MaThemeMode.Light), Is.True);
        Assert.That(vault.Load().Theme, Is.EqualTo("Light"));
    }

    [Test]
    public void The_gateway_runs_with_the_Vaults_address_port_and_certificate()
    {
        var store = Store();
        store.LoadSettings();
        vault.Update(v =>
        {
            v.Gateway.Bind = "100.64.0.1";
            v.Gateway.Port = 9443;
            v.Gateway.CertThumbprint = "AB12";
            v.Gateway.TokenMinutes = 5;
        });
        var args = store.LoadSettings().Gateway.Arguments();
        Assert.That(args, Is.EqualTo(new[] { "serve", "--bind", "100.64.0.1", "--port", "9443", "--cert-thumbprint", "AB12", "--token-minutes", "5" }));
    }

    [Test]
    public void The_weekly_reserve_from_the_Vault_is_where_every_cli_stops()
    {
        var store = Store();
        store.LoadSettings();
        vault.Update(v => v.WeeklyReservePercent = 20);
        Assert.That(store.LoadSettings().WeeklyStopPercent, Is.EqualTo(80));
    }

    [Test]
    public void The_pace_gates_exempt_commands_reach_the_row_it_reads()
    {
        // pace-gate.py reads ExemptCommands from the settings table through stdlib sqlite3.
        var store = Store();
        store.LoadSettings();
        vault.Update(v => v.ExemptCommands = ["/deploy"]);
        store.SyncVaultSettings();
        using var c = Row().Db.Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT value FROM settings WHERE name = 'ExemptCommands';";
        Assert.That(cmd.ExecuteScalar(), Is.EqualTo("[\"/deploy\"]"));
    }

    // ── Keys ──────────────────────────────────────────────────────────────

    private (NamedKeyStore Keys, string Root) Keys()
    {
        var root = Path.Combine(dir, "keys");
        return (new NamedKeyStore(root, new KeyAssignmentStore(Path.Combine(root, KeyAssignmentStore.Bucket))), root);
    }

    [Test]
    public void With_no_key_assigned_the_orchestrator_bills_the_key_it_always_did()
    {
        var (keys, _) = Keys();
        keys.Add("claude", "shared", "sk-ant-shared-0000000000000000000000");
        var orchestrator = new OrchestratorKeys(keys, fallback: () => "sk-ant-old-path-000000000000000000000");
        Assert.That(orchestrator.HasAssignment("claude"), Is.False);
        Assert.That(orchestrator.ClaudeKey(), Is.EqualTo("sk-ant-old-path-000000000000000000000"), "the keyring's Claude key, as before LAMP");
    }

    [Test]
    public void A_key_assigned_to_the_orchestrator_in_Vault_is_the_one_it_bills_skipping_a_failed_one()
    {
        var (keys, root) = Keys();
        keys.Add("claude", "shared", "sk-ant-shared-0000000000000000000000");
        keys.Add("claude", "2026-10-31 Orchestrator", "sk-ant-assigned-00000000000000000000");
        keys.Add("claude", "spare", "sk-ant-spare-00000000000000000000000");
        keys.Assignments.Set(OrchestratorVaultSettings.AppId, "claude", ["2026-10-31 Orchestrator", "spare"]);
        var health = new KeyHealthStore(Path.Combine(root, "health"));
        var orchestrator = new OrchestratorKeys(keys, health, fallback: () => "never");
        Assert.That(orchestrator.ClaudeKey(), Is.EqualTo("sk-ant-assigned-00000000000000000000"));

        // The first assigned key failed authentication (401): the next assigned one is used.
        new KeySelector(new FileKeySource(keys), health).ReportFailure("claude", "2026-10-31 Orchestrator", KeyFailureKind.Auth);
        Assert.That(orchestrator.ClaudeKey(), Is.EqualTo("sk-ant-spare-00000000000000000000000"));
    }

    [Test]
    public void The_billing_switch_reads_its_key_through_the_orchestrators_named_keys()
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot(), "src", "MindAttic.Orchestrator.Core", "Billing", "BillingSwitch.cs"));
        Assert.That(source, Does.Contain("VaultKey { get; init; } = () => OrchestratorKeys.ForCurrentUser().ClaudeKey();"));
        Assert.That(source, Does.Not.Contain("AppScopedCredentialStore"));
    }

    // ── The cog ───────────────────────────────────────────────────────────

    private sealed class FakeLauncher(bool running) : IVaultDesktopLauncher
    {
        public List<VaultOpenRequest> Sent { get; } = [];
        public List<VaultOpenRequest> Started { get; } = [];

        public bool TrySend(VaultOpenRequest request)
        {
            if (running) Sent.Add(request);
            return running;
        }

        public bool TryStart(VaultOpenRequest request)
        {
            Started.Add(request);
            return true;
        }
    }

    [Test]
    public void The_cog_opens_Vault_Desktop_at_the_Orchestrator_tab()
    {
        var launcher = new FakeLauncher(running: true);
        VaultSettings.Launcher = launcher;
        Assert.That(OrchestratorVaultSettings.OpenInVault(), Is.True);
        Assert.That(launcher.Sent.Single().ToArgs(), Is.EqualTo(new[] { "--app", "Orchestrator" }));

        Assert.That(OrchestratorVaultSettings.OpenInVault(Prefix + "maxConcurrentSessions"), Is.True);
        Assert.That(launcher.Sent[1].ToArgs(), Is.EqualTo(new[] { "--app", "Orchestrator", "--setting", Prefix + "maxConcurrentSessions" }));

        var closed = new FakeLauncher(running: false);
        VaultSettings.Launcher = closed;
        Assert.That(OrchestratorVaultSettings.OpenInVault(), Is.True);
        Assert.That(closed.Started.Single().AppId, Is.EqualTo("Orchestrator"), "started when it isn't running");
    }

    [Test]
    public void The_dashboards_cog_command_opens_the_Vault_and_says_so_when_it_cant()
    {
        Assert.That(MindAttic.Orchestrator.Wpf.ViewModels.MainViewModel.OpenSettingsWith(() => true), Is.Null);
        var toast = MindAttic.Orchestrator.Wpf.ViewModels.MainViewModel.OpenSettingsWith(() => false);
        Assert.That(toast!.Title, Does.Contain("MindAttic Vault"));
        Assert.That(MindAttic.Orchestrator.Wpf.ViewModels.MainViewModel.OpenSettingsWith(() => throw new IOException("pipe")), Is.Not.Null, "never throws");

        var phases = File.ReadAllText(Path.Combine(RepoRoot(), "src", "MindAttic.Orchestrator.Wpf", "ViewModels", "MainViewModel.Phases.cs"));
        Assert.That(phases, Does.Contain("OpenSettingsCommand = new RelayCommand(OpenSettings);")
            .And.Contain("internal Func<bool> OpenVault { get; set; } = () => OrchestratorVaultSettings.OpenInVault();"));
    }

    [Test]
    public void The_old_settings_panel_is_gone()
    {
        var wpf = typeof(MindAttic.Orchestrator.Wpf.ThemeChoice).Assembly;
        Assert.That(wpf.GetType("MindAttic.Orchestrator.Wpf.ViewModels.SettingsViewModel"), Is.Null);
        Assert.That(File.Exists(Path.Combine(RepoRoot(), "src", "MindAttic.Orchestrator.Wpf", "ViewModels", "SettingsViewModel.cs")), Is.False);
        Assert.That(typeof(MindAttic.Orchestrator.Wpf.ViewModels.MainViewModel).GetProperty("Settings"), Is.Null);
        Assert.That(typeof(MindAttic.Orchestrator.Wpf.ViewModels.MainViewModel).GetProperty("ToggleSettingsCommand"), Is.Null);
    }

    [Test]
    public void The_run_script_writes_the_settings_manifest_after_installing()
    {
        var run = File.ReadAllText(Path.Combine(RepoRoot(), "run.ps1"));
        Assert.That(run, Does.Contain("& $cliExe --write-settings-manifest"));
        var cli = File.ReadAllText(Path.Combine(RepoRoot(), "src", "MindAttic.Orchestrator.Cli", "Program.cs"));
        Assert.That(cli, Does.Contain("OrchestratorSettingsManifest.TryRun(args, Console.Out, out var manifestExit)"));
    }

    private static string RepoRoot()
    {
        var root = TestContext.CurrentContext.TestDirectory;
        while (!Path.Exists(Path.Combine(root, ".git"))) root = Path.GetDirectoryName(root) ?? throw new AssertionException("no repo root above the tests");
        return root;
    }
}
