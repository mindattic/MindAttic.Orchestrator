using System.Diagnostics;
using MindAttic.Components.Wpf.Mvvm;
using MindAttic.Orchestrator.Settings;
using MindAttic.Orchestrator.Tickets;

namespace MindAttic.Orchestrator.Wpf.ViewModels;

/// <summary>
/// The Roadmap (M.ORCH-T2384): every repo's open work as a tree of epics, features, stories and tasks with how far along each
/// is, read from the ticket database off the UI thread.
/// </summary>
public sealed class RoadmapViewModel(Func<string> workspaceRoot, Func<OrchestratorSettings> settings) : ObservableObject
{
    private readonly RoadmapSource source = new();
    private RoadmapTree tree = RoadmapTree.Empty;
    private bool reading, readAgain;

    /// <summary>The tree as last read.</summary>
    public RoadmapTree Tree { get => tree; private set { tree = value; Raise(); Raise(nameof(OverallPercent)); Raise(nameof(SummaryText)); Applied(); } }

    /// <summary>Every repo's done leaves over all of them, 0 to 100.</summary>
    public double OverallPercent => Math.Round(tree.Fraction * 100);

    /// <summary>The sidebar row's trailing text: "42% · 17 milestones".</summary>
    public string SummaryText => $"{OverallPercent:0}% · {tree.ActiveMilestones} milestone{(tree.ActiveMilestones == 1 ? "" : "s")}";

    /// <summary>The live settings the page draws with.</summary>
    internal OrchestratorSettings Settings => settings();

    /// <summary>Reads the tree off the UI thread, then shows it; a read asked for while one runs follows it.</summary>
    public void Load()
    {
        if (reading) { readAgain = true; return; }
        if (SynchronizationContext.Current is null) return;
        var ui = TaskScheduler.FromCurrentSynchronizationContext();
        var root = workspaceRoot();
        var s = settings();
        reading = true;
        Task.Run(() =>
        {
            try { return source.Read(root, s.RoadmapClosedEpicDays, s.RoadmapWorkingCredit, DateTime.UtcNow); }
            catch (Exception ex) { Trace.TraceWarning($"Roadmap read failed: {ex.Message}"); return null; }
        }).ContinueWith(read =>
        {
            reading = false;
            try { if (read.Status == TaskStatus.RanToCompletion && read.Result is { } t) Tree = t; }
            catch (Exception ex) { Trace.TraceWarning($"Roadmap apply failed: {ex.Message}"); }
            if (readAgain) { readAgain = false; Load(); }
        }, CancellationToken.None, TaskContinuationOptions.None, ui);
    }

    /// <summary>Rebuilds what the page shows from a new tree.</summary>
    private void Applied() { }
}
