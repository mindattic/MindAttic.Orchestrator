using System.Collections.Specialized;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using MindAttic.Orchestrator.Wpf.ViewModels;

namespace MindAttic.Orchestrator.Wpf;

public partial class MainWindow : Window
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);

    private readonly MainViewModel model = new();
    private readonly DispatcherTimer timer;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = model;

        CommandBindings.Add(new CommandBinding(WindowCommands.FocusSearch, (_, _) =>
        {
            SearchBox.Focus();
            SearchBox.SelectAll();
        }));
        CommandBindings.Add(new CommandBinding(WindowCommands.Escape, (_, _) => OnEscape()));

        model.Toasts.CollectionChanged += OnToastsChanged;

        timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = PollInterval };
        timer.Tick += async (_, _) => await model.RefreshAsync();
        Loaded += async (_, _) =>
        {
            await model.RefreshAsync();
            timer.Start();
        };
        Closed += (_, _) => timer.Stop();
    }

    private void OnEscape()
    {
        if (model.IsSettingsOpen) model.IsSettingsOpen = false;
        else if (SearchBox.IsKeyboardFocusWithin && model.Search.Length > 0) model.Search = "";
        else if (model.Selected is not null) model.SelectCommand.Execute(model.Selected);
        else model.Search = "";
    }

    /// <summary>A session that needs you should be noticed even when this window is behind others.</summary>
    private void OnToastsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action != NotifyCollectionChangedAction.Add || IsActive) return;
        var info = new FlashInfo
        {
            Size = (uint)Marshal.SizeOf<FlashInfo>(),
            Hwnd = new WindowInteropHelper(this).Handle,
            Flags = FlashTray | FlashTimerNoForeground,
            Count = 3,
        };
        FlashWindowEx(ref info);
    }

    private const uint FlashTray = 0x2, FlashTimerNoForeground = 0xC;

    [StructLayout(LayoutKind.Sequential)]
    private struct FlashInfo
    {
        public uint Size;
        public IntPtr Hwnd;
        public uint Flags;
        public uint Count;
        public uint Timeout;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool FlashWindowEx(ref FlashInfo info);
}
