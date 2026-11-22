using System.IO.Compression;
using MindAttic.Orchestrator.Backup;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>M.ORCH-T2046: the backup engine ported from MindAttic.Launcher (dated folder, SQLite dumps, AppData zip).</summary>
public sealed class BackupEngineTests
{
    private string temp = "";

    [SetUp]
    public void SetUp()
    {
        temp = Path.Combine(Path.GetTempPath(), "MindAtticOrchBackupTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
    }

    [TearDown]
    public void TearDown()
    {
        try { if (Directory.Exists(temp)) Directory.Delete(temp, recursive: true); } catch { /* best-effort */ }
    }

    [Test]
    public void Target_is_the_bare_date_when_free_then_the_first_free_letter()
    {
        var now = new DateTime(2026, 11, 22, 9, 0, 0);
        Assert.That(BackupFolders.ResolveTarget(@"R:\B", now, _ => false), Is.EqualTo(@"R:\B\2026-11-22"));

        var taken = new HashSet<string> { @"R:\B\2026-11-22", @"R:\B\2026-11-22_a" };
        Assert.That(BackupFolders.ResolveTarget(@"R:\B", now, taken.Contains), Is.EqualTo(@"R:\B\2026-11-22_b"));
        Assert.Throws<InvalidOperationException>(() => BackupFolders.ResolveTarget(@"R:\B", now, _ => true));
    }

    [Test]
    public void Stale_keeps_the_newest_dates_with_their_letter_siblings_and_ignores_other_folders()
    {
        var folders = new[]
        {
            @"R:\B\2026-11-20", @"R:\B\2026-11-21", @"R:\B\2026-11-21_a", @"R:\B\2026-11-22",
            @"R:\B\notes", @"R:\B\2026-11-19-manual",
        };
        Assert.That(BackupFolders.ResolveStale(folders, 2), Is.EquivalentTo(new[] { @"R:\B\2026-11-20" }));
        Assert.That(BackupFolders.ResolveStale(folders, 0), Has.Count.EqualTo(4));
    }

    [Test]
    public void Sqlite_paths_args_and_targets()
    {
        Assert.Multiple(() =>
        {
            Assert.That(SqliteBackup.ResolveBackupFilePath(@"R:\B\d", "A:B", @"C:\x\app.db"),
                Is.EqualTo(@"R:\B\d\Databases\SQLite\A_B\app.sql"));
            Assert.That(SqliteBackup.BuildArguments(@"C:\x\app.db"), Is.EqualTo(new[] { "-readonly", @"C:\x\app.db", ".dump" }));
            Assert.That(SqliteBackup.ComputeOk(0, false), Is.True);
            Assert.That(SqliteBackup.ComputeOk(0, true), Is.False);
            Assert.That(SqliteBackup.ComputeOk(1, false), Is.False);
        });

        var targets = SqliteBackup.CollectTargets(
        [
            ("One", @"D:\P\One", new[] { "data\\one.db", " ", @"R:\shared.db" }),
            ("Two", @"D:\P\Two", new[] { @"R:\shared.db" }),
            ("Three", @"D:\P\Three", null),
        ]);
        Assert.That(targets, Is.EqualTo(new[]
        {
            new SqliteBackupTarget("One", @"D:\P\One\data\one.db"),
            new SqliteBackupTarget("One", @"R:\shared.db"),
        }));
    }

    [Test]
    public void Sqlite_failure_leaves_no_partial_file_and_a_missing_db_never_runs_sqlite3()
    {
        var db = Path.Combine(temp, "app.db");
        File.WriteAllText(db, "x");
        var failing = new SqliteBackup("sqlite3", (_, _) => (1, "Error: malformed"));
        var r = failing.BackupOne(new SqliteBackupTarget("P", db), temp);
        Assert.That(r.Ok, Is.False);
        Assert.That(File.Exists(r.BackupFile), Is.False);
        Assert.That(r.Output, Does.Contain("malformed"));

        var invoked = false;
        var watching = new SqliteBackup("sqlite3", (_, _) => { invoked = true; return (0, ""); });
        var missing = watching.BackupOne(new SqliteBackupTarget("P", Path.Combine(temp, "nope.db")), temp);
        Assert.That(missing.Ok, Is.False);
        Assert.That(invoked, Is.False);
    }

    [Test]
    public void Run_produces_a_dated_folder_with_numbered_dumps_and_an_appdata_zip()
    {
        var a = Path.Combine(temp, "src", "a", "app.db");
        var b = Path.Combine(temp, "src", "b", "app.sqlite");
        Directory.CreateDirectory(Path.GetDirectoryName(a)!);
        Directory.CreateDirectory(Path.GetDirectoryName(b)!);
        File.WriteAllText(a, "a");
        File.WriteAllText(b, "b");
        var appDataSource = Path.Combine(temp, "appdata");
        Directory.CreateDirectory(Path.Combine(appDataSource, "Vault"));
        File.WriteAllText(Path.Combine(appDataSource, "Vault", "settings.json"), "{}");

        var service = new BackupService(
            Path.Combine(temp, "backups"),
            new SqliteBackup("sqlite3", (args, _) => (0, "dump of " + args[1])),
            new AppDataBackup(appDataSource),
            () => new DateTime(2026, 11, 22));

        var run = service.Run([new SqliteBackupTarget("P", a), new SqliteBackupTarget("P", b)]);

        Assert.Multiple(() =>
        {
            Assert.That(run.Ok, Is.True);
            Assert.That(run.Folder, Is.EqualTo(Path.Combine(temp, "backups", "2026-11-22")));
            Assert.That(run.Sqlite.Select(s => Path.GetFileName(s.BackupFile)), Is.EqualTo(new[] { "app.sql", "app-2.sql" }));
            Assert.That(File.ReadAllText(run.Sqlite[1].BackupFile), Is.EqualTo("dump of " + b));
            Assert.That(File.Exists(Path.Combine(run.Folder, AppDataBackup.ZipFileName)), Is.True);
        });
        using (var zip = ZipFile.OpenRead(run.AppData.ZipPath))
            Assert.That(zip.Entries.Select(e => e.FullName.Replace('\\', '/')), Does.Contain("Vault/settings.json"));

        // A second run the same day goes to _a rather than overwriting the first.
        Assert.That(service.Run([]).Folder, Is.EqualTo(Path.Combine(temp, "backups", "2026-11-22_a")));
    }

    [Test]
    public void A_missing_appdata_tree_is_skipped_not_failed_and_restore_round_trips()
    {
        var skipped = new AppDataBackup(Path.Combine(temp, "absent")).Zip(temp);
        Assert.That(skipped.Ok, Is.True);
        Assert.That(skipped.Output, Does.Contain("skipped"));

        var src = Path.Combine(temp, "src");
        Directory.CreateDirectory(src);
        File.WriteAllText(Path.Combine(src, "k.json"), "1");
        var zipped = new AppDataBackup(src).Zip(Path.Combine(temp, "out"));
        var dest = Path.Combine(temp, "restored");
        var restored = new AppDataBackup(src).Restore(zipped.ZipPath, dest);
        Assert.That(restored.Ok, Is.True);
        Assert.That(File.ReadAllText(Path.Combine(dest, "k.json")), Is.EqualTo("1"));
    }

    [Test]
    public void Prune_deletes_only_dated_folders_past_the_newest_keep()
    {
        var root = Path.Combine(temp, "backups");
        foreach (var d in new[] { "2026-11-20", "2026-11-21", "2026-11-21_a", "2026-11-22", "keep-me" })
            Directory.CreateDirectory(Path.Combine(root, d));

        var removed = new BackupService(root).Prune(1);

        Assert.That(removed.Select(Path.GetFileName), Is.EquivalentTo(new[] { "2026-11-20", "2026-11-21", "2026-11-21_a" }));
        Assert.That(Directory.GetDirectories(root).Select(Path.GetFileName), Is.EquivalentTo(new[] { "2026-11-22", "keep-me" }));
    }
}
