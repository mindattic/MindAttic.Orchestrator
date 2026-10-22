using MindAttic.Orchestrator.Sessions;

namespace MindAttic.Orchestrator.Wpf.ViewModels;

/// <summary>
/// Move to account (M.ORCH-T607): each CLI on the Accounts view and the agent board has a "Move to" menu of the other signed-in
/// logins. Choosing one sends its Launcher tab the restart-resume request with that login's folder (<see cref="AccountMove"/>),
/// so the CLI moves to that account at its next idle point, keeping its session; <c>orchestrator move</c> does the same.
/// </summary>
public sealed partial class MainViewModel
{
    /// <summary>The move (seam: tests replace it, never sending to a real tab).</summary>
    internal Func<AccountMove> AccountMover { get; set; } = () => AccountMove.Default();

    private bool moving;

    /// <summary>The logins a CLI on <paramref name="ownLogin"/> (null: the global login) can move to, read off the UI thread
    /// (a login's <c>.claude.json</c> can run to megabytes).</summary>
    public async Task<IReadOnlyList<MoveTarget>> MoveTargetsAsync(string? ownLogin)
    {
        var mover = AccountMover;
        try { return await Task.Run(() => mover().TargetsFor(ownLogin)); }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            StatusMessage = $"Couldn't read the logins: {ex.Message}";
            return [];
        }
    }

    /// <summary>Moves the CLI of <paramref name="sessionId"/> (or, failing that, claude.exe <paramref name="cliPid"/>) to
    /// <paramref name="target"/>, saying what it did in the status bar and the Accounts view, and a toast when it couldn't.</summary>
    public async Task MoveToAccountAsync(string repo, string sessionId, int? cliPid, MoveTarget target)
    {
        if (moving) return;
        moving = true;
        var hosts = full?.Repos.SelectMany(r => r.Hosts).ToList() ?? [];
        var mover = AccountMover;
        AccountMoveResult result;
        try
        {
            result = await Task.Run(async () =>
            {
                var move = mover();
                var now = DateTime.UtcNow;
                var live = move.Live(hosts, now);
                var cli = live.FirstOrDefault(c => c.SessionId == sessionId) ?? (cliPid is { } pid ? live.FirstOrDefault(c => c.Pid == pid) : null);
                return cli is null
                    ? new AccountMoveResult(false, $"The CLI in {repo} isn't running any more, so it wasn't moved.")
                    : await move.MoveAsync(cli, target.Email, now);
            });
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            result = new AccountMoveResult(false, $"Couldn't move the CLI in {repo}: {ex.Message}");
            OrchestratorLog.For("Dashboard").Error(ex, "move to account failed");
        }
        finally { moving = false; }
        StatusMessage = result.Message;
        AccountsMessage = result.Message;
        if (!result.Ok) Toasts.Insert(0, new ToastViewModel($"{repo} not moved to {target.Label}", result.Message, Palette.Over, null));
    }
}
