using System.Diagnostics;
using System.Text;

namespace MindAttic.Orchestrator.Setup;

/// <summary>
/// Makes the single exe self-installing: drop it anywhere and double-click it. Run from outside the
/// install folder, it copies itself in and hands over to that copy. Every start of the installed copy
/// then checks (cheaply) that the daemon is running and the shortcuts exist and point at it, and fixes
/// whatever doesn't, so there is no setup step, and a moved or upgraded exe repairs itself. Starting the
/// daemon at logon and keeping it running is MindAttic.Scheduler's job (its Orchestrator.Daemon job, M.WD-T5,
/// runs this exe with <see cref="DaemonArg"/>); this exe no longer registers a scheduled task of its own (M.ORCH-T475).
/// </summary>
public sealed class Installer
{
    public const string ExeName = "MindAttic.Orchestrator.exe";
    /// <summary>The logon task this exe registered before WatchDog took the daemon over (M.ORCH-T475). Never registered
    /// now; still ended on an upgrade and deleted on an uninstall, for a machine that has it yet.</summary>
    public const string LegacyTaskName = @"\MindAttic\Orchestrator\Orchestrator daemon";
    public const string DaemonArg = "--daemon";
    public const string ShortcutName = "MindAttic Orchestrator.lnk";

    /// <summary>Held by the running daemon, so the dashboard can tell whether one is up without WMI.</summary>
    public const string DaemonMutex = @"Local\MindAttic.Orchestrator.Daemon";

    /// <summary>The event a dashboard (process <paramref name="pid"/>) waits on to exit: an upgrade's way to close it, and a newer
    /// dashboard's way to retire one an older build left hidden in the tray (M.ORCH-T183), which has no window to close.</summary>
    public static string DashboardExitEvent(int pid) => $@"Local\MindAttic.Orchestrator.Dashboard.Exit.{pid}";

    public static readonly string PreferredDir = @"C:\Apps\MindAttic.Orchestrator";
    public static readonly string FallbackDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "MindAttic.Orchestrator");

    public Installer() : this(ChooseInstallDir(PreferredDir, FallbackDir)) { }

    public Installer(string installDir) => InstallDir = installDir;

    public string InstallDir { get; }
    public string InstalledExe => Path.Combine(InstallDir, ExeName);

    /// <summary>The house folder (C:\Apps) when it's writable or already holds the install; otherwise per-user Programs.</summary>
    internal static string ChooseInstallDir(string preferred, string fallback)
    {
        if (File.Exists(Path.Combine(preferred, ExeName))) return preferred;
        if (File.Exists(Path.Combine(fallback, ExeName))) return fallback;
        try
        {
            Directory.CreateDirectory(preferred);
            var probe = Path.Combine(preferred, $".write-test-{Environment.ProcessId}");
            File.WriteAllText(probe, "");
            File.Delete(probe);
            return preferred;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return fallback; }
    }

    /// <summary>
    /// A dev or test build never installs itself on a double-click: bin\Debug, bin\Release, obj, anything
    /// inside a git checkout or worktree (a repo's artifacts folder, a worktree's publish), a *.worktrees
    /// folder, or the temp folder (an agent's scratch copy). Only <c>--install</c> installs those.
    /// </summary>
    public static bool IsDevBuild(string exePath) => IsDevBuild(exePath, Path.GetTempPath());

    internal static bool IsDevBuild(string exePath, string tempDir)
    {
        if (exePath.Contains(@"\bin\Debug\", StringComparison.OrdinalIgnoreCase) ||
            exePath.Contains(@"\bin\Release\", StringComparison.OrdinalIgnoreCase) ||
            exePath.Contains(@"\obj\", StringComparison.OrdinalIgnoreCase) ||
            exePath.Contains(@".worktrees\", StringComparison.OrdinalIgnoreCase))
            return true;
        string full;
        try { full = Path.GetFullPath(exePath); }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException) { return false; }
        if (!string.IsNullOrWhiteSpace(tempDir))
        {
            var temp = Path.GetFullPath(tempDir).TrimEnd('\\') + '\\';
            if (full.StartsWith(temp, StringComparison.OrdinalIgnoreCase)) return true;
        }
        // A worktree's .git is a file, a checkout's a folder.
        for (var dir = Path.GetDirectoryName(full); !string.IsNullOrEmpty(dir); dir = Path.GetDirectoryName(dir))
        {
            var git = Path.Combine(dir, ".git");
            if (Directory.Exists(git) || File.Exists(git)) return true;
        }
        return false;
    }

    /// <summary>
    /// Why <paramref name="runningExe"/> mustn't replace the installed copy, or null when it may: an
    /// installed exe built later than this one is never downgraded (a copy keeps its build's write time).
    /// </summary>
    public string? WhyNotReplace(string runningExe)
    {
        if (IsInstalled(runningExe) || !File.Exists(InstalledExe)) return null;
        var installed = File.GetLastWriteTimeUtc(InstalledExe);
        var mine = File.GetLastWriteTimeUtc(runningExe);
        return installed > mine
            ? $"the installed {InstalledExe} ({installed.ToLocalTime():yyyy-MM-dd HH:mm}) is newer than this build ({mine.ToLocalTime():yyyy-MM-dd HH:mm})"
            : null;
    }

    /// <summary>Whether two paths name the same file; an empty one (a shortcut with no icon set reports ",0") names none.</summary>
    public static bool SamePath(string a, string b) =>
        !string.IsNullOrWhiteSpace(a) && !string.IsNullOrWhiteSpace(b) && string.Equals(Path.GetFullPath(a).TrimEnd('\\'), Path.GetFullPath(b).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase);

    public bool IsInstalled(string runningExe) => SamePath(runningExe, InstalledExe);

    public static bool DaemonRunning() => Mutex.TryOpenExisting(DaemonMutex, out var m) && Dispose(m);

    private static bool Dispose(IDisposable d) { d.Dispose(); return true; }

    /// <summary>
    /// Copies <paramref name="runningExe"/> over the installed copy. An installed daemon or dashboard
    /// holds that file open, so they're stopped first (it's an upgrade; the daemon restarts after).
    /// </summary>
    public void Install(string runningExe, bool force = false)
    {
        if (!force && WhyNotReplace(runningExe) is { } why) throw new IOException($"Not installing: {why}. Pass --force to install it anyway.");
        Directory.CreateDirectory(InstallDir);
        StopInstalledCopies();
        try { File.Copy(runningExe, InstalledExe, overwrite: true); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Stopped for nothing (the old exe still locked, say by an elevated dashboard): the old install's daemon runs
            // again, or pacing and the hooks' holds stay off until the next logon (M.ORCH-T353).
            // The copy's failure is what's reported: a daemon that can't start too (no old exe there) mustn't replace it.
            try { if (!DaemonRunning()) StartDaemon(InstalledExe); }
            catch (InvalidOperationException) { }
            throw;
        }
    }

    public void StopInstalledCopies()
    {
        Run("schtasks", "/End", "/TN", LegacyTaskName);
        foreach (var p in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(ExeName)))
        {
            try
            {
                if (p.Id == Environment.ProcessId || p.MainModule?.FileName is not { } path || !SamePath(path, InstalledExe)) continue;
                // Asked to exit first, so it flushes its log (and an older build disposes its tray icon); killed only if it doesn't.
                if (EventWaitHandle.TryOpenExisting(DashboardExitEvent(p.Id), out var exit))
                    using (exit) { exit.Set(); p.WaitForExit(5000); }
                if (!p.HasExited && p.MainWindowHandle != IntPtr.Zero) { p.CloseMainWindow(); p.WaitForExit(5000); }
                if (!p.HasExited) { p.Kill(); p.WaitForExit(3000); }
            }
            catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or UnauthorizedAccessException) { }
            finally { p.Dispose(); }
        }
    }

    /// <summary>Starts the daemon from <paramref name="exe"/> when none is running, as Scheduler would at logon: after an upgrade
    /// stopped it, pacing and the hooks' holds don't stay off until the next logon. Registers nothing (M.ORCH-T475).
    /// Returns what it did, or null when one was already up.</summary>
    public string? EnsureDaemon(string exe)
    {
        if (DaemonRunning()) return null;
        StartDaemon(exe);
        return "started the daemon";
    }

    /// <summary>How the daemon is started: <paramref name="exe"/> <see cref="DaemonArg"/>, from its own folder, windowless.</summary>
    public static ProcessStartInfo DaemonStartInfo(string exe)
    {
        var psi = new ProcessStartInfo(exe) { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = Path.GetDirectoryName(exe)! };
        psi.ArgumentList.Add(DaemonArg);
        return psi;
    }

    private static void StartDaemon(string exe)
    {
        try { Process.Start(DaemonStartInfo(exe))?.Dispose(); }
        catch (System.ComponentModel.Win32Exception ex) { throw new InvalidOperationException($"Couldn't start the daemon: {ex.Message}", ex); }
    }

    public static IReadOnlyList<string> ShortcutPaths() =>
    [
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "MindAttic", ShortcutName),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), ShortcutName),
    ];

    /// <summary>Start menu and desktop shortcuts to <paramref name="exe"/>, showing its embedded icon; only (re)written
    /// when missing, pointing elsewhere or taking their icon from elsewhere.</summary>
    public static string? EnsureShortcuts(string exe)
    {
        var shellType = Type.GetTypeFromProgID("WScript.Shell");
        if (shellType is null) return null;
        dynamic shell = Activator.CreateInstance(shellType)!;
        var icon = exe + ",0";
        var made = 0;
        foreach (var path in ShortcutPaths())
        {
            dynamic link = shell.CreateShortcut(path);
            if (File.Exists(path) && SamePath((string)link.TargetPath, exe)
                && SamePath(((string)link.IconLocation).Split(',')[0], exe)) continue;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            link.TargetPath = exe;
            link.IconLocation = icon;
            link.WorkingDirectory = Path.GetDirectoryName(exe);
            link.Description = "Every agent session across the workspace: status, budget and pace, the ticket queue";
            link.Save();
            made++;
        }
        return made == 0 ? null : $"made {made} shortcut{(made == 1 ? "" : "s")}";
    }

    /// <summary>Removes the legacy logon task (if it's still there), the shortcuts, the installed exe's companions and the gate hooks it registered in every CLI's
    /// settings (M.ORCH-T439); the running exe can't delete itself. Returns what was done to the hooks, or null when none were there.</summary>
    public string? Uninstall()
    {
        StopInstalledCopies();
        Run("schtasks", "/Delete", "/TN", LegacyTaskName, "/F");
        foreach (var path in ShortcutPaths())
            try { File.Delete(path); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        // After the daemon is stopped, so it can't register them again.
        return AgentGateRemoval.Remove();
    }

    private static (int Code, string Output) Run(string file, params string[] args)
    {
        var psi = new ProcessStartInfo(file)
        {
            RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
        };
        foreach (var a in args) psi.ArgumentList.Add(a);
        try
        {
            using var p = Process.Start(psi)!;
            var stdout = p.StandardOutput.ReadToEndAsync();
            var stderr = p.StandardError.ReadToEnd();
            p.WaitForExit();
            return (p.ExitCode, stdout.Result + stderr);
        }
        catch (System.ComponentModel.Win32Exception ex) { return (-1, ex.Message); }
    }
}
