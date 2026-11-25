using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using MindAttic.Orchestrator.Sessions;

namespace MindAttic.Orchestrator.Wpf;

public partial class MainWindow
{
    /// <summary>The menu bar's top-level labels (M.ORCH-T2100), the only home of the window's tools since the icon strip went
    /// (M.ORCH-T2101): each opens its dropdown under itself. The Dashboard menu holds the window tools, bound to the global
    /// login's tab (<see cref="model"/>), its Inbox item carrying the count the strip's badge did; the rest act on the
    /// selected login (<see cref="Shown"/>).</summary>
    private void MainMenu_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string name, Content: string label } button) return;
        e.Handled = true;
        var menu = DropdownMenu(button, label, PlacementMode.Bottom);
        var shown = Shown;
        switch (name)
        {
            case "Dashboard":
                menu.Items.Add(MenuAction("", "Team Wall", () => TeamWallButton_Click(button, e)));
                menu.Items.Add(MenuCommand("", model.HasInbox ? $"Inbox ({model.InboxBadge})" : "Inbox", model.ToggleInboxCommand));
                menu.Items.Add(MenuCommand("", "Plans", model.TogglePlansCommand));
                menu.Items.Add(MenuCommand("", "Open Tickets", model.OpenTicketsAppCommand));
                menu.Items.Add(MenuCommand("", "User Management", model.ToggleLoginsCommand));
                menu.Items.Add(MenuCommand("", "Settings", model.OpenSettingsCommand));
                menu.Items.Add(DropdownSeparator());
                menu.Items.Add(MenuAction("", "Quick Save All", async () => await RunQuickAllAsync(isSave: true)));
                menu.Items.Add(MenuCommand("", "Quicksave shown CLI", model.QuicksaveShownCommand));
                menu.Items.Add(MenuAction("", "Quick Load All", async () => await RunQuickAllAsync(isSave: false)));
                break;
            case "Projects":
                menu.Items.Add(MenuAction("", "Open Project…", () => ShowOpenProjectWindow(shown), shown.IsTabSignedIn));
                break;
            case "Git":
                menu.Items.Add(MenuAction("", "Commit and push projects…", OpenCommitProjects));
                menu.Items.Add(MenuAction("", "Refresh projects", () => _ = shown.RefreshProjectListAsync(), shown.IsTabSignedIn));
                menu.Items.Add(MenuAction("", "Commit + push all dirty projects…", () => ToolbarCommitAllButton_Click(button, e), shown.IsTabSignedIn));
                break;
            case "Settings":
                menu.Items.Add(MenuCommand("", "Vault", shown.OpenSettingsCommand));
                menu.Items.Add(MenuAction("", "Users…", OpenUsers));
                menu.Items.Add(MenuCommand("", "Launcher settings", shown.OpenLauncherSettingsCommand));
                var project = MenuCommand("", "Project settings", shown.OpenProjectSettingsCommand);
                project.IsEnabled = shown.Terminals.SelectedPane is not null;
                menu.Items.Add(project);
                menu.Items.Add(DropdownSeparator());
                var providers = LauncherProviders.All();
                menu.Items.Add(ProviderMenu(providers));
                menu.Items.Add(ModelMenu(providers));
                break;
            case "Launcher":
                menu.Items.Add(MenuCommand("", "Run Launcher", shown.RunLauncherCommand));
                menu.Items.Add(MenuAction("", "Bootstrap this machine…", () => ToolbarBootstrapButton_Click(button, e)));
                break;
            case "Backup":
                menu.Items.Add(MenuAction("", "Backup & Restore…", () => ToolbarBackupRestoreButton_Click(button, e)));
                break;
            case "Azure":
                menu.Items.Add(MenuAction("", "Kickstart web apps…", OpenKickstart));
                menu.Items.Add(MenuAction("", "Export Azure SQL DB…", OpenAzureExport));
                break;
            case "Shells":
                menu.Items.Add(MenuAction("", "Command Prompt (Admin)", () => OpenAdminShell("cmd.exe")));
                menu.Items.Add(MenuAction("", "PowerShell (Admin)", () => OpenAdminShell("powershell.exe")));
                break;
            default:
                return;
        }
        menu.IsOpen = true;
    }

    /// <summary>The Settings menu's Provider submenu (M.ORCH-T2106): the Launcher's providers, the one Add opens General Prompt
    /// and projects on checked.</summary>
    private static MenuItem ProviderMenu(IReadOnlyList<LauncherProviders.Provider> providers)
    {
        var root = DropdownItem("", "Provider");
        root.ToolTip = "Which agent CLI Add opens General Prompt and projects on";
        foreach (var p in providers)
        {
            var item = DropdownItem("", p.Name == p.Key ? p.Key : $"{p.Key} ({p.Name})");
            item.IsCheckable = true;
            item.IsChecked = p.Key.Equals(OpenProjectWindow.ProviderKey, StringComparison.OrdinalIgnoreCase);
            item.Click += (_, e) => { e.Handled = true; OpenProjectWindow.ProviderKey = p.Key; };
            root.Items.Add(item);
        }
        return root;
    }

    /// <summary>The Settings menu's Model submenu (M.ORCH-T2106): a submenu per provider with its current model checked, the
    /// known ones and the CLI's default, a pick written into that provider's Launcher RunCommand as the Launcher's own
    /// Settings model picker does (its <c>host</c> has no <c>--model</c> of its own).</summary>
    private static MenuItem ModelMenu(IReadOnlyList<LauncherProviders.Provider> providers)
    {
        var root = DropdownItem("", "Model");
        root.ToolTip = "The model each provider runs: saved to its Launcher command";
        foreach (var p in providers)
        {
            var sub = DropdownItem("", p.Key);
            foreach (var m in LauncherProviders.ModelsFor(p)) sub.Items.Add(ModelItem(p, m, m));
            sub.Items.Add(DropdownSeparator());
            sub.Items.Add(ModelItem(p, "(CLI default)", null));
            root.Items.Add(sub);
        }
        return root;
    }

    private static MenuItem ModelItem(LauncherProviders.Provider p, string label, string? model)
    {
        var item = DropdownItem("", label);
        item.IsCheckable = true;
        item.IsChecked = string.Equals(model, p.Model, StringComparison.OrdinalIgnoreCase);
        item.Click += (_, e) =>
        {
            e.Handled = true;
            if (string.Equals(model, p.Model, StringComparison.OrdinalIgnoreCase)) return;
            if (LauncherProviders.SetModel(p.Key, model) is { } error)
                OrchestratorLog.For("Dashboard").Warning("Settings menu: {Error}", error);
        };
        return item;
    }

    /// <summary>Settings &gt; Users (M.ORCH-T2184): every login, with Lock/Unlock and the only Delete. One window at a time.</summary>
    private void OpenUsers()
    {
        if (OwnedWindows.OfType<UsersWindow>().FirstOrDefault() is { } open) { open.Activate(); return; }
        try { new UsersWindow(model) { Owner = this }.Show(); }
        catch (Exception ex) when (ex is not OutOfMemoryException) { OrchestratorLog.For("Dashboard").Error(ex, "Opening Users failed; contained"); }
    }

    private static MenuItem MenuAction(string icon, string label, Action act, bool enabled = true)
    {
        var item = DropdownItem(icon, label);
        item.IsEnabled = enabled;
        item.Click += (_, _) => act();
        return item;
    }

    private static MenuItem MenuCommand(string icon, string label, ICommand command)
    {
        var item = DropdownItem(icon, label);
        item.Command = command;
        return item;
    }
}
