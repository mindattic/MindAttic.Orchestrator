using System.ComponentModel;
using System.Diagnostics;
using MindAttic.Orchestrator.Setup;
using MindAttic.Orchestrator.Term;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>
/// M.ORCH-T1691: what is meant to outlive its caller (the dashboard, the daemon, the gateway, viewers, Tickets Desktop) starts
/// outside the caller's job through <see cref="Breakaway"/>, and a script reaches it through <c>orchestrator spawn</c>. No test
/// here starts a process: the <c>CreateProcess</c> attempt and the verb's start are both seams.
/// </summary>
[TestFixture]
public sealed class BreakawayTests
{
    [Test]
    public void The_first_attempt_asks_to_break_away_and_a_success_says_it_did()
    {
        var flags = new List<uint>();
        var started = Breakaway.Start(BreakawayConsole.Inherit, f => { flags.Add(f); return (4242, 0); });
        Assert.That(started, Is.EqualTo(new BreakawayStart(4242, BrokeAway: true)));
        Assert.That(flags, Has.Count.EqualTo(1));
        Assert.That(flags[0] & Breakaway.CREATE_BREAKAWAY_FROM_JOB, Is.Not.Zero);
    }

    [Test]
    public void A_job_that_forbids_breakaway_gets_an_ordinary_start_instead()
    {
        var flags = new List<uint>();
        var started = Breakaway.Start(BreakawayConsole.NoWindow, f =>
        {
            flags.Add(f);
            return (f & Breakaway.CREATE_BREAKAWAY_FROM_JOB) != 0 ? (0, Breakaway.ERROR_ACCESS_DENIED) : (77, 0);
        });
        Assert.That(started, Is.EqualTo(new BreakawayStart(77, BrokeAway: false)));
        Assert.That(flags, Has.Count.EqualTo(2));
        Assert.That(flags[1] & Breakaway.CREATE_BREAKAWAY_FROM_JOB, Is.Zero, "the retry stays inside the job");
        Assert.That(flags[1] & Breakaway.CREATE_NO_WINDOW, Is.Not.Zero, "and keeps the rest of what was asked");
    }

    [Test]
    public void Any_other_failure_throws_without_a_second_attempt()
    {
        var attempts = 0;
        var ex = Assert.Throws<Win32Exception>(() => Breakaway.Start(BreakawayConsole.Inherit, _ => { attempts++; return (0, 2); }));
        Assert.That(ex!.NativeErrorCode, Is.EqualTo(2), "file not found");
        Assert.That(attempts, Is.EqualTo(1));
    }

    [Test]
    public void A_refused_ordinary_start_throws_its_own_error()
    {
        var ex = Assert.Throws<Win32Exception>(() => Breakaway.Start(BreakawayConsole.Inherit,
            f => (0, (f & Breakaway.CREATE_BREAKAWAY_FROM_JOB) != 0 ? Breakaway.ERROR_ACCESS_DENIED : 740)));
        Assert.That(ex!.NativeErrorCode, Is.EqualTo(740));
    }

    [Test]
    public void The_console_decides_the_other_creation_flags()
    {
        Assert.That(Breakaway.CreationFlags(BreakawayConsole.Inherit, breakaway: true),
            Is.EqualTo(Breakaway.CREATE_UNICODE_ENVIRONMENT | Breakaway.CREATE_BREAKAWAY_FROM_JOB));
        Assert.That(Breakaway.CreationFlags(BreakawayConsole.NoWindow, breakaway: false),
            Is.EqualTo(Breakaway.CREATE_UNICODE_ENVIRONMENT | Breakaway.CREATE_NO_WINDOW));
        // A detached TermHost's flags, as TermHost's own SpawnDetached had them before it came here.
        Assert.That(Breakaway.CreationFlags(BreakawayConsole.Detached, breakaway: true),
            Is.EqualTo(0x00000008u | 0x00000200u | 0x00000400u | 0x01000000u));
    }

    [Test]
    public void A_start_info_becomes_the_command_line_process_start_would_build()
    {
        var psi = new ProcessStartInfo(@"C:\Apps\Mind Attic\Dashboard.exe") { UseShellExecute = false };
        psi.ArgumentList.Add("--start");
        psi.ArgumentList.Add("My Repo");
        psi.ArgumentList.Add(@"C:\with space\");
        Assert.That(Breakaway.CommandLine(psi), Is.EqualTo(@"""C:\Apps\Mind Attic\Dashboard.exe"" --start ""My Repo"" ""C:\with space\\"""));

        Assert.That(Breakaway.CommandLine(new ProcessStartInfo("wt.exe", "-w 0 nt") { UseShellExecute = false }), Is.EqualTo(@"""wt.exe"" -w 0 nt"));
        Assert.That(Breakaway.CommandLine(new ProcessStartInfo("wt.exe") { UseShellExecute = false }), Is.EqualTo(@"""wt.exe"""));
    }

    [Test]
    public void Quoting_reads_back_as_the_argument()
    {
        Assert.That(Breakaway.Quote("plain"), Is.EqualTo("plain"));
        Assert.That(Breakaway.Quote(""), Is.EqualTo("\"\""));
        Assert.That(Breakaway.Quote("a b"), Is.EqualTo("\"a b\""));
        Assert.That(Breakaway.Quote("say \"hi\""), Is.EqualTo("\"say \\\"hi\\\"\""));
        Assert.That(Breakaway.Quote(@"C:\dir name\"), Is.EqualTo(@"""C:\dir name\\"""));
    }

    [Test]
    public void A_shell_start_or_redirected_streams_are_refused_before_anything_starts()
    {
        Assert.Throws<NotSupportedException>(() => Breakaway.Start(new ProcessStartInfo("notepad.exe") { UseShellExecute = true })); // window-hygiene: safe, refused before anything starts
        Assert.Throws<NotSupportedException>(() => Breakaway.Start(new ProcessStartInfo("cmd.exe") { UseShellExecute = false, RedirectStandardOutput = true }));
    }

    [Test]
    public void The_environment_block_is_sorted_null_terminated_and_leaves_out_removed_variables()
    {
        var block = new string(Breakaway.EnvironmentBlock(new Dictionary<string, string?>
        {
            ["zeta"] = "1",
            ["Alpha"] = "two words",
            ["GONE"] = null,
        }));
        Assert.That(block, Is.EqualTo("Alpha=two words\0zeta=1\0\0"));
        Assert.That(new string(Breakaway.EnvironmentBlock([])), Is.EqualTo("\0\0"));
    }

    [Test]
    public void The_daemon_s_start_info_is_one_the_breakaway_start_takes()
    {
        // Installer.StartDaemon hands this to Breakaway.Start: it must stay a plain start (no shell, no redirected streams).
        var psi = Installer.DaemonStartInfo(@"C:\Apps\MindAttic\MindAttic.Orchestrator.Desktop.exe");
        Assert.That(psi.UseShellExecute, Is.False);
        Assert.That(psi.RedirectStandardOutput || psi.RedirectStandardError || psi.RedirectStandardInput, Is.False);
        Assert.That(Breakaway.CommandLine(psi), Is.EqualTo($@"C:\Apps\MindAttic\MindAttic.Orchestrator.Desktop.exe {Installer.DaemonArg}"));
    }

    // ---- orchestrator spawn ----

    [Test]
    public void Spawn_passes_the_program_s_own_arguments_on_untouched()
    {
        var request = SpawnVerb.Parse(["--cwd", @"C:\work", "--no-window", "--", "app.exe", "--cwd", "x", "--", "-v"], out var error);
        Assert.That(error, Is.Null);
        Assert.That(request!.Program, Is.EqualTo("app.exe"));
        Assert.That(request.Arguments, Is.EqualTo(new[] { "--cwd", "x", "--", "-v" }));
        Assert.That(request.WorkingDirectory, Is.EqualTo(@"C:\work"));
        Assert.That(request.NoWindow, Is.True);

        var bare = SpawnVerb.Parse([@"C:\Apps\Dashboard.exe", "--start", "Repo"], out _);
        Assert.That((bare!.Program, bare.WorkingDirectory, bare.NoWindow), Is.EqualTo((@"C:\Apps\Dashboard.exe", (string?)null, false)));
        Assert.That(bare!.Arguments, Is.EqualTo(new[] { "--start", "Repo" }));
    }

    [TestCase(new string[0], "No program")]
    [TestCase(new[] { "--" }, "No program")]
    [TestCase(new[] { "--cwd" }, "--cwd needs")]
    [TestCase(new[] { "--wait", "app.exe" }, "Unknown option --wait")]
    public void Spawn_says_what_is_wrong_with_its_command_line(string[] args, string expected)
    {
        Assert.That(SpawnVerb.Parse(args, out var error), Is.Null);
        Assert.That(error, Does.Contain(expected));
    }

    [Test]
    public void Spawn_runs_only_as_the_first_word_and_reports_how_it_went()
    {
        var output = new StringWriter();
        var errors = new StringWriter();
        SpawnVerb.Request? asked = null;

        Assert.That(SpawnVerb.TryRun(["status"], output, errors, out _, _ => throw new InvalidOperationException("not reached")), Is.False);
        Assert.That(SpawnVerb.TryRun(["send", "spawn"], output, errors, out _, _ => throw new InvalidOperationException("not reached")), Is.False);

        Assert.That(SpawnVerb.TryRun(["spawn", "--", "app.exe", "one"], output, errors, out var exit, r => { asked = r; return new(9, true); }), Is.True);
        Assert.That(exit, Is.Zero);
        Assert.That(asked!.Program, Is.EqualTo("app.exe"));
        Assert.That(output.ToString(), Does.Contain("pid 9"));

        Assert.That(SpawnVerb.TryRun(["spawn", "app.exe"], output, errors, out exit, _ => new(10, false)), Is.True);
        Assert.That(exit, Is.Zero, "started inside the job is still started");
        Assert.That(output.ToString(), Does.Contain("ends with it"));

        Assert.That(SpawnVerb.TryRun(["spawn", "missing.exe"], output, errors, out exit, _ => throw new Win32Exception(2)), Is.True);
        Assert.That(exit, Is.EqualTo(1));
        Assert.That(errors.ToString(), Does.Contain("Couldn't start missing.exe"));

        Assert.That(SpawnVerb.TryRun(["spawn"], output, errors, out exit, _ => throw new InvalidOperationException("not reached")), Is.True);
        Assert.That(exit, Is.EqualTo(2));
        Assert.That(errors.ToString(), Does.Contain(SpawnVerb.Usage));
    }

    [Test]
    public void The_deploy_script_starts_the_dashboard_through_spawn_and_falls_back_to_start_process()
    {
        var script = File.ReadAllText(Path.Combine(RepoRoot(), "run.ps1"));
        var at = script.IndexOf("function Start-Outliving", StringComparison.Ordinal);
        Assert.That(at, Is.GreaterThanOrEqualTo(0));
        var body = script[at..script.IndexOf("function Exit-Refused", at, StringComparison.Ordinal)];
        Assert.That(body, Does.Contain("& $cliExe spawn -- $path"));
        Assert.That(body, Does.Contain("Start-Process -FilePath $path"), "a CLI from before the verb: as before");
        // Nothing else in the script starts the dashboard the old way.
        Assert.That(script.Replace(body, ""), Does.Not.Contain("Start-Process -FilePath $installed"));
        Assert.That(script.Replace(body, ""), Does.Not.Contain("Start-Process -FilePath (Find-Installed)"));
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "run.ps1"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("run.ps1 not found above the test output folder.");
    }
}
