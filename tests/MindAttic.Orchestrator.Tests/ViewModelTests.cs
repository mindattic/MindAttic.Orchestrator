using System.Collections.ObjectModel;
using System.Collections.Specialized;
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
            (v => v.MaxDelay = "-1", "Longest hold"),
            (v => v.MaxDelay = (Setup.AgentGate.MaxHoldSeconds + 1).ToString(), "Longest hold"),
            (v => v.ContextPercent = "9", "Cycle context at: 10 to 95"),
            (v => v.ContextPercent = "96", "Cycle context at: 10 to 95"),
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
