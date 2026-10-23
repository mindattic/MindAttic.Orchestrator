using System.ComponentModel;
using MindAttic.Orchestrator.Settings;
using Spectre.Console;
using Spectre.Console.Cli;

/// <summary><c>orchestrator db</c> (M.ORCH-T658): where the orchestrator's data is kept, and moving it between
/// data/orchestrator.db (SQLite) and its SQL Server database.</summary>
internal sealed class DbStatusCommand : Command<DbStatusCommand.Settings>
{
    public sealed class Settings : CommandSettings { }

    public override int Execute(CommandContext context, Settings settings)
    {
        var db = new OrchestratorDb(OrchestratorDb.LivePath);
        var connection = OrchestratorServer.Configured();
        AnsiConsole.MarkupLine(connection is null
            ? $"[bold]SQLite[/]: everything is in {Markup.Escape(db.Path)}. Run [blue]orchestrator db migrate[/] to move the orchestrator's data to SQL Server."
            : $"[bold]SQL Server[/]: the orchestrator's data is in {Markup.Escape(new OrchestratorServer(connection).Describe())}; " +
              $"{Markup.Escape(db.Path)} keeps this machine's leases, marks and session states, and a copy of the settings, assignments and saved state for the hooks.");
        try
        {
            var table = new Table().Border(TableBorder.Rounded).AddColumn("Table").AddColumn("SQL Server", c => c.RightAligned()).AddColumn("orchestrator.db", c => c.RightAligned());
            foreach (var (name, server, local) in OrchestratorMigration.Counts(db, connection))
                table.AddRow(name, server?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "–", local.ToString(System.Globalization.CultureInfo.InvariantCulture));
            AnsiConsole.Write(table);
            return 0;
        }
        catch (Exception ex) when (ex is System.Data.Common.DbException or IOException or InvalidOperationException)
        {
            AnsiConsole.MarkupLine($"[red]Couldn't read the row counts: {Markup.Escape(ex.Message)}[/]");
            return 2;
        }
    }
}

internal sealed class DbMigrateCommand : Command<DbMigrateCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("--server <NAME>")]
        [Description("The SQL Server instance (default: localhost, the full instance beside the Prose database).")]
        [DefaultValue("localhost")]
        public string Server { get; init; } = "localhost";

        [CommandOption("--database <NAME>")]
        [Description("The database to keep it in (created when it isn't there).")]
        [DefaultValue(OrchestratorServer.DefaultDatabase)]
        public string Database { get; init; } = OrchestratorServer.DefaultDatabase;

        [CommandOption("--connection <STRING>")]
        [Description("A full connection string, instead of --server and --database.")]
        public string? Connection { get; init; }

        [CommandOption("--replace")]
        [Description("Copy over a SQL Server database that already holds data.")]
        public bool Replace { get; init; }
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        var connection = settings.Connection ?? OrchestratorServer.DefaultConnection(settings.Server, settings.Database);
        try { _ = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(connection); }
        catch (ArgumentException ex)
        {
            // A malformed --connection is a bad option, not a crash: said in one line and never echoed, since it may hold
            // a password (M.ORCH-T716). The builder's message names only the keyword or index it stopped at.
            AnsiConsole.MarkupLine($"[red]--connection isn't a valid SQL Server connection string: {Markup.Escape(ex.Message)}[/]");
            return 2;
        }
        var db = new OrchestratorDb(OrchestratorDb.LivePath);
        try
        {
            // Held for the copy: no daemon, dashboard or CLI writes the file between its rows being read and SQL Server taking over.
            var copied = db.HoldingWrites(() => OrchestratorMigration.ToServer(db, connection, settings.Replace));
            db.SyncMirror();
            foreach (var c in copied) AnsiConsole.MarkupLine($"  {c.Table,-14} {c.Rows,7} rows");
            AnsiConsole.MarkupLine($"[green]The orchestrator's data is in {Markup.Escape(new OrchestratorServer(connection).Describe())} now[/]; every process uses it within seconds. " +
                $"{Markup.Escape(db.Path)} keeps this machine's state and the hooks' copy. [blue]orchestrator db back[/] returns it all to SQLite.");
            return 0;
        }
        catch (Exception ex) when (ex is System.Data.Common.DbException or IOException or InvalidOperationException)
        {
            AnsiConsole.MarkupLine($"[red]Not moved: {Markup.Escape(ex.Message)}[/]");
            return 2;
        }
    }
}

internal sealed class DbBackCommand : Command<DbBackCommand.Settings>
{
    public sealed class Settings : CommandSettings { }

    public override int Execute(CommandContext context, Settings settings)
    {
        var db = new OrchestratorDb(OrchestratorDb.LivePath);
        if (OrchestratorServer.Configured() is not { } connection)
        {
            AnsiConsole.MarkupLine($"Everything is in {Markup.Escape(db.Path)} already.");
            return 0;
        }
        try
        {
            foreach (var c in OrchestratorMigration.ToSqlite(db, connection)) AnsiConsole.MarkupLine($"  {c.Table,-14} {c.Rows,7} rows");
            AnsiConsole.MarkupLine($"[green]Everything is in {Markup.Escape(db.Path)} again[/]; the SQL Server database is left as it was.");
            return 0;
        }
        catch (Exception ex) when (ex is System.Data.Common.DbException or IOException or InvalidOperationException)
        {
            AnsiConsole.MarkupLine($"[red]Not moved back: {Markup.Escape(ex.Message)}[/]");
            return 2;
        }
    }
}
