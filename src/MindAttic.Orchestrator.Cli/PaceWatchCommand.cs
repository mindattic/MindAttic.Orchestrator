using System.ComponentModel;
using System.Text.Json;
using MindAttic.Orchestrator.Budget;
using MindAttic.Orchestrator.Settings;
using Spectre.Console;
using Spectre.Console.Cli;

/// <summary>Live gate activity: the pace gate's own calculated delay right now (<c>rl-status.json</c>'s
/// <c>pace_delay_seconds</c>, the same number <c>~/.claude/pace-gate.py</c> sleeps for before each tool call), and every
/// hold or refusal as it happens, so pacing's actual effect on tool calls is externally visible rather than inferred from
/// ordinary latency.</summary>
internal sealed class PaceWatchCommand : Command<PaceWatchCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("--interval <SECONDS>")]
        [Description("How often the current calculated delay refreshes.")]
        [DefaultValue(1)]
        public int Interval { get; init; } = 1;
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        var db = new OrchestratorDb(OrchestratorDb.LivePath);
        var interval = TimeSpan.FromSeconds(Math.Clamp(settings.Interval, 1, 60));
        var since = DateTime.UtcNow;
        using var stop = new CancellationTokenSource();
        ConsoleCancelEventHandler onCtrlC = (_, e) => { e.Cancel = true; stop.Cancel(); };
        Console.CancelKeyPress += onCtrlC;
        AnsiConsole.MarkupLine("[grey]Ctrl+C to stop. Only holds of a second or more are logged; a shorter (or no) delay never prints.[/]");
        double? shown = null;
        try
        {
            while (!stop.IsCancellationRequested)
            {
                var now = CurrentDelayMs();
                if (now != shown)
                {
                    AnsiConsole.MarkupLine($"[grey]{DateTime.Now:T}[/] calculated delay right now: [bold]{Format(now)}[/]");
                    shown = now;
                }
                // A hold the daemon's database couldn't take (held past its own short wait) falls back to the old log file;
                // read that in first so a watcher here sees it too (M.ORCH-T526).
                try { db.ImportGateLog(PaceLedgerWatch.DefaultEventsPath); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
                List<GateEvent> events;
                try { events = db.LoadGateEvents(since); }
                catch (Exception ex) when (ex is System.Data.Common.DbException or IOException or UnauthorizedAccessException) { events = []; }
                foreach (var e in events.Where(e => e.Decision is "held" or "refused").OrderBy(e => e.AtUtc))
                {
                    var color = e.Decision == "refused" ? "red" : "yellow";
                    AnsiConsole.MarkupLine($"[grey]{e.AtUtc.ToLocalTime():T}[/] [{color}]{e.Decision}[/] [bold]{Format(e.HeldSeconds * 1000)}[/] before " +
                        $"{Markup.Escape(e.Tool ?? "a tool call")}" + (e.Account is { Length: > 0 } a ? $" ({Markup.Escape(a)})" : ""));
                    if (e.AtUtc >= since) since = e.AtUtc.AddTicks(1);
                }
                try { stop.Token.WaitHandle.WaitOne(interval); } catch (ObjectDisposedException) { }
            }
        }
        finally { Console.CancelKeyPress -= onCtrlC; }
        return 0;
    }

    /// <summary>The delay <c>pace-gate.py</c> would hold the very next tool call for, read fresh from <c>rl-status.json</c>
    /// (the same file it reads); null when the file can't be read (no daemon running yet, or mid-write).</summary>
    internal static double? CurrentDelayMs(string? path = null)
    {
        path ??= Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "rl-status.json");
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            return doc.RootElement.TryGetProperty("pace_delay_seconds", out var d) && d.ValueKind == JsonValueKind.Number ? d.GetDouble() * 1000 : 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { return null; }
    }

    internal static string Format(double? ms) => ms is not { } v ? "unknown (no daemon yet)" : v < 1000 ? $"{v:0}ms" : $"{v / 1000:0.0}s";
}
