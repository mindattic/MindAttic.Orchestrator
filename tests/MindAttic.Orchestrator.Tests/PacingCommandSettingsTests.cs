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
        Assert.That(new OrchestratorSettings().ExemptCommands, Is.EqualTo(new[] { "/pacing", "/deploy", "/run", "/allocate", "/commit", "/quicksave", "/quickload" }));
    }

    [Test]
    public void A_saved_list_naming_throttle_or_unthrottle_reads_as_pacing()
    {
        var s = JsonSerializer.Deserialize<OrchestratorSettings>("""{ "ExemptCommands": ["/throttle", "/unthrottle", "/deploy"] }""")!;
        Assert.That(s.ExemptCommands, Is.EqualTo(new[] { "/pacing", "/deploy" }));
        Assert.That((s with { ExemptCommands = ["unthrottle", "/run"] }).ExemptCommands, Is.EqualTo(new[] { "/pacing", "/run" }));
    }

    [Test]
    public void A_null_entry_in_a_saved_list_is_dropped_rather_than_kept_as_null()
    {
        // System.Text.Json allows a null element in a string array; RenameRetired must not let it survive into the
        // non-nullable ExemptCommands list (it used to fall through its retired-name check unchanged, still null).
        var s = JsonSerializer.Deserialize<OrchestratorSettings>("""{ "ExemptCommands": ["/pacing", null, "/deploy"] }""")!;
        Assert.That(s.ExemptCommands, Is.EqualTo(new[] { "/pacing", "/deploy" }));
        Assert.That((s with { ExemptCommands = ["unthrottle", null!, "/run"] }).ExemptCommands, Is.EqualTo(new[] { "/pacing", "/run" }));
    }

    [Test]
    public void Pacing_is_typed_only()
    {
        // M.ORCH-T519: only a typed /pacing records anything; a Skill call did nothing while its body had the model say it had.
        Assert.That(MindAttic.Orchestrator.Setup.AgentGate.PacingCommand, Does.Contain("disable-model-invocation: true"));
    }
}
