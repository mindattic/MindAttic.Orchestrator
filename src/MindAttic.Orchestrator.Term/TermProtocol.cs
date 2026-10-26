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

    /// <summary>u16 cols | u16 rows: the session's size, sent whenever it changes. From protocol 2 a third byte follows, 1 when
    /// the viewer it goes to owns the size (M.ORCH-T930), and it is also sent when only the owner changed, and on attach
    /// before the Replay. A viewer that doesn't own the size shows the session at this size and letterboxes.</summary>
    Size = 6,

    /// <summary>UTF-8 text: the window title (the CLI's last OSC 0/2, else the host's <c>--title</c>).</summary>
    Title = 7,

    /// <summary>Empty (protocol 2): this viewer and another sent input within <see cref="TermProtocol.TypingWindow"/> of each
    /// other. A viewer may show "another viewer is typing" for that long (M.ORCH-T930).</summary>
    Typing = 8,

    // Both ways.

    /// <summary>u16 protocol | u16 cols | u16 rows, and from protocol 2 (viewer to host) u8 flags (<see cref="TermHelloFlags"/>)
    /// and the viewer's id as UTF-8. A viewer opens with it; the host answers with the protocol the two will speak (the
    /// viewer's, when the host speaks it) and the session's size, or with its own protocol when it doesn't speak the viewer's,
    /// and then closes the pipe.</summary>
    Hello = 0x10,

    // Viewer -> host.

    /// <summary>Bytes for the ConPTY's input, as the renderer produced them (win32-input-mode VT, pastes).</summary>
    Input = 0x11,

    /// <summary>u16 cols | u16 rows: the size this viewer would show. Applied only while the viewer owns the size (M.ORCH-T930);
    /// otherwise kept, and applied once it takes the size.</summary>
    Resize = 0x12,

    /// <summary>Empty (protocol 2): this interactive viewer takes the size explicitly (M.ORCH-T930).</summary>
    TakeSize = 0x13,
}

/// <summary>The flags byte of a protocol 2 Hello from a viewer.</summary>
[Flags]
public enum TermHelloFlags : byte
{
    None = 0,
    /// <summary>The viewer may type and own the size; without it the host drops its Input, Resize and TakeSize frames.</summary>
    Interactive = 1,
}

/// <summary>A viewer's Hello. A protocol 1 Hello, which has no flags, is interactive with no id.</summary>
public sealed record TermHello(ushort Protocol, ushort Cols, ushort Rows, bool Interactive, string ViewerId);

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
/// <para>Attach: the viewer sends Hello; the host answers Hello, then (protocol 2) Size, then Replay, Live, Title (when known)
/// and Exit (when the CLI has exited), then the live Output, Modes, Size, Title, Typing and Exit frames as they happen.</para>
/// <para>Protocol 1 hosts serve one viewer at a time: a new connection replaces the old one, whose pipe is closed. From
/// protocol 2 (M.ORCH-T930) a host serves any number at once: output goes to all of them, input from interactive viewers is
/// merged (tmux semantics), input from read-only viewers is dropped, and the size belongs to the interactive viewer that last
/// sent input or TakeSize; only its Resize frames apply. A protocol 2 host still accepts protocol 1 viewers, as interactive
/// ones, answering their Hello with 1.</para>
/// </summary>
public static class TermProtocol
{
    /// <summary>The protocol this build speaks.</summary>
    public const ushort Version = 2;

    /// <summary>
    /// Every protocol this build's viewer can speak (M.ORCH-T932). TermHost installs side by side and a running host keeps its
    /// binary across upgrades, so hosts of several protocols run at once. A viewer must keep speaking every version still
    /// running: drop a number from here only once no host of it can still be running (its folder is cleaned up, see
    /// <see cref="TermHostInstalls"/>), and add the new one when <see cref="Version"/> is bumped. A bump that changes frames
    /// a viewer reads needs the viewer to branch on the host's Hello protocol.
    /// </summary>
    public static readonly IReadOnlyList<ushort> Supported = [1, 2];

    /// <summary>The lowest protocol that serves several viewers at once, with Size ownership, TakeSize and Typing (M.ORCH-T930).</summary>
    public const ushort MultiViewer = 2;

    /// <summary>Two viewers' input this close together makes the host send each a <see cref="TermFrameKind.Typing"/> frame.</summary>
    public static readonly TimeSpan TypingWindow = TimeSpan.FromSeconds(2);

    /// <summary>The protocol a host of <see cref="Version"/> speaks with a viewer that opened with <paramref name="viewerProtocol"/>:
    /// that one when this build speaks it, else null (the host answers with its own and closes).</summary>
    public static ushort? Negotiate(ushort viewerProtocol) => Supported.Contains(viewerProtocol) ? viewerProtocol : null;

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

    /// <summary>A protocol 2 viewer's Hello: <see cref="Hello(ushort, ushort, ushort)"/>, then the flags and the viewer's id. For
    /// a protocol 1 host it is the plain Hello.</summary>
    public static byte[] Hello(ushort protocol, ushort cols, ushort rows, bool interactive, string viewerId)
    {
        if (protocol < MultiViewer) return Hello(protocol, cols, rows);
        var id = Encoding.UTF8.GetBytes(viewerId);
        var payload = new byte[7 + id.Length];
        Hello(protocol, cols, rows).CopyTo(payload, 0);
        payload[6] = (byte)(interactive ? TermHelloFlags.Interactive : TermHelloFlags.None);
        id.CopyTo(payload, 7);
        return payload;
    }

    /// <summary>A viewer's Hello, of any protocol; null when it is too short to be one. Without the flags byte (protocol 1, or a
    /// protocol 2 viewer that left them out) it is interactive.</summary>
    public static TermHello? ReadViewerHello(ReadOnlySpan<byte> payload)
    {
        if (ReadHello(payload) is not { } h) return null;
        var interactive = payload.Length < 7 || ((TermHelloFlags)payload[6] & TermHelloFlags.Interactive) != 0;
        var id = payload.Length > 7 ? Encoding.UTF8.GetString(payload[7..]) : "";
        return new TermHello(h.Protocol, h.Cols, h.Rows, interactive, id);
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

    /// <summary>A protocol 2 Size payload: the size, and whether the viewer it goes to owns it.</summary>
    public static byte[] Size(ushort cols, ushort rows, bool owned)
    {
        var payload = new byte[5];
        BinaryPrimitives.WriteUInt16LittleEndian(payload, cols);
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(2), rows);
        payload[4] = owned ? (byte)1 : (byte)0;
        return payload;
    }

    /// <summary>A Size payload with its owner byte; <c>Owned</c> is null for a protocol 1 Size, which has none. Null when it is
    /// too short to be one.</summary>
    public static (ushort Cols, ushort Rows, bool? Owned)? ReadSizeOwner(ReadOnlySpan<byte> payload) =>
        ReadSize(payload) is not { } size ? null : (size.Cols, size.Rows, payload.Length >= 5 ? payload[4] != 0 : (bool?)null);

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
