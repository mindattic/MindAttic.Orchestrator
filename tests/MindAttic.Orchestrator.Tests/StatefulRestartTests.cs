using MindAttic.Orchestrator.Sessions;
using MindAttic.Orchestrator.Wpf;
using MindAttic.Orchestrator.Wpf.ViewModels;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>M.ORCH-T1510: a restart (clean or not) opens where it was left: the CLI tab that was on screen, the window's place;
/// M.ORCH-T1509: a tab name too long for the tab scrolls, blinks 3 times, and starts over.</summary>
public sealed class StatefulRestartTests
{
    private readonly TempDirs temp = new();

    [TearDown]
    public void TearDown() => temp.DeleteAll();

    [Test]
    public void The_tab_on_screen_and_the_window_round_trip_through_the_restore_file()
    {
        var saved = new RestoreState
        {
            Clean = true,
            SelectedPerson = "amy@example.com",
            SelectedTab = 1,
            Tabs =
            [
                new RestoreTab { Request = new PaneRequest("A1", "C:\\a"), HostId = "a1" },
                new RestoreTab { Request = new PaneRequest("A2", "C:\\a") { CustomName = "Release fixes" }, HostId = "a2" },
            ],
            Window = new RestoreWindow(100, 50, 1400, 900, Maximized: true),
        };
        var store = new SessionRestoreStore(temp.New());
        Assert.That(store.Save(saved), Is.True);
        var previous = store.Load();

        Assert.That(MainViewModel.RememberedShown(previous), Is.EqualTo("a2"));
        Assert.That(previous!.Window, Is.EqualTo(saved.Window));
        Assert.That(previous.Tabs[1].Request.CustomName, Is.EqualTo("Release fixes"), "a renamed tab keeps its name");
    }

    [Test]
    public void No_tab_on_screen_or_an_unhosted_one_remembers_none()
    {
        var tabs = new[] { new RestoreTab { Request = new PaneRequest("Local", "C:\\c") } };
        Assert.That(MainViewModel.RememberedShown(null), Is.Null);
        Assert.That(MainViewModel.RememberedShown(new RestoreState { Tabs = tabs }), Is.Null);
        Assert.That(MainViewModel.RememberedShown(new RestoreState { Tabs = tabs, SelectedTab = 0 }), Is.Null);
        Assert.That(MainViewModel.RememberedShown(new RestoreState { Tabs = tabs, SelectedTab = 5 }), Is.Null);
    }

    [TestCase(100, 50, 1400, 900, true, TestName = "On the screen")]
    [TestCase(-1000, 50, 1400, 900, true, TestName = "Partly off the left edge, its top still reachable")]
    [TestCase(5000, 50, 1400, 900, false, TestName = "On a monitor unplugged since")]
    [TestCase(100, -500, 1400, 900, false, TestName = "Title bar above the screen")]
    [TestCase(100, 50, 50, 40, false, TestName = "Too small to be a real size")]
    public void A_saved_window_place_is_used_only_when_reachable(double left, double top, double width, double height, bool fits)
    {
        // One 1920 x 1080 screen.
        Assert.That(new RestoreWindow(left, top, width, height, false).Fits(0, 0, 1920, 1080), Is.EqualTo(fits));
    }

    [Test]
    public void A_long_name_holds_scrolls_then_blinks_three_times_before_starting_over()
    {
        var (scroll, blinkAt, end) = MarqueeBox.Timeline(80);
        Assert.That(scroll, Is.EqualTo(TimeSpan.FromSeconds(80 / MarqueeBox.PixelsPerSecond)));
        Assert.That(blinkAt, Is.GreaterThan(MarqueeBox.Hold + scroll), "the blinks start once the scroll has stopped");
        Assert.That(end - blinkAt, Is.GreaterThanOrEqualTo(TimeSpan.FromTicks(MarqueeBox.BlinkHalf.Ticks * 2 * MarqueeBox.Blinks)),
            "all three blinks fit before the loop starts over");
        Assert.That(MarqueeBox.Blinks, Is.EqualTo(3));
    }
}
