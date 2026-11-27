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
    public const string DefaultRoot = @"R:\MindAttic\Media\Capture\MindAttic.Orchestrator";

    /// <summary>The root before captures moved under R:\MindAttic\Media (M.CAPT-T199, M.CAPT-T208); any frames still there
    /// from before the move are read once for timing, so a dashboard that hasn't captured since isn't treated as idle
    /// longer than it was. LEGACY-ROOT</summary>
    public const string LegacyRoot = @"R:\MindAttic\Capture\MindAttic.Orchestrator"; // LEGACY-ROOT

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

    /// <summary>How long one window runs: a 5-hour window's frames make one GIF.</summary>
    public static readonly TimeSpan WindowLength = TimeSpan.FromHours(5);

    /// <summary>The file name of the GIF stitched from the window that started at <paramref name="windowStart"/> (M.ORCH-T1941).</summary>
    public static string GifName(DateTimeOffset windowStart) => "Orchestrator_" + windowStart.UtcDateTime.ToString(Stamp, CultureInfo.InvariantCulture) + ".gif";

    /// <summary>The finished windows among <paramref name="frames"/> (oldest first): every frame before <paramref name="boundary"/>,
    /// the start of the window that's still running, grouped into windows of at most <see cref="WindowLength"/> each. A group whose
    /// first frame falls in one of <paramref name="knownStarts"/>' windows is named by that start, else by its first frame (the
    /// window's real start isn't known, as after a restart).</summary>
    public static IReadOnlyList<(DateTimeOffset Start, IReadOnlyList<string> Frames)> Finished(
        IReadOnlyList<(DateTimeOffset At, string Path)> frames, DateTimeOffset boundary, IEnumerable<DateTimeOffset> knownStarts)
    {
        var starts = knownStarts.ToList();
        var windows = new List<(DateTimeOffset, IReadOnlyList<string>)>();
        var i = 0;
        while (i < frames.Count && frames[i].At < boundary)
        {
            var first = frames[i].At;
            var start = starts.Where(s => s <= first && first < s + WindowLength).Select(s => (DateTimeOffset?)s).Max() ?? first;
            var end = start + WindowLength < boundary ? start + WindowLength : boundary;
            var paths = new List<string>();
            while (i < frames.Count && frames[i].At < end) paths.Add(frames[i++].Path);
            windows.Add((start, paths));
        }
        return windows;
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
