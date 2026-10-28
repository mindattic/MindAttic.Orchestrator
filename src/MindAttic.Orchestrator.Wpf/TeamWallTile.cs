using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using MindAttic.Orchestrator.Sessions;
using MindAttic.Orchestrator.Term;

namespace MindAttic.Orchestrator.Wpf;

/// <summary>
/// One CLI on the Team Wall (M.ORCH-T936): a header with whose it is, its title and its state, over a small read-only
/// <see cref="RelayTerminal"/> rendering the real TUI. The renderer is an HWND, which WPF can't scale, so the tile shrinks the
/// font until the session's whole grid fits (the tile is a watcher, so it letterboxes to the session's size and never sets it).
/// Clicking the header, or the terminal, opens the CLI larger in a watch-only TermViewer window; Interact shows only where the
/// grant allows it. A paused tile has no connection at all. Nothing here throws to the wall.
/// </summary>
internal sealed class TeamWallTile : Border, IDisposable
{
    private static readonly Serilog.ILogger log = OrchestratorLog.For("TeamWall");
    private static readonly Brush HeaderBrush = Frozen(Color.FromRgb(0x24, 0x24, 0x24));
    private static readonly Brush EdgeBrush = Frozen(Color.FromRgb(0x3A, 0x3A, 0x3A));
    private static readonly Brush LiveBrush = Frozen(Color.FromRgb(0x4C, 0xC3, 0x6B));
    private static readonly Brush WaitBrush = Frozen(Color.FromRgb(0xE0, 0xB0, 0x40));
    private static readonly Brush DownBrush = Frozen(Color.FromRgb(0x9A, 0x9A, 0x9A));

    private readonly TeamWallWindow wall;
    private readonly Border pill = new() { CornerRadius = new CornerRadius(3), Padding = new Thickness(6, 0, 6, 0), Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock person = new() { Foreground = Brushes.White, FontWeight = FontWeights.SemiBold, FontSize = 11 };
    private readonly TextBlock title = new() { Foreground = Brushes.Gainsboro, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center, FontSize = 12 };
    private readonly Ellipse dot = new() { Width = 8, Height = 8, Margin = new Thickness(8, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock state = new() { Foreground = Brushes.Silver, VerticalAlignment = VerticalAlignment.Center, FontSize = 11 };
    private readonly Button watch, interact;
    private readonly Border body = new() { Background = Brushes.Black, ClipToBounds = true };
    private readonly TextBlock placeholder = new() { Foreground = Brushes.Gray, TextWrapping = TextWrapping.Wrap, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(16), TextAlignment = TextAlignment.Center };
    private RelayTerminal? relay;
    private string link = "";
    private ushort cols, rows;
    private int? exitCode;
    private bool disposed;

    public TeamWallTile(WallSession session, TeamWallWindow wall)
    {
        this.wall = wall;
        Session = session;
        BorderBrush = EdgeBrush;
        BorderThickness = new Thickness(1);
        Margin = new Thickness(4);
        Background = Brushes.Black;
        SnapsToDevicePixels = true;

        pill.Child = person;
        watch = SmallButton("Watch", "Open it larger, watch only");
        watch.Click += (_, _) => PopOut(interactive: false);
        interact = SmallButton("Interact", "Open it larger to type into it (the grant allows it)");
        interact.Click += (_, _) => PopOut(interactive: true);

        var right = new StackPanel { Orientation = Orientation.Horizontal };
        right.Children.Add(dot);
        right.Children.Add(state);
        right.Children.Add(watch);
        right.Children.Add(interact);
        var header = new DockPanel { Background = HeaderBrush, LastChildFill = true, Height = 28, Cursor = Cursors.Hand };
        DockPanel.SetDock(pill, Dock.Left);
        DockPanel.SetDock(right, Dock.Right);
        header.Children.Add(pill);
        header.Children.Add(right);
        header.Children.Add(title);
        header.Margin = new Thickness(0);
        header.MouseLeftButtonUp += (_, e) =>
        {
            if (e.OriginalSource is DependencyObject d && FindButton(d)) return;
            PopOut(interactive: false);
        };

        var root = new DockPanel();
        DockPanel.SetDock(header, Dock.Top);
        root.Children.Add(header);
        root.Children.Add(body);
        Child = root;
        body.Child = placeholder;
        body.SizeChanged += (_, _) => Fit();
        Update(session);
    }

    public WallSession Session { get; private set; }

    /// <summary>Whether it has a connection (streaming) now.</summary>
    public bool Streaming => relay is not null;

    private static Brush Frozen(Color c)
    {
        var b = new SolidColorBrush(c);
        b.Freeze();
        return b;
    }

    private static Button SmallButton(string text, string tip) =>
        FlatButton(text, tip, new Thickness(6, 3, 0, 3), new Thickness(8, 0, 8, 0), 11);

    private static ControlTemplate? flatTemplate;

    /// <summary>A flat dark button for the wall's own dark chrome (the default template paints a light one).</summary>
    internal static Button FlatButton(string text, string tip, Thickness margin, Thickness padding, double fontSize)
    {
        if (flatTemplate is null)
        {
            var border = new FrameworkElementFactory(typeof(Border), "Chrome");
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(3));
            border.SetValue(Border.BorderThicknessProperty, new Thickness(1));
            border.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding(nameof(Background)) { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
            border.SetBinding(Border.BorderBrushProperty, new System.Windows.Data.Binding(nameof(BorderBrush)) { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
            border.SetBinding(Border.PaddingProperty, new System.Windows.Data.Binding(nameof(Padding)) { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
            var content = new FrameworkElementFactory(typeof(ContentPresenter));
            content.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            content.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            border.AppendChild(content);
            var template = new ControlTemplate(typeof(Button)) { VisualTree = border };
            var hover = new Trigger { Property = IsMouseOverProperty, Value = true };
            hover.Setters.Add(new Setter(Border.BackgroundProperty, Frozen(Color.FromRgb(0x44, 0x44, 0x44)), "Chrome"));
            template.Triggers.Add(hover);
            var off = new Trigger { Property = IsEnabledProperty, Value = false };
            off.Setters.Add(new Setter(OpacityProperty, 0.4));
            template.Triggers.Add(off);
            template.Seal();
            flatTemplate = template;
        }
        // Focusable, with a name and help text (M.ORCH-T1021): the wall's buttons are its only keyboard way to page, refresh
        // and open a tile, and a glyph like the page arrows reads as nothing to a screen reader.
        var button = new Button
        {
            Content = text, ToolTip = tip, Margin = margin, Padding = padding, FontSize = fontSize, Cursor = Cursors.Hand,
            Template = flatTemplate, Background = Frozen(Color.FromRgb(0x30, 0x30, 0x30)), Foreground = Brushes.Gainsboro,
            BorderBrush = Frozen(Color.FromRgb(0x4A, 0x4A, 0x4A)),
        };
        System.Windows.Automation.AutomationProperties.SetName(button, text.Any(char.IsLetter) ? text : tip);
        System.Windows.Automation.AutomationProperties.SetHelpText(button, tip);
        return button;
    }

    private static bool FindButton(DependencyObject d)
    {
        for (var at = d; at is not null; at = at is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(at) : LogicalTreeHelper.GetParent(at))
            if (at is Button) return true;
        return false;
    }

    /// <summary>A colour per person, so a glance tells whose tiles are whose.</summary>
    internal static Color PersonColor(string who)
    {
        Color[] palette =
        [
            Color.FromRgb(0x2E, 0x6F, 0xD8), Color.FromRgb(0x8E, 0x44, 0xAD), Color.FromRgb(0x16, 0x8F, 0x7A), Color.FromRgb(0xC0, 0x5A, 0x1C),
            Color.FromRgb(0xB0, 0x2E, 0x5C), Color.FromRgb(0x5D, 0x7A, 0x1F), Color.FromRgb(0x6B, 0x5B, 0xD6), Color.FromRgb(0x9C, 0x6B, 0x22),
        ];
        var hash = 17;
        foreach (var c in who.ToLowerInvariant()) hash = unchecked(hash * 31 + c);
        return palette[(int)((uint)hash % (uint)palette.Length)];
    }

    /// <summary>Takes the latest look at the session: its title, state and whether the grant allows interacting.</summary>
    public void Update(WallSession session)
    {
        Session = session;
        person.Text = session.Person;
        pill.Background = new SolidColorBrush(PersonColor(session.Person));
        var text = string.IsNullOrWhiteSpace(session.Title) ? session.Repo ?? session.Id : session.Title!.Trim();
        title.Text = text;
        ToolTip = $"{session.Person}: {text}" + (session.Login is { Length: > 0 } l ? $" ({l})" : "") + (session.IsLocal ? "" : $"\nGateway {session.Gateway}");
        interact.Visibility = session.CanInteract ? Visibility.Visible : Visibility.Collapsed;
        // Named per tile (M.ORCH-T1021): a screen reader hears whose CLI each tile and its buttons are, not a dozen "Watch"es.
        System.Windows.Automation.AutomationProperties.SetName(this, $"{session.Person}: {text}");
        System.Windows.Automation.AutomationProperties.SetName(watch, $"Watch {session.Person}: {text}");
        System.Windows.Automation.AutomationProperties.SetName(interact, $"Interact with {session.Person}: {text}");
        if (cols == 0 && session.Cols > 0) { cols = (ushort)session.Cols; rows = (ushort)Math.Max(1, session.Rows); Fit(); }
        ShowState();
    }

    private void ShowState()
    {
        string text;
        Brush brush;
        if (exitCode is not null || Session.State == TermSessionStates.Exited)
        {
            text = (exitCode ?? Session.ExitCode) is { } code ? $"exited {code}" : "exited";
            brush = DownBrush;
        }
        else if (relay is null) { text = "paused"; brush = DownBrush; }
        else if (link == "live") { text = "live"; brush = LiveBrush; }
        else if (link.StartsWith("ended", StringComparison.Ordinal)) { text = "ended"; brush = DownBrush; }
        else { text = link.Length > 0 ? link : "connecting"; brush = WaitBrush; }
        state.Text = text;
        dot.Fill = brush;
    }

    /// <summary>Starts streaming, if it isn't: a read-only connection through the wall (the pipe for this machine's own CLIs,
    /// the teammate's gateway for theirs).</summary>
    public void Resume()
    {
        if (disposed || relay is not null) return;
        try
        {
            var r = new RelayTerminal(TerminalThemes.Campbell) { FontPoints = 6, Focusable = false };
            r.SessionSizeChanged += (c, rw) => { cols = c; rows = rw; Fit(); };
            r.Exited += code => { exitCode = code; ShowState(); };
            r.Detached += why => { link = "ended: " + why; ShowState(); };
            r.ReconnectingChanged += reconnecting => { link = reconnecting ? "reconnecting" : "live"; ShowState(); };
            // The renderer is an HWND: a click on it never reaches WPF, but it takes focus, which opens the pop-out.
            r.Terminal.GotFocus += (_, _) =>
            {
                if (Mouse.LeftButton != MouseButtonState.Pressed) return;
                Dispatcher.BeginInvoke(() =>
                {
                    wall.ParkFocus();
                    PopOut(interactive: false);
                });
            };
            relay = r;
            link = "connecting";
            body.Child = r;
            r.AttachLink(Session.Key, wall.ConnectFor(Session), Session.Protocol, wall.AliveFor(Session), interactive: false, viewer: TeamWallWindow.ViewerId);
            Fit();
        }
        catch (Exception ex)
        {
            log.Error(ex, "Streaming tile {Key:l} failed; contained", Session.Key);
            Pause("couldn't connect: " + ex.Message);
        }
        ShowState();
    }

    /// <summary>Stops streaming (drops the connection; the CLI is untouched) and says why in its place.</summary>
    public void Pause(string why)
    {
        placeholder.Text = why;
        if (relay is { } r)
        {
            relay = null;
            link = "";
            try { r.Dispose(); }
            catch (Exception ex) { log.Error(ex, "Pausing tile {Key:l} failed; contained", Session.Key); }
        }
        if (!ReferenceEquals(body.Child, placeholder)) body.Child = placeholder;
        ShowState();
    }

    /// <summary>The largest font (in points) at which <paramref name="cols"/> by <paramref name="rows"/> cells fit in
    /// <paramref name="width"/> by <paramref name="height"/> DIPs: Cascadia Code's cell is about 0.6 by 1.3 of its size, and a
    /// point is 4/3 of a DIP. Between 3 and 12.</summary>
    internal static short FitPoints(double width, double height, int cols, int rows)
    {
        if (cols <= 0 || rows <= 0 || width <= 0 || height <= 0 || double.IsNaN(width) || double.IsNaN(height)) return 6;
        var px = Math.Min(width / (cols * 0.6), height / (rows * 1.3));
        return (short)Math.Clamp(Math.Floor(px * 0.75), 3, 12);
    }

    private void Fit()
    {
        try
        {
            if (relay is not { } r || cols == 0 || rows == 0) return;
            r.FontPoints = FitPoints(body.ActualWidth, body.ActualHeight, cols, rows);
        }
        catch (Exception ex) { log.Error(ex, "Fitting tile {Key:l} failed; contained", Session.Key); }
    }

    private void PopOut(bool interactive)
    {
        try
        {
            if (!TeamWall.CanPopOut(Session))
            {
                state.Text = "one viewer at a time: can't open larger";
                dot.Fill = DownBrush;
                return;
            }
            if (TeamWall.PopOut(Session, interactive)) return;
            state.Text = "no TermViewer to open it in";
            dot.Fill = WaitBrush;
        }
        catch (Exception ex) { log.Error(ex, "Opening {Key:l} larger failed; contained", Session.Key); }
    }

    public void Dispose()
    {
        if (disposed) return;
        Pause("");
        disposed = true;
    }
}
