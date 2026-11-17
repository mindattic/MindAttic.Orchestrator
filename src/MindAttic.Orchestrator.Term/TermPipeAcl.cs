using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using Microsoft.Win32.SafeHandles;

namespace MindAttic.Orchestrator.Term;

/// <summary>
/// The named pipe of a TermHost that runs under a member's own Windows account (hub phase 2b, M.ORCH-T1667). An ordinary host's
/// pipe is <see cref="PipeOptions.CurrentUserOnly"/>, which a host running as the member would close to the daemon and gateway
/// (the host's own user). This one's DACL is explicit and protected (nothing inherited): the account the host runs under
/// (full control, so it can make more instances), the one peer it was started for (the host's user: read and write, no new
/// instances, no change of permissions), and a deny for any network logon, so no other local user and no remote caller opens it.
/// <para>The peer's side checks the other way round: <see cref="ServerProcessId"/> names the process serving the pipe it opened,
/// which must be the TermHost it started, so a pipe of the same name squatted by anyone else is refused.</para>
/// Nothing here touches any pipe but the one a caller creates or opened.
/// </summary>
public static class TermPipeAcl
{
    /// <summary>What the peer is given: read and write the pipe, nothing more.</summary>
    public const PipeAccessRights PeerRights = PipeAccessRights.ReadWrite | PipeAccessRights.Synchronize;

    /// <summary>The SID of the user this process runs as.</summary>
    public static SecurityIdentifier CurrentUser
    {
        get
        {
            using var identity = WindowsIdentity.GetCurrent();
            return identity.User ?? throw new InvalidOperationException("This process's user has no SID.");
        }
    }

    /// <summary>The SID <paramref name="text"/> spells (<c>S-1-5-21-...</c>), or null when it isn't one, or is a well-known
    /// group (Everyone, Users, Authenticated Users...) rather than an account.</summary>
    public static SecurityIdentifier? ParseSid(string? text)
    {
        if (string.IsNullOrWhiteSpace(text) || !text.Trim().StartsWith("S-", StringComparison.OrdinalIgnoreCase)) return null;
        try
        {
            var sid = new SecurityIdentifier(text.Trim());
            return sid.IsAccountSid() ? sid : null;
        }
        catch (ArgumentException) { return null; }
    }

    /// <summary>The DACL for a pipe served by <paramref name="self"/> for <paramref name="peer"/> (see the class summary).</summary>
    public static PipeSecurity For(SecurityIdentifier self, SecurityIdentifier peer)
    {
        var security = new PipeSecurity();
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        security.AddAccessRule(new PipeAccessRule(self, PipeAccessRights.FullControl, AccessControlType.Allow));
        if (peer != self) security.AddAccessRule(new PipeAccessRule(peer, PeerRights, AccessControlType.Allow));
        security.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(WellKnownSidType.NetworkSid, null), PipeAccessRights.FullControl, AccessControlType.Deny));
        return security;
    }

    /// <summary>A server instance of <paramref name="pipeName"/> with <see cref="For"/>'s DACL for <paramref name="peer"/>, as the
    /// host's accept loop makes them (asynchronous, byte mode, any number of instances; the first with
    /// <see cref="PipeOptions.FirstPipeInstance"/>, so a name someone else already serves fails instead of being shared).</summary>
    public static NamedPipeServerStream CreateServer(string pipeName, SecurityIdentifier peer, bool firstInstance) =>
        NamedPipeServerStreamAcl.Create(pipeName, PipeDirection.InOut, NamedPipeServerStream.MaxAllowedServerInstances, PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous | (firstInstance ? PipeOptions.FirstPipeInstance : PipeOptions.None), 0, 0, For(CurrentUser, peer));

    /// <summary>The pid of the process serving the pipe <paramref name="pipe"/> is connected to; null when Windows won't say.</summary>
    public static int? ServerProcessId(PipeStream pipe)
    {
        try { return GetNamedPipeServerProcessId(pipe.SafePipeHandle, out var pid) ? (int)pid : null; }
        catch (Exception ex) when (ex is ObjectDisposedException or InvalidOperationException) { return null; }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetNamedPipeServerProcessId(SafePipeHandle pipe, out uint serverProcessId);
}
