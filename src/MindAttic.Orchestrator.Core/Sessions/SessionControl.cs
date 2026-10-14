using System.Diagnostics;
using System.IO.Pipes;
using System.Text;

namespace MindAttic.Orchestrator.Sessions;

/// <summary>
/// Starts and talks to agent sessions through the Launcher, the same way its own menus do: a new
/// Windows Terminal tab running <c>MindAttic.Launcher.exe host --path …</c>, and text written to a
/// host's input pipe (a trailing newline submits it).
/// </summary>
public sealed class SessionControl(OrchestratorOptions options)
{
    public static readonly IReadOnlyList<string> Providers = ["Claude", "Codex", "Gemini", "Kimi"];

    /// <summary>Opens a new tab for <paramref name="directory"/>; returns an error, or null on success.</summary>
    public string? Start(string title, string directory, string provider, string? prompt = null)
    {
        if (!File.Exists(options.LauncherExe))
            return $"Launcher not found at {options.LauncherExe} (run MindAttic.Launcher/deploy.ps1).";
        if (!Directory.Exists(directory))
            return $"Folder not found: {directory}";

        var psi = new ProcessStartInfo("wt.exe") { UseShellExecute = false };
        foreach (var a in StartArguments(title, directory, provider, prompt)) psi.ArgumentList.Add(a);
        try
        {
            using var _ = Process.Start(psi);
            return null;
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            return $"Couldn't start Windows Terminal: {ex.Message}";
        }
    }

    internal IEnumerable<string> StartArguments(string title, string directory, string provider, string? prompt)
    {
        var tabTitle = $"{title} [{provider}]";
        string[] head =
        [
            "-w", "0", "new-tab", "--title", tabTitle, "-d", directory, "--",
            options.LauncherExe, "host", "--path", directory, "--title", tabTitle, "--provider", provider,
        ];
        string[] all = string.IsNullOrWhiteSpace(prompt) ? head : [.. head, "--prompt", prompt];
        // wt splits its whole command line into subcommands on ';' unless it's escaped.
        return all.Select(a => a.Replace(";", @"\;"));
    }

    /// <summary>Types <paramref name="text"/> into a hosted session; <paramref name="submit"/> presses Enter after it.</summary>
    public static async Task<bool> SendAsync(string pipeName, string text, bool submit, CancellationToken ct = default)
    {
        var payload = submit ? text.TrimEnd('\r', '\n') + "\n" : text;
        try
        {
            using var client = new NamedPipeClientStream(".", pipeName, PipeDirection.Out, PipeOptions.Asynchronous);
            await client.ConnectAsync(1000, ct).ConfigureAwait(false);
            await client.WriteAsync(Encoding.UTF8.GetBytes(payload), ct).ConfigureAwait(false);
            await client.FlushAsync(ct).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex) when (ex is IOException or TimeoutException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    public static void OpenFolder(string directory)
    {
        if (Directory.Exists(directory))
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{directory}\"") { UseShellExecute = false })?.Dispose();
    }
}
