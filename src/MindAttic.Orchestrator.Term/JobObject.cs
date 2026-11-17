using System.ComponentModel;
using System.Runtime.InteropServices;

namespace MindAttic.Orchestrator.Term;

/// <summary>
/// A Windows Job Object that ends every process in it when its last handle closes (<c>JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE</c>):
/// what guarantees a process tree leaves nothing behind (roadmap Phase 4). A TermHost puts its CLI in one (M.ORCH-T1674) and
/// the daemon its builds and tests (M.ORCH-T1675). Whoever holds this object owns the tree: when the holder disposes it, exits
/// or is killed, Windows ends every member, children and grandchildren included, however they were started.
/// <para>The job is unnamed and its handle is never inherited, so only the holder can close it. A member that asks for it
/// (<c>CREATE_BREAKAWAY_FROM_JOB</c>, as a detached TermHost does) may start a child outside the job
/// (<c>JOB_OBJECT_LIMIT_BREAKAWAY_OK</c>); everything else it starts stays inside. Jobs nest (Windows 8 and later), so a holder
/// that is itself in a job works too.</para>
/// Nothing here throws: a job that can't be made or joined is reported, and the caller carries on without it.
/// </summary>
public sealed class JobObject : IDisposable
{
    /// <summary>The attribute that starts a process inside jobs from its first instruction (<c>PROC_THREAD_ATTRIBUTE_JOB_LIST</c>):
    /// its value points at an array of job handles, which must stay valid until the attribute list is deleted.</summary>
    public static readonly IntPtr ProcThreadAttributeJobList = 0x0002000D;

    private const int JobObjectExtendedLimitInformation = 9;
    private const uint JOB_OBJECT_LIMIT_BREAKAWAY_OK = 0x0800, JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE = 0x2000;

    private IntPtr handle;

    private JobObject(IntPtr handle) => this.handle = handle;

    /// <summary>The job's handle, for <see cref="ProcThreadAttributeJobList"/>; zero once disposed.</summary>
    public IntPtr Handle => handle;

    /// <summary>A new kill-on-close job, or null (and <paramref name="why"/>) when Windows refuses one.</summary>
    public static JobObject? TryCreate(out string? why)
    {
        why = null;
        try
        {
            var job = CreateJobObjectW(IntPtr.Zero, null);
            if (job == IntPtr.Zero)
            {
                why = Describe("CreateJobObject", Marshal.GetLastWin32Error());
                return null;
            }
            if (!SetLimits(job, JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE | JOB_OBJECT_LIMIT_BREAKAWAY_OK, out why))
            {
                CloseHandle(job);
                return null;
            }
            return new JobObject(job);
        }
        catch (Exception ex) when (ex is EntryPointNotFoundException or DllNotFoundException or MarshalDirectiveException)
        {
            why = ex.Message;
            return null;
        }
    }

    /// <summary>Puts a running process (and every process it starts from now on) in the job; false (and <paramref name="why"/>)
    /// when Windows refuses, e.g. because it has already exited. Processes it started before this call stay outside.</summary>
    public bool TryAssign(IntPtr process, out string? why)
    {
        why = null;
        var job = handle;
        if (job == IntPtr.Zero) { why = "the job is closed"; return false; }
        if (AssignProcessToJobObject(job, process)) return true;
        why = Describe("AssignProcessToJobObject", Marshal.GetLastWin32Error());
        return false;
    }

    /// <summary>Ends every process in the job now; the job itself stays usable. False when Windows refuses or it is closed.</summary>
    public bool Terminate(uint exitCode = 1)
    {
        var job = handle;
        return job != IntPtr.Zero && TerminateJobObject(job, exitCode);
    }

    /// <summary>Closes the job without ending its members: kill-on-close is taken off first, so what still runs carries on
    /// unowned. If the limit can't be lifted the handle is closed all the same, and the members end.</summary>
    public void Release()
    {
        var job = handle;
        if (job != IntPtr.Zero) SetLimits(job, JOB_OBJECT_LIMIT_BREAKAWAY_OK, out _);
        Dispose();
    }

    /// <summary>Closes the job: every process still in it ends.</summary>
    public void Dispose()
    {
        var job = Interlocked.Exchange(ref handle, IntPtr.Zero);
        if (job != IntPtr.Zero) CloseHandle(job);
    }

    private static bool SetLimits(IntPtr job, uint flags, out string? why)
    {
        why = null;
        var info = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION();
        info.BasicLimitInformation.LimitFlags = flags;
        if (SetInformationJobObject(job, JobObjectExtendedLimitInformation, ref info, (uint)Marshal.SizeOf<JOBOBJECT_EXTENDED_LIMIT_INFORMATION>())) return true;
        why = Describe("SetInformationJobObject", Marshal.GetLastWin32Error());
        return false;
    }

    private static string Describe(string call, int error) => $"{call}: {new Win32Exception(error).Message} (error {error})";

    [StructLayout(LayoutKind.Sequential)]
    private struct JOBOBJECT_BASIC_LIMIT_INFORMATION
    {
        public long PerProcessUserTimeLimit, PerJobUserTimeLimit;
        public uint LimitFlags;
        public UIntPtr MinimumWorkingSetSize, MaximumWorkingSetSize;
        public uint ActiveProcessLimit;
        public UIntPtr Affinity;
        public uint PriorityClass, SchedulingClass;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IO_COUNTERS
    {
        public ulong ReadOperationCount, WriteOperationCount, OtherOperationCount, ReadTransferCount, WriteTransferCount, OtherTransferCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
    {
        public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
        public IO_COUNTERS IoInfo;
        public UIntPtr ProcessMemoryLimit, JobMemoryLimit, PeakProcessMemoryUsed, PeakJobMemoryUsed;
    }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateJobObjectW(IntPtr lpJobAttributes, string? lpName);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetInformationJobObject(IntPtr hJob, int infoClass, ref JOBOBJECT_EXTENDED_LIMIT_INFORMATION info, uint cbInfo);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AssignProcessToJobObject(IntPtr hJob, IntPtr hProcess);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool TerminateJobObject(IntPtr hJob, uint uExitCode);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr hObject);
}
