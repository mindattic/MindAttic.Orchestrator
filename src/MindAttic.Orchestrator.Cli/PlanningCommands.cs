using System.ComponentModel;
using MindAttic.Orchestrator;
using MindAttic.Orchestrator.Scheduling;
using MindAttic.Orchestrator.Settings;
using Spectre.Console;
using Spectre.Console.Cli;

/// <summary>Which CLIs have /allocate on, and what each would hand its subagents next, without claiming anything.</summary>
internal sealed class DryRunCommand : Command
{
    public override int Execute(CommandContext context)
    {
        var (_, store, snapshot, plan) = Ctx.Load();
        var cycler = new MindAttic.Orchestrator.Sessions.ContextCycler();
        var clis = cycler.AllocateClis();
        if (clis.Count == 0) { AnsiConsole.MarkupLine("[grey]/allocate is off in every CLI; type /allocate (optionally -repo=A,B) in the CLI that should work the tickets.[/]"); return 0; }
        var since = cycler.AllocateSince();
        var own = cycler.OnOwnLogin();
        var orgs = MindAttic.Orchestrator.Sessions.OrgRouting.Load(store, store.LoadSettings(), snapshot.GlobalEmail);
        var dirs = new MindAttic.Orchestrator.Sessions.AccountDirs();
        foreach (var (pid, repos) in clis)
        {
            AnsiConsole.MarkupLine($"/allocate is on in CLI {pid} for {Markup.Escape(string.Join(", ", repos))}:");
            var self = cycler.LatestSessionOf(pid);
            // Its login (M.ORCH-T668): a ticket assigned to a user goes only to a CLI on theirs.
            var login = (self is not null && own.TryGetValue(self, out var dir) ? dirs.Of(dir)?.Email : null) ?? snapshot.GlobalEmail;
            var (handed, why) = Allocate.Next(plan, store.LoadSettings(), store, repos, null, claim: false, DateTime.UtcNow,
                self: self,
                cli: pid, pilots: clis, onSince: since, login: login, orgs: orgs);
            foreach (var h in handed) AnsiConsole.MarkupLine($"  next: {Markup.Escape(h.Item.Id)} in {Markup.Escape(h.Item.Repo)}" +
                (h.Model is { } m ? $" on {m.Model}" : "") + $": {Markup.Escape(h.Item.Ticket.Summary)}");
            if (why is not null) AnsiConsole.MarkupLine($"  [grey]{Markup.Escape(why)}[/]");
        }
        AnsiConsole.MarkupLine("[grey]Each CLI hands these to its own subagents (orchestrator next --claim). Nothing was claimed.[/]");
        return 0;
    }
}

/// <summary>The next tickets a CLI running /allocate hands to its subagents: each with its worktree and full prompt when claimed.</summary>
internal sealed class NextCommand : Command<NextCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("--repos <REPOS>")]
        [Description("Repos to take tickets from, comma-separated (also --repo), wildcards allowed (\"GTFO.*\"); \"*\" for every repo. Default: the repo of the current folder.")]
        public string? Repos { get; init; }

        [CommandOption("--repo <REPOS>")]
        public string? Repo { get; init; }

        [CommandOption("--count <N>")]
        [Description("How many to hand out (default: as many as the pace's subagent allowance has room for).")]
        public int? Count { get; init; }

        [CommandOption("--claim")]
        [Description("Claim them: make each its worktree, record it as assigned, and print the subagent's prompt.")]
        public bool Claim { get; init; }
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        // The calling CLI, as the pace gate finds it: MINDATTIC_CLI_PID, else the claude.exe this runs under (M.ORCH-T195).
        var cli = int.TryParse(Environment.GetEnvironmentVariable("MINDATTIC_CLI_PID"), out var envPid) && envPid > 0 ? envPid
            : MindAttic.Orchestrator.Sessions.ProcessTree.ClaudeAncestor(Environment.ProcessId);
        // Planned on the account that CLI itself runs on, not the global or latest login (M.ORCH-T673).
        var (_, store, snapshot, plan) = Ctx.Load(cli);
        IReadOnlyList<string> repos = (settings.Repos ?? settings.Repo) is { } list
            ? [.. list.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)]
            : Allocate.RepoFor(Environment.CurrentDirectory, snapshot) is { } here ? [here.Name] : [];
        if (repos.Count == 0) { Console.Error.WriteLine("No repo here; name them with --repos A,B (or \"*\")."); return 1; }
        // A name that matches no repo is said as one, not read as a repo that ran dry with a bug hunt offered (M.ORCH-T728).
        if (Allocate.Unmatched(repos, snapshot) is { Count: > 0 } unknown)
        {
            Console.Error.WriteLine($"No repo named {string.Join(", ", unknown)} in the workspace.");
            if (unknown.Count == repos.Count) return 1;
            repos = [.. repos.Except(unknown)];
        }
        var cycler = new MindAttic.Orchestrator.Sessions.ContextCycler();
        var unthrottled = cli is { } pid && cycler.IsUnthrottled(pid);
        // What the calling CLI filed itself isn't held from its own subagents; what anyone else (its subagents included) filed
        // and is fixing is (M.ORCH-T255).
        var self = cli is { } me ? cycler.LatestSessionOf(me) : null;
        var orchestratorSettings = store.LoadSettings();
        // Weekly-aware pacing (M.ORCH-T666): a ticket this CLI's login can't fit goes to another signed-in login with room (a tab
        // opened on it), else the fallback login.
        var dirs = new MindAttic.Orchestrator.Sessions.AccountDirs();
        // The calling CLI's login (M.ORCH-T668), as its plan was made (M.ORCH-T673): its recorded per-CLI login, else this process's
        // CLAUDE_CONFIG_DIR's, else the global login's. A ticket assigned to a user is handed only to a CLI on theirs.
        var own = Ctx.CurrentLogin is { } seam ? seam() : Ctx.LoginOfCli(cli) ?? dirs.Current();
        var login = own?.Email ?? snapshot.GlobalEmail;
        // A login a ticket was just sent to (picked within LoginPicker.ClaimFor) takes no second one until its CLI's spend shows.
        var justPicked = dirs.All().Where(d => MindAttic.Orchestrator.Sessions.LoginPicker.ClaimedAt(d) is { } at &&
                DateTime.UtcNow - at < MindAttic.Orchestrator.Sessions.LoginPicker.ClaimFor)
            .Select(d => d.Email).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var logins = snapshot.Accounts.Where(a => own is null || !a.Email.Equals(own.Email, StringComparison.OrdinalIgnoreCase))
            .Select(a => LoginRoom.Of(a, orchestratorSettings))
            .Select(l => l.Email is { } e && justPicked.Contains(e) ? l with { Open = false } : l).ToList();
        string? Redelegate(QueueItem item, LoginRoom to)
        {
            if (to.Email is { } email && dirs.All().FirstOrDefault(d => d.Email.Equals(email, StringComparison.OrdinalIgnoreCase)) is { } account)
            {
                try { dirs.Prepare(account); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return $"couldn't prepare {email}'s login: {ex.Message}"; }
                MindAttic.Orchestrator.Sessions.LoginPicker.Claim(account, DateTime.UtcNow);
            }
            var r = Ctx.Dispatcher(store).Dispatch(item, MindAttic.Orchestrator.Providers.Claude, useWorktree: true, submit: true,
                login: new Dispatcher.TabLogin(to.ConfigDir, to.Email));
            return r.Ok ? null : r.Message;
        }
        var (all, why) = Allocate.Next(plan, orchestratorSettings, store, repos, settings.Count, settings.Claim, DateTime.UtcNow,
            unthrottled: unthrottled, self: self,
            cli: cli, pilots: cycler.AllocateClis(), onSince: cycler.AllocateSince(), loginOf: cycler.LoginOfCli(),
            login: login,
            logins: logins, redelegate: Redelegate, canFallBack: own is not null && snapshot.Billing?.Mode == MindAttic.Orchestrator.Billing.BillingModes.Api,
            orgs: MindAttic.Orchestrator.Sessions.OrgRouting.Load(store, orchestratorSettings, snapshot.GlobalEmail));
        foreach (var d in all.Where(h => h.DelegatedTo is not null))
            Console.WriteLine($"=== {d.Item.Id} ({d.Item.Repo}) {d.Delegated}. A tab opened on {d.DelegatedTo!.Label} works it: it isn't this CLI's to start.");
        var handed = all.Where(h => h.DelegatedTo is null).ToList();
        foreach (var h in handed)
        {
            Console.WriteLine($"=== {h.Item.Id} ({h.Item.Repo}, {h.Item.Ticket.Type}): {h.Item.Ticket.Summary}");
            // M.ORCH-T563: the model /allocate starts this ticket's subagent on (the Agent tool's model).
            if (h.Model is { } model) Console.WriteLine(ModelChoice.Line(model));
            if (h.Worktree is not null) Console.WriteLine($"worktree: {h.Worktree}");
            if (h.Prompt is not null) Console.WriteLine("prompt:\n" + h.Prompt + "\n");
        }
        if (why is not null) Console.WriteLine($"Nothing handed out: {why}.");
        // M.ORCH-T529: a CLI held two claims unstarted to avoid merge conflicts while the window went under its target.
        else if (settings.Claim && handed.Count > 0) Console.WriteLine(Allocate.StartAllLine(handed.Count));
        return 0;
    }
}

/// <summary>A Sonnet ticket's one Opus review of its diff before it lands (M.ORCH-T563), run from the ticket's worktree.</summary>
internal sealed class ReviewCommand : Command<ReviewCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<TICKET>")]
        public string Ticket { get; init; } = "";

        [CommandOption("--dry-run")]
        [Description("Only say whether the diff gets its review, and why.")]
        public bool DryRun { get; init; }

        [CommandOption("--force")]
        [Description("Review it whatever its size or model, even a second time.")]
        public bool Force { get; init; }
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        var store = Ctx.Store();
        var id = Ctx.Id(settings.Ticket);
        var root = OrchestratorOptions.Load(store).WorkspaceRoot;
        MindAttic.Orchestrator.Tickets.TicketQueue.Found? found;
        using (var db = MindAttic.Tickets.Data.TicketsDb.OpenIfMigrated(root))
            found = MindAttic.Orchestrator.Tickets.TicketQueue.Find(db, root, id);
        var outcome = DiffReview.Review(store, store.LoadSettings(), Environment.CurrentDirectory, id, found?.Ticket, found?.Repo,
            settings.Force, settings.DryRun, DateTime.UtcNow);
        Console.WriteLine(outcome.Text);
        return 0;
    }
}

internal static class TokenText
{
    /// <summary>The most tokens that can be typed: a quadrillion, far past any limit, with room for thousands of them summed.</summary>
    public const double MaxTokens = 1e15;

    /// <summary>20000000, 20M, 1.5B, 500k.</summary>
    public static bool TryParse(string text, out long value)
    {
        value = 0;
        var t = text.Trim().Replace(",", "").Replace("_", "");
        if (t.Length == 0) return false;
        var scale = char.ToUpperInvariant(t[^1]) switch { 'K' => 1e3, 'M' => 1e6, 'B' => 1e9, _ => 1 };
        if (scale != 1) t = t[..^1];
        if (!double.TryParse(t, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var n) || !double.IsFinite(n) || n < 0) return false;
        // Under one token would round to 0, which clears a budget or releases a reservation; only a plain 0 means that.
        if (n * scale is > 0 and < 1) return false;
        // Past a quadrillion is a typo, not a budget: it was taken as long.MaxValue, and two such reservations overflowed the
        // scheduler's sum of them, so every plan threw (M.ORCH-T458).
        if (n * scale > MaxTokens) return false;
        value = (long)(n * scale);
        return true;
    }
}
