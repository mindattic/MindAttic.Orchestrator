using System.Globalization;

namespace MindAttic.Orchestrator;

/// <summary>
/// The dashboard timelapse's frames on disk (M.ORCH-T534, M.ORCH-T1940): one PNG per capture, named by its UTC time, in a folder
/// that outlives the dashboard, so a restart mid-window keeps what was captured before it. When the next frame is due is worked out
/// from the newest one there, and a gap while the dashboard was closed is skipped rather than filled in: one frame as soon as it's
/// back, then one every interval.
/// </summary>
public static class TimelapseFrames
{
    /// <summary>Where the timelapse's frames and GIFs go.</summary>
    public const string DefaultRoot = @"R:\MindAttic\Capture\MindAttic.Orchestrator";

    private const string Prefix = "frame-";
    private const string Extension = ".png";
    private const string Stamp = "yyyyMMdd'T'HHmmss'Z'";

    /// <summary>The folder the frames wait in until they're stitched, under <paramref name="root"/>.</summary>
    public static string FramesDir(string root) => Path.Combine(root, "frames");

    /// <summary>The file name of a frame captured at <paramref name="at"/>.</summary>
    public static string FileName(DateTimeOffset at) => Prefix + at.UtcDateTime.ToString(Stamp, CultureInfo.InvariantCulture) + Extension;

    /// <summary>The frames in <paramref name="dir"/>, oldest first, each with the time its name gives; files of other names are left out.</summary>
    public static IReadOnlyList<(DateTimeOffset At, string Path)> List(string dir)
    {
        if (!Directory.Exists(dir)) return [];
        var frames = new List<(DateTimeOffset, string)>();
        foreach (var path in Directory.EnumerateFiles(dir, Prefix + "*" + Extension))
        {
            var name = Path.GetFileName(path);
            var stamp = name[Prefix.Length..^Extension.Length];
            if (DateTime.TryParseExact(stamp, Stamp, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var at))
                frames.Add((new DateTimeOffset(at, TimeSpan.Zero), path));
        }
        frames.Sort((a, b) => a.Item1.CompareTo(b.Item1));
        return frames;
    }

    /// <summary>When the newest frame in <paramref name="dir"/> was captured, or null with none.</summary>
    public static DateTimeOffset? Latest(string dir) => List(dir) is { Count: > 0 } frames ? frames[^1].At : null;

    /// <summary>How long until the next frame is due: none yet, or the last one an <paramref name="interval"/> or more ago (a closed
    /// gap included), is now; otherwise the rest of the interval. A last frame in the future (the clock moved back) counts as now.</summary>
    public static TimeSpan UntilNext(DateTimeOffset? last, DateTimeOffset now, TimeSpan interval)
    {
        if (last is not { } at || at > now) return TimeSpan.Zero;
        var left = at + interval - now;
        return left > TimeSpan.Zero ? left : TimeSpan.Zero;
    }

    /// <summary>Writes <paramref name="png"/> as the frame for <paramref name="at"/> in <paramref name="dir"/>, through a temp file so a
    /// half-written frame is never listed, and returns its path.</summary>
    public static string Save(string dir, DateTimeOffset at, byte[] png)
    {
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, FileName(at));
        var temp = path + ".tmp";
        File.WriteAllBytes(temp, png);
        File.Move(temp, path, overwrite: true);
        return path;
    }
}
