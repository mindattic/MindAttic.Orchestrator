using System.Windows;
using System.Windows.Threading;
using System.Xml.Linq;
using MindAttic.Orchestrator.Wpf;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>A full-screen loader covers the dashboard until its first refresh lands (M.ORCH-T184), and its spinner keeps
/// turning while the window's own thread is busy applying that refresh (M.ORCH-T200).</summary>
[Apartment(ApartmentState.STA)]
public sealed class LoaderTests
{
    [Test]
    public void The_loader_covers_every_row_of_the_dashboard_until_the_first_refresh()
    {
        var root = TestContext.CurrentContext.TestDirectory;
        while (!Path.Exists(Path.Combine(root, ".git"))) root = Path.GetDirectoryName(root) ?? throw new AssertionException("no repo root above the tests");
        XNamespace p = "http://schemas.microsoft.com/winfx/2006/xaml/presentation", x = "http://schemas.microsoft.com/winfx/2006/xaml";
        var grid = XDocument.Load(Path.Combine(root, "src", "MindAttic.Orchestrator.Wpf", "MainWindow.xaml")).Root!.Element(p + "Grid")!;
        var rows = grid.Element(p + "Grid.RowDefinitions")!.Elements(p + "RowDefinition").Count();
        var loader = grid.Elements().Last();

        Assert.That((string?)loader.Attribute(x + "Name"), Is.EqualTo("Loader"), "the loader is the root grid's last child, so it draws over everything");
        Assert.That((int?)loader.Attribute("Grid.RowSpan"), Is.EqualTo(rows));
        Assert.That((string?)loader.Attribute("Visibility"), Does.StartWith("{Binding IsLoading,"));
    }

    [Test]
    public void The_loader_spins_on_its_own_thread_rather_than_a_storyboard_on_the_windows_thread()
    {
        var root = TestContext.CurrentContext.TestDirectory;
        while (!Path.Exists(Path.Combine(root, ".git"))) root = Path.GetDirectoryName(root) ?? throw new AssertionException("no repo root above the tests");
        XNamespace p = "http://schemas.microsoft.com/winfx/2006/xaml/presentation", local = "clr-namespace:MindAttic.Orchestrator.Wpf";
        var loader = XDocument.Load(Path.Combine(root, "src", "MindAttic.Orchestrator.Wpf", "MainWindow.xaml")).Root!.Element(p + "Grid")!.Elements().Last();

        Assert.That(loader.Descendants(local + "ThreadedSpinner").Count(), Is.EqualTo(1));
        Assert.That(loader.Descendants(p + "Storyboard"), Is.Empty, "a storyboard here would tick on the window's thread and freeze with it");
    }

    [Test]
    public void The_spinner_keeps_turning_while_the_windows_thread_is_blocked_and_stops_its_thread_when_hidden()
    {
        var spinner = new ThreadedSpinner { Width = 44, Height = 44 };
        var window = OffScreenWindow.Show(120, 120, spinner);
        try
        {
            Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle); // let Loaded run

            Assert.That(spinner.IsSpinning, Is.True);
            Assert.That(spinner.RingThread, Is.Not.Null.And.Not.SameAs(Thread.CurrentThread));

            // This thread now does nothing but block, as it does applying the first snapshot; the ring still turns.
            var angles = new List<double>();
            for (var i = 0; i < 4; i++)
            {
                angles.Add(spinner.Angle ?? double.NaN);
                Thread.Sleep(150);
            }
            Assert.That(angles.Distinct().Count(), Is.GreaterThan(1), $"angles while blocked: {string.Join(", ", angles)}");

            var ring = spinner.RingThread!;
            spinner.Visibility = Visibility.Collapsed;
            Assert.That(spinner.IsSpinning, Is.False);
            Assert.That(ring.Join(TimeSpan.FromSeconds(5)), Is.True, "the ring's thread ends once the loader is hidden");
        }
        finally
        {
            window.Close();
        }
    }
}
