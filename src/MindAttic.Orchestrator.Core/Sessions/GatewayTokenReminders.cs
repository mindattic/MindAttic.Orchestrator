using System.Diagnostics;
using System.Text;

namespace MindAttic.Orchestrator.Sessions;

/// <summary>
/// Renewal reminders for a gateway member token (M.ORCH-T1057): a 365-day token (M.ORCH-T1056) would otherwise lapse without
/// warning a year on. When one is minted, a Psst reminder is scheduled at 9:00 local time on each of <see cref="DaysBefore"/> days
/// before it expires, with <c>psst remind --at &lt;date&gt; --name &lt;id&gt; &lt;message&gt;</c> (M.PSST-T25), which registers it through
/// MindAttic.Scheduler and texts the owner's Psst recipient then. Each reminder is named per member and lead time, so re-minting a
/// member's token replaces their reminders. The token itself is never stored.
/// </summary>
public static class GatewayTokenReminders
{
    public static readonly int[] DaysBefore = [30, 14, 7, 5, 3, 2, 1];

    /// <summary>One reminder to schedule: its Psst name, local run time and message.</summary>
    public sealed record Reminder(string Name, DateTime AtLocal, string Message);

    /// <summary>The reminders for <paramref name="member"/>'s token expiring at <paramref name="expiresUtc"/>, those still in the
    /// future at <paramref name="nowLocal"/>.</summary>
    public static IReadOnlyList<Reminder> Plan(string member, DateTime expiresUtc, DateTime nowLocal)
    {
        var expiresLocal = expiresUtc.ToLocalTime();
        var list = new List<Reminder>();
        foreach (var days in DaysBefore)
        {
            var at = expiresLocal.Date.AddDays(-days).AddHours(9);
            if (at <= nowLocal.AddMinutes(1)) continue;
            var plural = days == 1 ? "day" : "days";
            list.Add(new Reminder($"gateway-token-{Slug(member)}-{days}d", at,
                $"Renew the Orchestrator gateway token for {member}: it expires {expiresLocal:yyyy-MM-dd} (in {days} {plural}). " +
                $"Run: orchestrator gateway token {member}"));
        }
        return list;
    }

    /// <summary>Schedules <paramref name="member"/>'s reminders through Psst; returns how many were scheduled and why any were not.</summary>
    public static (int Scheduled, string? Problem) Schedule(string workspaceRoot, string member, DateTime expiresUtc)
    {
        if (FindPsst(workspaceRoot) is not { } psst) return (0, "psst.exe wasn't found (on PATH or in MindAttic.Psst's build output)");
        var done = 0;
        string? problem = null;
        foreach (var r in Plan(member, expiresUtc, DateTime.Now))
        {
            var args = new List<string> { "--silent", "remind", "--at", r.AtLocal.ToString("yyyy-MM-ddTHH:mm"), "--name", r.Name };
            args.AddRange(Plain(r.Message).Split(' ', StringSplitOptions.RemoveEmptyEntries));
            var (ok, output) = Run(psst, args);
            if (ok) done++;
            else problem ??= $"psst remind {r.Name} failed: {output.Trim()}";
        }
        return (done, problem);
    }

    /// <summary>psst.exe: on PATH, else MindAttic.Psst's Release or Debug build output.</summary>
    internal static string? FindPsst(string workspaceRoot)
    {
        foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try { if (File.Exists(Path.Combine(dir.Trim(), "psst.exe"))) return Path.Combine(dir.Trim(), "psst.exe"); }
            catch (ArgumentException) { }
        }
        foreach (var config in new[] { "Release", "Debug" })
        {
            var built = Path.Combine(workspaceRoot, "MindAttic.Psst", "MindAttic.Psst.Cli", "bin", config, "net10.0-windows", "psst.exe");
            if (File.Exists(built)) return built;
        }
        return null;
    }

    internal static string Slug(string s) => new(s.ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray());

    /// <summary>The message as plain words for a scheduled task's command line: no quotes or shell metacharacters.</summary>
    internal static string Plain(string message)
    {
        var sb = new StringBuilder(message.Length);
        foreach (var c in message) sb.Append(c is '"' or '&' or '|' or '<' or '>' or '^' or '%' or '`' ? ' ' : c);
        return sb.ToString();
    }

    private static (bool Ok, string Output) Run(string exe, IEnumerable<string> args)
    {
        try
        {
            var psi = new ProcessStartInfo(exe) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
            foreach (var a in args) psi.ArgumentList.Add(a);
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
}
