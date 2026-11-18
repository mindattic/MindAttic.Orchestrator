using System.Windows;
using System.Windows.Controls;
using MindAttic.Orchestrator.Wpf;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>M.ORCH-T1756: <see cref="TerminalClip"/> clips only the shown login's one selected pane each layout pass, not
/// every CLI ever opened across every login.</summary>
[Apartment(ApartmentState.STA)]
public sealed class TerminalClipTests
{
    [TestCase(0, 0, 100, 100, 0, 0, 100, 100, null, Description = "fully inside: no region needed")]
    [TestCase(10, 10, 50, 50, 0, 0, 100, 100, null, Description = "fully inside, off the viewport's own origin")]
    [TestCase(80, 0, 50, 50, 0, 0, 100, 100, "0,0,20,50", Description = "half off the right edge")]
    [TestCase(200, 0, 50, 50, 0, 0, 100, 100, "empty", Description = "entirely past the viewport: empty region")]
    public void Region_is_the_part_of_the_window_inside_the_viewport(double x, double y, double w, double h,
        double vx, double vy, double vw, double vh, string? expected)
    {
        var bounds = new Rect(x, y, w, h);
        var viewport = new Rect(vx, vy, vw, vh);
        var region = TerminalClip.Region(bounds, viewport);
        if (expected is null) Assert.That(region, Is.Null);
        else if (expected == "empty") Assert.That(region!.Value.IsEmpty, Is.True);
        else
        {
            var parts = expected.Split(',').Select(int.Parse).ToArray();
            Assert.That(region, Is.EqualTo(new Int32Rect(parts[0], parts[1], parts[2], parts[3])));
        }
    }

    [Test]
    public void Clip_reads_the_selected_pane_fresh_each_layout_pass_rather_than_walking_every_pane()
    {
        var host = new Grid();
        var window = OffScreenWindow.Show(content: host);
        try
        {
            var calls = 0;
            TerminalClip.Attach(host, () => { calls++; return null; });
            window.UpdateLayout();
            Assert.That(calls, Is.GreaterThan(0), "a real layout pass must read selected(), not walk the visual tree for it");
        }
        finally { window.Close(); }
    }

    [Test]
    public void Clip_ignores_a_selected_control_with_no_terminal_window_inside_it()
    {
        // A real pane's Control always wraps an HwndHost; this stands in for "none found" and must not throw.
        var host = new Grid();
        var border = new Border();
        host.Children.Add(border);
        var window = OffScreenWindow.Show(content: host);
        try
        {
            TerminalClip.Attach(host, () => border);
            Assert.DoesNotThrow(() => window.UpdateLayout());
        }
        finally { window.Close(); }
    }
}
