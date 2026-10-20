using System.Diagnostics;
using MindAttic.Orchestrator.Budget;
using MindAttic.Orchestrator.Settings;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>The orchestrator's history in orchestrator.db (M.ORCH-T494): the one-time import of the JSON files it replaced,
/// two processes writing at once, and a Python hook reading while the daemon writes.</summary>
[NonParallelizable]
public sealed class OrchestratorHistoryTests
{
    private static readonly DateTime Base = new(2026, 10, 15, 0, 0, 0, DateTimeKind.Utc);
    private readonly TempDirs temps = new();

    [TearDown]
    public void TearDown() => temps.DeleteAll();

    private static UsageSample Sample(int minute, double percent = 10) =>
        new(Base.AddMinutes(minute), percent, Base.AddHours(5), 1_000L * minute);

    private static LedgerWindow Window(int n, string? account = null) =>
        new(Base.AddHours(5 * n), Base.AddHours(5 * (n + 1)), WindowGrade.Under, "ended at 90%", 91, 90.5, 12_345_678, 59, 0, null, null,
            3, 1.5, 0, 0, [new MeterGap(Base.AddHours(5 * n + 1), Base.AddHours(5 * n + 1.5), 77)], 1, account);

    /// <summary>One of the old history files, as the JSON store wrote it before M.ORCH-T524 moved the store into the database.</summary>
    private static void WriteJson<T>(string dir, string file, T value)
    {
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, file), System.Text.Json.JsonSerializer.Serialize(value, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
    }

    private static void WriteJsonFiles(string dir, List<UsageSample> samples, List<LedgerWindow> ledger, List<DayUsage> history, List<SessionCost> costs)
    {
        WriteJson(dir, OrchestratorHistory.SamplesFile, samples);
        WriteJson(dir, OrchestratorHistory.HistoryFile, history);
        WriteJson(dir, OrchestratorHistory.CostsFile, costs);
        File.WriteAllLines(Path.Combine(dir, OrchestratorHistory.LedgerFile), ledger.Select(PaceLedger.Serialize));
    }

    [Test]
    public void The_json_history_moves_into_the_tables_once_and_the_files_are_kept_as_bak()
    {
        var dir = temps.New();
        var db = new OrchestratorDb(Path.Combine(temps.New(), "data", "orchestrator.db"));
        List<UsageSample> samples = [Sample(1, 5), Sample(5, 7.5)];
        List<LedgerWindow> ledger = [Window(0), Window(1, "acct")];
        List<DayUsage> history = [new(new DateOnly(2026, 10, 1), "A", "Claude", 100), new(new DateOnly(2026, 10, 2), "B", "Claude", 200, "acct", Base)];
        List<SessionCost> costs = [new("s1", "A-T1", "fix", "A", 500, Base), new("s2/agent", "A-T2", null, "A", 60, Base.AddHours(1), OwnShare: true)];
        WriteJsonFiles(dir, samples, ledger, history, costs);
        // A day the database already has wins over the file's.
        db.SaveUsageHistory([history[0] with { Fresh = 999 }]);

        db.ImportJson(dir);

        Assert.That(db.LoadUsageSamples(), Is.EqualTo(samples));
        IReadOnlyList<MeterGap> none = [];
        Assert.That(db.LoadLedger().Select(w => w with { Gaps = none }), Is.EqualTo(ledger.Select(w => w with { Gaps = none })));
        Assert.That(db.LoadLedger().Select(w => w.Gaps.Single()), Is.EqualTo(ledger.Select(w => w.Gaps.Single())));
        Assert.That(db.LoadUsageHistory(), Is.EqualTo(new[] { history[0] with { Fresh = 999 }, history[1] }));
        Assert.That(db.LoadTicketCosts(), Is.EqualTo(costs));
        string[] files = [OrchestratorHistory.SamplesFile, OrchestratorHistory.LedgerFile, OrchestratorHistory.HistoryFile, OrchestratorHistory.CostsFile];
        Assert.That(Directory.GetFiles(dir).Select(Path.GetFileName).Order(), Is.EqualTo(files.Select(f => f + ".bak").Order()), "each file kept as .bak");
        Assert.That(File.ReadAllText(db.DumpPath), Does.Contain("INSERT INTO usage_samples (at, percent, reset, window_fresh) VALUES ('2026-10-15T00:01:00Z', 5, '2026-10-15T05:00:00Z', 1000);"),
            "the dump has every row, one INSERT a line");

        // The same files again (another process that read them before the rename, or a rename that failed): nothing twice.
        var again = temps.New();
        WriteJsonFiles(again, samples, ledger, history, costs);
        db.ImportJson(again);
        Assert.That((db.LoadUsageSamples().Count, db.LoadLedger().Count, db.LoadUsageHistory().Count, db.LoadTicketCosts().Count), Is.EqualTo((2, 2, 2, 2)));
    }

    [Test]
    public void A_held_file_waits_for_the_next_run_and_an_unreadable_one_is_kept_with_nothing_taken()
    {
        var dir = temps.New();
        var db = new OrchestratorDb(Path.Combine(temps.New(), "orchestrator.db"));
        WriteJson(dir, OrchestratorHistory.SamplesFile, new List<UsageSample> { Sample(1) });
        File.WriteAllText(Path.Combine(dir, OrchestratorHistory.CostsFile), "[ {");

        using (new FileStream(Path.Combine(dir, OrchestratorHistory.SamplesFile), FileMode.Open, FileAccess.Read, FileShare.None))
            db.ImportJson(dir);
        Assert.That(db.LoadUsageSamples(), Is.Empty, "held: not read");
        Assert.That(File.Exists(Path.Combine(dir, OrchestratorHistory.SamplesFile)), Is.True, "and left where it is");
        Assert.That(File.Exists(Path.Combine(dir, OrchestratorHistory.CostsFile + ".bak")), Is.True, "not JSON: kept aside as .bak");
        Assert.That(db.LoadTicketCosts(), Is.Empty);

        db.ImportJson(dir);
        Assert.That(db.LoadUsageSamples(), Is.EqualTo(new[] { Sample(1) }), "read on the next run, once free");
        Assert.That(File.Exists(Path.Combine(dir, OrchestratorHistory.SamplesFile + ".bak")), Is.True);
    }

    [Test]
    public void The_live_files_are_never_moved_into_a_scratch_database()
    {
        // OrchestratorDbPerTest sets MINDATTIC_ORCHESTRATOR_DB, as any override does: %APPDATA%'s files stay where they are.
        var live = new JsonStore().Directory;
        var before = Directory.Exists(live) ? Directory.GetFiles(live).Order().ToList() : [];
        new OrchestratorDb(OrchestratorDb.DefaultPath()).ImportJson(live);
        Assert.That(Directory.Exists(live) ? Directory.GetFiles(live).Order().ToList() : [], Is.EqualTo(before));
        Assert.That(new OrchestratorDb(OrchestratorDb.DefaultPath()).LoadUsageSamples(), Is.Empty);
    }

    [Test]
    public void Readings_older_than_the_week_are_trimmed_as_new_ones_are_added()
    {
        var db = new OrchestratorDb(Path.Combine(temps.New(), "orchestrator.db"));
        db.AddUsageSamples([Sample(1), Sample(2)], DateTime.MinValue);
        db.AddUsageSamples([Sample(2, 50), Sample(3)], Base.AddMinutes(2));
        Assert.That(db.LoadUsageSamples(), Is.EqualTo(new[] { Sample(2), Sample(3) }), "the first trimmed; a reading already kept isn't replaced");
    }

    /// <summary>One writer process (<see cref="SnapshotWriterProgram.HistoryVerb"/>): <paramref name="count"/> meter readings and
    /// ledger windows of its own, each its own transaction, as the daemon and the dashboard each write theirs.</summary>
    internal static int Writer(string dbPath, int offset, int count)
    {
        try
        {
            var db = new OrchestratorDb(dbPath);
            for (var k = 0; k < count; k++)
            {
                db.AddUsageSamples([Sample(offset * count + k)], DateTime.MinValue);
                if (k % 10 == 0) db.AppendLedger([Window(offset * count + k)]);
            }
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }

    [Test]
    public void Two_processes_writing_at_once_lose_nothing()
    {
        const int count = 60;
        var dbPath = Path.Combine(temps.New(), "orchestrator.db");
        new OrchestratorDb(dbPath).Open().Dispose(); // created up front, so the writers race on rows, not on making the schema
        var host = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") is { Length: > 0 } h && File.Exists(h) ? h : "dotnet";
        var assembly = typeof(OrchestratorHistoryTests).Assembly.Location;
        var writers = new[] { 0, 1 }.Select(offset =>
        {
            var psi = new ProcessStartInfo(host) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true };
            foreach (var arg in new[] { "exec", assembly, SnapshotWriterProgram.HistoryVerb, dbPath, offset.ToString(), count.ToString() }) psi.ArgumentList.Add(arg);
            return Process.Start(psi)!;
        }).ToList();
        foreach (var w in writers)
        {
            var error = w.StandardError.ReadToEnd();
            Assert.That(w.WaitForExit(120_000), Is.True, "the writer finished");
            Assert.That(w.ExitCode, Is.Zero, error);
        }

        var db = new OrchestratorDb(dbPath);
        Assert.That(db.LoadUsageSamples().Select(s => s.AtUtc), Is.EqualTo(Enumerable.Range(0, 2 * count).Select(m => Base.AddMinutes(m))), "every reading from both");
        Assert.That(db.LoadLedger(), Has.Count.EqualTo(2 * count / 10), "every window from both");
        using (var c = db.Open())
        using (var check = c.CreateCommand())
        {
            check.CommandText = "PRAGMA integrity_check;";
            Assert.That(check.ExecuteScalar(), Is.EqualTo("ok"));
        }
        Assert.That(File.ReadAllText(db.DumpPath).Split('\n').Count(l => l.StartsWith("INSERT INTO usage_samples ", StringComparison.Ordinal)),
            Is.EqualTo(2 * count), "the dump holds what the last writer committed: all of it");
        Assert.That(Directory.GetFiles(Path.GetDirectoryName(dbPath)!, "*.tmp"), Is.Empty, "no temp dump left behind");
    }

    [Test]
    public void A_python_hook_reads_the_tables_while_the_daemon_writes()
    {
        var db = new OrchestratorDb(Path.Combine(temps.New(), "orchestrator.db"));
        db.Open().Dispose();
        // As a hook would: the stdlib sqlite3 module, a fresh connection each read, its default busy timeout (5 s).
        const string script = """
            import sqlite3, sys, time
            path, until = sys.argv[1], time.time() + float(sys.argv[2])
            reads, errors, last = 0, [], -1
            while time.time() < until:
                try:
                    con = sqlite3.connect(path, timeout=5)
                    try:
                        n = con.execute("SELECT count(*) FROM usage_samples").fetchone()[0]
                        con.execute("SELECT end_utc, grade FROM pace_ledger").fetchall()
                    finally:
                        con.close()
                    if n < last: errors.append("went back from %d to %d" % (last, n))
                    last, reads = n, reads + 1
                except Exception as e:
                    errors.append(repr(e))
            print(reads, last)
            for e in errors: print(e, file=sys.stderr)
            sys.exit(1 if errors else 0)
            """;
        Process python;
        try
        {
            var psi = new ProcessStartInfo("python") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var arg in new[] { "-I", "-c", script, db.Path, "4" }) psi.ArgumentList.Add(arg);
            python = Process.Start(psi)!;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            Assert.Ignore("python isn't on PATH");
            return;
        }

        var written = 0;
        var until = DateTime.UtcNow.AddSeconds(3);
        while (DateTime.UtcNow < until)
        {
            db.AddUsageSamples([Sample(written)], DateTime.MinValue);
            if (written % 20 == 0) db.AppendLedger([Window(written)]);
            written++;
        }
        var output = python.StandardOutput.ReadToEndAsync();
        var error = python.StandardError.ReadToEnd();
        Assert.That(python.WaitForExit(30_000), Is.True);
        Assert.That(python.ExitCode, Is.Zero, error);
        var parts = output.Result.Split(' ', StringSplitOptions.TrimEntries);
        Assert.That(int.Parse(parts[0]), Is.GreaterThan(0), "it read while the writes went on");
        Assert.That(int.Parse(parts[1]), Is.EqualTo(written), "and sees every reading once they're done");
        Assert.That(written, Is.GreaterThan(10));
    }
}
