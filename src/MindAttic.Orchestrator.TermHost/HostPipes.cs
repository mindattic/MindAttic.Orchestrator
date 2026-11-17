using System.ComponentModel;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.Principal;
using Microsoft.Win32.SafeHandles;

namespace MindAttic.Orchestrator.TermHost;

/// <summary>Makes the server end of a session's pipe (M.ORCH-T1656). The seam <see cref="TermSession"/> creates every pipe
/// instance through, so tests can see which kind it picked without a real elevated host.</summary>
internal interface IHostPipeFactory
{
    /// <summary>A new, unconnected, asynchronous server instance of <paramref name="pipeName"/>. With
    /// <paramref name="firstInstance"/> it fails (an <see cref="IOException"/> or <see cref="UnauthorizedAccessException"/>)
    /// when the name already exists, which is how a second host with a taken id finds out.</summary>
    NamedPipeServerStream Create(string pipeName, bool firstInstance);
}

/// <summary>Picks the pipe factory for this host (M.ORCH-T1656).</summary>
internal static class HostPipes
{
    /// <summary>An unelevated host (almost every one) keeps today's pipe exactly (<see cref="CurrentUserPipeFactory"/>); an
    /// elevated one gets <see cref="ElevatedPipeFactory"/>, so the dashboard's own unelevated viewer can still attach.</summary>
    public static IHostPipeFactory For(bool elevated) => elevated ? new ElevatedPipeFactory() : new CurrentUserPipeFactory();

    /// <summary>Whether this process runs elevated: its token has the Administrators group enabled, which a UAC-filtered
    /// (unelevated) admin token never has. False when it can't be told.</summary>
    public static bool IsProcessElevated()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException) { return false; }
    }
}

/// <summary>Today's pipe, unchanged: .NET's <see cref="PipeOptions.CurrentUserOnly"/>, which gives the process token's owner
/// SID read/write and makes it the owner.</summary>
internal sealed class CurrentUserPipeFactory : IHostPipeFactory
{
    public NamedPipeServerStream Create(string pipeName, bool firstInstance) =>
        new(pipeName, PipeDirection.InOut, NamedPipeServerStream.MaxAllowedServerInstances, PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly | (firstInstance ? PipeOptions.FirstPipeInstance : 0));
}

/// <summary>
/// An elevated host's pipe (M.ORCH-T1656), created whole with <see cref="ElevatedPipeSecurity.Sddl"/> as its security
/// descriptor, so nothing is relabeled after the fact (that would need WRITE_OWNER on the handle, which .NET's server handle
/// doesn't carry). Why the .NET <see cref="PipeOptions.CurrentUserOnly"/> pipe can't serve here: in an elevated process the
/// token's <em>owner</em> SID is BUILTIN\Administrators, not the user, so that pipe is owned by and grants only Administrators.
/// The dashboard's unelevated viewer has Administrators only as a deny-only group, so the DACL refuses it, and its own
/// <c>CurrentUserOnly</c> client check (pipe owner == its token owner, the user) refuses the pipe too; and the pipe's High
/// mandatory label blocks a Medium caller's write regardless. See <see cref="ElevatedPipeSecurity"/> for what this grants.
/// </summary>
internal sealed class ElevatedPipeFactory : IHostPipeFactory
{
    private readonly Func<SecurityIdentifier> user;

    /// <param name="user">The SID the pipe is restricted to; this process's user by default.</param>
    public ElevatedPipeFactory(Func<SecurityIdentifier>? user = null) => this.user = user ?? ElevatedPipeSecurity.CurrentUser;

    public NamedPipeServerStream Create(string pipeName, bool firstInstance)
    {
        var handle = ElevatedPipeSecurity.CreateServerHandle(pipeName, firstInstance, ElevatedPipeSecurity.Sddl(user()));
        try { return new NamedPipeServerStream(PipeDirection.InOut, isAsync: true, isConnected: false, handle); }
        catch
        {
            handle.Dispose();
            throw;
        }
    }
}

/// <summary>
/// The security descriptor of an elevated host's pipe (M.ORCH-T1656), and the trade-off it makes.
/// <list type="bullet">
/// <item>Owner and group: the user's own SID, which is what the unelevated viewer's <c>CurrentUserOnly</c> check expects.</item>
/// <item>DACL, protected (nothing inherited): one ACE, the user's SID, full access. No Administrators, SYSTEM, Everyone or
/// logon-session ACE: the elevated host itself runs as that same user SID, so it needs nothing else, and no other account
/// (another admin included) can open the pipe. Remote clients are rejected too (<c>PIPE_REJECT_REMOTE_CLIENTS</c>).</item>
/// <item>Mandatory label: <b>Medium</b>, no-write-up (<c>S:(ML;;NW;;;ME)</c>). The ticket says "low-integrity label", but
/// the viewer it has to let in is Medium, and Medium is enough: no-write-up only blocks callers below the label. Low would also
/// let a Low-integrity process of this user (a sandboxed browser or document renderer) type into an Administrator shell, which
/// Medium refuses.</item>
/// </list>
/// The trade-off: while an elevated tab is open, any unelevated process running as this user can attach to its pipe and type
/// into an Administrator shell, which is the very thing the no-write-up policy normally stops. That is the feature (the
/// dashboard is such a process), and it is bounded to the one user, the one session, and its lifetime; an unelevated host (every
/// other tab) keeps today's pipe. UAC was never a security boundary between a user's own Medium and High processes, but this
/// does remove the prompt's friction for whatever already runs as the user, so an elevated tab is only opened on an explicit ask.
/// </summary>
internal static class ElevatedPipeSecurity
{
    /// <summary>The SDDL for a pipe restricted to <paramref name="user"/> with a Medium no-write-up label.</summary>
    public static string Sddl(SecurityIdentifier user)
    {
        var sid = user.Value;
        return $"O:{sid}G:{sid}D:P(A;;GA;;;{sid})S:(ML;;NW;;;ME)";
    }

    /// <summary>This process's user SID (the token user, never the token owner, which is Administrators when elevated).</summary>
    public static SecurityIdentifier CurrentUser()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return identity.User ?? throw new InvalidOperationException("This process's token has no user SID.");
    }

    private const uint PIPE_ACCESS_DUPLEX = 0x00000003, FILE_FLAG_OVERLAPPED = 0x40000000, FILE_FLAG_FIRST_PIPE_INSTANCE = 0x00080000;
    private const uint PIPE_TYPE_BYTE = 0, PIPE_READMODE_BYTE = 0, PIPE_WAIT = 0, PIPE_REJECT_REMOTE_CLIENTS = 0x00000008;
    private const uint PIPE_UNLIMITED_INSTANCES = 255;
    private const uint SDDL_REVISION_1 = 1;
    private const int SE_KERNEL_OBJECT = 6;
    private const uint OWNER_SECURITY_INFORMATION = 0x1, GROUP_SECURITY_INFORMATION = 0x2, DACL_SECURITY_INFORMATION = 0x4,
        LABEL_SECURITY_INFORMATION = 0x10;

    [StructLayout(LayoutKind.Sequential)]
    private struct SECURITY_ATTRIBUTES
    {
        public int nLength;
        public IntPtr lpSecurityDescriptor;
        public int bInheritHandle;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafePipeHandle CreateNamedPipeW(string lpName, uint dwOpenMode, uint dwPipeMode, uint nMaxInstances,
        uint nOutBufferSize, uint nInBufferSize, uint nDefaultTimeOut, ref SECURITY_ATTRIBUTES lpSecurityAttributes);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool ConvertStringSecurityDescriptorToSecurityDescriptorW(string StringSecurityDescriptor,
        uint StringSDRevision, out IntPtr SecurityDescriptor, IntPtr SecurityDescriptorSize);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool ConvertSecurityDescriptorToStringSecurityDescriptorW(IntPtr SecurityDescriptor, uint RequestedStringSDRevision,
        uint SecurityInformation, out IntPtr StringSecurityDescriptor, IntPtr StringSecurityDescriptorLen);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern uint GetSecurityInfo(SafeHandle handle, int ObjectType, uint SecurityInfo, IntPtr ppsidOwner, IntPtr ppsidGroup,
        IntPtr ppDacl, IntPtr ppSacl, out IntPtr ppSecurityDescriptor);

    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr hMem);

    /// <summary>A server handle for <c>\\.\pipe\<paramref name="pipeName"/></c>, overlapped, byte mode, local clients only,
    /// created with <paramref name="sddl"/>. Throws <see cref="IOException"/> (or <see cref="UnauthorizedAccessException"/>
    /// when the name is taken under <paramref name="firstInstance"/>) on failure.</summary>
    public static SafePipeHandle CreateServerHandle(string pipeName, bool firstInstance, string sddl)
    {
        if (!ConvertStringSecurityDescriptorToSecurityDescriptorW(sddl, SDDL_REVISION_1, out var sd, IntPtr.Zero))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Building the pipe's security descriptor failed");
        try
        {
            var sa = new SECURITY_ATTRIBUTES { nLength = Marshal.SizeOf<SECURITY_ATTRIBUTES>(), lpSecurityDescriptor = sd, bInheritHandle = 0 };
            var handle = CreateNamedPipeW(@"\\.\pipe\" + pipeName,
                PIPE_ACCESS_DUPLEX | FILE_FLAG_OVERLAPPED | (firstInstance ? FILE_FLAG_FIRST_PIPE_INSTANCE : 0),
                PIPE_TYPE_BYTE | PIPE_READMODE_BYTE | PIPE_WAIT | PIPE_REJECT_REMOTE_CLIENTS, PIPE_UNLIMITED_INSTANCES, 0, 0, 0, ref sa);
            if (handle.IsInvalid)
            {
                var error = Marshal.GetLastWin32Error();
                handle.Dispose();
                var why = new Win32Exception(error).Message;
                if (error == Native.ERROR_ACCESS_DENIED) throw new UnauthorizedAccessException($"Creating pipe {pipeName} was refused: {why}");
                throw new IOException($"Creating pipe {pipeName} failed: {why}", error);
            }
            return handle;
        }
        finally { LocalFree(sd); }
    }

    /// <summary>The owner, group, DACL and mandatory label of <paramref name="handle"/> as SDDL. For tests and diagnostics.</summary>
    public static string ReadSddl(SafeHandle handle)
    {
        const uint what = OWNER_SECURITY_INFORMATION | GROUP_SECURITY_INFORMATION | DACL_SECURITY_INFORMATION | LABEL_SECURITY_INFORMATION;
        var rc = GetSecurityInfo(handle, SE_KERNEL_OBJECT, what, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, out var sd);
        if (rc != 0) throw new Win32Exception((int)rc, "Reading the pipe's security failed");
        try
        {
            if (!ConvertSecurityDescriptorToStringSecurityDescriptorW(sd, SDDL_REVISION_1, what, out var text, IntPtr.Zero))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Formatting the pipe's security failed");
            try { return Marshal.PtrToStringUni(text) ?? ""; }
            finally { LocalFree(text); }
        }
        finally { LocalFree(sd); }
    }
}
