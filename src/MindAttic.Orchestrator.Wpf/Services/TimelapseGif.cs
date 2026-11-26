using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MindAttic.Orchestrator.Wpf.Services;

/// <summary>
/// Stitches the timelapse's PNG frames into one looping animated GIF (M.ORCH-T1941). WPF's GifBitmapEncoder writes each frame
/// with its own palette but no frame delay or loop, so each frame is encoded on its own (reduced to a 256-colour palette made
/// from that frame) and its image is copied into a GIF89a that carries the delay, a full clear between frames, and the
/// NETSCAPE2.0 loop.
/// </summary>
public static class TimelapseGif
{
    /// <summary>Writes <paramref name="pngs"/>, in order, to <paramref name="path"/> at <paramref name="fps"/> frames a second,
    /// through a temp file so a half-written GIF is never left under its name; frames that won't decode are skipped. False, and
    /// nothing written, with no frame to stitch.</summary>
    public static bool Write(IReadOnlyList<string> pngs, string path, int fps)
    {
        var frames = new List<GifImage>();
        foreach (var png in pngs)
        {
            try { frames.Add(Parse(EncodeSingle(png))); }
            catch (Exception ex) when (ex is IOException or NotSupportedException or FileFormatException or InvalidDataException or ArgumentException) { }
        }
        if (frames.Count == 0) return false;

        var delay = (ushort)Math.Max(1, (int)Math.Round(100.0 / Math.Max(1, fps)));
        var width = (ushort)frames.Max(f => f.Width);
        var height = (ushort)frames.Max(f => f.Height);
        var temp = path + ".tmp";
        using (var file = File.Create(temp))
        using (var w = new BinaryWriter(file))
        {
            w.Write("GIF89a"u8);
            w.Write(width); w.Write(height);
            w.Write((byte)0x70); w.Write((byte)0); w.Write((byte)0); // no global colour table
            w.Write(new byte[] { 0x21, 0xFF, 0x0B }); w.Write("NETSCAPE2.0"u8); w.Write(new byte[] { 0x03, 0x01, 0x00, 0x00, 0x00 }); // loop forever
            foreach (var f in frames)
            {
                w.Write(new byte[] { 0x21, 0xF9, 0x04, 0x08 }); w.Write(delay); w.Write(new byte[] { 0x00, 0x00 }); // restore to background, no transparency
                w.Write((byte)0x2C); w.Write((ushort)0); w.Write((ushort)0); w.Write(f.Width); w.Write(f.Height);
                w.Write((byte)(0x80 | (f.Interlaced ? 0x40 : 0) | f.TableBits));
                w.Write(f.ColorTable);
                w.Write(f.Data);
            }
            w.Write((byte)0x3B);
        }
        File.Move(temp, path, overwrite: true);
        return true;
    }

    private static byte[] EncodeSingle(string png)
    {
        BitmapSource source;
        using (var stream = File.OpenRead(png))
            source = BitmapFrame.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
        var indexed = new FormatConvertedBitmap(source, PixelFormats.Indexed8, new BitmapPalette(source, 256), 0);
        var encoder = new GifBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(indexed));
        using var output = new MemoryStream();
        encoder.Save(output);
        return output.ToArray();
    }

    private sealed record GifImage(ushort Width, ushort Height, bool Interlaced, int TableBits, byte[] ColorTable, byte[] Data);

    /// <summary>The first image of a single-image GIF: its size, its colour table (its own, else the global one) and its LZW data
    /// (the minimum code size and every sub-block through the terminator).</summary>
    private static GifImage Parse(byte[] gif)
    {
        if (gif.Length < 13 || gif[0] != (byte)'G' || gif[1] != (byte)'I' || gif[2] != (byte)'F') throw new InvalidDataException("Not a GIF");
        var pos = 13;
        var lsd = gif[10];
        byte[]? global = null;
        var globalBits = lsd & 7;
        if ((lsd & 0x80) != 0)
        {
            global = gif[pos..(pos + TableLength(globalBits))];
            pos += global.Length;
        }
        while (pos < gif.Length)
        {
            switch (gif[pos])
            {
                case 0x21:
                    pos = SkipSubBlocks(gif, pos + 2);
                    break;
                case 0x2C:
                    var width = BitConverter.ToUInt16(gif, pos + 5);
                    var height = BitConverter.ToUInt16(gif, pos + 7);
                    var packed = gif[pos + 9];
                    pos += 10;
                    byte[] table;
                    int bits;
                    if ((packed & 0x80) != 0)
                    {
                        bits = packed & 7;
                        table = gif[pos..(pos + TableLength(bits))];
                        pos += table.Length;
                    }
                    else
                    {
                        table = global ?? throw new InvalidDataException("A GIF image with no colour table");
                        bits = globalBits;
                    }
                    var dataStart = pos;
                    pos = SkipSubBlocks(gif, pos + 1);
                    return new GifImage(width, height, (packed & 0x40) != 0, bits, table, gif[dataStart..pos]);
                default:
                    throw new InvalidDataException("No image in the GIF");
            }
        }
        throw new InvalidDataException("No image in the GIF");
    }

    private static int TableLength(int bits) => 3 << (bits + 1);

    /// <summary>The position just past the sub-blocks starting at <paramref name="pos"/> and their zero-length terminator.</summary>
    private static int SkipSubBlocks(byte[] gif, int pos)
    {
        while (pos < gif.Length && gif[pos] != 0) pos += gif[pos] + 1;
        if (pos >= gif.Length) throw new InvalidDataException("A GIF cut short");
        return pos + 1;
    }
}
