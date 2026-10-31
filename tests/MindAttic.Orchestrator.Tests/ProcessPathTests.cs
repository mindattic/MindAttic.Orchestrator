using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>M.ORCH-T1314: a start with only the machine PATH gets the user's entries (tickets, claude, npm) merged back in.</summary>
[TestFixture]
public sealed class ProcessPathTests
{
    [Test]
    public void Merge_adds_the_users_entries_a_machine_only_PATH_lacks()
    {
        var merged = ProcessPath.Merge(@"C:\Windows\system32;C:\Program Files\dotnet\",
            @"C:\Windows\system32;C:\Program Files\dotnet\",
            @"C:\Users\me\.dotnet\tools;C:\Users\me\.local\bin");
        Assert.That(merged.Split(';'), Is.EqualTo(new[]
        {
            @"C:\Windows\system32", @"C:\Program Files\dotnet\", @"C:\Users\me\.dotnet\tools", @"C:\Users\me\.local\bin",
        }));
    }

    [Test]
    public void Merge_keeps_the_current_order_first_and_drops_duplicates_ignoring_case_and_trailing_slash()
    {
        var merged = ProcessPath.Merge(@"D:\first;C:\Tools\", @"c:\tools;C:\Machine", @"D:\FIRST\;C:\User");
        Assert.That(merged.Split(';'), Is.EqualTo(new[] { @"D:\first", @"C:\Tools\", @"C:\Machine", @"C:\User" }));
    }

    [Test]
    public void Merge_expands_environment_variables_like_logon_does()
    {
        var merged = ProcessPath.Merge("", "", @"%SystemRoot%\System32");
        Assert.That(merged, Is.EqualTo(Environment.ExpandEnvironmentVariables(@"%SystemRoot%\System32")));
    }

    [Test]
    public void Merge_handles_empty_and_null_parts() =>
        Assert.That(ProcessPath.Merge(null, ";;", null), Is.Empty);

    [Test, NonParallelizable]
    public void Refresh_restores_a_stripped_PATH_and_is_a_no_op_when_whole()
    {
        var original = Environment.GetEnvironmentVariable("PATH");
        try
        {
            Environment.SetEnvironmentVariable("PATH", Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Machine));
            var user = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User) ?? "";
            var added = ProcessPath.Refresh();
            var now = Environment.GetEnvironmentVariable("PATH")!.Split(';');
            foreach (var e in user.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                Assert.That(now, Has.Some.EqualTo(Environment.ExpandEnvironmentVariables(e)).IgnoreCase.Or.Some.EqualTo(Environment.ExpandEnvironmentVariables(e).TrimEnd('\\')).IgnoreCase, e);
            if (user.Trim(';').Length > 0) Assert.That(added, Is.GreaterThanOrEqualTo(0));
            Assert.That(ProcessPath.Refresh(), Is.Zero, "a second refresh adds nothing");
        }
        finally { Environment.SetEnvironmentVariable("PATH", original); }
    }
}
