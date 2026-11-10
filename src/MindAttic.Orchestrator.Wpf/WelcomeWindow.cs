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
    private readonly Step tools, github, workspace, claude, firstRepo;
    private readonly TextBox email = new() { Width = 170 };
    private readonly TextBox repoName = new(), repoDescription = new();
    private readonly CheckBox repoPrivate = new() { Content = "Private", IsChecked = true };

    /// <summary>The repo the user made here (M.ORCH-T1559), which the dashboard opens a CLI in once it's up; null when none.</summary>
    internal static string? CreatedRepo { get; private set; }
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
        // The order a new user meets them (M.ORCH-T1558): the tools install themselves, then their Claude Team login, GitHub,
        // and where their projects live.
        claude = new Step("2. Claude Team account", "Sign in", () => Busy(SignInClaudeAsync));
        github = new Step("3. GitHub", "Connect GitHub", () => Busy(ConnectGitHubAsync));
        workspace = new Step("4. Workspace", "Start an empty one", () => Busy(StartEmptyWorkspaceAsync));
        var existing = new Button { Content = "MindAttic's, or one already here…" };
        existing.SetResourceReference(StyleProperty, "MaButton");
        existing.Click += (_, _) => Busy(ChooseWorkspaceAsync);
        AutomationNames(existing, "4. Workspace: clone MindAttic's workspace or point at an existing one");
        existing.Margin = Step.Gap;
        workspace.Extra.Children.Add(existing);
        firstRepo = new Step("5. Your first repo", "Create repo", () => Busy(CreateFirstRepoAsync));
        foreach (var (box, hint, width) in new[] { (repoName, "Name", 170d), (repoDescription, "Description (optional)", 220d) })
        {
            box.SetResourceReference(StyleProperty, "MaTextBox");
            MaTextBox.SetPlaceholder(box, hint);
            AutomationNames(box, $"Your first repo's {hint.ToLowerInvariant()}");
            box.Width = width;
            box.Margin = Step.Gap;
        }
        repoPrivate.SetResourceReference(StyleProperty, "MaCheck");
        repoPrivate.Margin = Step.Gap;
        repoPrivate.VerticalAlignment = VerticalAlignment.Center;
        firstRepo.Extra.Children.Insert(0, repoName);
        firstRepo.Extra.Children.Insert(1, repoPrivate);
        firstRepo.Extra.Children.Insert(2, repoDescription);
        email.SetResourceReference(StyleProperty, "MaTextBox");
        AutomationNames(email, "Your Claude account's email");
        MaTextBox.SetPlaceholder(email, "you@company.com");
        email.Margin = Step.Gap;
        claude.Extra.Children.Insert(0, email);

        var heading = new TextBlock { Text = "Let's get you set up", TextWrapping = TextWrapping.Wrap };
        heading.SetResourceReference(StyleProperty, "MaHeading");
        var intro = new TextBlock
        {
            Text = "Orchestrator runs your Claude CLIs on this PC: its memory and CPU do the work, while you drive them from here or, " +
                   "over Tailscale and Remote Desktop, from your phone (see docs/TAILSCALE.md). Missing tools install on their own; the rest is one button each.",
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
        foreach (var part in new UIElement[] { heading, intro, tools.Card, claude.Card, github.Card, workspace.Card, firstRepo.Card, note, buttons }) steps.Children.Add(part);
        steps.SetResourceReference(MarginProperty, "SpaceL");
        var scroll = new ScrollViewer { Content = steps, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, PanningMode = PanningMode.VerticalOnly };
        scroll.SetResourceReference(StyleProperty, "MaBodyScroll");
        Content = scroll;
        Loaded += (_, _) =>
        {
            Refresh();
            // Whatever's missing installs as soon as the window opens, without waiting for a click.
            if (AutoInstall && SetupTools.Missing().Count > 0) Busy(InstallToolsAsync);
        };
    }

    /// <summary>Off only for a render of the window, which must install nothing.</summary>
    internal static bool AutoInstall { get; set; } = true;

    private static string WorkspaceRoot() => OrchestratorOptions.Load(new JsonStore()).WorkspaceRoot;

    private static void AutomationNames(FrameworkElement e, string name) => System.Windows.Automation.AutomationProperties.SetName(e, name);

    /// <summary>Reads every step again and says what's left.</summary>
    private void Refresh()
    {
        var missing = SetupTools.Missing();
        tools.Show(missing.Count == 0, missing.Count == 0 ? "git, the GitHub CLI, Claude Code, Python and the .NET 10 runtimes are installed."
            : $"Missing: {string.Join(", ", missing.Select(t => t.Name))}. Installs them with winget and Claude Code's own installer.");
        var gh = missing.All(t => t.Exe != "gh") && SetupTools.GitHubSignedIn();
        github.Show(gh, gh ? "Signed in to GitHub: the workspace's repos clone and push as you."
            : "Sign in to GitHub in your browser, so your repos clone and your CLIs can push.");
        var root = WorkspaceRoot();
        var ws = !WorkspaceBootstrap.IsEmpty(root);
        workspace.Show(ws, ws ? $"Workspace: {root}" : "The folder your repos live in. Start an empty one and clone your own repos into it; or clone MindAttic's workspace (its owner must give your GitHub account access to the private mindattic/codex-standard), or point at a folder already on this PC.");
        var signedIn = dirs.AnySignedIn;
        claude.Show(signedIn, signedIn ? "A Claude account is signed in. Add more from the dashboard's Logins."
            : "Your Claude account's email (a MindAttic Claude Team seat works as is), then Sign in: the browser asks you to confirm.");
        claude.Action.IsEnabled = !busy && missing.All(t => t.Exe != "claude");
        var repo = CreatedRepo is { } made ? Path.Combine(root, made) : ws ? FirstRepoIn(root) : null;
        firstRepo.Show(repo is not null, repo is not null
            ? $"Your repo: {repo}. Drag your code into that folder, then type /commit in its CLI on the dashboard, and you're all set."
            : "A repo on your GitHub, cloned into the workspace with a Hello World first commit on main. Then you drop your code into it.");
        firstRepo.Action.IsEnabled = !busy && gh && ws;
        open.Content = ws && signedIn ? "Open the dashboard" : "Skip for now";
    }

    /// <summary>A git repo already in the workspace, or null.</summary>
    private static string? FirstRepoIn(string root)
    {
        try { return Directory.EnumerateDirectories(root).FirstOrDefault(d => Directory.Exists(Path.Combine(d, ".git"))); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return null; }
    }

    /// <summary>GitHub's own rule for a repo's name: letters, digits, '.', '-', '_', at most 100.</summary>
    internal static bool IsRepoName(string name) =>
        name.Length is > 0 and <= 100 && name is not "." and not ".." && name.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '-' or '_');

    /// <summary>
    /// The first repo (M.ORCH-T1559): made on the signed-in GitHub account (<c>gh repo create --clone</c>, in the workspace), with
    /// a Hello World first commit (README.md and an empty .gitignore) pushed to main, then opened in Explorer for the user's code.
    /// A PC with no git identity gets the GitHub account's name and its no-reply address, so the commit can be made.
    /// </summary>
    private async Task CreateFirstRepoAsync()
    {
        var name = repoName.Text.Trim();
        if (!IsRepoName(name)) { note.Text = "A repo name is letters, digits, '.', '-' or '_' (no spaces)."; return; }
        var root = WorkspaceRoot();
        var dir = Path.Combine(root, name);
        if (Directory.Exists(dir)) { note.Text = $"{dir} already exists: pick another name."; return; }
        if (SetupTools.Find("gh") is not { } gh || SetupTools.Find("git") is not { } git) { note.Text = "git and the GitHub CLI are needed first (step 1)."; return; }
        note.Text = $"Creating {name} on your GitHub…";
        var create = new List<string> { "repo", "create", name, repoPrivate.IsChecked == true ? "--private" : "--public", "--clone" };
        if (repoDescription.Text.Trim() is { Length: > 0 } description) { create.Add("--description"); create.Add(description); }
        if (await SetupTools.RunAsync(gh, create, root) is { Code: not 0 } made) { note.Text = $"GitHub didn't create it: {made.Output.Trim()}"; return; }
        if ((await SetupTools.RunAsync(git, ["config", "--global", "user.email"], root)).Output.Trim().Length == 0)
        {
            var login = (await SetupTools.RunAsync(gh, ["api", "user", "-q", ".login"], root)).Output.Trim();
            var id = (await SetupTools.RunAsync(gh, ["api", "user", "-q", ".id"], root)).Output.Trim();
            var fullName = (await SetupTools.RunAsync(gh, ["api", "user", "-q", ".name // .login"], root)).Output.Trim();
            await SetupTools.RunAsync(git, ["config", "--global", "user.name", fullName.Length > 0 ? fullName : login], root);
            await SetupTools.RunAsync(git, ["config", "--global", "user.email", $"{id}+{login}@users.noreply.github.com"], root);
        }
        File.WriteAllText(Path.Combine(dir, "README.md"), $"# {name}\n\nHello World\n");
        File.WriteAllText(Path.Combine(dir, ".gitignore"), "");
        foreach (var step in new[] { new[] { "checkout", "-B", "main" }, ["add", "-A"], ["commit", "-m", "Hello World"], ["push", "-u", "origin", "main"] })
            if (await SetupTools.RunAsync(git, step, dir) is { Code: not 0 } failed)
            {
                note.Text = $"git {step[0]} failed: {failed.Output.Trim()}";
                return;
            }
        CreatedRepo = name;
        log.Information("Welcome created the first repo {Repo:l}", dir);
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{dir}\"") { UseShellExecute = true })?.Dispose();
        note.Text = $"{name} is on your GitHub and in {dir}, which just opened: drag your code into it. Then Open the dashboard, where a CLI starts in it: type /commit there.";
    }

    private async void Busy(Func<Task> work)
    {
        if (busy) return;
        busy = true;
        foreach (var step in new[] { tools, github, workspace, claude, firstRepo }) step.Action.IsEnabled = false;
        try { await work(); }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            log.Error(ex, "A welcome step failed");
            note.Text = $"That didn't finish: {ex.Message}";
        }
        finally
        {
            busy = false;
            foreach (var step in new[] { tools, github, workspace, claude, firstRepo }) step.Action.IsEnabled = true;
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

    /// <summary>A workspace of your own (M.ORCH-T1557): any folder, which your repos are cloned into; no MindAttic access needed.</summary>
    private Task StartEmptyWorkspaceAsync()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog { Title = "MindAttic Orchestrator: choose or create a folder to hold your repos" };
        if (dialog.ShowDialog(this) != true) return Task.CompletedTask;
        var folder = dialog.FolderName;
        if (WorkspaceBootstrap.IsEmpty(folder))
            File.WriteAllText(Path.Combine(folder, "README.md"),
                "# Workspace\n\nEach folder here is one repo the Orchestrator can run Claude CLIs in. Clone yours in:\n\n" +
                "    git clone https://github.com/<you>/<repo>.git\n\nthen refresh the dashboard (F5).\n");
        note.Text = App.SaveWorkspaceRoot(folder, log) ? $"Workspace: {folder}. Clone your repos into it, then refresh the dashboard." : "";
        return Task.CompletedTask;
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
        /// <summary>The step's controls, wrapping onto a second line in a phone-narrow window.</summary>
        public WrapPanel Extra { get; } = new();

        /// <summary>The gap after each of <see cref="Extra"/>'s controls, across and below.</summary>
        public static readonly Thickness Gap = new(0, 0, 8, 6);
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
            Action.Margin = Gap;
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
            Extra.Visibility = done ? Visibility.Collapsed : Visibility.Visible;
        }
    }
}

/// <summary>The tools a new user's PC needs (M.ORCH-T1545), how to find them and how to install them.</summary>
internal static class SetupTools
{
    /// <param name="Present">How to tell it's installed when it isn't an exe on PATH (a .NET runtime).</param>
    internal sealed record Tool(string Name, string Exe, string Install, Func<bool>? Present = null);

    private const string Winget = "--exact --source winget --accept-package-agreements --accept-source-agreements";

    internal static readonly Tool[] All =
    [
        new("git", "git", $"winget install --id Git.Git {Winget}"),
        new("the GitHub CLI", "gh", $"winget install --id GitHub.cli {Winget}"),
        new("Claude Code", "claude", "irm https://claude.ai/install.ps1 | iex"),
        // The pacing and context hooks the Orchestrator installs into Claude Code are Python (AgentGate.Command, M.ORCH-T1558).
        new("Python 3", "python", $"winget install --id Python.Python.3.13 {Winget}"),
        // The CLI, TermHost, Gateway and TermViewer beside the self-contained Desktop exe run on the shared runtimes (M.ORCH-T1547).
        new("the .NET 10 Desktop Runtime", "", $"winget install --id Microsoft.DotNet.DesktopRuntime.10 {Winget}", () => HasRuntime("Microsoft.WindowsDesktop.App")),
        new("the ASP.NET Core 10 Runtime", "", $"winget install --id Microsoft.DotNet.AspNetCore.10 {Winget}", () => HasRuntime("Microsoft.AspNetCore.App")),
    ];

    /// <summary>The tools not installed as things are now (PATH read fresh from the registry, so an install just made counts).</summary>
    internal static IReadOnlyList<Tool> Missing() => [.. All.Where(t => !(t.Present is { } present ? present() : Find(t.Exe) is not null))];

    private static bool HasRuntime(string framework)
    {
        var shared = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "dotnet", "shared", framework);
        try { return Directory.Exists(shared) && Directory.EnumerateDirectories(shared, "10.*").Any(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return false; }
    }

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
                    // WindowsApps' python.exe is only a shortcut to the Store page, not Python.
                    if (File.Exists(candidate) && !candidate.Contains(@"\Microsoft\WindowsApps\", StringComparison.OrdinalIgnoreCase)) return candidate;
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

    /// <summary>Runs <paramref name="exe"/> with <paramref name="args"/> in <paramref name="directory"/>, windowless; its exit code
    /// and everything it printed. Never longer than two minutes.</summary>
    internal static async Task<(int Code, string Output)> RunAsync(string exe, IEnumerable<string> args, string directory)
    {
        var psi = new ProcessStartInfo(exe)
        {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true,
            WorkingDirectory = directory,
        };
        foreach (var arg in args) psi.ArgumentList.Add(arg);
        // gh would stop to ask in a terminal that isn't there.
        psi.Environment["GH_PROMPT_DISABLED"] = "1";
        try
        {
            using var p = Process.Start(psi) ?? throw new InvalidOperationException($"{exe} didn't start.");
            var output = p.StandardOutput.ReadToEndAsync();
            var error = p.StandardError.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            try { await p.WaitForExitAsync(timeout.Token); }
            catch (OperationCanceledException) { try { p.Kill(true); } catch (InvalidOperationException) { } return (-1, "timed out"); }
            return (p.ExitCode, await output + await error);
        }
        catch (System.ComponentModel.Win32Exception ex) { return (-1, ex.Message); }
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
