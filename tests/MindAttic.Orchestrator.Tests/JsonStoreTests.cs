using MindAttic.Orchestrator.Settings;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

public sealed class JsonStoreTests
{
    private string dir = "";

    [SetUp]
    public void SetUp() => dir = Path.Combine(Path.GetTempPath(), "orch-store-" + Guid.NewGuid().ToString("N"), "nested");

    [TearDown]
    public void TearDown()
    {
        var top = Path.GetDirectoryName(dir)!;
        if (Directory.Exists(top)) Directory.Delete(top, recursive: true);
    }

    [Test]
    public void A_missing_unreadable_or_locked_file_loads_as_defaults()
    {
        var store = new JsonStore(dir);
        Assert.That(SameJson(store.LoadSettings(), new OrchestratorSettings()), Is.True, "missing");

        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "settings.json");
        File.WriteAllText(path, "{ not json");
        Assert.That(store.LoadSettings().MaxConcurrentSessions, Is.EqualTo(new OrchestratorSettings().MaxConcurrentSessions), "invalid JSON");

        File.WriteAllText(path, """{ "MaxConcurrentSessions": 9 }""");
        using (new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            Assert.That(store.LoadSettings().MaxConcurrentSessions, Is.EqualTo(new OrchestratorSettings().MaxConcurrentSessions), "locked by another process");
        Assert.That(store.LoadSettings().MaxConcurrentSessions, Is.EqualTo(9), "readable again");
    }

    [Test]
    public void A_file_locked_only_for_a_moment_loads_its_real_contents_and_concurrent_saves_never_tear()
    {
        // Callers load, change and save: defaults read during another process's save were saved over the real file.
        var store = new JsonStore(dir);
        store.SaveSettings(new OrchestratorSettings { MaxConcurrentSessions = 9 });
        var path = Path.Combine(dir, "settings.json");
        var locked = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        var release = Task.Delay(200).ContinueWith(_ => locked.Dispose());
        Assert.That(store.LoadSettings().MaxConcurrentSessions, Is.EqualTo(9));
        release.Wait();

        Parallel.For(0, 40, i => new JsonStore(dir).SaveSettings(new OrchestratorSettings { MaxConcurrentSessions = 1 + i % 5 }));
        Assert.That(store.LoadSettings().MaxConcurrentSessions, Is.InRange(1, 5), "always one whole save, never a torn mix");
        Assert.That(Directory.GetFiles(dir).Select(Path.GetFileName), Is.EqualTo(new[] { "settings.json" }), "no temp left behind");
    }

    [Test]
    public void Assignments_changed_at_once_by_several_writers_all_survive()
    {
        // A dashboard's Dispatch and the daemon's autopilot each loaded, changed and saved: one dropped the other's (M.ORCH-T119).
        Parallel.For(0, 24, i => new JsonStore(dir).UpdateAssignments(all =>
            all.Add(new Assignment($"X-T{i}", "X", @"D:\W\X", "Claude", DateTime.UtcNow, true))));
        Assert.That(new JsonStore(dir).LoadAssignments().Select(a => a.TicketId), Is.EquivalentTo(Enumerable.Range(0, 24).Select(i => $"X-T{i}")));
        Assert.That(Directory.GetFiles(dir).Select(Path.GetFileName), Is.EqualTo(new[] { "assignments.json" }), "the lock file goes with its holder");
    }

    [Test]
    public void A_change_to_a_file_that_cant_be_read_leaves_it_alone_instead_of_saving_an_empty_one_over_it()
    {
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "assignments.json");
        File.WriteAllText(path, "[ { \"TicketId\": \"X-T1\", ");
        Assert.That(() => new JsonStore(dir).UpdateAssignments(all => all.Clear()), Throws.InstanceOf<InvalidDataException>());
        Assert.That(File.ReadAllText(path), Is.EqualTo("[ { \"TicketId\": \"X-T1\", "), "untouched for the next try");
    }

    [Test]
    public void The_lease_is_one_holders_at_a_time_and_names_it()
    {
        using var first = new MindAttic.Orchestrator.Scheduling.DispatchLease(dir);
        using var second = new MindAttic.Orchestrator.Scheduling.DispatchLease(dir);
        Assert.That((first.TryAcquire(), second.TryAcquire()), Is.EqualTo((true, false)));
        using (var read = new FileStream(first.Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            Assert.That(new StreamReader(read).ReadToEnd(), Is.EqualTo(Environment.ProcessId.ToString()));
        first.Dispose();
        Assert.That(second.TryAcquire(), Is.True, "free once its holder lets go");
    }

    [Test]
    public void Save_creates_the_folder_writes_through_a_temp_file_and_leaves_no_temp_behind()
    {
        var store = new JsonStore(dir);
        store.SaveSettings(new OrchestratorSettings { MaxConcurrentSessions = 3 });
        store.SaveSettings(new OrchestratorSettings { MaxConcurrentSessions = 4 }); // over an existing file
        Assert.That(Directory.GetFiles(dir).Select(Path.GetFileName), Is.EqualTo(new[] { "settings.json" }));
        Assert.That(store.LoadSettings().MaxConcurrentSessions, Is.EqualTo(4));
    }

    [Test]
    public void Settings_and_assignments_round_trip_and_nulls_are_left_out()
    {
        var store = new JsonStore(dir);
        var s = new OrchestratorSettings
        {
            RepoBudgetPercent = new() { ["Prose"] = 40 },
            Reservations = new() { ["X-T5"] = 2_000_000 },
            BlockedBy = new() { ["X-T2"] = ["X-T1"] },
            QuietHours = ["22:00-07:00"],
            NotifyUrl = null,
        };
        store.SaveSettings(s);
        var back = store.LoadSettings();
        Assert.That(back.RepoBudgetPercent["Prose"], Is.EqualTo(40));
        Assert.That(back.Reservations["X-T5"], Is.EqualTo(2_000_000));
        Assert.That(back.BlockedBy["X-T2"], Is.EqualTo(new[] { "X-T1" }));
        Assert.That(back.QuietHours, Is.EqualTo(new[] { "22:00-07:00" }));
        Assert.That(File.ReadAllText(Path.Combine(dir, "settings.json")), Does.Not.Contain("NotifyUrl"), "nulls omitted");

        var a = new Assignment("X-T1", "X", @"D:\W\X", "Claude", new DateTime(2026, 10, 15, 4, 0, 0, DateTimeKind.Utc), true) { Pilot = 7, Log = "x.log" };
        store.SaveAssignments([a]);
        Assert.That(store.LoadAssignments().Single(), Is.EqualTo(a));
    }

    private static bool SameJson(OrchestratorSettings x, OrchestratorSettings y) =>
        System.Text.Json.JsonSerializer.Serialize(x) == System.Text.Json.JsonSerializer.Serialize(y);
}
