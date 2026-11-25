using System.Windows.Input;
using MindAttic.Components.Wpf.Mvvm;

namespace MindAttic.Orchestrator.Wpf.ViewModels;

/// <summary>The menu bar's Notifications button (M.ORCH-T2241): a badge and the list it opens, the toast dots' replacement
/// since the per-login home page that drew them (the old <c>ChartsHeader</c>/<c>ToastDots</c>) was deleted entirely
/// (M.ORCH-T2238) - the toasts kept arriving from their ~20 call sites with nowhere left to show. Built from
/// <see cref="AllToasts"/>, every tab's merged, newest first and capped (M.ORCH-T1574); opening a row goes to its CLI's
/// tab or ticket and removes it everywhere (<see cref="OpenToastCommand"/>), the same as the old toast dots did.</summary>
public sealed partial class MainViewModel
{
    private bool isNotificationsOpen;
    private ICommand? toggleNotificationsCommand, closeNotificationsCommand;

    /// <summary>Whether the notifications list is open; the same on every tab.</summary>
    public bool IsNotificationsOpen { get => Root.isNotificationsOpen; set { Root.isNotificationsOpen = value; RaiseOnEveryTab(nameof(IsNotificationsOpen)); } }

    /// <summary>The menu bar's Notifications button: opens the list, or closes it.</summary>
    public ICommand ToggleNotificationsCommand => Root.toggleNotificationsCommand ??= new RelayCommand(() => Root.IsNotificationsOpen = !Root.IsNotificationsOpen);

    /// <summary>Closes the list (its close button, Esc, the backdrop).</summary>
    public ICommand CloseNotificationsCommand => Root.closeNotificationsCommand ??= new RelayCommand(() => Root.IsNotificationsOpen = false);

    /// <summary>How many toasts are up.</summary>
    public int NotificationsCount => AllToasts.Count;

    /// <summary>Whether any toast is up.</summary>
    public bool HasNotifications => NotificationsCount > 0;

    /// <summary>The count on the Notifications button: empty with none up, "99+" past 99 (<see cref="BadgeOf"/>, shared with
    /// the Inbox button).</summary>
    public string NotificationsBadge => BadgeOf(NotificationsCount);

    /// <summary>The list's title, with its count.</summary>
    public string NotificationsTitle => NotificationsCount > 0 ? $"Notifications ({NotificationsCount})" : "Notifications";

    /// <summary>Re-reads the badge and title on every tab after <see cref="AllToasts"/> changes.</summary>
    private void RaiseNotificationCounts()
    {
        foreach (var name in new[] { nameof(NotificationsCount), nameof(HasNotifications), nameof(NotificationsBadge), nameof(NotificationsTitle) })
            RaiseOnEveryTab(name);
    }
}
