using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;
using MindAttic.Orchestrator.Setup;
using Forms = System.Windows.Forms;
using Drawing = System.Drawing;

namespace MindAttic.Orchestrator.Wpf;

/// <summary>
/// The dashboard lives in the tray again (M.ORCH-T1889, superseding M.ORCH-T501's "no tray to hide
/// in"): minimizing or closing the window hides it here instead of quitting, a left click brings it
/// back, and only the tray menu's Exit quits for real. The icon and tooltip show whether the daemon
/// (<see cref="Installer.DaemonRunning"/>'s mutex) is up, polled on a timer; "Restart Daemon" acts on
/// a daemon seen down without waiting for the next poll.
/// </summary>
public sealed class Tray : IDisposable
{
    private readonly Window window;
    private readonly Installer installer;
    private readonly string exe;
    private readonly Forms.NotifyIcon icon;
    private readonly DispatcherTimer timer;
    private static readonly Drawing.Icon HealthyIcon = Dot(Drawing.Color.FromArgb(0x2E, 0xA0, 0x43));
    private static readonly Drawing.Icon DownIcon = Dot(Drawing.Color.FromArgb(0xD9, 0x3B, 0x3B));
    private bool exiting;
    private bool? lastHealthy;

    public Tray(Window window, Installer installer, string exe)
    {
        this.window = window;
        this.installer = installer;
        this.exe = exe;
        icon = new Forms.NotifyIcon
        {
            Icon = HealthyIcon,
            Visible = true,
            ContextMenuStrip = new Forms.ContextMenuStrip(),
        };
        icon.MouseClick += (_, e) => { if (e.Button == Forms.MouseButtons.Left) ShowWindow(); };
        icon.MouseDoubleClick += (_, e) => { if (e.Button == Forms.MouseButtons.Left) ShowWindow(); };
        icon.ContextMenuStrip.Opening += (_, _) => BuildMenu();
        MinimizeToTray(window);
        CloseToTray(window, () => exiting);

        timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(20) };
        timer.Tick += (_, _) => Update();
        timer.Start();
        Update();
    }

    private void Update()
    {
        var healthy = Installer.DaemonRunning();
        icon.Text = healthy ? "MindAttic Orchestrator - daemon running" : "MindAttic Orchestrator - daemon down";
        if (healthy != lastHealthy) icon.Icon = healthy ? HealthyIcon : DownIcon;
        lastHealthy = healthy;
    }

    private void BuildMenu()
    {
        var menu = icon.ContextMenuStrip!;
        var old = menu.Items.Cast<Forms.ToolStripItem>().ToList();
        menu.Items.Clear();
        foreach (var item in old) item.Dispose(); // Clear() doesn't dispose; rebuilt fresh on every open.

        var healthy = Installer.DaemonRunning();
        menu.Items.Add(new Forms.ToolStripLabel(healthy ? "Daemon: running" : "Daemon: down") { ForeColor = healthy ? Drawing.SystemColors.GrayText : Drawing.Color.Firebrick });
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Open Dashboard", null, (_, _) => ShowWindow());
        menu.Items.Add(new Forms.ToolStripMenuItem("Restart Daemon", null, (_, _) => RestartDaemon()) { Enabled = !healthy });
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => Exit());
    }

    private void RestartDaemon()
    {
        try { installer.EnsureDaemon(exe); }
        catch (InvalidOperationException ex) { icon.ShowBalloonTip(4000, "Couldn't restart the daemon", ex.Message, Forms.ToolTipIcon.Error); }
        Update();
    }

    private void ShowWindow() => DashboardWindow.Restore(window);

    /// <summary>Quits for real: the tray's Exit, or an upgrade asking this dashboard to make way (<see cref="Installer.DashboardExitEvent"/>).</summary>
    public void Exit()
    {
        exiting = true;
        Application.Current.Shutdown();
    }

    /// <summary>Minimizing <paramref name="window"/> hides it to the tray rather than leaving it on the taskbar.</summary>
    private static void MinimizeToTray(Window window)
    {
        DashboardWindow.Track(window);
        window.StateChanged += (_, _) => { if (window.WindowState == WindowState.Minimized) window.Hide(); };
    }

    /// <summary>The close button hides <paramref name="window"/> to the tray instead of quitting (M.ORCH-T183's original
    /// behavior, restored): it closes for real only once <paramref name="exiting"/> says so (the tray's Exit). Windows
    /// ending the session closes it regardless (App.xaml.cs's SessionEnding handler runs first either way).</summary>
    private static void CloseToTray(Window window, Func<bool> exiting) =>
        window.Closing += (_, e) =>
        {
            if (exiting()) return;
            e.Cancel = true;
            window.Hide();
        };

    /// <summary>A small filled circle as a 16x16 icon: simplest readable health dot at tray size, no asset file needed.
    /// Built once per color and cached (never per tick), so no icon handles leak from repeated swaps.</summary>
    private static Drawing.Icon Dot(Drawing.Color color)
    {
        using var bmp = new Drawing.Bitmap(16, 16);
        using (var g = Drawing.Graphics.FromImage(bmp))
        {
            g.SmoothingMode = Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var brush = new Drawing.SolidBrush(color);
            g.FillEllipse(brush, 1, 1, 13, 13);
        }
        var hIcon = bmp.GetHicon();
        try { return (Drawing.Icon)Drawing.Icon.FromHandle(hIcon).Clone(); } // Clone owns its own copy; the GDI handle is freed either way.
        finally { DestroyIcon(hIcon); }
    }

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr handle);

    public void Dispose()
    {
        timer.Stop();
        icon.Visible = false;
        icon.Dispose();
    }
}
