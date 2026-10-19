using System.Text.Json;
using MindAttic.Orchestrator.Settings;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>/pacing replaced /throttle and /unthrottle (M.ORCH-T483): a settings file saved before still exempts it.</summary>
public sealed class PacingCommandSettingsTests
{
    [Test]
    public void The_default_exempt_commands_name_pacing_and_not_the_retired_ones()
    {
        Assert.That(new OrchestratorSettings().ExemptCommands, Is.EqualTo(new[] { "/pacing", "/deploy", "/run" }));
    }

    [Test]
    public void A_saved_list_naming_throttle_or_unthrottle_reads_as_pacing()
    {
        var s = JsonSerializer.Deserialize<OrchestratorSettings>("""{ "ExemptCommands": ["/throttle", "/unthrottle", "/deploy"] }""")!;
        Assert.That(s.ExemptCommands, Is.EqualTo(new[] { "/pacing", "/deploy" }));
        Assert.That((s with { ExemptCommands = ["unthrottle", "/run"] }).ExemptCommands, Is.EqualTo(new[] { "/pacing", "/run" }));
    }
}
