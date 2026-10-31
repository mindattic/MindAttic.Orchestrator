using System.Collections.ObjectModel;
using System.Windows.Threading;
using MindAttic.Orchestrator.Wpf.ViewModels;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>
/// M.ORCH-T1317: closing a person's last tab must be able to open their new Launcher, which changes the pane collection.
/// Done inside CollectionChanged that throws; posted with AfterChange it runs once the change is over.
/// </summary>
[TestFixture, Apartment(ApartmentState.STA)]
public sealed class AfterChangeTests
{
    private static void Pump() => Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);

    [Test]
    public void Changing_the_collection_inside_its_own_CollectionChanged_throws_which_is_the_bug()
    {
        var panes = new ObservableCollection<string> { "HO: Launcher", "OTHER: Launcher" };
        // A second handler subscribed, as the dashboard has several, makes the reentrancy guard refuse the change.
        panes.CollectionChanged += (_, _) => { };
        panes.CollectionChanged += (_, e) => { if (e.OldItems is not null) panes.Add("HO: Launcher (new)"); };
        Assert.Throws<InvalidOperationException>(() => panes.Remove("HO: Launcher"));
    }

    [Test]
    public void Posted_work_runs_after_the_change_and_may_change_the_collection()
    {
        var panes = new ObservableCollection<string> { "HO: Launcher", "OTHER: Launcher" };
        panes.CollectionChanged += (_, _) => { };
        panes.CollectionChanged += (_, e) =>
        {
            if (e.OldItems is not null) AfterChange.Post(() => panes.Add("HO: Launcher (new)"));
        };

        Assert.DoesNotThrow(() => panes.Remove("HO: Launcher"));
        Assert.That(panes, Has.No.Member("HO: Launcher (new)"), "not run inline, during the change");
        Pump();
        Assert.That(panes, Has.Member("HO: Launcher (new)"), "run once the change finished");
    }
}
