using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows.Input;
using System.Windows.Media;
using MindAttic.Components.Wpf.Mvvm;
using MindAttic.Orchestrator.Sessions;

namespace MindAttic.Orchestrator.Wpf.ViewModels;

/// <summary>
/// One dashboard tab per Claude login (M.ORCH-T613): the global <c>~/.claude</c> login's first, then each per-CLI login under
/// <c>~/.claude-accounts</c> that's signed in or has a CLI on it, titled by its name (M.ORCH-T614) in its terminal tabs' color
/// (M.ORCH-T616), a tab added or removed as logins are signed in or out. Each tab is this whole view model, scoped to its
/// login: its stat tiles, its 5-hour and weekly meter and chart from its own pace, its agents and repos (only the CLIs and
/// Launcher tabs on it), its toasts, and Start opening on its login. Tickets stay workspace-wide, so the queue and the audit
/// read the same everywhere; the Accounts view stays the overview of every login. The global login's tab owns the one
/// shared refresh and hands each tab its part of the snapshot (<see cref="LoginScope.For"/>): no tab refreshes on its own.
/// </summary>
public sealed partial class MainViewModel
{
    private LoginTab tab;
    private readonly ObservableCollection<MainViewModel> loginTabs = null!;
    private MainViewModel? selectedTab;
    private ICommand? nextTabCommand, previousTabCommand;

    /// <summary>The login this tab shows.</summary>
    public LoginTab Tab => tab;

    /// <summary>This tab's per-CLI login (its email); null on the global login's tab.</summary>
    public string? ScopeEmail => tab.Email;

    public bool IsGlobalTab => root is null;

    /// <summary>The login's name ("Jordan", "Ryan (global)").</summary>
    public string TabTitle => tab.Title;

    /// <summary>Its email and which login it is, and how many of its CLIs wait on you.</summary>
    public string TabTip => WaitingCount == 0 ? tab.ToolTip : $"{tab.ToolTip}. {WaitingCount} need{(WaitingCount == 1 ? "s" : "")} you.";

    /// <summary>The login's terminal tab color (M.ORCH-T616), the tab's accent; the theme's muted brush when it has none.</summary>
    public Brush TabBrush => BrushOf(tab.Color);

    /// <summary>How many of its repos need you, on the tab itself so one waiting on another tab is seen; empty when none.</summary>
    public string TabBadge => WaitingCount > 0 ? WaitingCount.ToString(System.Globalization.CultureInfo.InvariantCulture) : "";

    /// <summary>Every login's tab, the global login's first; the same list in every tab.</summary>
    public ObservableCollection<MainViewModel> LoginTabs => Root.loginTabs;

    /// <summary>The tab strip shows once there's a second login: with only the global one it would be a lone tab.</summary>
    public bool HasLoginTabs => LoginTabs.Count > 1;

    /// <summary>The tab on screen; the same in every tab. The window shows it (its DataContext follows this).</summary>
    public MainViewModel SelectedTab
    {
        get => Root.selectedTab ?? Root;
        // Ctrl+click on the selected pill deselects it, writing null: it stays on the one it was.
        set => Root.Select(value);
    }

    /// <summary>Raised on the global login's tab when another tab is chosen.</summary>
    public event EventHandler? SelectedTabChanged;

    /// <summary>Raised on the global login's tab when a toast goes up on any tab, so the window can flash for it.</summary>
    public event EventHandler<ToastViewModel>? ToastRaised;

    /// <summary>Ctrl+Tab and Ctrl+Shift+Tab: the next and the previous login's tab.</summary>
    public ICommand NextTabCommand => nextTabCommand ??= new RelayCommand(() => Root.Step(+1));
    public ICommand PreviousTabCommand => previousTabCommand ??= new RelayCommand(() => Root.Step(-1));

    private void Select(MainViewModel? next)
    {
        if (next is null || ReferenceEquals(next, SelectedTab) || !loginTabs.Contains(next)) return;
        selectedTab = next;
        foreach (var t in loginTabs) t.Raise(nameof(SelectedTab));
        SelectedTabChanged?.Invoke(this, EventArgs.Empty);
        // The Accounts view reads its cards only while open: one opened in the newly shown tab gets them now.
        if (next.IsAccountsView) _ = next.RefreshLoginCardsAsync();
    }

    private void Step(int by)
    {
        if (loginTabs.Count < 2) return;
        var at = loginTabs.IndexOf(SelectedTab);
        Select(loginTabs[((at + by) % loginTabs.Count + loginTabs.Count) % loginTabs.Count]);
    }

    /// <summary>
    /// Keeps a tab per login in <paramref name="tabs"/> (the global login's, this one, first), adding a tab for a login just
    /// signed in and dropping one signed out (back to the global login's tab if it was showing), then hands each tab its own
    /// view of <paramref name="all"/>, this one first: the others reuse the roster it reads.
    /// </summary>
    private void ApplyTabs(Snapshot all, IReadOnlyList<LoginTab> tabs)
    {
        var before = loginTabs.Count;
        loginTabs.SyncBy([.. tabs], t => t.Key, v => v.tab.Key,
            t => t.IsGlobal ? this : new MainViewModel(this, t),
            (v, t) => v.Retitle(t));
        // The global login's tab is always first and always here, whatever the list said.
        if (!loginTabs.Contains(this)) loginTabs.Insert(0, this);
        else if (loginTabs.IndexOf(this) != 0) loginTabs.Move(loginTabs.IndexOf(this), 0);
        if (selectedTab is not null && !loginTabs.Contains(selectedTab)) Select(this);
        if (before != loginTabs.Count) foreach (var t in loginTabs) t.Raise(nameof(HasLoginTabs));

        var options = aggregator.Options;
        foreach (var t in loginTabs.ToList())
            t.Apply(all, LoginScope.For(all, t.ScopeEmail, options.WorkingWindow, options.WaitingWindow));
    }

    private void Retitle(LoginTab next)
    {
        if (next == tab) return;
        tab = next;
        Raise(nameof(Tab)); Raise(nameof(TabTitle)); Raise(nameof(TabTip)); Raise(nameof(TabBrush));
    }

    /// <summary>Tags each new toast with this tab while there's more than one (toasts tagged by tab), and tells the window.</summary>
    private void OnToastsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action != NotifyCollectionChangedAction.Add || e.NewItems is null) return;
        foreach (var t in e.NewItems.OfType<ToastViewModel>())
        {
            if (Root.loginTabs.Count > 1) t.Tab ??= TabTitle;
            Root.ToastRaised?.Invoke(this, t);
        }
    }

    private static readonly Dictionary<string, Brush> brushes = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>A login's tab color ("#2E7D32") as a frozen brush; the theme's muted brush when it isn't one.</summary>
    internal static Brush BrushOf(string? color)
    {
        if (string.IsNullOrEmpty(color)) return Palette.Quiet;
        if (brushes.TryGetValue(color, out var known)) return known;
        Brush brush;
        try { brush = (Brush)new BrushConverter().ConvertFromInvariantString(color)!; }
        catch (Exception ex) when (ex is FormatException or NotSupportedException or InvalidOperationException) { return Palette.Quiet; }
        brush.Freeze();
        return brushes[color] = brush;
    }
}
