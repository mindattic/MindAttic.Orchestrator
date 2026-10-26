using System.ComponentModel;
using MindAttic.Orchestrator;
using MindAttic.Orchestrator.Budget;
using MindAttic.Orchestrator.Sessions;
using MindAttic.Orchestrator.Settings;
using MindAttic.Orchestrator.Term;
using Spectre.Console;
using Spectre.Console.Cli;

/// <summary>
/// The team gateway's viewing ACL and audit (M.ORCH-T934): <c>orchestrator gateway grant|revoke|grants|audit|token</c>. By
/// default, members of the same org may watch each other's sessions; interact needs a grant here, with an expiry. Each change
/// rewrites the gateway's snapshot at once, so it applies to the next request and closes a stream that lost its access.
/// </summary>
internal static class GatewayAcl
{
    internal static OrchestratorDb Db() => MembersCommand.Db();

    internal static string Owner() => TermGrants.OwnerOf(Ctx.Store().LoadSettings().Gateway);

    /// <summary>The member <paramref name="who"/> names (email, the part before the @, name, first name, alias or initials), who
    /// must be in the members table; null with why said in red.</summary>
    internal static string? Member(string who)
    {
        var found = MemberLookup.Match(OrgHelpCommand.People(), who);
        if (found.Count != 1)
        {
            AnsiConsole.MarkupLine(found.Count > 1
                ? $"[red]\"{Markup.Escape(who.Trim())}\" could be more than one member: name one of them.[/]"
                : $"[red]No member is \"{Markup.Escape(who.Trim())}\": name one by email, the part before the @, name, first name, login alias or initials.[/]");
            foreach (var p in found) AnsiConsole.MarkupLine($"  {Markup.Escape(p.Label)}");
            return null;
        }
        if (MemberRoster.Find(Db(), found[0].Email) is null)
        {
            AnsiConsole.MarkupLine($"[red]{Markup.Escape(found[0].Label)} isn't in the members table: orchestrator members add {Markup.Escape(found[0].Email)} first.[/]");
            return null;
        }
        return found[0].Email;
    }

    /// <summary>Rewrites the gateway's snapshot now, so a grant or revoke applies at once (the daemon would within a tick).</summary>
    internal static void Publish()
    {
        try { TermGrants.Publish(Db(), DateTime.UtcNow); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Data.Common.DbException)
        {
            AnsiConsole.MarkupLine($"[yellow]Couldn't update the gateway's ACL now ({Markup.Escape(ex.Message)}); the daemon does within a tick.[/]");
        }
    }

    internal static string When(DateTime? utc) => utc is { } t ? t.ToLocalTime().ToString("yyyy-MM-dd HH:mm") : "never";
}

/// <summary><c>orchestrator gateway grant &lt;who&gt; watch|interact [--session &lt;id&gt;] [--for 2h]</c>.</summary>
internal sealed class GatewayGrantCommand : Command<GatewayGrantCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<WHO>")]
        [Description("A member: email, the part before the @, name, first name, login alias or initials.")]
        public string Who { get; init; } = "";

        [CommandArgument(1, "<LEVEL>")]
        [Description("watch or interact.")]
        public string Level { get; init; } = "";

        [CommandOption("--session <ID>")]
        [Description("Only this session (its id, as GET /sessions lists it); left out, every session of yours.")]
        public string? Session { get; init; }

        [CommandOption("--for <DURATION>")]
        [Description("How long it lasts: 30m, 2h, 1d, up to 30d. Interact defaults to 1h; watch, left out, never expires.")]
        public string? For { get; init; }
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        var level = settings.Level.Trim().ToLowerInvariant();
        if (level is not (TermGrants.Watch or TermGrants.Interact)) { AnsiConsole.MarkupLine("[red]The level is watch or interact.[/]"); return 1; }
        TimeSpan? span = null;
        if (settings.For is { } f && (span = TermGrants.ParseDuration(f)) is null) { AnsiConsole.MarkupLine("[red]--for takes a length of time: 30m, 2h, 1d.[/]"); return 1; }
        if (span is null && level == TermGrants.Interact) span = TermGrants.DefaultInteract;
        if (span > TermGrants.MaxFor) { AnsiConsole.MarkupLine($"[red]A grant lasts at most {TermGrants.MaxFor.TotalDays:0} days.[/]"); return 1; }
        if (GatewayAcl.Member(settings.Who) is not { } email) return 1;
        var now = DateTime.UtcNow;
        TermGrant grant;
        try
        {
            grant = TermGrants.Grant(GatewayAcl.Db(), GatewayAcl.Owner(), email, level, settings.Session, span is { } s ? now + s : null, Environment.UserName, now);
        }
        catch (ArgumentException ex) { AnsiConsole.MarkupLine($"[red]{Markup.Escape(ex.Message)}[/]"); return 1; }
        GatewayAcl.Publish();
        AnsiConsole.MarkupLine($"[green]Granted.[/] {Markup.Escape(grant.Viewer)} may [bold]{grant.Level}[/] " +
            $"{(grant.Session.Length == 0 ? "all your sessions" : "session " + Markup.Escape(grant.Session))} until {GatewayAcl.When(grant.ExpiresUtc)}.");
        if (grant.Level == TermGrants.Interact)
            AnsiConsole.MarkupLine($"[grey]Their token: orchestrator gateway token {Markup.Escape(grant.Viewer)} --scope interact[/]");
        return 0;
    }
}

/// <summary><c>orchestrator gateway revoke &lt;who&gt; [--session &lt;id&gt;]</c>.</summary>
internal sealed class GatewayRevokeCommand : Command<GatewayRevokeCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<WHO>")]
        [Description("A member: email, the part before the @, name, first name, login alias or initials.")]
        public string Who { get; init; } = "";

        [CommandOption("--session <ID>")]
        [Description("Only the grant for this session; left out, every grant to them.")]
        public string? Session { get; init; }
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        // A grant can outlive the member it names: their email is taken as given when no member matches.
        var email = MemberLookup.Match(OrgHelpCommand.People(), settings.Who) is [var one] ? one.Email
            : AccountDirs.Normalize(settings.Who) ?? settings.Who.Trim().ToLowerInvariant();
        var removed = TermGrants.Revoke(GatewayAcl.Db(), GatewayAcl.Owner(), email, settings.Session);
        GatewayAcl.Publish();
        AnsiConsole.MarkupLine(removed > 0
            ? $"[green]Revoked {removed} grant{(removed == 1 ? "" : "s")}[/] to {Markup.Escape(email)}. A stream it allowed closes within seconds."
            : $"[grey]{Markup.Escape(email)} had no such grant.[/]");
        AnsiConsole.MarkupLine("[grey]Members of the same org may still watch by default; take them out of the org (orchestrator orgs) or the members to stop that.[/]");
        return 0;
    }
}

/// <summary><c>orchestrator gateway grants [--all]</c>.</summary>
internal sealed class GatewayGrantsCommand : Command<GatewayGrantsCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("--all")]
        [Description("Expired grants too.")]
        public bool All { get; init; }
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        var now = DateTime.UtcNow;
        var owner = GatewayAcl.Owner();
        var grants = TermGrants.List(GatewayAcl.Db(), now, owner, settings.All);
        AnsiConsole.MarkupLine($"Owner [bold]{Markup.Escape(owner)}[/] always interacts. By default, active members of a session's org may watch it.");
        if (grants.Count == 0) { AnsiConsole.MarkupLine("[grey]No grants.[/]"); return 0; }
        var table = new Table().Border(TableBorder.Rounded).AddColumns("Viewer", "Level", "Session", "Expires", "Granted by", "Granted");
        foreach (var g in grants)
            table.AddRow(Markup.Escape(g.Viewer), g.Level, g.Session.Length == 0 ? "[grey]all[/]" : Markup.Escape(g.Session),
                g.IsLive(now) ? GatewayAcl.When(g.ExpiresUtc) : $"[grey]{GatewayAcl.When(g.ExpiresUtc)} (expired)[/]",
                Markup.Escape(g.CreatedBy ?? ""), GatewayAcl.When(g.CreatedUtc));
        AnsiConsole.Write(table);
        return 0;
    }
}

/// <summary><c>orchestrator gateway audit [--since 2h|&lt;date&gt;] [--limit N]</c>: imports what the gateway spooled, then lists.</summary>
internal sealed class GatewayAuditCommand : Command<GatewayAuditCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("--since <WHEN>")]
        [Description("A length of time back (30m, 2h, 7d) or a date/time; left out, the latest records.")]
        public string? Since { get; init; }

        [CommandOption("--limit <N>")]
        [Description("At most this many (the newest), 200 by default.")]
        public int? Limit { get; init; }
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        DateTime? since = null;
        if (settings.Since is { } s)
        {
            if (TermGrants.ParseDuration(s) is { } back) since = DateTime.UtcNow - back;
            else if (DateTime.TryParse(s, System.Globalization.CultureInfo.CurrentCulture, System.Globalization.DateTimeStyles.AssumeLocal, out var at)) since = at.ToUniversalTime();
            else { AnsiConsole.MarkupLine("[red]--since takes a length of time (2h, 7d) or a date.[/]"); return 1; }
        }
        var db = GatewayAcl.Db();
        try { TermGrants.ImportAudit(db); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Data.Common.DbException)
        {
            AnsiConsole.MarkupLine($"[yellow]Couldn't import the gateway's newest records now ({Markup.Escape(ex.Message)}).[/]");
        }
        var records = TermGrants.Audit(db, since, settings.Limit ?? 200);
        if (records.Count == 0) { AnsiConsole.MarkupLine("[grey]No audit records.[/]"); return 0; }
        var table = new Table().Border(TableBorder.Rounded).AddColumns("When", "Event", "Viewer", "Session", "Level", "From", "Frames", "Bytes", "Detail");
        foreach (var r in records)
            table.AddRow(r.AtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"), r.Event, Markup.Escape(r.Viewer), Markup.Escape(r.Session), r.Level,
                Markup.Escape(r.Remote ?? ""), r.Frames == 0 ? "" : r.Frames.ToString(), r.Bytes == 0 ? "" : r.Bytes.ToString(), Markup.Escape(r.Detail ?? ""));
        AnsiConsole.Write(table);
        AnsiConsole.MarkupLine("[grey]Input is counted (frames, bytes), never recorded.[/]");
        return 0;
    }
}

/// <summary>
/// <c>orchestrator gateway token &lt;who&gt; [--scope watch|interact] [--minutes N]</c>: a bearer token for a member, minted on the
/// owner's machine with the gateway's signing key and handed to them over a channel the owner trusts. It proves who they are;
/// their grants decide what it can do. Printed alone on stdout, never logged.
/// </summary>
internal sealed class GatewayTokenCommand : Command<GatewayTokenCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<WHO>")]
        [Description("A member: email, the part before the @, name, first name, login alias or initials.")]
        public string Who { get; init; } = "";

        [CommandOption("--scope <SCOPE>")]
        [Description("watch or interact; left out, interact when they have a live interact grant, else watch.")]
        public string? Scope { get; init; }

        [CommandOption("--minutes <N>")]
        [Description("1 to 60 for a short-lived token; left out, the token lasts 365 days.")]
        public int? Minutes { get; init; }
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        if (settings.Scope is { } sc && !TermGatewayTokens.IsScope(sc)) { AnsiConsole.MarkupLine("[red]--scope is watch or interact.[/]"); return 1; }
        if (settings.Minutes is < 1 or > TermGatewayTokens.MaxMinutes) { AnsiConsole.MarkupLine($"[red]--minutes is 1 to {TermGatewayTokens.MaxMinutes}.[/]"); return 1; }
        if (GatewayAcl.Member(settings.Who) is not { } email) return 1;
        var gateway = Ctx.Store().LoadSettings().Gateway;
        var now = DateTime.UtcNow;
        var scope = settings.Scope?.ToLowerInvariant()
            ?? (TermGrants.List(GatewayAcl.Db(), now, TermGrants.OwnerOf(gateway)).Any(g => g.Viewer == email && g.Level == TermGrants.Interact)
                ? TermGrants.Interact : TermGrants.Watch);
        var key = TermGatewayTokens.LoadOrCreateKey(TermGateway.DefaultDirectory, TermGatewayTokens.SigningKeyFile);
        var lifetime = settings.Minutes is { } minutes ? TimeSpan.FromMinutes(minutes) : TimeSpan.FromDays(TermGatewayTokens.MemberDays);
        var (token, expires) = TermGatewayTokens.Issue(key, email, scope, lifetime, now, member: true);
        Console.WriteLine(token);
        Console.Error.WriteLine($"A {scope} token for {email}, good until {expires.ToLocalTime():yyyy-MM-dd HH:mm}. Their grants decide what it can do; " +
            "revoke them, or replace the gateway's signing key, to cut it off at once.");
        if (settings.Minutes is null)
        {
            var (scheduled, problem) = GatewayTokenReminders.Schedule(OrchestratorOptions.Load().WorkspaceRoot, email, expires);
            Console.Error.WriteLine($"Scheduled {scheduled} Psst renewal reminder{(scheduled == 1 ? "" : "s")} before it expires (M.ORCH-T1057)." +
                (problem is null ? "" : $" {problem}"));
        }
        return 0;
    }
}
