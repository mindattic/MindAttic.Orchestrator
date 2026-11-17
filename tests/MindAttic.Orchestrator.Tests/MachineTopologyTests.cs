using System.Reflection;
using MindAttic.Orchestrator.Settings;
using MindAttic.Orchestrator.Setup;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>
/// Hub phase 0 (M.ORCH-T1639): the one topology read point over <see cref="MachineRole"/>. These pin a standalone install's
/// behaviour so later hub phases can't change a single-machine install by accident, and pin host and client to today's
/// docs/REMOTE_HOST.md behaviour.
/// </summary>
public sealed class MachineTopologyTests
{
    private static readonly MachineTopology Standalone = MachineTopology.For(MachineRoleKind.Standalone);
    private static readonly MachineTopology Host = MachineTopology.For(MachineRoleKind.Host);
    private static readonly MachineTopology Client = MachineTopology.For(MachineRoleKind.Client);

    [Test]
    public void No_machine_file_is_a_standalone_topology()
    {
        var dir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "orch-topology-" + Guid.NewGuid().ToString("N"))).FullName;
        try
        {
            var topology = MachineTopology.From(MachineRole.Load(Path.Combine(dir, "machine.json"), null));
            Assert.That(topology.Kind, Is.EqualTo(TopologyKind.Standalone));
            Assert.That(topology.IsStandalone, Is.True);
        }
        finally { try { Directory.Delete(dir, recursive: true); } catch (IOException) { } }
    }

    [TestCase(MachineRoleKind.Standalone, TopologyKind.Standalone)]
    [TestCase(MachineRoleKind.Host, TopologyKind.Host)]
    [TestCase(MachineRoleKind.Client, TopologyKind.Client)]
    public void Each_role_maps_to_its_topology(MachineRoleKind role, TopologyKind expected) =>
        Assert.That(MachineTopology.For(role).Kind, Is.EqualTo(expected));

    [Test]
    public void No_role_selects_the_reserved_topologies()
    {
        var selected = Enum.GetValues<MachineRoleKind>().Select(r => MachineTopology.For(r).Kind).ToList();
        Assert.That(selected, Does.Not.Contain(TopologyKind.MemberClient));
        Assert.That(selected, Does.Not.Contain(TopologyKind.Spoke));
    }

    // ---- standalone, pinned ----

    [Test]
    public void Standalone_runs_the_daemon_with_its_pace_hooks()
    {
        Assert.That(Standalone.RunsDaemon, Is.True);
        Assert.That(Standalone.InstallsPaceHooks, Is.True);
        Assert.That(Standalone.ShowsWelcome, Is.True);
        Assert.That(Standalone.OpensHostTeamWall, Is.False);
    }

    [Test]
    public void Standalone_keeps_its_data_local_and_synced_to_git()
    {
        Assert.That(Standalone.UsesCheckoutDatabase, Is.True, "the checkout's data/orchestrator.db");
        Assert.That(Standalone.SyncsDatabasesToGit, Is.True);
        Assert.That(Standalone.UsesSqlServer, Is.True, "a configured SQL Server stays the store of record");
        Assert.That(Standalone.UsesLocalTicketsDb, Is.True, "the checkout's Tickets.db");
        Assert.That(Standalone.UsesTicketsShim, Is.False);
    }

    [Test]
    public void Standalone_with_default_settings_listens_on_no_network_and_builds_no_branches()
    {
        var defaults = new OrchestratorSettings();
        Assert.That(Standalone.GatewayListensOnNetwork(defaults.Gateway), Is.False);
        Assert.That(Standalone.RunsBranchBuilds(defaults.RemoteBranchBuilds), Is.False);
        Assert.That(defaults.Gateway.Enabled, Is.False, "the gateway is off by default");
        Assert.That(defaults.Gateway.Bind, Is.EqualTo("127.0.0.1"));
        Assert.That(defaults.RemoteBranchBuilds, Is.False);
    }

    [TestCase("127.0.0.1")]
    [TestCase("localhost")]
    [TestCase("::1")]
    public void A_loopback_gateway_is_not_on_the_network(string bind) =>
        Assert.That(Standalone.GatewayListensOnNetwork(new GatewaySettings { Enabled = true, Bind = bind }), Is.False);

    [Test]
    public void The_topology_owns_no_process_or_socket()
    {
        // A plain value over the role: reading it starts nothing and holds nothing open.
        var fields = typeof(MachineTopology).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.That(fields.Select(f => f.FieldType), Is.All.EqualTo(typeof(TopologyKind)));
        Assert.That(typeof(IDisposable).IsAssignableFrom(typeof(MachineTopology)), Is.False);
        Assert.That(MachineTopology.For(MachineRoleKind.Standalone), Is.EqualTo(Standalone));
    }

    // ---- host and client: today's REMOTE_HOST.md behaviour ----

    [Test]
    public void Host_runs_everything_a_standalone_machine_does()
    {
        Assert.That(Host.RunsDaemon && Host.InstallsPaceHooks && Host.SyncsDatabasesToGit, Is.True);
        Assert.That(Host.UsesCheckoutDatabase && Host.UsesSqlServer && Host.UsesLocalTicketsDb, Is.True);
        Assert.That(Host.UsesTicketsShim || Host.OpensHostTeamWall, Is.False);
        Assert.That(Host.GatewayListensOnNetwork(new GatewaySettings { Enabled = true, Bind = "0.0.0.0" }), Is.True);
        Assert.That(Host.RunsBranchBuilds(remoteBranchBuildsSetting: true), Is.True);
    }

    [Test]
    public void Client_runs_nothing_and_watches_its_host()
    {
        Assert.That(Client.IsClient, Is.True);
        Assert.That(Client.RunsDaemon || Client.InstallsPaceHooks || Client.SyncsDatabasesToGit, Is.False);
        Assert.That(Client.UsesCheckoutDatabase || Client.UsesSqlServer || Client.UsesLocalTicketsDb, Is.False);
        Assert.That(Client.UsesTicketsShim && Client.OpensHostTeamWall, Is.True);
        Assert.That(Client.ShowsWelcome, Is.False);
        Assert.That(Client.GatewayListensOnNetwork(new GatewaySettings { Enabled = true, Bind = "0.0.0.0" }), Is.False);
        Assert.That(Client.RunsBranchBuilds(remoteBranchBuildsSetting: true), Is.False);
    }
}
