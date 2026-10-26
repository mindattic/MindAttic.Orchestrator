using System.Windows.Input;
using MindAttic.Orchestrator.Wpf;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>Copying an embedded terminal's selection (M.ORCH-T867): which chords take it, and that a missing or unreadable
/// selection always leaves the key for the CLI (its ^C interrupt).</summary>
public sealed class TerminalCopyTests
{
    [Test]
    public void PlainCtrlCCopies() =>
        Assert.That(TerminalCopy.ChordOf(Key.C, ModifierKeys.Control), Is.EqualTo(TerminalCopy.Chord.Copy));

    [TestCase(Key.C, ModifierKeys.None)]
    [TestCase(Key.C, ModifierKeys.Control | ModifierKeys.Shift)]
    [TestCase(Key.C, ModifierKeys.Control | ModifierKeys.Alt)]
    [TestCase(Key.C, ModifierKeys.Shift)]
    [TestCase(Key.V, ModifierKeys.Control)]
    public void OtherChordsAreLeftAlone(Key key, ModifierKeys modifiers) =>
        Assert.That(TerminalCopy.ChordOf(key, modifiers), Is.EqualTo(TerminalCopy.Chord.None));

    [Test]
    public void ASelectionGoesToTheClipboardAndTakesTheKey()
    {
        string? copied = null;
        Assert.That(TerminalCopy.TryTake(() => "hello", t => copied = t), Is.True);
        Assert.That(copied, Is.EqualTo("hello"));
    }

    [TestCase("")]
    [TestCase(null)]
    public void NoSelectionLeavesTheKeyForTheCli(string? selection)
    {
        var touched = false;
        Assert.That(TerminalCopy.TryTake(() => selection, _ => touched = true), Is.False);
        Assert.That(touched, Is.False);
    }

    [Test]
    public void AnUnreadableSelectionLeavesTheKeyForTheCli() =>
        Assert.That(TerminalCopy.TryTake(() => throw new InvalidOperationException(), _ => { }), Is.False);

    [Test]
    public void ABusyClipboardIsContainedAndStillTakesTheKey() =>
        Assert.That(TerminalCopy.TryTake(() => "x", _ => throw new System.Runtime.InteropServices.COMException()), Is.True);
}
