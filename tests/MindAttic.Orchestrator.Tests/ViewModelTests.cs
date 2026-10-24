using System.Collections.ObjectModel;
using System.Collections.Specialized;
using MindAttic.Components.Wpf.Mvvm;
using MindAttic.Orchestrator.Settings;
using MindAttic.Orchestrator.Wpf.ViewModels;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>The dashboard's view-model logic: the settings panel's parsing and checks, and the row syncing every list uses.</summary>
public sealed class ViewModelTests
{
    private string dir = "";

    [SetUp]
    public void SetUp() => dir = Path.Combine(Path.GetTempPath(), "orch-vm-" + Guid.NewGuid().ToString("N"));

    [TearDown]
    public void TearDown() { if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true); }

    [TestCase("", null)]
    [TestCase("  ", null)]
    [TestCase("20000000", 20_000_000L)]
    [TestCase("20M", 20_000_000L)]
    [TestCase("1.5b", 1_500_000_000L)]
    [TestCase("500k", 500_000L)]
    [TestCase("2,500,000", 2_500_000L)]
    public void Token_fields_read_blank_as_unset_and_take_suffixes(string text, long? expected)
    {
        Assert.That(SettingsViewModel.TryTokens(text, out var value), Is.True, text);
        Assert.That(value, Is.EqualTo(expected));
    }

    [TestCase("0")]
    [TestCase("-5M")]
    [TestCase("lots")]
    [TestCase("NaN")]
    [TestCase("Infinity")]
    [TestCase("0.5")]
    [TestCase("0.0004k")]
    public void Token_fields_refuse_zero_negative_and_junk(string text) =>
        Assert.That(SettingsViewModel.TryTokens(text, out _), Is.False, text);

    [Test]
    public void Save_refuses_anything_out_of_range_says_why_and_saves_nothing()
    {
        var store = new JsonStore(dir);
        var saves = 0;
        var vm = new SettingsViewModel(store, () => saves++);
        var cases = new (Action<SettingsViewModel> Set, string Error)[]
        {
            (v => v.BlockBudget = "0", "5-hour budget"),
            (v => v.WeeklyBudget = "junk", "Weekly budget"),
            (v => v.WeeklyReset = "someday", "Week resets"),
            (v => v.WarnPercent = "0", "Warn at: 1 to 100"),
            (v => v.WarnPercent = "101", "Warn at: 1 to 100"),
            (v => v.MaxSessions = "51", "Max sessions: 1 to 50"),
            (v => v.MaxSessions = "x", "Max sessions: 1 to 50"),
            (v => v.PaceTarget = "0", "Pace target: 1 to 100"),
            (v => v.ContextPercent = "9", "Cycle context at: 10 to 95"),
            (v => v.ContextPercent = "96", "Cycle context at: 10 to 95"),
            (v => v.OrphanRedispatches = "21", "Orphan redispatches: 0 to 20"),
            (v => v.OrphanQuiet = "0", "Orphan quiet minutes: 1 to 1440"),
            (v => v.TabBlinkTokens = "0", "Blink once per: 1 to 10,000,000 tokens"),
            (v => v.TabBlinkTokens = "lots", "Blink once per"),
        };
        foreach (var (set, error) in cases)
        {
            vm.Load();
            set(vm);
            vm.SaveCommand.Execute(null);
            Assert.That(vm.Error, Does.StartWith(error), error);
        }
        Assert.That((saves, File.Exists(Path.Combine(dir, "settings.json"))), Is.EqualTo((0, false)), "nothing saved");

        vm.Load();
        vm.BlockBudget = "20M";
        vm.WarnPercent = "75";
        vm.ContextPercent = "85";
        vm.SaveCommand.Execute(null);
        var saved = store.LoadSettings();
        Assert.That((vm.Error, saves, saved.BlockBudget, saved.WarnPercent, saved.ContextCyclePercent), Is.EqualTo(("", 1, (long?)20_000_000, 75, 85)));
        Assert.That(vm.BlockBudget, Is.EqualTo("20M"), "reloaded from what was saved");
    }

    [Test]
    public void The_orphan_settings_round_trip_through_the_panel()
    {
        var store = new JsonStore(dir);
        var vm = new SettingsViewModel(store, () => { });
        Assert.That((vm.OrphanRedispatches, vm.OrphanQuiet), Is.EqualTo(("3", "10")));
        vm.OrphanRedispatches = "0";
        vm.OrphanQuiet = "30";
        vm.SaveCommand.Execute(null);
        var s = store.LoadSettings();
        Assert.That((vm.Error, s.OrphanRedispatches, s.OrphanQuietMinutes), Is.EqualTo(("", 0, 30)));
    }

    [Test]
    public void The_tab_blink_setting_round_trips_through_the_panel()
    {
        var store = new JsonStore(dir);
        var vm = new SettingsViewModel(store, () => { });
        Assert.That(vm.TabBlinkTokens, Is.EqualTo("1000"), "M.ORCH-T757's default");
        vm.TabBlinkTokens = "100";
        vm.SaveCommand.Execute(null);
        Assert.That((vm.Error, store.LoadSettings().TabBlinkTokens), Is.EqualTo(("", 100)));
    }

    [Test]
    public async Task A_second_send_while_the_first_is_under_way_types_nothing_more()
    {
        var pipe = "orch-vm-" + Guid.NewGuid().ToString("N");
        var received = new System.Collections.Concurrent.ConcurrentQueue<string>();
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(6));
        var server = Task.Run(async () =>
        {
            try
            {
                while (true)
                {
                    using var s = new System.IO.Pipes.NamedPipeServerStream(pipe, System.IO.Pipes.PipeDirection.In, 4,
                        System.IO.Pipes.PipeTransmissionMode.Byte, System.IO.Pipes.PipeOptions.Asynchronous);
                    await s.WaitForConnectionAsync(stop.Token);
                    using var r = new StreamReader(s);
                    received.Enqueue(await r.ReadToEndAsync(stop.Token));
                }
            }
            catch (OperationCanceledException) { }
        });
        var row = new HostRowViewModel(new HostSession("Claude", 1, pipe, null)) { Draft = "hello" };
        var first = row.SendAsync();
        var second = row.SendAsync(); // a double click
        await Task.WhenAll(first, second);
        Assert.That((row.Draft, row.Sending), Is.EqualTo(("", false)));
        await server;
        Assert.That(received.Count(t => t == "hello"), Is.EqualTo(1), "the text is typed once");
    }

    [TestCase("a@b.com", null, "a@b.com")]
    [TestCase(null, "g@b.com", "g@b.com")]
    [TestCase("a@b.com", "g@b.com", "a@b.com")]
    [TestCase(null, null, null)]
    public void A_cli_names_its_own_login_else_the_global_one(string? own, string? global, string? expected) =>
        Assert.That(StatusAggregator.LoginText(own, global), Is.EqualTo(expected));

    [TestCase("a@b.com", "g@b.com", "a@b.com")]
    [TestCase(null, "g@b.com", "API key (fallback)")]
    [TestCase(null, null, "API key (fallback)")]
    public void While_the_api_key_bills_a_cli_with_no_login_of_its_own_runs_on_the_key(string? own, string? global, string? expected) =>
        Assert.That(StatusAggregator.LoginText(own, global, billingIsApiKey: true), Is.EqualTo(expected));

    [Test]
    public void Agent_cards_and_launcher_tabs_show_the_login_each_cli_bills()
    {
        var now = DateTime.UtcNow;
        var row = new MindAttic.Orchestrator.Activity.AgentBoardRow("s1", "A", null, null, now, now,
            MindAttic.Orchestrator.Activity.AgentState.Working, "", false, default, []) { Login = "a@b.com" };
        var card = new AgentRowViewModel("s1");
        card.Update(row, null, now);
        Assert.That((card.Login, card.AccessibleName.Contains("Login: a@b.com.")), Is.EqualTo(("Login: a@b.com", true)));
        card.Update(row with { Login = null }, null, now);
        Assert.That(card.Login, Is.Empty, "nothing shown when the login isn't known");

        card.Update(row with { Login = "Jordan", LoginEmail = "j@b.com" }, null, now);
        Assert.That((card.Login, card.LoginTip), Is.EqualTo(("Login: Jordan", "The Claude login this CLI bills: j@b.com")), "the name, the email in the tooltip (M.ORCH-T614)");

        var host = new HostRowViewModel(new HostSession("Claude", 7, "p", null) { Login = "g (fallback)", LoginEmail = "g@b.com" });
        Assert.That((host.Detail, host.DetailTip), Is.EqualTo(("PID 7 · g (fallback)", "Bills the Claude login g@b.com")));
        host.Update(new HostSession("Claude", 7, "p", null));
        Assert.That(host.Detail, Is.EqualTo("PID 7"));
    }

    [Test]
    public void A_save_that_cant_write_settings_says_so_instead_of_throwing()
    {
        var store = new JsonStore(dir);
        store.SaveSettings(store.LoadSettings());
        var saves = 0;
        var vm = new SettingsViewModel(store, () => saves++);
        vm.WarnPercent = "70";
        using (StoreFaults.Unwritable(store, JsonStore.SettingsName))
            Assert.DoesNotThrow(() => vm.SaveCommand.Execute(null));
        Assert.That((vm.Error, vm.WarnPercent, saves), Is.EqualTo((vm.Error, "70", 0)));
        Assert.That(vm.Error, Does.StartWith("Couldn't save"));
    }

    [Test]
    public void Saving_the_panel_keeps_an_allocate_pause_made_elsewhere_meanwhile()
    {
        var store = new JsonStore(dir);
        store.SaveSettings(store.LoadSettings() with { Allocate = true });
        var vm = new SettingsViewModel(store, () => { });
        store.SaveSettings(store.LoadSettings() with { Allocate = false }); // Settings' Pause allocate
        vm.WarnPercent = "70";
        vm.SaveCommand.Execute(null);
        Assert.That((vm.Error, store.LoadSettings().Allocate), Is.EqualTo(("", false)), "the toggle wasn't touched in the panel");

        vm.Allocate = true;
        vm.SaveCommand.Execute(null);
        Assert.That(store.LoadSettings().Allocate, Is.True, "turned on in the panel");
    }

    [Test]
    public void The_alert_recipient_round_trips_through_the_panel_and_is_checked()
    {
        // M.ORCH-T742: Ryan texted to start with; editable here.
        var store = new JsonStore(dir);
        store.SaveSettings(store.LoadSettings());
        Directory.CreateDirectory(dir);
        var contacts = Path.Combine(dir, "contacts.json");
        File.WriteAllText(contacts, """{ "contacts": [ { "name": "ryan", "phone": "+15555550101" } ] }""");
        var vm = new SettingsViewModel(store, () => { }) { PsstContactsPath = contacts };
        Assert.That(vm.AlertTo, Is.EqualTo("ryan"), "the default");

        vm.AlertTo = "nobody-known";
        vm.SaveCommand.Execute(null);
        Assert.That(vm.Error, Does.StartWith("Text to"));
        Assert.That(store.LoadSettings().PsstAlertTo, Is.EqualTo("ryan"), "nothing saved");

        vm.AlertTo = "920 555 0101";
        vm.SaveCommand.Execute(null);
        var saved = store.LoadSettings();
        Assert.That((vm.Error, saved.PsstAlertTo), Is.EqualTo(("", "920 555 0101")));

        vm.AlertTo = "";
        vm.SaveCommand.Execute(null);
        saved = store.LoadSettings();
        Assert.That((vm.Error, saved.PsstAlertTo), Is.EqualTo(("", "")), "blank texts nobody, and stays blank");
        Assert.That((new SettingsViewModel(store, () => { }).AlertTo), Is.EqualTo(""));
    }

    [Test]
    public void The_weekly_pacing_fields_round_trip_through_the_panel_and_are_validated()
    {
        // M.ORCH-T680: the weekly target (saved as the reserve).
        var store = new JsonStore(dir);
        store.SaveSettings(store.LoadSettings());
        var vm = new SettingsViewModel(store, () => { });
        Assert.That(vm.WeeklyTarget, Is.EqualTo("90"), "the defaults");

        vm.WeeklyTarget = "85";
        vm.SaveCommand.Execute(null);
        var saved = store.LoadSettings();
        Assert.That((vm.Error, saved.WeeklyAwarePacing, saved.WeeklyReservePercent),
            Is.EqualTo(("", false, 15)), "weekly-aware pacing can't be switched on");
        Assert.That(saved.WeeklyAwareTarget, Is.EqualTo(85));
        var again = new SettingsViewModel(store, () => { });
        Assert.That(again.WeeklyTarget, Is.EqualTo("85"));

        again.WeeklyTarget = "40";
        again.SaveCommand.Execute(null);
        Assert.That(again.Error, Does.StartWith("Weekly target"));
        Assert.That(store.LoadSettings().WeeklyReservePercent, Is.EqualTo(15), "nothing invalid was saved");
    }

    [Test]
    public void Saving_the_panel_writes_only_the_fields_edited_in_it_not_what_it_read_before_the_cli_changed_others()
    {
        // M.ORCH-T338: every field was written back as the panel read it, undoing a change made from the CLI meanwhile.
        var store = new JsonStore(dir);
        store.SaveSettings(store.LoadSettings() with { BlockBudget = 20_000_000, MaxConcurrentSessions = 4, Pacing = true });
        var vm = new SettingsViewModel(store, () => { });
        // orchestrator budget --block 30M --pace off, settings --max-sessions 8 --exempt /do, while the panel is open
        store.SaveSettings(store.LoadSettings() with { BlockBudget = 30_000_000, MaxConcurrentSessions = 8, Pacing = false, ExemptCommands = ["/do"] });

        vm.WarnPercent = "70";
        vm.SaveCommand.Execute(null);

        var saved = store.LoadSettings();
        Assert.That((vm.Error, saved.WarnPercent, saved.BlockBudget, saved.MaxConcurrentSessions, saved.Pacing, string.Join(",", saved.ExemptCommands)),
            Is.EqualTo(("", 70, (long?)30_000_000, 8, false, "/do")));
        Assert.That((vm.BlockBudget, vm.MaxSessions), Is.EqualTo(("30M", "8")), "and the panel shows what's saved now");
    }

    [Test]
    public void A_value_set_from_the_cli_outside_the_panels_range_doesnt_block_saving_other_fields()
    {
        // M.ORCH-T349: settings --max-sessions 60 made every Save fail on "Max sessions: 1 to 50".
        var store = new JsonStore(dir);
        store.SaveSettings(store.LoadSettings() with { MaxConcurrentSessions = 60 });
        var vm = new SettingsViewModel(store, () => { });
        vm.BlockBudget = "30M";
        vm.SaveCommand.Execute(null);
        Assert.That((vm.Error, store.LoadSettings().BlockBudget, store.LoadSettings().MaxConcurrentSessions), Is.EqualTo(("", (long?)30_000_000, 60)));

        vm.MaxSessions = "61";
        vm.SaveCommand.Execute(null);
        Assert.That(vm.Error, Does.StartWith("Max sessions: 1 to 50"), "typed in the panel, it's still checked");
    }

    [Test]
    public void An_empty_queue_names_the_tickets_waiting_for_approval()
    {
        // M.ORCH-T350: it said "No open tickets anywhere" while unapproved features were open.
        Assert.That(MainViewModel.EmptyQueue(0), Does.StartWith("No open tickets anywhere"));
        Assert.That(MainViewModel.EmptyQueue(2), Is.EqualTo("Nothing workable: 2 open tickets wait for your approval (tickets ticket approve <ID>); fixes need none."));
        Assert.That(MainViewModel.EmptyQueue(1), Does.Contain("1 open ticket waits"));
    }

    [Test]
    public void A_finished_row_follows_its_tickets_latest_assignment()
    {
        // M.ORCH-T341: the row kept the assignment it was built from, and its button acted on the old directory.
        var row = new FinishedViewModel(new Assignment("A-T1", "A", @"D:\W\A", "Claude", DateTime.UtcNow, false), null!);
        var changed = new List<string?>();
        row.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        row.Update(new Assignment("A-T1", "A", @"D:\WT\A\A-T1", "Claude", DateTime.UtcNow, true));

        Assert.That((row.Text, row.ActionLabel), Is.EqualTo(($@"A-T1 in A is closed. Its worktree is still at D:\WT\A\A-T1.", "Remove worktree")));
        Assert.That(changed, Is.EquivalentTo(new[] { nameof(row.Text), nameof(row.TextAfterId), nameof(row.ActionLabel) }));
        Assert.That(row.TextAfterId, Is.EqualTo(@" in A is closed. Its worktree is still at D:\WT\A\A-T1."), "the id before it is a link into Tickets Desktop (M.ORCH-T500)");
    }

    [Test]
    public void A_hand_edited_cycle_percent_no_field_shows_never_blocks_save()
    {
        var store = new JsonStore(dir);
        store.SaveSettings(store.LoadSettings() with { ContextCyclePercent = 99 });
        var vm = new SettingsViewModel(store, () => { });
        vm.WarnPercent = "70";
        vm.SaveCommand.Execute(null);
        Assert.That((vm.Error, store.LoadSettings().WarnPercent), Is.EqualTo(("", 70)));
    }

    [Test]
    public void Saving_other_settings_never_rounds_a_budget_set_to_the_token()
    {
        var store = new JsonStore(dir);
        store.SaveSettings(store.LoadSettings() with { BlockBudget = 12_345_678, WeeklyBudget = 1_234_567_890 });
        var vm = new SettingsViewModel(store, () => { });
        vm.Allocate = !vm.Allocate;
        vm.SaveCommand.Execute(null);
        var saved = store.LoadSettings();
        Assert.That((vm.Error, saved.BlockBudget, saved.WeeklyBudget), Is.EqualTo(("", (long?)12_345_678, (long?)1_234_567_890)));
    }

    [Test]
    public void A_quiet_repo_active_under_a_minute_ago_says_just_now_not_now_ago()
    {
        // M.ORCH-T463: the detail pane read "Quiet. Last agent activity now ago."
        var now = new DateTime(2026, 10, 18, 12, 0, 0, DateTimeKind.Utc);
        RepoTileViewModel Quiet(TimeSpan ago)
        {
            var tile = new RepoTileViewModel("A");
            tile.Update(new RepoStatus("A", @"D:\A", RepoState.Dormant, [], [], [], now - ago, default), now);
            return tile;
        }

        Assert.That(Quiet(TimeSpan.FromSeconds(20)).Headline, Is.EqualTo("Quiet. Last agent activity just now."));
        Assert.That(Quiet(TimeSpan.FromMinutes(5)).Headline, Is.EqualTo("Quiet. Last agent activity 5m ago."));
    }

    private sealed class Row(string key, int value)
    {
        public string Key { get; } = key;
        public int Value { get; set; } = value;
    }

    [Test]
    public void Reorder_moves_existing_rows_inserts_new_ones_and_drops_extras_keeping_each_row()
    {
        Row a = new("a", 1), b = new("b", 2), c = new("c", 3), d = new("d", 4);
        var rows = new ObservableCollection<Row>([a, b, c]);
        var changes = new List<NotifyCollectionChangedAction>();
        rows.CollectionChanged += (_, e) => changes.Add(e.Action);

        rows.ReorderTo([c, a, d]);

        Assert.That(rows, Is.EqualTo(new[] { c, a, d }).AsCollection);
        Assert.That(changes, Does.Not.Contain(NotifyCollectionChangedAction.Reset));
        Assert.That(changes.Count(x => x == NotifyCollectionChangedAction.Add), Is.EqualTo(1), "only d is new; c and a were moved, not re-added");
        Assert.That(changes, Has.Some.EqualTo(NotifyCollectionChangedAction.Move));
        Assert.That(changes.Count(x => x == NotifyCollectionChangedAction.Remove), Is.EqualTo(1), "b dropped");

        changes.Clear();
        rows.ReorderTo([c, a, d]);
        Assert.That(changes, Is.Empty, "already in order: untouched");
    }

    [Test]
    public void Keyed_sync_updates_surviving_rows_in_place_creates_new_ones_and_drops_the_rest()
    {
        var rows = new ObservableCollection<Row>([new("a", 1), new("b", 2)]);
        var a = rows[0];

        rows.SyncBy([("c", 30), ("a", 10)], s => s.Item1, r => r.Key, s => new Row(s.Item1, s.Item2), (r, s) => r.Value = s.Item2);

        Assert.That(rows.Select(r => (r.Key, r.Value)), Is.EqualTo(new[] { ("c", 30), ("a", 10) }));
        Assert.That(rows[1], Is.SameAs(a), "a's row is the same object, updated, so its bound control keeps its state");
    }

    [Test]
    public void Keyed_sync_keeps_the_first_of_a_repeated_key_so_no_row_is_placed_twice()
    {
        // Placed twice, the next refresh's ToDictionary threw and the async void timer took the dashboard down.
        var rows = new ObservableCollection<Row>();
        rows.SyncBy([("a", 1), ("b", 2), ("a", 3)], s => s.Item1, r => r.Key, s => new Row(s.Item1, s.Item2), (r, s) => r.Value = s.Item2);
        Assert.That(rows.Select(r => (r.Key, r.Value)), Is.EqualTo(new[] { ("a", 1), ("b", 2) }));
        Assert.That(() => rows.SyncBy([("a", 4)], s => s.Item1, r => r.Key, s => new Row(s.Item1, s.Item2), (r, s) => r.Value = s.Item2), Throws.Nothing);
        Assert.That(rows.Select(r => (r.Key, r.Value)), Is.EqualTo(new[] { ("a", 4) }));
    }
}
