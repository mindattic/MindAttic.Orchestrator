using System.Windows;
using MindAttic.Orchestrator.Wpf;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>No tray (M.ORCH-T501): closing the dashboard quits it, and a later launch brings the open one to the front
/// (SingleInstanceTests, M.ORCH-T497), restored as it was.</summary>
[Apartment(ApartmentState.STA)]
public sealed class DashboardWindowTests
{
    private static Window NewWindow() => new() { Width = 200, Height = 120, ShowActivated = false, ShowInTaskbar = false };

    [Test]
    public void The_dashboard_has_no_tray_icon_to_hide_in()
    {
        var wpf = typeof(MainWindow).Assembly;
        Assert.That(wpf.GetReferencedAssemblies().Select(a => a.Name), Has.None.EqualTo("System.Windows.Forms"), "NotifyIcon was the only use");
        Assert.That(wpf.GetType("MindAttic.Orchestrator.Wpf.Tray"), Is.Null);
    }

    [Test]
    public void Closing_the_dashboard_closes_it()
    {
        var window = NewWindow();
        var closed = false;
        window.Closed += (_, _) => closed = true;
        DashboardWindow.Track(window);
        window.Show();

        window.Close();

        Assert.That(closed, Is.True);
    }

    [Test]
    public void A_maximized_dashboard_brought_back_from_minimized_comes_back_maximized()
    {
        // M.ORCH-T336: Restore set every minimized window to Normal.
        var window = NewWindow();
        DashboardWindow.Track(window);
        window.Show();
        window.WindowState = WindowState.Maximized;
        window.WindowState = WindowState.Minimized;

        DashboardWindow.Restore(window);
        Assert.That((window.IsVisible, window.WindowState), Is.EqualTo((true, WindowState.Maximized)));

        window.WindowState = WindowState.Normal;
        window.WindowState = WindowState.Minimized;
        DashboardWindow.Restore(window);
        Assert.That(window.WindowState, Is.EqualTo(WindowState.Normal), "and a normal one normal");
        window.Close();
    }

    [Test]
    public void Bringing_it_back_puts_it_in_front_without_leaving_it_always_on_top()
    {
        var window = NewWindow();
        DashboardWindow.Track(window);
        window.Show();
        window.WindowState = WindowState.Minimized;

        DashboardWindow.Restore(window);

        Assert.That((window.IsVisible, window.WindowState, window.Topmost), Is.EqualTo((true, WindowState.Normal, false)));
        window.Close();
    }

    [Test]
    public void Retiring_hidden_copies_leaves_this_process_and_the_daemon_alone()
    {
        // Only dashboards wait on their exit event; this test process has none, and is the only copy of its exe.
        Assert.That(DashboardWindow.RetireHiddenCopies(Environment.ProcessPath!), Is.Zero);
    }
}
