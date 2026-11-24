using System.Windows.Controls;
using System.Windows.Threading;
using MindAttic.Components.Wpf.Controls;
using MindAttic.Orchestrator.Settings;
using MindAttic.Orchestrator.Wpf;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>
/// The sidebar remembers its layout across restarts (M.ORCH-T2179): the draggable panels' order and every keyed panel's
/// collapsed state, nested ones included, applied at start and saved once per change.
/// </summary>
[Apartment(ApartmentState.STA)]
public sealed class SidebarPanelsTests
{
    private string dir = "";

    [SetUp]
    public void SetUp() => Directory.CreateDirectory(dir = Path.Combine(Path.GetTempPath(), "orch-sidebar-" + Guid.NewGuid().ToString("N")));

    [TearDown]
    public void TearDown()
    {
        try { Directory.Delete(dir, recursive: true); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    private JsonStore Store() => new(dir);

    private static MaPanel Panel(string key, bool draggable = true, object? content = null)
    {
        var panel = new MaPanel { IsCollapsible = true, Content = content };
        MaPanelGroup.SetPanelKey(panel, key);
        MaPanelGroup.SetIsDraggable(panel, draggable);
        return panel;
    }

    private static string Key(object item) => MaPanelGroup.GetPanelKey((MaPanel)item)!;

    [Test]
    public void The_saved_order_and_collapsed_panels_apply_at_start()
    {
        var store = Store();
        store.SaveSettings(store.LoadSettingsToChange() with
        {
            SidebarPanelOrder = ["tickets", "gone", "orgs"],
            SidebarCollapsedPanels = ["users", "tickets.top"],
        });
        var top = Panel("tickets.top", draggable: false);
        var group = new MaPanelGroup();
        foreach (var p in new[] { Panel("orgs"), Panel("users"), Panel("tickets", content: new StackPanel { Children = { top } }) })
            group.Items.Add(p);

        MainWindow.AttachSidebarPanels(group, store);

        Assert.That(group.Items.Cast<object>().Select(Key), Is.EqualTo(new[] { "tickets", "users", "orgs" }),
            "listed panels take the saved order around the unlisted one; an unknown key is ignored");
        Assert.That(((MaPanel)group.Items[1]).IsExpanded, Is.False);
        Assert.That(top.IsExpanded, Is.False, "a nested panel is restored too");
        Assert.That(((MaPanel)group.Items[0]).IsExpanded, Is.True);
        Pump();
        Assert.That(store.LoadSettings().SidebarCollapsedPanels, Is.EqualTo(new[] { "users", "tickets.top" }), "loading writes nothing back");
    }

    [Test]
    public void A_collapse_or_a_move_is_saved_keeping_every_other_setting()
    {
        var store = Store();
        store.SaveSettings(store.LoadSettingsToChange() with { MaxConcurrentSessions = 7 });
        var orgs = Panel("orgs");
        var users = Panel("users");
        var group = new MaPanelGroup();
        group.Items.Add(orgs);
        group.Items.Add(users);
        MainWindow.AttachSidebarPanels(group, store);

        orgs.IsExpanded = false;
        Pump();
        var s = store.LoadSettings();
        Assert.That(s.SidebarCollapsedPanels, Is.EqualTo(new[] { "orgs" }));
        Assert.That(s.SidebarPanelOrder, Is.Empty, "no panel has moved");
        Assert.That(s.MaxConcurrentSessions, Is.EqualTo(7));

        Assert.That(group.MovePanel(users, 0), Is.True);
        Pump();
        Assert.That(store.LoadSettings().SidebarPanelOrder, Is.EqualTo(new[] { "users", "orgs" }));
    }

    private static void Pump() => Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
}
