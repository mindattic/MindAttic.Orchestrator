using System.ComponentModel;
using System.Text.Json;
using MindAttic.Orchestrator;
using MindAttic.Orchestrator.Budget;
using MindAttic.Orchestrator.Sessions;
using MindAttic.Orchestrator.Settings;
using Spectre.Console;
using Spectre.Console.Cli;

Console.OutputEncoding = System.Text.Encoding.UTF8;
OrchestratorLog.Start(args.FirstOrDefault() == "daemon" ? "Daemon" : "Cli");
// A change this command makes to the members, orgs, settings, pause or reminders is pushed to every dashboard through the
// daemon's inbox as it commits (M.ORCH-T891), so none has to poll for it. Sent before the command returns: the process exits next.
MindAttic.Orchestrator.Status.StateBus.Enable(background: false);

var app = CliApp.Create();
try
{
    return app.Run(args);
}
catch (Exception ex) when (ex is Spectre.Console.Cli.CommandParseException or Spectre.Console.Cli.CommandRuntimeException)
{
    // A typing mistake (an unknown flag, a missing argument, a value that isn't a number) is said in one line, not
    // logged as a crash.
    AnsiConsole.MarkupLine($"[red]{Markup.Escape(ex.Message)}[/]");
    return 1;
}
catch (InvalidDataException ex)
{
    // A store file that can't be read (settings.json with a hand edit's typo, or held past the retries) was left as it
    // is rather than saved over: the message says so and what to do. Not a crash (M.ORCH-T332).
    AnsiConsole.MarkupLine($"[red]{Markup.Escape(ex.Message)}[/]");
    return 1;
}
catch (Exception ex)
{
    // Only the command's name: its options and arguments may hold secrets, such as db migrate --connection's password (M.ORCH-T716).
    OrchestratorLog.For("Cli").Fatal(ex, "orchestrator {Command:l} crashed", CliApp.CommandName(args));
    AnsiConsole.WriteException(ex, ExceptionFormats.ShortenEverything);
    return 1;
}
finally
{
    OrchestratorLog.Stop();
}

/// <summary>Every command the CLI has, so tests can run them in-process (see <see cref="Ctx.StoreDirectory"/>).</summary>
internal static class CliApp
{
    /// <summary>The command <paramref name="args"/> runs (say "db migrate"), without its options or arguments, which may
    /// hold secrets: the leading words that are plain lower-case command names (M.ORCH-T716).</summary>
    public static string CommandName(IEnumerable<string> args)
    {
        var words = args.TakeWhile(a => a.Length > 0 && a.All(c => c is (>= 'a' and <= 'z') or '-') && a[0] != '-').Take(3).ToList();
        return words.Count == 0 ? "(no command)" : string.Join(' ', words);
    }

    /// <summary>The app, which takes <c>--5h</c> as typed: Spectre won't name an option with a leading digit, so it's declared as
    /// <c>--five-hour</c> and the argument is renamed before parsing (M.ORCH-T707).</summary>
    public sealed class Runner(CommandApp app)
    {
        public int Run(IEnumerable<string> args)
        {
            var all = args.ToList();
            // Only org limit has the option, so a --5h elsewhere (in a prompt, say) stays as typed.
            if (all.Count >= 2 && all[0].Equals("org", StringComparison.OrdinalIgnoreCase) && all[1].Equals("limit", StringComparison.OrdinalIgnoreCase))
                all = [.. all.Select(a =>
                    a.Equals("--5h", StringComparison.OrdinalIgnoreCase) ? "--five-hour"
                    : a.StartsWith("--5h=", StringComparison.OrdinalIgnoreCase) ? "--five-hour" + a[4..]
                    : a)];
            return app.Run(MembersOrgFirst(all));
        }

        /// <summary>The members branch's <c>--org</c> (M.ORCH-T755) is the branch's option, which Spectre takes only before its
        /// subcommand, so <c>org members add &lt;email&gt; ... --org sales</c> has it moved to just after <c>members</c>.</summary>
        internal static List<string> MembersOrgFirst(List<string> all)
        {
            var members = all.Count > 0 && all[0].Equals("members", StringComparison.OrdinalIgnoreCase) ? 0
                : all.Count > 1 && all[0].Equals("org", StringComparison.OrdinalIgnoreCase) && all[1].Equals("members", StringComparison.OrdinalIgnoreCase) ? 1
                : -1;
            if (members < 0) return all;
            var at = all.FindIndex(members + 1, a => a.Equals("--org", StringComparison.OrdinalIgnoreCase) || a.StartsWith("--org=", StringComparison.OrdinalIgnoreCase));
            if (at <= members + 1) return all;
            var take = all[at].Contains('=') || at + 1 >= all.Count ? 1 : 2;
            var option = all.GetRange(at, take);
            all.RemoveRange(at, take);
            all.InsertRange(members + 1, option);
            return all;
        }
    }

    public static Runner Create() => new(CreateApp());

    private static CommandApp CreateApp()
    {
        var app = new CommandApp();
        app.Configure(config =>
        {
            config.SetApplicationName("orchestrator");
            // Help goes where every command writes, so a test swapping AnsiConsole.Console sees it too (M.ORCH-T496).
            config.ConfigureConsole(AnsiConsole.Console);
            config.AddCommand<StatusCommand>("status").WithDescription("Every repo: state, live sessions, open tickets, tokens today.");
            config.AddCommand<StartCommand>("start").WithDescription("Open an agent tab for a repo through the Launcher; --account <email> runs its Claude CLI on that per-CLI login's token pool.");
            config.AddCommand<SendCommand>("send").WithDescription("Type text into a live Launcher-hosted session.");
            config.AddCommand<BudgetCommand>("budget").WithDescription("Token budget for the current 5-hour block and the week, and the pace; --block/--weekly/--warn/--pace/--target set them, --hits lists learned limits.");
            config.AddCommand<QueueCommand>("queue").WithDescription("Every open ticket across the workspace, in the order the scheduler would start them: explicit priority, then newest first, each ticket's open prerequisites just before it.");
            config.AddCommand<DispatchCommand>("dispatch").WithDescription("Start a session on a ticket, in its repo's own checkout on main.");
            config.AddCommand<ReleaseCommand>("release").WithDescription("Return a ticket to the queue; --remove-worktree also deletes its old clean worktree (one made before tickets were worked on main).");
            config.AddCommand<DaemonCommand>("daemon").WithDescription("Keep the budget file, pace and hooks fresh without the dashboard open.");
            config.AddCommand<SettingsCommand>("settings").WithDescription("Show or change scheduler settings.");
            config.AddCommand<AuditCommand>("audit").WithDescription("Find drift in shared agent files and code duplicated across repos.");
            config.AddCommand<NextCommand>("next").WithDescription("The next tickets a CLI running /allocate hands its subagents, each with its model (--claim: assignment and prompt, worked in the repo's own checkout on main).");
            config.AddCommand<ReviewCommand>("review").WithDescription("From a Sonnet ticket's repo checkout: its one Opus review of the diff before it lands, when the diff is big or touches risky files (--dry-run only says).");
            config.AddCommand<DryRunCommand>("dryrun").WithDescription("Show what /allocate would start right now, without starting anything.");
            config.AddCommand<ShowCommand>("show").WithDescription("The sessions the orchestrator ran on a ticket (the ticket itself, its history and commits: tickets ticket show).");
            config.AddCommand<UsageReportCommand>("report").WithDescription("The usage report: every member's 5-hour and weekly use and resets, points left to the weekly target, how old each reading is and whether it's accurate; --detail adds the CLIs' spend and the API key's; --since <date>, --csv, --json; --md/--html <file> write it to share.");
            config.AddCommand<ReportCommand>("tokens").WithDescription("Uncached tokens by week, repo, provider and ticket type, and cost per completed ticket by model; --csv writes it out (was report before M.ORCH-T685).");
            config.AddBranch("gateway", gw =>
            {
                gw.SetDescription("The team gateway (M.ORCH-T933), off by default: on|off, and where it listens (--bind, --port) and its certificate (--cert-path, --cert-thumbprint, --dev-cert-names); shows whether it runs and its certificate pin. Who may watch or interact: grant, revoke, grants; who did: audit; a member's token: token (M.ORCH-T934). Teammates' gateways the team wall watches: remote add|remove|list (M.ORCH-T936).");
                gw.SetDefaultCommand<GatewayCommand>();
                gw.AddCommand<GatewayCommand>("on").WithDescription("Turn the gateway on (with any of the gateway options).");
                gw.AddCommand<GatewayCommand>("off").WithDescription("Turn the gateway off.");
                gw.AddCommand<GatewayGrantCommand>("grant").WithDescription("Let a member watch or interact: grant <who> watch|interact [[--session <id>]] [[--for 2h]] (interact defaults to 1h; watch never expires unless --for).");
                gw.AddCommand<GatewayRevokeCommand>("revoke").WithDescription("Take a member's grants back: revoke <who> [[--session <id>]]; a stream they allowed closes within seconds.");
                gw.AddCommand<GatewayGrantsCommand>("grants").WithDescription("List the grants (--all: expired ones too); same-org members may watch by default.");
                gw.AddCommand<GatewayAuditCommand>("audit").WithDescription("Who attached, detached and typed (counts, never content): audit [[--since 2h|<date>]] [[--limit N]].");
                gw.AddCommand<GatewayTokenCommand>("token").WithDescription("A bearer token for a member, to hand them: token <who> [[--scope watch|interact]] [[--minutes N]].");
                gw.AddBranch("remote", r =>
                {
                    r.SetDescription("Teammates' gateways the dashboard's team wall watches (M.ORCH-T936): add, remove, list.");
                    r.SetDefaultCommand<GatewayRemoteListCommand>();
                    r.AddCommand<GatewayRemoteAddCommand>("add").WithDescription("Add or replace a teammate's gateway: add <name> <url> --pin <sha256> [[--person <who>]] [[--token <token>]] (asked for when left out; the token they issued you with orchestrator gateway token <you>, kept DPAPI-sealed).");
                    r.AddCommand<GatewayRemoteRemoveCommand>("remove").WithDescription("Remove a teammate's gateway: remove <name>.");
                    r.AddCommand<GatewayRemoteListCommand>("list").WithDescription("List the teammates' gateways: name, person, URL, pin and when each token expires; --check also asks each for its sessions.");
                });
            });
            config.AddCommand<BroadcastCommand>("broadcast").WithDescription("What the team gateway shows remote viewers (M.ORCH-T935), nothing by default: share|unshare|pause|resume <session|all>, mask|unmask <session>; without an action, every hosted session's choices and who is watching.");
            config.AddCommand<BillingCommand>("billing").WithDescription("Who every CLI bills (Claude Team or the Vault's API key) and each open CLI's own billing; team or api switches every CLI.");
            config.AddBranch("accounts", a =>
            {
                a.SetDescription("Every Claude account signed in to here: weekly % used and left, its reset, the 5-hour % and reset, and when each was last read (orchestrator.db).");
                a.SetDefaultCommand<AccountsCommand>();
                a.AddCommand<AccountsAliasCommand>("alias").WithDescription("Name a Claude login (alias <email> \"<name>\"): the dashboard, the Accounts view, agent cards and its status line show the name, the email in the tooltip; without a name, says its name; --reset goes back to the email's part before the @.");
                a.AddCommand<AccountsPickCommand>("pick").WithDescription("The Auto login a new Claude CLI should start on (one CLI per account): prints its CLAUDE_CONFIG_DIR, or an empty line for the global login.");
            });
            config.AddBranch<MembersBranchSettings>("members", m =>
            {
                m.SetDescription("The organization's members (orchestrator.db): name, email, role, tier, status, and whether each has signed in here; the dashboard has a tab for each.");
                m.SetDefaultCommand<MembersCommand>();
                m.AddCommand<MembersAddCommand>("add").WithDescription("Add a member: add <email> --name \"<name>\" --role primary-owner|owner|admin|member --tier premium|standard [[--status active|pending]].");
                m.AddCommand<MembersSetCommand>("set").WithDescription("Change a member: set <email> with any of --name, --role, --tier, --status (--name \"\" clears the name).");
                m.AddCommand<MembersRemoveCommand>("remove").WithDescription("Remove a member: remove <email>.");
            });
            // The global /org and /organization commands run these (M.ORCH-T686); each is a command above, a member named any way.
            config.AddBranch("org", o =>
            {
                o.SetDescription("The organization: usage (the usage report), login <who>, members, start <repo> <who>, allow <who> [[--until reset|<time>]], disallow <who>, limit [[<who> --weekly <pct> --5h <pct> | --reset]]; <who> is a member's email, the part before the @, name, first name, login alias or initials. Each works on the org chosen in the dashboard's switcher, or --org <id|name>'s (M.ORCH-T755).");
                o.SetDefaultCommand<OrgHelpCommand>();
                o.AddCommand<OrgHelpCommand>("help").WithDescription("List the org subcommands.");
                o.AddCommand<UsageReportCommand>("usage").WithDescription("The usage report, as orchestrator report prints it: --detail, --since <date>, --csv, --json, --md/--html <file>.");
                o.AddCommand<OrgLoginCommand>("login").WithDescription("Sign a member's own login in (orchestrator login --account <their email>); opens the browser.");
                o.AddBranch<MembersBranchSettings>("members", m =>
                {
                    m.SetDescription("The members, as orchestrator members lists them; add, set and remove change them.");
                    m.SetDefaultCommand<MembersCommand>();
                    m.AddCommand<MembersAddCommand>("add").WithDescription("Add a member: add <email> --name \"<name>\" --role primary-owner|owner|admin|member --tier premium|standard [[--status active|pending]].");
                    m.AddCommand<MembersSetCommand>("set").WithDescription("Change a member: set <email> with any of --name, --role, --tier, --status.");
                    m.AddCommand<MembersRemoveCommand>("remove").WithDescription("Remove a member: remove <email>.");
                });
                o.AddCommand<OrgStartCommand>("start").WithDescription("Start a CLI in <repo> on a member's login (orchestrator start <repo> --account <their email>).");
                o.AddCommand<OrgAllowCommand>("allow").WithDescription("Let a member's login take work past its weekly target: allow <who> [[--until reset|<time>]], until its weekly reset by default (M.ORCH-T694).");
                o.AddCommand<OrgLimitCommand>("limit").WithDescription("Per-user usage cutoffs (M.ORCH-T707): limit alone lists them; limit <who> --weekly <pct> --5h <pct> sets them, --reset puts back 90 and 98.");
                o.AddCommand<OrgDisallowCommand>("disallow").WithDescription("Take back org allow: disallow <who>; that login stops at its weekly target again.");
            });
            config.AddCommand<LoginCommand>("login").WithDescription("The account roster (as the dashboard's account menu shows it); with an email or short name, opens that account's login (claude auth login --email); --account <email> signs in a per-CLI login of its own instead; --api-key bills the Vault's API key.");
            config.AddCommand<MoveCommand>("move").WithDescription("Move a running Claude CLI (<repo|pid>) to another signed-in login (--account <email> or --global): its Launcher tab restarts it with claude --resume on that login at its next idle point, keeping its session.");
            config.AddCommand<ClisCommand>("clis").WithDescription("Every open Claude CLI and whether /allocate is on in it (the roster the daemon keeps in orchestrator.db).");
            config.AddCommand<BusyCommand>("busy").WithDescription("Whether any Claude CLI on this machine (any login) is still working: mid-turn, a subagent running, an /allocate claim in flight, or holding on a prompt. Exits 1 when any is working or can't be read, 0 when all are idle; the calling CLI is left out. --wait polls until all are idle. deploy.ps1 runs it before a redeploy (M.ORCH-T873).");
            config.AddBranch("db", d =>
            {
                d.SetDescription("Where the orchestrator's data is kept: SQL Server (its own database beside Prose's) or data/orchestrator.db (SQLite), with each table's rows in both.");
                d.SetDefaultCommand<DbStatusCommand>();
                d.AddCommand<DbMigrateCommand>("migrate").WithDescription("Copy the orchestrator's data from data/orchestrator.db into SQL Server (--server, --database or --connection; the database is created), check every table's row count, then use it on this machine; --replace copies over a database that already holds data.");
                d.AddCommand<DbBackCommand>("back").WithDescription("Copy the data back from SQL Server into data/orchestrator.db and stop using SQL Server on this machine.");
            });
            // Organizations above people (M.ORCH-T655); org (above) stays about the members of the current one.
            config.AddBranch("orgs", o =>
            {
                o.SetDescription("The organizations the dashboard switches between above the person tabs, each with its people and their logins (each on its own LLM provider); with none added, one org (MindAttic, id default) holds every login.");
                o.SetDefaultCommand<OrgsListCommand>();
                o.AddCommand<OrgsAddCommand>("add").WithDescription("Add an org (add \"Sales\" [[--id sales]]).");
                o.AddCommand<OrgsRenameCommand>("rename").WithDescription("Rename an org (rename <id> \"<name>\"; default is the default org).");
                o.AddCommand<OrgsRemoveCommand>("remove").WithDescription("Remove an org; its logins go back to the default org.");
                o.AddCommand<OrgsMemberCommand>("member").WithDescription("Put a login in an org as a person's (member <org> \"<person>\" <email|global> [[--provider Codex]]), moving it from any other org.");
                o.AddCommand<OrgsUnmemberCommand>("unmember").WithDescription("Take a login out of its org, back to the default one (unmember <email|global> [[--provider Codex]]).");
                o.AddCommand<OrgsRepoCommand>("repo").WithDescription("Which org a repo's tickets belong to, dispatched only to its members' logins (repo <repo> <org>, repo <repo> --default; no repo lists them); a repo not set is the default org's.");
            });
            config.AddCommand<LedgerCommand>("ledger").WithDescription("Every 5-hour window graded against 99%-never-over (ok, under, blind, over) and the record so far; --csv writes it out. Exits 1 if one went over in --days.");
            config.AddCommand<PaceWatchCommand>("pace-watch").WithDescription("Live: the pace gate's calculated delay right now, and every hold or refusal as it happens, so pacing's actual effect on tool calls is visible outside the chat; --interval seconds (1 by default). Ctrl+C to stop.");
            config.AddCommand<RemindCommand>("remind").WithDescription("Save a reminder the daemon texts through Psst when it's due (\"<text>\" --at \"tomorrow 9am\" [[--to <contact>]]); --list, --cancel <id>.");
            config.AddBranch("workspace", w =>
            {
                w.SetDescription("The workspace as a list of repos: export it, or build it on a new machine.");
                w.AddCommand<WorkspaceExportCommand>("export").WithDescription("Write docs\\workspace-repos.json in the workspace root repo from the repos on disk.");
                w.AddCommand<WorkspaceInstallAppsCommand>("install-apps").WithDescription("Run every app's deploy.ps1 in the workspace (not the orchestrator's own).");
                w.AddCommand<WorkspaceBuildCommand>("build").WithDescription("Clone the workspace root, then every repo it lists, into a folder.");
            });
            // Let crashes reach the catch below so they're logged, not just printed.
            config.PropagateExceptions();
        });
        return app;
    }
}

internal sealed class StatusCommand : Command<StatusCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("--all")]
        [Description("Include dormant repos with no open tickets.")]
        public bool All { get; init; }

        [CommandOption("--json")]
        [Description("Write the snapshot as JSON.")]
        public bool Json { get; init; }
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        var store = Ctx.Store();
        var snapshot = new StatusAggregator(OrchestratorOptions.Load(), store, new RateLimitStatusWriter(), Ctx.Lease(store)).Refresh();
        if (settings.Json)
        {
            Console.WriteLine(JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true }));
            return 0;
        }

        var usage = snapshot.UsageToday;
        AnsiConsole.MarkupLine(
            $"[bold]{snapshot.Repos.Count}[/] repos · [green]{snapshot.Working} working[/] · [yellow]{snapshot.Waiting} waiting[/] · " +
            $"{snapshot.LiveHosts} hosted tabs · {snapshot.OpenTickets} open tickets · " +
            $"tokens today [bold]{Format.Tokens(usage.Total)}[/] ({Format.Tokens(usage.Fresh)} uncached, {Format.Tokens(usage.Output)} out)");
        if (snapshot.Budget is { } b) AnsiConsole.MarkupLine(BudgetCommand.Line(b, snapshot.TakenUtc));
        if (snapshot.Pace is { IsPaced: true } pace) AnsiConsole.MarkupLine(BudgetCommand.PaceLine(pace));
        if (snapshot.Billing is { } billing) BillingCommand.Write(billing, snapshot.TakenUtc);
        foreach (var x in MindAttic.Orchestrator.Activity.Watchdog.Stuck(snapshot))
            AnsiConsole.MarkupLine($"[red]looks stuck:[/] a session in {Markup.Escape(x.Repo)} is repeating {Markup.Escape(x.Loop.Describe())} since {x.Loop.SinceUtc.ToLocalTime():t}");
        // Tickets whose CLI or tab exited mid-task (M.ORCH-T748), and what the daemon did about them.
        QueueCommand.WriteOrphans(store);

        var table = new Table().Border(TableBorder.Rounded)
            .AddColumns("Repo", "State", "Sessions", "Open", "Last active", "Tokens today");
        foreach (var r in Order(snapshot.Repos).Where(r => settings.All || r.State != RepoState.Dormant || r.OpenTickets.Count > 0))
        {
            var color = r.State switch { RepoState.Working => "green", RepoState.Waiting => "yellow", _ => "grey" };
            table.AddRow(
                Markup.Escape(r.Name),
                $"[{color}]{r.State}[/]",
                Format.Sessions(r),
                r.OpenTickets.Count == 0 ? "" : r.OpenTickets.Count.ToString(),
                Format.Ago(r.LastActivityUtc, snapshot.TakenUtc),
                r.UsageToday.Total == 0 ? "" : Format.Tokens(r.UsageToday.Total));
        }
        AnsiConsole.Write(table);
        foreach (var w in snapshot.Warnings) AnsiConsole.MarkupLine($"[red]![/] {Markup.Escape(w)}");
        return 0;
    }

    internal static IEnumerable<RepoStatus> Order(IEnumerable<RepoStatus> repos) => repos
        .OrderByDescending(r => r.State)
        .ThenByDescending(r => r.LastActivityUtc ?? DateTime.MinValue)
        .ThenByDescending(r => r.OpenTickets.Count)
        .ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase);
}

internal sealed class StartCommand : Command<StartCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<REPO>")]
        public string Repo { get; init; } = "";

        [CommandOption("--provider <PROVIDER>")]
        [DefaultValue("Claude")]
        public string Provider { get; init; } = "Claude";

        [CommandOption("--prompt <TEXT>")]
        public string? Prompt { get; init; }

        [CommandOption("--account <EMAIL>")]
        [Description("Run the CLI on this account's own login (signed in first, in the browser, when it has none yet), paced on its own token pool. Without it a Claude CLI goes on the Auto login: a signed-in per-CLI login no CLI runs on yet (most headroom first), else the global login (settings --auto-login false: always the global login).")]
        public string? Account { get; init; }
    }

    /// <summary>Where per-CLI logins' folders are; tests point it elsewhere (never the real ~/.claude-accounts).</summary>
    internal static Func<AccountDirs>? DirsOverride { get; set; }

    /// <summary>The live CLIs' config folders the Auto login sees as taken; tests replace it (null reads the context gate).</summary>
    internal static Func<IEnumerable<string?>>? LiveOverride { get; set; }

    /// <summary>Seam: whether API billing is on; null reads the billing mode in force.</summary>
    internal static Func<bool>? BillingOverride { get; set; }

    public override int Execute(CommandContext context, Settings settings)
    {
        var options = OrchestratorOptions.Load();
        var repo = new MindAttic.Tickets.Workspace(options.WorkspaceRoot).Find(settings.Repo);
        if (repo is null) { AnsiConsole.MarkupLine($"[red]No repo named {Markup.Escape(settings.Repo)}.[/]"); return 1; }
        if (Ctx.Provider(settings.Provider) is not { } provider) return 1;
        string? configDir = null, email = null, pickNote = null;
        var dirs = DirsOverride?.Invoke() ?? new AccountDirs();
        if (settings.Account is not null)
        {
            if (OwnLogin(dirs, settings.Account, provider) is not { } dir) return 1;
            (configDir, email) = (dir.Directory, dir.Email);
        }
        // No --account: the Auto login (M.ORCH-T605) puts it on a per-CLI login no CLI runs on yet, else the global one.
        else if (new LoginPicker(dirs, LiveOverride) { Allowances = () => WeeklyAllowances.Active(Ctx.Store(), DateTime.UtcNow), Cutoffs = () => UsageCutoffs.Load(Ctx.Store()) }.ForNewTab(provider, Ctx.Settings.AutoLogin, DateTime.UtcNow, out pickNote, Ctx.Settings.WeeklyStopPercent) is { } auto)
            (configDir, email) = (auto.Directory, auto.Email);
        // No login of its own has room: never the global login, a member's own seat (M.ORCH-T747); the API key when billing is on (M.ORCH-T706), else it waits.
        if (settings.Account is null && configDir is null && provider == "Claude"
            && !(BillingOverride?.Invoke() ?? MindAttic.Orchestrator.Scheduling.Dispatcher.ApiBillingInForce()))
        {
            // Why the picker found none (bughunt 2026-10-27): Auto login off, or its own reason, not only "no room".
            var why = !Ctx.Settings.AutoLogin ? "Auto login is off" : pickNote is { Length: > 0 } note ? note : "no login of its own has room";
            AnsiConsole.MarkupLine($"[red]{Markup.Escape(why)} and API billing is off: not started on the global login (a member's own seat). Pass --account, or switch API billing on.[/]");
            return 1;
        }
        var error = new SessionControl(options).Start(repo.Name, repo.Path, provider, settings.Prompt, configDir);
        if (error is not null) { AnsiConsole.MarkupLine($"[red]{Markup.Escape(error)}[/]"); return 1; }
        AnsiConsole.MarkupLine($"Started [bold]{provider}[/] in {Markup.Escape(repo.Name)}" +
            (email is null ? "." : $" on {Markup.Escape(email)}'s own login."));
        return 0;
    }

    /// <summary>The per-CLI login (M.ORCH-T559) a tab is started on, brought up to date; null (said in red) when it can't be.</summary>
    internal static AccountDir? OwnLogin(AccountDirs dirs, string email, string provider)
    {
        if (provider != "Claude")
        {
            AnsiConsole.MarkupLine($"[red]--account is a Claude login: {Markup.Escape(provider)} has none.[/]");
            return null;
        }
        if (AccountDirs.Normalize(email) is null)
        {
            AnsiConsole.MarkupLine($"[red]\"{Markup.Escape(email)}\" isn't an email address: --account takes the account's whole email.[/]");
            return null;
        }
        var dir = dirs.For(email);
        if (!dir.SignedIn)
        {
            AnsiConsole.MarkupLine($"{Markup.Escape(dir.Email)} has no login of its own yet: signing it in first.");
            var signedIn = LoginCommand.SignInFirst(dirs, dir.Email);
            if (signedIn.Outcome != OwnLoginOutcome.SignedIn)
            {
                AnsiConsole.MarkupLine($"[red]No tab opened: {Markup.Escape(dir.Email)} isn't signed in as itself.[/]");
                return null;
            }
        }
        // Its settings follow ~/.claude's (hooks, statusline) each time a CLI starts on it.
        foreach (var line in dirs.Prepare(dir)) AnsiConsole.MarkupLine($"[grey]{Markup.Escape(line)}[/]");
        return dir;
    }
}

internal sealed class SendCommand : AsyncCommand<SendCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<REPO>")]
        public string Repo { get; init; } = "";

        [CommandArgument(1, "<TEXT>")]
        public string Text { get; init; } = "";

        [CommandOption("--no-submit")]
        [Description("Type the text without pressing Enter.")]
        public bool NoSubmit { get; init; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        var snapshot = new StatusAggregator(OrchestratorOptions.Load()).Refresh();
        var repo = snapshot.Repos.FirstOrDefault(r => r.Name.Equals(settings.Repo, StringComparison.OrdinalIgnoreCase));
        if (repo is null || repo.Hosts.Count == 0)
        {
            AnsiConsole.MarkupLine($"[red]No Launcher-hosted session for {Markup.Escape(settings.Repo)}.[/]");
            return 1;
        }
        var sent = 0;
        foreach (var h in repo.Hosts)
            // Text and Enter sent together read as a paste and leave the prompt unsent: Enter goes as a keystroke of its own.
            if (settings.NoSubmit ? await SessionControl.SendAsync(h.PipeName, settings.Text, submit: false)
                                  : await SessionControl.SubmitAsync(h.PipeName, settings.Text)) sent++;
        AnsiConsole.MarkupLine($"Delivered to {sent} of {repo.Hosts.Count} session(s).");
        return sent > 0 ? 0 : 1;
    }
}

internal sealed class BudgetCommand : Command<BudgetCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("--block <TOKENS>")]
        [Description("Uncached tokens per 5-hour block (0 clears it: Claude's reported limit, else the learned one, else your peak block).")]
        public string? Block { get; init; }

        [CommandOption("--weekly <TOKENS>")]
        [Description("Uncached tokens per week (0 clears it).")]
        public string? Weekly { get; init; }

        [CommandOption("--weekly-reset <WHEN>")]
        [Description("When the week resets, local time: \"Mon 9:00\", \"Tue 5pm\". \"none\" clears it. Claude's own reported reset comes first; then this, the learned one, or a rolling 7 days.")]
        public string? WeeklyReset { get; init; }

        [CommandOption("--warn <PERCENT>")]
        public int? Warn { get; init; }

        [CommandOption("--pace <ON>")]
        [Description("true or false: cap sessions so spend lands on the target at the reset instead of hitting the limit.")]
        public bool? Pace { get; init; }

        [CommandOption("--target <PERCENT>")]
        [Description("How much of each limit pacing aims to use by the reset (default 98).")]
        public int? Target { get; init; }

        [CommandOption("--week")]
        [Description("The week day by day, and where it ends at this pace.")]
        public bool Week { get; init; }

        [CommandOption("--hits")]
        [Description("List the limit hits the learned limits come from.")]
        public bool Hits { get; init; }
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        var store = Ctx.Store();
        WeeklyReset? reset = null;
        if (settings.WeeklyReset is not null && !WeeklyReset.TryParse(settings.WeeklyReset, out reset))
        {
            AnsiConsole.MarkupLine($"[red]Couldn't read \"{Markup.Escape(settings.WeeklyReset)}\"; use a day and a time, like \"Mon 9:00\" or \"Tue 5pm\".[/]");
            return 1;
        }
        if (settings.Target is < 1 or > 100) { AnsiConsole.MarkupLine("[red]--target is a percent from 1 to 100.[/]"); return 1; }
        if (settings.Warn is < 1 or > 100) { AnsiConsole.MarkupLine("[red]--warn is a percent from 1 to 100.[/]"); return 1; }
        // Token counts read as the README and the Settings panel take them: 20M, 500k, 1.5B, or a plain number.
        long? blockTokens = null, weeklyTokens = null;
        if (settings.Block is { } bt) { if (!TokenText.TryParse(bt, out var v)) { AnsiConsole.MarkupLine("[red]--block: a number like 20M, 500k or 0.[/]"); return 1; } blockTokens = v; }
        if (settings.Weekly is { } wt) { if (!TokenText.TryParse(wt, out var v)) { AnsiConsole.MarkupLine("[red]--weekly: a number like 150M, 500k or 0.[/]"); return 1; } weeklyTokens = v; }
        if (settings.Block is not null || settings.Weekly is not null || settings.Warn is not null || settings.WeeklyReset is not null ||
            settings.Pace is not null || settings.Target is not null)
        {
            var s = store.LoadSettingsToChange();
            store.SaveSettings(s with
            {
                BlockBudget = blockTokens is { } bl ? (bl > 0 ? bl : null) : s.BlockBudget,
                WeeklyBudget = weeklyTokens is { } w ? (w > 0 ? w : null) : s.WeeklyBudget,
                WarnPercent = settings.Warn ?? s.WarnPercent,
                WeeklyReset = settings.WeeklyReset is null ? s.WeeklyReset : reset?.ToString(),
                Pacing = settings.Pace ?? s.Pacing,
                PaceTargetPercent = settings.Target ?? s.PaceTargetPercent,
            });
        }

        // Built here (not the daemon's published copy) so a budget just changed above shows at once.
        var snapshot = new StatusAggregator(OrchestratorOptions.Load(), store, new RateLimitStatusWriter(), Ctx.Lease(store)).Refresh(DateTime.UtcNow);
        var b = snapshot.Budget!;
        AnsiConsole.MarkupLine(Line(b, snapshot.TakenUtc));
        if (b.CurrentBlock is { } block) AnsiConsole.MarkupLine(BlockLine(b, block, snapshot.TakenUtc));
        AnsiConsole.MarkupLine($"  today {Format.Tokens(b.Today.Fresh)} uncached ({Format.Tokens(b.Today.CacheRead)} cache reads)");
        AnsiConsole.MarkupLine($"  {WeekLine(b)}");
        AnsiConsole.MarkupLine($"  limits: {Markup.Escape(SourceLine(b))}");
        foreach (var p in snapshot.ProviderUsage.Where(p => p.Provider != Providers.Claude))
            AnsiConsole.MarkupLine($"  {p.Provider}: {Format.Tokens(p.Today.Fresh)} uncached today, {Format.Tokens(p.Week.Fresh)} this week" +
                string.Concat(p.Limits.OrderBy(l => l.Window).Select(l => $" · {l.Window} {l.UsedPercent:0}% (its own count, resets {l.ResetsUtc.ToLocalTime():ddd h:mm tt})")));
        if (snapshot.Pace is { } pace) AnsiConsole.MarkupLine($"  {PaceLine(pace)}");
        if (settings.Hits) WriteHits(b.Calibration);
        if (settings.Week) WriteWeek(Burndown.Compute(b, snapshot.History, snapshot.TakenUtc, snapshot.Account));
        return 0;
    }

    /// <summary>The block's span and burn rate; a time on another day (a block begun before midnight, a reset or limit after it) names its weekday (M.ORCH-T437).</summary>
    public static string BlockLine(BudgetStatus b, UsageBlock block, DateTime nowUtc) =>
        $"  block {Formatting.ResetTime(block.StartUtc, nowUtc)}–{Formatting.ResetTime(block.EndUtc, nowUtc)} · burning {Format.Tokens((long)b.RecentFreshPerHour)}/h now, {Format.Tokens((long)b.FreshPerHour)}/h this block" +
        (b.ProjectedLimitUtc is { } p ? $" · [red]limit at this pace: {Formatting.ResetTime(p, nowUtc)}[/]" : "");

    public static string PaceLine(Pace pace)
    {
        var color = pace.Mode switch { PaceMode.Open => "green", PaceMode.Throttled => "yellow", PaceMode.Coast or PaceMode.Over => "red", _ => "grey" };
        return $"[{color}]pace: {pace.Key}[/] {Markup.Escape(pace.Note)}" + (pace.Advise ? " [grey](running agents are asked to conserve)[/]" : "");
    }

    public static string SourceLine(BudgetStatus b)
    {
        string Of(LimitSource? source, long? limit, LimitKind kind, bool outdated, long? old) => source switch
        {
            LimitSource.Manual => $"{Format.Tokens(limit ?? 0)} (set)",
            LimitSource.Reported => $"{Format.Tokens(limit ?? 0)} (from Claude's own usage percent)",
            LimitSource.Learned => $"{Format.Tokens(limit ?? 0)} (learned from {b.Calibration?.Basis(kind)} limit hits)",
            _ when kind == LimitKind.Weekly && b.WeeklyLedgerPartial => "Claude's percent alone (the token ledger starts at the account switch, part way through the week)",
            _ when outdated => $"relearning (already past the {Format.Tokens(old ?? 0)} learned earlier, with no wall)",
            _ => "none",
        };
        return $"5h {Of(b.BlockLimitIsPeak ? null : b.BlockLimitSource, b.BlockLimit, LimitKind.Session, b.BlockLimitOutdated, b.Calibration?.Learned5h)} · " +
               $"weekly {Of(b.WeeklyLimitSource, b.WeeklyLimit, LimitKind.Weekly, b.WeeklyLimitOutdated, b.Calibration?.LearnedWeekly)}";
    }

    private static void WriteWeek(Burndown w)
    {
        var max = Math.Max(1, w.Days.Max(d => d.Fresh));
        foreach (var (day, fresh) in w.Days)
            AnsiConsole.MarkupLine($"  {day:ddd MM-dd} {new string('█', (int)Math.Round(30.0 * fresh / max)),-30} {Format.Tokens(fresh)}");
        var pace = $"{Format.Tokens((long)(w.PerHour * 24))}/day";
        AnsiConsole.MarkupLine(w switch
        {
            { Limit: null } => $"  [grey]{Format.Tokens(w.Used)} this week at {pace}; no weekly limit known yet, so no projection[/]",
            { RunsOut: true } => $"  [red]at {pace} the weekly limit ({Format.Tokens(w.Limit!.Value)}) runs out {w.ExhaustedUtc!.Value.ToLocalTime():ddd h:mm tt}, before the {w.ResetUtc!.Value.ToLocalTime():ddd h:mm tt} reset[/]",
            _ => $"  [green]at {pace} the week ends with room to spare[/] ({Format.Tokens(w.Used)} of {Format.Tokens(w.Limit!.Value)})",
        });
    }

    private static void WriteHits(LimitCalibration? calibration)
    {
        if (calibration is null || calibration.Observations.Count == 0) { AnsiConsole.MarkupLine("  [grey]no limit hits recorded yet[/]"); return; }
        var table = new Table().Border(TableBorder.Rounded).AddColumns("Limit", "Hit", "Reset", "Uncached used", "");
        foreach (var o in calibration.Observations.OrderByDescending(o => o.HitUtc))
            table.AddRow(o.Kind.ToString(), $"{o.HitUtc.ToLocalTime():ddd MM-dd h:mm tt}", $"{o.ResetUtc.ToLocalTime():ddd MM-dd h:mm tt}",
                Format.Tokens(o.FreshUsed), o.Covered ? "" : "[grey]partial: transcripts start later[/]");
        AnsiConsole.Write(table);
        AnsiConsole.MarkupLine($"  [grey]learned = median of the {LimitCalibration.RecentHits} most recent complete hits of each kind[/]");
    }

    public static string WeekLine(BudgetStatus b)
    {
        var used = $"{Format.Tokens(b.Week.Fresh)} uncached" + (b.WeeklyPercent is { } wp ? $" ({wp:0}% of weekly)" : "");
        return b.WeekResetUtc is { } next
            ? $"this week (since {b.WeekStartUtc.ToLocalTime():ddd h:mm tt}) {used} · resets {next.ToLocalTime():ddd h:mm tt}"
            : $"last 7 days {used} [grey]· rolling: set the real reset with --weekly-reset \"Mon 9:00\"[/]";
    }

    /// <param name="nowUtc">When the times are read: one on another day names its weekday (M.ORCH-T437).</param>
    public static string Line(BudgetStatus b, DateTime nowUtc)
    {
        if (b.CurrentBlock is not { } block) return "[grey]budget: no active 5-hour block[/]";
        var used = $"{Format.Tokens(block.Usage.Fresh)} uncached this block, resets {Formatting.ResetTime(block.EndUtc, nowUtc)}";
        if (b.BlockLimitIsPeak)
            return $"[aqua]this block is at {b.BlockPercent:0}% of your heaviest block in 7 days[/] ({used}) [grey]· no budget set: `orchestrator budget --block <tokens>`[/]";
        var color = b.Level switch { BudgetLevel.Over => "red", BudgetLevel.Warn => "yellow", _ => "green" };
        var what = b.BlockLimitSource switch { LimitSource.Learned => "learned 5h limit", LimitSource.Reported => "5h limit (Claude's count)", _ => "5h budget" };
        var wall = b.AtLimitUntilUtc is { } until ? $" [red]· at the {(b.AtLimitKind == LimitKind.Weekly ? "weekly" : "5-hour")} limit until {Formatting.ResetTime(until, nowUtc)}[/]" : "";
        return $"[{color}]budget: {Formatting.FiveHourPercent(b.BlockPercent)} of your {what}[/] ({Format.Tokens(block.Usage.Fresh)} of {Format.Tokens(b.BlockLimit)}; {used}){wall}";
    }
}

internal static class Format
{
    public static string Tokens(long n) => Formatting.Tokens(n);

    public static string Ago(DateTime? utc, DateTime nowUtc) => Formatting.Ago(utc, nowUtc);

    public static string Sessions(RepoStatus r)
    {
        var parts = new List<string>();
        if (r.Hosts.Count > 0) parts.Add(string.Join(", ", r.Hosts.GroupBy(h => h.Provider).Select(g => $"{g.Count()} {g.Key} tab")));
        var recent = r.Sessions.Count(s => s.LastWriteUtc >= DateTime.UtcNow.AddMinutes(-30));
        if (recent > 0) parts.Add($"{recent} recent");
        return string.Join(" · ", parts);
    }
}
