using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using MindAttic.Components.Wpf.Controls;
using MindAttic.Orchestrator.Sessions;
using MindAttic.Orchestrator.Settings;
using MindAttic.Orchestrator.Setup;

namespace MindAttic.Orchestrator.Wpf;

/// <summary>
/// A new user's first run (M.ORCH-T1545): one window that gets a fresh Windows PC from "downloaded the exe" to a working
/// dashboard, in order: the tools (git, the GitHub CLI, Claude Code), GitHub signed in, the workspace, and a Claude account
/// signed in. Each step says whether it's done and has the one button that does it; Recheck reads them all again, and the
/// dashboard can be opened at any point. Built from MindAttic.Components.Wpf: its chrome, MaCard steps with an MaStatusLight,
/// MaTextBox, MaButton and MaPrimaryButton, in an MaBodyScroll so it fits a phone-sized Remote Desktop too.
/// </summary>
public sealed class WelcomeWindow : Window
{
    private readonly Serilog.ILogger log;
    private readonly AccountDirs dirs = new();
    private readonly Step tools, github, workspace, claude;
    private readonly TextBox email = new() { MinWidth = 220 };
    private readonly Button open = new() { Content = "Open the dashboard" };
    private readonly TextBlock note = new() { TextWrapping = TextWrapping.Wrap };
    private bool busy;

    /// <summary>Whether a new user still has something to do here: no workspace yet, or no Claude account signed in.</summary>
    internal static bool Needed(AccountDirs? dirs = null) =>
        WorkspaceBootstrap.IsEmpty(WorkspaceRoot()) || !(dirs ?? new AccountDirs()).AnySignedIn;

    /// <summary>Shows the window until the user opens the dashboard (or closes it).</summary>
    internal static void Run(Serilog.ILogger log) => new WelcomeWindow(log).ShowDialog();

    private WelcomeWindow(Serilog.ILogger log)
    {
        this.log = log;
        Title = "Welcome to MindAttic Orchestrator";
        Width = 680;
        Height = 640;
        MinWidth = 320;
        MinHeight = 400;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        MaWindow.SetThemedTitleBar(this, true);
        SetResourceReference(BackgroundProperty, "MaChrome");
        SetResourceReference(ForegroundProperty, "MaText");

        tools = new Step("1. Tools", "Install missing tools", () => Busy(InstallToolsAsync));
        github = new Step("2. GitHub", "Connect GitHub", () => Busy(ConnectGitHubAsync));
        workspace = new Step("3. Workspace", "Choose…", () => Busy(ChooseWorkspaceAsync));
        claude = new Step("4. Claude account", "Sign in", () => Busy(SignInClaudeAsync));
        email.SetResourceReference(StyleProperty, "MaTextBox");
        AutomationNames(email, "Your Claude account's email");
        claude.Extra.Children.Add(email);

        var heading = new TextBlock { Text = "Let's get you set up", TextWrapping = TextWrapping.Wrap };
        heading.SetResourceReference(StyleProperty, "MaHeading");
        var intro = new TextBlock
        {
            Text = "Orchestrator runs your Claude CLIs on this PC: its memory and CPU do the work, while you drive them from here or, " +
                   "over Tailscale and Remote Desktop, from your phone (see docs/TAILSCALE.md). Four steps, each one button.",
            TextWrapping = TextWrapping.Wrap,
        };
        intro.SetResourceReference(StyleProperty, "MaMutedText");
        intro.SetResourceReference(MarginProperty, "SpaceBS");
        note.SetResourceReference(StyleProperty, "MaMutedText");

        var recheck = new Button { Content = "Recheck" };
        recheck.SetResourceReference(StyleProperty, "MaButton");
        recheck.Click += (_, _) => Refresh();
        open.SetResourceReference(StyleProperty, "MaPrimaryButton");
        open.Click += (_, _) => Close();
        var buttons = new MaStackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right };
        buttons.Children.Add(recheck);
        buttons.Children.Add(open);
        buttons.SetResourceReference(MarginProperty, "SpaceTM");

        var steps = new MaStackPanel { Spacing = 10 };
        foreach (var part in new UIElement[] { heading, intro, tools.Card, github.Card, workspace.Card, claude.Card, note, buttons }) steps.Children.Add(part);
        steps.SetResourceReference(MarginProperty, "SpaceL");
        var scroll = new ScrollViewer { Content = steps, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, PanningMode = PanningMode.VerticalOnly };
        scroll.SetResourceReference(StyleProperty, "MaBodyScroll");
        Content = scroll;
        Loaded += (_, _) => Refresh();
    }

    private static string WorkspaceRoot() => OrchestratorOptions.Load(new JsonStore()).WorkspaceRoot;

    private static void AutomationNames(FrameworkElement e, string name) => System.Windows.Automation.AutomationProperties.SetName(e, name);

    /// <summary>Reads every step again and says what's left.</summary>
    private void Refresh()
    {
        var missing = SetupTools.Missing();
        tools.Show(missing.Count == 0, missing.Count == 0 ? "git, the GitHub CLI and Claude Code are installed."
            : $"Missing: {string.Join(", ", missing.Select(t => t.Name))}. Installs them with winget and Claude Code's own installer.");
        var gh = missing.All(t => t.Exe != "gh") && SetupTools.GitHubSignedIn();
        github.Show(gh, gh ? "Signed in to GitHub: the workspace's repos clone and push as you."
            : "Sign in to GitHub in your browser, so the repos clone and your CLIs can push.");
        var root = WorkspaceRoot();
        var ws = !WorkspaceBootstrap.IsEmpty(root);
        workspace.Show(ws, ws ? $"Workspace: {root}" : "Clone the MindAttic workspace into a folder, or point at one already on this PC.");
        var signedIn = dirs.AnySignedIn;
        claude.Show(signedIn, signedIn ? "A Claude account is signed in. Add more from the dashboard's Logins."
            : "Your Claude account's email (a MindAttic Claude Team seat works as is), then Sign in: the browser asks you to confirm.");
        claude.Action.IsEnabled = !busy && missing.All(t => t.Exe != "claude");
        open.Content = ws && signedIn ? "Open the dashboard" : "Skip for now";
    }

    private async void Busy(Func<Task> work)
    {
        if (busy) return;
        busy = true;
        foreach (var step in new[] { tools, github, workspace, claude }) step.Action.IsEnabled = false;
        try { await work(); }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            log.Error(ex, "A welcome step failed");
            note.Text = $"That didn't finish: {ex.Message}";
        }
        finally
        {
            busy = false;
            foreach (var step in new[] { tools, github, workspace, claude }) step.Action.IsEnabled = true;
            Refresh();
        }
    }

    private async Task InstallToolsAsync()
    {
        var missing = SetupTools.Missing();
        if (missing.Count == 0) return;
        note.Text = "Installing in the PowerShell window that opened; it closes when done.";
        var code = await SetupTools.RunVisibleAsync(string.Join("\n", missing.Select(t => t.Install)));
        note.Text = code == 0 ? "Tools installed." : $"The installer ended with exit code {code}: see its window, then Recheck.";
    }

    private async Task ConnectGitHubAsync()
    {
        note.Text = "Signing in to GitHub in the window that opened: follow its browser prompt.";
        var code = await SetupTools.RunVisibleAsync("gh auth login --hostname github.com --git-protocol https --web\nif ($LASTEXITCODE -eq 0) { gh auth setup-git }");
        note.Text = code == 0 ? "GitHub connected." : "GitHub isn't connected yet: try again, or run gh auth login yourself.";
    }

    private Task ChooseWorkspaceAsync()
    {
        note.Text = App.EnsureWorkspace(log) ? "" : "No workspace chosen yet.";
        return Task.CompletedTask;
    }

    private async Task SignInClaudeAsync()
    {
        if (AccountDirs.Normalize(email.Text) is not { } address)
        {
            note.Text = "Type your Claude account's whole email first.";
            return;
        }
        note.Text = $"Opening {address}'s sign-in in a private browser window: sign in there as {address}…";
        var result = await Task.Run(() => OwnLogins.SignIn(dirs, address, sso: false, minting: new TokenMinting(LoginTokens.Default)));
        log.Information("Welcome sign-in for {Email:l}: {Outcome}", address, result.Outcome);
        note.Text = result.Token is { } token ? $"{result.Message} {token.Message}" : result.Message;
    }

    /// <summary>One step's card: its light, title, what it means now, and its button.</summary>
    private sealed class Step
    {
        public Border Card { get; } = new();
        public Button Action { get; } = new();
        public MaStackPanel Extra { get; } = new() { Orientation = Orientation.Horizontal, Spacing = 8 };
        private readonly MaStatusLight light = new() { VerticalAlignment = VerticalAlignment.Center };
        private readonly TextBlock detail = new() { TextWrapping = TextWrapping.Wrap };
        private readonly string title;

        public Step(string title, string action, Action run)
        {
            this.title = title;
            Card.SetResourceReference(StyleProperty, "MaCard");
            Action.Content = action;
            Action.SetResourceReference(StyleProperty, "MaPrimaryButton");
            Action.Click += (_, _) => run();
            AutomationNames(Action, $"{title}: {action}");
            detail.SetResourceReference(StyleProperty, "MaMutedText");
            var name = new TextBlock { Text = title, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center };
            var top = new MaStackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            top.Children.Add(light);
            top.Children.Add(name);
            Extra.Children.Add(Action);
            var body = new MaStackPanel { Spacing = 6 };
            body.Children.Add(top);
            body.Children.Add(detail);
            body.Children.Add(Extra);
            Card.Child = body;
        }

        public void Show(bool done, string text)
        {
            light.Status = done ? MaStatus.Good : MaStatus.Warning;
            light.Label = done ? $"{title}: done" : $"{title}: to do";
            detail.Text = text;
            Action.Visibility = done ? Visibility.Collapsed : Visibility.Visible;
        }
    }
}

/// <summary>The tools a new user's PC needs (M.ORCH-T1545), how to find them and how to install them.</summary>
internal static class SetupTools
{
    internal sealed record Tool(string Name, string Exe, string Install);

    private const string Winget = "--exact --source winget --accept-package-agreements --accept-source-agreements";

    internal static readonly Tool[] All =
    [
        new("git", "git", $"winget install --id Git.Git {Winget}"),
        new("the GitHub CLI", "gh", $"winget install --id GitHub.cli {Winget}"),
        new("Claude Code", "claude", "irm https://claude.ai/install.ps1 | iex"),
    ];

    /// <summary>The tools not on this user's PATH as it is now (read fresh from the registry, so an install just made counts).</summary>
    internal static IReadOnlyList<Tool> Missing() => [.. All.Where(t => Find(t.Exe) is null)];

    internal static string? Find(string exe)
    {
        var path = $"{Environment.GetEnvironmentVariable("Path", EnvironmentVariableTarget.Machine)};{Environment.GetEnvironmentVariable("Path", EnvironmentVariableTarget.User)}";
        // Claude Code's own installer puts it here, and adds that to PATH only for new shells.
        var local = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "bin");
        foreach (var dir in path.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Append(local))
            foreach (var ext in new[] { ".exe", ".cmd" })
            {
                try
                {
                    var candidate = Path.Combine(Environment.ExpandEnvironmentVariables(dir), exe + ext);
                    if (File.Exists(candidate)) return candidate;
                }
                catch (ArgumentException) { }
            }
        return null;
    }

    /// <summary>Whether the GitHub CLI is signed in (<c>gh auth status</c> exits 0).</summary>
    internal static bool GitHubSignedIn()
    {
        if (Find("gh") is not { } gh) return false;
        try
        {
            using var p = Process.Start(new ProcessStartInfo(gh, "auth status")
            {
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true,
            });
            if (p is null) return false;
            _ = p.StandardOutput.ReadToEndAsync();
            _ = p.StandardError.ReadToEndAsync();
            if (!p.WaitForExit(15_000)) { try { p.Kill(); } catch (InvalidOperationException) { } return false; }
            return p.ExitCode == 0;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException) { return false; }
    }

    /// <summary>Runs <paramref name="script"/> in a PowerShell window the user can watch (an installer's progress, a sign-in's
    /// device code), with PATH read fresh so a tool installed a step earlier is found; its exit code.</summary>
    internal static async Task<int> RunVisibleAsync(string script)
    {
        var full = "$env:Path = [Environment]::GetEnvironmentVariable('Path','Machine') + ';' + [Environment]::GetEnvironmentVariable('Path','User') + ';' + \"$env:USERPROFILE\\.local\\bin\"\n" +
                   "$ErrorActionPreference = 'Continue'\n" + script + "\nexit $LASTEXITCODE";
        var encoded = Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(full));
        using var p = Process.Start(new ProcessStartInfo("powershell.exe", $"-NoProfile -ExecutionPolicy Bypass -EncodedCommand {encoded}")
        {
            UseShellExecute = true,
        }) ?? throw new InvalidOperationException("PowerShell didn't start.");
        await p.WaitForExitAsync();
        return p.ExitCode;
    }
}
