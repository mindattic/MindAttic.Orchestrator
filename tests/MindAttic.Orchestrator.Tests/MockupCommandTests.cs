using NUnit.Framework;
using Spectre.Console;

namespace MindAttic.Orchestrator.Tests;

/// <summary><c>orchestrator mockup [SCREEN]</c> (M.ORCH-T1309): a Braille-dot wireframe of one of Orchestrator's own
/// screens, drawn with MindAttic.Helpers.Ascii.AsciiMockup — the first cross-repo consumer of MindAttic.Helpers.</summary>
[NonParallelizable] // AnsiConsole.Console is process-wide
public sealed class MockupCommandTests
{
    private IAnsiConsole previousConsole = null!;
    private StringWriter output = null!;

    [SetUp]
    public void SetUp()
    {
        previousConsole = AnsiConsole.Console;
        output = new StringWriter();
        AnsiConsole.Console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Out = new AnsiConsoleOutput(output),
            Ansi = AnsiSupport.No,
            ColorSystem = ColorSystemSupport.NoColors,
            Interactive = InteractionSupport.No,
        });
    }

    [TearDown]
    public void TearDown() => AnsiConsole.Console = previousConsole;

    private int Run(params string[] args) => CliApp.Create().Run(args);

    [TestCase("dashboard", 80)]
    [TestCase("queue", 70)]
    [TestCase("settings", 70)]
    [TestCase("logins", 70)]
    public void Every_named_mockup_renders_without_throwing_at_its_fixed_width(string name, int width)
    {
        // Any casing (M.ORCH-T1309's command reads it case-insensitively).
        Assert.That(Run("mockup", name.ToUpperInvariant()), Is.Zero, output.ToString());
        var said = output.ToString();
        // AnsiConsole writes "\r\n": trim each line's own trailing '\r' before measuring (Spectre's line ending, not
        // AsciiMockup's — AsciiMockup joins its rows with a bare '\n').
        var lines = said.TrimEnd('\n', '\r').Split('\n').Select(l => l.TrimEnd('\r')).ToArray();
        Assert.That(lines.Length, Is.GreaterThan(1), "a title line plus the wireframe's own rows");
        // The title line is printed as-is (padded/cut to width by AsciiMockup); every wireframe row is exactly
        // `width` characters wide — the same invariant AsciiMockup and AsciiChart test on the Helpers side.
        foreach (var line in lines)
            Assert.That(line.Length, Is.EqualTo(width), $"line {Array.IndexOf(lines, line)}: {line}");
    }

    [Test]
    public void With_no_screen_it_lists_the_names_instead_of_crashing()
    {
        Assert.That(Run("mockup"), Is.Zero);
        var said = output.ToString();
        Assert.That(said, Does.Contain("dashboard").And.Contain("queue").And.Contain("settings").And.Contain("logins"));
    }

    [Test]
    public void Settings_mockup_shows_the_Vault_tab_not_the_removed_dialog()
    {
        Assert.That(Run("mockup", "settings"), Is.Zero);
        var said = output.ToString();
        Assert.That(said, Does.Contain("Vault"));
        Assert.That(said, Does.Not.Contain("Person tabs"), "the old Settings dialog's tab strip is gone (M.ORCH-T1311)");
    }

    [Test]
    public void An_unknown_screen_says_so_and_lists_the_names_rather_than_crashing()
    {
        Assert.That(Run("mockup", "bogus"), Is.EqualTo(1));
        var said = output.ToString();
        Assert.That(said, Does.Contain("No mockup named").And.Contain("bogus").And.Contain("dashboard"));
    }

    [Test]
    public void Mockups_help_renders_without_a_markup_error()
    {
        Assert.That(Run("mockup", "--help"), Is.Zero, output.ToString());
    }
}
