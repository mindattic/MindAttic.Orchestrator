using System.Buffers.Binary;
using System.Text;

namespace MindAttic.Orchestrator.Term;

/// <summary>
/// The kind byte of a TermHost pipe frame (M.ORCH-T924, spike docs/spikes/cli-host-viewer.md section 5).
/// </summary>
public enum TermFrameKind : byte
{
    // Host -> viewer.

    /// <summary>What a viewer gets on attach: u16 cols | u16 rows | u32 <see cref="TermModes"/> | u32 dataLen | data |
    /// re-asserted modes (the rest). Write data and the rest to the renderer, in that order.</summary>
    Replay = 1,

    /// <summary>Empty: everything after it is the live stream.</summary>
    Live = 2,

    /// <summary>Raw VT output, as the CLI wrote it.</summary>
    Output = 3,

    /// <summary>u32 <see cref="TermModes"/>, sent whenever a tracked mode changes.</summary>
    Modes = 4,

    /// <summary>i32 exit code: the CLI has exited (sent on attach too, once it has).</summary>
    Exit = 5,

    /// <summary>u16 cols | u16 rows: the session's size, sent whenever it changes.</summary>
    Size = 6,

    /// <summary>UTF-8 text: the window title (the CLI's last OSC 0/2, else the host's <c>--title</c>).</summary>
    Title = 7,

    // Both ways.

    /// <summary>u16 protocol | u16 cols | u16 rows. A viewer opens with it; the host answers with its own protocol and the
    /// session's size, and closes the pipe after that when it doesn't speak the viewer's protocol.</summary>
    Hello = 0x10,

    // Viewer -> host.

    /// <summary>Bytes for the ConPTY's input, as the renderer produced them (win32-input-mode VT, pastes).</summary>
    Input = 0x11,

    /// <summary>u16 cols | u16 rows: resize the session.</summary>
    Resize = 0x12,
}

/// <summary>The sticky terminal modes the host tracks from the CLI's output, as the bits of a Modes or Replay frame.</summary>
[Flags]
public enum TermModes : uint
{
    None = 0,
    /// <summary><c>?2004</c>: a paste should be wrapped in <c>ESC[200~</c> and <c>ESC[201~</c>.</summary>
    BracketedPaste = 1,
    /// <summary><c>?25</c> (on unless the CLI hid it).</summary>
    CursorVisible = 2,
    /// <summary><c>?1049</c>, <c>?47</c> or <c>?1047</c>.</summary>
    AltScreen = 4,
    /// <summary><c>?1</c>: application cursor keys.</summary>
    AppCursorKeys = 8,
    /// <summary><c>?1000</c>, <c>?1002</c> or <c>?1003</c>.</summary>
    Mouse = 16,
    /// <summary><c>?1006</c>: SGR mouse encoding.</summary>
    MouseSgr = 32,
    /// <summary><c>?1004</c>: focus in and out events.</summary>
    FocusEvents = 64,
    /// <summary><c>?2026</c>: synchronized output.</summary>
    SyncOutput = 128,
}

/// <summary>How a host's protocol compares to the ones a viewer speaks.</summary>
public enum TermCompatStatus { Compatible, HostTooNew, HostTooOld }

/// <summary>The result of <see cref="TermProtocol.Check"/>: <see cref="Message"/> is empty when compatible, else a clear status for the user.</summary>
public sealed record TermCompatibility(TermCompatStatus Status, ushort HostProtocol, string Message)
{
    public bool Ok => Status == TermCompatStatus.Compatible;
}

/// <summary>A decoded Replay frame. <see cref="Data"/> is the ring, <see cref="Reassert"/> the modes and title to write after it.</summary>
public sealed record TermReplay(ushort Cols, ushort Rows, TermModes Modes, byte[] Data, byte[] Reassert);

/// <summary>
/// The TermHost pipe protocol (M.ORCH-T924): one pipe per session, <see cref="PipeName"/>, current user only, carrying
/// <c>u8 kind | u32 length (little-endian) | payload</c> frames. Versioned in <see cref="TermFrameKind.Hello"/>.
/// <para>Attach: the viewer sends Hello; the host answers Hello, then Replay, Live, Title (when known) and Exit (when the CLI
/// has exited), then the live Output, Modes, Size, Title and Exit frames as they happen. One viewer at a time: a new
/// connection replaces the old one, whose pipe is closed.</para>
/// </summary>
public static class TermProtocol
{
    /// <summary>The protocol this build speaks.</summary>
    public const ushort Version = 1;

    /// <summary>
    /// Every protocol this build's viewer can speak (M.ORCH-T932). TermHost installs side by side and a running host keeps its
    /// binary across upgrades, so hosts of several protocols run at once. A viewer must keep speaking every version still
    /// running: drop a number from here only once no host of it can still be running (its folder is cleaned up, see
    /// <see cref="TermHostInstalls"/>), and add the new one when <see cref="Version"/> is bumped. A bump that changes frames
    /// a viewer reads needs the viewer to branch on the host's Hello protocol.
    /// </summary>
    public static readonly IReadOnlyList<ushort> Supported = [1];

    /// <summary>Whether a viewer that speaks <see cref="Supported"/> can talk to a host that answered Hello with
    /// <paramref name="hostProtocol"/>. Never throws: an unknown future version is <see cref="TermCompatStatus.HostTooNew"/>,
    /// shown to the user as its message instead of crashing the viewer.</summary>
    public static TermCompatibility Check(ushort hostProtocol) =>
        Supported.Contains(hostProtocol) ? new(TermCompatStatus.Compatible, hostProtocol, "")
        : hostProtocol > Supported.Max()
            ? new(TermCompatStatus.HostTooNew, hostProtocol,
                $"This session's terminal host speaks protocol {hostProtocol}, newer than this dashboard understands ({Supported.Max()}). Update the dashboard to attach; the CLI keeps running.")
            : new(TermCompatStatus.HostTooOld, hostProtocol,
                $"This session's terminal host speaks protocol {hostProtocol}, which this dashboard no longer supports. Close the session to move to the current host.");

    /// <summary>The largest frame either side accepts.</summary>
    public const int MaxPayload = 64 * 1024 * 1024;

    /// <summary>The pipe name prefix; the whole name is <c>mindattic-term-&lt;user&gt;-&lt;sessionId&gt;</c>.</summary>
    public const string PipePrefix = "mindattic-term-";

    /// <summary>The session's pipe: <c>mindattic-term-&lt;sanitized user&gt;-&lt;sanitized id&gt;</c>.</summary>
    public static string PipeName(string sessionId, string? user = null) =>
        PipePrefix + Sanitize(user ?? Environment.UserName) + "-" + Sanitize(sessionId);

    /// <summary>Letters and digits kept, anything else a dash, so a name is safe in a pipe or file name.</summary>
    public static string Sanitize(string text) => new(text.Select(c => char.IsAsciiLetterOrDigit(c) ? c : '-').ToArray());

    /// <summary>A new session id: 12 random hex characters.</summary>
    public static string NewSessionId() => Guid.NewGuid().ToString("N")[..12];

    /// <summary>One frame, ready to write.</summary>
    public static byte[] Frame(TermFrameKind kind, ReadOnlySpan<byte> payload)
    {
        var buffer = new byte[5 + payload.Length];
        buffer[0] = (byte)kind;
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(1), (uint)payload.Length);
        payload.CopyTo(buffer.AsSpan(5));
        return buffer;
    }

    /// <summary>Reads one frame; throws <see cref="EndOfStreamException"/> when the pipe closes, <see cref="InvalidDataException"/>
    /// on a frame over <see cref="MaxPayload"/>.</summary>
    public static async Task<(TermFrameKind Kind, byte[] Payload)> ReadFrameAsync(Stream stream, CancellationToken ct = default)
    {
        var head = new byte[5];
        await stream.ReadExactlyAsync(head, ct).ConfigureAwait(false);
        var length = BinaryPrimitives.ReadUInt32LittleEndian(head.AsSpan(1));
        if (length > MaxPayload) throw new InvalidDataException($"A {length}-byte frame is over the {MaxPayload}-byte limit.");
        var payload = new byte[length];
        if (length > 0) await stream.ReadExactlyAsync(payload, ct).ConfigureAwait(false);
        return ((TermFrameKind)head[0], payload);
    }

    public static byte[] Hello(ushort protocol, ushort cols, ushort rows)
    {
        var payload = new byte[6];
        BinaryPrimitives.WriteUInt16LittleEndian(payload, protocol);
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(2), cols);
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(4), rows);
        return payload;
    }

    /// <summary>A Hello payload; null when it is too short to be one.</summary>
    public static (ushort Protocol, ushort Cols, ushort Rows)? ReadHello(ReadOnlySpan<byte> payload) =>
        payload.Length < 6 ? null
            : (BinaryPrimitives.ReadUInt16LittleEndian(payload), BinaryPrimitives.ReadUInt16LittleEndian(payload[2..]),
               BinaryPrimitives.ReadUInt16LittleEndian(payload[4..]));

    /// <summary>A Size or Resize payload.</summary>
    public static byte[] Size(ushort cols, ushort rows)
    {
        var payload = new byte[4];
        BinaryPrimitives.WriteUInt16LittleEndian(payload, cols);
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(2), rows);
        return payload;
    }

    /// <summary>A Size or Resize payload; null when it is too short to be one.</summary>
    public static (ushort Cols, ushort Rows)? ReadSize(ReadOnlySpan<byte> payload) =>
        payload.Length < 4 ? null : (BinaryPrimitives.ReadUInt16LittleEndian(payload), BinaryPrimitives.ReadUInt16LittleEndian(payload[2..]));

    public static byte[] Modes(TermModes modes)
    {
        var payload = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(payload, (uint)modes);
        return payload;
    }

    public static TermModes ReadModes(ReadOnlySpan<byte> payload) =>
        payload.Length < 4 ? TermModes.None : (TermModes)BinaryPrimitives.ReadUInt32LittleEndian(payload);

    public static byte[] Exit(int code)
    {
        var payload = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(payload, code);
        return payload;
    }

    public static int ReadExit(ReadOnlySpan<byte> payload) => payload.Length < 4 ? -1 : BinaryPrimitives.ReadInt32LittleEndian(payload);

    public static byte[] Title(string title) => Encoding.UTF8.GetBytes(title);

    public static string ReadTitle(ReadOnlySpan<byte> payload) => Encoding.UTF8.GetString(payload);

    public static byte[] Replay(ushort cols, ushort rows, TermModes modes, ReadOnlySpan<byte> data, ReadOnlySpan<byte> reassert)
    {
        var payload = new byte[12 + data.Length + reassert.Length];
        BinaryPrimitives.WriteUInt16LittleEndian(payload, cols);
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(2), rows);
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(4), (uint)modes);
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(8), (uint)data.Length);
        data.CopyTo(payload.AsSpan(12));
        reassert.CopyTo(payload.AsSpan(12 + data.Length));
        return payload;
    }

    /// <summary>A Replay payload; null when it is malformed.</summary>
    public static TermReplay? ReadReplay(ReadOnlySpan<byte> payload)
    {
        if (payload.Length < 12) return null;
        var length = BinaryPrimitives.ReadUInt32LittleEndian(payload[8..]);
        if (length > payload.Length - 12) return null;
        var data = payload.Slice(12, (int)length);
        return new TermReplay(BinaryPrimitives.ReadUInt16LittleEndian(payload), BinaryPrimitives.ReadUInt16LittleEndian(payload[2..]),
            (TermModes)BinaryPrimitives.ReadUInt32LittleEndian(payload[4..]), data.ToArray(), payload[(12 + (int)length)..].ToArray());
    }
}
