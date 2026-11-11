using System.Diagnostics;
using System.Management;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace MindAttic.Orchestrator.Sessions;

/// <summary>
/// Ends TermHosts a test run left behind (M.ORCH-T1623): a test that opens a real hosted tab starts a TermHost (with its
/// powershell and conhost) whose <c>--cwd</c> is an <c>orch-test-*</c> temp folder; one a failed test never closed lived on
/// all night (6 found 2026-11-11, the oldest 22:59 the night before). The daemon sweeps for them as a safety net (every
/// <see cref="Settings.OrchestratorSettings.TestTermHostSweepMinutes"/>, M.ORCH-T1624), and the test
/// suite's own teardown ends the ones it started (<see cref="EndIn"/>). Only a host whose folder is an <c>orch-test-*</c> folder
/// directly under the temp folder, or inside one, is ever touched: any other host is left alone.
/// </summary>
public static class TestTermHostReaper
{
    /// <summary>The name a test's temp folder starts with (the tests' TempDirs).</summary>
    public const string TestFolderPrefix = "orch-test-";

    /// <summary>The host's process name, as WMI lists it.</summary>
    private const string HostProcessName = "MindAttic.Orchestrator.TermHost.exe";

    public sealed record HostInfo(int Pid, DateTime StartUtc, string? CommandLine);

    public sealed record Ended(HostInfo Host, string Cwd, string Reason);

    private static readonly Regex CwdArgument = new("""(?:^|\s)--cwd\s+(?:"(?<q>[^"]*)"|(?<p>\S+))""", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>The <c>--cwd</c> a host's command line names, or null.</summary>
    public static string? CwdOf(string? commandLine)
    {
        if (string.IsNullOrWhiteSpace(commandLine)) return null;
        var m = CwdArgument.Match(commandLine);
        if (!m.Success) return null;
        var value = m.Groups["q"].Success ? m.Groups["q"].Value : m.Groups["p"].Value;
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    /// <summary>Whether <paramref name="cwd"/> is an <c>orch-test-*</c> folder directly under <paramref name="tempRoot"/>, or inside one.</summary>
    public static bool IsTestFolder(string? cwd, string tempRoot)
    {
        if (string.IsNullOrWhiteSpace(cwd) || string.IsNullOrWhiteSpace(tempRoot)) return false;
        try
        {
            var root = Path.GetFullPath(tempRoot).TrimEnd('\\', '/');
            var full = Path.GetFullPath(cwd).TrimEnd('\\', '/');
            if (!full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) return false;
            var first = full[(root.Length + 1)..].Split('\\', '/')[0];
            return first.StartsWith(TestFolderPrefix, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException) { return false; }
    }

    /// <summary>
    /// Which of <paramref name="hosts"/> to end: those running in a test folder that's gone (<paramref name="exists"/> false) or
    /// that started more than <paramref name="maxAge"/> ago. A host in any other folder, or with no folder named, is never selected.
    /// </summary>
    public static IReadOnlyList<Ended> Select(IEnumerable<HostInfo> hosts, DateTime nowUtc, TimeSpan maxAge, string tempRoot, Func<string, bool> exists)
    {
        var result = new List<Ended>();
        foreach (var host in hosts)
        {
            var cwd = CwdOf(host.CommandLine);
            if (cwd is null || !IsTestFolder(cwd, tempRoot)) continue;
            if (!exists(cwd)) result.Add(new Ended(host, cwd, "its test folder no longer exists"));
            else if (host.StartUtc != ProcessReaper.Unknown && nowUtc - host.StartUtc > maxAge)
                result.Add(new Ended(host, cwd, $"a test's host running {MindAttic.Orchestrator.Formatting.Ago(host.StartUtc, nowUtc)}, over {maxAge.TotalMinutes:0.#} min"));
        }
        return result;
    }

    /// <summary>The daemon's sweep: ends every leaked test host (and its process tree). Best effort; never throws.</summary>
    public static IReadOnlyList<Ended> ReapOnce(DateTime nowUtc, TimeSpan maxAge)
    {
        var ended = Select(Snapshot(), nowUtc, maxAge, Path.GetTempPath(), Directory.Exists);
        foreach (var e in ended) Kill(e.Host);
        return ended;
    }

    /// <summary>The test suite's teardown: ends every TermHost running in one of <paramref name="folders"/> (or inside one), with its
    /// process tree, and waits briefly for them to be gone so the folders can be deleted. Never throws.</summary>
    public static int EndIn(IEnumerable<string> folders)
    {
        var wanted = new List<string>();
        foreach (var f in folders)
            try { wanted.Add(Path.GetFullPath(f).TrimEnd('\\', '/')); } catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException) { }
        if (wanted.Count == 0) return 0;
        var count = 0;
        foreach (var host in Snapshot())
        {
            var cwd = CwdOf(host.CommandLine);
            if (cwd is null) continue;
            string full;
            try { full = Path.GetFullPath(cwd).TrimEnd('\\', '/'); } catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException) { continue; }
            if (!wanted.Any(w => full.Equals(w, StringComparison.OrdinalIgnoreCase) || full.StartsWith(w + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))) continue;
            if (!IsTestFolder(full, Path.GetTempPath())) continue; // belt and braces: only a test folder
            if (Kill(host)) count++;
        }
        return count;
    }

    private static bool Kill(HostInfo host)
    {
        try
        {
            using var p = Process.GetProcessById(host.Pid);
            // Its id may have gone to another process since the scan.
            if (host.StartUtc != ProcessReaper.Unknown && !BackgroundAgent.SameStart(p.StartTime.ToUniversalTime(), host.StartUtc)) return false;
            p.Kill(true);
            p.WaitForExit(5000);
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception or AggregateException) { return false; }
    }

    private static IReadOnlyList<HostInfo> Snapshot()
    {
        var result = new List<HostInfo>();
        try
        {
            using var searcher = new ManagementObjectSearcher(
                $"SELECT ProcessId, CreationDate, CommandLine FROM Win32_Process WHERE Name = '{HostProcessName}'");
            using var found = searcher.Get();
            foreach (var o in found)
                using (o)
                {
                    if (o["ProcessId"] is not uint pid) continue;
                    var started = ProcessReaper.Unknown;
                    if (o["CreationDate"] is string cd)
                        try { started = ManagementDateTimeConverter.ToDateTime(cd).ToUniversalTime(); } catch (Exception ex) when (ex is FormatException or ArgumentException) { }
                    result.Add(new HostInfo((int)pid, started, o["CommandLine"] as string));
                }
        }
        catch (ManagementException ex) { OrchestratorLog.For("TestTermHostReaper").Warning(ex, "The WMI TermHost scan failed; contained"); }
        catch (COMException ex) { OrchestratorLog.For("TestTermHostReaper").Warning(ex, "The WMI TermHost scan failed; contained"); }
        return result;
    }
}
