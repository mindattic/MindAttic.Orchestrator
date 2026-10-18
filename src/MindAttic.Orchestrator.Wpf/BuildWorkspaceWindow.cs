using System.IO;
using System.Windows;
using System.Windows.Controls;
using MindAttic.Orchestrator.Setup;

namespace MindAttic.Orchestrator.Wpf;

/// <summary>The first run on a new machine: clones the workspace into a folder and shows each repo as it lands.</summary>
public sealed class BuildWorkspaceWindow : Window
{
    private readonly string folder;
    private readonly Serilog.ILogger log;
    private readonly TextBox output = new()
    {
        IsReadOnly = true, FontFamily = new System.Windows.Media.FontFamily("Consolas"), FontSize = 12,
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto, TextWrapping = TextWrapping.NoWrap,
    };
    private readonly ProgressBar bar = new() { IsIndeterminate = true, Height = 4, Margin = new Thickness(0, 8, 0, 8) };
    private readonly Button done = new() { Content = "Working…", IsEnabled = false, Padding = new Thickness(20, 6, 20, 6), HorizontalAlignment = HorizontalAlignment.Right };
    private bool succeeded;

    private BuildWorkspaceWindow(string folder, Serilog.ILogger log)
    {
        this.folder = folder;
        this.log = log;
        Title = "Building the MindAttic workspace";
        Width = 720;
        Height = 480;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        var panel = new DockPanel { Margin = new Thickness(16) };
        var header = new TextBlock { Text = $"Cloning into {folder}", FontSize = 15, Margin = new Thickness(0, 0, 0, 4) };
        DockPanel.SetDock(header, Dock.Top);
        DockPanel.SetDock(bar, Dock.Top);
        DockPanel.SetDock(done, Dock.Bottom);
        done.Margin = new Thickness(0, 10, 0, 0);
        done.Click += (_, _) => { DialogResult = succeeded; };
        panel.Children.Add(header);
        panel.Children.Add(bar);
        panel.Children.Add(done);
        panel.Children.Add(output);
        Content = panel;
        Loaded += async (_, _) => await BuildAsync();
    }

    /// <summary>True when the workspace is there to use (some repos may have failed; they're listed and can be retried).</summary>
    /// Closed with the title bar's X once the build is done counts as done too: the repos are there either way.
    public static bool Run(string folder, Serilog.ILogger log)
    {
        var window = new BuildWorkspaceWindow(folder, log);
        window.ShowDialog();
        return window.succeeded;
    }

    private void Say(string line)
    {
        output.AppendText(line + Environment.NewLine);
        output.ScrollToEnd();
    }

    private async Task BuildAsync()
    {
        try
        {
            var r = await WorkspaceBootstrap.BuildAsync(folder, WorkspaceBootstrap.DefaultRootRemote, new Progress<string>(Say));
            Say("");
            Say($"Cloned {r.Cloned}, {r.AlreadyThere} already there, {r.Failed.Count} failed.");
            foreach (var f in r.Failed) Say("  " + f);
            // The dashboard only builds an empty folder, and a machine set up from the exe has no orchestrator CLI: the retry
            // that works anywhere is git itself, one line per repo that failed.
            if (r.Failed.Count > 0 && WorkspaceBootstrap.Load(folder) is { } manifest)
            {
                Say("To retry one later:");
                foreach (var repo in manifest.Repos.Where(m => r.Failed.Any(f => f.StartsWith(m.Name + ":", StringComparison.Ordinal))))
                    Say($"  git clone {repo.Remote} \"{Path.Combine(folder, repo.Name)}\"");
            }
            log.Information("Built the workspace in {Folder:l}: cloned {Cloned}, failed {Failed}", folder, r.Cloned, r.Failed.Count);
            var apps = WorkspaceBootstrap.Apps(folder);
            if (apps.Count > 0 && MessageBox.Show(this, $"Also install the {apps.Count} apps that have a deploy script?\n\n{string.Join(", ", apps)}",
                    "MindAttic Orchestrator", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                Say("");
                var appsFailed = await WorkspaceBootstrap.InstallAppsAsync(folder, new Progress<string>(Say));
                Say(appsFailed.Count == 0 ? "All apps installed." : $"{appsFailed.Count} app(s) failed:");
                foreach (var f in appsFailed) Say("  " + f);
                log.Information("Installed {Count} apps, {Failed} failed", apps.Count, appsFailed.Count);
            }
            succeeded = true;
            done.Content = "Open the dashboard";
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            Say("");
            Say(ex.Message);
            log.Error(ex, "Building the workspace in {Folder:l} failed", folder);
            done.Content = "Close";
        }
        bar.IsIndeterminate = false;
        bar.Value = bar.Maximum;
        done.IsEnabled = true;
    }
}
