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
    public const string PipeNamePrefix = "mindattic-host-";

    public IReadOnlyList<HostSession> Scan()
    {
        var pipes = EnumeratePipes()
            .Select(ParsePipeName)
            .OfType<(string Provider, int Pid, string Name)>()
            .ToList();
        if (pipes.Count == 0) return [];

        var commandLines = QueryLauncherCommandLines();
        var roster = LoadRoster();
        return [.. pipes.Select(p =>
        {
            commandLines.TryGetValue(p.Pid, out var cmd);
            return new HostSession(p.Provider, p.Pid, p.Name, cmd is null ? null : WorkingDirectoryOf(cmd, roster));
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

    private static IEnumerable<string> EnumeratePipes()
    {
        try { return Directory.GetFiles(@"\\.\pipe\"); }
        catch (IOException) { return []; }
        catch (UnauthorizedAccessException) { return []; }
    }

    private static Dictionary<int, string> QueryLauncherCommandLines()
    {
        var result = new Dictionary<int, string>();
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT ProcessId, CommandLine FROM Win32_Process WHERE Name = 'MindAttic.Launcher.exe'");
            foreach (var o in searcher.Get())
            {
                using (o)
                {
                    if (o["CommandLine"] is string cmd && o["ProcessId"] is uint pid)
                        result[(int)pid] = cmd;
                }
            }
        }
        catch (ManagementException) { }
        catch (COMException) { }
        return result;
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
