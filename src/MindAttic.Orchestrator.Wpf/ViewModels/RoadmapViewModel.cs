using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using MindAttic.Components.Wpf.Mvvm;
using MindAttic.Orchestrator.Settings;
using MindAttic.Orchestrator.Status;
using MindAttic.Orchestrator.Tickets;

namespace MindAttic.Orchestrator.Wpf.ViewModels;

/// <summary>
/// One block on the Roadmap page (M.ORCH-T2386): a repo in the repo grid (<see cref="Row"/> 0) or a ticket in a level row under
/// it, with its fill, percent and who has it.
/// </summary>
public sealed class RoadmapBlockViewModel : ObservableObject
{
    private bool isSelected;

    internal RoadmapBlockViewModel(int row, RoadmapRepo repo)
    {
        Row = row;
        Repo = repo;
        Id = repo.Repo;
        Name = repo.Repo;
        Subtitle = repo.Milestone ?? "No active milestone";
        Fraction = repo.Fraction;
        Detail = (repo.ChecksTotal > 0 ? $"checks {repo.ChecksPassed}/{repo.ChecksTotal} · " : "") + $"{repo.OpenTasks} open";
        Tip = repo.MilestoneId is { } m ? $"{m} · {repo.Milestone}" : repo.Repo;
        Fill = Fraction >= 1 ? Palette.Working : Fraction > 0 ? Palette.Accent : Palette.Quiet;
    }

    internal RoadmapBlockViewModel(int row, RoadmapNode node, Func<string, string> nameOf)
    {
        Row = row;
        Node = node;
        Id = node.Id;
        Name = node.Name;
        Subtitle = node.Id;
        Fraction = node.Fraction;
        Detail = node.Children.Count > 0 ? $"{node.Children.Count} {node.Children[0].Level}{(node.Children.Count == 1 ? "" : "s")}" : node.Status;
        Tip = node.Summary;
        if (node.Who is { } who)
        {
            WhoName = nameOf(who);
            Initials = InitialsOf(WhoName);
        }
        Fill = Fraction >= 1 ? Palette.Working : node.WorkingLogin is not null || Fraction > 0 ? Palette.Accent : Palette.Quiet;
    }

    /// <summary>0 for the repo grid, then 1 for the repo's top-level tickets, and one more per level down.</summary>
    public int Row { get; }
    /// <summary>The repo's name, or the ticket's id.</summary>
    public string Id { get; }
    public string Name { get; }
    /// <summary>The repo's milestone, or the ticket's id (muted).</summary>
    public string Subtitle { get; }
    public double Fraction { get; }
    public double Percent => Math.Round(Fraction * 100);
    public string PercentText => $"{Percent:0}%";
    /// <summary>The repo's checks and open tasks, or what the ticket holds.</summary>
    public string Detail { get; }
    /// <summary>The ticket summary's first line, or the repo's milestone.</summary>
    public string Tip { get; }
    /// <summary>Done (MaGood), being worked (MaAccent) or not started (MaMuted).</summary>
    public Brush Fill { get; }
    /// <summary>Whoever is working on the ticket, else whoever it's assigned to; null for nobody.</summary>
    public string? WhoName { get; }
    public string Initials { get; } = "";
    public bool HasWho => WhoName is not null;
    public RoadmapNode? Node { get; }
    public RoadmapRepo? Repo { get; }
    public string SpokenName => $"{Name}, {Subtitle}, {PercentText} done" + (WhoName is { } w ? $", {w}" : "");

    /// <summary>Whether it's the selected block of its row, whose children open in the row below.</summary>
    public bool IsSelected { get => isSelected; internal set => Set(ref isSelected, value); }

    internal static string InitialsOf(string name)
    {
        var at = name.IndexOf('@');
        var words = (at > 0 ? name[..at] : name).Split([' ', '.', '_', '-'], StringSplitOptions.RemoveEmptyEntries);
        return words.Length switch
        {
            0 => "?",
            1 => words[0][..Math.Min(2, words[0].Length)].ToUpperInvariant(),
            _ => $"{char.ToUpperInvariant(words[0][0])}{char.ToUpperInvariant(words[^1][0])}",
        };
    }
}

/// <summary>One level row under the selection: its blocks in priority order.</summary>
public sealed record RoadmapLevelViewModel(string Title, IReadOnlyList<RoadmapBlockViewModel> Blocks);

/// <summary>One breadcrumb: a name and how deep it is (0 the repo).</summary>
public sealed record RoadmapCrumb(string Name, int Depth, bool IsFirst)
{
    public string Text => IsFirst ? Name : $"› {Name}";
}

/// <summary>
/// The Roadmap (M.ORCH-T2384, M.ORCH-T2386): every repo's open work as a tree of epics, features, stories and tasks with how far
/// along each is, read from the ticket database off the UI thread. The repo grid on top, then one row per level under the
/// selected block.
/// </summary>
public sealed class RoadmapViewModel(Func<string> workspaceRoot, Func<OrchestratorSettings> settings, Func<string, string> nameOf,
    IReadOnlyList<string> savedPath, Action<IReadOnlyList<string>> savePath) : ObservableObject
{
    public const string PortfolioSort = "Portfolio order";
    public const string LeastDoneSort = "Least done";
    public const string MostOpenSort = "Most open tasks";

    private readonly RoadmapSource source = new();
    private RoadmapTree tree = RoadmapTree.Empty;
    private bool reading, readAgain;
    private string filter = "";
    private string sort = PortfolioSort;
    private List<string> path = [.. savedPath];
    private DispatcherTimer? reloadTimer;
    private IReadOnlyList<RoadmapBlockViewModel> repos = [];
    private IReadOnlyList<RoadmapLevelViewModel> levels = [];
    private IReadOnlyList<RoadmapCrumb> crumbs = [];
    private ICommand? selectCommand, crumbCommand;

    /// <summary>The tree as last read.</summary>
    public RoadmapTree Tree
    {
        get => tree;
        private set { tree = value; Raise(); Raise(nameof(OverallPercent)); Raise(nameof(SummaryText)); Rebuild(); }
    }

    /// <summary>Every repo's done leaves over all of them, 0 to 100.</summary>
    public double OverallPercent => Math.Round(tree.Fraction * 100);

    /// <summary>The sidebar row's trailing text: "42% · 17 milestones".</summary>
    public string SummaryText => $"{OverallPercent:0}% · {tree.ActiveMilestones} milestone{(tree.ActiveMilestones == 1 ? "" : "s")}";

    /// <summary>The live settings the page draws with.</summary>
    internal OrchestratorSettings Settings => settings();

    public double BlockWidth => settings().RoadmapBlockWidthPx;
    public double RowGap => settings().RoadmapRowGapPx;
    public Thickness RowGapMargin => new(0, RowGap, 0, 0);

    /// <summary>What the repo grid is narrowed to: repos whose name or milestone holds it.</summary>
    public string Filter { get => filter; set { if (Set(ref filter, value ?? "")) Rebuild(); } }

    public IReadOnlyList<string> Sorts { get; } = [PortfolioSort, LeastDoneSort, MostOpenSort];
    public string Sort { get => sort; set { if (Set(ref sort, value ?? PortfolioSort)) Rebuild(); } }

    public IReadOnlyList<RoadmapBlockViewModel> Repos { get => repos; private set => Set(ref repos, value); }
    public IReadOnlyList<RoadmapLevelViewModel> Levels { get => levels; private set => Set(ref levels, value); }
    public IReadOnlyList<RoadmapCrumb> Crumbs { get => crumbs; private set => Set(ref crumbs, value); }

    /// <summary>The selection as repo then ticket ids, top down.</summary>
    public IReadOnlyList<string> SelectedPath => path;

    /// <summary>A block's click: selects it, opening its children in the row below.</summary>
    public ICommand SelectCommand => selectCommand ??= new RelayCommand(p =>
    {
        if (p is not RoadmapBlockViewModel block) return;
        path = [.. path.Take(block.Row), block.Id];
        Rebuild();
        savePath(path);
        Drilled?.Invoke();
    });

    /// <summary>The selection moved by a click (M.ORCH-T2392): the page scrolls the deepest row into view.</summary>
    public event Action? Drilled;

    /// <summary>Whether a repo is picked: the repo grid then shows only it, and the filter and sort hide (M.ORCH-T2392).</summary>
    public bool IsDrilled => path.Count > 0;
    public bool ShowsRepoTools => !IsDrilled;

    /// <summary>A breadcrumb's click: back up to that level.</summary>
    public ICommand CrumbCommand => crumbCommand ??= new RelayCommand(p =>
    {
        if (p is not RoadmapCrumb crumb) return;
        path = [.. path.Take(crumb.Depth + 1)];
        Rebuild();
        savePath(path);
        Drilled?.Invoke();
    });

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

    /// <summary>A ticket changed (M.ORCH-T2388): reads the tree again once changes stop for the Vault's refresh delay, so a
    /// burst (a sync, an import) reads once.</summary>
    public void ScheduleLoad()
    {
        if (SynchronizationContext.Current is null) return;
        reloadTimer ??= new DispatcherTimer(DispatcherPriority.Background);
        reloadTimer.Stop();
        reloadTimer.Interval = TimeSpan.FromMilliseconds(Math.Max(0, settings().RoadmapRefreshDebounceMs));
        reloadTimer.Tick -= ReloadTick;
        reloadTimer.Tick += ReloadTick;
        reloadTimer.Start();
    }

    private void ReloadTick(object? sender, EventArgs e)
    {
        reloadTimer?.Stop();
        Load();
    }

    /// <summary>Assigns a block's ticket to <paramref name="email"/> (null: unassigns it), the one change the Roadmap makes,
    /// off the UI thread; the tree is read again after it.</summary>
    public void Assign(RoadmapBlockViewModel block, string? email)
    {
        if (block.Node is not { } node) return;
        var root = workspaceRoot();
        var ui = TaskScheduler.FromCurrentSynchronizationContext();
        Task.Run(() => OwnerInboxActions.Delegate(root, node.Id, email)).ContinueWith(done =>
        {
            if (done.Exception is { } ex) Trace.TraceWarning($"Roadmap assign of {node.Id} failed: {ex.GetBaseException().Message}");
            Load();
        }, CancellationToken.None, TaskContinuationOptions.None, ui);
    }

    /// <summary>Rebuilds the repo grid, the level rows and the breadcrumb from the tree and the selection; a selected id no
    /// longer in the tree ends the selection there.</summary>
    private void Rebuild()
    {
        if (ReferenceEquals(tree, RoadmapTree.Empty)) return;
        var shown = tree.Repos.Where(r => filter.Length == 0 || r.Repo.Contains(filter, StringComparison.OrdinalIgnoreCase)
            || (r.Milestone?.Contains(filter, StringComparison.OrdinalIgnoreCase) ?? false));
        shown = sort switch
        {
            LeastDoneSort => shown.OrderBy(r => r.Fraction),
            MostOpenSort => shown.OrderByDescending(r => r.OpenTasks),
            _ => shown,
        };
        var repoBlocks = shown.Select(r => new RoadmapBlockViewModel(0, r)).ToList();
        var repo = path.Count > 0 ? tree.Repos.FirstOrDefault(r => string.Equals(r.Repo, path[0], StringComparison.OrdinalIgnoreCase)) : null;
        var kept = new List<string>();
        var rows = new List<RoadmapLevelViewModel>();
        var trail = new List<RoadmapCrumb>();
        if (repo is not null)
        {
            kept.Add(repo.Repo);
            // Drilled in (M.ORCH-T2392): the grid shrinks to the picked repo, so its epics open right under it rather than
            // below ~90 other repo blocks, and "All repos" goes back to the full grid.
            trail.Add(new("All repos", -1, true));
            trail.Add(new(repo.Repo, 0, false));
            repoBlocks = [new RoadmapBlockViewModel(0, repo) { IsSelected = true }];
            IReadOnlyList<RoadmapNode> children = repo.Epics;
            for (var row = 1; children.Count > 0; row++)
            {
                var blocks = children.Select(n => new RoadmapBlockViewModel(row, n, nameOf)).ToList();
                rows.Add(new(TitleOf(children), blocks));
                var picked = path.Count > row ? blocks.FirstOrDefault(b => string.Equals(b.Id, path[row], StringComparison.OrdinalIgnoreCase)) : null;
                if (picked?.Node is not { } node) break;
                picked.IsSelected = true;
                kept.Add(node.Id);
                trail.Add(new(node.Name, row, false));
                children = node.Children;
            }
        }
        path = kept;
        Repos = repoBlocks;
        Levels = rows;
        Crumbs = trail;
        Raise(nameof(SelectedPath));
        Raise(nameof(IsDrilled));
        Raise(nameof(ShowsRepoTools));
    }

    /// <summary>A row's title from its tickets' levels: "Epics", or "Features and tasks" for a mix.</summary>
    private static string TitleOf(IReadOnlyList<RoadmapNode> nodes)
    {
        var names = nodes.Select(n => n.Level).Distinct(StringComparer.OrdinalIgnoreCase).Select(l => l + "s").ToList();
        var text = names.Count == 1 ? names[0] : string.Join(", ", names.Take(names.Count - 1)) + " and " + names[^1];
        return char.ToUpperInvariant(text[0]) + text[1..];
    }
}
