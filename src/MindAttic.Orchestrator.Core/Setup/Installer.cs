using System.Diagnostics;
using System.Security;
using System.Text;

namespace MindAttic.Orchestrator.Setup;

/// <summary>
/// Makes the single exe self-installing: drop it anywhere and double-click it. Run from outside the
/// install folder, it copies itself in and hands over to that copy. Every start of the installed copy
/// then checks (cheaply) that the logon daemon task and the shortcuts exist and point at it, and fixes
/// whatever doesn't, so there is no setup step, and a moved or upgraded exe repairs itself.
/// </summary>
public sealed class Installer
{
    public const string ExeName = "MindAttic.Orchestrator.exe";
    public const string TaskName = @"\MindAttic\Orchestrator\Orchestrator daemon";
    public const string DaemonArg = "--daemon";
    public const string ShortcutName = "MindAttic Orchestrator.lnk";

    /// <summary>Held by the running daemon, so the dashboard can tell whether one is up without WMI.</summary>
    public const string DaemonMutex = @"Local\MindAttic.Orchestrator.Daemon";

    /// <summary>The event a dashboard (process <paramref name="pid"/>) waits on to exit as the tray's Exit does: closing its
    /// window only hides it to the tray (M.ORCH-T183), and a hidden one has no window to close at all.</summary>
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
        File.Copy(runningExe, InstalledExe, overwrite: true);
    }

    public void StopInstalledCopies()
    {
        Run("schtasks", "/End", "/TN", TaskName);
        foreach (var p in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(ExeName)))
        {
            try
            {
                if (p.Id == Environment.ProcessId || p.MainModule?.FileName is not { } path || !SamePath(path, InstalledExe)) continue;
                // Asked to exit first, so it disposes its tray icon and flushes its log; killed only if it doesn't.
                if (EventWaitHandle.TryOpenExisting(DashboardExitEvent(p.Id), out var exit))
                    using (exit) { exit.Set(); p.WaitForExit(5000); }
                if (!p.HasExited && p.MainWindowHandle != IntPtr.Zero) { p.CloseMainWindow(); p.WaitForExit(5000); }
                if (!p.HasExited) { p.Kill(); p.WaitForExit(3000); }
            }
            catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or UnauthorizedAccessException) { }
            finally { p.Dispose(); }
        }
    }

    /// <summary>Registers the logon task when it's missing or runs something else; starts the daemon if it isn't running.
    /// Returns what it did, or null when everything was already in place.</summary>
    public string? EnsureDaemon(string exe)
    {
        string? did = null;
        var (code, xml) = Run("schtasks", "/Query", "/TN", TaskName, "/XML");
        if (code != 0 || !TaskRuns(xml, exe))
        {
            var file = Path.Combine(Path.GetTempPath(), $"mindattic-orchestrator-task-{Environment.ProcessId}.xml");
            try
            {
                File.WriteAllText(file, TaskXml(exe, $@"{Environment.UserDomainName}\{Environment.UserName}"), Encoding.Unicode);
                var (created, output) = Run("schtasks", "/Create", "/TN", TaskName, "/XML", file, "/F");
                if (created != 0) throw new InvalidOperationException($"Couldn't register the logon task: {output.Trim()}");
                did = "registered the logon daemon";
            }
            finally { File.Delete(file); }
        }
        if (!DaemonRunning())
        {
            Run("schtasks", "/Run", "/TN", TaskName);
            did = did is null ? "started the daemon" : did + " and started it";
        }
        return did;
    }

    /// <summary>
    /// The logon task: the daemon as you, at logon, with no time limit (Task Scheduler's default kills
    /// tasks after 3 days), restarted if it fails, and one at a time.
    /// </summary>
    public static string TaskXml(string exe, string user) => $"""
        <?xml version="1.0" encoding="UTF-16"?>
        <Task version="1.4" xmlns="http://schemas.microsoft.com/windows/2004/02/mit/task">
          <RegistrationInfo>
            <Description>Keeps MindAttic.Orchestrator's budget, pace and autopilot running. Installed by MindAttic.Orchestrator.exe; it repairs this task whenever it starts.</Description>
          </RegistrationInfo>
          <Triggers>
            <LogonTrigger><Enabled>true</Enabled><UserId>{Escape(user)}</UserId></LogonTrigger>
          </Triggers>
          <Principals>
            <Principal id="Author"><UserId>{Escape(user)}</UserId><LogonType>InteractiveToken</LogonType><RunLevel>LeastPrivilege</RunLevel></Principal>
          </Principals>
          <Settings>
            <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
            <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
            <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
            <StartWhenAvailable>true</StartWhenAvailable>
            <ExecutionTimeLimit>PT0S</ExecutionTimeLimit>
            <RestartOnFailure><Interval>PT1M</Interval><Count>3</Count></RestartOnFailure>
          </Settings>
          <Actions Context="Author">
            <Exec>
              <Command>{Escape(exe)}</Command>
              <Arguments>{DaemonArg}</Arguments>
              <WorkingDirectory>{Escape(Path.GetDirectoryName(exe)!)}</WorkingDirectory>
            </Exec>
          </Actions>
        </Task>
        """;

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

    /// <summary>Removes the task, the shortcuts and the installed exe's companions; the running exe can't delete itself.</summary>
    public void Uninstall()
    {
        StopInstalledCopies();
        Run("schtasks", "/Delete", "/TN", TaskName, "/F");
        foreach (var path in ShortcutPaths())
            try { File.Delete(path); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    private static string Escape(string s) => SecurityElement.Escape(s) ?? s;

    /// <summary>Whether the task's exported XML runs <paramref name="exe"/> as the daemon. Task Scheduler's export escapes only
    /// &amp;, &lt; and &gt; in text, never an apostrophe (C:\Users\o'brien), which <see cref="Escape"/> writes as &amp;apos;: looking
    /// for that form alone never matched, so the task was registered again on every start (M.ORCH-T334).</summary>
    internal static bool TaskRuns(string xml, string exe) => xml.Contains(DaemonArg, StringComparison.Ordinal) &&
        (xml.Contains(Escape(exe), StringComparison.OrdinalIgnoreCase) ||
         xml.Contains(exe.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;"), StringComparison.OrdinalIgnoreCase));

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
