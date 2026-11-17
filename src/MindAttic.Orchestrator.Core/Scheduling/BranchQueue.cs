using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using MindAttic.Orchestrator.Settings;
using MindAttic.Orchestrator.Setup;

namespace MindAttic.Orchestrator.Scheduling;

/// <summary>
/// The owner's PC's copy of its host's ready-to-test queue (M.ORCH-T1634, a M.ORCH-T1501 follow-up). The host's gateway serves
/// <see cref="BranchBuilds.ReadyToTest(IReadOnlyDictionary{string, Dictionary{string, BranchBuilds.BranchState}}, IReadOnlyDictionary{string, Dictionary{string, BranchBuilds.TestState}})"/>
/// (<c>orchestrator branches --ready --json</c> through <c>POST /branches</c>, the owner's interact token only); a MindAttic.Scheduler
/// job on the client (<see cref="JobName"/> under <see cref="JobOwner"/>) runs <c>orchestrator branches fetch</c> every
/// <see cref="OrchestratorSettings.BranchQueueFetchMinutes"/> and keeps what came back here (<see cref="StateName"/>), for the local
/// test run (M.ORCH-T1635) to start from. Fetching never runs a test, builds or checks anything out: it only reads the queue.
/// A fetch that fails keeps the last queue it got and says why, so a host that's briefly away doesn't empty it.
/// </summary>
public static class BranchQueue
{
    /// <summary>The client's store record of the last fetch.</summary>
    public const string StateName = "branch-queue";

    /// <summary>The client's store record of the Scheduler job as it was last registered, so it's only re-registered on a change.</summary>
    public const string JobStateName = "branch-queue-job";

    /// <summary>The Scheduler job's name, under <see cref="JobOwner"/>: <c>\MindAttic\Scheduler\Orchestrator\BranchQueue</c>.</summary>
    public const string JobName = "BranchQueue";

    /// <summary>The Scheduler owner folder the job is registered under.</summary>
    public const string JobOwner = "Orchestrator";

    /// <summary>What the client last fetched: the queue, when it last came back whole, and why the latest fetch didn't (null when it did).</summary>
    public sealed class State
    {
        /// <summary>Gets or sets the host's ready-to-test queue as last fetched whole.</summary>
        public List<BranchBuilds.Ready> Ready { get; set; } = [];

        /// <summary>Gets or sets when the latest fetch was tried.</summary>
        public DateTime? FetchedUtc { get; set; }

        /// <summary>Gets or sets when a fetch last came back whole (<see cref="Ready"/> is from then).</summary>
        public DateTime? LastGoodUtc { get; set; }

        /// <summary>Gets or sets why the latest fetch didn't come back whole; null when it did.</summary>
        public string? Problem { get; set; }
    }

    /// <summary>The Scheduler job as last registered here: how often, and the command it runs.</summary>
    public sealed class JobState
    {
        /// <summary>Gets or sets the job's <c>--every</c> (hh:mm:ss).</summary>
        public string Every { get; set; } = "";

        /// <summary>Gets or sets the command line the job runs.</summary>
        public string Command { get; set; } = "";
    }

    /// <summary>The client's last fetch; empty when there's none or it can't be read.</summary>
    public static State Load(JsonStore store) => store.TryLoad<State>(StateName, out var s) ? s : new State();

    /// <summary>The arguments the host's gateway runs for the queue.</summary>
    public static readonly IReadOnlyList<string> HostArgs = ["branches", "--ready", "--json"];

    private static readonly JsonSerializerOptions Read = new() { PropertyNameCaseInsensitive = true };

    /// <summary>The queue in <c>orchestrator branches --ready --json</c>'s output; null when it isn't one.</summary>
    public static List<BranchBuilds.Ready>? Parse(string? output)
    {
        if (string.IsNullOrWhiteSpace(output)) return null;
        var start = output.IndexOf('[');
        var end = output.LastIndexOf(']');
        if (start < 0 || end < start) return null;
        try
        {
            var list = JsonSerializer.Deserialize<List<BranchBuilds.Ready>>(output[start..(end + 1)], Read);
            return list?.Where(r => r is not null && !string.IsNullOrWhiteSpace(r.Repo) && !string.IsNullOrWhiteSpace(r.Branch) && !string.IsNullOrWhiteSpace(r.Head)).ToList();
        }
        catch (JsonException) { return null; }
    }

    /// <summary>
    /// The next state after a fetch that came back with <paramref name="exitCode"/> and <paramref name="output"/>: the new queue when
    /// it's whole, otherwise <paramref name="previous"/>'s queue kept with why.
    /// </summary>
    public static State Next(State previous, int exitCode, string output, DateTime nowUtc)
    {
        var parsed = exitCode == 0 ? Parse(output) : null;
        if (parsed is not null)
            return new State { Ready = parsed, FetchedUtc = nowUtc, LastGoodUtc = nowUtc };
        var why = FirstLine(output);
        return new State
        {
            Ready = previous.Ready ?? [],
            FetchedUtc = nowUtc,
            LastGoodUtc = previous.LastGoodUtc,
            Problem = exitCode != 0
                ? $"the host answered exit {exitCode}" + (why is null ? "" : $": {why}")
                : "the host's answer wasn't a queue" + (why is null ? "" : $": {why}"),
        };
    }

    /// <summary>
    /// Fetches the host's queue once through <paramref name="runOnHost"/> (the gateway's <c>POST /branches</c>) and keeps it in
    /// <paramref name="store"/>. Runs no test. Returns the state kept.
    /// </summary>
    public static async Task<State> FetchAsync(Func<IReadOnlyList<string>, CancellationToken, Task<(int ExitCode, string Output)>> runOnHost,
        JsonStore store, DateTime nowUtc, CancellationToken ct)
    {
        var (code, output) = await runOnHost(HostArgs, ct).ConfigureAwait(false);
        var next = Next(Load(store), code, output, nowUtc);
        store.Save(StateName, next);
        return next;
    }

    /// <summary>The Scheduler job's <c>--every</c> for <paramref name="minutes"/>: at least a minute, at most a day.</summary>
    public static string Every(double minutes)
    {
        var m = double.IsFinite(minutes) ? Math.Clamp(minutes, 1, 1440) : OrchestratorSettings.DefaultBranchQueueFetchMinutes;
        var span = TimeSpan.FromMinutes(Math.Round(m));
        return span >= TimeSpan.FromDays(1) ? "23:59:00" : span.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture);
    }

    /// <summary>The command line the job runs: the orchestrator CLI's <c>branches fetch</c>.</summary>
    public static string Command(string orchestratorExe) => $"\"{orchestratorExe}\" branches fetch";

    /// <summary>The job wanted on this machine (null: none): only on a client, with the feature on
    /// (<see cref="MachineTopology.FetchesBranchQueue"/>), and an orchestrator CLI to run.</summary>
    public static JobState? Wanted(MachineTopology topology, OrchestratorSettings s, string? orchestratorExe) =>
        topology.FetchesBranchQueue(s.RemoteBranchBuilds) && !string.IsNullOrEmpty(orchestratorExe)
            ? new JobState { Every = Every(s.BranchQueueFetchMinutes), Command = Command(orchestratorExe) }
            : null;

    /// <summary>The installed orchestrator CLI, else the one running now when it is the CLI; null when neither.</summary>
    public static string? OrchestratorExe()
    {
        const string installed = @"C:\Apps\MindAttic.Orchestrator\Cli\orchestrator.exe";
        if (File.Exists(installed)) return installed;
        return Environment.ProcessPath is { } self && Path.GetFileName(self).Equals("orchestrator.exe", StringComparison.OrdinalIgnoreCase) ? self : null;
    }

    /// <summary>MindAttic.Scheduler's <c>scheduler.ps1</c> in the workspace; null when it isn't there.</summary>
    public static string? SchedulerScript(string workspaceRoot)
    {
        try
        {
            var path = Path.Combine(workspaceRoot, "MindAttic.Scheduler", "scheduler.ps1");
            return File.Exists(path) ? path : null;
        }
        catch (ArgumentException) { return null; }
    }

    /// <summary>
    /// Makes the Scheduler job match this machine (M.ORCH-T1634): registered on a client with the feature on, at the Vault's
    /// interval, and cancelled anywhere else. Only calls the Scheduler when what's wanted differs from what was last registered
    /// (<see cref="JobStateName"/>), so it's cheap on every dashboard start and every fetch. Returns what it changed, or null.
    /// Never throws.
    /// </summary>
    public static string? EnsureJob(JsonStore store, OrchestratorSettings s, string workspaceRoot, MachineTopology? topology = null,
        Func<string, IReadOnlyList<string>, (bool Ok, string Output)>? run = null)
    {
        try
        {
            var wanted = Wanted(topology ?? MachineTopology.Current, s, OrchestratorExe());
            var had = store.TryLoad<JobState>(JobStateName, out var j) && j.Every.Length > 0 ? j : null;
            if (wanted is null && had is null) return null;
            if (wanted is not null && had is not null && wanted.Every == had.Every && wanted.Command == had.Command) return null;
            if (SchedulerScript(workspaceRoot) is not { } script) return wanted is null ? null : "couldn't register the branch queue fetch: MindAttic.Scheduler isn't in the workspace";
            run ??= Run;
            if (wanted is null)
            {
                var (ok, output) = run(script, ["job", "cancel", JobName, "--owner", JobOwner]);
                // A job already gone is as good as cancelled.
                store.Save(JobStateName, new JobState());
                return ok ? "cancelled the branch queue fetch job" : $"the branch queue fetch job may still be registered: {FirstLine(output)}";
            }
            var (added, said) = run(script, ["job", "add", JobName, "--owner", JobOwner, "--every", wanted.Every, "--run", wanted.Command,
                "--description", "MindAttic Orchestrator (client): fetches the host's ready-to-test work branches for a local test run (M.ORCH-T1634); runs no test."]);
            if (!added) return $"couldn't register the branch queue fetch job: {FirstLine(said)}";
            store.Save(JobStateName, wanted);
            return $"fetches the host's ready-to-test queue every {wanted.Every} (Scheduler job {JobOwner}\\{JobName})";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return $"couldn't set up the branch queue fetch job: {ex.Message}";
        }
    }

    private static (bool Ok, string Output) Run(string script, IReadOnlyList<string> args)
    {
        try
        {
            var psi = new ProcessStartInfo("powershell.exe") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
            foreach (var a in new[] { "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", script }.Concat(args)) psi.ArgumentList.Add(a);
            using var p = Process.Start(psi)!;
            var stdout = p.StandardOutput.ReadToEndAsync();
            var stderr = p.StandardError.ReadToEndAsync();
            if (!p.WaitForExit(60_000)) { try { p.Kill(true); } catch (InvalidOperationException) { } return (false, "timed out"); }
            return (p.ExitCode == 0, stdout.Result + stderr.Result);
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or IOException)
        {
            return (false, ex.Message);
        }
    }

    private static string? FirstLine(string? text) =>
        text?.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();
}
