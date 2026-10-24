using System.Diagnostics;
using System.Text.Json;

namespace MindAttic.Orchestrator.Sessions;

/// <summary>
/// A headless Claude working one story in a repo: no window, nothing typed into anyone's tab. It runs
/// the same command the Launcher's Claude tabs do, with <c>-p</c>, under <c>cmd /c</c> so its prompt comes
/// from a file and its output goes straight to a log; it carries on if the orchestrator restarts, and the
/// global hooks pace it like any other CLI.
/// </summary>
public static class BackgroundAgent
{
    public const string FallbackCommand = "claude --dangerously-skip-permissions --model claude-sonnet-5-5";

    public static string LogDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MindAttic", "MindAttic.Orchestrator", "allocate");

    /// <summary>Starts the agent; returns the id of its <c>cmd</c> process (which lives exactly as long as it does), or null.</summary>
    public static int? Start(string directory, string prompt, string logPath)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
            var promptPath = Path.ChangeExtension(logPath, ".prompt.txt");
            File.WriteAllText(promptPath, prompt);
            // cmd's own quoting, not .NET's: /s strips the outer pair and keeps the inner ones as written.
            var psi = new ProcessStartInfo("cmd.exe", $"/d /s /c \"{CommandLine(promptPath, logPath)}\"")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = directory,
            };
            using var p = Process.Start(psi);
            return p?.Id;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            return null;
        }
    }

    internal static string CommandLine(string promptPath, string logPath, string? command = null) =>
        $"{command ?? ClaudeCommand()} -p < \"{promptPath}\" > \"{logPath}\" 2>&1";

    /// <summary>Whether the agent started as <paramref name="pid"/> (at <paramref name="startedUtc"/>, when known) is still
    /// working; a <c>cmd</c> that started at another time only reuses its id.</summary>
    public static bool IsRunning(int pid, DateTime? startedUtc = null)
    {
        if (pid <= 0) return false;
        try
        {
            using var p = Process.GetProcessById(pid);
            return p.ProcessName.Equals("cmd", StringComparison.OrdinalIgnoreCase) && !p.HasExited &&
                (startedUtc is not { } at || SameStart(p.StartTime.ToUniversalTime(), at));
        }
        catch (ArgumentException) { return false; }
        catch (InvalidOperationException) { return false; }
        catch (System.ComponentModel.Win32Exception) { return false; }
    }

    internal static bool SameStart(DateTime a, DateTime b) => (a - b).Duration() < TimeSpan.FromSeconds(1);

    /// <summary>When process <paramref name="pid"/> started; null when it's gone or can't be read.</summary>
    public static DateTime? StartTimeOf(int pid)
    {
        try { using var p = Process.GetProcessById(pid); return p.StartTime.ToUniversalTime(); }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception) { return null; }
    }

    /// <summary>The Launcher's Claude command (its model included), so agents match the tabs you open yourself.</summary>
    internal static string ClaudeCommand(string? launcherSettings = null)
    {
        var path = launcherSettings ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MindAttic", "MindAttic.Launcher", "settings.json");
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            if (doc.RootElement.TryGetProperty("agentProviders", out var providers) && providers.ValueKind == JsonValueKind.Array)
                foreach (var p in providers.EnumerateArray())
                    if (p.TryGetProperty("key", out var k) && k.GetString() == Providers.Claude &&
                        p.TryGetProperty("runCommand", out var c) && c.GetString() is { Length: > 0 } command &&
                        command.TrimStart().StartsWith("claude", StringComparison.OrdinalIgnoreCase))
                        return command.Trim();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException) { }
        return FallbackCommand;
    }
}
