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

    /// <summary>A dev build (bin\Debug, bin\Release, dotnet run) never installs itself.</summary>
    public static bool IsDevBuild(string exePath) =>
        exePath.Contains(@"\bin\Debug\", StringComparison.OrdinalIgnoreCase) ||
        exePath.Contains(@"\bin\Release\", StringComparison.OrdinalIgnoreCase);

    public static bool SamePath(string a, string b) =>
        string.Equals(Path.GetFullPath(a).TrimEnd('\\'), Path.GetFullPath(b).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase);

    public bool IsInstalled(string runningExe) => SamePath(runningExe, InstalledExe);

    public static bool DaemonRunning() => Mutex.TryOpenExisting(DaemonMutex, out var m) && Dispose(m);

    private static bool Dispose(IDisposable d) { d.Dispose(); return true; }

    /// <summary>
    /// Copies <paramref name="runningExe"/> over the installed copy. An installed daemon or dashboard
    /// holds that file open, so they're stopped first (it's an upgrade; the daemon restarts after).
    /// </summary>
    public void Install(string runningExe)
    {
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
                if (p.MainWindowHandle != IntPtr.Zero) { p.CloseMainWindow(); p.WaitForExit(5000); }
                if (!p.HasExited) { p.Kill(); p.WaitForExit(3000); }
            }
            catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception) { }
            finally { p.Dispose(); }
        }
    }

    /// <summary>Registers the logon task when it's missing or runs something else; starts the daemon if it isn't running.
    /// Returns what it did, or null when everything was already in place.</summary>
    public string? EnsureDaemon(string exe)
    {
        string? did = null;
        var (code, xml) = Run("schtasks", "/Query", "/TN", TaskName, "/XML");
        if (code != 0 || !xml.Contains(Escape(exe), StringComparison.OrdinalIgnoreCase) || !xml.Contains(DaemonArg, StringComparison.Ordinal))
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
