using System.Diagnostics;
using System.IO;

namespace MindAttic.Orchestrator.Wpf;

/// <summary>
/// The dashboard's Tickets button (M.ORCH-T878): opens Tickets Desktop, or brings its window forward when it's already
/// running. The exe is where MindAttic.Tickets/deploy.ps1 installs it: the configured
/// <see cref="OrchestratorOptions.TicketsDesktopExe"/> (C:\Apps\...), else %LOCALAPPDATA%\Programs\... when there's no C:\Apps.
/// Never throws.
/// </summary>
public static class TicketsApp
{
    private const string Name = "MindAttic.Tickets.Desktop";

    /// <summary>Opens or focuses Tickets Desktop; returns why it couldn't, or null once it's up.</summary>
    public static string? OpenOrFocus(OrchestratorOptions options)
    {
        try
        {
            if (RunningWindow() is { } hwnd)
            {
                TerminalFocus.Foreground(hwnd);
                return null;
            }

            if (Resolve(options) is not { } exe)
                return $"Tickets Desktop isn't installed (looked for {options.TicketsDesktopExe}; run MindAttic.Tickets/deploy.ps1).";

            var psi = new ProcessStartInfo(exe) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(exe)! };
            if (Directory.Exists(options.WorkspaceRoot)) { psi.ArgumentList.Add("--root"); psi.ArgumentList.Add(options.WorkspaceRoot); }
            using var _ = Process.Start(psi);
            return null;
        }
        catch (Exception ex)
        {
            return $"Couldn't open Tickets Desktop: {ex.Message}";
        }
    }

    /// <summary>The main window of a running Tickets Desktop, if any.</summary>
    private static IntPtr? RunningWindow()
    {
        IntPtr? found = null;
        foreach (var p in Process.GetProcessesByName(Name))
        {
            using (p)
            {
                try
                {
                    if (found is null && p.MainWindowHandle != IntPtr.Zero) found = p.MainWindowHandle;
                }
                catch (Exception) { /* exited or inaccessible: skip it */ }
            }
        }
        return found;
    }

    private static string? Resolve(OrchestratorOptions options)
    {
        string[] candidates =
        [
            options.TicketsDesktopExe,
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", Name, Name + ".exe"),
        ];
        return candidates.FirstOrDefault(c => !string.IsNullOrWhiteSpace(c) && File.Exists(c));
    }
}
