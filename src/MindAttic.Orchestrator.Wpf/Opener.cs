using System.ComponentModel;
using System.Diagnostics;
using System.IO;

namespace MindAttic.Orchestrator.Wpf;

/// <summary>Hands files and folders to the tools you already use.</summary>
public static class Opener
{
    public static void Reveal(string path)
    {
        if (File.Exists(path)) Start("explorer.exe", $"/select,\"{path}\"");
        else if (Directory.Exists(path)) Start("explorer.exe", $"\"{path}\"");
    }

    public static void Open(string path)
    {
        if (!File.Exists(path)) { Reveal(path); return; }
        try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true })?.Dispose(); }
        catch (Win32Exception) { Reveal(path); }
    }

    /// <summary>Side-by-side diff in VS Code when it's installed; otherwise opens both files.</summary>
    public static void Diff(string left, string right)
    {
        try
        {
            var psi = new ProcessStartInfo("cmd.exe") { UseShellExecute = false, CreateNoWindow = true };
            foreach (var a in new[] { "/c", "code", "--diff", left, right }) psi.ArgumentList.Add(a);
            using var p = Process.Start(psi)!;
            if (p.WaitForExit(5000) && p.ExitCode == 0) return;
        }
        catch (Win32Exception) { }
        Open(left);
        Open(right);
    }

    private static void Start(string exe, string args)
    {
        try { Process.Start(new ProcessStartInfo(exe, args) { UseShellExecute = false })?.Dispose(); }
        catch (Win32Exception) { }
    }
}
