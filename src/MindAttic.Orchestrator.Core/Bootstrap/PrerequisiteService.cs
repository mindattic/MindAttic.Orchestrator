using System.Diagnostics;

namespace MindAttic.Orchestrator.Bootstrap;

/// <summary>What one <c>Ensure</c> did: whether <see cref="Command"/> is now present, whether this call installed it, and why not.</summary>
public sealed record PrerequisiteCheckResult(string Command, bool Present, bool Installed, string? Error);

/// <summary>
/// An optional dev tool bootstrap can offer to install, never required. <paramref name="CommandOnPath"/> is set only for a tool
/// that really puts its own command on PATH; a GUI app that doesn't (Claude desktop, Antigravity IDE, Visual Studio, SSMS) is found
/// through <paramref name="InstallPaths"/> (environment variables expanded) or, failing that, by asking winget.
/// </summary>
public sealed record OptionalToolSpec(string Name, string? CommandOnPath, string WingetId, IReadOnlyList<string>? InstallPaths = null);

/// <summary>
/// An AI agent CLI. Installed by the vendor's own PowerShell install script when <paramref name="InstallScriptUrl"/> is set, else
/// from npm when <paramref name="NpmPackage"/> is set, otherwise from winget.
/// </summary>
public sealed record AiCliSpec(string Name, string Command, string? NpmPackage, string? WingetId, string? InstallScriptUrl = null);

/// <summary>Which group a prerequisite belongs to.</summary>
public enum PrerequisiteKind
{
    /// <summary>Required: the Azure CLI and sqlcmd.</summary>
    Required,
    /// <summary>An AI agent CLI the tabs run.</summary>
    AiCli,
    /// <summary>An optional dev tool.</summary>
    Optional,
}

/// <summary>One prerequisite's installed state, as <see cref="PrerequisiteService.CheckAll"/> reports it.</summary>
public sealed record PrerequisiteStatus(string Name, PrerequisiteKind Kind, bool Installed);

/// <summary>
/// The Launcher's prerequisite engine (its <c>PrerequisiteService</c>), ported into Core for the Orchestrator's Bootstrap window
/// (M.ORCH-T2051, part of M.ORCH-T1984). Checks for the SQL tools, the AI CLIs and the optional dev tools, and installs a missing one
/// via winget, npm or the vendor's script, refreshing this process's PATH after each install so the next step finds it.
/// </summary>
public sealed class PrerequisiteService
{
    /// <summary>Whether a command resolves on PATH.</summary>
    public delegate bool CommandExistsChecker(string command);

    /// <summary>Runs <paramref name="executable"/> to completion (or <paramref name="ct"/>), returning its exit code and output.</summary>
    public delegate (int ExitCode, string Output) CommandRunner(string executable, IReadOnlyList<string> args, CancellationToken ct);

    /// <summary>
    /// (command on PATH, winget ids tried in order). The databases are on Azure SQL: the Azure CLI signs exports and kickstart in,
    /// and sqlcmd applies an exported .sql by hand.
    /// </summary>
    public static readonly IReadOnlyList<(string Command, string[] WingetIds)> HardRequirements =
    [
        ("az", ["Microsoft.AzureCLI"]),
        ("sqlcmd", ["Microsoft.sqlcmd"]),
    ];

    /// <summary>The optional dev tools offered.</summary>
    public static readonly IReadOnlyList<OptionalToolSpec> OptionalTools =
    [
        new("Notepad++", "notepad++", "Notepad++.Notepad++"),
        new("Node.js and npm", "npm", "OpenJS.NodeJS.LTS"),
        new("VS Code", "code", "Microsoft.VisualStudioCode"),
        // Neither devenv nor ssms is on PATH outside a Developer prompt, so winget answers for them.
        new("Visual Studio Community", null, "Microsoft.VisualStudio.2022.Community"),
        new("SQL Server Management Studio", null, "Microsoft.SQLServerManagementStudio"),
        // 'claude' on PATH is the Claude Code CLI, not the desktop app; 'agy' is the Antigravity CLI, not the IDE.
        new("Claude desktop", null, "Anthropic.Claude",
            [@"%LOCALAPPDATA%\AnthropicClaude\claude.exe", @"%LOCALAPPDATA%\Programs\AnthropicClaude\Claude.exe"]),
        new("Antigravity IDE", null, "Google.AntigravityIDE",
            [@"%LOCALAPPDATA%\Programs\Antigravity\Antigravity.exe"]),
    ];

    /// <summary>The AI CLIs the Launcher's tabs run.</summary>
    public static readonly IReadOnlyList<AiCliSpec> AiClis =
    [
        new("Claude Code CLI", "claude", "@anthropic-ai/claude-code", null),
        new("OpenAI Codex CLI", "codex", "@openai/codex", null),
        new("Antigravity CLI", "agy", null, "Google.AntigravityCLI"),
        // Moonshot's documented Windows install: a single kimi.exe in %USERPROFILE%\.kimi-code\bin, added to the user PATH.
        new("Kimi Code CLI", "kimi", null, null, KimiInstallScriptUrl),
    ];

    /// <summary>Moonshot's official Kimi Code CLI installer for Windows.</summary>
    public const string KimiInstallScriptUrl = "https://code.kimi.com/kimi-code/install.ps1";

    /// <summary>winget package that provides npm, installed on demand when an npm-based CLI is missing.</summary>
    public const string NodeWingetId = "OpenJS.NodeJS.LTS";

    private readonly CommandExistsChecker exists;
    private readonly CommandRunner run;
    private readonly Action refreshPath;
    private readonly Func<string, bool> fileExists;

    /// <summary>The real service: <c>where</c> for PATH lookups, real processes, the registry PATH merged in after installs.</summary>
    public PrerequisiteService() : this(DefaultCommandExists, DefaultRun, () => ProcessPath.Refresh(), File.Exists) { }

    /// <summary>
    /// Test seam. <paramref name="refreshPath"/> defaults to a no-op and <paramref name="fileExists"/> to "nothing exists", so a
    /// test never touches the real environment or disk.
    /// </summary>
    public PrerequisiteService(CommandExistsChecker exists, CommandRunner run, Action? refreshPath = null, Func<string, bool>? fileExists = null)
    {
        this.exists = exists;
        this.run = run;
        this.refreshPath = refreshPath ?? (() => { });
        this.fileExists = fileExists ?? (_ => false);
    }

    /// <summary>winget arguments that install <paramref name="wingetId"/> silently.</summary>
    public static IReadOnlyList<string> BuildWingetInstallArguments(string wingetId) =>
        ["install", "--id", wingetId, "-e", "--silent", "--accept-package-agreements", "--accept-source-agreements"];

    /// <summary>winget arguments that ask whether <paramref name="wingetId"/> is installed (exit 0 when it is).</summary>
    public static IReadOnlyList<string> BuildWingetListArguments(string wingetId) =>
        ["list", "--id", wingetId, "-e", "--accept-source-agreements"];

    /// <summary>npm arguments that install <paramref name="package"/> globally.</summary>
    public static IReadOnlyList<string> BuildNpmInstallArguments(string package) => ["install", "-g", package];

    /// <summary>
    /// Runs a vendor's <c>irm &lt;url&gt; | iex</c> installer. -NonInteractive matters: Kimi's script ends a failed install with
    /// <c>Read-Host</c>, which then throws instead of waiting forever on a console nobody is watching.
    /// </summary>
    public static IReadOnlyList<string> BuildInstallScriptArguments(string url) =>
        ["-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-Command", $"irm '{url}' | iex"];

    /// <summary>Whether <paramref name="command"/> resolves on PATH.</summary>
    public bool CommandExists(string command) => exists(command);

    /// <summary>
    /// Every prerequisite's installed state: the required tools, then the AI CLIs, then the optional tools. Read-only: installs
    /// nothing. An optional tool with no command on PATH may ask winget, which takes a few seconds.
    /// </summary>
    public IReadOnlyList<PrerequisiteStatus> CheckAll(CancellationToken ct = default)
    {
        var list = new List<PrerequisiteStatus>();
        foreach (var (command, _) in HardRequirements)
            list.Add(new PrerequisiteStatus(command, PrerequisiteKind.Required, exists(command)));
        foreach (var cli in AiClis)
            list.Add(new PrerequisiteStatus(cli.Name, PrerequisiteKind.AiCli, exists(cli.Command)));
        foreach (var tool in OptionalTools)
        {
            ct.ThrowIfCancellationRequested();
            list.Add(new PrerequisiteStatus(tool.Name, PrerequisiteKind.Optional, IsInstalled(tool, ct)));
        }
        return list;
    }

    /// <summary>Installs a winget package and, on success, refreshes this process's PATH.</summary>
    public (bool Ok, string Output) InstallViaWinget(string wingetId, CancellationToken ct = default)
    {
        var (code, output) = run("winget", BuildWingetInstallArguments(wingetId), ct);
        if (code == 0) refreshPath();
        return (code == 0, output);
    }

    /// <summary>Whether an optional tool is installed: its command on PATH, one of its install paths, or winget listing it.</summary>
    public bool IsInstalled(OptionalToolSpec tool, CancellationToken ct = default)
    {
        if (tool.CommandOnPath is { Length: > 0 } cmd && exists(cmd)) return true;
        foreach (var path in tool.InstallPaths ?? [])
            if (fileExists(Environment.ExpandEnvironmentVariables(path))) return true;
        try
        {
            var (code, _) = run("winget", BuildWingetListArguments(tool.WingetId), ct);
            return code == 0;
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception) { return false; }
    }

    /// <summary>
    /// Ensures one hard requirement is on PATH, trying each winget id in order until one succeeds. Never raises a UAC prompt
    /// itself; winget raises its own if the package needs it.
    /// </summary>
    public PrerequisiteCheckResult EnsureHard(string command, IReadOnlyList<string> wingetIds, CancellationToken ct = default)
    {
        if (exists(command)) return new PrerequisiteCheckResult(command, Present: true, Installed: false, null);

        string? lastError = null;
        foreach (var id in wingetIds)
        {
            var (ok, output) = InstallViaWinget(id, ct);
            // Present only once the command is really found, not just because winget exited 0 (M.ORCH-T2298).
            if (exists(command)) return new PrerequisiteCheckResult(command, Present: true, Installed: true, null);
            lastError = ok ? $"winget installed {id}, but it still isn't on PATH" : output;
            if (ok) break;
        }
        return new PrerequisiteCheckResult(command, Present: false, Installed: false,
            lastError is null ? $"'{command}' not found and no winget id succeeded." : $"'{command}' not found: {lastError}");
    }

    /// <summary>
    /// Ensures an AI CLI is on PATH: the vendor's install script when it has one, npm for an npm package (installing Node.js via
    /// winget first when npm is missing), winget otherwise. A failure is reported, never thrown.
    /// </summary>
    public PrerequisiteCheckResult EnsureAiCli(AiCliSpec cli, CancellationToken ct = default)
    {
        if (exists(cli.Command)) return new PrerequisiteCheckResult(cli.Command, Present: true, Installed: false, null);
        try { return InstallAiCli(cli, ct); }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return new PrerequisiteCheckResult(cli.Command, false, false, $"'{cli.Command}' not installed: {ex.Message}");
        }
    }

    private PrerequisiteCheckResult InstallAiCli(AiCliSpec cli, CancellationToken ct)
    {
        string? error;
        if (cli.InstallScriptUrl is { Length: > 0 } url)
        {
            // The script adds its bin folder to the user PATH; re-read it so the command is found below.
            var (code, output) = run("powershell.exe", BuildInstallScriptArguments(url), ct);
            if (code == 0) refreshPath();
            error = code == 0 ? null : output;
        }
        else if (cli.NpmPackage is { Length: > 0 } package)
        {
            if (!exists("npm"))
            {
                var (nodeOk, nodeOut) = InstallViaWinget(NodeWingetId, ct);
                if (!nodeOk && !exists("npm"))
                    return new PrerequisiteCheckResult(cli.Command, false, false,
                        $"npm isn't available and Node.js couldn't be installed ({nodeOut}); install Node, then 'npm install -g {package}'.");
            }

            // npm is npm.cmd on Windows; Process.Start only resolves .exe from a bare name.
            var (code, output) = run("npm.cmd", BuildNpmInstallArguments(package), ct);
            if (code == 0) refreshPath();
            error = code == 0 ? null : output;
        }
        else if (cli.WingetId is { Length: > 0 } id)
        {
            var (ok, output) = InstallViaWinget(id, ct);
            error = ok ? null : output;
        }
        else
        {
            error = "no install source";
        }

        return exists(cli.Command)
            ? new PrerequisiteCheckResult(cli.Command, Present: true, Installed: true, null)
            : new PrerequisiteCheckResult(cli.Command, Present: false, Installed: false,
                $"'{cli.Command}' not installed: {error ?? "the install finished, but it still isn't on PATH"}");
    }

    private static bool DefaultCommandExists(string command)
    {
        try
        {
            var psi = new ProcessStartInfo("where")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            psi.ArgumentList.Add(command);
            using var p = Process.Start(psi);
            if (p is null) return false;
            if (!p.WaitForExit(5000)) { try { p.Kill(entireProcessTree: true); } catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception) { } return false; }
            return p.ExitCode == 0;
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception) { return false; }
    }

    private static (int, string) DefaultRun(string executable, IReadOnlyList<string> args, CancellationToken ct)
    {
        var psi = new ProcessStartInfo(executable)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        foreach (var a in args) psi.ArgumentList.Add(a);

        using var p = Process.Start(psi) ?? throw new InvalidOperationException($"Failed to start {executable}.");
        var stdoutTask = Task.Run(() => p.StandardOutput.ReadToEnd(), CancellationToken.None);
        var stderrTask = Task.Run(() => p.StandardError.ReadToEnd(), CancellationToken.None);

        while (!p.HasExited)
        {
            if (ct.IsCancellationRequested) { try { p.Kill(entireProcessTree: true); } catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception) { } break; }
            p.WaitForExit(250);
        }
        try { p.WaitForExit(10_000); } catch (InvalidOperationException) { }
        try { Task.WaitAll([stdoutTask, stderrTask], TimeSpan.FromSeconds(10)); } catch (AggregateException) { }

        int code;
        try { code = p.ExitCode; } catch (InvalidOperationException) { code = -1; }
        var stderr = stderrTask.IsCompletedSuccessfully ? stderrTask.Result : "";
        var stdout = stdoutTask.IsCompletedSuccessfully ? stdoutTask.Result : "";
        return (code, string.Join("\n", stdout, stderr).Trim());
    }
}
