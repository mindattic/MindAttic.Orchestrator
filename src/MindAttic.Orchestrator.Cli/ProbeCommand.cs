using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using MindAttic.Orchestrator.Activity;
using MindAttic.Orchestrator.Health;
using MindAttic.Orchestrator.Settings;
using MindAttic.Orchestrator.Status;
using Spectre.Console;
using Spectre.Console.Cli;

/// <summary>
/// The shared half of every <c>orchestrator probe &lt;name&gt;</c> command (M.ORCH-T2022, docs/HEALTH.md section 6): runs the
/// probe's checks, wraps them in a <see cref="HealthReport"/>, publishes it on the app bus (<see cref="AppBusClient.PublishHealth"/>,
/// best effort) and prints it. <c>--once</c> runs one check and exits with the Nagios code of its status (0 ok, 1 warn, 2 critical,
/// 3 when the probe itself couldn't run); without it the probe loops on <c>--interval</c> until Ctrl+C. <c>--json</c> prints the
/// health+json report instead of the summary line (indented once, one compact report per line when looping). A probe only reads
/// and reports, never acts. A new probe is a subclass with a name, a description and <see cref="Check"/>, added to the
/// <c>probe</c> branch in Program.cs.
/// </summary>
internal abstract class ProbeCommand : AsyncCommand<ProbeCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("--once")]
        [Description("Run one check, publish it, print it and exit with its Nagios code (0 ok, 1 warn, 2 critical, 3 the probe couldn't run).")]
        public bool Once { get; init; }

        [CommandOption("--json")]
        [Description("Print the report as health+json instead of the summary line.")]
        public bool Json { get; init; }

        [CommandOption("--interval <SECONDS>")]
        [Description("Seconds between runs when looping, and each run's timeout (default: the health sample setting, 30).")]
        public double? Interval { get; init; }

        public override ValidationResult Validate() =>
            Interval is { } i && !(i >= 1 && i <= 86_400)
                ? ValidationResult.Error("--interval is seconds, 1 to 86400.")
                : ValidationResult.Success();
    }

    /// <summary>The <c>probe</c> name in each report and on the command line (<c>machine</c>, <c>azure-sql</c>, <c>links</c>).</summary>
    protected abstract string Probe { get; }

    /// <summary>The report's one-line description.</summary>
    protected abstract string Description { get; }

    /// <summary>Takes every reading of one run and grades it. A check that can't read reports degraded with a null value and
    /// the reason (<see cref="HealthResult.Graded"/> with NaN), never healthy; throwing means the whole probe couldn't run.</summary>
    protected abstract Task<IReadOnlyList<HealthResult>> Check(OrchestratorSettings s, HealthThresholds t, CancellationToken ct);

    /// <summary>The Nagios code for a probe that couldn't run at all.</summary>
    private const int Unknown = 3;

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        using var stop = new CancellationTokenSource();
        ConsoleCancelEventHandler onCtrlC = (_, e) => { e.Cancel = true; stop.Cancel(); };
        Console.CancelKeyPress += onCtrlC;
        try
        {
            int code;
            do
            {
                // Settings are read every run, so a changed line or interval applies on the next one.
                var s = Ctx.Store().TryLoadSettings(out var loaded) ? loaded : new OrchestratorSettings();
                var interval = TimeSpan.FromSeconds(settings.Interval ?? Math.Clamp(s.HealthSampleSeconds, 1, 86_400));
                var started = Stopwatch.StartNew();
                code = await RunOnce(s, interval, settings, stop.Token);
                if (settings.Once || stop.IsCancellationRequested) break;
                var wait = interval - started.Elapsed;
                if (wait > TimeSpan.Zero)
                    try { await Task.Delay(wait, stop.Token); } catch (OperationCanceledException) { break; }
            }
            while (!stop.IsCancellationRequested);
            return code;
        }
        finally { Console.CancelKeyPress -= onCtrlC; }
    }

    /// <summary>One run: check, publish, print; its Nagios code.</summary>
    private async Task<int> RunOnce(OrchestratorSettings s, TimeSpan timeout, Settings settings, CancellationToken stop)
    {
        var watch = Stopwatch.StartNew();
        IReadOnlyList<HealthResult> results;
        using var run = CancellationTokenSource.CreateLinkedTokenSource(stop);
        run.CancelAfter(timeout);
        try { results = await Check(s, HealthThresholds.Of(s), run.Token).WaitAsync(run.Token); }
        catch (OperationCanceledException) when (!stop.IsCancellationRequested)
        {
            return Failed($"timed out after {timeout.TotalSeconds:0} s");
        }
        catch (OperationCanceledException) { return Unknown; }
        catch (Exception ex) when (ex is not OutOfMemoryException) { return Failed(ex.Message); }

        var report = new HealthReport(Probe, Environment.MachineName, Description, DateTimeOffset.UtcNow, watch.ElapsedMilliseconds,
            (int)Math.Round(timeout.TotalSeconds), results);
        AppBusClient.PublishHealth(report);
        if (settings.Json) Console.WriteLine(report.ToJson(indented: settings.Once));
        else Console.WriteLine((settings.Once ? "" : DateTime.Now.ToString("T", CultureInfo.CurrentCulture) + " ") + Summary(report));
        return HealthGrades.NagiosExit(report.Status);
    }

    /// <summary>Says the probe couldn't run (nothing is published: the hub marks its checks stale on its own).</summary>
    private int Failed(string why)
    {
        Console.Error.WriteLine($"UNKNOWN {Probe}: {why}");
        return Unknown;
    }

    /// <summary>The summary line: the status, then each result that isn't healthy (worst first), or every result when all are,
    /// e.g. <c>WARN memory:commit 86% (&gt;= 80)</c>.</summary>
    internal static string Summary(HealthReport report)
    {
        var shown = report.Status == HealthGrade.Healthy
            ? report.Results
            : report.Results.Where(r => r.Grade != HealthGrade.Healthy).OrderByDescending(r => r.Grade).ToList();
        var parts = shown.Select(Describe).ToList();
        return Word(report.Status) + " " + (parts.Count == 0 ? report.Probe + ": no checks" : string.Join("; ", parts));
    }

    private static string Word(HealthGrade g) => g switch
    {
        HealthGrade.Healthy => "OK",
        HealthGrade.Degraded => "WARN",
        _ => "CRITICAL",
    };

    /// <summary>One result as <c>check[ instance] value unit (op line)</c>, plus its output when it isn't healthy.</summary>
    internal static string Describe(HealthResult r)
    {
        var name = r.ComponentId is { } c ? $"{r.CheckId} {c}" : r.CheckId;
        var value = r.ObservedState ?? (r.ObservedValue is { } v ? Number(v) + Unit(r.ObservedUnit) : "no reading");
        var line = r.Grade switch
        {
            HealthGrade.Unhealthy => r.UnhealthyAt,
            HealthGrade.Degraded => r.DegradedAt,
            _ => null,
        };
        // A line whose degraded mark sits above its unhealthy one is lower-is-worse (free RAM, free disk).
        var op = r.DegradedAt is { } d && r.UnhealthyAt is { } u && d > u ? "<" : ">=";
        var text = line is { } l && r.ObservedValue is not null ? $"{name} {value} ({op} {Number(l)})" : $"{name} {value}";
        return r.Grade != HealthGrade.Healthy && r.Output is { Length: > 0 } o ? $"{text}: {o}" : text;
    }

    private static string Number(double v) => v.ToString(Math.Abs(v) >= 100 ? "#,0" : "0.#", CultureInfo.InvariantCulture);

    private static string Unit(string? unit) => unit switch
    {
        null or "" or "count" => "",
        "%" => "%",
        _ => " " + unit,
    };
}

/// <summary>
/// <c>orchestrator probe machine</c> (M.ORCH-T2022, docs/HEALTH.md section 3): this machine's free RAM (<c>memory:free</c>),
/// commit charge (<c>memory:commit</c>), kernel handles (<c>memory:handles</c>, graded by <see cref="MachineHealth"/>'s own handle
/// lines) and each fixed drive's free space (<c>disk:free</c>), from one <see cref="MachineHealth.Sample"/>. RAM and commit are
/// graded against <see cref="HealthThresholds"/>; when <see cref="MemoryGuard"/> would be past ok on the same reading its level
/// is named in the output. Reads only: it never reclaims memory or holds work the way the guard itself does.
/// </summary>
internal sealed class MachineProbeCommand : ProbeCommand
{
    protected override string Probe => "machine";

    protected override string Description => "RAM, commit/pagefile, handles and every disk";

    protected override Task<IReadOnlyList<HealthResult>> Check(OrchestratorSettings s, HealthThresholds t, CancellationToken ct) =>
        Task.Run(() => Results(MachineHealth.Sample(), s, t, DateTimeOffset.UtcNow), ct);

    private const double Gb = 1024.0 * 1024 * 1024;

    /// <summary>The machine's results for <paramref name="sample"/> (and this machine's drives).</summary>
    internal static IReadOnlyList<HealthResult> Results(HealthSample sample, OrchestratorSettings s, HealthThresholds t, DateTimeOffset now)
    {
        // Sample leaves free RAM at MaxValue when Windows couldn't say; commit then reads 0, so neither is a reading.
        bool memoryRead = sample.FreeRamGb < double.MaxValue;
        var free = memoryRead ? sample.FreeRamGb : double.NaN;
        var commit = memoryRead ? sample.CommitPercent : double.NaN;
        var unbounded = MachineHealth.PagefileUnbounded();

        var results = new List<HealthResult>();

        var freeResult = HealthResult.Graded(HealthCheck.RamFreeGb, t, Math.Round(free, 2), now,
            output: memoryRead ? null : "Windows couldn't report memory");
        results.Add(freeResult with { Output = Join(freeResult.Output, Guard(sample, s, unbounded, memoryRead), Hogs(freeResult, sample)) });

        var commitResult = HealthResult.Graded(HealthCheck.CommitPercent, t, Math.Round(commit, 1), now,
            output: memoryRead ? null : "Windows couldn't report memory");
        results.Add(commitResult with
        {
            Output = Join(commitResult.Output, unbounded ? "pagefile is system-managed, so the commit limit grows with it" : null, Hogs(commitResult, sample)),
        });

        // Handles alone: memory is graded above against the health lines, so it is taken out of MachineHealth's judgment here.
        var (level, why) = MachineHealth.Judge(sample with { CommitPercent = 0, FreeRamGb = double.MaxValue });
        var top = sample.Top.FirstOrDefault();
        results.Add(new HealthResult("memory:handles", null, "system", sample.TotalHandles, "count", HealthGrades.From(level), now,
            Output: why ?? (top is null ? null : $"most: {top.Name} (pid {top.Pid}) {top.Handles:N0}")));

        foreach (var drive in DriveInfo.GetDrives().Where(d => d.DriveType == DriveType.Fixed))
        {
            var id = drive.Name.TrimEnd('\\', '/');
            double gb;
            string? output = null;
            try { gb = drive.IsReady ? drive.AvailableFreeSpace / Gb : double.NaN; if (double.IsNaN(gb)) output = "drive not ready"; }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { gb = double.NaN; output = ex.Message; }
            results.Add(HealthResult.Graded(HealthCheck.DiskFreeGb, t, double.IsNaN(gb) ? gb : Math.Round(gb, 1), now, id, output));
        }
        return results;
    }

    /// <summary>The memory guard's level on this reading when it is past ok (and the guard is on), else null. Judged here
    /// without <see cref="MemoryGuard.Observe"/>, which would start a reclaim: a probe never acts.</summary>
    private static string? Guard(HealthSample sample, OrchestratorSettings s, bool unbounded, bool memoryRead)
    {
        if (!s.MemoryGuard || !memoryRead) return null;
        var reading = new MemoryReading(sample.FreeRamGb, sample.CommitPercent, CommitCounted: !unbounded,
            UsedPercent: MachineHealth.RamUtilizationPercent() ?? 100);
        var (level, _) = MemoryGuard.Judge(reading, s, margin: false);
        return level == MemoryLevel.Ok ? null : $"memory guard would read {level.ToString().ToLowerInvariant()}";
    }

    /// <summary>The biggest memory users, when <paramref name="r"/> isn't healthy.</summary>
    private static string? Hogs(HealthResult r, HealthSample sample) =>
        r.Grade == HealthGrade.Healthy || r.ObservedValue is null || sample.TopMemory.Count == 0
            ? null
            : string.Join(", ", sample.TopMemory.Select(m => $"{m.Name} {m.Gb.ToString("0.0", CultureInfo.InvariantCulture)} GB"));

    private static string? Join(params string?[] parts)
    {
        var kept = parts.Where(p => p is { Length: > 0 }).ToList();
        return kept.Count == 0 ? null : string.Join("; ", kept);
    }
}
