using System.Runtime.InteropServices;

namespace MindAttic.Orchestrator.Sessions;

/// <summary>Parent links between running processes, to tell which Launcher tab a Claude CLI runs in.</summary>
public static partial class ProcessTree
{
    /// <summary>Whether <paramref name="ancestor"/> started <paramref name="pid"/>, directly or through processes between.</summary>
    public static bool IsAncestor(int ancestor, int pid) => IsAncestor(ancestor, pid, Parents());

    /// <summary>The same walk over a given child-to-parent map; it gives up after 16 steps, so a pid cycle ends.</summary>
    internal static bool IsAncestor(int ancestor, int pid, IReadOnlyDictionary<int, int> parents)
    {
        for (var i = 0; i < 16 && parents.TryGetValue(pid, out var parent) && parent != 0; i++)
        {
            if (parent == ancestor) return true;
            pid = parent;
        }
        return false;
    }

    private static Dictionary<int, int> Parents()
    {
        var parents = new Dictionary<int, int>();
        var snap = CreateToolhelp32Snapshot(2 /* processes */, 0);
        if (snap == new IntPtr(-1)) return parents;
        try
        {
            var e = new PROCESSENTRY32W { dwSize = (uint)Marshal.SizeOf<PROCESSENTRY32W>() };
            for (var ok = Process32FirstW(snap, ref e); ok; ok = Process32NextW(snap, ref e))
                parents[(int)e.th32ProcessID] = (int)e.th32ParentProcessID;
        }
        finally { CloseHandle(snap); }
        return parents;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct PROCESSENTRY32W
    {
        public uint dwSize, cntUsage, th32ProcessID;
        public IntPtr th32DefaultHeapID;
        public uint th32ModuleID, cntThreads, th32ParentProcessID;
        public int pcPriClassBase;
        public uint dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szExeFile;
    }

    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial IntPtr CreateToolhelp32Snapshot(uint flags, uint processId);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool Process32FirstW(IntPtr snapshot, ref PROCESSENTRY32W entry);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool Process32NextW(IntPtr snapshot, ref PROCESSENTRY32W entry);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CloseHandle(IntPtr handle);
}
