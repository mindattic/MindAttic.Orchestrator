using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using MindAttic.Orchestrator.Sessions;
using MindAttic.Orchestrator.Term;
using Spectre.Console;
using Spectre.Console.Cli;

/// <summary><c>orchestrator memory</c> (M.ORCH-T1733): what each hosted CLI's whole process tree holds, beside the dashboard, the
/// daemon and the other MindAttic processes, so a memory change is judged by numbers. One process snapshot; reads only.</summary>
internal sealed class MemoryCommand : Command<MemoryCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("--json")]
        [Description("Write the groups as JSON.")]
        public bool Json { get; init; }
    }

    internal sealed record Proc(int Pid, string Name, long WorkingSet, long Private, DateTime? Start);

    internal sealed record Group(string Kind, string Name, string Login, int Processes, long WorkingSetMb, long PrivateMb, IReadOnlyDictionary<string, long> PrivateMbByExe);

    /// <summary>Processes that are a group of their own even when a CLI's hook started them.</summary>
    private static readonly Dictionary<string, string> Standalone = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Hub"] = "Prose Hub",
        ["idiotproof-hub"] = "IdiotProof Hub",
    };

    public override int Execute(CommandContext context, Settings settings)
    {
        var procs = Read();
        var children = Children(procs, ProcessTree.ParentMap());
        var claimed = new HashSet<int>();
        var groups = new List<Group>();

        foreach (var s in TermSessions.Enumerate().OrderBy(s => s.Login ?? "", StringComparer.OrdinalIgnoreCase).ThenBy(s => s.Repo ?? s.Title ?? s.Id, StringComparer.OrdinalIgnoreCase))
            if (procs.ContainsKey(s.HostPid))
                groups.Add(Sum(s.IsMenu ? "Launcher menu" : "CLI", s.Repo ?? s.Title ?? s.Id, s.Login ?? "global",
                    Tree(s.HostPid, children, procs, claimed, p => Standalone.ContainsKey(p.Name))));

        // Every other MindAttic process, a Claude CLI in its own terminal and the hubs: each its own group, never reaching into another's.
        var roots = procs.Values.Where(p => !claimed.Contains(p.Pid) && Kind(p) is not null).OrderBy(p => p.Start ?? DateTime.MaxValue).ToList();
        var rootPids = roots.Select(p => p.Pid).ToHashSet();
        foreach (var root in roots)
        {
            if (claimed.Contains(root.Pid)) continue;
            groups.Add(Sum(Kind(root)!, root.Name, "", Tree(root.Pid, children, procs, claimed, p => rootPids.Contains(p.Pid))));
        }

        if (settings.Json)
        {
            Console.WriteLine(JsonSerializer.Serialize(groups, new JsonSerializerOptions { WriteIndented = true }));
            return 0;
        }
        var table = new Table().Border(TableBorder.Rounded).AddColumns("What", "Name", "Login", "Procs", "Private MB", "Working set MB", "Private MB by exe");
        foreach (var g in groups)
            table.AddRow(Markup.Escape(g.Kind), Markup.Escape(g.Name), Markup.Escape(g.Login), g.Processes.ToString(), g.PrivateMb.ToString("N0"), g.WorkingSetMb.ToString("N0"),
                Markup.Escape(ByExe(g.PrivateMbByExe)));
        AnsiConsole.Write(table);
        foreach (var kind in groups.GroupBy(g => g.Kind).OrderByDescending(k => k.Sum(g => g.PrivateMb)))
            AnsiConsole.MarkupLine($"{Markup.Escape(kind.Key)}: {kind.Count()}, {kind.Sum(g => g.PrivateMb):N0} MB private in all");
        var total = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / Mb;
        AnsiConsole.MarkupLine($"All of the above: [aqua]{groups.Sum(g => g.PrivateMb):N0} MB[/] private of {total:N0} MB of RAM. Private is what a process alone holds; working sets share pages, so their sum overstates.");
        return 0;
    }

    private const long Mb = 1024 * 1024;

    /// <summary>The five biggest exes of a group, names without the MindAttic prefixes; --json has them all.</summary>
    internal static string ByExe(IReadOnlyDictionary<string, long> byExe)
    {
        var ordered = byExe.OrderByDescending(e => e.Value).ToList();
        var shown = string.Join(", ", ordered.Take(5).Select(e => $"{e.Key.Replace("MindAttic.Orchestrator.", "").Replace("MindAttic.", "")} {e.Value:N0}"));
        return ordered.Count > 5 ? $"{shown}, +{ordered.Count - 5} more" : shown;
    }

    private static string? Kind(Proc p) => p.Name switch
    {
        _ when Standalone.TryGetValue(p.Name, out var hub) => hub,
        "MindAttic.Orchestrator.Desktop" => HasWindow(p.Pid) ? "Dashboard" : "Daemon",
        "MindAttic.Orchestrator.TermHost" => "TermHost (not in the registry)",
        "claude" => "Claude CLI (own terminal)",
        _ when p.Name.StartsWith("MindAttic.", StringComparison.OrdinalIgnoreCase) => "App",
        _ => null,
    };

    private static bool HasWindow(int pid)
    {
        try
        {
            using var p = Process.GetProcessById(pid);
            return p.MainWindowHandle != IntPtr.Zero;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or Win32Exception) { return false; }
    }

    private static Dictionary<int, Proc> Read()
    {
        var procs = new Dictionary<int, Proc>();
        foreach (var p in Process.GetProcesses())
            using (p)
            {
                try
                {
                    DateTime? start = null;
                    try { start = p.StartTime.ToUniversalTime(); }
                    catch (Exception ex) when (ex is Win32Exception or InvalidOperationException) { }
                    procs[p.Id] = new Proc(p.Id, p.ProcessName, p.WorkingSet64, p.PrivateMemorySize64, start);
                }
                catch (Exception ex) when (ex is Win32Exception or InvalidOperationException) { }
            }
        return procs;
    }

    /// <summary>Each process's children; a link whose parent started after its child is a reused pid, not a parent.</summary>
    internal static Dictionary<int, List<int>> Children(IReadOnlyDictionary<int, Proc> procs, IReadOnlyDictionary<int, int> parents)
    {
        var children = new Dictionary<int, List<int>>();
        foreach (var (pid, parent) in parents)
        {
            if (parent == pid || !procs.TryGetValue(pid, out var child) || !procs.TryGetValue(parent, out var up)) continue;
            if (child.Start is { } c && up.Start is { } u && u > c) continue;
            if (!children.TryGetValue(parent, out var list)) children[parent] = list = [];
            list.Add(pid);
        }
        return children;
    }

    /// <summary><paramref name="root"/> and everything under it not yet claimed, stopping at a process that is another group's root.</summary>
    internal static List<Proc> Tree(int root, IReadOnlyDictionary<int, List<int>> children, IReadOnlyDictionary<int, Proc> procs, HashSet<int> claimed, Func<Proc, bool> othersRoot)
    {
        var tree = new List<Proc>();
        var todo = new Stack<int>();
        todo.Push(root);
        while (todo.Count > 0)
        {
            var pid = todo.Pop();
            if (!procs.TryGetValue(pid, out var p) || !claimed.Add(pid)) continue;
            tree.Add(p);
            foreach (var child in children.GetValueOrDefault(pid) ?? [])
                if (procs.TryGetValue(child, out var c) && !othersRoot(c)) todo.Push(child);
        }
        return tree;
    }

    private static Group Sum(string kind, string name, string login, List<Proc> tree) =>
        new(kind, name, login, tree.Count, tree.Sum(p => p.WorkingSet) / Mb, tree.Sum(p => p.Private) / Mb,
            tree.GroupBy(p => p.Name, StringComparer.OrdinalIgnoreCase).ToDictionary(e => e.Key, e => e.Sum(p => p.Private) / Mb));
}
