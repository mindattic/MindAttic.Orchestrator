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
    public static readonly IReadOnlyList<string> Providers = ["Claude", "Codex", "Gemini", "Kimi"];

    /// <summary>Opens a new tab for <paramref name="directory"/>; returns an error, or null on success.</summary>
    /// <param name="configDir">A Claude login of its own (M.ORCH-T559, <see cref="AccountDir.Directory"/>) the tab's CLI runs
    /// under; null runs it on the global login, as every tab did before.</param>
    public string? Start(string title, string directory, string provider, string? prompt = null, string? configDir = null)
    {
        if (configDir is not null && provider != "Claude")
            return $"--account is a Claude login: {provider} has none.";
        if (!File.Exists(options.LauncherExe))
            return $"Launcher not found at {options.LauncherExe} (run MindAttic.Launcher/deploy.ps1).";
        if (!Directory.Exists(directory))
            return $"Folder not found: {directory}";

        var psi = new ProcessStartInfo("wt.exe") { UseShellExecute = false };
        foreach (var a in StartArguments(title, directory, provider, prompt, configDir)) psi.ArgumentList.Add(a);
        try
        {
            using var _ = Process.Start(psi);
            return null;
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            return $"Couldn't start Windows Terminal: {ex.Message}";
        }
    }

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
        if (!File.Exists(options.LauncherExe))
            return $"Launcher not found at {options.LauncherExe} (run MindAttic.Launcher/deploy.ps1).";
        if ((running = LauncherMenuOf(LauncherMenus(), email)) is not null) return null;
        var psi = new ProcessStartInfo("wt.exe") { UseShellExecute = false };
        foreach (var a in LauncherArguments(email)) psi.ArgumentList.Add(a);
        try
        {
            using var _ = Process.Start(psi);
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
    internal IEnumerable<string> LauncherArguments(string? email)
    {
        var normalized = AccountDirs.Normalize(email);
        var configDir = normalized is null ? null : Dirs.For(normalized).Directory;
        var look = Look(configDir);
        var title = LauncherTitle(look);
        var root = Directory.Exists(options.WorkspaceRoot) ? options.WorkspaceRoot : Path.GetDirectoryName(options.LauncherExe) ?? ".";
        string[] tab = ["-w", look.Window, "new-tab", "--title", title, "--tabColor", look.TabColor, "-d", root, "--"];
        string[] all = configDir is null
            ? [.. tab, options.LauncherExe]
            : [.. tab, "powershell.exe", "-NoLogo", "-NoProfile", "-ExecutionPolicy", "Bypass", "-EncodedCommand", Convert.ToBase64String(Encoding.Unicode.GetBytes(
                $"$env:{AccountDirs.EnvVar} = {AccountDirs.Ps(configDir)}; {TokenEnv(configDir)}& {AccountDirs.Ps(options.LauncherExe)} '--account' {AccountDirs.Ps(normalized!)}; exit $LASTEXITCODE"))];
        // wt splits its whole command line into subcommands on ';' unless it's escaped.
        return all.Select(a => a.Replace(";", @"\;"));
    }

    /// <summary>The title of a login's Launcher tab: "RD: MindAttic.Launcher".</summary>
    internal static string LauncherTitle(LoginLook look) => $"{look.Initials}: MindAttic.Launcher";

    /// <summary>The title <see cref="OpenLauncher"/> gives <paramref name="email"/>'s Launcher tab (null: the global login's), to find it again.</summary>
    public string LauncherTitleOf(string? email) =>
        LauncherTitle(Look(AccountDirs.Normalize(email) is { } normalized ? Dirs.For(normalized).Directory : null));

    /// <summary>The command lines of the Launcher processes running now, by pid; tests set their own.</summary>
    internal Func<IReadOnlyDictionary<int, string>> LauncherMenus { get; init; } = HostSessionScanner.LauncherCommandLines;

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

    /// <summary>A tab's title (M.ORCH-T632): "JR: Repo [Claude]", the login's initials (when it has one) then the repo's
    /// <see cref="ShortName"/> and its provider.</summary>
    private static string TabTitle(string title, string provider, LoginLook? look) =>
        look is null ? $"{ShortName(title)} [{provider}]" : $"{look.Initials}: {ShortName(title)} [{provider}]";

    /// <summary>The leading prefixes a repo's name drops where a tab shows it (M.ORCH-T632), as the Launcher drops them.</summary>
    public static readonly IReadOnlyList<string> NamePrefixes = ["GTFO.", "MindAttic.", "Unity."];

    /// <summary>A repo's name as a tab shows it: without a leading <see cref="NamePrefixes"/> entry ("GTFO.EZ" → "EZ"); a
    /// name that is only the prefix stays whole.</summary>
    public static string ShortName(string repo)
    {
        foreach (var prefix in NamePrefixes)
            if (repo.Length > prefix.Length && repo.StartsWith(prefix, StringComparison.Ordinal)) return repo[prefix.Length..];
        return repo;
    }

    internal IEnumerable<string> StartArguments(string title, string directory, string provider, string? prompt, string? configDir = null)
    {
        // A Claude tab gathers in its login's own window, badged and colored as that login; other providers have no login.
        var look = provider == "Claude" ? Look(configDir) : null;
        var tabTitle = TabTitle(title, provider, look);
        string[] tab = look is null
            ? ["-w", "0", "new-tab", "--title", tabTitle]
            : ["-w", look.Window, "new-tab", "--title", tabTitle, "--tabColor", look.TabColor];
        if (configDir is not null)
        {
            // Windows Terminal starts the tab itself, from its own environment, not this process's: the Launcher (and the
            // CLI it hosts) is started by a PowerShell that sets CLAUDE_CONFIG_DIR first. Encoded, so no quote, space or ';' in
            // the folder, title or prompt reaches wt's parse.
            // Its long-lived token (M.ORCH-T696), when the login has one, goes in beside the folder.
            var script = $"$env:{AccountDirs.EnvVar} = {AccountDirs.Ps(configDir)}; {TokenEnv(configDir)}& {AccountDirs.Ps(options.LauncherExe)} host --path {AccountDirs.Ps(directory)} " +
                $"--title {AccountDirs.Ps(tabTitle)} --provider {AccountDirs.Ps(provider)}" +
                (string.IsNullOrWhiteSpace(prompt) ? "" : $" --prompt {AccountDirs.Ps(SafePrompt(prompt))}") + "; exit $LASTEXITCODE";
            string[] wrapped = [.. tab, "-d", directory, "--",
                "powershell.exe", "-NoLogo", "-NoProfile", "-ExecutionPolicy", "Bypass", "-EncodedCommand", Convert.ToBase64String(Encoding.Unicode.GetBytes(script))];
            return wrapped.Select(a => a.Replace(";", @"\;"));
        }
        string[] head =
        [
            .. tab, "-d", directory, "--",
            options.LauncherExe, "host", "--path", directory, "--title", tabTitle, "--provider", provider,
        ];
        // A double quote (or a line break) in the prompt survives .NET's quoting but not wt's own re-parse,
        // which splits the prompt into stray arguments the Launcher rejects ("Unknown command"), so the tab dies.
        string[] all = string.IsNullOrWhiteSpace(prompt) ? head : [.. head, "--prompt", SafePrompt(prompt)];
        // wt splits its whole command line into subcommands on ';' unless it's escaped.
        return all.Select(a => a.Replace(";", @"\;"));
    }

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

    /// <summary>The Launcher host processes running now, so a tab opened next can be told apart from them.</summary>
    public IReadOnlySet<int> HostIds() => new HostSessionScanner(options).Scan().Select(h => h.ProcessId).ToHashSet();

    /// <summary>
    /// Presses Enter in the tab just opened at <paramref name="directory"/> (the first host there that
    /// isn't in <paramref name="before"/>), once it has been up for <paramref name="settle"/> so the agent
    /// has loaded its pre-filled prompt. False if no such tab appears within <paramref name="timeout"/>.
    /// </summary>
    public async Task<bool> SubmitWhenReadyAsync(string directory, IReadOnlySet<int> before, TimeSpan settle, TimeSpan timeout, CancellationToken ct = default)
    {
        if (await WaitForHostAsync(directory, before, timeout, ct).ConfigureAwait(false) is not { } host) return false;
        await Task.Delay(settle, ct).ConfigureAwait(false);
        return await SendAsync(host.PipeName, "", submit: true, ct).ConfigureAwait(false);
    }

    /// <summary>The tab just opened at <paramref name="directory"/>, once it has stayed up for a few seconds
    /// (a tab whose agent fails to start exits at once); null if none appears and stays within <paramref name="timeout"/>.</summary>
    public async Task<HostSession?> WaitForHostAsync(string directory, IReadOnlySet<int> before, TimeSpan timeout, CancellationToken ct = default)
    {
        var scanner = new HostSessionScanner(options);
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (NewHostAt(scanner.Scan(), directory, before) is { } host)
            {
                await Task.Delay(TimeSpan.FromSeconds(5), ct).ConfigureAwait(false);
                if (scanner.Scan().Any(h => h.ProcessId == host.ProcessId)) return host;
            }
            await Task.Delay(TimeSpan.FromSeconds(1), ct).ConfigureAwait(false);
        }
        return null;
    }

    internal static HostSession? NewHostAt(IEnumerable<HostSession> hosts, string directory, IReadOnlySet<int> before)
    {
        var target = Path.GetFullPath(directory).TrimEnd('\\', '/');
        return hosts.FirstOrDefault(h => !before.Contains(h.ProcessId) && h.WorkingDirectory is { } d &&
            string.Equals(Path.GetFullPath(d).TrimEnd('\\', '/'), target, StringComparison.OrdinalIgnoreCase));
    }

    public static void OpenFolder(string directory)
    {
        if (Directory.Exists(directory))
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{directory}\"") { UseShellExecute = false })?.Dispose();
    }
}
