using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MindAttic.Orchestrator.Wpf.Services;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>The dashboard timelapse's frame capture (M.ORCH-T534, M.ORCH-T1940): the window's own visual, minimized included,
/// one frame per interval, a closed gap skipped rather than filled in, and the frames kept across a restart. Test windows are
/// off-screen (M.ORCH-T530).</summary>
[Apartment(ApartmentState.STA)]
public sealed class TimelapseCaptureTests
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(15);
    private static readonly DateTimeOffset T0 = new(2026, 11, 26, 12, 0, 0, TimeSpan.Zero);
    private string root = "";

    [SetUp]
    public void SetUp() => root = Directory.CreateTempSubdirectory("orch-timelapse-").FullName;

    [TearDown]
    public void TearDown()
    {
        try { Directory.Delete(root, recursive: true); } catch (IOException) { }
    }

    private static Window RedWindow() =>
        OffScreenWindow.Show(content: new Border { Background = Brushes.Red, Width = 80, Height = 40 });

    private TimelapseCapture Capture(Window window, Func<DateTimeOffset> clock, Func<bool>? enabled = null) =>
        new(window, root, enabled ?? (() => true), () => Interval, TimeSpan.FromHours(1), clock);

    [Test]
    public async Task A_minimized_window_still_captures_its_own_visual()
    {
        var window = RedWindow();
        window.WindowState = WindowState.Minimized;
        var capture = Capture(window, () => T0);
        capture.Start();

        var path = await capture.TickAsync();

        Assert.That(path, Is.EqualTo(Path.Combine(TimelapseFrames.FramesDir(root), TimelapseFrames.FileName(T0))));
        var frame = BitmapFrame.Create(new MemoryStream(File.ReadAllBytes(path!)));
        Assert.That(frame.PixelWidth, Is.GreaterThan(0));
        var pixel = new byte[4];
        new FormatConvertedBitmap(frame, PixelFormats.Bgra32, null, 0).CopyPixels(new Int32Rect(frame.PixelWidth / 2, frame.PixelHeight / 2, 1, 1), pixel, 4, 0);
        Assert.That(pixel, Is.EqualTo(new byte[] { 0, 0, 255, 255 }), "the window's red content, not the screen behind it");
        capture.Dispose();
        window.Close();
    }

    [Test]
    public async Task One_frame_per_interval_and_a_closed_gap_is_skipped_not_filled()
    {
        var window = RedWindow();
        var now = T0;
        var capture = Capture(window, () => now);
        capture.Start();

        Assert.That(await capture.TickAsync(), Is.Not.Null, "the first frame at once");
        now = T0 + TimeSpan.FromMinutes(14);
        Assert.That(await capture.TickAsync(), Is.Null, "none before the interval is up");
        now = T0 + Interval;
        Assert.That(await capture.TickAsync(), Is.Not.Null);
        capture.Dispose();

        // Closed for two hours, then back: one frame on its return, timed afresh from it.
        var back = T0 + TimeSpan.FromHours(2) + TimeSpan.FromMinutes(7);
        now = back;
        var restarted = Capture(window, () => now);
        restarted.Start();
        Assert.That(await restarted.TickAsync(), Is.Not.Null);
        Assert.That(await restarted.TickAsync(), Is.Null);
        now = back + Interval;
        Assert.That(await restarted.TickAsync(), Is.Not.Null);

        Assert.That(TimelapseFrames.List(TimelapseFrames.FramesDir(root)).Select(f => f.At),
            Is.EqualTo(new[] { T0, T0 + Interval, back, back + Interval }));
        restarted.Dispose();
        window.Close();
    }

    [Test]
    public async Task Frames_from_before_a_restart_are_kept_and_time_the_next_one()
    {
        var window = RedWindow();
        var now = T0;
        var before = Capture(window, () => now);
        before.Start();
        await before.TickAsync();
        before.Dispose();

        now = T0 + TimeSpan.FromMinutes(5);
        var after = Capture(window, () => now);
        after.Start();

        Assert.That(await after.TickAsync(), Is.Null, "the frame before the restart still counts");
        Assert.That(TimelapseFrames.Latest(TimelapseFrames.FramesDir(root)), Is.EqualTo(T0));
        after.Dispose();
        window.Close();
    }

    [Test]
    public async Task Timelapse_off_captures_nothing()
    {
        var window = RedWindow();
        var capture = Capture(window, () => T0, enabled: () => false);
        capture.Start();

        Assert.That(await capture.TickAsync(), Is.Null);
        Assert.That(Directory.Exists(TimelapseFrames.FramesDir(root)), Is.False);
        capture.Dispose();
        window.Close();
    }

    [Test]
    public void Until_next_is_now_with_no_frame_after_a_gap_or_a_clock_moved_back()
    {
        Assert.That(TimelapseFrames.UntilNext(null, T0, Interval), Is.EqualTo(TimeSpan.Zero));
        Assert.That(TimelapseFrames.UntilNext(T0, T0 + TimeSpan.FromMinutes(10), Interval), Is.EqualTo(TimeSpan.FromMinutes(5)));
        Assert.That(TimelapseFrames.UntilNext(T0, T0 + TimeSpan.FromHours(3), Interval), Is.EqualTo(TimeSpan.Zero));
        Assert.That(TimelapseFrames.UntilNext(T0 + TimeSpan.FromHours(1), T0, Interval), Is.EqualTo(TimeSpan.Zero));
    }

    [Test]
    public async Task At_the_reset_the_window_is_stitched_into_a_looping_gif_and_the_next_window_starts()
    {
        var window = RedWindow();
        var now = T0;
        DateTime? reset = (T0 + TimeSpan.FromHours(4)).UtcDateTime;
        var start = T0 + TimeSpan.FromHours(4) - TimelapseFrames.WindowLength;
        var capture = new TimelapseCapture(window, root, () => true, () => Interval, TimeSpan.FromHours(1), () => now,
            fiveHourReset: () => reset, fps: () => 4);
        capture.Start();
        await capture.TickAsync();
        now = T0 + Interval;
        await capture.TickAsync();
        now = T0 + 2 * Interval;
        await capture.TickAsync();
        Assert.That(Directory.GetFiles(root, "*.gif"), Is.Empty, "nothing stitched while the window runs");

        // The reset passes before the next window is known: the finished one is stitched, named by its start.
        now = T0 + TimeSpan.FromHours(4) + TimeSpan.FromMinutes(1);
        reset = null;
        Assert.That(await capture.TickAsync(), Is.Not.Null, "the next window's first frame");

        var gif = Path.Combine(root, TimelapseFrames.GifName(start));
        Assert.That(Directory.GetFiles(root, "*.gif"), Is.EqualTo(new[] { gif }));
        var decoder = new GifBitmapDecoder(new MemoryStream(File.ReadAllBytes(gif)), BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
        Assert.That(decoder.Frames, Has.Count.EqualTo(3));
        var bytes = File.ReadAllBytes(gif);
        Assert.That(System.Text.Encoding.ASCII.GetString(bytes).Contains("NETSCAPE2.0"), Is.True, "it loops");
        Assert.That(BitConverter.ToUInt16(bytes, Array.IndexOf(bytes, (byte)0xF9) + 3), Is.EqualTo(25), "4 fps is 25 hundredths a frame");
        Assert.That(TimelapseFrames.List(TimelapseFrames.FramesDir(root)).Select(f => f.At), Is.EqualTo(new[] { now }),
            "the stitched frames are gone and the next window has begun");
        capture.Dispose();
        window.Close();
    }

    [Test]
    public void Finished_windows_are_the_frames_before_the_boundary_in_windows_of_five_hours()
    {
        var frames = new[] { T0, T0 + TimeSpan.FromHours(1), T0 + TimeSpan.FromHours(6), T0 + TimeSpan.FromHours(9), T0 + TimeSpan.FromHours(10) }
            .Select(at => (at, TimelapseFrames.FileName(at))).ToList();
        var known = T0 + TimeSpan.FromHours(5);

        var windows = TimelapseFrames.Finished(frames, T0 + TimeSpan.FromHours(10), [known]);

        Assert.That(windows.Select(w => w.Start), Is.EqualTo(new[] { T0, known }), "a known start names its window, else the first frame");
        Assert.That(windows.Select(w => w.Frames.Count), Is.EqualTo(new[] { 2, 2 }), "the running window's frame is left");
    }

    [Test]
    public void Listing_skips_files_that_are_not_frames()
    {
        var dir = TimelapseFrames.FramesDir(root);
        TimelapseFrames.Save(dir, T0, [1]);
        File.WriteAllBytes(Path.Combine(dir, "notes.png"), [1]);
        File.WriteAllBytes(Path.Combine(dir, TimelapseFrames.FileName(T0 + Interval) + ".tmp"), [1]);

        Assert.That(TimelapseFrames.List(dir).Select(f => f.At), Is.EqualTo(new[] { T0 }));
    }
}
