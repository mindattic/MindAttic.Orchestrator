using MindAttic.Orchestrator.Budget;

namespace MindAttic.Orchestrator.Wpf.ViewModels;

/// <summary>The Accounts card (M.ORCH-T490): every Claude account signed in to here, how much of its week and 5-hour
/// window it has left and when each resets, read from the roster the daemon keeps.</summary>
public sealed partial class MainViewModel
{
    private IReadOnlyList<string> accounts = [];

    public IReadOnlyList<string> Accounts { get => accounts; private set => Set(ref accounts, value); }
    public bool HasAccounts => Accounts.Count > 0;

    private void ApplyAccounts(Snapshot next, DateTime nowUtc)
    {
        IReadOnlyList<AccountRow> rows;
        try { rows = AccountRoster.Load(aggregator.Db, next.Account); }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException or Microsoft.Data.Sqlite.SqliteException) { return; }
        var lines = AccountLines(rows, next.Account, nowUtc);
        if (lines.SequenceEqual(Accounts)) return;
        Accounts = lines;
        Raise(nameof(HasAccounts));
    }

    internal static IReadOnlyList<string> AccountLines(IEnumerable<AccountRow> rows, string? current, DateTime nowUtc) =>
        [.. rows.Select(r => AccountRoster.Line(r, current, nowUtc))];
}
