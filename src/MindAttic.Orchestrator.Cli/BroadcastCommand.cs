using System.ComponentModel;
using MindAttic.Orchestrator.Term;
using Spectre.Console;
using Spectre.Console.Cli;

/// <summary>
/// <c>orchestrator broadcast [ACTION] [SESSION|all]</c> (M.ORCH-T935): what the team gateway shows remote viewers. Remote
/// sharing is off by default; <c>share</c> turns it on for one session (<c>share all</c>: "share my sessions"),
/// <c>mask off</c>/<c>mask on</c> changes masking of secrets on its remote streams (on by default), and <c>pause</c>/<c>resume</c>
/// stops and restarts its broadcast (<c>pause all</c>: every session). The running gateway picks each change up within a
/// fraction of a second. Without an action it lists the hosted sessions, their choices and who is watching.
/// </summary>
internal sealed class BroadcastCommand : Command<BroadcastCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "[ACTION]")]
        [Description("share, unshare, pause, resume, mask or unmask; without it, shows every hosted session's choices.")]
        public string? Action { get; init; }

        [CommandArgument(1, "[SESSION]")]
        [Description("A hosted session's id (or the start of one), or all.")]
        public string? Session { get; init; }
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        var live = TermSessions.Enumerate();
        if (settings.Action is { } action)
        {
            var target = settings.Session?.Trim();
            if (string.IsNullOrEmpty(target)) { AnsiConsole.MarkupLine("[red]Name a session id, or all.[/]"); return 1; }
            var all = target.Equals("all", StringComparison.OrdinalIgnoreCase);
            string? id = null;
            if (!all)
            {
                var matches = live.Where(s => s.Id.StartsWith(target, StringComparison.OrdinalIgnoreCase)).ToList();
                if (matches.Count != 1)
                {
                    AnsiConsole.MarkupLine(matches.Count == 0 ? $"[red]No hosted session {Markup.Escape(target)}.[/]" : $"[red]{Markup.Escape(target)} matches {matches.Count} sessions.[/]");
                    return 1;
                }
                id = matches[0].Id;
            }
            var now = DateTime.UtcNow;
            Func<TermBroadcastSettings, TermBroadcastSettings>? change = action.ToLowerInvariant() switch
            {
                "share" => s => all ? s with { ShareAll = true } : s.Share(id!, true),
                "unshare" => s => all ? s with { ShareAll = false } : s.Share(id!, false),
                "pause" => s => all ? s.PauseEvery(true, now) : s.Pause(id!, true, now),
                "resume" => s => all ? s.PauseEvery(false, now) : s.Pause(id!, false, now),
                "mask" when !all => s => s.Mask(id!, true),
                "unmask" when !all => s => s.Mask(id!, false),
                _ => null,
            };
            if (change is null) { AnsiConsole.MarkupLine("[red]Say share, unshare, pause, resume (a session or all), mask or unmask (a session).[/]"); return 1; }
            if (TermBroadcast.Update(s => change(s).Prune(live.Select(l => l.Id))) is null)
            {
                AnsiConsole.MarkupLine($"[red]Couldn't write {Markup.Escape(TermBroadcast.PathIn())}.[/]");
                return 1;
            }
            AnsiConsole.MarkupLine("[green]Saved.[/] [grey]A running gateway applies it within a second.[/]");
        }

        var b = TermBroadcast.Read();
        var watchers = TermWatchersBus.Current();
        AnsiConsole.MarkupLine($"Share my sessions: {(b.ShareAll ? "[yellow]on[/]" : "[grey]off[/]")} · every broadcast paused: {(b.PauseAll ? "[yellow]yes[/]" : "no")} · gateway {(TermGateway.Running() is null ? "[grey]not running[/]" : "[green]running[/]")}");
        if (live.Count == 0) { AnsiConsole.MarkupLine("[grey]No hosted sessions.[/]"); return 0; }
        var table = new Table().AddColumns("Session", "Title", "Shared", "Masked", "Paused", "Watching");
        foreach (var s in live)
        {
            var who = watchers.Where(w => string.Equals(w.SessionId, s.Id, StringComparison.OrdinalIgnoreCase))
                .Select(w => $"{w.Viewer} ({w.Mode})");
            table.AddRow(Markup.Escape(s.Id), Markup.Escape(s.Title ?? s.Repo ?? ""), b.IsShared(s.Id) ? "[yellow]yes[/]" : "no",
                b.IsMasked(s.Id) ? "yes" : "[red]no[/]", b.IsPaused(s.Id) ? "[yellow]yes[/]" : "no", Markup.Escape(string.Join(", ", who)));
        }
        AnsiConsole.Write(table);
        return 0;
    }
}
