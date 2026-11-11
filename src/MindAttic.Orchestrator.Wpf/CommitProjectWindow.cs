using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MindAttic.Components.Wpf.Controls;
using MindAttic.Orchestrator.Sessions;

namespace MindAttic.Orchestrator.Wpf;

/// <summary>
/// "Commit…" on one project (M.ORCH-T1588), from a right-click on a project in a login tab's Open Project tree: a commit
/// message, Save and Cancel. Save commits that project's whole working tree under the message, pulls --rebase and pushes it
/// (<see cref="ProjectCommit.CommitAndPush"/>), then says how it went. A window of its own, since the terminals' native
/// windows would draw over an in-window modal.
/// </summary>
internal sealed class CommitProjectWindow : Window
{
    private readonly RosterProject project;
    private readonly TextBox message = new() { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 72, MaxHeight = 160,
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    private readonly TextBlock status = new() { TextWrapping = TextWrapping.Wrap };
    private readonly ProgressBar bar = new() { Height = 4 };
    private readonly Button cancel = new() { Content = "_Cancel", IsCancel = true };
    private readonly Button save = new() { Content = "_Save", IsDefault = true };
    private bool running;
    private bool finished;

    internal CommitProjectWindow(RosterProject project)
    {
        this.project = project;
        Title = $"Commit {project.Name}";
        Width = 560;
        SizeToContent = SizeToContent.Height;
        MinWidth = 380;
        ResizeMode = ResizeMode.CanResizeWithGrip;
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        MaWindow.SetThemedTitleBar(this, true);
        SetResourceReference(BackgroundProperty, "MaChrome");
        SetResourceReference(ForegroundProperty, "MaText");

        var prompt = new TextBlock
        {
            Text = $"Commit message for {project.Name}. Save commits its whole working tree, pulls --rebase and pushes it.",
            TextWrapping = TextWrapping.Wrap,
            ToolTip = project.Path,
        };
        prompt.SetResourceReference(StyleProperty, "MaMutedText");
        prompt.SetResourceReference(MarginProperty, "SpaceBXS");
        message.SetResourceReference(StyleProperty, "MaTextArea");
        message.SetResourceReference(MarginProperty, "SpaceBXS");
        message.TextChanged += (_, _) => UpdateSave();
        System.Windows.Automation.AutomationProperties.SetName(message, "Commit message");
        // Enter saves (the default button); Shift+Enter or Ctrl+Enter is a new line.
        message.PreviewKeyDown += (_, e) =>
        {
            if (e.Key != Key.Enter || Keyboard.Modifiers != ModifierKeys.None) return;
            e.Handled = true;
            if (save.IsEnabled) _ = SaveAsync();
        };
        status.SetResourceReference(StyleProperty, "MaMutedText");
        status.Text = "Reading its git status…";
        bar.SetResourceReference(StyleProperty, "MaProgressBar");
        bar.SetResourceReference(MarginProperty, "SpaceH0VS");
        cancel.SetResourceReference(StyleProperty, "MaButton");
        cancel.SetResourceReference(MarginProperty, "SpaceRS");
        save.SetResourceReference(StyleProperty, "MaPrimaryButton");
        save.ToolTip = "Commit this project's working tree under the message, pull --rebase and push it";

        var footer = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        footer.SetResourceReference(MarginProperty, "SpaceTS");
        footer.Children.Add(cancel);
        footer.Children.Add(save);

        var panel = new StackPanel();
        panel.SetResourceReference(MarginProperty, "SpaceL");
        panel.Children.Add(prompt);
        panel.Children.Add(message);
        panel.Children.Add(status);
        panel.Children.Add(bar);
        panel.Children.Add(footer);
        Content = panel;

        save.Click += async (_, _) =>
        {
            if (finished) Close();
            else await SaveAsync();
        };
        cancel.Click += (_, _) => Close();
        // Closing mid-run would hide the result, not stop the commit: wait for it.
        Closing += (_, e) => e.Cancel = running;
        UpdateSave();
        Loaded += async (_, _) =>
        {
            Keyboard.Focus(message);
            var n = await Task.Run(() => ProjectCommit.ChangedFiles(project.Path));
            if (running || finished) return;
            status.Text = n switch
            {
                null => "Not a git checkout git can read.",
                0 => "No changed files (unpushed commits, if any, are still pushed).",
                1 => "1 changed file.",
                _ => $"{n} changed files.",
            };
        };
    }

    /// <summary>Opens it over <paramref name="owner"/>; the commit happens inside.</summary>
    internal static void Show(Window owner, RosterProject project) => new CommitProjectWindow(project) { Owner = owner }.Show();

    private void UpdateSave() => save.IsEnabled = finished || (!running && !string.IsNullOrWhiteSpace(message.Text));

    private async Task SaveAsync()
    {
        var text = message.Text.Trim();
        if (running || finished || text.Length == 0) return;
        running = true;
        message.IsReadOnly = true;
        cancel.IsEnabled = false;
        UpdateSave();
        bar.IsIndeterminate = true;
        status.Text = "Committing and pushing…";
        ProjectCommitResult result;
        try { result = await Task.Run(() => ProjectCommit.CommitAndPush(project, text)); }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            OrchestratorLog.For("Dashboard").Error(ex, "Commit {Project:l} failed; contained", project.Name);
            result = new(project, ProjectCommitOutcome.Failed, ex.Message);
        }
        OrchestratorLog.For("Dashboard").Information("Commit {Project:l}: {Outcome} {Detail:l}", project.Name, result.Outcome, result.Detail);
        bar.IsIndeterminate = false;
        bar.Value = bar.Maximum;
        status.Text = result.Detail;
        running = false;
        if (result.Outcome == ProjectCommitOutcome.Failed)
        {
            // Let the message be fixed and saved again.
            message.IsReadOnly = false;
            cancel.IsEnabled = true;
            UpdateSave();
            return;
        }
        finished = true;
        save.Content = "Close";
        UpdateSave();
    }
}
