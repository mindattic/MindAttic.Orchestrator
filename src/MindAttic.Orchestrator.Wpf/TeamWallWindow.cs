using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using MindAttic.Orchestrator.Sessions;
using MindAttic.Orchestrator.Settings;
using MindAttic.Orchestrator.Term;

namespace MindAttic.Orchestrator.Wpf;

/// <summary>
/// The Team Wall (M.ORCH-T936, Phase 3 of docs/spikes/cli-host-viewer.md): every live CLI of this machine and of every
/// teammate's gateway (<c>orchestrator gateway remote add</c>) as a grid of small read-only terminals rendering the real TUI.
/// <list type="bullet">
/// <item>Every <see cref="TeamWall.RefreshEvery"/> it looks at this machine's registry and each gateway's <c>GET /sessions</c>,
/// all at once and off the UI thread, and merges them (<see cref="TeamWall.Merge"/>); the streams themselves are live.</item>
/// <item>It pages rather than scrolls: a renderer is an HWND, which a ScrollViewer can't clip. A page holds what fits, at most
/// <see cref="TeamWall.MaxStreaming"/>, and only the page on screen has terminals at all; the rest have no connection. Minimized,
/// every tile pauses.</item>
/// <item>A gateway that can't be reached, refuses its token or times out shows as offline in the header and never blocks the
/// window; every exception is contained.</item>
/// </list>
/// It is its own window rather than a panel of the dashboard because the dashboard's own terminal HWNDs would draw over a panel.
/// </summary>
internal sealed class TeamWallWindow : Window
{
    private static readonly Serilog.ILogger log = OrchestratorLog.For("TeamWall");
    private static TeamWallWindow? shown;

    /// <summary>How the wall's read-only viewers name themselves in their Hello (the host logs it).</summary>
    internal static readonly string ViewerId = $"wall-{Environment.ProcessId}";

    private readonly JsonStore store = new();
    private readonly Grid tilesHost = new();
    private readonly TextBlock empty = new()
    {
        Foreground = Brushes.Gray, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center, FontSize = 14,
        HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(40),
        Text = "Looking for CLIs…",
    };
    private readonly WrapPanel chips = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock pageText = new() { Foreground = Brushes.Silver, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 8, 0) };
    private readonly Button previous, next;
    private readonly Button focusPark = new() { Width = 0, Height = 0, Opacity = 0, IsTabStop = false };
    private readonly DispatcherTimer timer = new() { Interval = TeamWall.RefreshEvery };
    private readonly CancellationTokenSource lifetime = new();
    private readonly Dictionary<string, TeamWallTile> tiles = new(StringComparer.Ordinal);
    // Concurrent (M.ORCH-T1014): a tile's reconnect reads it from a thread-pool thread (ConnectFor) while SyncClients and
    // Shutdown change it on the UI thread. A client disposed under a reconnect fails that attach, which the relay contains.
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, GatewayClient> clients = new(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyList<WallSession> all = [];
    private IReadOnlyList<WallGateway> gateways = [];
    private volatile HashSet<string> listed = new(StringComparer.Ordinal);
    private int page;
    private bool refreshing, closed;

    private TeamWallWindow()
    {
        Title = "Team Wall";
        Width = 1500;
        Height = 920;
        Background = new SolidColorBrush(Color.FromRgb(0x14, 0x14, 0x14));
        try { Icon = new System.Windows.Media.Imaging.BitmapImage(new Uri("pack://application:,,,/logo.ico")); }
        catch (Exception ex) { log.Warning(ex, "The Team Wall's icon couldn't be loaded"); }

        previous = Bar("‹", "Previous page");
        previous.Click += (_, _) => { page--; Render(); };
        next = Bar("›", "Next page");
        next.Click += (_, _) => { page++; Render(); };
        var refresh = Bar("Refresh", "Look at every gateway now");
        refresh.Click += (_, _) => _ = RefreshAsync();

        var right = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        right.Children.Add(previous);
        right.Children.Add(pageText);
        right.Children.Add(next);
        right.Children.Add(refresh);
        right.Children.Add(focusPark);
        var heading = new TextBlock { Text = "Team Wall", Foreground = Brushes.White, FontSize = 16, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 16, 0) };
        var bar = new DockPanel { Background = new SolidColorBrush(Color.FromRgb(0x1F, 0x1F, 0x1F)), LastChildFill = true, MinHeight = 40 };
        DockPanel.SetDock(heading, Dock.Left);
        DockPanel.SetDock(right, Dock.Right);
        bar.Children.Add(heading);
        bar.Children.Add(right);
        bar.Children.Add(chips);

        var body = new Grid { Margin = new Thickness(6) };
        body.Children.Add(tilesHost);
        body.Children.Add(empty);
        var root = new DockPanel();
        DockPanel.SetDock(bar, Dock.Top);
        root.Children.Add(bar);
        root.Children.Add(body);
        Content = root;

        tilesHost.SizeChanged += (_, _) => Render();
        StateChanged += (_, _) => Render();
        IsVisibleChanged += (_, _) => Render();
        Loaded += (_, _) => _ = RefreshAsync();
        timer.Tick += (_, _) => _ = RefreshAsync();
        timer.Start();
        Closed += (_, _) => Shutdown();
    }

    /// <summary>Opens the wall, or brings it forward when it's open already. Never throws.</summary>
    public static void Open()
    {
        try
        {
            if (shown is { } open)
            {
                DashboardWindow.Restore(open);
                return;
            }
            var wall = new TeamWallWindow();
            shown = wall;
            wall.Closed += (_, _) => { if (ReferenceEquals(shown, wall)) shown = null; };
            wall.Show();
        }
        catch (Exception ex) { log.Error(ex, "Opening the Team Wall failed; contained"); }
    }

    private static Button Bar(string text, string tip) =>
        TeamWallTile.FlatButton(text, tip, new Thickness(4, 6, 4, 6), new Thickness(10, 2, 10, 2), 12);

    /// <summary>Takes keyboard focus off a tile's renderer (a click on it opens the pop-out; focus left there would open it
    /// again when the window is next activated).</summary>
    internal void ParkFocus()
    {
        try { focusPark.Focus(); }
        catch (Exception) { }
    }

    /// <summary>How a tile opens its stream: this machine's own session's pipe, or the teammate's gateway, watch only.</summary>
    internal Func<CancellationToken, Task<Stream>> ConnectFor(WallSession session)
    {
        if (session.IsLocal)
        {
            var pipe = session.Pipe ?? TermProtocol.PipeName(session.Id);
            return ct => RelayConnection.ConnectPipeAsync(pipe, ct);
        }
        var gateway = session.Gateway;
        var id = session.Id;
        return ct => clients.TryGetValue(gateway, out var client)
            ? client.ConnectAsync(id, watch: true, ct)
            : Task.FromException<Stream>(new InvalidOperationException($"Gateway {gateway} is no longer configured."));
    }

    /// <summary>Whether a dropped tile should attach again: its host still runs (this machine's), or its gateway still listed it
    /// at the last look (a teammate's). Called off the UI thread.</summary>
    internal Func<bool> AliveFor(WallSession session)
    {
        if (session.IsLocal)
        {
            var id = session.Id;
            return () => !closed && TermSessions.Get(id) is not null;
        }
        var key = session.Key;
        return () => !closed && listed.Contains(key);
    }

    /// <summary>Looks at every source at once and merges what they say. One look at a time; never throws.</summary>
    private async Task RefreshAsync()
    {
        if (refreshing || closed) return;
        refreshing = true;
        try
        {
            var ct = lifetime.Token;
            var remotes = await Task.Run(() => store.LoadSettings().Gateway.Remotes, ct).ConfigureAwait(true);
            SyncClients(remotes);
            var local = Task.Run(LocalSource, ct);
            var looks = clients.Values.Select(c => RemoteSourceAsync(c, ct)).ToList();
            var sources = new List<WallSource> { await local.ConfigureAwait(true) };
            sources.AddRange(await Task.WhenAll(looks).ConfigureAwait(true));
            if (closed) return;
            // Settings order for the remotes, after this machine's own.
            var order = remotes.Select((r, i) => (r.Name, i)).ToDictionary(x => x.Name, x => x.i, StringComparer.OrdinalIgnoreCase);
            sources = [.. sources.OrderBy(s => s.Gateway == TeamWall.LocalGateway ? -1 : order.GetValueOrDefault(s.Gateway, int.MaxValue))];
            var layout = TeamWall.Merge(all, sources);
            all = layout.Tiles;
            gateways = layout.Gateways;
            listed = new HashSet<string>(all.Select(t => t.Key), StringComparer.Ordinal);
            Render();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { log.Error(ex, "Refreshing the Team Wall failed; contained"); }
        finally { refreshing = false; }
    }

    private static WallSource LocalSource()
    {
        try
        {
            var sessions = TermSessions.Enumerate().Select(r => TeamWall.FromLocal(r, "You")).ToList();
            return new WallSource(TeamWall.LocalGateway, "You", true, null, sessions);
        }
        catch (Exception ex)
        {
            log.Error(ex, "Reading this machine's sessions failed; contained");
            return new WallSource(TeamWall.LocalGateway, "You", false, "its sessions couldn't be read", []);
        }
    }

    private static async Task<WallSource> RemoteSourceAsync(GatewayClient client, CancellationToken ct)
    {
        var remote = client.Remote;
        try
        {
            var listing = await client.ListAsync(ct).ConfigureAwait(false);
            return listing.Online
                ? new WallSource(remote.Name, remote.Who, true, null, listing.Sessions.Select(s => TeamWall.FromRemote(remote, s)).ToList())
                : new WallSource(remote.Name, remote.Who, false, listing.Problem, []);
        }
        catch (Exception ex)
        {
            log.Error(ex, "Looking at gateway {Name:l} failed; contained", remote.Name);
            return new WallSource(remote.Name, remote.Who, false, "failed", []);
        }
    }

    /// <summary>One client per configured remote; a remote that changed (URL, pin, token) gets a new one.</summary>
    private void SyncClients(IReadOnlyList<GatewayRemote> remotes)
    {
        var wanted = remotes.Where(r => GatewayRemotes.IsValidName(r.Name))
            .GroupBy(r => r.Name, StringComparer.OrdinalIgnoreCase).Select(g => g.Last()).ToList();
        foreach (var (name, client) in clients.ToArray())
            if (wanted.FirstOrDefault(r => string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase)) is not { } r || r != client.Remote)
            {
                clients.TryRemove(new KeyValuePair<string, GatewayClient>(name, client));
                try { client.Dispose(); } catch (Exception) { }
            }
        foreach (var r in wanted)
            if (!clients.ContainsKey(r.Name))
                // Its streams send the gateway the fresh token the member is given since, before theirs expires (M.ORCH-T1041).
                try { clients[r.Name] = new GatewayClient(r, () => GatewayRemotes.Find(store.LoadSettings().Gateway, r.Name)); }
                catch (Exception ex) { log.Error(ex, "Setting up gateway {Name:l} failed; contained", r.Name); }
    }

    /// <summary>Lays out the page on screen: its tiles in the grid, streaming up to the cap; every tile off it disposed.</summary>
    private void Render()
    {
        if (closed || !IsLoaded) return;
        try
        {
            var width = tilesHost.ActualWidth;
            var height = tilesHost.ActualHeight;
            var pageSize = TeamWall.PageSize(width, height);
            page = TeamWall.ClampPage(page, all.Count, pageSize);
            var onScreen = TeamWall.Page(all, page, pageSize);
            var visible = IsVisible && WindowState != WindowState.Minimized;
            var streaming = TeamWall.Streaming(onScreen, TeamWall.MaxStreaming, visible);

            var keep = onScreen.Select(s => s.Key).ToHashSet(StringComparer.Ordinal);
            foreach (var key in tiles.Keys.Where(k => !keep.Contains(k)).ToList())
            {
                var gone = tiles[key];
                tiles.Remove(key);
                tilesHost.Children.Remove(gone);
                gone.Dispose();
            }

            var (columns, rows) = TeamWall.Shape(onScreen.Count, width);
            Resize(tilesHost.ColumnDefinitions, columns, () => new ColumnDefinition());
            Resize(tilesHost.RowDefinitions, rows, () => new RowDefinition());
            for (var i = 0; i < onScreen.Count; i++)
            {
                var s = onScreen[i];
                if (!tiles.TryGetValue(s.Key, out var tile))
                {
                    tile = new TeamWallTile(s, this);
                    tiles[s.Key] = tile;
                    tilesHost.Children.Add(tile);
                }
                else tile.Update(s);
                // Moving a tile to another cell keeps it in the same parent: its renderer's HWND is never torn down.
                Grid.SetColumn(tile, i % columns);
                Grid.SetRow(tile, i / columns);
                if (streaming.Contains(s.Key)) tile.Resume();
                else tile.Pause(!visible ? "Paused while the wall is minimized."
                    : !s.Streamable ? "Its terminal host serves one viewer at a time (protocol 1), so watching it would take it from its owner's tab."
                    : "Paused: more tiles than stream at once.");
            }

            var pages = TeamWall.PageCount(all.Count, pageSize);
            pageText.Text = $"{page + 1} / {pages}";
            System.Windows.Automation.AutomationProperties.SetName(pageText, $"Page {page + 1} of {pages}");
            previous.IsEnabled = page > 0;
            next.IsEnabled = page < pages - 1;
            empty.Visibility = all.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            empty.Text = gateways.Count <= 1 && !clients.Any()
                ? "No live CLIs here yet, and no teammates' gateways.\nAdd one with: orchestrator gateway remote add <name> <url> --pin <sha256>"
                : "No live CLIs on any gateway right now.";
            ShowChips();
        }
        catch (Exception ex) { log.Error(ex, "Laying out the Team Wall failed; contained"); }
    }

    private static void Resize<T>(IList<T> definitions, int count, Func<T> make)
    {
        while (definitions.Count > count) definitions.RemoveAt(definitions.Count - 1);
        while (definitions.Count < count) definitions.Add(make());
    }

    /// <summary>The header's line per source: green with how many CLIs, or grey "offline" with why in its tooltip.</summary>
    private void ShowChips()
    {
        chips.Children.Clear();
        foreach (var g in gateways)
        {
            var dot = new Ellipse
            {
                Width = 8, Height = 8, Margin = new Thickness(0, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center,
                Fill = new SolidColorBrush(g.Online ? Color.FromRgb(0x4C, 0xC3, 0x6B) : Color.FromRgb(0x8A, 0x8A, 0x8A)),
            };
            var label = g.Gateway == TeamWall.LocalGateway ? "This machine" : g.Person;
            var text = new TextBlock
            {
                Text = g.Online ? $"{label}: {g.Sessions}" : $"{label}: offline",
                Foreground = g.Online ? Brushes.Gainsboro : Brushes.Gray, VerticalAlignment = VerticalAlignment.Center,
            };
            var chip = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 18, 0) };
            chip.Children.Add(dot);
            chip.Children.Add(text);
            chip.ToolTip = g.Online ? $"{label} ({(g.Gateway == TeamWall.LocalGateway ? "this machine" : "gateway " + g.Gateway)}): {g.Sessions} live CLI(s)"
                : $"{label} (gateway {g.Gateway}) is offline: {g.Problem}";
            chips.Children.Add(chip);
        }
    }

    private void Shutdown()
    {
        if (closed) return;
        closed = true;
        timer.Stop();
        try { lifetime.Cancel(); } catch (Exception) { }
        foreach (var tile in tiles.Values)
            try { tile.Dispose(); } catch (Exception ex) { log.Error(ex, "Closing a tile failed; contained"); }
        tiles.Clear();
        foreach (var name in clients.Keys)
            if (clients.TryRemove(name, out var client))
                try { client.Dispose(); } catch (Exception) { }
    }
}
