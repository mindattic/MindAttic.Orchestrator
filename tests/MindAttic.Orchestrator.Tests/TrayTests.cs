using System.Reflection;
using System.Windows;
using MindAttic.Orchestrator.Wpf;
using NUnit.Framework;
using Forms = System.Windows.Forms;

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

    /// <summary>Raises the NotifyIcon's own mouse event, as its tray message handler does for a click on the icon.</summary>
    private static void Raise(Forms.NotifyIcon icon, string handler, Forms.MouseButtons button) =>
        typeof(Forms.NotifyIcon).GetMethod(handler, BindingFlags.Instance | BindingFlags.NonPublic, [typeof(Forms.MouseEventArgs)])!
            .Invoke(icon, [new Forms.MouseEventArgs(button, handler == "OnMouseDoubleClick" ? 2 : 1, 0, 0, 0)]);

    [Test]
    public void Clicking_or_double_clicking_the_tray_icon_opens_the_dashboard_and_the_right_button_does_not()
    {
        using var icon = new Forms.NotifyIcon();
        var opened = new List<string>();
        Tray.OpenOnClick(icon, () => opened.Add("open"));

        Raise(icon, "OnMouseClick", Forms.MouseButtons.Right);
        Assert.That(opened, Is.Empty, "the right button is the menu");

        Raise(icon, "OnMouseClick", Forms.MouseButtons.Left);
        Assert.That(opened, Has.Count.EqualTo(1), "a single click");

        // A double click: Windows sends down, up, dblclk, up; NotifyIcon raises MouseClick then MouseDoubleClick.
        opened.Clear();
        Raise(icon, "OnMouseDoubleClick", Forms.MouseButtons.Left);
        Assert.That(opened, Is.Not.Empty, "a double click on its own opens it too");
    }

    [Test]
    public void Minimizing_hides_the_dashboard_to_the_tray()
    {
        var window = NewWindow();
        Tray.MinimizeToTray(window);
        window.Show();

        window.WindowState = WindowState.Minimized;

        Assert.That(window.IsVisible, Is.False);
        window.Close();
    }

    [Test]
    public void Restoring_brings_the_dashboard_to_the_front_without_leaving_it_always_on_top()
    {
        var window = NewWindow();
        var exiting = false;
        Tray.CloseToTray(window, () => exiting);
        window.Show();
        window.Close(); // hidden to the tray

        Tray.Restore(window);

        Assert.That((window.IsVisible, window.WindowState, window.Topmost), Is.EqualTo((true, WindowState.Normal, false)));
        exiting = true;
        window.Close();
    }
}
