namespace MindAttic.Orchestrator.Settings;

/// <summary>
/// One change written to the orchestrator's store (M.ORCH-T891): a <see cref="JsonStore"/> value saved (<see cref="Store"/> set,
/// <see cref="Name"/> its name: settings, reminders, ...) or a table of <see cref="Db"/> changed (<see cref="Store"/>
/// null, <see cref="Name"/> one of <see cref="StoreChanges.Members"/> or <see cref="StoreChanges.Orgs"/>).
/// </summary>
public sealed record StoreChange(string Name, OrchestratorDb Db, JsonStore? Store);

/// <summary>
/// Says each write to the store as it commits (M.ORCH-T891), so the status bus can push what changed to every dashboard the
/// moment it changes, whichever process wrote it (<see cref="Status.StateBus"/>). Nothing listens unless a process turns the bus
/// on (the CLI, the dashboard, the daemon), so a test's scratch store is never published. Never throws into the writer.
/// </summary>
public static class StoreChanges
{
    /// <summary>The <c>members</c> table (<see cref="Budget.MemberRoster"/>).</summary>
    public const string Members = "members";

    /// <summary>The <c>organizations</c> and <c>org_members</c> tables (<see cref="Sessions.OrgStore"/>).</summary>
    public const string Orgs = "orgs";

    /// <summary>Told of every change; null while nothing listens.</summary>
    internal static Action<StoreChange>? Changed { get; set; }

    /// <summary>A <see cref="JsonStore"/> value was saved.</summary>
    public static void Note(JsonStore store, string name) => Note(new StoreChange(name, store.Db, store));

    /// <summary>A table of <paramref name="db"/> changed: <see cref="Members"/> or <see cref="Orgs"/>.</summary>
    public static void Note(OrchestratorDb db, string table) => Note(new StoreChange(table, db, null));

    private static void Note(StoreChange change)
    {
        if (Changed is not { } changed) return;
        try { changed(change); }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            OrchestratorLog.For("StateBus").Debug(ex, "Saying a change to {Name:l} failed", change.Name);
        }
    }
}
