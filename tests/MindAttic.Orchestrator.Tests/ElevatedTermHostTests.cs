using System.Diagnostics;
using System.IO.Pipes;
using System.Security.Principal;
using MindAttic.Orchestrator.Sessions;
using MindAttic.Orchestrator.Term;
using MindAttic.Orchestrator.TermHost;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>
/// An elevated (Administrator) CLI tab (M.ORCH-T1656): the <c>runas</c> <c>--launch</c> respawn and the elevated host's pipe
/// security. Nothing here elevates or raises a UAC prompt: the start goes through fakes, and the one real pipe is a throwaway
/// one this (unelevated) test process creates for itself.
/// </summary>
public sealed class ElevatedTermHostTests
{
    private static readonly SecurityIdentifier SomeUser = new("S-1-5-21-1111111111-2222222222-3333333333-1001");

    // ── The pipe's security ─────────────────────────────────────────────

    [Test]
    public void The_elevated_pipe_is_owned_by_and_open_only_to_the_user_with_a_Medium_label()
    {
        var sddl = ElevatedPipeSecurity.Sddl(SomeUser);
        var sid = SomeUser.Value;

        Assert.That(sddl, Does.StartWith($"O:{sid}G:{sid}"), "owned by the user, not BUILTIN\\Administrators");
        Assert.That(sddl, Does.Contain($"D:P(A;;GA;;;{sid})"), "a protected DACL with the user's ACE alone");
        Assert.That(sddl, Does.Contain("S:(ML;;NW;;;ME)"), "Medium no-write-up, not Low");
        foreach (var other in new[] { ";;;BA)", ";;;SY)", ";;;WD)", ";;;AU)", ";;;IU)", ";;;LW)" })
            Assert.That(sddl, Does.Not.Contain(other), $"no ACE or label for {other}");
    }

    [Test]
    public void The_user_is_the_token_user_never_the_token_owner()
    {
        using var identity = WindowsIdentity.GetCurrent();
        Assert.That(ElevatedPipeSecurity.CurrentUser(), Is.EqualTo(identity.User));
    }

    [Test]
    public void An_unelevated_host_keeps_todays_pipe_and_an_elevated_one_gets_the_restricted_pipe()
    {
        Assert.That(HostPipes.For(elevated: false), Is.InstanceOf<CurrentUserPipeFactory>());
        Assert.That(HostPipes.For(elevated: true), Is.InstanceOf<ElevatedPipeFactory>());
    }

    [Test]
    public void A_pipe_made_by_the_elevated_factory_carries_that_security_and_takes_a_current_user_client()
    {
        // A throwaway pipe of this test's own, at this process's own (Medium) integrity: labeling it Medium needs no privilege.
        var name = "mindattic-test-elevated-" + Guid.NewGuid().ToString("N");
        var user = ElevatedPipeSecurity.CurrentUser();
        using var server = new ElevatedPipeFactory(() => user).Create(name, firstInstance: true);

        var sddl = ElevatedPipeSecurity.ReadSddl(server.SafePipeHandle);
        Assert.That(sddl, Does.StartWith($"O:{user.Value}"));
        Assert.That(sddl, Does.Contain($"(A;;FA;;;{user.Value})").Or.Contain($"(A;;GA;;;{user.Value})"));
        Assert.That(sddl, Does.Contain("ML;;NW;;;ME"));

        // The viewer's own CurrentUserOnly check (pipe owner == its token owner) passes.
        var accept = server.WaitForConnectionAsync();
        using var client = new NamedPipeClientStream(".", name, PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        client.Connect(5000);
        Assert.That(accept.Wait(5000), Is.True);
        Assert.That(server.IsConnected, Is.True);
    }

    [Test]
    public void A_second_first_instance_of_a_taken_name_is_refused()
    {
        var name = "mindattic-test-elevated-" + Guid.NewGuid().ToString("N");
        var factory = new ElevatedPipeFactory(ElevatedPipeSecurity.CurrentUser);
        using var first = factory.Create(name, firstInstance: true);
        Assert.That(() => factory.Create(name, firstInstance: true),
            Throws.InstanceOf<UnauthorizedAccessException>().Or.InstanceOf<IOException>());
    }

    // ── The runas --launch respawn ──────────────────────────────────────

    private static TermLaunchSpec Spec() => new()
    {
        Id = "elev123", CommandLine = "powershell.exe -NoLogo", Directory = @"C:\Work", Title = "Admin: PowerShell", Elevated = true,
    };

    [Test]
    public void The_elevated_start_is_a_runas_shell_execute_of_the_launch_respawn()
    {
        var psi = TermLauncher.ElevatedStartInfo(@"C:\Hosts\MindAttic.Orchestrator.TermHost.exe", Spec(), "elev123");

        Assert.That(psi.UseShellExecute, Is.True);
        Assert.That(psi.Verb, Is.EqualTo("runas"));
        Assert.That(psi.WorkingDirectory, Is.EqualTo(@"C:\Hosts"), "the host's folder, never the CLI's");
        Assert.That(psi.ArgumentList, Is.EqualTo(TermLauncher.Arguments(Spec(), "elev123")), "the same arguments as an unelevated launch");
        Assert.That(psi.ArgumentList[0], Is.EqualTo("--launch"));
    }

    [Test]
    public void StartElevated_returns_the_record_once_the_host_registers()
    {
        ProcessStartInfo? started = null;
        var lookups = 0;
        var record = TermLauncher.StartElevated(Spec(), TimeSpan.FromSeconds(10), () => @"C:\Hosts\host.exe", psi => started = psi,
            id => ++lookups < 3 ? null : new TermSessionRecord { Id = id, Pipe = TermProtocol.PipeName(id) },
            () => DateTime.UtcNow, _ => { });

        Assert.That(started, Is.Not.Null);
        Assert.That(started!.Verb, Is.EqualTo("runas"));
        Assert.That(record.Id, Is.EqualTo("elev123"));
    }

    [Test]
    public void A_declined_UAC_prompt_is_reported_as_cancelled()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => TermLauncher.StartElevated(Spec(), null, () => @"C:\Hosts\host.exe",
            _ => throw new System.ComponentModel.Win32Exception(TermLauncher.ErrorCancelled), _ => null, () => DateTime.UtcNow, _ => { }));
        Assert.That(ex!.Message, Does.Contain("cancelled"));
    }

    [Test]
    public void A_host_that_never_registers_times_out_on_the_given_wait()
    {
        var clock = new DateTime(2026, 11, 16, 12, 0, 0, DateTimeKind.Utc);
        var ex = Assert.Throws<InvalidOperationException>(() => TermLauncher.StartElevated(Spec(), TimeSpan.FromSeconds(30),
            () => @"C:\Hosts\host.exe", _ => { }, _ => null, () => clock, ms => clock = clock.AddMilliseconds(ms * 100)));
        Assert.That(ex!.Message, Does.Contain("didn't register within 30s"));
    }

    [Test]
    public void An_elevated_start_refuses_environment_variables_and_a_missing_host()
    {
        var withEnv = Spec() with { Environment = new Dictionary<string, string?> { ["TOKEN"] = "x" } };
        Assert.Throws<NotSupportedException>(() => TermLauncher.StartElevated(withEnv, null, () => @"C:\Hosts\host.exe",
            _ => Assert.Fail("must not start"), _ => null, () => DateTime.UtcNow, _ => { }));
        Assert.Throws<InvalidOperationException>(() => TermLauncher.StartElevated(Spec(), null, () => null,
            _ => Assert.Fail("must not start"), _ => null, () => DateTime.UtcNow, _ => { }));
    }
}
