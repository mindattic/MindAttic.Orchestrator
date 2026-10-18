using MindAttic.Orchestrator.Billing;
using NUnit.Framework;
using Spectre.Console;

namespace MindAttic.Orchestrator.Tests;

/// <summary><c>orchestrator billing [team|api]</c> run in-process against a temp session folder and a temp <c>~/.claude.json</c>,
/// never the real ones (M.ORCH-T436).</summary>
[NonParallelizable] // BillingCommand.Override and AnsiConsole.Console are process-wide
public sealed class BillingCommandTests
{
    private const string Key = "sk-ant-api03-0123456789abcdefghijWXYZ";
    private string dir = "";
    private IAnsiConsole previousConsole = null!;
    private StringWriter output = null!;
    private string? vaultKey = Key;

    private string ConfigPath => Path.Combine(dir, ".claude.json");

    [SetUp]
    public void SetUp()
    {
        dir = Path.Combine(Path.GetTempPath(), "billing-cli-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(dir, "session-context"));
        File.WriteAllText(ConfigPath, """{"oauthAccount": {"accountUuid": "acct-1", "organizationUuid": "org-1", "emailAddress": "pdftransferjr@mindattic.com", "organizationName": "MindAttic", "organizationType": "claude_team"}}""");
        vaultKey = Key;
        BillingCommand.Override = () => (new BillingSwitch(Path.Combine(dir, "session-context"))
        {
            ConfigPath = ConfigPath,
            VaultKey = () => vaultKey,
            IsAlive = _ => true,
            Send = (_, _) => Task.FromResult(true),
        }, []);
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
    public void TearDown()
    {
        BillingCommand.Override = null;
        AnsiConsole.Console = previousConsole;
        if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
    }

    private int Run(params string[] args) => CliApp.Create().Run(args);

    [Test]
    public void With_no_mode_it_says_who_is_billed()
    {
        Assert.That(Run("billing"), Is.EqualTo(0));
        Assert.That(output.ToString(), Does.StartWith("Billing: Claude Team: pdftransferjr@mindattic.com (MindAttic)"));
        Assert.That(File.Exists(Path.Combine(dir, "session-context", BillingModeFile.FileName)), Is.False, "looking changes nothing");
    }

    [Test]
    public void Api_switches_every_cli_to_the_vault_key_and_shows_it_masked()
    {
        Assert.That(Run("billing", "api"), Is.EqualTo(0));
        var said = output.ToString();
        Assert.That(said, Does.StartWith("Billing is API key."));
        Assert.That(said, Does.Contain("Billing: API KEY: sk-ant-...WXYZ").And.Not.Contain(Key[^20..]));
        Assert.That(ClaudeKeyChoice.Read(ConfigPath, Key[^20..]).Choice, Is.EqualTo(KeyChoice.Approved));
        Assert.That(new BillingModeFile(Path.Combine(dir, "session-context", BillingModeFile.FileName)).Read()!.By, Is.EqualTo("cli"));

        output.GetStringBuilder().Clear();
        Assert.That(Run("billing", "team"), Is.EqualTo(0));
        Assert.That(output.ToString(), Does.StartWith("Billing is Claude Team."));
        Assert.That(ClaudeKeyChoice.Read(ConfigPath, Key[^20..]).Choice, Is.EqualTo(KeyChoice.Rejected));
    }

    [Test]
    public void A_mode_it_doesnt_know_or_a_vault_with_no_key_fails_and_changes_nothing()
    {
        var before = File.ReadAllText(ConfigPath);
        Assert.That(Run("billing", "bedrock"), Is.EqualTo(1));
        Assert.That(output.ToString(), Does.Contain("isn't a billing mode"));
        vaultKey = null;
        Assert.That(Run("billing", "api"), Is.EqualTo(1));
        Assert.That(output.ToString(), Does.Contain("no Claude key in the MindAttic Vault"));
        Assert.That(File.ReadAllText(ConfigPath), Is.EqualTo(before));
    }
}
