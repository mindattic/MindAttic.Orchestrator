using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;

namespace MindAttic.Orchestrator.TermHost;

/// <summary>
/// What a TermHost started under a member's own Windows account does for its peer (hub phase 2b, M.ORCH-T1667; the host's user,
/// <see cref="HostOptions.PeerSid"/>), beyond the pipe ACL (<see cref="Term.TermPipeAcl"/>): it adds one ACE to its <b>own</b>
/// process's DACL so the peer can tell it still runs (its start time, which the registry and the gateway's ledger check) and end it
/// (<c>machine stop</c>). Nothing more: no read or write of its memory, no handle duplication, no thread access. It touches only
/// this process's own security descriptor, which its owner may always change.
/// </summary>
internal static class MemberHost
{
    /// <summary>PROCESS_TERMINATE | PROCESS_QUERY_LIMITED_INFORMATION | SYNCHRONIZE: what the peer gets on this process.</summary>
    public const int PeerProcessRights = 0x0001 | 0x1000 | 0x00100000;

    private const uint DACL_SECURITY_INFORMATION = 0x4;

    /// <summary>Gives <paramref name="peer"/> <see cref="PeerProcessRights"/> on this process; false (and why) when Windows refuses.</summary>
    public static bool GrantPeer(SecurityIdentifier peer, out string? why)
    {
        why = null;
        try
        {
            var self = GetCurrentProcess();
            var descriptor = new RawSecurityDescriptor(Read(self), 0);
            descriptor.DiscretionaryAcl = WithPeer(descriptor.DiscretionaryAcl, peer);
            var bytes = new byte[descriptor.BinaryLength];
            descriptor.GetBinaryForm(bytes, 0);
            if (!SetKernelObjectSecurity(self, DACL_SECURITY_INFORMATION, bytes))
            {
                why = new Win32Exception(Marshal.GetLastWin32Error()).Message;
                return false;
            }
            return true;
        }
        catch (Exception ex) when (ex is Win32Exception or ArgumentException or InvalidOperationException)
        {
            why = ex.Message;
            return false;
        }
    }

    /// <summary><paramref name="dacl"/> with an allow ACE for <paramref name="peer"/> (<see cref="PeerProcessRights"/>) at its end
    /// (allow ACEs need no particular order among themselves). A null DACL, which already allows everyone, is left alone.</summary>
    internal static RawAcl? WithPeer(RawAcl? dacl, SecurityIdentifier peer)
    {
        if (dacl is null) return null;
        var copy = new byte[dacl.BinaryLength];
        dacl.GetBinaryForm(copy, 0);
        var result = new RawAcl(copy, 0);
        result.InsertAce(result.Count, new CommonAce(AceFlags.None, AceQualifier.AccessAllowed, PeerProcessRights, peer, isCallback: false, opaque: null));
        return result;
    }

    private static byte[] Read(IntPtr handle)
    {
        GetKernelObjectSecurity(handle, DACL_SECURITY_INFORMATION, null, 0, out var needed);
        if (needed == 0) throw new Win32Exception(Marshal.GetLastWin32Error());
        var buffer = new byte[needed];
        if (!GetKernelObjectSecurity(handle, DACL_SECURITY_INFORMATION, buffer, needed, out _)) throw new Win32Exception(Marshal.GetLastWin32Error());
        return buffer;
    }

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentProcess();

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetKernelObjectSecurity(IntPtr handle, uint requested, byte[]? descriptor, uint length, out uint needed);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetKernelObjectSecurity(IntPtr handle, uint information, byte[] descriptor);
}
