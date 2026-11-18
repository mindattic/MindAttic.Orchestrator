using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using MindAttic.Components.Wpf.Mvvm;
using MindAttic.Orchestrator.Settings;
using MindAttic.Orchestrator.Status;

namespace MindAttic.Orchestrator.Wpf.ViewModels;

/// <summary>One row of the Plans list (M.ORCH-T1826): a saved plan's title (a link that opens its file), the repo it was
/// saved in and when it was last written, its status when the file gives one, and its summary.</summary>
public sealed class PlanRowViewModel(PlanEntry plan, DateTime nowUtc)
{
    /// <summary>What names it across refreshes: its path and when it was written.</summary>
    public string Key { get; } = plan.Key;
    /// <summary>Its title: the file's first heading, else its file name.</summary>
    public string Title { get; } = plan.Title;
    /// <summary>The repo it was saved in; the workspace's own name for one at the workspace root.</summary>
    public string Repo { get; } = plan.Repo;
    /// <summary>What its <c>Status:</c> line says; empty when it has none.</summary>
    public string Status { get; } = plan.Status ?? "";
    /// <summary>Whether the file gives a status.</summary>
    public bool HasStatus => Status.Length > 0;
    /// <summary>Its first paragraph, cut to the settings' length; empty when it has none.</summary>
    public string Summary { get; } = plan.Summary;
    /// <summary>Whether it has a summary.</summary>
    public bool HasSummary => Summary.Length > 0;
    /// <summary>The file's full path: what the title opens and Show in folder reveals.</summary>
    public string Path { get; } = plan.Path;
    /// <summary>Its repo and how long ago it was last written, "MindAttic.Orchestrator · 2d ago".</summary>
    public string Where { get; } = string.Join(" · ", new[] { plan.Repo, Formatting.Ago(plan.LastWriteTimeUtc, nowUtc) }.Where(p => !string.IsNullOrEmpty(p)));
    /// <summary>What a screen reader says for the row.</summary>
    public string AccessibleName { get; } = string.Join(". ", new[]
    {
        $"Plan: {plan.Title}", plan.Repo, plan.Status is { Length: > 0 } status ? $"Status: {status}" : null,
        $"last changed {Formatting.Ago(plan.LastWriteTimeUtc, nowUtc)}",
    }.Where(p => !string.IsNullOrEmpty(p)));
}

/// <summary>
/// The saved plans in the dashboard (M.ORCH-T1826): the menu bar's Plans button and the list it opens. A plan is a markdown
/// file under <c>docs\plans</c> in the workspace or any repo: a half-formed idea, or a feature not ready for an implementation
/// ticket yet. Read with the rest of the refresh, off the UI thread (<see cref="PlanCatalog"/>), from the repos the snapshot
/// already lists. Kept on the global login's tab and shown from every tab, like the Inbox.
/// </summary>
public sealed partial class MainViewModel
{
    private volatile IReadOnlyList<PlanEntry>? plansRead;
    private readonly ObservableCollection<PlanRowViewModel> planRows = [];
    private bool isPlansOpen;
    private ICommand? togglePlansCommand, closePlansCommand, openPlanCommand, revealPlanCommand;

    /// <summary>The plans saved in the workspace and in <paramref name="built"/>'s repos; null (the last list stays) when they
    /// couldn't be read. Never throws.</summary>
    internal IReadOnlyList<PlanEntry>? PlansNow(Snapshot built, OrchestratorSettings? settings)
    {
        try { return PlanCatalog.Read(WorkspaceRoot, built, settings ?? new OrchestratorSettings()); }
        catch (Exception ex) when (ex is not OutOfMemoryException) { return null; }
    }

    /// <summary>Shows the plans the refresh read: rows kept newest first, the count and its texts said on every tab.</summary>
    private void ApplyPlans()
    {
        if (!IsGlobalTab) { Root.ApplyPlans(); return; }
        if (plansRead is not { } plans) return;
        var now = DateTime.UtcNow;
        var same = planRows.Count == plans.Count && planRows.Select(r => r.Key).SequenceEqual(plans.Select(p => p.Key), StringComparer.OrdinalIgnoreCase);
        // Unchanged and on screen: left alone, so the open list keeps its scroll and focus. Otherwise rebuilt (the list is
        // short), which also moves each row's "2h ago" on.
        if (same && isPlansOpen) return;
        planRows.Clear();
        foreach (var plan in plans) planRows.Add(new PlanRowViewModel(plan, now));
        if (same) return;
        foreach (var name in new[] { nameof(PlansCount), nameof(HasPlans), nameof(PlansToolTip), nameof(PlansTitle) })
            RaiseOnEveryTab(name);
    }

    /// <summary>Every saved plan, newest first.</summary>
    public ObservableCollection<PlanRowViewModel> PlansItems => Root.planRows;

    /// <summary>How many plans are saved.</summary>
    public int PlansCount => Root.planRows.Count;

    /// <summary>Whether any plan is saved.</summary>
    public bool HasPlans => PlansCount > 0;

    /// <summary>The Plans button's tooltip and accessible name.</summary>
    public string PlansToolTip => PlansLine(PlansCount);

    internal static string PlansLine(int count) => count switch
    {
        <= 0 => "Plans",
        1 => "Plans: 1 saved plan",
        _ => $"Plans: {count} saved plans",
    };

    /// <summary>The list's title, with its count.</summary>
    public string PlansTitle => PlansCount > 0 ? $"Plans ({PlansCount})" : "Plans";

    /// <summary>What the list says with no plan saved: where to put one.</summary>
    public string PlansEmptyText => PlansEmptyLine;

    internal const string PlansEmptyLine = @"No plans yet. Save a markdown file under docs\plans in the workspace or any repo.";

    /// <summary>Whether the Plans list is open; the same on every tab.</summary>
    public bool IsPlansOpen { get => Root.isPlansOpen; set { Root.isPlansOpen = value; RaiseOnEveryTab(nameof(IsPlansOpen)); } }

    /// <summary>The menu bar's Plans button: opens the list, or closes it.</summary>
    public ICommand TogglePlansCommand => Root.togglePlansCommand ??= new RelayCommand(() => Root.IsPlansOpen = !Root.IsPlansOpen);

    /// <summary>Closes the list (its close button, Esc, the backdrop).</summary>
    public ICommand ClosePlansCommand => Root.closePlansCommand ??= new RelayCommand(() => Root.IsPlansOpen = false);

    /// <summary>Opens a row's file in whatever program opens markdown (its title).</summary>
    public ICommand OpenPlanCommand => Root.openPlanCommand ??= new RelayCommand(p => { if (p is PlanRowViewModel row) Shell(row, Opener.Open); });

    /// <summary>Shows a row's file selected in Explorer.</summary>
    public ICommand RevealPlanCommand => Root.revealPlanCommand ??= new RelayCommand(p => { if (p is PlanRowViewModel row) Shell(row, Opener.Reveal); });

    /// <summary>Hands <paramref name="row"/>'s file to the shell; one that can't be started is logged, never thrown at the click.</summary>
    private static void Shell(PlanRowViewModel row, Action<string> act)
    {
        try { act(row.Path); }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            OrchestratorLog.For("Dashboard").Error(ex, "Opening or showing a plan's file failed");
        }
    }
}
