using System.Collections.ObjectModel;
using System.Collections.Specialized;
using MindAttic.Components.Wpf.Mvvm;
using MindAttic.Orchestrator.Budget;
using MindAttic.Orchestrator.Settings;
using MindAttic.Orchestrator.Wpf.ViewModels;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>The dashboard's view-model logic: the row syncing every list uses. (The settings panel's tests went with it: the
/// settings are edited in MindAttic Vault now, LAMP M.ORCH-T1308; see LampSettingsTests.)</summary>
public sealed class ViewModelTests
{
    private string dir = "";

    [SetUp]
    public void SetUp() => dir = Path.Combine(Path.GetTempPath(), "orch-vm-" + Guid.NewGuid().ToString("N"));

    [TearDown]
    public void TearDown() { if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true); }

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
    public void Launcher_tabs_show_the_login_each_cli_bills()
    {
        var host = new HostRowViewModel(new HostSession("Claude", 7, "p", null) { Login = "g (fallback)", LoginEmail = "g@b.com" });
        Assert.That((host.Detail, host.DetailTip), Is.EqualTo(("PID 7 · g (fallback)", "Bills the Claude login g@b.com")));
        host.Update(new HostSession("Claude", 7, "p", null));
        Assert.That(host.Detail, Is.EqualTo("PID 7"));
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

    [Test]
    public void Toggle_lock_state_locks_then_unlocks_a_login_never_the_api_key()
    {
        // M.ORCH-T2118: IsLoginLocked and ToggleLockCommand read/write through LoginLocks the same way; IsLocked is the
        // static read both share, so exercising it here toggles the same lock state the command flips.
        const string erin = "ohleo199@mindattic.com";
        var temps = new TempDirs();
        try
        {
            var store = new JsonStore(temps.New());
            Assert.That(MainViewModel.IsLocked(store, erin, isApiKeyTab: false), Is.False);

            Assert.That(LoginLocks.Lock(store, erin, "Erin", LoginLocks.ReasonManual, "you", null, DateTime.UtcNow), Is.Not.Null);
            Assert.That(MainViewModel.IsLocked(store, erin, isApiKeyTab: false), Is.True, "locked: the menu now offers Unlock");

            Assert.That(LoginLocks.Unlock(store, erin, "Erin", "you", DateTime.UtcNow), Is.Not.Null);
            Assert.That(MainViewModel.IsLocked(store, erin, isApiKeyTab: false), Is.False, "unlocked again: the menu offers Lock once more");

            Assert.That(MainViewModel.IsLocked(store, null, isApiKeyTab: false), Is.False, "no email known yet (the global login, not signed in anywhere): never locked");
            Assert.That(MainViewModel.IsLocked(store, erin, isApiKeyTab: true), Is.False, "the API key tab is never considered locked");
        }
        finally { temps.DeleteAll(); }
    }

    [Test]
    public void Is_locked_is_gated_by_the_live_locked_set_before_the_lock_book()
    {
        // M.ORCH-T2141: IsLoginLocked passes LoginLocks.LiveLocked's cached set, so an unlocked login never reads the book.
        const string erin = "ohleo199@mindattic.com";
        var temps = new TempDirs();
        try
        {
            var store = new JsonStore(temps.New());
            LoginLocks.Lock(store, erin, "Erin", LoginLocks.ReasonManual, "you", null, DateTime.UtcNow);
            Assert.That(MainViewModel.IsLocked(store, erin, isApiKeyTab: false, new HashSet<string>()), Is.False, "the live set doesn't name it: not locked");
            Assert.That(MainViewModel.IsLocked(store, "OHLEO199@MindAttic.com", isApiKeyTab: false, new HashSet<string> { erin }), Is.True, "named, and confirmed by the book");

            LoginLocks.Unlock(store, erin, "Erin", "you", DateTime.UtcNow);
            Assert.That(MainViewModel.IsLocked(store, erin, isApiKeyTab: false, new HashSet<string> { erin }), Is.False, "a stale set is confirmed against the book");
        }
        finally { temps.DeleteAll(); }
    }

    /// <summary>M.ORCH-T2125: EligibleForGeneralPrompt (MainViewModel.Tabs.cs), the Launcher auto-open/respawn's "wanted"
    /// filter and RunLauncherCommand's CanExecute each add a locked check (IsLoginLocked, the same MainViewModel.IsLocked
    /// just exercised above) alongside their existing signed-in/disabled ones, so a locked login never gets a new General
    /// Prompt or Launcher opened on it. This composes those checks exactly as the view model does, pinning the outcome
    /// without standing up a full MainViewModel (the same approach as
    /// A_tabs_busy_idle_and_needs_you_badges_follow_the_live_agent_board in TabStatusTests).</summary>
    [Test]
    public void A_locked_login_is_never_eligible_for_a_new_General_Prompt_or_Launcher()
    {
        const string erin = "ohleo199@mindattic.com";
        var temps = new TempDirs();
        try
        {
            var store = new JsonStore(temps.New());
            bool EligibleForGeneralPrompt(bool signedInAsItself, bool signedIn, bool disabled) =>
                !disabled && !MainViewModel.IsLocked(store, erin, isApiKeyTab: false) && (signedInAsItself || !signedIn);
            bool WantsLauncher(bool tabSignedIn) => tabSignedIn && !MainViewModel.IsLocked(store, erin, isApiKeyTab: false);
            bool CanRunLauncher(bool tabSignedIn) => tabSignedIn && !MainViewModel.IsLocked(store, erin, isApiKeyTab: false);

            Assert.That(EligibleForGeneralPrompt(signedInAsItself: true, signedIn: true, disabled: false), Is.True);
            Assert.That(WantsLauncher(tabSignedIn: true), Is.True);
            Assert.That(CanRunLauncher(tabSignedIn: true), Is.True);

            LoginLocks.Lock(store, erin, "Erin", LoginLocks.ReasonManual, "you", null, DateTime.UtcNow);

            Assert.That(EligibleForGeneralPrompt(signedInAsItself: true, signedIn: true, disabled: false), Is.False, "locked: no new General Prompt opens");
            Assert.That(WantsLauncher(tabSignedIn: true), Is.False, "locked: no new Launcher respawns");
            Assert.That(CanRunLauncher(tabSignedIn: true), Is.False, "locked: Run Launcher is disabled");

            LoginLocks.Unlock(store, erin, "Erin", "you", DateTime.UtcNow);
            Assert.That(EligibleForGeneralPrompt(signedInAsItself: true, signedIn: true, disabled: false), Is.True, "unlocked again: eligible once more");
        }
        finally { temps.DeleteAll(); }
    }

    /// <summary>M.ORCH-T2185: "+ Add" (CanAddCli/AddCliLockedTip, raised with the tab status) is disabled while the login is
    /// locked, with a tooltip saying how to unlock it, and comes back once it's unlocked.</summary>
    [Test]
    public void Add_is_disabled_with_an_unlock_tip_while_the_login_is_locked()
    {
        const string erin = "ohleo199@mindattic.com";
        var temps = new TempDirs();
        try
        {
            var store = new JsonStore(temps.New());
            bool Locked() => MainViewModel.IsLocked(store, erin, isApiKeyTab: false);

            Assert.That(MainViewModel.CanAddCliOf(signedIn: true, Locked()), Is.True);
            Assert.That(MainViewModel.AddCliLockedTipOf(Locked()), Is.Null, "unlocked: each button keeps its own tooltip");
            Assert.That(MainViewModel.CanAddCliOf(signedIn: false, Locked()), Is.False, "signed out: still disabled as before");

            LoginLocks.Lock(store, erin, "Erin", LoginLocks.ReasonManual, "you", null, DateTime.UtcNow);
            Assert.That(MainViewModel.CanAddCliOf(signedIn: true, Locked()), Is.False, "locked: + Add is disabled");
            Assert.That(MainViewModel.AddCliLockedTipOf(Locked()), Does.StartWith("Locked").And.Contains("Unlock"));

            LoginLocks.Unlock(store, erin, "Erin", "you", DateTime.UtcNow);
            Assert.That(MainViewModel.CanAddCliOf(signedIn: true, Locked()), Is.True, "unlocked again: + Add is back");
        }
        finally { temps.DeleteAll(); }
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
