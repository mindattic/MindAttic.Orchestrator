using MindAttic.Orchestrator.Term;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>Side-by-side TermHost installs (M.ORCH-T932): the newest-version resolver, the cleanup selection and the protocol check.</summary>
public sealed class TermHostInstallsTests
{
    private string root = "";

    [SetUp]
    public void SetUp() => root = Directory.CreateTempSubdirectory("termhost-installs-").FullName;

    [TearDown]
    public void TearDown() { try { Directory.Delete(root, true); } catch (IOException) { } }

    private string Install(string name, bool withExe = true)
    {
        var dir = Path.Combine(root, name);
        Directory.CreateDirectory(dir);
        if (withExe) File.WriteAllText(Path.Combine(dir, TermHostInstalls.ExeName), "");
        return dir;
    }

    [Test]
    public void The_newest_is_the_highest_version_then_the_latest_build_stamp()
    {
        Install("1.0.0-20261001000000");
        Install("1.0.0-20261026153000");
        Install("2.0.0-20250101000000");
        Install("2.0.0");
        Assert.That(TermHostInstalls.Newest(root)!.Name, Is.EqualTo("2.0.0-20250101000000"));
    }

    [Test]
    public void Version_numbers_compare_as_numbers_not_text()
    {
        Install("2.0.0");
        Install("10.0.0");
        Assert.That(TermHostInstalls.Newest(root)!.Name, Is.EqualTo("10.0.0"));
    }

    [Test]
    public void Folders_without_an_exe_or_a_version_name_are_ignored()
    {
        Install("1.0.0-20261001000000");
        Install("9.0.0", withExe: false);
        Install(".staging-abc123");
        Install("notes");
        Install("3.0.0-123");
        Assert.That(TermHostInstalls.List(root).Select(v => v.Name), Is.EqualTo(new[] { "1.0.0-20261001000000" }));
    }

    [Test]
    public void Nothing_installed_resolves_to_null()
    {
        Assert.That(TermHostInstalls.Newest(Path.Combine(root, "missing")), Is.Null);
        Assert.That(TermHostInstalls.Newest(root), Is.Null);
    }

    [Test]
    public void ResolveExe_reads_the_TermHost_folder_under_the_install_dir()
    {
        var termHost = Directory.CreateDirectory(Path.Combine(root, "TermHost")).FullName;
        var v1 = Directory.CreateDirectory(Path.Combine(termHost, "1.0.0-20261001000000")).FullName;
        var v2 = Directory.CreateDirectory(Path.Combine(termHost, "1.0.0-20261002000000")).FullName;
        File.WriteAllText(Path.Combine(v1, TermHostInstalls.ExeName), "");
        File.WriteAllText(Path.Combine(v2, TermHostInstalls.ExeName), "");
        Assert.That(TermHostInstalls.ResolveExe(root), Is.EqualTo(Path.Combine(v2, TermHostInstalls.ExeName)));
    }

    private static TermHostInstalls.Installed V(string name) => TermHostInstalls.Parse(name, @"C:\Apps\X\TermHost\" + name)!;

    [Test]
    public void Cleanup_selects_every_older_version_nothing_runs_from_and_never_the_newest()
    {
        var all = new[] { V("1.0.0-20261001000000"), V("1.0.0-20261002000000"), V("1.0.0-20261003000000") };
        var removable = TermHostInstalls.SelectRemovable(all, []);
        Assert.That(removable.Select(v => v.Name), Is.EqualTo(new[] { "1.0.0-20261001000000", "1.0.0-20261002000000" }));
    }

    [Test]
    public void Cleanup_keeps_a_version_a_process_runs_from()
    {
        var all = new[] { V("1.0.0-20261001000000"), V("1.0.0-20261002000000"), V("1.0.0-20261003000000") };
        var running = new[] { @"c:\apps\x\termhost\1.0.0-20261001000000\MindAttic.Orchestrator.TermHost.exe" };
        Assert.That(TermHostInstalls.SelectRemovable(all, running).Select(v => v.Name), Is.EqualTo(new[] { "1.0.0-20261002000000" }));
    }

    [Test]
    public void A_running_path_only_counts_inside_its_own_folder()
    {
        // 1.0.0 is a name prefix of 1.0.0-2026...; the folder boundary must be respected.
        var all = new[] { V("1.0.0"), V("1.0.0-20261002000000"), V("2.0.0") };
        var running = new[] { @"C:\Apps\X\TermHost\1.0.0-20261002000000\MindAttic.Orchestrator.TermHost.exe" };
        Assert.That(TermHostInstalls.SelectRemovable(all, running).Select(v => v.Name), Is.EqualTo(new[] { "1.0.0" }));
    }

    [Test]
    public void One_installed_version_is_never_removed()
    {
        Assert.That(TermHostInstalls.SelectRemovable([V("1.0.0")], []), Is.Empty);
        Assert.That(TermHostInstalls.SelectRemovable([], []), Is.Empty);
    }

    [Test]
    public void Cleanup_deletes_the_selected_folders_only_and_does_nothing_when_a_process_cant_be_read()
    {
        var old = Install("1.0.0-20261001000000");
        var used = Install("1.0.0-20261002000000");
        var newest = Install("1.0.0-20261003000000");

        Assert.That(TermHostInstalls.Cleanup(root, null), Is.Empty);
        Assert.That(Directory.Exists(old), Is.True);

        var deleted = TermHostInstalls.Cleanup(root, [Path.Combine(used, TermHostInstalls.ExeName)]);
        Assert.That(deleted, Is.EqualTo(new[] { "1.0.0-20261001000000" }));
        Assert.That(Directory.Exists(old), Is.False);
        Assert.That(Directory.Exists(used), Is.True);
        Assert.That(Directory.Exists(newest), Is.True);
    }

    [Test]
    public void A_folder_name_round_trips_through_Parse()
    {
        var name = TermHostInstalls.VersionFolderName(new Version(1, 2, 3, 4), new DateTime(2026, 10, 26, 15, 30, 0, DateTimeKind.Utc));
        Assert.That(name, Is.EqualTo("1.2.3-20261026153000"));
        Assert.That(TermHostInstalls.Parse(name, "x")!.Version, Is.EqualTo(new Version(1, 2, 3)));
    }

    [Test]
    public void The_protocol_check_accepts_supported_versions_and_explains_the_rest_without_throwing()
    {
        Assert.That(TermProtocol.Check(TermProtocol.Version).Ok, Is.True);
        var future = TermProtocol.Check((ushort)(TermProtocol.Supported.Max() + 1));
        Assert.That(future.Status, Is.EqualTo(TermCompatStatus.HostTooNew));
        Assert.That(future.Message, Does.Contain("newer"));
        Assert.That(TermProtocol.Check(ushort.MaxValue).Status, Is.EqualTo(TermCompatStatus.HostTooNew));
        Assert.That(TermProtocol.Check(0).Status, Is.EqualTo(TermCompatStatus.HostTooOld));
    }
}
