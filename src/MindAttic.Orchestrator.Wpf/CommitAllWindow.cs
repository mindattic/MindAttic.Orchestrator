using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MindAttic.Components.Wpf.Controls;

namespace MindAttic.Orchestrator.Wpf;

/// <summary>
/// "Commit + push all" (M.ORCH-T1590), from a right-click on the project tree's root (Open Project): one commit message for
/// every dirty project on the Launcher's roster, then each project's result as the Launcher's own <c>commit</c> prints it.
/// A blank message lets the Launcher write one per project from its status. Built from MindAttic.Components.Wpf, like
/// <see cref="BuildWorkspaceWindow"/>.
/// </summary>
public sealed class CommitAllWindow : Window
{
    private readonly TextBox message = new();
    private readonly TextBox output = new() { IsReadOnly = true, FontFamily = new System.Windows.Media.FontFamily("Consolas"), FontSize = 12 };
    private readonly ProgressBar bar = new() { Height = 4 };
    private readonly Button cancel = new() { Content = "_Cancel", IsCancel = true };
    private readonly Button commit = new() { Content = "Commit + _push all", IsDefault = true };
    private readonly Func<string?, Action<string>, Task<int>> run;
    private bool running;
    private bool finished;

    /// <param name="run">Commits and pushes every dirty project under the message, each line it prints to the callback;
    /// returns its exit code.</param>
    internal CommitAllWindow(Func<string?, Action<string>, Task<int>> run)
    {
        this.run = run;
        Title = "Commit + push every dirty project";
        Width = 760;
        Height = 480;
        MaWindow.SetThemedTitleBar(this, true);
        SetResourceReference(BackgroundProperty, "MaChrome");
        SetResourceReference(ForegroundProperty, "MaText");
        message.SetResourceReference(StyleProperty, "MaTextBox");
        output.SetResourceReference(StyleProperty, "MaTextArea");
        output.TextWrapping = TextWrapping.NoWrap;
        bar.SetResourceReference(StyleProperty, "MaProgressBar");
        cancel.SetResourceReference(StyleProperty, "MaButton");
        commit.SetResourceReference(StyleProperty, "MaPrimaryButton");
        cancel.SetResourceReference(MarginProperty, "SpaceRS");
        System.Windows.Automation.AutomationProperties.SetName(message, "Commit message");

        var prompt = new TextBlock
        {
            Text = "One commit message for every project with changes (blank: one written per project from its status). Clean projects are skipped.",
            TextWrapping = TextWrapping.Wrap,
        };
        prompt.SetResourceReference(StyleProperty, "MaMutedText");
        prompt.SetResourceReference(MarginProperty, "SpaceBXS");
        message.SetResourceReference(MarginProperty, "SpaceBXS");
        bar.SetResourceReference(MarginProperty, "SpaceH0VS");
        var footer = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        footer.SetResourceReference(MarginProperty, "SpaceTS");
        footer.Children.Add(cancel);
        footer.Children.Add(commit);

        var panel = new DockPanel();
        panel.SetResourceReference(MarginProperty, "SpaceL");
        DockPanel.SetDock(prompt, Dock.Top);
        DockPanel.SetDock(message, Dock.Top);
        DockPanel.SetDock(bar, Dock.Top);
        DockPanel.SetDock(footer, Dock.Bottom);
        panel.Children.Add(prompt);
        panel.Children.Add(message);
        panel.Children.Add(bar);
        panel.Children.Add(footer);
        panel.Children.Add(output);
        Content = panel;

        commit.Click += async (_, _) =>
        {
            if (finished) Close();
            else await CommitAsync();
        };
        cancel.Click += (_, _) => Close();
        // Closing mid-run would hide the result, not stop the commits: wait for it.
        Closing += (_, e) => e.Cancel = running;
        Loaded += (_, _) => Keyboard.Focus(message);
    }

    /// <summary>Opens it over <paramref name="owner"/>; the run happens inside.</summary>
    internal static void Show(Window owner, Func<string?, Action<string>, Task<int>> run) =>
        new CommitAllWindow(run) { Owner = owner, ShowInTaskbar = false, WindowStartupLocation = WindowStartupLocation.CenterOwner }.Show();

    private void Say(string line)
    {
        output.AppendText(line + Environment.NewLine);
        output.ScrollToEnd();
    }

    internal async Task CommitAsync()
    {
        running = true;
        message.IsReadOnly = true;
        commit.IsEnabled = false;
        cancel.IsEnabled = false;
        bar.IsIndeterminate = true;
        var text = string.IsNullOrWhiteSpace(message.Text) ? null : message.Text.Trim();
        Say(text is null ? "Committing every dirty project (messages written per project)…" : $"Committing every dirty project as \"{text}\"…");
        int exit;
        try
        {
            var dispatcher = Dispatcher;
            exit = await run(text, line => dispatcher.BeginInvoke(() => Say(line)));
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            OrchestratorLog.For("Dashboard").Error(ex, "Commit + push all failed; contained");
            Say(ex.Message);
            exit = -1;
        }
        // The output lines are queued behind this; say the outcome after them.
        await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.Background);
        Say("");
        Say(exit == 0 ? "Done: every project committed and pushed, or had nothing to commit." : "Done, with failures: see the lines above.");
        bar.IsIndeterminate = false;
        bar.Value = bar.Maximum;
        running = false;
        finished = true;
        commit.Content = "Close";
        commit.IsEnabled = true;
    }
}
