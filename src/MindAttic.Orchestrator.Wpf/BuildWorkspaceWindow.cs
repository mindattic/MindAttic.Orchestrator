using System.IO;
using System.Windows;
using System.Windows.Controls;
using MindAttic.Components.Wpf.Controls;
using MindAttic.Orchestrator.Setup;

namespace MindAttic.Orchestrator.Wpf;

/// <summary>The first run on a new machine: clones the workspace into a folder and shows each repo as it lands. Built from
/// MindAttic.Components.Wpf (M.ORCH-T499, M.ORCH-T531): the dark title bar and chrome, MaPrimaryButton, an MaModal for the
/// one question it asks, the log in an MaTextArea (no wrapping, so long lines scroll) and an MaProgressBar.</summary>
public sealed class BuildWorkspaceWindow : Window
{
    private readonly string folder;
    private readonly Serilog.ILogger log;
    private readonly TextBox output = new()
    {
        IsReadOnly = true, FontFamily = new System.Windows.Media.FontFamily("Consolas"), FontSize = 12,
    };
    private readonly ProgressBar bar = new() { IsIndeterminate = true, Height = 4, Margin = new Thickness(0, 8, 0, 8) };
    private readonly Button done = new() { Content = "Working…", IsEnabled = false, Padding = new Thickness(20, 6, 20, 6), HorizontalAlignment = HorizontalAlignment.Right };
    private readonly MaModal question = new() { DialogWidth = 460 };
    private readonly Func<IProgress<string>, Task<BootstrapResult>> clone;
    private TaskCompletionSource<bool>? answer;
    private bool succeeded;

    /// <param name="clone">The clone itself; tests give a stand-in, so they clone nothing.</param>
    internal BuildWorkspaceWindow(string folder, Serilog.ILogger log, Func<IProgress<string>, Task<BootstrapResult>>? clone = null)
    {
        this.folder = folder;
        this.log = log;
        this.clone = clone ?? (progress => WorkspaceBootstrap.BuildAsync(folder, WorkspaceBootstrap.DefaultRootRemote, progress));
        Title = "Building the MindAttic workspace";
        Width = 720;
        Height = 480;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        MaWindow.SetDarkTitleBar(this, true);
        SetResourceReference(BackgroundProperty, "MaChrome");
        SetResourceReference(ForegroundProperty, "MaText");
        output.SetResourceReference(StyleProperty, "MaTextArea");
        output.TextWrapping = TextWrapping.NoWrap;
        bar.SetResourceReference(StyleProperty, "MaProgressBar");
        done.SetResourceReference(StyleProperty, "MaPrimaryButton");
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
        question.Closed += (_, _) => answer?.TrySetResult(false);
        // The window's X at the question is no as well (M.ORCH-T555): nothing else would ever answer it.
        Closed += (_, _) => answer?.TrySetResult(false);
        var root = new Grid();
        root.Children.Add(panel);
        root.Children.Add(question);
        Content = root;
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

    /// <summary>Whether the workspace is there to use (what <see cref="Run"/> returns).</summary>
    internal bool Succeeded => succeeded;

    /// <summary>Whether its question is open.</summary>
    internal bool Asking => question.IsOpen;

    private void Say(string line)
    {
        output.AppendText(line + Environment.NewLine);
        output.ScrollToEnd();
    }

    /// <summary>Asks a yes/no question in an MaModal over the log; Esc, the close button or the backdrop answer no.</summary>
    private Task<bool> AskAsync(string title, string detail)
    {
        answer = new TaskCompletionSource<bool>();
        var no = new Button { Content = "_No", Margin = new Thickness(0, 0, 8, 0) };
        var yes = new Button { Content = "_Yes", IsDefault = true };
        no.SetResourceReference(StyleProperty, "MaButton");
        yes.SetResourceReference(StyleProperty, "MaPrimaryButton");
        no.Click += (_, _) => Answer(false);
        yes.Click += (_, _) => Answer(true);
        var footer = new StackPanel { Orientation = Orientation.Horizontal };
        footer.Children.Add(no);
        footer.Children.Add(yes);
        question.Title = title;
        question.Content = new TextBlock { Text = detail, TextWrapping = TextWrapping.Wrap };
        question.Footer = footer;
        question.IsOpen = true;
        return answer.Task;
    }

    private void Answer(bool yes)
    {
        answer?.TrySetResult(yes);
        question.IsOpen = false;
    }

    internal async Task BuildAsync()
    {
        try
        {
            var r = await clone(new Progress<string>(Say));
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
            // Done from here: the repos are there whatever's answered about the apps, and the window may be closed at
            // that question or while they install (M.ORCH-T555).
            succeeded = true;
            var apps = WorkspaceBootstrap.Apps(folder);
            if (apps.Count > 0 && await AskAsync($"Also install the {apps.Count} apps that have a deploy script?", string.Join(", ", apps)))
            {
                Say("");
                var appsFailed = await WorkspaceBootstrap.InstallAppsAsync(folder, new Progress<string>(Say));
                Say(appsFailed.Count == 0 ? "All apps installed." : $"{appsFailed.Count} app(s) failed:");
                foreach (var f in appsFailed) Say("  " + f);
                log.Information("Installed {Count} apps, {Failed} failed", apps.Count, appsFailed.Count);
            }
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
