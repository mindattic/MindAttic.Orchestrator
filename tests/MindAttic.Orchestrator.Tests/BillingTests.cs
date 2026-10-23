using System.Text;
using System.Text.Json;
using MindAttic.Orchestrator.Billing;
using MindAttic.Orchestrator.Sessions;
using MindAttic.Orchestrator.Setup;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>
/// The billing switch (M.ORCH-T436): the mode file, Claude Code's choice of the Vault key in a <c>~/.claude.json</c> (always a
/// temp copy, never the real one), the restart requests to Launcher tabs, and the status lines.
/// </summary>
public sealed class BillingTests
{
    private const string Key = "sk-ant-api03-0123456789abcdefghijWXYZ";
    private static string KeyTail => Key[^20..];
    private const string TeamLogin = "\"oauthAccount\": {\"accountUuid\": \"acct-1\", \"organizationUuid\": \"org-1\", \"emailAddress\": \"pdftransferjr@mindattic.com\", " +
                                     "\"organizationName\": \"MindAttic\", \"organizationType\": \"claude_team\"}";

    private string dir = "";
    private string Contexts => Path.Combine(dir, "session-context");
    private string ConfigPath => Path.Combine(dir, ".claude.json");
    private readonly List<(HostSession Host, string Message)> sent = [];
    private bool launcherTakes = true;

    [SetUp]
    public void SetUp()
    {
        dir = Path.Combine(Path.GetTempPath(), "billing-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Contexts);
        sent.Clear();
        launcherTakes = true;
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
    }

    private static readonly HostSession Tab = new("Claude", 9001, "mindattic-host-claude-9001", @"D:\Projects\Prose");

    private BillingSwitch Switch(string? key = Key) => new(Contexts)
    {
        ConfigPath = ConfigPath,
        VaultKey = () => key,
        IsAlive = _ => true,
        // pid 100 runs in the Launcher tab; anything else in a plain terminal.
        HostOf = (pid, hosts) => pid == 100 ? hosts.FirstOrDefault() : null,
        Send = (host, message) => { lock (sent) sent.Add((host, message)); return Task.FromResult(launcherTakes); },
    };

    private void Cli(int pid, string session, string auth, string cwd)
    {
        File.WriteAllText(Path.Combine(Contexts, session + ".state.json"),
            JsonSerializer.Serialize(new { pid, state = "idle", auth, cwd, billing = "[Billing] This CLI bills something." }));
    }

    private JsonElement Responses()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(ConfigPath));
        return doc.RootElement.GetProperty("customApiKeyResponses").Clone();
    }

    private static string[] List(JsonElement responses, string name) => [.. responses.GetProperty(name).EnumerateArray().Select(x => x.GetString()!)];

    // ── The mode file ───────────────────────────────────────────────────────

    [Test]
    public void The_mode_file_round_trips_and_reads_nothing_it_cant_trust()
    {
        var file = new BillingModeFile(Path.Combine(Contexts, BillingModeFile.FileName));
        Assert.That(file.Read(), Is.Null, "no switch made yet");
        var since = new DateTime(2026, 10, 18, 16, 0, 0, DateTimeKind.Utc);
        file.Write(new BillingState(BillingModes.Api, since, "acct-1/org-1", "dashboard") { Requested = ["s1"], Refused = ["s2"], KeyApprovedUtc = since });
        var read = file.Read()!;
        Assert.That((read.Mode, read.SinceUtc, read.Login, read.By), Is.EqualTo((BillingModes.Api, since, "acct-1/org-1", "dashboard")));
        Assert.That((read.Requested, read.Refused, read.KeyApprovedUtc), Is.EqualTo((new[] { "s1" }, new[] { "s2" }, (DateTime?)since)));
        using (var doc = JsonDocument.Parse(File.ReadAllText(file.Path)))
            Assert.That(doc.RootElement.GetProperty("since").GetDouble(), Is.EqualTo((since - DateTime.UnixEpoch).TotalSeconds), "epoch seconds, as the hooks read");
        Assert.That(Directory.GetFiles(Contexts, "*.tmp"), Is.Empty);

        foreach (var bad in new[] { "{not json", "[]", "{\"mode\":\"cloud\"}", "{}" })
        {
            File.WriteAllText(file.Path, bad);
            Assert.That(file.Read(), Is.Null, bad);
        }
    }

    [TestCase("team", BillingModes.Team)]
    [TestCase("Claude Team", BillingModes.Team)]
    [TestCase("API", BillingModes.Api)]
    [TestCase("api-key", BillingModes.Api)]
    [TestCase("cloud", null)]
    [TestCase("", null)]
    public void A_typed_mode_is_read_as_team_or_api(string typed, string? mode) => Assert.That(BillingModes.Parse(typed), Is.EqualTo(mode));

    [Test]
    public void The_mode_file_outlives_the_weekly_prune_of_the_session_folder()
    {
        var file = Path.Combine(Contexts, BillingModeFile.FileName);
        var stale = Path.Combine(Contexts, "old.state.json");
        File.WriteAllText(file, "{\"mode\":\"api\"}");
        File.WriteAllText(stale, "{}");
        foreach (var f in new[] { file, stale }) File.SetLastWriteTimeUtc(f, DateTime.UtcNow.AddDays(-30));
        new ContextCycler(Contexts).Prune(DateTime.UtcNow);
        Assert.That((File.Exists(file), File.Exists(stale)), Is.EqualTo((true, false)));
    }

    /// <summary>M.ORCH-T457: a CLI started on a key before it was set aside bills it until it's reopened, however long that is, so
    /// when it was set aside outlives the weekly prune.</summary>
    [Test]
    public void When_a_key_was_set_aside_outlives_the_weekly_prune()
    {
        var file = Path.Combine(Contexts, Setup.AgentGate.ApiKeySetAsideName);
        File.WriteAllText(file, "{\"0123456789abcdef\": 1700000000}");
        File.SetLastWriteTimeUtc(file, DateTime.UtcNow.AddDays(-30));
        new ContextCycler(Contexts).Prune(DateTime.UtcNow);
        Assert.That(File.Exists(file), Is.True);
    }

    // ── ~/.claude.json (a temp copy) ────────────────────────────────────────

    [Test]
    public void Flipping_the_key_changes_only_that_field_and_keeps_the_rest_byte_for_byte()
    {
        // Keys that differ only in case (what PowerShell's ConvertFrom-Json can't read), numbers JsonNode would rewrite, and
        // characters an escaping writer would turn into \u sequences: all of it must come back exactly.
        string Config(string responses) =>
            "{\r\n  \"numStartups\": 2138,\r\n  \"Projects\": {\"a\": 1},\r\n  \"projects\": {\r\n    \"D:\\\\Repo\": {\"lastCost\": 1.10, \"big\": 12345678901234567890, \"tiny\": 1e-7, \"name\": \"caf\u00e9 \u2603\"}\r\n  },\r\n" +
            "  \"customApiKeyResponses\": " + responses + ",\r\n  " + TeamLogin + "\r\n}";
        var original = Config($"{{\r\n    \"approved\": [\r\n      \"other-tail-xxxxxxxxxx\",\r\n      \"{KeyTail}\"\r\n    ],\r\n    \"rejected\": []\r\n  }}");
        var expected = Config($"{{\r\n    \"approved\": [\r\n      \"other-tail-xxxxxxxxxx\"\r\n    ],\r\n    \"rejected\": [\r\n      \"{KeyTail}\"\r\n    ]\r\n  }}");
        File.WriteAllBytes(ConfigPath, [0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes(original)]);

        Assert.That(ClaudeKeyChoice.Set(ConfigPath, KeyTail, approve: false), Is.True);
        var bytes = File.ReadAllBytes(ConfigPath);
        Assert.That(bytes[..3], Is.EqualTo(new byte[] { 0xEF, 0xBB, 0xBF }), "its byte order mark kept");
        Assert.That(Encoding.UTF8.GetString(bytes[3..]), Is.EqualTo(expected));
        Assert.That(Encoding.UTF8.GetString(File.ReadAllBytes(ConfigPath + ClaudeKeyChoice.BackupSuffix)[3..]), Is.EqualTo(original), "a backup of what it was");
        Assert.That(Directory.GetFiles(dir, "*.tmp"), Is.Empty, "put in place whole, nothing left behind");

        Assert.That(ClaudeKeyChoice.Set(ConfigPath, KeyTail, approve: false), Is.False, "already set aside: nothing written");
        Assert.That(ClaudeKeyChoice.Set(ConfigPath, KeyTail, approve: true), Is.True);
        Assert.That(ClaudeKeyChoice.Read(ConfigPath, KeyTail).Choice, Is.EqualTo(KeyChoice.Approved));
        Assert.That((List(Responses(), "approved"), List(Responses(), "rejected")), Is.EqualTo((new[] { "other-tail-xxxxxxxxxx", KeyTail }, Array.Empty<string>())));
    }

    [Test]
    public void A_config_with_no_choices_yet_gets_them_as_its_last_member()
    {
        File.WriteAllText(ConfigPath, "{\n  \"numStartups\": 3,\n  " + TeamLogin + "\n}");
        Assert.That(ClaudeKeyChoice.Set(ConfigPath, KeyTail, approve: true), Is.True);
        var text = File.ReadAllText(ConfigPath);
        Assert.That(text, Does.StartWith("{\n  \"numStartups\": 3,\n  " + TeamLogin + ",\n  \"customApiKeyResponses\": {\n    \"approved\": [\n      \"" + KeyTail + "\"\n    ],"));
        Assert.That(ClaudeKeyChoice.Read(ConfigPath, KeyTail), Is.EqualTo((new ClaudeLogin("acct-1", "org-1", "pdftransferjr@mindattic.com", "MindAttic", "Claude Team"), KeyChoice.Approved)));
    }

    [Test]
    public void A_missing_or_unreadable_config_is_never_written()
    {
        Assert.That(ClaudeKeyChoice.Set(ConfigPath, KeyTail, approve: true), Is.False);
        Assert.That(File.Exists(ConfigPath), Is.False);
        foreach (var bad in new[] { "{not json", "[1]", "{\"customApiKeyResponses\": \"yes\"}" })
        {
            File.WriteAllText(ConfigPath, bad);
            Assert.Throws<InvalidDataException>(() => ClaudeKeyChoice.Set(ConfigPath, KeyTail, approve: true), bad);
            Assert.That(File.ReadAllText(ConfigPath), Is.EqualTo(bad));
        }
    }

    [Test]
    public void A_key_is_only_ever_shown_masked()
    {
        Assert.That(ClaudeKeyChoice.Mask(Key), Is.EqualTo("sk-ant-...WXYZ"));
        Assert.That(ClaudeKeyChoice.Mask("abc"), Is.EqualTo("..."));
    }

    // ── The switch ──────────────────────────────────────────────────────────

    [Test]
    public void Switching_to_the_api_key_approves_it_writes_the_mode_and_asks_the_launcher_to_restart_the_cli_on_the_login()
    {
        File.WriteAllText(ConfigPath, "{" + TeamLogin + ", \"customApiKeyResponses\": {\"approved\": [], \"rejected\": [\"" + KeyTail + "\"]}}");
        Cli(100, "hosted", "subscription", @"D:\Projects\Prose");
        Cli(200, "plain", "subscription", @"D:\Projects\Claudia");
        Cli(300, "keyed", "api-key", @"D:\Projects\Vault");
        var now = new DateTime(2026, 10, 18, 16, 0, 0, DateTimeKind.Utc);

        var result = Switch().Switch("api", "cli", [Tab], now);

        Assert.That(result.Error, Is.Null);
        Assert.That(ClaudeKeyChoice.Read(ConfigPath, KeyTail).Choice, Is.EqualTo(KeyChoice.Approved));
        var state = new BillingModeFile(Path.Combine(Contexts, BillingModeFile.FileName)).Read()!;
        Assert.That((state.Mode, state.SinceUtc, state.Login, state.By, state.KeyApprovedUtc, state.Requested),
            Is.EqualTo((BillingModes.Api, now, "acct-1/org-1", "cli", (DateTime?)now, new[] { "hosted" })));

        Assert.That(sent, Has.Count.EqualTo(1), "only the hosted CLI on the login is asked; the one on the key already bills it");
        Assert.That(sent[0].Host, Is.EqualTo(Tab));
        using (var message = JsonDocument.Parse(sent[0].Message))
        {
            var m = message.RootElement;
            Assert.That((m.GetProperty("type").GetString(), m.GetProperty("reason").GetString(), m.GetProperty("billing").GetString(),
                         m.GetProperty("session").GetString(), m.GetProperty("cliPid").GetInt32(), m.GetProperty("requestedAt").GetString()),
                Is.EqualTo(("restart-resume", "billing", "api", "hosted", 100, "2026-10-18T16:00:00Z")));
        }
        Assert.That(sent[0].Message, Does.Not.Contain("\n"), "one line");
        Assert.That(SessionControl.ControlPipeName(Tab), Is.EqualTo("mindattic-control-claude-9001"));
        Assert.That(HostSessionScanner.ParsePipeName(SessionControl.ControlPipeName(Tab)), Is.Null, "never taken for a tab of its own");

        Assert.That(result.Status.Headline, Is.EqualTo("API KEY: sk-ant-...WXYZ"));
        Assert.That(string.Join("\n", result.Lines) + result.Status.Headline + JsonSerializer.Serialize(result.Status), Does.Not.Contain(KeyTail).And.Not.Contain(Key));
        Assert.That(result.Status.Clis.Select(c => (c.SessionId, c.Restart)),
            Is.EquivalentTo(new (string, string?)[] { ("hosted", "requested"), ("plain", "manual"), ("keyed", null) }));
        Assert.That(result.Lines, Has.Some.StartsWith("asked the Launcher to restart the CLI in Prose (pid 100) with claude --resume at its next idle point"));
        Assert.That(result.Lines, Has.Some.Contains("the CLI in Claudia (pid 200) bills Claude Team and isn't in a Launcher tab: close it and reopen it (claude --resume plain)"));

        // The daemon's next tick asks nobody twice, and says the plain terminal's CLI only once.
        Assert.That(Switch().Tick([Tab], now.AddMinutes(1)), Has.None.Contains("Prose"));
        Assert.That(sent, Has.Count.EqualTo(1));
    }

    [Test]
    public void A_cli_failed_over_to_the_api_key_is_never_asked_back_onto_the_mode()
    {
        // M.ORCH-T664: the rebalancer moved one CLI onto the key with the mode left on Claude Team; the switch leaves it there,
        // and doesn't reject the key under it on the global login.
        File.WriteAllText(ConfigPath, "{" + TeamLogin + ", \"customApiKeyResponses\": {\"approved\": [\"" + KeyTail + "\"], \"rejected\": []}}");
        Cli(100, "hosted", "api-key", @"D:\Projects\Prose");
        var now = new DateTime(2026, 10, 18, 16, 0, 0, DateTimeKind.Utc);
        var billing = Switch();
        billing.ModeFile.Write(new BillingState(BillingModes.Team, now, "acct-1/org-1", "dashboard"));
        billing.FailedOver = () => [new ApiFailover("hosted", 100, 9001, @"D:\Projects\Prose", null, "", "Ryan", "its week is at 98%", now)];

        Assert.That(billing.Tick([Tab], now.AddMinutes(1)), Is.Empty);
        Assert.That(sent, Is.Empty);
        Assert.That(List(Responses(), "approved"), Is.EqualTo(new[] { KeyTail }), "still approved for the failed-over CLI");
        Assert.That(billing.Status([Tab], now.AddMinutes(1)).Mismatched, Is.Empty);
    }

    [Test]
    public void The_key_can_be_approved_in_one_logins_config_alone()
    {
        File.WriteAllText(ConfigPath, "{" + TeamLogin + "}");
        Assert.That(Switch().SetKeyChoice(ConfigPath, approve: true), Is.Null);
        Assert.That(List(Responses(), "approved"), Is.EqualTo(new[] { KeyTail }));
        Assert.That(Switch().SetKeyChoice(ConfigPath, approve: false), Is.Null);
        Assert.That(List(Responses(), "rejected"), Is.EqualTo(new[] { KeyTail }));
        Assert.That(Switch(key: null).SetKeyChoice(ConfigPath, approve: true), Does.Contain("no Claude key"));
        Assert.That(File.Exists(Path.Combine(Contexts, BillingModeFile.FileName)), Is.False, "the mode is left alone");
    }

    [Test]
    public void A_switch_made_while_restart_requests_are_sent_is_never_overwritten_with_the_older_mode()
    {
        // M.ORCH-T454: sending takes seconds; the context gate switching back to Claude Team meanwhile (a /login) was overwritten
        // by the requests' bookkeeping, written from the copy read before sending.
        File.WriteAllText(ConfigPath, "{" + TeamLogin + ", \"customApiKeyResponses\": {\"approved\": [\"" + KeyTail + "\"], \"rejected\": []}}");
        Cli(100, "hosted", "subscription", @"D:\Projects\Prose");
        var file = new BillingModeFile(Path.Combine(Contexts, BillingModeFile.FileName));
        var since = new DateTime(2026, 10, 18, 16, 0, 0, DateTimeKind.Utc);
        file.Write(new BillingState(BillingModes.Api, since, "acct-1/org-1", "cli"));
        var back = new BillingState(BillingModes.Team, since.AddSeconds(2), "acct-2/org-2", "login") { Refused = ["elsewhere"] };
        var racing = new BillingSwitch(Contexts)
        {
            ConfigPath = ConfigPath, VaultKey = () => Key, IsAlive = _ => true,
            HostOf = (pid, hosts) => pid == 100 ? hosts.FirstOrDefault() : null,
            Send = (_, _) => { file.Write(back); return Task.FromResult(true); },
        };

        racing.Tick([Tab], since.AddMinutes(1));

        var read = file.Read()!;
        Assert.That((read.Mode, read.SinceUtc, read.By, read.Requested, read.Refused),
            Is.EqualTo((BillingModes.Team, back.SinceUtc, "login", Array.Empty<string>(), new[] { "elsewhere" })), "the newer switch stands as written");

        // On the same switch, what was recorded meanwhile is kept beside this pass's requests.
        file.Write(new BillingState(BillingModes.Api, since, "acct-1/org-1", "cli"));
        var same = new BillingSwitch(Contexts)
        {
            ConfigPath = ConfigPath, VaultKey = () => Key, IsAlive = _ => true,
            HostOf = (pid, hosts) => pid == 100 ? hosts.FirstOrDefault() : null,
            Send = (_, _) => { file.Write(file.Read()! with { Refused = ["other"] }); return Task.FromResult(true); },
        };
        same.Tick([Tab], since.AddMinutes(1));
        read = file.Read()!;
        Assert.That((read.Mode, read.Requested, read.Refused), Is.EqualTo((BillingModes.Api, new[] { "hosted" }, new[] { "other" })));
    }

    [Test]
    public void Switching_to_claude_team_sets_the_key_aside_dated_for_the_clis_still_on_it()
    {
        File.WriteAllText(ConfigPath, "{" + TeamLogin + ", \"customApiKeyResponses\": {\"approved\": [\"" + KeyTail + "\"], \"rejected\": []}}");
        Cli(100, "hosted", "api-key", @"D:\Projects\Prose");
        var now = DateTime.UtcNow;

        var result = Switch().Switch("team", "dashboard", [Tab], now);

        Assert.That(result.Error, Is.Null);
        Assert.That((List(Responses(), "approved"), List(Responses(), "rejected")), Is.EqualTo((Array.Empty<string>(), new[] { KeyTail })));
        using (var marks = JsonDocument.Parse(File.ReadAllText(Path.Combine(Contexts, AgentGate.ApiKeySetAsideName))))
            Assert.That(marks.RootElement.GetProperty(BillingSwitch.KeyMark(Key)).GetDouble(), Is.EqualTo((now - DateTime.UnixEpoch).TotalSeconds).Within(1),
                "the context gate's own record, so a CLI started on the key still reads as on it");
        Assert.That(BillingSwitch.KeyMark(Key), Has.Length.EqualTo(16).And.Not.Contain(KeyTail));
        Assert.That(result.Status.Headline, Is.EqualTo("Claude Team: pdftransferjr@mindattic.com (MindAttic)"));
        Assert.That(JsonDocument.Parse(sent.Single().Message).RootElement.GetProperty("billing").GetString(), Is.EqualTo("team"));
    }

    [TestCase("\"customApiKeyResponses\": {\"approved\": [], \"rejected\": [\"TAIL\"]}")]
    [TestCase("\"numStartups\": 1")]
    public void A_key_that_wasnt_approved_isnt_dated_set_aside(string responses)
    {
        // M.ORCH-T450: a CLI started with the key in its environment billed the login all along; dating the key set aside
        // would make it read as on the key (env-set-aside), unpaced, and asked to restart.
        File.WriteAllText(ConfigPath, "{" + TeamLogin + ", " + responses.Replace("TAIL", KeyTail) + "}");
        var result = Switch().Switch("team", "dashboard", [], DateTime.UtcNow);
        Assert.That(result.Error, Is.Null);
        Assert.That(ClaudeKeyChoice.Read(ConfigPath, KeyTail).Choice, Is.EqualTo(KeyChoice.Rejected));
        Assert.That(File.Exists(Path.Combine(Contexts, AgentGate.ApiKeySetAsideName)), Is.False);
    }

    [Test]
    public void The_api_key_cant_be_chosen_without_one_in_the_vault_and_nothing_is_changed()
    {
        var config = "{" + TeamLogin + "}";
        File.WriteAllText(ConfigPath, config);
        var result = Switch(key: null).Switch("api", "cli", [Tab], DateTime.UtcNow);
        Assert.That(result.Error, Does.Contain("no Claude key in the MindAttic Vault"));
        Assert.That(File.ReadAllText(ConfigPath), Is.EqualTo(config));
        Assert.That(File.Exists(Path.Combine(Contexts, BillingModeFile.FileName)), Is.False);
        Assert.That(Switch().Switch("cloud", "cli", [], DateTime.UtcNow).Error, Does.Contain("isn't a billing mode"));
    }

    [Test]
    public void A_login_to_another_claude_ai_account_switches_back_to_claude_team()
    {
        File.WriteAllText(ConfigPath, "{" + TeamLogin + ", \"customApiKeyResponses\": {\"approved\": [], \"rejected\": []}}");
        var billing = Switch();
        billing.Switch("api", "cli", [], DateTime.UtcNow);
        Assert.That(billing.Tick([], DateTime.UtcNow), Is.Empty, "the same login: the API key stays");

        File.WriteAllText(ConfigPath, File.ReadAllText(ConfigPath).Replace("org-1", "org-2"));
        var said = billing.Tick([], DateTime.UtcNow);
        Assert.That(said[0], Is.EqualTo("a /login to Claude Team: pdftransferjr@mindattic.com (MindAttic) switches billing back to Claude Team"));
        var state = billing.ModeFile.Read()!;
        Assert.That((state.Mode, state.By, state.Login), Is.EqualTo((BillingModes.Team, "login", "acct-1/org-2")));
        Assert.That(ClaudeKeyChoice.Read(ConfigPath, KeyTail).Choice, Is.EqualTo(KeyChoice.Rejected));
    }

    /// <summary>M.ORCH-T459: a switch back the daemon couldn't make leaves the API key billing, so it's said to you, not only logged.</summary>
    [Test]
    public void A_switch_back_the_daemon_couldnt_make_needs_you()
    {
        File.WriteAllText(ConfigPath, "{" + TeamLogin + ", \"customApiKeyResponses\": {\"approved\": [], \"rejected\": []}}");
        var billing = Switch();
        billing.Switch("api", "cli", [], DateTime.UtcNow);
        // A /login to another account, with Claude Code's key choices in a shape the switch won't touch.
        File.WriteAllText(ConfigPath, "{" + TeamLogin.Replace("org-1", "org-2") + ", \"customApiKeyResponses\": 5}");
        var said = billing.Tick([], DateTime.UtcNow);
        Assert.That(said, Has.Count.EqualTo(2));
        Assert.That(said[1], Does.StartWith("Couldn't set Claude Code's choice"));
        Assert.That(BillingSwitch.NeedsYou(said[0]), Is.False, "the switch itself is news, not a problem");
        Assert.That(BillingSwitch.NeedsYou(said[1]), Is.True);
        Assert.That(billing.ModeFile.Read()!.Mode, Is.EqualTo(BillingModes.Api), "billing is left as it was");

        Assert.That(BillingSwitch.NeedsYou("There's no Claude key in the MindAttic Vault keyring, so nothing can bill the API key; billing is left as it was."), Is.True);
        Assert.That(BillingSwitch.NeedsYou("Couldn't write billing.json: denied"), Is.True);
        Assert.That(BillingSwitch.NeedsYou("couldn't ask the Launcher tab running the CLI in Prose (pid 1) to restart it"), Is.True);
        Assert.That(BillingSwitch.NeedsYou("the CLI in Prose (pid 1) bills Claude Team and isn't in a Launcher tab"), Is.True);
        Assert.That(BillingSwitch.NeedsYou("asked the Launcher to restart the CLI in Prose (pid 1)"), Is.False);
    }

    [Test]
    public void A_key_choice_out_of_line_with_the_mode_is_put_back()
    {
        File.WriteAllText(ConfigPath, "{" + TeamLogin + "}");
        var billing = Switch();
        billing.Switch("team", "cli", [], DateTime.UtcNow);
        ClaudeKeyChoice.Set(ConfigPath, KeyTail, approve: true); // a "yes" answered in a CLI's prompt
        billing.Tick([], DateTime.UtcNow);
        Assert.That(ClaudeKeyChoice.Read(ConfigPath, KeyTail).Choice, Is.EqualTo(KeyChoice.Rejected));
        Assert.That(billing.ModeFile.Read()!.By, Is.EqualTo("cli"), "the same switch, put back in line");
    }

    [Test]
    public void A_tab_that_cant_be_asked_is_flagged_and_asked_again_later()
    {
        File.WriteAllText(ConfigPath, "{" + TeamLogin + "}");
        Cli(100, "hosted", "subscription", @"D:\Projects\Prose");
        launcherTakes = false;
        var billing = Switch();
        var now = DateTime.UtcNow;
        var result = billing.Switch("api", "cli", [Tab], now);
        Assert.That(result.Lines, Has.Some.StartsWith("couldn't ask the Launcher tab running the CLI in Prose (pid 100)"));
        Assert.That(result.Status.Clis.Single().Restart, Is.EqualTo("refused"));
        Assert.That(BillingText.Of(result.Status.Clis.Single()), Is.EqualTo("Claude Team: its Launcher tab couldn't be asked, so close it and reopen it to bill API key"));

        billing.Tick([Tab], now.AddMinutes(1));
        Assert.That(sent, Has.Count.EqualTo(1), "not asked again at once");
        launcherTakes = true;
        Assert.That(billing.Tick([Tab], now + BillingSwitch.RetryAfter), Has.Some.StartsWith("asked the Launcher"));
        Assert.That(billing.Status([Tab], now).Clis.Single().Restart, Is.EqualTo("requested"));
    }

    [Test]
    public void With_no_switch_made_nothing_is_enforced_and_the_mode_reads_from_the_config()
    {
        File.WriteAllText(ConfigPath, "{" + TeamLogin + ", \"customApiKeyResponses\": {\"approved\": [\"" + KeyTail + "\"]}}");
        Cli(100, "hosted", "subscription", @"D:\Projects\Prose");
        var billing = Switch();
        Assert.That(billing.Tick([Tab], DateTime.UtcNow), Is.Empty);
        Assert.That(sent, Is.Empty);
        var status = billing.Status([Tab], DateTime.UtcNow);
        Assert.That(status.Mode, Is.EqualTo(BillingModes.Api), "the Vault key approved");
        // M.ORCH-T453: it read "the Launcher is being asked to restart it", though nothing asks until a switch is made.
        Assert.That((status.Clis.Single().Restart, status.Mismatched), Is.EqualTo(((string?)null, Array.Empty<CliBilling>())));
        Assert.That(BillingText.Of(status.Clis.Single()), Is.EqualTo("Claude Team"));
    }

    // ── Status lines ────────────────────────────────────────────────────────

    [Test]
    public void The_status_says_who_is_billed_and_each_clis_own_billing()
    {
        var now = new DateTime(2026, 10, 18, 16, 0, 0, DateTimeKind.Utc);
        var status = new BillingStatus(BillingModes.Team, now.AddMinutes(-30), "Claude Team: pdftransferjr@mindattic.com (MindAttic)",
        [
            new CliBilling(100, "a", @"D:\Projects\Prose", "subscription", Tab),
            new CliBilling(200, "b", @"D:\Projects\Claudia", "api-key", null) { Restart = "manual" },
            new CliBilling(300, "c", @"D:\Projects\Vault", "api-key", Tab) { Restart = "requested" },
            new CliBilling(400, "d", null, "cloud", null),
        ]);
        var lines = BillingText.Lines(status, now);
        Assert.That(lines[0], Does.StartWith("Billing: Claude Team: pdftransferjr@mindattic.com (MindAttic) (since "));
        Assert.That(lines.Skip(1), Is.EqualTo(new[]
        {
            @"  Prose (pid 100, Launcher tab): Claude Team",
            @"  Claudia (pid 200, outside the Launcher): API key: outside the Launcher, so restart it by hand to bill Claude Team",
            @"  Vault (pid 300, Launcher tab): API key: the Launcher restarts it at its next idle point to bill Claude Team",
            @"  an unknown folder (pid 400, outside the Launcher): bills cloud, not Claude Team or the API key",
            @"  2 of 4 open CLIs bill API key; 1 outside the Launcher need a manual restart",
        }));
        // A dashboard reads the daemon's published snapshot: the status comes back whole.
        var copy = JsonSerializer.Deserialize<BillingStatus>(JsonSerializer.Serialize(status))!;
        Assert.That(BillingText.Lines(copy, now), Is.EqualTo(lines));
        Assert.That(Wpf.ViewModels.MainViewModel.DetailOf(status),
            Is.EqualTo("2 open CLIs still bill API key; 1 restarts through the Launcher at the next idle point; restart by hand: Claudia (pid 200)."));
    }
}
