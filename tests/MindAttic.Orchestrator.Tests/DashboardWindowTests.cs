using System.Windows;
using MindAttic.Orchestrator.Wpf;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>No tray (M.ORCH-T501): closing the dashboard quits it, and a later launch brings the open one to the front
/// (SingleInstanceTests, M.ORCH-T497), restored as it was.</summary>
[Apartment(ApartmentState.STA)]
public sealed class DashboardWindowTests
{
    /// <summary>Restore as the dashboard does, but recording the activate and focus calls instead of making them, so the
    /// off-screen test window (<see cref="OffScreenWindow"/>, M.ORCH-T530) never takes the user's keyboard focus.</summary>
    private static List<string> Restore(Window window)
    {
        var calls = new List<string>();
        DashboardWindow.Restore(window, w => calls.Add($"activate topmost={w.Topmost}"), w => calls.Add($"focus topmost={w.Topmost}"));
        return calls;
    }

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
        var window = OffScreenWindow.Show();
        var closed = false;
        window.Closed += (_, _) => closed = true;
        DashboardWindow.Track(window);

        window.Close();

        Assert.That(closed, Is.True);
    }

    [Test]
    public void A_maximized_dashboard_brought_back_from_minimized_comes_back_maximized()
    {
        // M.ORCH-T336: Restore set every minimized window to Normal.
        var window = OffScreenWindow.Show();
        DashboardWindow.Track(window);
        window.WindowState = WindowState.Maximized;
        window.WindowState = WindowState.Minimized;

        Restore(window);
        Assert.That((window.IsVisible, window.WindowState), Is.EqualTo((true, WindowState.Maximized)));

        window.WindowState = WindowState.Normal;
        window.WindowState = WindowState.Minimized;
        Restore(window);
        Assert.That(window.WindowState, Is.EqualTo(WindowState.Normal), "and a normal one normal");
        window.Close();
    }

    [Test]
    public void Bringing_it_back_puts_it_in_front_without_leaving_it_always_on_top()
    {
        var window = OffScreenWindow.Show();
        DashboardWindow.Track(window);
        window.WindowState = WindowState.Minimized;

        var calls = Restore(window);

        Assert.That((window.IsVisible, window.WindowState, window.Topmost), Is.EqualTo((true, WindowState.Normal, false)));
        Assert.That(calls, Is.EqualTo(new[] { "activate topmost=True", "focus topmost=False" }), "raised above the rest while it's activated, then let back down");
        window.Close();
    }

    [Test]
    public void Retiring_hidden_copies_leaves_this_process_and_the_daemon_alone()
    {
        // Only dashboards wait on their exit event; this test process has none, and is the only copy of its exe.
        Assert.That(DashboardWindow.RetireHiddenCopies(Environment.ProcessPath!), Is.Zero);
    }
}
