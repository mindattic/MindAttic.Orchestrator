using System.ComponentModel;
using System.Runtime.InteropServices;

namespace MindAttic.Orchestrator.Term;

/// <summary>
/// The passwords of members' own Windows accounts on the host (hub phase 2b, M.ORCH-T1667), which starting a TermHost under one
/// needs. Never plain text on disk, never in a log, an audit record, a snapshot or an exception message: the real store is Windows
/// Credential Manager (<see cref="CredentialManagerMemberCredentials"/>), sealed with the host user's DPAPI; tests use a fake.
/// </summary>
public interface IMemberCredentials
{
    /// <summary>Whether a password is stored for <paramref name="account"/>.</summary>
    bool Has(string account);

    /// <summary>Stores <paramref name="password"/> for <paramref name="account"/>, replacing any before it. Throws
    /// <see cref="InvalidOperationException"/> (whose message never holds the password) when it can't.</summary>
    void Save(string account, ReadOnlySpan<char> password);

    /// <summary>Forgets <paramref name="account"/>'s password; false when there was none.</summary>
    bool Forget(string account);

    /// <summary>Hands <paramref name="account"/>'s password to <paramref name="use"/> as a zero-terminated UTF-16 buffer in native
    /// memory, valid only during the call, then zeroes and frees it. Throws <see cref="KeyNotFoundException"/> when none is stored.</summary>
    T Use<T>(string account, Func<IntPtr, T> use);
}

/// <summary>
/// <see cref="IMemberCredentials"/> in Windows Credential Manager: one generic credential per account, target
/// <c>MindAttic.Orchestrator/hub-member:&lt;account&gt;</c>, persisted for this Windows user on this machine (Credential Manager
/// encrypts it with the user's DPAPI key, so it can't be read by another user or copied to another machine). Every buffer that held
/// a password is zeroed before it's freed.
/// </summary>
public sealed class CredentialManagerMemberCredentials : IMemberCredentials
{
    /// <summary>The one shared instance.</summary>
    public static readonly CredentialManagerMemberCredentials Instance = new();

    /// <summary>The credential's target name prefix.</summary>
    public const string TargetPrefix = "MindAttic.Orchestrator/hub-member:";

    /// <summary>The longest password accepted (Credential Manager's blob limit is 2560 bytes; UTF-16 halves it).</summary>
    public const int MaxPasswordChars = 1024;

    private const int CRED_TYPE_GENERIC = 1;
    private const int CRED_PERSIST_LOCAL_MACHINE = 2;
    private const int ERROR_NOT_FOUND = 1168;

    /// <summary>The target an account's credential is stored under.</summary>
    public static string TargetOf(string account) => TargetPrefix + account.Trim().ToLowerInvariant();

    /// <inheritdoc/>
    public bool Has(string account)
    {
        if (!CredReadW(TargetOf(account), CRED_TYPE_GENERIC, 0, out var credential)) return false;
        Wipe(credential);
        CredFree(credential);
        return true;
    }

    /// <inheritdoc/>
    public void Save(string account, ReadOnlySpan<char> password)
    {
        if (password.IsEmpty) throw new InvalidOperationException("An empty password isn't stored.");
        if (password.Length > MaxPasswordChars) throw new InvalidOperationException($"A password is at most {MaxPasswordChars} characters here.");
        var bytes = password.Length * 2;
        var blob = Marshal.AllocHGlobal(bytes);
        try
        {
            // Char by char, never through a managed copy of the password that would linger until collected.
            for (var i = 0; i < password.Length; i++) Marshal.WriteInt16(blob, i * 2, (short)password[i]);
            var credential = new CREDENTIAL
            {
                Type = CRED_TYPE_GENERIC,
                TargetName = TargetOf(account),
                Comment = "MindAttic.Orchestrator: starts this member's CLIs under their own Windows account (hub phase 2b)",
                CredentialBlobSize = bytes,
                CredentialBlob = blob,
                Persist = CRED_PERSIST_LOCAL_MACHINE,
                UserName = account.Trim(),
            };
            if (!CredWriteW(ref credential, 0))
                throw new InvalidOperationException($"Credential Manager refused it: {new Win32Exception(Marshal.GetLastWin32Error()).Message}");
        }
        finally
        {
            Zero(blob, bytes);
            Marshal.FreeHGlobal(blob);
        }
    }

    /// <inheritdoc/>
    public bool Forget(string account)
    {
        if (CredDeleteW(TargetOf(account), CRED_TYPE_GENERIC, 0)) return true;
        var error = Marshal.GetLastWin32Error();
        return error == ERROR_NOT_FOUND ? false : throw new InvalidOperationException($"Credential Manager refused it: {new Win32Exception(error).Message}");
    }

    /// <inheritdoc/>
    public T Use<T>(string account, Func<IntPtr, T> use)
    {
        if (!CredReadW(TargetOf(account), CRED_TYPE_GENERIC, 0, out var credential))
            throw new KeyNotFoundException($"No password is stored for {account.Trim()}.");
        IntPtr copy = IntPtr.Zero;
        var size = 0;
        try
        {
            var read = Marshal.PtrToStructure<CREDENTIAL>(credential);
            size = read.CredentialBlobSize + 2;
            copy = Marshal.AllocHGlobal(size);
            Zero(copy, size);
            // Native to native, byte by byte: no managed copy of the password.
            for (var i = 0; i < read.CredentialBlobSize; i++) Marshal.WriteByte(copy, i, Marshal.ReadByte(read.CredentialBlob, i));
        }
        finally
        {
            Wipe(credential);
            CredFree(credential);
        }
        try { return use(copy); }
        finally
        {
            Zero(copy, size);
            Marshal.FreeHGlobal(copy);
        }
    }

    /// <summary>Zeroes the blob Credential Manager handed back before it's freed (best effort).</summary>
    private static void Wipe(IntPtr credential)
    {
        try
        {
            var read = Marshal.PtrToStructure<CREDENTIAL>(credential);
            if (read.CredentialBlob != IntPtr.Zero && read.CredentialBlobSize > 0) Zero(read.CredentialBlob, read.CredentialBlobSize);
        }
        catch (Exception ex) when (ex is AccessViolationException or ArgumentException) { }
    }

    private static void Zero(IntPtr buffer, int bytes)
    {
        for (var i = 0; i < bytes; i++) Marshal.WriteByte(buffer, i, 0);
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct CREDENTIAL
    {
        public int Flags;
        public int Type;
        public string TargetName;
        public string? Comment;
        public int LastWrittenLow;
        public int LastWrittenHigh;
        public int CredentialBlobSize;
        public IntPtr CredentialBlob;
        public int Persist;
        public int AttributeCount;
        public IntPtr Attributes;
        public string? TargetAlias;
        public string? UserName;
    }

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredWriteW(ref CREDENTIAL credential, int flags);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredReadW(string target, int type, int flags, out IntPtr credential);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredDeleteW(string target, int type, int flags);

    [DllImport("advapi32.dll")]
    private static extern void CredFree(IntPtr buffer);
}
