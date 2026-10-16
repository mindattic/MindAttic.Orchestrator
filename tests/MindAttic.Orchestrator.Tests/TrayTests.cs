using System.Windows;
using MindAttic.Orchestrator.Wpf;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>The dashboard lives in the tray: closing hides it, the tray brings it back, only Exit quits (M.ORCH-T183).</summary>
[Apartment(ApartmentState.STA)]
public sealed class TrayTests
{
    private static Window NewWindow() => new() { Width = 200, Height = 120, ShowActivated = false, ShowInTaskbar = false };

    [Test]
    public void Close_button_hides_the_dashboard_to_the_tray_instead_of_quitting()
    {
        var window = NewWindow();
        var closed = false;
        window.Closed += (_, _) => closed = true;
        Tray.CloseToTray(window, () => false);
        window.Show();

        window.Close();

        Assert.That((closed, window.IsVisible), Is.EqualTo((false, false)));
    }

    [Test]
    public void Exit_closes_the_dashboard_for_real()
    {
        var window = NewWindow();
        var closed = false;
        window.Closed += (_, _) => closed = true;
        var exiting = false;
        Tray.CloseToTray(window, () => exiting);
        window.Show();

        exiting = true;
        window.Close();

        Assert.That(closed, Is.True);
    }

    [Test]
    public void The_tray_brings_back_a_dashboard_that_was_closed_or_minimized()
    {
        var window = NewWindow();
        var exiting = false;
        Tray.CloseToTray(window, () => exiting);
        window.Show();
        window.WindowState = WindowState.Minimized;
        window.Close();
        Assert.That(window.IsVisible, Is.False);

        Tray.Restore(window);

        Assert.That((window.IsVisible, window.WindowState), Is.EqualTo((true, WindowState.Normal)));
        exiting = true;
        window.Close();
    }
}
