using System.ComponentModel;
using System.Diagnostics;
using MindAttic.Orchestrator.Settings;
using MindAttic.Orchestrator.Term;

namespace MindAttic.Orchestrator.Sessions;

/// <summary>One live CLI on the team wall (M.ORCH-T936): this machine's own (<see cref="TeamWall.LocalGateway"/>) or one a
/// teammate's gateway lists.</summary>
public sealed record WallSession
{
    /// <summary><c>gateway/id</c>: unique across the wall.</summary>
    public required string Key { get; init; }

    /// <summary>The remote's name, or <see cref="TeamWall.LocalGateway"/>.</summary>
    public required string Gateway { get; init; }

    /// <summary>Whose CLI it is, as the tile says.</summary>
    public required string Person { get; init; }

    public required string Id { get; init; }
    public string? Title { get; init; }
    public string? Login { get; init; }
    public string? Repo { get; init; }
    public string State { get; init; } = TermSessionStates.Running;
    public int? ExitCode { get; init; }
    public int Protocol { get; init; }
    public int Cols { get; init; }
    public int Rows { get; init; }
    public DateTime StartedUtc { get; init; }

    /// <summary>Whether its pop-out may be interactive: a remote's grant allows it (<c>access: interact</c>), or it is this
    /// machine's own.</summary>
    public bool CanInteract { get; init; }

    /// <summary>The host's pipe, for this machine's own sessions only.</summary>
    public string? Pipe { get; init; }

    public bool IsLocal => Gateway == TeamWall.LocalGateway;

    /// <summary>A tile can stream it without disturbing anyone: its host serves several viewers (protocol 2, M.ORCH-T930). A
    /// protocol 1 host serves one, so a tile would take the session from the owner's own tab.</summary>
    public bool Streamable => Protocol >= TermProtocol.MultiViewer;
}

/// <summary>What one look at a source (this machine, or a remote gateway) found.</summary>
public sealed record WallSource(string Gateway, string Person, bool Online, string? Problem, IReadOnlyList<WallSession> Sessions);

/// <summary>A source's line in the wall's header: online with how many CLIs, or offline with why.</summary>
public sealed record WallGateway(string Gateway, string Person, bool Online, string? Problem, int Sessions);

/// <summary>The wall after a merge: its tiles in order, and every source's state.</summary>
public sealed record WallLayout(IReadOnlyList<WallSession> Tiles, IReadOnlyList<WallGateway> Gateways);

/// <summary>
/// The team wall's pure logic (M.ORCH-T936): merging each look at the sources into a stable list of tiles, paging them to what
/// fits on screen, and choosing which tiles stream. The dashboard's TeamWallWindow does the I/O and the drawing.
/// </summary>
public static class TeamWall
{
    /// <summary>The gateway name of this machine's own sessions (no remote can be called this: names have no parentheses).</summary>
    public const string LocalGateway = "(local)";

    /// <summary>The most tiles that stream at once, however large the window.</summary>
    public const int MaxStreaming = 12;

    /// <summary>How often the wall looks at every source's session list (the streams themselves are live).</summary>
    public static readonly TimeSpan RefreshEvery = TimeSpan.FromSeconds(10);

    /// <summary>The smallest a tile is drawn, in device-independent pixels: the grid fits as many as these allow.</summary>
    public const double MinTileWidth = 420, MinTileHeight = 260;

    public static string KeyOf(string gateway, string id) => gateway + "/" + id;

    /// <summary>One of this machine's hosted sessions as a tile.</summary>
    public static WallSession FromLocal(TermSessionRecord r, string person) => new()
    {
        Key = KeyOf(LocalGateway, r.Id),
        Gateway = LocalGateway,
        Person = person,
        Id = r.Id,
        Title = r.Title,
        Login = r.Login,
        Repo = r.Repo,
        State = r.State,
        ExitCode = r.ExitCode,
        Protocol = r.Protocol,
        Cols = r.Cols,
        Rows = r.Rows,
        StartedUtc = r.StartedUtc,
        CanInteract = true,
        Pipe = r.Pipe,
    };

    /// <summary>A session a remote gateway listed, as a tile.</summary>
    public static WallSession FromRemote(GatewayRemote remote, RemoteSessionInfo s) => new()
    {
        Key = KeyOf(remote.Name, s.Id),
        Gateway = remote.Name,
        Person = remote.Who,
        Id = s.Id,
        Title = s.Title,
        Login = s.Login,
        Repo = s.Repo,
        State = string.IsNullOrWhiteSpace(s.State) ? TermSessionStates.Running : s.State,
        ExitCode = s.ExitCode,
        Protocol = s.Protocol,
        Cols = s.Cols,
        Rows = s.Rows,
        StartedUtc = s.StartedUtc,
        CanInteract = string.Equals(s.Access, "interact", StringComparison.OrdinalIgnoreCase),
    };

    /// <summary>
    /// Merges a new look at every source into the wall:
    /// <list type="bullet">
    /// <item>tiles are grouped by source, in <paramref name="sources"/>' order (this machine first, then the remotes as
    /// configured);</item>
    /// <item>within a source, a tile already on the wall keeps its place, so the grid doesn't reshuffle on every look, and a new
    /// one goes after them, oldest first; each tile takes the source's latest title, state and size;</item>
    /// <item>an offline source contributes no tiles (its streams can't run), only its header line with why; a source no longer
    /// configured disappears; a session a source lists twice counts once.</item>
    /// </list>
    /// </summary>
    public static WallLayout Merge(IReadOnlyList<WallSession> previous, IReadOnlyList<WallSource> sources)
    {
        var place = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < previous.Count; i++) place.TryAdd(previous[i].Key, i);
        var tiles = new List<WallSession>();
        var gateways = new List<WallGateway>();
        var seenSources = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var source in sources)
        {
            if (!seenSources.Add(source.Gateway)) continue;
            if (!source.Online)
            {
                gateways.Add(new WallGateway(source.Gateway, source.Person, false, source.Problem, 0));
                continue;
            }
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var mine = source.Sessions
                .Where(s => !string.IsNullOrWhiteSpace(s.Id))
                .Select(s => s.Key == KeyOf(source.Gateway, s.Id) ? s : s with { Key = KeyOf(source.Gateway, s.Id), Gateway = source.Gateway })
                .Where(s => seen.Add(s.Key))
                .OrderBy(s => place.TryGetValue(s.Key, out var at) ? at : int.MaxValue)
                .ThenBy(s => s.StartedUtc)
                .ThenBy(s => s.Id, StringComparer.Ordinal)
                .ToList();
            tiles.AddRange(mine);
            gateways.Add(new WallGateway(source.Gateway, source.Person, true, null, mine.Count));
        }
        return new WallLayout(tiles, gateways);
    }

    /// <summary>How many tiles one page holds in a <paramref name="width"/> by <paramref name="height"/> area: as many
    /// <see cref="MinTileWidth"/> by <see cref="MinTileHeight"/> cells as fit, at least one, at most <paramref name="cap"/>.</summary>
    public static int PageSize(double width, double height, int cap = MaxStreaming, double minWidth = MinTileWidth, double minHeight = MinTileHeight)
    {
        if (cap < 1) cap = 1;
        if (double.IsNaN(width) || double.IsNaN(height) || width <= 0 || height <= 0) return 1;
        var cols = Math.Max(1, (int)Math.Floor(width / Math.Max(1, minWidth)));
        var rows = Math.Max(1, (int)Math.Floor(height / Math.Max(1, minHeight)));
        return (int)Math.Clamp((long)cols * rows, 1, cap);
    }

    /// <summary>The grid for <paramref name="count"/> tiles in that area: columns as the width allows (at most the count), and
    /// enough rows.</summary>
    public static (int Columns, int Rows) Shape(int count, double width, double minWidth = MinTileWidth)
    {
        if (count <= 0) return (1, 1);
        var cols = double.IsNaN(width) || width <= 0 ? 1 : Math.Max(1, (int)Math.Floor(width / Math.Max(1, minWidth)));
        cols = Math.Min(cols, count);
        return (cols, (count + cols - 1) / cols);
    }

    public static int PageCount(int count, int pageSize) => count <= 0 ? 1 : (count + Math.Max(1, pageSize) - 1) / Math.Max(1, pageSize);

    /// <summary><paramref name="page"/> (0-based) kept within the pages there are.</summary>
    public static int ClampPage(int page, int count, int pageSize) => Math.Clamp(page, 0, PageCount(count, pageSize) - 1);

    /// <summary>The tiles on page <paramref name="page"/>: the only ones on screen, so the only ones with a terminal at all.</summary>
    public static IReadOnlyList<WallSession> Page(IReadOnlyList<WallSession> tiles, int page, int pageSize)
    {
        pageSize = Math.Max(1, pageSize);
        page = ClampPage(page, tiles.Count, pageSize);
        return tiles.Skip(page * pageSize).Take(pageSize).ToList();
    }

    /// <summary>
    /// Which tiles stream: those on screen whose host can be watched beside its owner (<see cref="WallSession.Streamable"/>), in
    /// order, at most <paramref name="cap"/>; none while the wall isn't shown (minimized or hidden). Every other tile is paused:
    /// it has no connection, and shows why.
    /// </summary>
    public static IReadOnlySet<string> Streaming(IReadOnlyList<WallSession> onScreen, int cap = MaxStreaming, bool shown = true)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        if (!shown || cap <= 0) return keys;
        foreach (var tile in onScreen)
        {
            if (keys.Count >= cap) break;
            if (tile.Streamable) keys.Add(tile.Key);
        }
        return keys;
    }

    /// <summary>
    /// Opens a tile larger in a TermViewer window (T931's pop-out, extended to remote gateways): this machine's own session as
    /// <c>--session &lt;id&gt; --stay</c> (with <c>--watch</c> unless <paramref name="interact"/>), a remote one as
    /// <c>--remote &lt;name&gt; --session &lt;id&gt;</c> (with <c>--interact</c> only when asked and the grant allows it). The
    /// token never goes on the command line: the viewer reads the remote from the settings. Never throws; false when no
    /// TermViewer is installed or it couldn't start, or when the session's host serves one viewer (<see cref="CanPopOut"/>).
    /// </summary>
    public static bool PopOut(WallSession session, bool interact)
    {
        if (!CanPopOut(session)) return false;
        try
        {
            if (FallbackViewers.DefaultExe() is not { } exe) return false;
            var psi = new ProcessStartInfo(exe) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(exe) ?? "" };
            foreach (var arg in PopOutArguments(session, interact)) psi.ArgumentList.Add(arg);
            using var process = Process.Start(psi);
            return process is not null;
        }
        catch (Exception ex) when (ex is Win32Exception or IOException or InvalidOperationException or UnauthorizedAccessException)
        {
            OrchestratorLog.For("TeamWall").Error(ex, "Popping out {Key:l} failed; contained", session.Key);
            return false;
        }
    }

    /// <summary>Whether a tile can be opened larger (M.ORCH-T1032): only on a host that serves several viewers
    /// (<see cref="WallSession.Streamable"/>), as the dashboard's own pop-out requires. On a protocol 1 host the pop-out and the
    /// owner's tab would take the single-viewer session from each other in a reconnect loop.</summary>
    public static bool CanPopOut(WallSession session) => session.Streamable;

    /// <summary>The TermViewer arguments <see cref="PopOut"/> uses.</summary>
    public static IReadOnlyList<string> PopOutArguments(WallSession session, bool interact)
    {
        var interactive = interact && session.CanInteract;
        if (session.IsLocal)
            return interactive ? ["--session", session.Id, "--stay"] : ["--session", session.Id, "--stay", "--watch"];
        return interactive ? ["--remote", session.Gateway, "--session", session.Id, "--interact"] : ["--remote", session.Gateway, "--session", session.Id];
    }
}
