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

    /// <summary>Variables to set (a null value removes one) on top of the caller's environment, which the host and its CLI
    /// inherit. Set on the launching process's environment, never on any command line, so a token here is never visible in a
    /// process list.</summary>
    public IReadOnlyDictionary<string, string?> Environment { get; init; } = new Dictionary<string, string?>();

    /// <summary>How long the host stays after the CLI exits while no viewer has seen it; null for the host's default (10 minutes).</summary>
    public TimeSpan? Linger { get; init; }

    /// <summary>
    /// <paramref name="request"/> as a hosted CLI, built the way <c>TerminalPaneViewModel</c> builds its tab: the command line is
    /// <see cref="PaneRequest.ProcessCommandLine"/> (which clears <c>NO_COLOR</c>, sets <c>COLORTERM</c>, the pane pipe's
    /// <see cref="PaneBroker.InsideVariable"/> so a Launcher inside routes its new CLIs back to the dashboard, and the API key
    /// person's own <c>CLAUDE_CONFIG_DIR</c> and billing variables, M.ORCH-T906), in <see cref="PaneRequest.Directory"/>, with
    /// the caller's environment, as the tab's ConPTY gets the dashboard's. A pane on a login resolves its own token in the
    /// Launcher it runs (M.ORCH-T712), so none is set here; add one to <see cref="Environment"/> when a caller needs it.
    /// </summary>
    public static TermLaunchSpec From(PaneRequest request, string? pipeName = null) => new()
    {
        CommandLine = request.ProcessCommandLine(pipeName),
        Directory = request.Directory,
        Title = request.Title,
        Login = request.Login,
        Repo = request.Name,
    };
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
    /// The host's executable: <see cref="HostPath"/>, else <see cref="HostPathVariable"/>, else <see cref="HostExeName"/> beside
    /// this app or in its <c>TermHost</c> folder. Null when there's none.
    /// </summary>
    public static string? FindHost()
    {
        foreach (var candidate in new[]
                 {
                     HostPath,
                     System.Environment.GetEnvironmentVariable(HostPathVariable),
                     Path.Combine(AppContext.BaseDirectory, HostExeName),
                     Path.Combine(AppContext.BaseDirectory, "TermHost", HostExeName),
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
    /// fails to launch, or exits before registering (its log is <c>terms\&lt;id&gt;.log</c>).
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
                try { launch.Kill(); } catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception) { }
                throw new InvalidOperationException($"TermHost --launch didn't finish (session {id}).");
            }
            output = stdout.GetAwaiter().GetResult();
            error = stderr.GetAwaiter().GetResult();
            exitCode = launch.ExitCode;
        }
        if (exitCode != 0 || ReadLaunch(output) is not { } launched)
            throw new InvalidOperationException($"TermHost --launch failed (exit {exitCode}): {(error.Length > 0 ? error.Trim() : output.Trim())}");

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
                    State = TermSessionStates.Running, StartedUtc = DateTime.UtcNow,
                };
            Thread.Sleep(50);
        }
    }

    /// <summary>The <c>--launch</c> arguments for <paramref name="spec"/> as session <paramref name="id"/>.</summary>
    internal static IReadOnlyList<string> Arguments(TermLaunchSpec spec, string id)
    {
        var args = new List<string> { "--launch", "--id", id };
        if (!string.IsNullOrWhiteSpace(spec.Directory)) args.AddRange(["--cwd", spec.Directory]);
        if (!string.IsNullOrWhiteSpace(spec.Title)) args.AddRange(["--title", spec.Title]);
        if (!string.IsNullOrWhiteSpace(spec.Login)) args.AddRange(["--login", spec.Login]);
        if (!string.IsNullOrWhiteSpace(spec.Repo)) args.AddRange(["--repo", spec.Repo]);
        if (spec.Linger is { } linger) args.AddRange(["--linger", ((int)Math.Max(0, linger.TotalSeconds)).ToString(System.Globalization.CultureInfo.InvariantCulture)]);
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
