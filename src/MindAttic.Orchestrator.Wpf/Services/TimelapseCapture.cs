using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using MindAttic.Orchestrator.Settings;
using MindAttic.Orchestrator.Wpf.ViewModels;
using Serilog;

namespace MindAttic.Orchestrator.Wpf.Services;

/// <summary>
/// The dashboard timelapse's frame capture (M.ORCH-T534, M.ORCH-T1940): every <see cref="DashboardTimings.TimelapseInterval"/>
/// while the dashboard runs, its window's own visual is rendered to a PNG in <see cref="TimelapseFrames.FramesDir"/>. It renders
/// the visual tree, not the screen, so a minimized, covered or tray-hidden window still captures and nothing flashes. The frames
/// stay on disk, so a restart keeps them, and the next one is timed from the newest there: a gap while the dashboard was closed
/// gets one frame on its return, not a catch-up run. Hosted native terminals (HwndHost) aren't part of the visual and render blank.
/// At each 5-hour window's reset (M.ORCH-T1941) the finished window's frames are stitched into
/// <see cref="TimelapseFrames.GifName"/> under the root at <see cref="OrchestratorSettings.TimelapseFrameRate"/> and removed, so
/// the frames folder only ever holds the running window.
/// </summary>
public sealed class TimelapseCapture : IDisposable
{
    private static readonly ILogger Log = OrchestratorLog.For("Timelapse");

    private readonly Window window;
    private readonly string root;
    private readonly string dir;
    private readonly Func<bool> enabled;
    private readonly Func<TimeSpan> interval;
    private readonly Func<DateTimeOffset> clock;
    private readonly Func<DateTime?> fiveHourReset;
    private readonly Func<int> fps;
    private readonly DispatcherTimer timer;
    private DateTimeOffset? last;
    // The running window's start and end as last read, and the one before it, so a GIF is named by its window's real start.
    private DateTimeOffset? windowStart, windowEnd, previousStart;
    private bool saving;

    /// <summary>Captures <paramref name="window"/> into <paramref name="root"/>'s frames folder while <paramref name="enabled"/> says
    /// so, one frame per <paramref name="interval"/>; <paramref name="check"/> is how often it looks whether one is due.
    /// <paramref name="fiveHourReset"/> is when the running 5-hour window resets (null while none is known), and each finished
    /// window becomes a GIF at <paramref name="fps"/>.</summary>
    public TimelapseCapture(Window window, string root, Func<bool> enabled, Func<TimeSpan> interval, TimeSpan check, Func<DateTimeOffset>? clock = null,
        Func<DateTime?>? fiveHourReset = null, Func<int>? fps = null)
    {
        this.window = window;
        this.root = root;
        dir = TimelapseFrames.FramesDir(root);
        this.fiveHourReset = fiveHourReset ?? (() => null);
        this.fps = fps ?? (() => OrchestratorSettings.DefaultTimelapseFps);
        this.enabled = enabled;
        this.interval = interval;
        this.clock = clock ?? (() => DateTimeOffset.UtcNow);
        timer = new DispatcherTimer(DispatcherPriority.Background, window.Dispatcher) { Interval = check };
        timer.Tick += (_, _) => _ = TickAsync();
        window.Closed += (_, _) => timer.Stop();
    }

    /// <summary>The dashboard's: <see cref="TimelapseFrames.DefaultRoot"/>, the live settings, checked every minute, its windows
    /// those of <paramref name="fiveHourReset"/>.</summary>
    public static TimelapseCapture ForDashboard(Window window, Func<DateTime?> fiveHourReset) =>
        new(window, TimelapseFrames.DefaultRoot, () => DashboardTimings.Timelapse, () => DashboardTimings.TimelapseInterval, TimeSpan.FromMinutes(1),
            fiveHourReset: fiveHourReset, fps: () => DashboardTimings.TimelapseFps);

    /// <summary>Starts timing from the newest frame already on disk; the first check is one <c>check</c> from now.</summary>
    public void Start()
    {
        try
        {
            last = TimelapseFrames.Latest(dir);
            if (last is null && root == TimelapseFrames.DefaultRoot) // LEGACY-ROOT
                last = TimelapseFrames.Latest(TimelapseFrames.FramesDir(TimelapseFrames.LegacyRoot));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Log.Warning(ex, "Couldn't read the timelapse frames in {Dir:l}; timing from now", dir); }
        timer.Start();
    }

    /// <summary>Captures a frame if timelapses are on and one is due; the path written, else null. Never throws.</summary>
    public async Task<string?> TickAsync()
    {
        if (saving || !enabled()) return null;
        var now = clock();
        saving = true;
        try { await StitchFinishedAsync(now).ConfigureAwait(true); }
        catch (Exception ex) { Log.Warning(ex, "Timelapse GIF not stitched"); }
        finally { saving = false; }
        if (TimelapseFrames.UntilNext(last, now, interval()) > TimeSpan.Zero) return null;
        saving = true;
        try
        {
            if (Render(window) is not { } frame) return null;
            var path = await Task.Run(() => TimelapseFrames.Save(dir, now, Encode(frame))).ConfigureAwait(true);
            last = now;
            return path;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Timelapse frame not captured");
            // Not again until the next interval, so a full or missing drive isn't retried every check.
            last = now;
            return null;
        }
        finally { saving = false; }
    }

    /// <summary>At a 5-hour window's reset, stitches the frames from before the running window into one GIF per finished window
    /// and deletes them; the GIFs written. With no window known yet nothing is stitched, and the frames wait.</summary>
    public async Task<IReadOnlyList<string>> StitchFinishedAsync(DateTimeOffset now)
    {
        if (fiveHourReset() is { } reset)
        {
            var end = new DateTimeOffset(DateTime.SpecifyKind(reset, DateTimeKind.Utc));
            var start = end - TimelapseFrames.WindowLength;
            if (windowStart != start)
            {
                previousStart = windowStart;
                (windowStart, windowEnd) = (start, end);
            }
        }
        // The running window's start, or, with none running since the last one ended, that one's end.
        DateTimeOffset? boundary = windowEnd is { } e && e <= now ? e : windowStart;
        if (boundary is not { } b) return [];
        var starts = new[] { previousStart, windowStart }.OfType<DateTimeOffset>().ToList();
        var rate = fps();
        return await Task.Run(() =>
        {
            var written = new List<string>();
            foreach (var (start, frames) in TimelapseFrames.Finished(TimelapseFrames.List(dir), b, starts))
            {
                var gif = Path.Combine(root, TimelapseFrames.GifName(start));
                if (TimelapseGif.Write(frames, gif, rate)) written.Add(gif);
                foreach (var frame in frames) File.Delete(frame);
                Log.Information("Timelapse window {Start} stitched into {Gif:l} from {Count} frames", start, gif, frames.Count);
            }
            return (IReadOnlyList<string>)written;
        }).ConfigureAwait(true);
    }

    /// <summary><paramref name="window"/>'s content rendered over its background at its own DPI, frozen so another thread can encode
    /// it; null while it has no size yet. Minimized or hidden, WPF keeps the last layout, so the frame is the window as it was.</summary>
    public static BitmapSource? Render(Window window)
    {
        if (window.Content is not FrameworkElement content || content.ActualWidth < 1 || content.ActualHeight < 1) return null;
        var dpi = VisualTreeHelper.GetDpi(window);
        var width = (int)Math.Ceiling(content.ActualWidth * dpi.DpiScaleX);
        var height = (int)Math.Ceiling(content.ActualHeight * dpi.DpiScaleY);
        var bitmap = new RenderTargetBitmap(width, height, dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32);
        var background = new DrawingVisual();
        using (var dc = background.RenderOpen())
            dc.DrawRectangle(window.Background ?? Brushes.Black, null, new Rect(0, 0, content.ActualWidth, content.ActualHeight));
        bitmap.Render(background);
        bitmap.Render(content);
        bitmap.Freeze();
        return bitmap;
    }

    private static byte[] Encode(BitmapSource frame)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(frame));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }

    /// <inheritdoc/>
    public void Dispose() => timer.Stop();
}
