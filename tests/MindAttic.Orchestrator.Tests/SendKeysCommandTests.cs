using MindAttic.Orchestrator.Billing;
using MindAttic.Orchestrator.Sessions;
using MindAttic.Orchestrator.Term;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary><c>orchestrator clis send-keys</c> (M.ORCH-T2262): resolving TARGET to one open, hosted CLI, and the frames sent
/// over its TermHost pipe.</summary>
public sealed class SendKeysCommandTests
{
    private static CliRow Row(int pid, string repo) => new(pid, "s-" + pid, repo, @"D:\Repo", BillingModes.Team, true, true, ["*"], false, null);

    // ── Resolving TARGET to a roster row ────────────────────────────────

    [Test]
    public void A_pid_or_an_exact_repo_resolves_to_its_one_cli()
    {
        var clis = new[] { Row(100, "MindAttic.Orchestrator"), Row(200, "Prose") };
        Assert.That(SendKeysTarget.ResolveCli(clis, "200").Row, Is.EqualTo(clis[1]));
        Assert.That(SendKeysTarget.ResolveCli(clis, "prose").Row, Is.EqualTo(clis[1]), "case-insensitive");
    }

    [Test]
    public void An_empty_target_a_miss_or_several_matches_is_an_error_not_a_guess()
    {
        var clis = new[] { Row(100, "MindAttic.Orchestrator"), Row(200, "MindAttic.Orchestrator") };
        Assert.That(SendKeysTarget.ResolveCli(clis, "  ").Error, Does.Contain("Name a target"));
        Assert.That(SendKeysTarget.ResolveCli(clis, "999").Error, Does.Contain("No open CLI"));
        Assert.That(SendKeysTarget.ResolveCli(clis, "MindAttic.Orchestrator").Error, Does.Contain("matches 2"));
    }

    // ── Resolving a roster row to its hosted TermHost session ───────────

    [Test]
    public void A_clis_pid_resolves_to_the_hosted_session_whose_host_started_it()
    {
        // 500 (the TermHost) started 600 (a shell) started 700 (claude.exe), the roster's pid.
        var parents = new Dictionary<int, int> { [700] = 600, [600] = 500 };
        var sessions = new[] { new TermSessionRecord { Id = "abc", HostPid = 500 }, new TermSessionRecord { Id = "other", HostPid = 999 } };

        var (session, error) = SendKeysTarget.ResolveSession(Row(700, "Prose"), sessions, parents);

        Assert.That(error, Is.Null);
        Assert.That(session!.Id, Is.EqualTo("abc"));
    }

    [Test]
    public void A_pid_no_hosted_session_claims_is_an_error()
    {
        var (session, error) = SendKeysTarget.ResolveSession(Row(700, "Prose"), [new TermSessionRecord { Id = "abc", HostPid = 1 }],
            new Dictionary<int, int>());

        Assert.That(session, Is.Null);
        Assert.That(error, Does.Contain("isn't running under a TermHost"));
    }
}
