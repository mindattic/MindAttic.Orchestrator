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
        // A script's default action runs it (.py, .js, .cmd, a hook): the audit shows such files, so they open in an editor.
        if (Runs(path)) { Edit(path); return; }
        try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true })?.Dispose(); }
        catch (Win32Exception) { Reveal(path); }
    }

    private static readonly HashSet<string> RunnableExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".py", ".pyw", ".js", ".jse", ".vbs", ".vbe", ".wsf", ".wsh", ".ps1", ".psm1", ".cmd", ".bat", ".exe", ".com", ".scr",
        ".msi", ".hta", ".reg", ".lnk", ".sh", ".jar", ".cpl", ".pif", ".url",
    };

    /// <summary>Whether opening <paramref name="path"/> with its default action would run it rather than show it.</summary>
    internal static bool Runs(string path) => RunnableExtensions.Contains(Path.GetExtension(path));

    /// <summary>Shows a file's text: in VS Code when it's installed, otherwise Notepad.</summary>
    private static void Edit(string path)
    {
        try
        {
            var psi = new ProcessStartInfo("cmd.exe") { UseShellExecute = false, CreateNoWindow = true };
            foreach (var a in new[] { "/c", "code", "--", path }) psi.ArgumentList.Add(a);
            using var p = Process.Start(psi)!;
            if (p.WaitForExit(5000) && p.ExitCode == 0) return;
        }
        catch (Win32Exception) { }
        Start("notepad.exe", $"\"{path}\"");
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
