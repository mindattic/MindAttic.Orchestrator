using System.Diagnostics;
using System.IO.Pipes;
using System.Text;

namespace MindAttic.Orchestrator.Sessions;

/// <summary>
/// Starts and talks to agent sessions through the Launcher, the same way its own menus do: a new
/// Windows Terminal tab running <c>MindAttic.Launcher.exe host --path …</c>, and text written to a
/// host's input pipe (a trailing newline submits it).
/// </summary>
public sealed class SessionControl(OrchestratorOptions options)
{
    /// <summary>The Launcher exe to start (M.ORCH-T2285): a snapshot of <see cref="OrchestratorOptions.LauncherExe"/>'s folder
    /// (<see cref="LauncherRunCopy"/>) when <see cref="OrchestratorOptions.LauncherRunCopies"/> is on, so the Launcher's redeploy
    /// can't stop the tabs it started; the exe itself otherwise.</summary>
    private string LauncherPath() => options.LauncherRunCopies ? LauncherRunCopy.Resolve(options.LauncherExe) : options.LauncherExe;

    /// <summary>The agent providers a new tab can run.</summary>
    public static readonly IReadOnlyList<string> Providers = ["Claude", "Codex", "Gemini", "Kimi"];

    /// <summary>
    /// Opens the Launcher's menu as one person (M.ORCH-T615): <c>MindAttic.Launcher --account &lt;email&gt;</c> (M.LNCH-T54), so
    /// every repo CLI started from it runs on that per-CLI login and shows in its dashboard tab; <paramref name="email"/> null
    /// opens it unscoped, on the global login. It goes in that login's own Windows Terminal window, colored as it (M.ORCH-T616).
    /// One per login (M.ORCH-T699): a login whose Launcher menu is already running keeps it rather than getting a second, and
    /// <paramref name="running"/> is that menu's process (null when a new tab was opened). Returns an error, or null on success.
    /// </summary>
    public string? OpenLauncher(string? email, out int? running)
    {
        running = null;
        if (email is not null && AccountDirs.Normalize(email) is null)
            return $"\"{email}\" isn't an email address: the Launcher's --account takes the account's whole email.";
        var launcher = LauncherPath();
        if (!File.Exists(launcher))
            return $"Launcher not found at {options.LauncherExe} (run MindAttic.Launcher/run.ps1 -Redeploy).";
        if ((running = LauncherMenuOf(LauncherMenus(), email)) is not null) return null;
        var psi = new ProcessStartInfo("wt.exe") { UseShellExecute = false };
        foreach (var a in LauncherArguments(email, launcher)) psi.ArgumentList.Add(a);
        try
        {
            // Outside the caller's job (M.ORCH-T1691): a terminal opened by a command run in a hosted CLI outlives that CLI.
            Term.Breakaway.Start(psi);
            return null;
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            return $"Couldn't start Windows Terminal: {ex.Message}";
        }
    }

    /// <summary>Where the per-CLI logins live, to name <see cref="OpenLauncher"/>'s login folder for its look; tests set their own.</summary>
    internal AccountDirs Dirs { get; init; } = new();

    /// <summary><c>wt</c>'s arguments for <see cref="OpenLauncher"/>: a tab in the login's window, titled and colored as it, at the
    /// workspace root, running the Launcher (with <c>--account</c> for a per-CLI login). A per-CLI login's Launcher is started by
    /// a PowerShell that sets its <c>CLAUDE_CONFIG_DIR</c> and long-lived token first (M.ORCH-T696), as a repo tab on it is, so
    /// every CLI started from its menu inherits both.</summary>
    internal IEnumerable<string> LauncherArguments(string? email, string? launcher = null)
    {
        var normalized = AccountDirs.Normalize(email);
        var configDir = normalized is null ? null : Dirs.For(normalized).Directory;
        var look = Look(configDir);
        var title = LauncherTitle(look);
        launcher ??= LauncherPath();
        var root = Directory.Exists(options.WorkspaceRoot) ? options.WorkspaceRoot : Path.GetDirectoryName(launcher) ?? ".";
        string[] tab = ["-w", look.Window, "new-tab", "--title", title, "--tabColor", look.TabColor, "-d", root, "--"];
        string[] all = configDir is null
            ? [.. tab, launcher]
            : [.. tab, "powershell.exe", "-NoLogo", "-NoProfile", "-ExecutionPolicy", "Bypass", "-EncodedCommand", Convert.ToBase64String(Encoding.Unicode.GetBytes(
                $"$env:{AccountDirs.EnvVar} = {AccountDirs.Ps(configDir)}; {TokenEnv(configDir)}& {AccountDirs.Ps(launcher)} '--account' {AccountDirs.Ps(normalized!)}; exit $LASTEXITCODE"))];
        // wt splits its whole command line into subcommands on ';' unless it's escaped.
        return all.Select(a => a.Replace(";", @"\;"));
    }

    /// <summary>The title of a login's Launcher tab: "RD: Launcher" (M.LNCH-T77's title fix: no un-stripped "MindAttic." and
    /// no trailing name suffix, the same format as every other CLI's tab).</summary>
    internal static string LauncherTitle(LoginLook look) => $"{look.Initials}: Launcher";

    /// <summary>The title <see cref="OpenLauncher"/> gives <paramref name="email"/>'s Launcher tab (null: the global login's), to find it again.</summary>
    public string LauncherTitleOf(string? email) =>
        LauncherTitle(Look(AccountDirs.Normalize(email) is { } normalized ? Dirs.For(normalized).Directory : null));

    /// <summary>The Launcher's own menu as a <see cref="PaneRequest"/>, to open embedded as a Terminals panel tab (M.LNCH-T77)
    /// instead of its own Windows Terminal window: <see cref="PaneRequest.ProcessCommandLine"/> sets
    /// <see cref="PaneBroker.InsideVariable"/> for it automatically, so every CLI it opens, seeing itself hosted, routes back
    /// here too rather than opening a standalone window of its own. Unlike <see cref="LauncherArguments"/>'s Windows Terminal
    /// tab, nothing here needs to set <c>CLAUDE_CONFIG_DIR</c>/the long-lived token up front: a scoped Launcher resolves its
    /// own login from <c>--account</c>, and a CLI it opens from here arrives as a pane of its own, not a child process that
    /// would inherit this one's environment.</summary>
    public PaneRequest LauncherPaneRequest(string? email)
    {
        var normalized = AccountDirs.Normalize(email);
        var configDir = normalized is null ? null : Dirs.For(normalized).Directory;
        var look = Look(configDir);
        var launcher = LauncherPath();
        var root = Directory.Exists(options.WorkspaceRoot) ? options.WorkspaceRoot : Path.GetDirectoryName(launcher) ?? ".";
        return new PaneRequest(LauncherTitle(look), root)
        {
            Command = normalized is null ? [launcher] : [launcher, "--account", normalized],
            Repo = "Launcher",
            Provider = "Launcher",
            ConfigDir = configDir,
            Email = normalized,
            Initials = look.Initials,
            Alias = look.Alias,
            TabColor = look.TabColor,
            IsMenu = true,
        };
    }

    /// <summary>A repo (or the workspace root, for "General Prompt") CLI as a <see cref="PaneRequest"/> (M.ORCH-T1730), to open
    /// embedded as a Terminals panel tab for <paramref name="email"/>'s login: a <c>MindAttic.Launcher.exe host …</c> command, with
    /// <paramref name="configDir"/> passed as <c>--config-dir</c> (MindAttic.Launcher's <c>host --config-dir</c>, its own
    /// escape hatch for a tab whose process doesn't inherit a Windows Terminal tab's environment setup) rather than set as an
    /// environment variable.
    /// Every CLI the login-tab's right-click menu opens (Open Project, General Prompt), the tile grid's Start and a dispatched
    /// ticket's claim (<see cref="TicketLaunch"/>) use this, so every one of them docks under its login in the dashboard
    /// (M.ORCH-T2410/M.ORCH-T2414) rather than opening outside it the way a wt.exe tab once did.</summary>
    /// <param name="agentId">The agent id (M.ORCH-T1743) to carry on the pane, when the caller already named one (a dispatched
    /// ticket's claim, <see cref="TicketLaunch"/>); null mints a new one, as every caller before had it.</param>
    /// <param name="resumeSession">orchestrator start's own <c>--resume</c> (M.ORCH-T2413): <c>host --resume &lt;session&gt;</c>
    /// instead of a fresh start, so a Claude conversation carries over; ignored alongside <paramref name="prompt"/>,
    /// which a resume never sets anyway.</param>
    public PaneRequest RepoPaneRequest(string title, string directory, string provider, string? prompt, string? configDir, string? email, string? agentId = null,
        string? resumeSession = null)
    {
        // A non-Claude CLI starts authenticated (M.ORCH-T2042): Kimi's Vault key is synced into its config.toml now; Codex's and
        // Gemini's go on the hosted CLI's environment as it's launched (TermLaunchSpec.From, ProviderCredentials.EnvironmentFor).
        PrepareProvider(provider);
        var look = provider == "Claude" ? Look(configDir) : null;
        var tabTitle = TabTitle(title, provider, look, RepoNames);
        List<string> command = [LauncherPath(), "host", "--path", directory, "--title", tabTitle, "--provider", provider];
        // Every repo CLI opened here is an agent with a stable id (M.ORCH-T1743), carried on the pane and its TermHost.
        var agent = Term.AgentIds.Normalize(agentId) ?? Term.AgentIds.New();
        if (configDir is not null) command.AddRange(["--config-dir", configDir]);
        if (resumeSession is { Length: > 0 } session) command.AddRange(["--resume", session]);
        else if (!string.IsNullOrWhiteSpace(prompt)) command.AddRange(["--prompt", SafePrompt(prompt)]);
        return new PaneRequest(tabTitle, directory)
        {
            Command = command,
            Repo = title,
            Provider = provider,
            ConfigDir = configDir,
            Email = AccountDirs.Normalize(email),
            Initials = look?.Initials,
            Alias = look?.Alias,
            TabColor = look?.TabColor,
            AgentId = agent,
        };
    }

    /// <summary>
    /// A dispatched ticket's CLI (M.ORCH-T2410), built the way <see cref="RepoPaneRequest"/> builds a login tab's: a pure
    /// builder (no process starts, no registry touched) that turns a ticket's dispatch parameters into the session id a
    /// TermHost will register under, its <see cref="PaneRequest"/>, and the <see cref="TermLaunchSpec"/> to start it with
    /// (<see cref="TermLaunchSpec.From(PaneRequest, string?)"/>, with <see cref="TermLaunchSpec.Id"/> set to that id up front
    /// so a caller can write the <c>.pane</c> sidecar before the launch, as <c>TerminalsViewModel.StartHostedAsync</c> does).
    /// <see cref="TermLaunchSpec.Login"/> comes from <see cref="PaneRequest.Login"/>: <paramref name="configDir"/>'s own login
    /// when it names one, else <paramref name="email"/> (the assignee's), so the dispatched CLI registers under them and docks
    /// in the dashboard the same way a manually-opened Terminals tab already does.
    /// </summary>
    /// <param name="title">The tab's title, "{Repo} {Id}" (<see cref="Scheduling.Dispatcher.Dispatch"/>'s own).</param>
    /// <param name="agentId">The claim's own agent id (M.ORCH-T1743, <see cref="Settings.Assignment.AgentId"/>), carried
    /// through rather than minted here, so the id a ticket's claim already recorded is the one its CLI runs as.</param>
    public (string Id, PaneRequest Request, TermLaunchSpec Spec) TicketLaunch(string title, string directory, string provider, string? prompt, string? configDir, string? email, string? agentId)
    {
        var request = RepoPaneRequest(title, directory, provider, prompt, configDir, email, agentId);
        var id = Term.TermProtocol.NewSessionId();
        var spec = TermLaunchSpec.From(request) with { Id = id };
        return (id, request, spec);
    }

    /// <summary>Readies a provider's credentials on disk before its CLI starts (M.ORCH-T2042, <see cref="ProviderCredentials.Prepare"/>);
    /// tests set their own, so a run never touches the real Vault or Kimi config.</summary>
    internal Action<string> PrepareProvider { get; init; } = provider => ProviderCredentials.Prepare(provider);

    /// <summary>The command lines of the Launcher processes running now, by pid; tests set their own.</summary>
    internal Func<IReadOnlyDictionary<int, string>> LauncherMenus { get; init; } = HostSessionScanner.LauncherCommandLines;

    /// <summary>The pid of a standalone Launcher menu already running for <paramref name="email"/>'s login outside the
    /// dashboard (M.ORCH-T699), so opening it embedded (M.LNCH-T77) doesn't also open a second, standalone one; null when
    /// none is. Asks WMI: off the UI thread.</summary>
    public int? RunningLauncherOf(string? email) => LauncherMenuOf(LauncherMenus(), email);

    /// <summary>The Launcher menu (not a <c>host</c> tab) already running for <paramref name="email"/>'s login among
    /// <paramref name="launchers"/>: its <c>--account</c> is that email, or it has none for the global login. Null when none is.</summary>
    internal static int? LauncherMenuOf(IReadOnlyDictionary<int, string> launchers, string? email)
    {
        var wanted = AccountDirs.Normalize(email);
        foreach (var (pid, commandLine) in launchers.OrderBy(l => l.Key))
        {
            var args = HostSessionScanner.SplitCommandLine(commandLine).Skip(1).ToList();
            if (args.Any(a => a.Equals("host", StringComparison.OrdinalIgnoreCase))) continue;
            var at = args.FindIndex(a => a.Equals("--account", StringComparison.OrdinalIgnoreCase));
            var account = at >= 0 && at + 1 < args.Count ? AccountDirs.Normalize(args[at + 1]) : null;
            if (string.Equals(account, wanted, StringComparison.Ordinal)) return pid;
        }
        return null;
    }

    /// <summary>The look of a login's tabs (M.ORCH-T616); tests set their own rather than read the real ~/.claude.</summary>
    internal Func<string?, LoginLook> Look { get; init; } = LoginLook.ForConfigDir;

    /// <summary>The PowerShell a tab on a login (null: the global one) runs first for its long-lived token (M.ORCH-T696,
    /// <see cref="LoginTokens.TabEnv"/>): sets <c>CLAUDE_CODE_OAUTH_TOKEN</c> from the Vault, or clears one it would inherit.</summary>
    internal Func<string?, string> TokenEnv { get; init; } = configDir => LoginTokens.Default.TabEnv(configDir, DateTime.UtcNow);

    /// <summary>A tab's title (M.ORCH-T632): "JR: Repo", the login's initials (when it has one) then the repo's
    /// <see cref="ShortName(string, Func{IReadOnlyCollection{string}})"/> and, once there's more than one provider worth
    /// telling apart, its name ("JR: Repo [Codex]", <see cref="ProviderTag"/>). Internal rather than private so the tests can
    /// reach it.</summary>
    internal static string TabTitle(string title, string provider, LoginLook? look, Func<IReadOnlyCollection<string>> knownRepos) =>
        look is null ? $"{ShortName(title, knownRepos)}{ProviderTag(provider)}" : $"{look.Initials}: {ShortName(title, knownRepos)}{ProviderTag(provider)}";

    /// <summary>The " [Provider]" a tab's title ends with (M.ORCH-T1176): blank for Claude, since every tab was one until
    /// other providers are told apart some other way (icons, once they're further along); still shown for anything else, so
    /// a Codex or Gemini tab isn't mistaken for a Claude one. Public, like <see cref="ShortName(string)"/>, for the Wpf
    /// project's own tab labels (<c>TerminalPaneViewModel.Name</c>).</summary>
    public static string ProviderTag(string provider) => provider == "Claude" ? "" : $" [{provider}]";

    /// <summary>The repo names known right now, process-wide (M.ORCH-T632): every <see cref="StatusAggregator.Refresh"/>
    /// replaces this with the snapshot's own repos, so the single-argument <see cref="ShortName(string)"/> — the only form a
    /// call site with no workspace of its own (a tab-matching helper, a view model) can use — can tell a dot-prefix two or
    /// more repos share (worth stripping) from one unique to a single repo (kept whole, since stripping it would hide which
    /// org it's under). Tests set their own.</summary>
    public static IReadOnlyCollection<string> KnownRepoNames { get; set; } = [];

    /// <summary>A repo's name as a tab shows it (M.ORCH-T632): without its dot-prefix (up to and including the first '.')
    /// once two or more of <see cref="KnownRepoNames"/> share it ("GTFO.EZ" → "EZ" once another "GTFO." repo is known); a
    /// name whose prefix is unique, or that is only the prefix, stays whole. Generalized from a fixed
    /// "GTFO."/"MindAttic."/"Unity." list.</summary>
    public static string ShortName(string repo) => ShortName(repo, () => KnownRepoNames);

    /// <summary>Same as <see cref="ShortName(string)"/>, but checked against <paramref name="knownRepos"/> (not read until a
    /// dot-prefix is actually found, so a title with none never pays for it) instead of the process-wide
    /// <see cref="KnownRepoNames"/> — what a caller with its own live workspace (<see cref="RepoPaneRequest"/>) uses.</summary>
    internal static string ShortName(string repo, Func<IReadOnlyCollection<string>> knownRepos)
    {
        var dot = repo.IndexOf('.');
        if (dot <= 0 || dot >= repo.Length - 1) return repo;
        var prefix = repo[..(dot + 1)];
        return SharedPrefixes(knownRepos()).Contains(prefix) ? repo[(dot + 1)..] : repo;
    }

    private static HashSet<string> SharedPrefixes(IEnumerable<string> names) =>
        [.. names.Select(DotPrefix).OfType<string>().GroupBy(p => p, StringComparer.Ordinal).Where(g => g.Count() > 1).Select(g => g.Key)];

    private static string? DotPrefix(string name)
    {
        var dot = name.IndexOf('.');
        return dot > 0 && dot < name.Length - 1 ? name[..(dot + 1)] : null;
    }

    /// <summary>The repo names known right now, from the live workspace at <see cref="OrchestratorOptions.WorkspaceRoot"/>
    /// (M.ORCH-T632): what a newly opened tab's title checks its repo's dot-prefix against. Tests set their own rather than
    /// read the real workspace.</summary>
    internal Func<IReadOnlyCollection<string>> RepoNames { get; init; } =
        () => [.. new MindAttic.Tickets.Workspace(options.WorkspaceRoot).Repos().Select(r => r.Name)];

    internal static string SafePrompt(string prompt) =>
        string.Join(' ', prompt.Replace('"', '\'').Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).Select(l => l.Trim()));

    /// <summary>Types <paramref name="text"/> into a hosted session; <paramref name="submit"/> presses Enter after it.</summary>
    public static async Task<bool> SendAsync(string pipeName, string text, bool submit, CancellationToken ct = default)
    {
        var payload = submit ? text.TrimEnd('\r', '\n') + "\n" : text;
        try
        {
            using var client = new NamedPipeClientStream(".", pipeName, PipeDirection.Out, PipeOptions.Asynchronous);
            await client.ConnectAsync(1000, ct).ConfigureAwait(false);
            await client.WriteAsync(Encoding.UTF8.GetBytes(payload), ct).ConfigureAwait(false);
            await client.FlushAsync(ct).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex) when (ex is IOException or TimeoutException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>A Launcher tab's control pipe (M.LNCH-T38): requests to the host itself, never typed into the CLI. Apart from
    /// the input pipe, so a Launcher without one (from before it) is simply not there, rather than typing a request into the
    /// prompt box; and named outside <see cref="HostSessionScanner.PipeNamePrefix"/>, so it's never taken for a tab of its own.</summary>
    public const string ControlPipePrefix = "mindattic-control-";

    /// <summary>The control pipe of <paramref name="host"/>: <c>mindattic-control-{provider}-{host pid}</c>.</summary>
    public static string ControlPipeName(HostSession host) => $"{ControlPipePrefix}{host.Provider.ToLowerInvariant()}-{host.ProcessId}";

    /// <summary>How long typed text settles before Enter is pressed on its own.</summary>
    public static readonly TimeSpan EnterAfter = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Types <paramref name="text"/> into a hosted session, then presses Enter as a keystroke of its own.
    /// Claude Code reads a burst of keys as a paste, and a line break inside a paste is just a new line,
    /// so text and Enter sent together leave a long prompt sitting unsent in the box.
    /// </summary>
    public static async Task<bool> SubmitAsync(string pipeName, string text, CancellationToken ct = default)
    {
        if (!await SendAsync(pipeName, text.TrimEnd('\r', '\n'), submit: false, ct).ConfigureAwait(false)) return false;
        await Task.Delay(EnterAfter, ct).ConfigureAwait(false);
        return await SendAsync(pipeName, "", submit: true, ct).ConfigureAwait(false);
    }

    /// <summary><c>MindAttic.Launcher.exe commit</c>'s arguments: every project on its roster, under <paramref name="message"/>
    /// (blank: the Launcher writes one per project from its status).</summary>
    internal static IReadOnlyList<string> CommitAllArguments(string? message) =>
        string.IsNullOrWhiteSpace(message) ? ["commit"] : ["commit", "--message", message.Trim()];

    /// <summary>
    /// Commit + push every dirty project (M.ORCH-T1590), from the project tree's root: runs the Launcher's own
    /// <c>commit</c> command, hidden, so its roster (the wip-tagged repos), its "nothing to commit" skip for a clean repo and
    /// its ticket alignment for each commit are the same as its Commit and sync menu's All Projects. Each line it prints goes
    /// to <paramref name="line"/>; returns its exit code (0: every project committed or had nothing to), or -1 when it couldn't
    /// be started (said on <paramref name="line"/>).
    /// </summary>
    public async Task<int> CommitAllAsync(string? message, Action<string> line, CancellationToken ct = default)
    {
        var launcher = LauncherPath();
        if (!File.Exists(launcher))
        {
            line($"Launcher not found at {options.LauncherExe} (run MindAttic.Launcher/run.ps1 -Redeploy).");
            return -1;
        }
        var psi = new ProcessStartInfo(launcher)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            WorkingDirectory = Directory.Exists(options.WorkspaceRoot) ? options.WorkspaceRoot : Path.GetDirectoryName(launcher) ?? ".",
        };
        foreach (var a in CommitAllArguments(message)) psi.ArgumentList.Add(a);
        // Wide enough that Spectre doesn't wrap a project's one result line at its redirected default of 80 columns.
        psi.Environment["COLUMNS"] = "240";
        try
        {
            using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
            process.OutputDataReceived += (_, e) => { if (e.Data is { } d) line(d); };
            process.ErrorDataReceived += (_, e) => { if (e.Data is { } d) line(d); };
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            try { await process.WaitForExitAsync(ct).ConfigureAwait(false); }
            catch (OperationCanceledException)
            {
                try { process.Kill(entireProcessTree: true); } catch (Exception ex) when (ex is InvalidOperationException or AggregateException or System.ComponentModel.Win32Exception) { }
                throw;
            }
            return process.ExitCode;
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            line($"Couldn't start the Launcher: {ex.Message}");
            return -1;
        }
    }

    /// <summary>Opens <paramref name="directory"/> in Explorer; nothing when it doesn't exist.</summary>
    public static void OpenFolder(string directory)
    {
        if (Directory.Exists(directory))
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{directory}\"") { UseShellExecute = false })?.Dispose();
    }
}
