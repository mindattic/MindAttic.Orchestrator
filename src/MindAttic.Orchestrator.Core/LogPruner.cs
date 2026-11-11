using MindAttic.Orchestrator.Settings;

namespace MindAttic.Orchestrator;

/// <summary>
/// Hourly (M.ORCH-T1611): trims the MindAttic.Log database to <c>LogKeepDays</c> (the library's own retention is a fixed 365
/// days) and deletes the <c>terms\&lt;id&gt;.log</c> files of finished terminal hosts (no live <c>&lt;id&gt;.json</c> beside them)
/// older than <c>TermLogKeepDays</c>; nothing else ever removed those files.
/// </summary>
public sealed class LogPruner
{
    public static readonly TimeSpan Every = TimeSpan.FromHours(1);

    private DateTime? lastUtc;

    public void RunIfDue(OrchestratorSettings s, Action<string> say) => RunIfDue(s, say, DateTime.UtcNow, Term.TermSessions.DefaultDirectory);

    internal void RunIfDue(OrchestratorSettings s, Action<string> say, DateTime now, string termsDir)
    {
        if (lastUtc is { } at && now - at < Every) return;
        lastUtc = now;
        try
        {
            var rows = OrchestratorLog.Prune(s.LogKeepDays, now);
            if (rows > 0) say($"deleted {rows} log entries older than {s.LogKeepDays} days");
            var gone = PruneTermLogs(termsDir, s.TermLogKeepDays, now);
            if (gone > 0) say($"deleted {gone} finished terminal hosts' log files older than {s.TermLogKeepDays} days");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { say($"old logs: couldn't prune: {ex.Message}"); }
    }

    internal static int PruneTermLogs(string dir, int keepDays, DateTime nowUtc)
    {
        if (keepDays <= 0 || !Directory.Exists(dir)) return 0;
        var cutoff = nowUtc.AddDays(-Math.Min(keepDays, 36500));
        var gone = 0;
        foreach (var log in Directory.GetFiles(dir, "*.log"))
        {
            if (File.GetLastWriteTimeUtc(log) >= cutoff || File.Exists(Path.ChangeExtension(log, ".json"))) continue;
            try { File.Delete(log); gone++; } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
        return gone;
    }
}
