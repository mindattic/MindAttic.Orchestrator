using System.ComponentModel;
using MindAttic.Orchestrator.Activity;
using Spectre.Console;
using MindAttic.Orchestrator;
using Spectre.Console.Cli;

/// <summary>
/// <c>orchestrator disk-cleanup</c> (M.ORCH-T1449): deletes ended sessions' %TEMP%\claude folders when %TEMP%'s drive is under the
/// <c>--disk-cleanup-gb</c> line, as the daemon does: by session liveness (M.ORCH-T1467), plus the age rule of
/// <c>--temp-retention-hours</c> for sessions not running (M.ORCH-T1466). <c>--dry-run</c> deletes
/// nothing and lists every session folder with its size and verdict (why it would go, or why it's kept). The context gate starts it detached (<c>--quiet</c>) when a hook sees the
/// drive under the line, so cleanup happens without the dashboard or daemon open. One run at a time (a named mutex: a lock
/// file can't be made on a full disk). <c>--ledger</c> shows the ledger sweep's plan instead (M.ORCH-T1677,
/// <see cref="LedgerSweeper"/>: ledger entries whose owner is dead or finished, crash leftovers, stale bin/obj in the daemon's
/// worktrees) and deletes nothing; <c>--ledger-run</c> runs that sweep for real, whatever the dry-run setting says.
/// </summary>
internal sealed class DiskCleanupCommand : Command<DiskCleanupCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("--dry-run")]
        [Description("Delete nothing: list every session folder under %TEMP%\\claude, its size, and whether it would go and why.")]
        public bool DryRun { get; init; }

        [CommandOption("--quiet")]
        [Description("Print nothing; alert only when something was freed (how the context gate runs it).")]
        public bool Quiet { get; init; }

        [CommandOption("--ledger")]
        [Description("Show the ledger sweep's plan and delete nothing: every resource-ledger entry (ticket scratch dirs, temp files), crash leftover in %TEMP% and daemon worktree's bin/obj, its size, and whether it would go and why.")]
        public bool Ledger { get; init; }

        [CommandOption("--ledger-run")]
        [Description("Run the ledger sweep for real, whatever settings --ledger-sweep-dry-run says: delete what --ledger lists as going, and write each deletion to the disk-cleanup log.")]
        public bool LedgerRun { get; init; }
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        using var mutex = new Mutex(false, @"Global\MindAttic.Orchestrator.DiskCleanup");
        try { if (!mutex.WaitOne(0)) return 0; }
        catch (AbandonedMutexException) { }
        try
        {
            if (settings.Ledger || settings.LedgerRun) return SweepLedger(settings);
            var s = Ctx.Store().LoadSettings();
            var line = s.DiskCleanupGb;
            if (line <= 0 && s.TempRetentionHours <= 0 && !settings.DryRun)
            {
                if (!settings.Quiet) AnsiConsole.MarkupLine("Disk self-cleanup is off (orchestrator settings --disk-cleanup-gb, --temp-retention-hours).");
                return 0;
            }
            var liveness = MindAttic.Orchestrator.Sessions.SessionLiveness.Current(OrchestratorOptions.Load(Ctx.Store()).WorkspaceRoot);
            // The age pass (M.ORCH-T1466) runs with it, whatever the free space: --temp-retention-hours, 0 off.
            var r = DiskCleanup.Run(liveness, settings.DryRun, settings.DryRun ? null : DiskCleanup.LogPath, null, out var prune, s.TempRetentionHours);
            var text = DiskCleanup.Describe(r, settings.DryRun);
            if (!settings.Quiet)
            {
                AnsiConsole.MarkupLine(Markup.Escape(text));
                foreach (var f in prune.Folders.OrderByDescending(f => f.Delete).ThenByDescending(f => f.Bytes))
                    AnsiConsole.MarkupLine($"  {(f.Delete ? (settings.DryRun ? "[green]would go[/]" : "[green]deleted[/] ") : "[grey]kept[/]    ")}  {DiskWatch.Size(f.Bytes),9}  {Markup.Escape(f.Path)}{Environment.NewLine}             {Markup.Escape(f.Why)}");
                foreach (var f in prune.Scratch.OrderByDescending(f => f.Bytes))
                    AnsiConsole.MarkupLine($"  {(settings.DryRun ? "[green]would go[/]" : "[green]deleted[/] ")}  {DiskWatch.Size(f.Bytes),9}  {Markup.Escape(f.Path)}{Environment.NewLine}             {Markup.Escape(f.Why)}");
                foreach (var e in r.Errors) AnsiConsole.MarkupLine($"  [yellow]{Markup.Escape(e)}[/]");
            }
            if (!settings.DryRun && r.FreedBytes > 0) new Notifier().SendAsync(s.NotifyUrl, "Disk self-cleanup", text).GetAwaiter().GetResult();
            return 0;
        }
        finally { mutex.ReleaseMutex(); }
    }

    /// <summary>The ledger sweep (M.ORCH-T1677): <c>--ledger</c> is always a dry run that writes nothing; <c>--ledger-run</c>
    /// deletes, unless <c>--ledger</c> or <c>--dry-run</c> is given beside it, which keeps it dry.</summary>
    private static int SweepLedger(Settings settings)
    {
        var store = Ctx.Store();
        var dry = settings.Ledger || settings.DryRun || !settings.LedgerRun;
        var workspace = OrchestratorOptions.Load(store).WorkspaceRoot;
        var options = LedgerSweepOptions.From(store.LoadSettings(), dryRun: dry) with { LogPath = dry ? null : DiskCleanup.LogPath };
        var owners = LedgerOwners.Live(store, workspace, MindAttic.Orchestrator.Sessions.SessionLiveness.Current(workspace));
        var r = LedgerSweeper.Sweep(new ResourceLedger(store.Db), owners, options);
        if (settings.Quiet) return 0;
        AnsiConsole.MarkupLine(Markup.Escape(LedgerSweeper.Describe(r)));
        foreach (var i in r.Items.OrderByDescending(i => i.Delete).ThenByDescending(i => i.Bytes))
            AnsiConsole.MarkupLine($"  {(i.Delete ? (dry ? "[green]would go[/]" : "[green]deleted[/] ") : "[grey]kept[/]    ")}  {DiskWatch.Size(i.Bytes),9}  " +
                $"{Markup.Escape(i.Path)}{Environment.NewLine}             {Markup.Escape($"{i.Source}, {i.Kind}, owner {i.Owner}: {i.Why}")}");
        foreach (var e in r.Errors) AnsiConsole.MarkupLine($"  [yellow]{Markup.Escape(e)}[/]");
        if (dry && r.Going.Count > 0) AnsiConsole.MarkupLine("Nothing was deleted. orchestrator disk-cleanup --ledger-run deletes what's listed as going.");
        return 0;
    }
}
