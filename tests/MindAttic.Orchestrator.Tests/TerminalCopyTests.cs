using System.IO;
using System.Windows.Input;
using MindAttic.Orchestrator.Sessions;
using MindAttic.Orchestrator.Wpf;
using MindAttic.Orchestrator.Wpf.ViewModels;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>Copying an embedded terminal's selection (M.ORCH-T867) and cutting it (M.ORCH-T866): which chords take it, and that a
/// missing or unreadable selection always leaves the key for the CLI (its ^C interrupt, or ^X).</summary>
public sealed class TerminalCopyTests
{
    [Test]
    public void PlainCtrlCCopies() =>
        Assert.That(TerminalCopy.ChordOf(Key.C, ModifierKeys.Control), Is.EqualTo(TerminalCopy.Chord.Copy));

    [Test]
    public void PlainCtrlXCuts() =>
        Assert.That(TerminalCopy.ChordOf(Key.X, ModifierKeys.Control), Is.EqualTo(TerminalCopy.Chord.Cut));

    [TestCase(Key.X, ModifierKeys.None)]
    [TestCase(Key.X, ModifierKeys.Control | ModifierKeys.Shift)]
    [TestCase(Key.X, ModifierKeys.Control | ModifierKeys.Alt)]
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

    /// <summary>M.ORCH-T1278: swallowRepeats used to be a single static field shared by every pane, so holding Ctrl+C in one
    /// pane with a selection and shifting focus to another mid-repeat could swallow that other pane's real Ctrl+C. It is now
    /// per-pane state on TerminalPaneViewModel, so setting it on one pane must never be visible on another.</summary>
    [Test, Apartment(System.Threading.ApartmentState.STA)]
    public void SwallowCopyRepeatsIsPerPaneNotSharedAcrossTheWholeApp()
    {
        TerminalPaneViewModel Pane(string title) => new(new PaneRequest(title, Path.GetTempPath()) { CommandLine = "cmd.exe" });
        var (a, b) = (Pane("A"), Pane("B"));

        Assert.That(a.SwallowCopyRepeats, Is.False);
        Assert.That(b.SwallowCopyRepeats, Is.False);

        // Pane A just copied (as OnPreviewKeyDown does after a successful TryTake): its own repeats should be swallowed...
        a.SwallowCopyRepeats = true;
        Assert.That(a.SwallowCopyRepeats, Is.True);
        // ...but pane B, which never copied anything, must still let its own Ctrl+C reach its CLI as an interrupt.
        Assert.That(b.SwallowCopyRepeats, Is.False, "one pane's swallow state must not leak onto another pane");

        b.SwallowCopyRepeats = true;
        a.SwallowCopyRepeats = false;
        Assert.That(b.SwallowCopyRepeats, Is.True, "each pane's flag is independent in both directions");
    }
}
