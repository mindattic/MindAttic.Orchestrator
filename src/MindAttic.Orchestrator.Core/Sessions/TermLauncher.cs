using System.Diagnostics;
using System.Text.Json;
using MindAttic.Orchestrator.Term;

namespace MindAttic.Orchestrator.Sessions;

/// <summary>
/// A CLI to start under its own TermHost (M.ORCH-T925): what <see cref="TermLauncher.Start(TermLaunchSpec, TimeSpan?)"/> takes.
/// <see cref="From"/> builds one from a dashboard <see cref="PaneRequest"/>, exactly as a Terminals tab starts it today.
/// </summary>
public sealed record TermLaunchSpec
{
    /// <summary>The CLI's whole Windows command line, run as it is (a <see cref="PaneRequest.ProcessCommandLine"/>).</summary>
    public required string CommandLine { get; init; }

    /// <summary>The folder it runs in; null for the host's default.</summary>
    public string? Directory { get; init; }

    /// <summary>The window title until the CLI sets its own (the tab's whole title, "RD: Repo [Claude]").</summary>
    public string? Title { get; init; }

    /// <summary>The login (an email) it runs on, for the registry; null for the global login.</summary>
    public string? Login { get; init; }

    /// <summary>What it runs for, for the registry.</summary>
    public string? Repo { get; init; }

    /// <summary>The session id; a new one when null.</summary>
    public string? Id { get; init; }

    /// <summary>This is the Launcher's own menu, not a CLI it opened (M.LNCH-T77, M.ORCH-T1256): the registry record says so
    /// (<c>--menu</c>), so the Team Wall can leave it out per the ExcludeLauncherFromTeamWall setting.</summary>
    public bool IsMenu { get; init; }

    /// <summary>Variables to set (a null value removes one) on top of the caller's environment, which the host and its CLI
    /// inherit. Set on the launching process's environment, never on any command line, so a token here is never visible in a
    /// process list.</summary>
    public IReadOnlyDictionary<string, string?> Environment { get; init; } = new Dictionary<string, string?>();

    /// <summary>How long the host stays after the CLI exits while no viewer has seen it; null for the host's default (10 minutes).</summary>
    public TimeSpan? Linger { get; init; }

    /// <summary>Starts the host with <c>--unlisted</c> (M.ORCH-T1444): it registers no file and announces nothing, so no live
    /// dashboard on the machine opens a tab for it. For a caller that deliberately runs a real host beside one.</summary>
    public bool Unlisted { get; init; }

    /// <summary>Starts the host elevated (M.ORCH-T1656): <see cref="TermLauncher.StartElevated(TermLaunchSpec, TimeSpan?)"/> instead
    /// of <see cref="TermLauncher.Start(TermLaunchSpec, TimeSpan?)"/>. <see cref="Environment"/> must be empty — a UAC prompt
    /// can't carry it (see <see cref="TermLauncher.StartElevated(TermLaunchSpec, TimeSpan?)"/>).</summary>
    public bool Elevated { get; init; }

    /// <summary>
    /// <paramref name="request"/> as a hosted CLI, built the way <c>TerminalPaneViewModel</c> builds its tab: the command line is
    /// <see cref="PaneRequest.ProcessCommandLine"/> (which clears <c>NO_COLOR</c>, sets <c>COLORTERM</c>, the pane pipe's
    /// <see cref="PaneBroker.InsideVariable"/> so a Launcher inside routes its new CLIs back to the dashboard, and the API key
    /// person's own <c>CLAUDE_CONFIG_DIR</c> and billing variables, M.ORCH-T906), in <see cref="PaneRequest.Directory"/>, with
    /// the caller's environment, as the tab's ConPTY gets the dashboard's. A pane on a login resolves its own token in the
    /// Launcher it runs (M.ORCH-T712), so none is set here; add one to <see cref="Environment"/> when a caller needs it.
    /// A Codex or Gemini CLI gets its Vault API key's variable (M.ORCH-T2042, <see cref="ProviderCredentials.EnvironmentFor"/>)
    /// in <see cref="Environment"/>, so it starts authenticated; never for an elevated one, whose UAC prompt can't carry it.
    /// </summary>
    public static TermLaunchSpec From(PaneRequest request, string? pipeName = null) => new()
    {
        CommandLine = request.ProcessCommandLine(pipeName),
        Directory = request.Directory,
        Title = request.Title,
        Login = request.Login,
        Repo = request.Name,
        IsMenu = request.IsMenu,
        Unlisted = request.Unlisted,
        Elevated = request.Elevated,
        Environment = request.Elevated ? new Dictionary<string, string?>() : WithAgent(ProviderCredentials.EnvironmentFor(request.Provider), request.AgentId),
    };

    /// <summary><paramref name="environment"/> with the agent id (M.ORCH-T1743) set, so the TermHost stamps it on its record
    /// (<see cref="TermSessionRecord.AgentId"/>); as it is when there's none.</summary>
    private static IReadOnlyDictionary<string, string?> WithAgent(IReadOnlyDictionary<string, string?> environment, string? agentId)
    {
        if (AgentIds.Normalize(agentId) is not { } agent) return environment;
        var with = new Dictionary<string, string?>(environment) { [AgentIds.EnvVar] = agent };
        return with;
    }
}

/// <summary>
/// Starts CLIs under TermHost (M.ORCH-T925), so they outlive the dashboard: runs <c>MindAttic.Orchestrator.TermHost --launch</c>,
/// which re-spawns the host detached (its own process group, broken away from the caller's job) and exits, then waits for the
/// host's registry file and returns its record. The dashboard then attaches to <see cref="TermSessionRecord.Pipe"/>.
/// </summary>
public static class TermLauncher
{
    /// <summary>The host's file name.</summary>
    public const string HostExeName = "MindAttic.Orchestrator.TermHost.exe";

    /// <summary>A variable naming the host's executable, ahead of the folders <see cref="FindHost"/> looks in.</summary>
    public const string HostPathVariable = "MINDATTIC_TERMHOST";

    /// <summary>How long <see cref="Start(TermLaunchSpec, TimeSpan?)"/> waits for a host to register, by default.</summary>
    public static readonly TimeSpan DefaultRegisterWait = TimeSpan.FromSeconds(5);

    /// <summary>The host's executable when set; otherwise <see cref="FindHost"/> looks for it.</summary>
    public static string? HostPath { get; set; }

    /// <summary>
    /// The host's executable: <see cref="HostPath"/>, else <see cref="HostPathVariable"/>, else the newest side-by-side install
    /// (<see cref="TermHostInstalls.ResolveExe"/>, M.ORCH-T932) under the Orchestrator's install folder or this app's own, else
    /// <see cref="HostExeName"/> beside this app (a dev build). Null when there's none.
    /// </summary>
    public static string? FindHost()
    {
        string? Installed(Func<string> installDir)
        {
            try { return TermHostInstalls.ResolveExe(installDir()); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException) { return null; }
        }

        foreach (var candidate in new[]
                 {
                     HostPath,
                     System.Environment.GetEnvironmentVariable(HostPathVariable),
                     Installed(() => new Setup.Installer().InstallDir),
                     Installed(() => AppContext.BaseDirectory),
                     Path.Combine(AppContext.BaseDirectory, HostExeName),
                 })
            if (!string.IsNullOrWhiteSpace(candidate) && File.Exists(candidate)) return Path.GetFullPath(candidate);
        return null;
    }

    /// <summary>Starts <paramref name="request"/> under a TermHost (<see cref="TermLaunchSpec.From"/>); see
    /// <see cref="Start(TermLaunchSpec, TimeSpan?)"/>.</summary>
    public static TermSessionRecord Start(PaneRequest request, string? pipeName = null) => Start(TermLaunchSpec.From(request, pipeName));

    /// <summary>
    /// Starts <paramref name="spec"/> under a detached TermHost and returns its session record, read back from the registry once
    /// the host has registered (within <paramref name="registerWait"/>, <see cref="DefaultRegisterWait"/> by default; a host that
    /// is running but hasn't written its file by then is returned as its launch reported it). Blocks for at most that long plus
    /// the launch: call it off the UI thread. Throws <see cref="InvalidOperationException"/> when the host can't be found,
    /// fails to launch, or exits before registering (its log is <c>terms\&lt;id&gt;.log</c>). The host always gets the session id
    /// (<see cref="TermLaunchSpec.Id"/> or a new one) as <c>--id</c>, so a caller that sets one can find a host that started
    /// although the launch failed (M.ORCH-T971).
    /// </summary>
    public static TermSessionRecord Start(TermLaunchSpec spec, TimeSpan? registerWait = null)
    {
        var host = FindHost() ?? throw new InvalidOperationException(
            $"TermHost isn't installed: no {HostExeName} beside {AppContext.BaseDirectory}, and {HostPathVariable} isn't set.");
        var id = spec.Id ?? TermProtocol.NewSessionId();
        var psi = new ProcessStartInfo(host)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            // The host's own folder, never the CLI's: the launch holds no handle on a repo or worktree.
            WorkingDirectory = Path.GetDirectoryName(host) ?? AppContext.BaseDirectory,
        };
        foreach (var arg in Arguments(spec, id)) psi.ArgumentList.Add(arg);
        foreach (var (key, value) in spec.Environment)
        {
            if (value is null) psi.Environment.Remove(key);
            else psi.Environment[key] = value;
        }

        string output, error;
        int exitCode;
        using (var launch = Process.Start(psi) ?? throw new InvalidOperationException($"Couldn't start {host}."))
        {
            var stdout = launch.StandardOutput.ReadToEndAsync();
            var stderr = launch.StandardError.ReadToEndAsync();
            if (!launch.WaitForExit(TimeSpan.FromSeconds(30)))
            {
                // The tree, so a host it already spawned goes too where it's still a child; a detached one that isn't is found
                // by the caller under this session's id and adopted or ended (M.ORCH-T971).
                try { launch.Kill(entireProcessTree: true); } catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or AggregateException) { }
                throw new InvalidOperationException($"TermHost --launch didn't finish (session {id}).");
            }
            output = stdout.GetAwaiter().GetResult();
            error = stderr.GetAwaiter().GetResult();
            exitCode = launch.ExitCode;
        }
        if (exitCode != 0 || ReadLaunch(output) is not { } launched)
            throw new InvalidOperationException($"TermHost --launch failed (exit {exitCode}): {(error.Length > 0 ? error.Trim() : output.Trim())}");

        // Unlisted (M.ORCH-T1444): the host writes no registry file to poll for, and never will; build the record straight from
        // what --launch printed rather than waiting out registerWait for a file that's never coming.
        if (spec.Unlisted)
        {
            var hostStart = TermSessions.HostStartOf(launched.Pid)
                ?? throw new InvalidOperationException($"TermHost {launched.Pid} exited before it registered; see {Path.Combine(TermSessions.DefaultDirectory, TermProtocol.Sanitize(launched.Id) + ".log")}.");
            return new TermSessionRecord
            {
                Id = launched.Id, HostPid = launched.Pid, HostStartUtc = hostStart, Pipe = launched.Pipe, Title = spec.Title,
                Login = spec.Login, Repo = spec.Repo, Cwd = spec.Directory, CommandLine = spec.CommandLine,
                State = TermSessionStates.Running, StartedUtc = DateTime.UtcNow, IsMenu = spec.IsMenu, AgentId = AgentOf(spec),
            };
        }

        var until = DateTime.UtcNow + (registerWait ?? DefaultRegisterWait);
        while (true)
        {
            if (TermSessions.Get(launched.Id) is { } record) return record;
            var hostStart = TermSessions.HostStartOf(launched.Pid);
            if (hostStart is null)
                throw new InvalidOperationException($"TermHost {launched.Pid} exited before it registered; see {Path.Combine(TermSessions.DefaultDirectory, TermProtocol.Sanitize(launched.Id) + ".log")}.");
            if (DateTime.UtcNow >= until)
                return new TermSessionRecord
                {
                    Id = launched.Id, HostPid = launched.Pid, HostStartUtc = hostStart.Value, Pipe = launched.Pipe, Title = spec.Title,
                    Login = spec.Login, Repo = spec.Repo, Cwd = spec.Directory, CommandLine = spec.CommandLine,
                    State = TermSessionStates.Running, StartedUtc = DateTime.UtcNow, IsMenu = spec.IsMenu, AgentId = AgentOf(spec),
                };
            Thread.Sleep(50);
        }
    }

    /// <summary>How long <see cref="StartElevated"/> waits for the host to register when its caller passes no wait: long enough
    /// for a human to answer the UAC prompt, not just the few seconds <see cref="DefaultRegisterWait"/> gives an already-trusted,
    /// unelevated launch. The dashboard passes the Vault setting <see cref="Settings.OrchestratorSettings.ElevatedTermWaitSeconds"/>
    /// instead (M.ORCH-T1656); this is its default.</summary>
    public static readonly TimeSpan DefaultElevatedWait = TimeSpan.FromSeconds(Settings.OrchestratorSettings.DefaultElevatedTermWaitSeconds);

    /// <summary>
    /// Starts <paramref name="spec"/> under a detached, elevated TermHost (M.ORCH-T1656, M.LNCH-T125): the same
    /// <c>--launch</c> respawn as <see cref="Start(TermLaunchSpec, TimeSpan?)"/>, but through <c>ShellExecuteEx</c> with
    /// <c>runas</c> so Windows prompts for UAC consent first. The CLI <c>--launch</c> respawns (<see cref="Native.SpawnDetached"/>
    /// in <c>Program.cs</c>) inherits that elevated token, so the detached host — and the CLI it starts on its ConPTY — end up
    /// elevated without this process ever touching a handle across the privilege boundary itself.
    /// <para><c>ShellExecuteEx</c> can't redirect standard streams, so unlike <see cref="Start(TermLaunchSpec, TimeSpan?)"/> this
    /// never reads the <c>--launch</c> JSON line; it only waits on the session registry (<see cref="TermSessions.Get"/>), for up
    /// to <paramref name="registerWait"/> (<see cref="DefaultElevatedWait"/> by default — a human has to click through the
    /// prompt). It can't set <see cref="TermLaunchSpec.Environment"/> either (<c>ProcessStartInfo.Environment</c> throws with
    /// <c>UseShellExecute</c>): <paramref name="spec"/> must carry none, which holds for every current caller (a utility shell
    /// needs no Claude login or billing variable).</para>
    /// Throws <see cref="InvalidOperationException"/> when the host can't be found, the user declines the UAC prompt (Win32
    /// error 1223), or no host registers within <paramref name="registerWait"/>.
    /// </summary>
    public static TermSessionRecord StartElevated(TermLaunchSpec spec, TimeSpan? registerWait = null) =>
        StartElevated(spec, registerWait, FindHost, ShellStart, id => TermSessions.Get(id), () => DateTime.UtcNow, Thread.Sleep);

    /// <summary>The real elevated start: <c>ShellExecuteEx</c> with <c>runas</c>, which shows the UAC prompt.</summary>
    private static void ShellStart(ProcessStartInfo psi)
    {
        using var launch = Process.Start(psi);
    }

    /// <summary>Win32's ERROR_CANCELLED: the user declined the UAC prompt.</summary>
    internal const int ErrorCancelled = 1223;

    /// <summary>
    /// <see cref="StartElevated(TermLaunchSpec, TimeSpan?)"/> with its side effects as seams (M.ORCH-T1656), so tests drive it
    /// with fakes and never raise a UAC prompt: <paramref name="findHost"/> finds the host, <paramref name="shellStart"/> runs
    /// the <c>runas</c> start (<see cref="ElevatedStartInfo"/>), <paramref name="lookup"/> reads the session registry, and
    /// <paramref name="now"/> and <paramref name="sleep"/> are the clock.
    /// </summary>
    internal static TermSessionRecord StartElevated(TermLaunchSpec spec, TimeSpan? registerWait, Func<string?> findHost,
        Action<ProcessStartInfo> shellStart, Func<string, TermSessionRecord?> lookup, Func<DateTime> now, Action<int> sleep)
    {
        if (spec.Environment.Count > 0)
            throw new NotSupportedException("An elevated TermHost can't carry Environment variables: UseShellExecute can't set them, " +
                "and nothing here puts a secret on its command line instead.");

        var host = findHost() ?? throw new InvalidOperationException(
            $"TermHost isn't installed: no {HostExeName} beside {AppContext.BaseDirectory}, and {HostPathVariable} isn't set.");
        var id = spec.Id ?? TermProtocol.NewSessionId();
        var wait = registerWait ?? DefaultElevatedWait;

        try { shellStart(ElevatedStartInfo(host, spec, id)); }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == ErrorCancelled)
        {
            throw new InvalidOperationException("Elevation was cancelled.");
        }

        var until = now() + wait;
        while (true)
        {
            if (lookup(id) is { } record) return record;
            if (now() >= until)
                throw new InvalidOperationException($"The elevated TermHost (session {id}) didn't register within " +
                    $"{wait.TotalSeconds:0}s; see {Path.Combine(TermSessions.DefaultDirectory, TermProtocol.Sanitize(id) + ".log")}.");
            sleep(50);
        }
    }

    /// <summary>The <c>runas</c> start of <paramref name="host"/>'s <c>--launch</c> respawn for <paramref name="spec"/> as session
    /// <paramref name="id"/> (M.ORCH-T1656): shell-executed (the only way to get the UAC prompt), from the host's own folder,
    /// with exactly the arguments an unelevated <see cref="Start(TermLaunchSpec, TimeSpan?)"/> passes.</summary>
    internal static ProcessStartInfo ElevatedStartInfo(string host, TermLaunchSpec spec, string id)
    {
        var psi = new ProcessStartInfo(host)
        {
            UseShellExecute = true,
            Verb = "runas",
            WorkingDirectory = Path.GetDirectoryName(host) ?? AppContext.BaseDirectory,
        };
        foreach (var arg in Arguments(spec, id)) psi.ArgumentList.Add(arg);
        return psi;
    }

    /// <summary>The <c>--launch</c> arguments for <paramref name="spec"/> as session <paramref name="id"/>.</summary>
    /// <summary>The agent id (M.ORCH-T1743) <paramref name="spec"/> hands its host, for a record built here; null for none.</summary>
    private static string? AgentOf(TermLaunchSpec spec) =>
        spec.Environment.TryGetValue(AgentIds.EnvVar, out var agent) ? AgentIds.Normalize(agent) : null;

    internal static IReadOnlyList<string> Arguments(TermLaunchSpec spec, string id)
    {
        var args = new List<string> { "--launch", "--id", id };
        if (!string.IsNullOrWhiteSpace(spec.Directory)) args.AddRange(["--cwd", spec.Directory]);
        if (!string.IsNullOrWhiteSpace(spec.Title)) args.AddRange(["--title", spec.Title]);
        if (!string.IsNullOrWhiteSpace(spec.Login)) args.AddRange(["--login", spec.Login]);
        if (!string.IsNullOrWhiteSpace(spec.Repo)) args.AddRange(["--repo", spec.Repo]);
        if (spec.Linger is { } linger) args.AddRange(["--linger", ((int)Math.Max(0, linger.TotalSeconds)).ToString(System.Globalization.CultureInfo.InvariantCulture)]);
        if (spec.IsMenu) args.Add("--menu");
        if (spec.Unlisted) args.Add("--unlisted");
        // One argument: the host runs a single argument with a space in it as a whole command line.
        if (!string.IsNullOrWhiteSpace(spec.CommandLine)) args.AddRange(["--", spec.CommandLine]);
        return args;
    }

    /// <summary>The <c>{"id","pid","pipe"}</c> line <c>--launch</c> prints; null when there's none.</summary>
    internal static (string Id, int Pid, string Pipe)? ReadLaunch(string output)
    {
        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!line.StartsWith('{')) continue;
            try
            {
                using var doc = JsonDocument.Parse(line);
                var root = doc.RootElement;
                if (root.TryGetProperty("id", out var id) && root.TryGetProperty("pid", out var pid) && root.TryGetProperty("pipe", out var pipe)
                    && id.GetString() is { Length: > 0 } idText && pid.TryGetInt32(out var pidValue) && pipe.GetString() is { Length: > 0 } pipeText)
                    return (idText, pidValue, pipeText);
            }
            catch (JsonException) { }
        }
        return null;
    }
}
