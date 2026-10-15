using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using MindAttic.Orchestrator.Wpf.ViewModels;
using Forms = System.Windows.Forms;

namespace MindAttic.Orchestrator.Wpf;

/// <summary>
/// The workspace without the full window: a tray icon whose tooltip is the one-line summary, with
/// quick actions (open, pause or resume autopilot, start the next proposal, jump to a repo that's
/// waiting on you), and an optional always-on-top HUD strip. Minimizing the dashboard hides it here.
/// </summary>
public sealed class Tray : IDisposable
{
    private readonly Window window;
    private readonly MainViewModel vm;
    private readonly Forms.NotifyIcon icon;
    private readonly DispatcherTimer timer;
    private Window? hud;
    private TextBlock? hudText;

    public Tray(Window window, MainViewModel vm)
    {
        this.window = window;
        this.vm = vm;
        icon = new Forms.NotifyIcon
        {
            Icon = System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath!) ?? System.Drawing.SystemIcons.Application,
            Visible = true,
            Text = "MindAttic Orchestrator",
            ContextMenuStrip = new Forms.ContextMenuStrip(),
        };
        icon.DoubleClick += (_, _) => ShowWindow();
        icon.ContextMenuStrip.Opening += (_, _) => BuildMenu();
        window.StateChanged += (_, _) => { if (window.WindowState == WindowState.Minimized) window.Hide(); };

        timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        timer.Tick += (_, _) => Update();
        timer.Start();
        Update();
    }

    private void Update()
    {
        var summary = vm.TraySummary;
        icon.Text = summary.Length > 127 ? summary[..127] : summary;
        if (hudText is not null) hudText.Text = summary;
    }

    private void BuildMenu()
    {
        var menu = icon.ContextMenuStrip!;
        var old = menu.Items.Cast<Forms.ToolStripItem>().ToList();
        menu.Items.Clear();
        foreach (var item in old) item.Dispose(); // Clear doesn't, and this rebuilds on every open
        menu.Items.Add("Open dashboard", null, (_, _) => ShowWindow());
        menu.Items.Add(vm.AutopilotOn ? "Pause autopilot" : "Resume autopilot", null, (_, _) => vm.ToggleAutopilot());
        menu.Items.Add("Start the next proposal", null, (_, _) => icon.ShowBalloonTip(4000, "MindAttic Orchestrator", vm.DispatchNext(), Forms.ToolTipIcon.Info));
        var waiting = new Forms.ToolStripMenuItem("Waiting on you");
        foreach (var repo in vm.WaitingRepos)
            waiting.DropDownItems.Add(repo, null, (_, _) => { ShowWindow(); vm.SelectRepo(repo); });
        waiting.Enabled = waiting.DropDownItems.Count > 0;
        menu.Items.Add(waiting);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(hud is null ? "Show HUD" : "Hide HUD", null, (_, _) => ToggleHud());
        menu.Items.Add("Exit", null, (_, _) => Application.Current.Shutdown());
    }

    private void ShowWindow()
    {
        window.Show();
        if (window.WindowState == WindowState.Minimized) window.WindowState = WindowState.Normal;
        window.Activate();
    }

    private void ToggleHud()
    {
        if (hud is not null) { hud.Close(); return; }
        hudText = new TextBlock { Foreground = System.Windows.Media.Brushes.White, FontSize = 13, Margin = new Thickness(12, 6, 12, 6), Text = vm.TraySummary };
        hud = new Window
        {
            WindowStyle = WindowStyle.None, ResizeMode = ResizeMode.NoResize, Topmost = true, ShowInTaskbar = false,
            SizeToContent = SizeToContent.WidthAndHeight, Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(0xE6, 0x1E, 0x1F, 0x24)),
            Content = hudText, ToolTip = "Drag to move · double-click for the dashboard",
            Left = SystemParameters.WorkArea.Right - 420, Top = SystemParameters.WorkArea.Top + 12,
        };
        hud.MouseLeftButtonDown += (_, e) => { if (e.ClickCount == 2) ShowWindow(); else hud.DragMove(); };
        hud.Closed += (_, _) => { hud = null; hudText = null; };
        hud.Show();
    }

    public void Dispose()
    {
        timer.Stop();
        hud?.Close();
        icon.Visible = false;
        icon.Dispose();
    }
}
