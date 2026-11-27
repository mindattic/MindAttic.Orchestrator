using System.Windows.Input;
using MindAttic.Components.Wpf.Mvvm;

namespace MindAttic.Orchestrator.Wpf.ViewModels;

/// <summary>The Roadmap page (M.ORCH-T2384): shown in the main area in place of the CLI's terminal.</summary>
public sealed partial class MainViewModel
{
    private bool isRoadmapShown;
    private RoadmapViewModel? roadmap;
    private ICommand? showRoadmapCommand;
    private ICommand? showTerminalCommand;

    /// <summary>The Roadmap's own view model; the global login's tab holds the one.</summary>
    public RoadmapViewModel Roadmap => Root.roadmap ??= new RoadmapViewModel(() => Root.WorkspaceRoot, () => Root.ShownSettings, Root.NameOf);

    /// <summary>Whether the Roadmap fills the main area in place of the terminal; the same on every tab.</summary>
    public bool IsRoadmapShown
    {
        get => Root.isRoadmapShown;
        set
        {
            if (Root.isRoadmapShown == value) return;
            Root.isRoadmapShown = value;
            RaiseOnEveryTab(nameof(IsRoadmapShown));
            if (value) Root.Roadmap.Load();
        }
    }

    /// <summary>The sidebar's Roadmap row and the Dashboard menu's Roadmap item.</summary>
    public ICommand ShowRoadmapCommand => Root.showRoadmapCommand ??= new RelayCommand(() => Root.IsRoadmapShown = true);

    /// <summary>Back to the shown CLI's terminal.</summary>
    public ICommand ShowTerminalCommand => Root.showTerminalCommand ??= new RelayCommand(() => Root.IsRoadmapShown = false);
}
