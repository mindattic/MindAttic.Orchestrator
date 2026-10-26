using MindAttic.Orchestrator.Sessions;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>The dashboard's session-restore file (M.ORCH-T862): written whole, rotated and kept, and each tab reopened with
/// <c>claude --resume</c> on its session.</summary>
public sealed class SessionRestoreTests
{
    private readonly TempDirs temp = new();

    [TearDown]
    public void TearDown() => temp.DeleteAll();

    private static PaneRequest Host(string? configDir = null, string? prompt = null, string? email = null) => new("RD: Repo [Claude]", @"C:\Repo")
    {
        Command = ["launcher.exe", "host", "--name", "Repo", "--title", "RD: Repo [Claude]", "--provider", "Claude",
            .. configDir is null ? Array.Empty<string>() : ["--config-dir", configDir],
            .. prompt is null ? Array.Empty<string>() : ["--prompt", prompt]],
        Repo = "Repo",
        Provider = "Claude",
        ConfigDir = configDir,
        Email = email,
        Initials = "RD",
    };

    [Test]
    public void Saves_and_loads_every_tab()
    {
        var store = new SessionRestoreStore(temp.New());
        var state = new RestoreState
        {
            SavedUtc = new DateTime(2026, 10, 25, 12, 0, 0, DateTimeKind.Utc), Reason = "change", Pid = 42, SelectedPerson = "a@b.com", SelectedTab = 1,
            Tabs = [new RestoreTab { Request = Host(), SessionId = "s1", Login = "a@b.com", Chosen = true }, new RestoreTab { Request = Host() with { IsMenu = true } }],
        };
        Assert.That(store.Save(state), Is.True);
        var back = store.Load();
        Assert.That(back, Is.Not.Null);
        Assert.That(back!.Tabs, Has.Count.EqualTo(2));
        Assert.That(back.Tabs[0].SessionId, Is.EqualTo("s1"));
        Assert.That(back.Tabs[0].Request.Command, Is.EqualTo(Host().Command));
        Assert.That(back.Tabs[1].Request.IsMenu, Is.True);
        Assert.That(back.SelectedPerson, Is.EqualTo("a@b.com"));
        Assert.That(back.SelectedTab, Is.EqualTo(1));
        Assert.That(back.Clean, Is.False);
        Assert.That(Directory.GetFiles(store.Directory, "*.tmp"), Is.Empty);
    }

    [Test]
    public void Rotates_keeping_the_last_runs_and_never_deletes_on_read()
    {
        var store = new SessionRestoreStore(temp.New());
        for (var run = 1; run <= SessionRestoreStore.Keep + 2; run++)
        {
            store.Save(new RestoreState { Pid = run });
            Assert.That(store.Load()?.Pid, Is.EqualTo(run));
            Assert.That(File.Exists(store.CurrentPath), Is.True, "a read leaves the file");
            store.Rotate();
        }
        Assert.That(File.Exists(store.CurrentPath), Is.False);
        Assert.That(SessionRestoreStore.Load(store.RotatedPath(1))?.Pid, Is.EqualTo(SessionRestoreStore.Keep + 2));
        Assert.That(SessionRestoreStore.Load(store.RotatedPath(SessionRestoreStore.Keep))?.Pid, Is.EqualTo(3));
        Assert.That(File.Exists(store.RotatedPath(SessionRestoreStore.Keep + 1)), Is.False);
    }

    [Test]
    public void An_unreadable_file_loads_as_none()
    {
        var store = new SessionRestoreStore(temp.New());
        File.WriteAllText(store.CurrentPath, "{ not json");
        Assert.That(store.Load(), Is.Null);
    }

    [Test]
    public void Offers_only_an_unclean_run_with_tabs()
    {
        var tab = new RestoreTab { Request = Host() };
        Assert.That(SessionRestoreStore.ShouldOffer(null), Is.False);
        Assert.That(SessionRestoreStore.ShouldOffer(new RestoreState { Clean = true, Tabs = [tab] }), Is.False);
        Assert.That(SessionRestoreStore.ShouldOffer(new RestoreState { Clean = false, Tabs = [] }), Is.False);
        Assert.That(SessionRestoreStore.ShouldOffer(new RestoreState { Clean = false, Tabs = [tab] }), Is.True);
    }

    [Test]
    public void A_known_session_reopens_with_resume_and_without_its_first_prompt()
    {
        var request = SessionRestoreStore.ReopenRequest(new RestoreTab { Request = Host(@"C:\acc\a@b.com", "do the thing", "a@b.com"), SessionId = "abc", Login = "a@b.com" });
        Assert.That(request.Command, Is.EqualTo(new[]
        {
            "launcher.exe", "host", "--name", "Repo", "--title", "RD: Repo [Claude]", "--provider", "Claude", "--config-dir", @"C:\acc\a@b.com", "--resume", "abc",
        }));
        Assert.That(request.Directory, Is.EqualTo(@"C:\Repo"));
    }

    [Test]
    public void An_unknown_session_reopens_fresh()
    {
        var request = SessionRestoreStore.ReopenRequest(new RestoreTab { Request = Host(prompt: "hi") });
        Assert.That(request.Command, Does.Not.Contain("--resume"));
        Assert.That(request.Command, Does.Not.Contain("--prompt"));
    }

    [Test]
    public void A_stale_resume_is_replaced()
    {
        var old = Host() with { Command = [.. Host().Command!, "--resume", "old"] };
        var request = SessionRestoreStore.ReopenRequest(new RestoreTab { Request = old, SessionId = "new" });
        Assert.That(request.Command!.Count(a => a == "--resume"), Is.EqualTo(1));
        Assert.That(request.Command![^1], Is.EqualTo("new"));
    }

    [Test]
    public void A_launcher_menu_reopens_as_it_was()
    {
        var menu = new PaneRequest("RD: Launcher", @"C:\ws") { Command = ["launcher.exe", "--account", "a@b.com"], IsMenu = true, Provider = "Launcher" };
        Assert.That(SessionRestoreStore.ReopenRequest(new RestoreTab { Request = menu, SessionId = "x" }), Is.SameAs(menu));
    }

    [Test]
    public void Another_provider_never_resumes()
    {
        var codex = Host() with { Provider = "Codex" };
        Assert.That(SessionRestoreStore.ReopenRequest(new RestoreTab { Request = codex, SessionId = "x" }).Command, Does.Not.Contain("--resume"));
    }

    [Test]
    public void A_cli_moved_to_another_login_reopens_on_that_one()
    {
        var tab = new RestoreTab { Request = Host(@"C:\acc\a@b.com", email: "a@b.com"), SessionId = "s", Login = "c@d.com" };
        var request = SessionRestoreStore.ReopenRequest(tab, login => login is null ? @"C:\global" : $@"C:\acc\{login}",
            _ => new LoginLook("w", "CD", "#112233"));
        Assert.That(request.Email, Is.EqualTo("c@d.com"));
        Assert.That(request.ConfigDir, Is.EqualTo(@"C:\acc\c@d.com"));
        Assert.That(request.Command, Does.Contain(@"C:\acc\c@d.com"));
        Assert.That(request.Command, Does.Not.Contain(@"C:\acc\a@b.com"));
        Assert.That(request.Title, Is.EqualTo("CD: Repo [Claude]"));
        Assert.That(request.TabColor, Is.EqualTo("#112233"));
    }

    [Test]
    public void Unhosted_drops_tabs_whose_host_is_alive_and_keeps_the_rest()
    {
        var state = new RestoreState
        {
            Pid = 1, SelectedTab = 2,
            Tabs =
            [
                new RestoreTab { Request = Host(), SessionId = "s1", HostId = "alive" },
                new RestoreTab { Request = Host(), SessionId = "s2", HostId = "dead" },
                new RestoreTab { Request = Host(), SessionId = "s3" },
            ],
        };
        var kept = SessionRestoreStore.Unhosted(state, id => id == "alive");
        Assert.That(kept.Tabs.Select(t => t.SessionId), Is.EqualTo(new[] { "s2", "s3" }));
        Assert.That(kept.SelectedTab, Is.EqualTo(1));
    }

    [Test]
    public void Unhosted_with_every_host_alive_offers_nothing()
    {
        var state = new RestoreState { Pid = 1, SelectedTab = 0, Tabs = [new RestoreTab { Request = Host(), HostId = "a" }] };
        var kept = SessionRestoreStore.Unhosted(state, _ => true);
        Assert.That(SessionRestoreStore.ShouldOffer(kept), Is.False);
        Assert.That(kept.SelectedTab, Is.Null);
    }

    [Test]
    public void The_host_id_round_trips_through_the_file()
    {
        var store = new SessionRestoreStore(temp.New());
        store.Save(new RestoreState { Tabs = [new RestoreTab { Request = Host(), HostId = "h1" }] });
        Assert.That(store.Load()!.Tabs[0].HostId, Is.EqualTo("h1"));
    }
}
