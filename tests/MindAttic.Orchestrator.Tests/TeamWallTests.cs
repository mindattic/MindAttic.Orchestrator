using MindAttic.Orchestrator.Sessions;
using MindAttic.Orchestrator.Settings;
using MindAttic.Orchestrator.Term;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>The team wall (M.ORCH-T936): merging every source's session list into stable tiles, the page and streaming cap,
/// and the remote gateways' URLs, pins and pop-out arguments.</summary>
public sealed class TeamWallTests
{
    private static readonly DateTime T0 = new(2026, 10, 26, 12, 0, 0, DateTimeKind.Utc);

    private static WallSession Tile(string gateway, string id, int startedMinute = 0, string? title = null, int protocol = 2, bool interact = false) => new()
    {
        Key = TeamWall.KeyOf(gateway, id), Gateway = gateway, Person = gateway, Id = id, Title = title ?? id, Protocol = protocol,
        StartedUtc = T0.AddMinutes(startedMinute), CanInteract = interact, Cols = 120, Rows = 30,
    };

    private static WallSource Online(string gateway, params WallSession[] sessions) => new(gateway, gateway, true, null, sessions);
    private static WallSource Offline(string gateway, string why = "it can't be reached") => new(gateway, gateway, false, why, []);

    private static string[] Keys(WallLayout layout) => [.. layout.Tiles.Select(t => t.Key)];

    // ── Merge ───────────────────────────────────────────────────────────────

    [Test]
    public void A_first_look_groups_by_source_in_order_and_sorts_each_by_start()
    {
        var layout = TeamWall.Merge([],
        [
            Online(TeamWall.LocalGateway, Tile(TeamWall.LocalGateway, "b", 2), Tile(TeamWall.LocalGateway, "a", 1)),
            Online("jordan", Tile("jordan", "z", 0)),
        ]);
        Assert.That(Keys(layout), Is.EqualTo(new[] { "(local)/a", "(local)/b", "jordan/z" }));
        Assert.That(layout.Gateways.Select(g => (g.Gateway, g.Online, g.Sessions)),
            Is.EqualTo(new[] { (TeamWall.LocalGateway, true, 2), ("jordan", true, 1) }));
    }

    [Test]
    public void Tiles_already_on_the_wall_keep_their_place_and_new_ones_go_after_them()
    {
        var first = TeamWall.Merge([], [Online("jordan", Tile("jordan", "a", 5), Tile("jordan", "b", 6))]);
        // An older session appears later (its gateway was slow to list it): it goes after, not to the front.
        var second = TeamWall.Merge(first.Tiles, [Online("jordan", Tile("jordan", "b", 6), Tile("jordan", "old", 0), Tile("jordan", "a", 5))]);
        Assert.That(Keys(second), Is.EqualTo(new[] { "jordan/a", "jordan/b", "jordan/old" }));
    }

    [Test]
    public void A_tile_takes_its_sources_latest_title_and_state()
    {
        var first = TeamWall.Merge([], [Online("jordan", Tile("jordan", "a", title: "before"))]);
        var second = TeamWall.Merge(first.Tiles, [Online("jordan", Tile("jordan", "a", title: "after") with { State = TermSessionStates.Exited, ExitCode = 3 })]);
        Assert.That(second.Tiles.Single().Title, Is.EqualTo("after"));
        Assert.That(second.Tiles.Single().State, Is.EqualTo(TermSessionStates.Exited));
        Assert.That(second.Tiles.Single().ExitCode, Is.EqualTo(3));
    }

    [Test]
    public void Sessions_that_ended_leave_the_wall()
    {
        var first = TeamWall.Merge([], [Online("jordan", Tile("jordan", "a"), Tile("jordan", "b"))]);
        var second = TeamWall.Merge(first.Tiles, [Online("jordan", Tile("jordan", "b"))]);
        Assert.That(Keys(second), Is.EqualTo(new[] { "jordan/b" }));
    }

    [Test]
    public void An_offline_gateway_shows_as_offline_with_why_and_contributes_no_tiles()
    {
        var first = TeamWall.Merge([], [Online(TeamWall.LocalGateway, Tile(TeamWall.LocalGateway, "mine")), Online("jordan", Tile("jordan", "a"))]);
        var second = TeamWall.Merge(first.Tiles, [Online(TeamWall.LocalGateway, Tile(TeamWall.LocalGateway, "mine")), Offline("jordan", "it didn't answer in time")]);
        Assert.That(Keys(second), Is.EqualTo(new[] { "(local)/mine" }));
        var jordan = second.Gateways.Single(g => g.Gateway == "jordan");
        Assert.That(jordan.Online, Is.False);
        Assert.That(jordan.Problem, Is.EqualTo("it didn't answer in time"));
        Assert.That(jordan.Sessions, Is.Zero);
    }

    [Test]
    public void A_gateway_back_online_brings_its_tiles_back()
    {
        var offline = TeamWall.Merge([], [Offline("jordan")]);
        var back = TeamWall.Merge(offline.Tiles, [Online("jordan", Tile("jordan", "a"))]);
        Assert.That(Keys(back), Is.EqualTo(new[] { "jordan/a" }));
        Assert.That(back.Gateways.Single().Online, Is.True);
    }

    [Test]
    public void A_removed_gateway_disappears_and_its_tiles_with_it()
    {
        var first = TeamWall.Merge([], [Online("jordan", Tile("jordan", "a")), Online("sam", Tile("sam", "s"))]);
        var second = TeamWall.Merge(first.Tiles, [Online("sam", Tile("sam", "s"))]);
        Assert.That(Keys(second), Is.EqualTo(new[] { "sam/s" }));
        Assert.That(second.Gateways.Select(g => g.Gateway), Is.EqualTo(new[] { "sam" }));
    }

    [Test]
    public void The_same_id_on_two_gateways_is_two_tiles_and_a_repeat_within_one_counts_once()
    {
        var layout = TeamWall.Merge([],
        [
            Online("jordan", Tile("jordan", "same"), Tile("jordan", "same")),
            Online("sam", Tile("sam", "same")),
        ]);
        Assert.That(Keys(layout), Is.EqualTo(new[] { "jordan/same", "sam/same" }));
        Assert.That(layout.Gateways.Single(g => g.Gateway == "jordan").Sessions, Is.EqualTo(1));
    }

    [Test]
    public void A_source_listed_twice_counts_once()
    {
        var layout = TeamWall.Merge([], [Online("jordan", Tile("jordan", "a")), Online("jordan", Tile("jordan", "b"))]);
        Assert.That(Keys(layout), Is.EqualTo(new[] { "jordan/a" }));
        Assert.That(layout.Gateways, Has.Count.EqualTo(1));
    }

    [Test]
    public void A_session_keyed_for_another_gateway_is_rekeyed_to_its_source()
    {
        var stray = Tile("elsewhere", "a");
        var layout = TeamWall.Merge([], [Online("jordan", stray)]);
        Assert.That(layout.Tiles.Single().Key, Is.EqualTo("jordan/a"));
        Assert.That(layout.Tiles.Single().Gateway, Is.EqualTo("jordan"));
    }

    [Test]
    public void From_remote_takes_interact_only_from_the_grant()
    {
        var remote = new GatewayRemote { Name = "jordan", Person = "Jordan", Url = "https://jordan:7443" };
        var watch = TeamWall.FromRemote(remote, new RemoteSessionInfo { Id = "a", Access = "watch", Protocol = 2 });
        var interact = TeamWall.FromRemote(remote, new RemoteSessionInfo { Id = "b", Access = "interact", Protocol = 2 });
        Assert.That(watch.CanInteract, Is.False);
        Assert.That(interact.CanInteract, Is.True);
        Assert.That(watch.Person, Is.EqualTo("Jordan"));
        Assert.That(watch.Key, Is.EqualTo("jordan/a"));
        Assert.That(watch.State, Is.EqualTo(TermSessionStates.Running), "no state listed reads as running");
    }

    // ── Pages and the streaming cap ─────────────────────────────────────────

    [Test]
    public void A_page_holds_what_fits_capped()
    {
        Assert.That(TeamWall.PageSize(1600, 900), Is.EqualTo(9), "3 columns of 420 by 3 rows of 260");
        Assert.That(TeamWall.PageSize(4000, 2000), Is.EqualTo(TeamWall.MaxStreaming), "a huge window is still capped");
        Assert.That(TeamWall.PageSize(100, 100), Is.EqualTo(1), "at least one");
        Assert.That(TeamWall.PageSize(0, 0), Is.EqualTo(1), "not laid out yet");
        Assert.That(TeamWall.PageSize(double.NaN, 500), Is.EqualTo(1));
        Assert.That(TeamWall.PageSize(4000, 2000, cap: 4), Is.EqualTo(4));
    }

    [Test]
    public void Pages_split_the_tiles_and_a_page_out_of_range_is_clamped()
    {
        var tiles = Enumerable.Range(0, 7).Select(i => Tile("g", "s" + i)).ToList();
        Assert.That(TeamWall.PageCount(7, 3), Is.EqualTo(3));
        Assert.That(TeamWall.PageCount(0, 3), Is.EqualTo(1));
        Assert.That(TeamWall.Page(tiles, 0, 3).Select(t => t.Id), Is.EqualTo(new[] { "s0", "s1", "s2" }));
        Assert.That(TeamWall.Page(tiles, 2, 3).Select(t => t.Id), Is.EqualTo(new[] { "s6" }));
        Assert.That(TeamWall.Page(tiles, 9, 3).Select(t => t.Id), Is.EqualTo(new[] { "s6" }), "past the end: the last page");
        Assert.That(TeamWall.ClampPage(-1, 7, 3), Is.Zero);
    }

    [Test]
    public void Only_tiles_on_screen_stream_and_never_more_than_the_cap()
    {
        var onScreen = Enumerable.Range(0, 20).Select(i => Tile("g", "s" + i)).ToList();
        var streaming = TeamWall.Streaming(onScreen, cap: 12);
        Assert.That(streaming, Has.Count.EqualTo(12));
        Assert.That(streaming, Is.EquivalentTo(onScreen.Take(12).Select(t => t.Key)), "the first ones, in order");
    }

    [Test]
    public void Nothing_streams_while_the_wall_is_hidden()
    {
        var onScreen = new[] { Tile("g", "a"), Tile("g", "b") };
        Assert.That(TeamWall.Streaming(onScreen, shown: false), Is.Empty);
        Assert.That(TeamWall.Streaming(onScreen, cap: 0), Is.Empty);
    }

    [Test]
    public void A_one_viewer_host_never_streams_and_doesnt_use_up_the_cap()
    {
        var onScreen = new[] { Tile("g", "old", protocol: 1), Tile("g", "a"), Tile("g", "b") };
        var streaming = TeamWall.Streaming(onScreen, cap: 2);
        Assert.That(streaming, Is.EquivalentTo(new[] { "g/a", "g/b" }));
    }

    [Test]
    public void The_grid_shape_fits_the_width_and_the_count()
    {
        Assert.That(TeamWall.Shape(3, 1320), Is.EqualTo((3, 1)));
        Assert.That(TeamWall.Shape(7, 1320), Is.EqualTo((3, 3)));
        Assert.That(TeamWall.Shape(2, 4000), Is.EqualTo((2, 1)), "never more columns than tiles");
        Assert.That(TeamWall.Shape(0, 1000), Is.EqualTo((1, 1)));
        Assert.That(TeamWall.Shape(4, 0), Is.EqualTo((1, 4)));
    }

    // ── Remotes ─────────────────────────────────────────────────────────────

    [TestCase("jordan-pc:7443", "https://jordan-pc:7443")]
    [TestCase("jordan-pc", "https://jordan-pc:7443")]
    [TestCase("https://jordan-pc", "https://jordan-pc:7443")]
    [TestCase("https://jordan-pc:443", "https://jordan-pc:443")]
    [TestCase("wss://100.101.102.103:9000/sessions", "https://100.101.102.103:9000")]
    [TestCase(" https://Jordan-PC.tail1234.ts.net:7443/ ", "https://jordan-pc.tail1234.ts.net:7443")]
    public void A_gateway_url_is_normalized(string given, string expected) =>
        Assert.That(GatewayRemotes.NormalizeUrl(given), Is.EqualTo(expected));

    [TestCase("http://jordan-pc:7443")]
    [TestCase("ftp://jordan-pc")]
    [TestCase("")]
    [TestCase("   ")]
    public void A_plain_http_or_odd_url_is_refused(string given) => Assert.That(GatewayRemotes.NormalizeUrl(given), Is.Null);

    [Test]
    public void A_pin_is_64_hex_characters_and_separators_are_dropped()
    {
        var hex = string.Concat(Enumerable.Repeat("ab", 32));
        Assert.That(GatewayRemotes.NormalizePin(hex), Is.EqualTo(hex.ToUpperInvariant()));
        Assert.That(GatewayRemotes.NormalizePin(string.Join(":", Enumerable.Repeat("AB", 32))), Is.EqualTo(hex.ToUpperInvariant()));
        Assert.That(GatewayRemotes.NormalizePin("abc"), Is.Null);
        Assert.That(GatewayRemotes.NormalizePin(new string('g', 64)), Is.Null);
    }

    [Test]
    public void A_tokens_expiry_is_read_from_its_payload()
    {
        var exp = new DateTimeOffset(T0).ToUnixTimeSeconds();
        var payload = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{{\"sub\":\"j\",\"exp\":{exp}}}")).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        Assert.That(GatewayRemotes.ExpiresUtc($"v1.{payload}.sig"), Is.EqualTo(T0));
        Assert.That(GatewayRemotes.ExpiresUtc("not-a-token"), Is.Null);
        Assert.That(GatewayRemotes.ExpiresUtc("v1.!!!.sig"), Is.Null);
    }

    [Test]
    public void A_pop_out_is_watch_only_unless_asked_and_allowed()
    {
        var mine = Tile(TeamWall.LocalGateway, "m", interact: true);
        var theirs = Tile("jordan", "t", interact: false);
        var granted = Tile("jordan", "g", interact: true);
        Assert.That(TeamWall.PopOutArguments(mine, interact: false), Is.EqualTo(new[] { "--session", "m", "--stay", "--watch" }));
        Assert.That(TeamWall.PopOutArguments(mine, interact: true), Is.EqualTo(new[] { "--session", "m", "--stay" }));
        Assert.That(TeamWall.PopOutArguments(theirs, interact: true), Is.EqualTo(new[] { "--remote", "jordan", "--session", "t" }), "the grant doesn't allow it");
        Assert.That(TeamWall.PopOutArguments(granted, interact: true), Is.EqualTo(new[] { "--remote", "jordan", "--session", "g", "--interact" }));
    }

    [Test]
    public void Only_a_multi_viewer_host_can_be_popped_out()
    {
        Assert.That(TeamWall.CanPopOut(Tile(TeamWall.LocalGateway, "m", protocol: 2)), Is.True);
        Assert.That(TeamWall.CanPopOut(Tile(TeamWall.LocalGateway, "old", protocol: 1)), Is.False, "M.ORCH-T1032: it would fight the owner's tab");
        Assert.That(TeamWall.PopOut(Tile("jordan", "old", protocol: 1), interact: false), Is.False);
    }
}
