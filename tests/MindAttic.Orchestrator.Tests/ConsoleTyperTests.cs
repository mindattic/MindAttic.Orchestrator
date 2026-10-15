using MindAttic.Orchestrator.Sessions;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

public sealed class ConsoleTyperTests
{
    [Test]
    public void Records_press_and_release_each_character_and_turn_a_newline_into_enter()
    {
        var r = ConsoleTyper.Records("a\r\nb");
        Assert.That(r, Has.Length.EqualTo(6), "a, Enter, b: a key-down and a key-up each; the bare \\r is skipped");
        Assert.That(r.Select(x => (x.KeyEvent.bKeyDown, x.KeyEvent.UnicodeChar, x.KeyEvent.wVirtualKeyCode)), Is.EqualTo(new[]
        {
            (1, 'a', (ushort)0), (0, 'a', (ushort)0),
            (1, '\r', (ushort)0x0D), (0, '\r', (ushort)0x0D),
            (1, 'b', (ushort)0), (0, 'b', (ushort)0),
        }));
        Assert.That(r.All(x => x.EventType == 0x0001 && x.KeyEvent.wRepeatCount == 1), Is.True, "key events, once each");
        Assert.That(ConsoleTyper.Records(""), Is.Empty);
    }

    [Test]
    public void Nothing_is_typed_into_a_process_that_is_gone_or_isnt_claude()
    {
        Assert.That(ConsoleTyper.CanType(int.MaxValue), Is.False, "no such process");
        Assert.That(ConsoleTyper.Submit(int.MaxValue, "x"), Is.False);
        // The test runner is alive but isn't the Claude CLI: refused before any attach.
        Assert.That(ConsoleTyper.CanType(Environment.ProcessId), Is.False);
        Assert.That(ConsoleTyper.Submit(Environment.ProcessId, "/quicksave"), Is.False);
    }
}
