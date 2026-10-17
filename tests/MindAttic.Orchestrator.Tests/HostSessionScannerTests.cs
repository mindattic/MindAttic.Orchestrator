using MindAttic.Orchestrator.Sessions;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

public sealed class HostSessionScannerTests
{
    [TestCase("mindattic-host-claude-12", "Claude")]
    [TestCase("mindattic-host-CODEX-7", "Codex")]
    [TestCase("MindAttic-Host-gemini-3", "Gemini")]
    [TestCase("mindattic-host-kimi-99", "Kimi")]
    public void A_known_provider_gets_the_launchers_spelling_back(string pipe, string provider) =>
        Assert.That(HostSessionScanner.ParsePipeName(pipe)?.Provider, Is.EqualTo(provider));

    [TestCase("mindattic-host-newthing-5", "newthing")]
    [TestCase("mindattic-host-my-agent-9", "my-agent")]
    public void An_unknown_provider_is_kept_as_written(string pipe, string provider)
    {
        var parsed = HostSessionScanner.ParsePipeName(pipe);
        Assert.That(parsed?.Provider, Is.EqualTo(provider));
        Assert.That(parsed?.Name, Is.EqualTo(pipe));
    }

    [TestCase("mindattic-host-claude-abc")]
    [TestCase("mindattic-host-claude-")]
    [TestCase("mindattic-host-claude-12x")]
    [TestCase("mindattic-host-claude-99999999999")]
    [TestCase("mindattic-host--5")]
    [TestCase("mindattic-host-")]
    [TestCase(@"\\.\pipe\other-claude-5")]
    public void A_name_without_a_numeric_pid_or_provider_is_not_a_host(string pipe) =>
        Assert.That(HostSessionScanner.ParsePipeName(pipe), Is.Null);

    [Test]
    public void A_full_pipe_path_parses_to_its_bare_name()
    {
        var parsed = HostSessionScanner.ParsePipeName(@"\\.\pipe\mindattic-host-claude-4321");
        Assert.That(parsed, Is.EqualTo(("Claude", 4321, "mindattic-host-claude-4321")));
    }

    private static readonly HostSessionScanner Scanner = new(new OrchestratorOptions { WorkspaceRoot = @"D:\W" });

    private static readonly Dictionary<string, string> Roster =
        new(StringComparer.OrdinalIgnoreCase) { ["Vault"] = @"E:\Elsewhere\Vault" };

    [TestCase(@"L.exe host --path ""D:\My Repo"" --name Vault", @"D:\My Repo")]
    [TestCase(@"L.exe host --name Vault --path D:\First", @"D:\First")]
    [TestCase(@"L.exe host --PATH D:\Loud", @"D:\Loud")]
    public void Path_wins_over_name(string commandLine, string expected) =>
        Assert.That(Scanner.WorkingDirectoryOf(commandLine, Roster), Is.EqualTo(expected));

    [TestCase(@"""C:\Program Files\L.exe"" host --name Vault --provider Claude")]
    [TestCase(@"L.exe host --name vault")]
    [TestCase(@"L.exe host --path """" --name Vault")]
    public void A_name_is_looked_up_in_the_roster(string commandLine) =>
        Assert.That(Scanner.WorkingDirectoryOf(commandLine, Roster), Is.EqualTo(@"E:\Elsewhere\Vault"));

    [Test]
    public void A_name_missing_from_the_roster_falls_back_to_a_workspace_folder()
    {
        Assert.That(Scanner.WorkingDirectoryOf(@"L.exe host --name Unknown", Roster), Is.EqualTo(@"D:\W\Unknown"));
        Assert.That(Scanner.WorkingDirectoryOf(@"L.exe host --name ""Two Words""", new Dictionary<string, string>()),
            Is.EqualTo(@"D:\W\Two Words"));
    }

    [TestCase(@"L.exe host --provider Claude")]
    [TestCase(@"L.exe host --path")]
    [TestCase(@"L.exe host --name")]
    [TestCase(@"L.exe host --name ""  """)]
    public void Without_a_usable_path_or_name_there_is_no_directory(string commandLine) =>
        Assert.That(Scanner.WorkingDirectoryOf(commandLine, Roster), Is.Null);

    [Test]
    public void Splitting_keeps_quoted_spaces_inside_one_argument()
    {
        Assert.That(HostSessionScanner.SplitCommandLine(@"""C:\Program Files\L.exe"" host --path ""D:\My Repo"" --title ""A [Claude]"""),
            Is.EqualTo(new[] { @"C:\Program Files\L.exe", "host", "--path", @"D:\My Repo", "--title", "A [Claude]" }));
    }

    [Test]
    public void Splitting_follows_windows_rules_for_escaped_quotes_and_extra_spaces()
    {
        Assert.That(HostSessionScanner.SplitCommandLine(@"L.exe   --prompt ""say \""hi\"" now""  x"),
            Is.EqualTo(new[] { "L.exe", "--prompt", @"say ""hi"" now", "x" }));
    }
}
