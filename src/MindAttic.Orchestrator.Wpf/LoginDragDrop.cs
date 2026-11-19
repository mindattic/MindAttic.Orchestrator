using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MindAttic.Components.Wpf.Controls;
using MindAttic.Orchestrator.Wpf.ViewModels;

namespace MindAttic.Orchestrator.Wpf;

/// <summary>
/// Ctrl+drag a CLI tab, the Launcher's own tab, a login's own header, or (M.ORCH-T1768 note 6) any of the tree sidebar's
/// own equivalents, and drop it onto another login's row - in the login strip or the tree - to switch to it (M.ORCH-T1757):
/// the same <see cref="MainViewModel.StripSelection"/> a click or Ctrl+Tab already use (<c>MainViewModel.Tabs.cs</c>), so
/// "continues where it left off" needs no new state - every login's tab is already a long-lived object kept alive the
/// whole time.
/// <para>Ctrl is required at mouse-down so this never fights <see cref="MaTabControl.AllowReorder"/>'s own same-strip,
/// mouse-capture reorder drag (on the CLI/Launcher strip, and its tree counterpart, MainWindow.xaml.cs's own
/// LoginTreeChildRow_PreviewMouseLeftButtonDown): without Ctrl held, nothing here runs and that gesture is
/// untouched. With it, the window's own tunneling PreviewMouseLeftButtonDown (ahead of <c>MaTabItem</c>'s own handling of the
/// same event in the tunnel) marks the press handled, so the reorder drag never starts for it, and a plain WPF
/// <see cref="DragDrop"/> runs instead - a separate gesture that can cross from one control into another, which a
/// mouse-capture drag confined to its own strip cannot.</para>
/// </summary>
internal static class LoginDragDrop
{
    private const string Format = "MindAttic.Orchestrator.LoginDrag";

    /// <param name="loginTree">The tree sidebar's own root ItemsControl (M.ORCH-T1768 note 6, TreeMenuLayout's default-on
    /// view, MainWindow.xaml's LoginTree): both a login's own header row and any of its CLI/Launcher pane rows under it are
    /// draggable sources and the login row itself a drop target, the tree's own equivalents of <paramref name="loginStrip"/>
    /// and <paramref name="terminalStrip"/> below - that strip only ever shows the currently-selected login's own panes, so
    /// a CLI tab's owning login there is always the window's current selection, but the tree can have several logins'
    /// panes expanded at once, so each pane row's owning login is instead found by walking up to its own nearest
    /// MainViewModel DataContext (which <see cref="ItemsControl.ContainerFromElement(ItemsControl,DependencyObject)"/>
    /// does on its own here, skipping past the nested per-login ItemsControl of panes to loginTree's own per-login
    /// container, the same way it already skips a ContentPresenter to reach a flat strip's own tab).</param>
    public static void Attach(Window window, MaTabControl loginStrip, MaTabControl terminalStrip, ItemsControl loginTree)
    {
        MainViewModel? dragging = null;
        Point? pressedAt = null;

        window.PreviewMouseLeftButtonDown += (_, e) =>
        {
            if (Keyboard.Modifiers != ModifierKeys.Control || e.OriginalSource is not DependencyObject hit) return;
            if (LoginOf(loginStrip, terminalStrip, loginTree, window, hit) is not { } login) return;
            dragging = login;
            pressedAt = e.GetPosition(window);
            e.Handled = true;
        };
        window.PreviewMouseMove += (_, e) =>
        {
            if (dragging is not { } login || pressedAt is not { } from || e.LeftButton != MouseButtonState.Pressed) return;
            var now = e.GetPosition(window);
            if (Math.Abs(now.X - from.X) < SystemParameters.MinimumHorizontalDragDistance
                && Math.Abs(now.Y - from.Y) < SystemParameters.MinimumVerticalDragDistance) return;
            dragging = null;
            pressedAt = null;
            DragDrop.DoDragDrop(window, new DataObject(Format, login), DragDropEffects.Move);
        };
        window.PreviewMouseLeftButtonUp += (_, _) => { dragging = null; pressedAt = null; };

        void AttachDropTarget(ItemsControl strip)
        {
            strip.AllowDrop = true;
            FrameworkElement? hovered = null;
            void ClearHighlight() { if (hovered is { } h) h.Opacity = 1; hovered = null; }
            strip.PreviewDragOver += (_, e) =>
            {
                // Any other drag (a tree CLI row's reorder/transfer, M.ORCH-T1849) is left to its own handlers.
                if (!e.Data.GetDataPresent(Format)) return;
                e.Effects = DragDropEffects.Move;
                e.Handled = true;
                var over = e.OriginalSource is DependencyObject hit && ItemsControl.ContainerFromElement(strip, hit) is FrameworkElement row ? row : null;
                if (ReferenceEquals(over, hovered)) return;
                ClearHighlight();
                if (over is { } row2) { row2.Opacity = 0.65; hovered = row2; }
            };
            strip.PreviewDragLeave += (_, _) => ClearHighlight();
            strip.PreviewDrop += (_, e) =>
            {
                if (!e.Data.GetDataPresent(Format)) return;
                ClearHighlight();
                e.Handled = true;
                if (e.Data.GetData(Format) is not MainViewModel dragged || e.OriginalSource is not DependencyObject dropped) return;
                if (ItemsControl.ContainerFromElement(strip, dropped) is not FrameworkElement { DataContext: MainViewModel target }) return;
                if (ShouldSwitch(dragged, target)) target.StripSelection = target;
            };
        }
        AttachDropTarget(loginStrip);
        AttachDropTarget(loginTree);
    }

    /// <summary>The login <paramref name="hit"/> belongs to, when it's one of the draggable kinds: a login's own header in
    /// <paramref name="loginStrip"/> or <paramref name="loginTree"/>, a CLI/Launcher tab in <paramref name="terminalStrip"/>
    /// (whichever login's page that strip is showing right now, <paramref name="window"/>'s own DataContext, the only
    /// login whose tabs it can ever show at once), or one of <paramref name="loginTree"/>'s own CLI/Launcher pane rows
    /// (its actual owning login, found the same way regardless of which other login is currently selected). Null for
    /// anything else, so a plain Ctrl+click elsewhere in the window never starts this.</summary>
    private static MainViewModel? LoginOf(MaTabControl loginStrip, MaTabControl terminalStrip, ItemsControl loginTree, Window window, DependencyObject hit)
    {
        if (ItemsControl.ContainerFromElement(loginStrip, hit) is FrameworkElement { DataContext: MainViewModel login }) return login;
        if (ItemsControl.ContainerFromElement(terminalStrip, hit) is FrameworkElement { DataContext: TerminalPaneViewModel })
            return window.DataContext as MainViewModel;
        if (ItemsControl.ContainerFromElement(loginTree, hit) is FrameworkElement { DataContext: MainViewModel treeLogin }) return treeLogin;
        return null;
    }

    /// <summary>Whether dropping <paramref name="dragged"/>'s login onto <paramref name="target"/>'s row should switch to it
    /// (M.ORCH-T1757): any login but the one it was dragged from - dropping a login back onto its own row is a no-op, the
    /// same as <c>MainViewModel.Tabs.cs</c>'s own <c>Select()</c> already treats clicking the tab you're already on.</summary>
    internal static bool ShouldSwitch(MainViewModel dragged, MainViewModel target) => !ReferenceEquals(dragged, target);
}
