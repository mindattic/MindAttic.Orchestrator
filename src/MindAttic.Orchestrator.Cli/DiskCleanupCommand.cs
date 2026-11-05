using System.ComponentModel;
using MindAttic.Orchestrator.Activity;
using Spectre.Console;
using MindAttic.Orchestrator;
using Spectre.Console.Cli;

/// <summary>
/// <c>orchestrator disk-cleanup</c> (M.ORCH-T1449): deletes agent-made temp, oldest first, when %TEMP%'s drive is under the
/// <c>--disk-cleanup-gb</c> line, as the daemon does. The context gate starts it detached (<c>--quiet</c>) when a hook sees the
/// drive under the line, so cleanup happens without the dashboard or daemon open. One run at a time (a named mutex: a lock
/// file can't be made on a full disk).
/// </summary>
internal sealed class DiskCleanupCommand : Command<DiskCleanupCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("--dry-run")]
        [Description("Only list what would go (everything that may, oldest first), deleting nothing.")]
        public bool DryRun { get; init; }

        [CommandOption("--quiet")]
        [Description("Print nothing; alert only when something was freed (how the context gate runs it).")]
        public bool Quiet { get; init; }
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        using var mutex = new Mutex(false, @"Global\MindAttic.Orchestrator.DiskCleanup");
        try { if (!mutex.WaitOne(0)) return 0; }
        catch (AbandonedMutexException) { }
        try
        {
            var s = Ctx.Store().LoadSettings();
            var line = s.DiskCleanupGb;
            if (line <= 0 && !settings.DryRun)
            {
                if (!settings.Quiet) AnsiConsole.MarkupLine("Disk self-cleanup is off (orchestrator settings --disk-cleanup-gb).");
                return 0;
            }
            var now = DateTime.UtcNow;
            var r = DiskCleanup.Run(line, DiskCleanup.LiveSessionIds(now), now, settings.DryRun, settings.DryRun ? null : DiskCleanup.LogPath);
            var text = DiskCleanup.Describe(r, settings.DryRun);
            if (!settings.Quiet)
            {
                AnsiConsole.MarkupLine(Markup.Escape(text));
                foreach (var d in r.Deleted)
                    AnsiConsole.MarkupLine($"  {DiskWatch.Size(d.Bytes),9}  {d.NewestWriteUtc.ToLocalTime():MM-dd HH:mm}  {Markup.Escape(d.Reason)}  {Markup.Escape(d.Path)}");
                foreach (var e in r.Errors) AnsiConsole.MarkupLine($"  [yellow]{Markup.Escape(e)}[/]");
            }
            if (!settings.DryRun && r.FreedBytes > 0) new Notifier().SendAsync(s.NotifyUrl, "Disk self-cleanup", text).GetAwaiter().GetResult();
            return 0;
        }
        finally { mutex.ReleaseMutex(); }
    }
}
