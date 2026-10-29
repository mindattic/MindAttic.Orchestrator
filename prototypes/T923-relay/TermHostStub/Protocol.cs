using System.Buffers.Binary;
using System.IO;

namespace TermHostStub;

/// <summary>
/// The relay framing (M.ORCH-T923): u8 kind | u32 length (little-endian) | payload. The viewer project links this file.
/// Replay payload: u16 cols | u16 rows | u32 modeFlags | u32 dataLen | data | re-assert VT (the rest).
/// Modes payload: u32 modeFlags. Exit payload: i32 code. Hello and Resize payload: u16 cols | u16 rows. Input: UTF-8. Output: raw bytes.
/// </summary>
internal static class Protocol
{
    // Host -> viewer.
    public const byte Replay = 1, Live = 2, Output = 3, Modes = 4, Exit = 5;
    // Viewer -> host.
    public const byte Hello = 0x10, Input = 0x11, Resize = 0x12;

    // Sticky-mode bits in the Modes frame and in Replay.
    public const uint Bracketed = 1, CursorVisible = 2, AltScreen = 4, AppCursor = 8, Mouse = 16, MouseSgr = 32, Focus = 64, SyncOutput = 128;

    public static string PipeName(string id) =>
        "mindattic-termproto-" + Sanitize(Environment.UserName) + "-" + Sanitize(id);

    // A readable prefix plus a hash of the whole string: mapping every non-alphanumeric character to the same '-' would
    // otherwise collide two different ids ("foo.bar" and "foo-bar" both becoming "foo-bar"), handing a viewer connecting to
    // one id the other id's host, and colliding their log files too.
    public static string Sanitize(string s) =>
        new string(s.Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray()) + "-" +
        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(s)))[..8].ToLowerInvariant();

    public static byte[] Frame(byte kind, ReadOnlySpan<byte> payload)
    {
        var buf = new byte[5 + payload.Length];
        buf[0] = kind;
        BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(1), (uint)payload.Length);
        payload.CopyTo(buf.AsSpan(5));
        return buf;
    }

    public static async Task<(byte Kind, byte[] Payload)> ReadFrameAsync(Stream s, CancellationToken ct = default)
    {
        var head = new byte[5];
        await s.ReadExactlyAsync(head, ct).ConfigureAwait(false);
        var len = BinaryPrimitives.ReadUInt32LittleEndian(head.AsSpan(1));
        if (len > 64 * 1024 * 1024) throw new InvalidDataException("frame too large");
        var payload = new byte[len];
        if (len > 0) await s.ReadExactlyAsync(payload, ct).ConfigureAwait(false);
        return (head[0], payload);
    }

    public static byte[] Size(ushort cols, ushort rows)
    {
        var b = new byte[4];
        BinaryPrimitives.WriteUInt16LittleEndian(b, cols);
        BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(2), rows);
        return b;
    }
}
