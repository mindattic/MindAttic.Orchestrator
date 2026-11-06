using MindAttic.Orchestrator.Settings;
using MindAttic.Orchestrator.Setup;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>M.ORCH-T1473: the machine role is per machine, defaults to standalone, and the environment wins over the file.</summary>
public sealed class MachineRoleTests
{
    private string dir = "";

    [SetUp]
    public void Make() => dir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "orch-role-" + Guid.NewGuid().ToString("N"))).FullName;

    [TearDown]
    public void Clean()
    {
        try { Directory.Delete(dir, recursive: true); } catch (IOException) { }
    }

    [Test]
    public void No_file_is_standalone()
    {
        var role = MachineRole.Load(Path.Combine(dir, "machine.json"), null);
        Assert.That(role.Role, Is.EqualTo(MachineRoleKind.Standalone));
        Assert.That(role.IsClient || role.IsHost, Is.False);
    }

    [Test]
    public void A_saved_client_reads_back_with_its_host()
    {
        var path = Path.Combine(dir, "machine.json");
        new MachineRole { Role = MachineRoleKind.Client, Host = "mindattic-vm", GatewayUrl = "https://mindattic-vm:7443" }.Save(path);
        var role = MachineRole.Load(path, null);
        Assert.That(role.IsClient, Is.True);
        Assert.That(role.Host, Is.EqualTo("mindattic-vm"));
        Assert.That(role.GatewayUrl, Is.EqualTo("https://mindattic-vm:7443"));
        Assert.That(role.ChangedUtc, Is.Not.Null);
        Assert.That(File.ReadAllText(path), Does.Contain("\"Client\""));
    }

    [Test]
    public void The_environment_overrides_the_file()
    {
        var path = Path.Combine(dir, "machine.json");
        new MachineRole { Role = MachineRoleKind.Client, Host = "vm" }.Save(path);
        Assert.That(MachineRole.Load(path, "host").Role, Is.EqualTo(MachineRoleKind.Host));
        Assert.That(MachineRole.Load(path, "nonsense").Role, Is.EqualTo(MachineRoleKind.Client));
    }

    [Test]
    public void An_unreadable_file_is_standalone()
    {
        var path = Path.Combine(dir, "machine.json");
        File.WriteAllText(path, "{ not json");
        Assert.That(MachineRole.Load(path, null).Role, Is.EqualTo(MachineRoleKind.Standalone));
    }

    [TestCase("HOST", MachineRoleKind.Host)]
    [TestCase("vm", MachineRoleKind.Host)]
    [TestCase("Client", MachineRoleKind.Client)]
    [TestCase("pc", MachineRoleKind.Client)]
    [TestCase("standalone", MachineRoleKind.Standalone)]
    public void Parses_role_names(string text, MachineRoleKind expected) => Assert.That(MachineRole.Parse(text), Is.EqualTo(expected));

    [Test]
    public void A_client_owner_key_is_sealed_and_comes_back_only_here()
    {
        var remote = new GatewayRemote { Name = MachineRole.HostRemoteName, Url = "https://vm:7443", SealedOwnerKey = GatewayRemotes.Seal("owner-key-text") };
        Assert.That(remote.SealedOwnerKey, Does.Not.Contain("owner-key-text"));
        Assert.That(GatewayRemotes.OwnerKey(remote), Is.EqualTo("owner-key-text"));
        Assert.That(GatewayRemotes.Token(remote), Is.Null);
        using var client = new Sessions.GatewayClient(remote);
        Assert.That(client.HasToken, Is.True, "an owner key mints tokens, so the remote counts as having one");
    }
}
