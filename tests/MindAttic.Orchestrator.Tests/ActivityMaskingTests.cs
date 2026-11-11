using MindAttic.Orchestrator.Activity;
using MindAttic.Orchestrator.Gateway;
using MindAttic.Orchestrator.Setup;
using MindAttic.Orchestrator.Term;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>
/// Secret masking for activity targets (M.ORCH-T963): <see cref="SecretShapes"/> is the one source of the shapes the gateway's
/// broadcast masker, <see cref="ToolCallText"/> and the context gate's Python activity records use; an activity text masks
/// every token shape, a value after a secret-sounding name (keeping the name) and a long token run with a digit, and leaves
/// file names, prose and paths alone; a shell command without a description shows its program name only, never an argument.
/// <para>Every token here is a fake built at run time from a fixed seed, so no secret-shaped literal sits in the source.</para>
/// </summary>
public sealed class ActivityMaskingTests
{
    /// <summary>A fake token: <paramref name="prefix"/> and <paramref name="length"/> mixed-case letters and digits.</summary>
    private static string Fake(string prefix, int length, int seed = 11, string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789")
    {
        var random = new Random(seed);
        var body = new char[length];
        for (var i = 0; i < length; i++) body[i] = alphabet[random.Next(alphabet.Length)];
        var at = 0;
        foreach (var c in "7Qk")
            if (at < length && alphabet.Contains(c)) body[at++] = c;
        return prefix + new string(body);
    }

    private const string Upper = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    private const string Alnum = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789";

    private static IEnumerable<TestCaseData> Tokens()
    {
        yield return new TestCaseData(Fake("sk-ant-api03-", 40)).SetName("Anthropic key");
        yield return new TestCaseData(Fake("sk-proj-", 40)).SetName("OpenAI key");
        yield return new TestCaseData(Fake("ghp_", 36, alphabet: Alnum)).SetName("GitHub token");
        yield return new TestCaseData(Fake("github_pat_", 60, alphabet: Alnum)).SetName("GitHub fine-grained token");
        yield return new TestCaseData(Fake("AKIA", 16, alphabet: Upper)).SetName("AWS access key id");
        yield return new TestCaseData(Fake("eyJ", 20) + "." + Fake("", 24, seed: 3) + "." + Fake("", 30, seed: 5)).SetName("JWT");
        yield return new TestCaseData(Fake("xoxb-", 30, alphabet: Alnum)).SetName("Slack token");
        yield return new TestCaseData(Fake("AIza", 35)).SetName("Google API key");
        yield return new TestCaseData(Fake("sk_live_", 24, alphabet: Alnum)).SetName("Stripe key");
        yield return new TestCaseData(Fake("", 40, alphabet: "0123456789abcdef")).SetName("Long hex run");
        yield return new TestCaseData(Fake("", 44, seed: 19) + "==").SetName("Long base64 run");
    }

    [TestCaseSource(nameof(Tokens))]
    public void Every_token_shape_is_masked_in_activity_text(string token)
    {
        var masked = SecretShapes.MaskActivity($"use {token} now");
        Assert.That(masked, Does.Not.Contain(token));
        Assert.That(masked, Does.StartWith("use ").And.EndWith(" now").And.Contain(SecretShapes.ActivityMask));
    }

    [TestCase("API_KEY=", "API_KEY=")]
    [TestCase("token: \"", "token: \"")]
    [TestCase("\"password\":\"", "\"password\":\"")]
    [TestCase("Authorization: Bearer ", "Authorization: Bearer ")]
    public void A_value_after_a_secret_sounding_name_is_masked_and_the_name_kept(string before, string kept)
    {
        // Even a value that doesn't look generated: an activity target never needs it.
        var masked = SecretShapes.MaskActivity(before + "correcthorsebatterystaple");
        Assert.That(masked, Is.EqualTo(kept + SecretShapes.ActivityMask));
    }

    [TestCase("Daemon.cs")]
    [TestCase("Run the tests")]
    [TestCase("MindAttic.Orchestrator.Core.csproj")]
    [TestCase("src/MindAttic.Orchestrator.Core/Activity/ToolCallText.cs")]
    [TestCase("LiveSessions|SessionRecord")]
    [TestCase("skeleton_loader_component_name")]
    [TestCase("a_very_long_identifier_without_any_digits_in_it")]
    [TestCase("")]
    public void File_names_prose_paths_and_identifiers_are_left_alone(string text) =>
        Assert.That(SecretShapes.MaskActivity(text), Is.EqualTo(text));

    [Test]
    public void A_null_text_masks_to_empty() => Assert.That(SecretShapes.MaskActivity(null), Is.EqualTo(""));

    [Test]
    public void A_shell_command_without_a_description_shows_only_its_program()
    {
        var token = Fake("ghp_", 36, alphabet: Alnum);
        var cases = new (string Command, string Expected)[]
        {
            ($"curl -H \"Authorization: Bearer {token}\" https://api.example.com/x", "Running curl"),
            ($"GITHUB_TOKEN={token} gh pr list", "Running gh"),
            ("cd D:\\Projects\\X && dotnet build -c Release", "Running dotnet"),
            ("cd /d/Projects/X; git status", "Running git"),
            ("C:\\Tools\\psql.exe postgres://user:hunter2@db/x", "Running psql.exe"),
            ("/usr/bin/env python script.py --api-key abc", "Running python"),
            ("   ", "Running a command"),
        };
        foreach (var (command, expected) in cases)
        {
            var text = ToolCallText.Describe("Bash", System.Text.Json.JsonSerializer.Serialize(new { command }));
            Assert.That(text, Is.EqualTo(expected), command);
            Assert.That(text, Does.Not.Contain(token));
        }
        Assert.That(ToolCallText.Describe("PowerShell", System.Text.Json.JsonSerializer.Serialize(new { command = "$env:KEY='x'; Invoke-RestMethod -Headers @{a=1}" })),
            Does.Not.Contain("Headers").And.StartWith("Running "));
    }

    [Test]
    public void A_token_in_a_description_pattern_query_or_subagent_task_is_masked()
    {
        var token = Fake("sk-ant-api03-", 40);
        string Describe(string name, object input) => ToolCallText.Describe(name, System.Text.Json.JsonSerializer.Serialize(input));

        Assert.That(Describe("Bash", new { command = "x", description = $"Call the API with {token}" }), Is.EqualTo("Call the API with " + SecretShapes.ActivityMask));
        Assert.That(Describe("Grep", new { pattern = token }), Is.EqualTo($"Searching for '{SecretShapes.ActivityMask}'"));
        Assert.That(Describe("WebSearch", new { query = $"why does {token} fail" }), Does.Not.Contain(token));
        Assert.That(Describe("Agent", new { description = $"Check {token}" }), Does.Not.Contain(token));
        Assert.That(ToolCallText.FirstLine($"token={token}\nmore"), Is.EqualTo("token=" + SecretShapes.ActivityMask));
    }

    [Test]
    public void A_url_shows_its_host_and_an_mcp_tool_its_name_only()
    {
        var token = Fake("ghp_", 36, alphabet: Alnum);
        Assert.That(ToolCallText.Describe("WebFetch", System.Text.Json.JsonSerializer.Serialize(new { url = $"https://api.example.com/x?access_token={token}" })),
            Is.EqualTo("Reading api.example.com"));
        Assert.That(ToolCallText.Describe("mcp__claude_ai_Docs__batch", System.Text.Json.JsonSerializer.Serialize(new { secret = token })), Is.EqualTo("Using batch"));
    }

    [Test]
    public void The_broadcast_masker_reads_the_same_shapes()
    {
        // Google and Stripe keys came in with the shared shapes; the gateway's masker now masks them too.
        foreach (var token in new[] { Fake("AIza", 35), Fake("sk_live_", 24, alphabet: Alnum) })
            Assert.That(SecretMasker.ShapesOnly.MaskText($"key {token} end"), Is.EqualTo($"key {new string('•', token.Length)} end"));
    }

    [Test]
    public void The_context_gate_pastes_the_shared_activity_pattern_into_its_python()
    {
        var script = AgentGate.ContextScript(AgentGate.DefaultContextPercent);
        Assert.That(script, Does.Contain("re.sub(" + SecretShapes.PythonLiteral(SecretShapes.ActivityPattern) + ","));
        // T964's own looser pattern (\b(?:sk|pk|rk)... hid "skeleton_loader") is gone: one source.
        Assert.That(script, Does.Not.Contain("(?:sk|pk|rk|ghp"));
    }

    [TestCase("plain", "\"plain\"")]
    [TestCase("a\\b", "\"a\\\\b\"")]
    [TestCase("say \"hi\"", "\"say \\\"hi\\\"\"")]
    [TestCase("it's", "\"it's\"")]
    public void A_pattern_becomes_a_python_string_literal(string value, string expected) =>
        Assert.That(SecretShapes.PythonLiteral(value), Is.EqualTo(expected));
}
