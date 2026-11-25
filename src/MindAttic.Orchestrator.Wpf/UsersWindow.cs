using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using MindAttic.Components.Wpf.Controls;
using MindAttic.Orchestrator.Wpf.ViewModels;

namespace MindAttic.Orchestrator.Wpf;

/// <summary>
/// Settings &gt; Users (M.ORCH-T2184): every login (never the API key) with its name, email, whether it's signed in and locked,
/// its usage and its running CLIs, and the only place a login is deleted. Lock/Unlock runs the login's own
/// <see cref="MainViewModel.ToggleLockCommand"/>; Delete runs <see cref="MainViewModel.DeleteLoginCommand"/> (its themed
/// confirm names what goes), and is refused while the login has running project CLIs, which would otherwise end unsaved.
/// The global login has no Delete. Rows are rebuilt when the logins or their state change.
/// </summary>
internal sealed class UsersWindow : Window
{
    private readonly MainViewModel model;
    private readonly StackPanel list = new();
    private readonly TextBlock status = new() { TextWrapping = TextWrapping.Wrap };
    private readonly HashSet<MainViewModel> watched = [];
    private bool showQueued;
    private string shownState = "";

    internal UsersWindow(MainViewModel model)
    {
        this.model = model;
        Title = "Users";
        Width = 760;
        Height = 520;
        MinWidth = 480;
        MinHeight = 320;
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        MaWindow.SetThemedTitleBar(this, true);
        SetResourceReference(BackgroundProperty, "MaChrome");
        SetResourceReference(ForegroundProperty, "MaText");

        var header = new TextBlock
        {
            Text = "Every login on this PC. Lock stops a login opening a CLI or taking work until it's unlocked; Delete removes it for good.",
            TextWrapping = TextWrapping.Wrap,
        };
        header.SetResourceReference(StyleProperty, "MaMutedText");
        header.SetResourceReference(MarginProperty, "SpaceBXS");
        status.SetResourceReference(MarginProperty, "SpaceBXS");

        var scroll = new ScrollViewer
        {
            Content = list, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        };
        var panel = new MaPanel { Title = "Users", Content = scroll };

        var close = new Button { Content = "_Close", IsCancel = true };
        close.SetResourceReference(StyleProperty, "MaButton");
        close.Click += (_, _) => Close();
        var footer = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        footer.SetResourceReference(MarginProperty, "SpaceTS");
        footer.Children.Add(close);

        var top = new StackPanel();
        top.Children.Add(header);
        top.Children.Add(status);

        var dock = new DockPanel();
        dock.SetResourceReference(MarginProperty, "SpaceL");
        DockPanel.SetDock(top, Dock.Top);
        DockPanel.SetDock(footer, Dock.Bottom);
        dock.Children.Add(top);
        dock.Children.Add(footer);
        dock.Children.Add(panel);
        Content = dock;

        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) { e.Handled = true; Close(); } };
        model.LoginTabs.CollectionChanged += OnLoginsChanged;
        model.AllTerminals.Panes.CollectionChanged += OnPanesChanged;
        foreach (var tab in model.LoginTabs) Watch(tab);
        foreach (var pane in model.AllTerminals.Panes) pane.PropertyChanged += OnPaneChanged;
        Loaded += (_, _) => ShowRows();
        Closed += (_, _) =>
        {
            model.LoginTabs.CollectionChanged -= OnLoginsChanged;
            model.AllTerminals.Panes.CollectionChanged -= OnPanesChanged;
            foreach (var tab in watched) tab.PropertyChanged -= OnTabChanged;
            watched.Clear();
            foreach (var pane in model.AllTerminals.Panes) pane.PropertyChanged -= OnPaneChanged;
        };
    }

    /// <summary>What a row shows (M.ORCH-T2210): the lock has no notification of its own, but <see cref="MainViewModel.CanAddCli"/>
    /// is raised with it in every tab-status raise, so it stands in for <see cref="MainViewModel.IsLoginLocked"/>.</summary>
    private static readonly HashSet<string> ShownProperties =
    [
        nameof(MainViewModel.Tab), nameof(MainViewModel.TabTitle), nameof(MainViewModel.IsTabSignedIn), nameof(MainViewModel.IsApiKeyTab),
        nameof(MainViewModel.CanAddCli), nameof(MainViewModel.AddCliLockedTip), nameof(MainViewModel.TabTrailing), nameof(MainViewModel.TabCliCount),
    ];

    private void Watch(MainViewModel tab)
    {
        if (watched.Add(tab)) tab.PropertyChanged += OnTabChanged;
    }

    private void OnLoginsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        foreach (var tab in watched.Where(t => !model.LoginTabs.Contains(t)).ToList()) { tab.PropertyChanged -= OnTabChanged; watched.Remove(tab); }
        foreach (var tab in model.LoginTabs) Watch(tab);
        ShowRows();
    }

    private void OnTabChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is null || ShownProperties.Contains(e.PropertyName)) ShowRowsSoon();
    }

    private void OnPanesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null) foreach (TerminalPaneViewModel pane in e.OldItems) pane.PropertyChanged -= OnPaneChanged;
        if (e.NewItems is not null) foreach (TerminalPaneViewModel pane in e.NewItems) pane.PropertyChanged += OnPaneChanged;
        ShowRowsSoon();
    }

    private void OnPaneChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is null or nameof(TerminalPaneViewModel.Ended)) ShowRowsSoon();
    }

    /// <summary>One rebuild per burst (M.ORCH-T2210): a tab-status raise fires dozens of properties at once.</summary>
    private void ShowRowsSoon()
    {
        if (showQueued) return;
        showQueued = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Background, () => { showQueued = false; if (IsLoaded) ShowRows(); });
    }

    private IEnumerable<MainViewModel> Logins => model.LoginTabs.Where(t => !t.IsApiKeyTab && t.UsersEmail is not null);

    /// <summary>Rebuilds the rows when anything they show changed, so a property burst never rebuilds a row under the pointer for nothing.</summary>
    private void ShowRows()
    {
        var logins = Logins.ToList();
        var state = string.Join("\n", logins.Select(t =>
            $"{t.UsersEmail}|{t.TabTitle}|{t.IsTabSignedIn}|{t.IsLoginLocked}|{t.TabTrailing}|{t.RunningCliTitles().Count}|{t.DeleteLoginCommand.CanExecute(null)}"));
        if (state == shownState) return;
        shownState = state;
        list.Children.Clear();
        foreach (var tab in logins) list.Children.Add(Row(tab));
        status.Text = $"{logins.Count} login{(logins.Count == 1 ? "" : "s")}.";
    }

    private Grid Row(MainViewModel tab)
    {
        var email = tab.UsersEmail!;
        var running = tab.RunningCliTitles();
        var row = new Grid { Margin = new Thickness(0, 3, 0, 3) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var text = new StackPanel();
        text.Children.Add(new TextBlock { Text = tab.TabTitle, FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis });
        var facts = new List<string> { email, tab.IsTabSignedIn ? "signed in" : "signed out" };
        if (tab.IsLoginLocked) facts.Add("locked");
        if (tab.TabTrailing is { Length: > 0 } usage) facts.Add(usage);
        if (running.Count > 0) facts.Add($"{running.Count} running CLI{(running.Count == 1 ? "" : "s")}");
        var detail = new TextBlock { Text = string.Join(" · ", facts), TextTrimming = TextTrimming.CharacterEllipsis };
        detail.SetResourceReference(StyleProperty, "MaMutedText");
        text.Children.Add(detail);
        Grid.SetColumn(text, 0);
        row.Children.Add(text);

        var locked = tab.IsLoginLocked;
        var lockButton = RowButton(locked ? "Unlock" : "Lock", 1,
            locked ? "Lets this login open a CLI and take dispatched or moved work again" : "Stops this login opening a CLI or taking dispatched or moved work until it's unlocked again",
            $"{(locked ? "Unlock" : "Lock")} {email}");
        lockButton.IsEnabled = tab.ToggleLockCommand.CanExecute(null);
        lockButton.Click += (_, _) => { if (tab.ToggleLockCommand.CanExecute(null)) tab.ToggleLockCommand.Execute(null); };
        row.Children.Add(lockButton);

        var delete = RowButton("Delete…", 2, DeleteTip(tab, running), $"Delete {email}");
        ToolTipService.SetShowOnDisabled(delete, true);
        delete.IsEnabled = tab.ScopeEmail is not null && running.Count == 0 && tab.DeleteLoginCommand.CanExecute(null);
        delete.Click += (_, _) => Delete(tab);
        row.Children.Add(delete);
        return row;
    }

    private static string DeleteTip(MainViewModel tab, IReadOnlyList<string> running) =>
        tab.ScopeEmail is null ? "The global login can't be deleted: every CLI without a login of its own runs on it"
        : running.Count > 0 ? $"Can't delete while it has running CLIs ({string.Join(", ", running)}): end or move them first"
        : "Removes this member: closes its Launcher and General Prompt, takes it off the member roster (orchestrator members remove) and moves its login folder to .claude-accounts\\.deleted as a dated backup";

    private Button RowButton(string label, int column, string tip, string name)
    {
        var button = new Button { Content = label, VerticalAlignment = VerticalAlignment.Center, ToolTip = tip, Margin = new Thickness(8, 0, 0, 0) };
        button.SetResourceReference(StyleProperty, "MaButton");
        AutomationProperties.SetName(button, name);
        Grid.SetColumn(button, column);
        return button;
    }

    /// <summary>Delete, re-checking running CLIs at the click (one may have opened since the row was drawn): refused with a
    /// themed message naming them, else the login's own Delete, whose confirm names its folder, roster entry and backup.</summary>
    private void Delete(MainViewModel tab)
    {
        var running = tab.RunningCliTitles();
        if (running.Count > 0)
        {
            MaMessageBox.Show(this,
                $"{tab.TabTitle} ({tab.UsersEmail}) has {running.Count} running CLI{(running.Count == 1 ? "" : "s")}:\n\n{string.Join("\n", running)}\n\n" +
                "Deleting it would end them without a quicksave. End them, or move their work to another login, then delete it.",
                "Login in use", MessageBoxButton.OK, MessageBoxImage.Warning);
            shownState = "";
            ShowRows();
            return;
        }
        if (tab.DeleteLoginCommand.CanExecute(null)) tab.DeleteLoginCommand.Execute(null);
    }
}
