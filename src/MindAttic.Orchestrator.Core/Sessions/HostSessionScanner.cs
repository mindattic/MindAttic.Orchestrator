using System.Diagnostics;
using System.Management;
using System.Runtime.InteropServices;

namespace MindAttic.Orchestrator.Sessions;

/// <summary>
/// Finds every Launcher-hosted agent tab. Each <c>MindAttic.Launcher.exe host</c> process listens on
/// <c>mindattic-host-{provider}-{pid}</c> (MindAttic.Launcher's HostInputPipeServer), so the pipe
/// namespace is a live registry; the process command line says which directory the tab is rooted at.
/// </summary>
public sealed partial class HostSessionScanner(OrchestratorOptions options)
{
    /// <summary>What every Launcher host's input pipe name starts with (<c>mindattic-host-{provider}-{pid}</c>).</summary>
    public const string PipeNamePrefix = "mindattic-host-";

    /// <summary>Every Launcher-hosted tab whose input pipe is open now, with the directory it's rooted at when its command
    /// line can be read.</summary>
    public IReadOnlyList<HostSession> Scan()
    {
        var pipes = EnumeratePipes()
            .Select(ParsePipeName)
            .OfType<(string Provider, int Pid, string Name)>()
            .ToList();
        if (pipes.Count == 0) return [];

        var commandLines = LauncherCommandLines();
        var roster = LoadRoster();
        return [.. pipes.Select(p =>
        {
            commandLines.TryGetValue(p.Pid, out var cmd);
            return new HostSession(p.Provider, p.Pid, p.Name, cmd is null ? null : WorkingDirectoryOf(cmd, roster))
            {
                Title = cmd is null ? null : TitleOf(cmd),
            };
        })];
    }

    internal static (string Provider, int Pid, string Name)? ParsePipeName(string pathOrName)
    {
        var slash = pathOrName.LastIndexOf('\\');
        var name = slash >= 0 ? pathOrName[(slash + 1)..] : pathOrName;
        if (!name.StartsWith(PipeNamePrefix, StringComparison.OrdinalIgnoreCase)) return null;

        var rest = name[PipeNamePrefix.Length..];
        var dash = rest.LastIndexOf('-');
        if (dash <= 0 || !int.TryParse(rest[(dash + 1)..], out var pid)) return null;
        // The pipe lowercases the provider key; restore the Launcher's spelling when it's a known one.
        var provider = rest[..dash];
        provider = SessionControl.Providers.FirstOrDefault(p => p.Equals(provider, StringComparison.OrdinalIgnoreCase)) ?? provider;
        return (provider, pid, name);
    }

    /// <summary>
    /// The directory a <c>host</c> invocation is rooted at: <c>--path</c> wins, otherwise
    /// <c>--name</c> is looked up the way the Launcher does (its roster, falling back to a workspace
    /// folder of that name).
    /// </summary>
    internal string? WorkingDirectoryOf(string commandLine, IReadOnlyDictionary<string, string> roster)
    {
        var args = SplitCommandLine(commandLine);
        string? path = null, name = null;
        for (var i = 0; i < args.Count - 1; i++)
        {
            if (args[i].Equals("--path", StringComparison.OrdinalIgnoreCase)) path = args[i + 1];
            else if (args[i].Equals("--name", StringComparison.OrdinalIgnoreCase)) name = args[i + 1];
        }
        if (!string.IsNullOrWhiteSpace(path)) return path;
        if (string.IsNullOrWhiteSpace(name)) return null;
        return roster.TryGetValue(name, out var p) ? p : Path.Combine(options.WorkspaceRoot, name);
    }

    /// <summary>The <c>--title</c> a <c>host</c> invocation was given: what its Windows Terminal tab shows (M.ORCH-T619).</summary>
    internal static string? TitleOf(string commandLine)
    {
        var args = SplitCommandLine(commandLine);
        for (var i = 0; i < args.Count - 1; i++)
            if (args[i].Equals("--title", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(args[i + 1])) return args[i + 1];
        return null;
    }

    private static IEnumerable<string> EnumeratePipes()
    {
        try { return Directory.GetFiles(@"\\.\pipe\"); }
        catch (IOException) { return []; }
        catch (UnauthorizedAccessException) { return []; }
    }

    private const string LauncherProcessName = "MindAttic.Launcher";

    // A process's command line never changes, so WMI is asked once per (pid, start time) rather than every refresh (M.ORCH-T2213).
    private static readonly Dictionary<int, (DateTime Started, string CommandLine)> CommandLineCache = [];
    private static readonly Lock CommandLineCacheLock = new();

    /// <summary>Every MindAttic.Launcher.exe running now, its command line by pid: its host tabs and its menus alike.</summary>
    public static IReadOnlyDictionary<int, string> LauncherCommandLines()
    {
        var running = new Dictionary<int, DateTime>();
        foreach (var p in Process.GetProcessesByName(LauncherProcessName))
        {
            using (p)
            {
                try { running[p.Id] = p.StartTime; }
                catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception) { running[p.Id] = DateTime.MinValue; }
            }
        }
        lock (CommandLineCacheLock) return ResolveCommandLines(running, CommandLineCache, QueryCommandLine);
    }

    /// <summary>The command line of each running pid, from <paramref name="cache"/> when its start time still matches (so a reused
    /// pid is asked again), otherwise from <paramref name="query"/>; pids no longer running leave the cache, and a failed query is
    /// asked again next time.</summary>
    internal static Dictionary<int, string> ResolveCommandLines(
        IReadOnlyDictionary<int, DateTime> running, Dictionary<int, (DateTime Started, string CommandLine)> cache, Func<int, string?> query)
    {
        foreach (var gone in cache.Keys.Where(pid => !running.ContainsKey(pid)).ToList()) cache.Remove(gone);

        var result = new Dictionary<int, string>();
        foreach (var (pid, started) in running)
        {
            if (cache.TryGetValue(pid, out var entry) && entry.Started == started) result[pid] = entry.CommandLine;
            else if (query(pid) is { } cmd) { cache[pid] = (started, cmd); result[pid] = cmd; }
            else cache.Remove(pid);
        }
        return result;
    }

    private static string? QueryCommandLine(int pid)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher($"SELECT CommandLine FROM Win32_Process WHERE ProcessId = {pid}");
            using var found = searcher.Get();
            foreach (var o in found)
                using (o)
                    if (o["CommandLine"] is string cmd) return cmd;
        }
        catch (ManagementException ex) { OrchestratorLog.For("HostSessionScanner").Warning(ex, "The WMI Launcher command-line query failed; contained"); }
        catch (COMException ex) { OrchestratorLog.For("HostSessionScanner").Warning(ex, "The WMI Launcher command-line query failed; contained"); }
        catch (Exception ex) when (ex is UnauthorizedAccessException or InvalidOperationException or System.ComponentModel.Win32Exception) { }
        return null;
    }

    /// <summary>The Launcher's project roster (name → path), when its settings file can be read.</summary>
    private static Dictionary<string, string> LoadRoster()
    {
        var roster = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var file = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MindAttic", "MindAttic.Launcher", "settings.json");
        if (!File.Exists(file)) return roster;
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(file));
            if (Property(doc.RootElement, "projects") is { ValueKind: System.Text.Json.JsonValueKind.Array } projects)
            {
                foreach (var p in projects.EnumerateArray())
                {
                    if (Property(p, "name")?.GetString() is { } name && Property(p, "path")?.GetString() is { } path)
                        roster[name] = path;
                }
            }
        }
        catch (Exception ex) when (ex is IOException or System.Text.Json.JsonException or InvalidOperationException) { }
        return roster;
    }

    private static System.Text.Json.JsonElement? Property(System.Text.Json.JsonElement element, string name)
    {
        if (element.ValueKind != System.Text.Json.JsonValueKind.Object) return null;
        foreach (var p in element.EnumerateObject())
            if (p.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) return p.Value;
        return null;
    }

    internal static IReadOnlyList<string> SplitCommandLine(string commandLine)
    {
        var argv = CommandLineToArgvW(commandLine, out var argc);
        if (argv == IntPtr.Zero) return [];
        try
        {
            var result = new string[argc];
            for (var i = 0; i < argc; i++)
                result[i] = Marshal.PtrToStringUni(Marshal.ReadIntPtr(argv, i * IntPtr.Size)) ?? "";
            return result;
        }
        finally { LocalFree(argv); }
    }

    [LibraryImport("shell32.dll", EntryPoint = "CommandLineToArgvW", StringMarshalling = StringMarshalling.Utf16)]
    private static partial IntPtr CommandLineToArgvW(string lpCmdLine, out int pNumArgs);

    [LibraryImport("kernel32.dll")]
    private static partial IntPtr LocalFree(IntPtr hMem);
}
