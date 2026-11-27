using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using MindAttic.Components.Wpf.Mvvm;
using MindAttic.Orchestrator.Settings;
using MindAttic.Orchestrator.Status;
using MindAttic.Orchestrator.Tickets;
using MindAttic.Tickets.Log;

namespace MindAttic.Orchestrator.Wpf.ViewModels;

/// <summary>
/// One row of a Roadmap column (M.ORCH-T2394): a repo in the repo column (<see cref="Depth"/> 0), or a ticket in the column
/// that lists its parent's children, with its name, id, level and what it holds, its description, its fill and who has it.
/// </summary>
public sealed class RoadmapRowViewModel : ObservableObject
{
    /// <summary>The height of one description line, so the description stops after the Vault's line count.</summary>
    public const double DescriptionLineHeight = 18;

    private bool isSelected;

    internal RoadmapRowViewModel(RoadmapRepo repo)
    {
        Repo = repo;
        Id = repo.Repo;
        Name = repo.Repo;
        Meta = repo.Milestone ?? "No active milestone";
        Fraction = repo.Fraction;
        Detail = string.Join(" · ", new[]
        {
            repo.ChecksTotal > 0 ? $"checks {repo.ChecksPassed}/{repo.ChecksTotal}" : null,
            Count(repo.OpenTasks, "open task"),
            Count(repo.Epics.Count, "epic"),
        }.OfType<string>());
        Fill = Fraction >= 1 ? Palette.Working : Fraction > 0 ? Palette.Accent : Palette.Quiet;
    }

    internal RoadmapRowViewModel(int depth, RoadmapNode node, Func<string, string> nameOf, int descriptionLines)
    {
        Depth = depth;
        Node = node;
        Id = node.Id;
        Name = node.Name;
        var holds = node.Children.Count == 0 ? node.Status == TicketRules.Open ? null : node.Status
            : node.Children.Select(c => c.Level).Distinct(StringComparer.OrdinalIgnoreCase).Count() == 1
                ? Count(node.Children.Count, node.Children[0].Level)
                : Count(node.Children.Count, "child", "children");
        Meta = string.Join(" · ", new[] { node.Id, node.Level, holds }.OfType<string>());
        var text = PlainText(node.Description);
        Description = text.Length > 0 ? text : node.Summary == node.Name ? "" : node.Summary;
        DescriptionMaxHeight = Math.Max(0, descriptionLines) * DescriptionLineHeight;
        Fraction = node.Fraction;
        if (node.Who is { } who)
        {
            WhoName = nameOf(who);
            Initials = InitialsOf(WhoName);
        }
        Fill = Fraction >= 1 ? Palette.Working : node.WorkingLogin is not null || Fraction > 0 ? Palette.Accent : Palette.Quiet;
    }

    /// <summary>Which column it's in: 0 the repos, then 1 for a repo's top-level tickets, and one more per level down.</summary>
    public int Depth { get; }
    /// <summary>The repo's name, or the ticket's id.</summary>
    public string Id { get; }
    public string Name { get; }
    /// <summary>The repo's milestone, or the ticket's id, level and what it holds: "M.ORCH-T2381 · feature · 4 tasks".</summary>
    public string Meta { get; }
    /// <summary>The ticket's description as plain text, else its summary when that isn't its name; empty for a repo.</summary>
    public string Description { get; } = "";
    public bool HasDescription => Description.Length > 0 && DescriptionMaxHeight > 0;
    public double DescriptionMaxHeight { get; }
    public double Fraction { get; }
    public double Percent => Math.Round(Fraction * 100);
    public string PercentText => $"{Percent:0}%";
    /// <summary>A repo's checks, open tasks and epics; empty for a ticket.</summary>
    public string Detail { get; } = "";
    public bool HasDetail => Detail.Length > 0;
    /// <summary>Done (MaGood), being worked (MaAccent) or not started (MaMuted).</summary>
    public Brush Fill { get; }
    /// <summary>Whoever is working on the ticket, else whoever it's assigned to; null for nobody.</summary>
    public string? WhoName { get; }
    public string Initials { get; } = "";
    public bool HasWho => WhoName is not null;
    public RoadmapNode? Node { get; }
    public RoadmapRepo? Repo { get; }
    public string SpokenName => $"{Name}, {Meta}, {PercentText} done" + (WhoName is { } w ? $", {w}" : "");

    /// <summary>Whether it's the opened row of its column, whose children are the next column.</summary>
    public bool IsSelected { get => isSelected; internal set => Set(ref isSelected, value); }

    private static string Count(int n, string one, string? many = null) => $"{n} {(n == 1 ? one : many ?? one + "s")}";

    /// <summary>Markdown as plain text, lightly: headings, quotes, list bullets, emphasis, code marks and link targets go, blank
    /// lines collapse.</summary>
    internal static string PlainText(string? markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown)) return "";
        var lines = new List<string>();
        foreach (var raw in markdown.Replace("\r", "").Split('\n'))
        {
            var line = raw.Trim();
            if (line.StartsWith("```") || line.All(c => c is '-' or '=' or '*' or '_' or '|' or ':' or ' ') && line.Length > 2) continue;
            line = Regex.Replace(line, @"^(#{1,6}\s+|>\s*|[-*+]\s+\[[ xX]\]\s+|[-*+]\s+)", "");
            line = Regex.Replace(line, @"!?\[([^\]]*)\]\([^)]*\)", "$1");
            line = Regex.Replace(line, @"(\*\*|__|`)", "");
            if (line.Length == 0 && (lines.Count == 0 || lines[^1].Length == 0)) continue;
            lines.Add(line);
        }
        return string.Join("\n", lines).Trim();
    }

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

/// <summary>One column right of the repos: the children of the row opened in the column before it, under a header naming
/// them and their parent ("Features of M.ORCH-T2380 · Roadmap view").</summary>
public sealed record RoadmapColumnViewModel(string Title, int Depth, IReadOnlyList<RoadmapRowViewModel> Rows);

/// <summary>One breadcrumb: a name and how deep it is (0 the repo).</summary>
public sealed record RoadmapCrumb(string Name, int Depth, bool IsFirst)
{
    public string Text => IsFirst ? Name : $"› {Name}";
}

/// <summary>
/// The Roadmap (M.ORCH-T2384, M.ORCH-T2386): every repo's open work as a tree of epics, features, stories and tasks with how far
/// along each is, read from the ticket database off the UI thread. Drill-down columns (M.ORCH-T2394): the repos, then one column
/// per opened row listing its children, as deep as the tree goes.
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
    private IReadOnlyList<RoadmapRowViewModel> repos = [];
    private IReadOnlyList<RoadmapColumnViewModel> columns = [];
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

    /// <summary>The width of each column, the repos' too.</summary>
    public double ColumnWidth => settings().RoadmapColumnWidthPx;

    /// <summary>What the repo column is narrowed to: repos whose name or milestone holds it.</summary>
    public string Filter { get => filter; set { if (Set(ref filter, value ?? "")) Rebuild(); } }

    public IReadOnlyList<string> Sorts { get; } = [PortfolioSort, LeastDoneSort, MostOpenSort];
    public string Sort { get => sort; set { if (Set(ref sort, value ?? PortfolioSort)) Rebuild(); } }

    public IReadOnlyList<RoadmapRowViewModel> Repos { get => repos; private set => Set(ref repos, value); }
    /// <summary>The columns right of the repos, one per opened row.</summary>
    public IReadOnlyList<RoadmapColumnViewModel> Columns { get => columns; private set => Set(ref columns, value); }
    public IReadOnlyList<RoadmapCrumb> Crumbs { get => crumbs; private set => Set(ref crumbs, value); }

    /// <summary>The selection as repo then ticket ids, top down.</summary>
    public IReadOnlyList<string> SelectedPath => path;

    /// <summary>A row's click: opens it, its children the next column and any deeper column closed.</summary>
    public ICommand SelectCommand => selectCommand ??= new RelayCommand(p =>
    {
        if (p is not RoadmapRowViewModel row) return;
        path = [.. path.Take(row.Depth), row.Id];
        Rebuild();
        savePath(path);
        Drilled?.Invoke();
    });

    /// <summary>The selection moved by a click: the page scrolls the newest column into view.</summary>
    public event Action? Drilled;

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

    /// <summary>Assigns a row's ticket and every open ticket under it (M.ORCH-T2395) to <paramref name="email"/> (null:
    /// unassigns them), the one change the Roadmap makes, off the UI thread; the tree is read again after it.</summary>
    public void Assign(RoadmapRowViewModel row, string? email)
    {
        if (row.Node is not { } node) return;
        var ids = node.OpenBranch();
        if (ids.Count == 0) return;
        var root = workspaceRoot();
        var ui = TaskScheduler.FromCurrentSynchronizationContext();
        Task.Run(() => OwnerInboxActions.DelegateAll(root, ids, email)).ContinueWith(done =>
        {
            if (done.Exception is { } ex) Trace.TraceWarning($"Roadmap assign of {node.Id} failed: {ex.GetBaseException().Message}");
            else foreach (var (id, error) in done.Result) Trace.TraceWarning($"Roadmap assign of {id} failed: {error}");
            Load();
        }, CancellationToken.None, TaskContinuationOptions.None, ui);
    }

    /// <summary>Rebuilds the repo column, the columns right of it and the breadcrumb from the tree and the selection; a selected
    /// id no longer in the tree ends the selection there.</summary>
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
        var repoRows = shown.Select(r => new RoadmapRowViewModel(r)).ToList();
        var repo = path.Count > 0 ? tree.Repos.FirstOrDefault(r => string.Equals(r.Repo, path[0], StringComparison.OrdinalIgnoreCase)) : null;
        var kept = new List<string>();
        var opened = new List<RoadmapColumnViewModel>();
        var trail = new List<RoadmapCrumb> { new("All repos", -1, true) };
        if (repo is not null)
        {
            kept.Add(repo.Repo);
            trail.Add(new(repo.Repo, 0, false));
            foreach (var r in repoRows) r.IsSelected = string.Equals(r.Id, repo.Repo, StringComparison.OrdinalIgnoreCase);
            var lines = settings().RoadmapDescriptionLines;
            IReadOnlyList<RoadmapNode> children = repo.Epics;
            var parent = repo.Repo;
            for (var depth = 1; children.Count > 0; depth++)
            {
                var rows = children.Select(n => new RoadmapRowViewModel(depth, n, nameOf, lines)).ToList();
                opened.Add(new($"{TitleOf(children)} of {parent}", depth, rows));
                var picked = path.Count > depth ? rows.FirstOrDefault(b => string.Equals(b.Id, path[depth], StringComparison.OrdinalIgnoreCase)) : null;
                if (picked?.Node is not { } node) break;
                picked.IsSelected = true;
                kept.Add(node.Id);
                trail.Add(new(node.Name, depth, false));
                children = node.Children;
                parent = node.Name == node.Id ? node.Id : $"{node.Id} · {node.Name}";
            }
        }
        path = kept;
        Repos = repoRows;
        Columns = opened;
        Crumbs = trail;
        Raise(nameof(SelectedPath));
        Raise(nameof(ColumnWidth));
    }

    /// <summary>A column's title from its tickets' levels: "Epics", or "Features and tasks" for a mix.</summary>
    private static string TitleOf(IReadOnlyList<RoadmapNode> nodes)
    {
        var names = nodes.Select(n => n.Level).Distinct(StringComparer.OrdinalIgnoreCase).Select(l => l + "s").ToList();
        var text = names.Count == 1 ? names[0] : string.Join(", ", names.Take(names.Count - 1)) + " and " + names[^1];
        return char.ToUpperInvariant(text[0]) + text[1..];
    }
}
