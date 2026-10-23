using System.ComponentModel;
using MindAttic.Orchestrator.Budget;
using MindAttic.Orchestrator.Scheduling;
using MindAttic.Orchestrator.Sessions;
using Spectre.Console;
using Spectre.Console.Cli;

/// <summary>
/// <c>orchestrator org</c> (M.ORCH-T686): the organization, behind the global /org and /organization commands. <c>usage</c> is
/// <c>orchestrator report</c> (<see cref="UsageReportCommand"/>), <c>members</c> is <c>orchestrator members</c>, <c>login &lt;who&gt;</c>
/// is <c>orchestrator login --account &lt;email&gt;</c> and <c>start &lt;repo&gt; &lt;who&gt;</c> is <c>orchestrator start &lt;repo&gt;
/// --account &lt;email&gt;</c>, with &lt;who&gt; any way of naming a member (<see cref="MemberLookup"/>). Bare, or <c>help</c>, lists them.
/// </summary>
internal sealed class OrgHelpCommand : Command
{
    internal static readonly (string Use, string What)[] Subcommands =
    [
        ("usage [--detail] [--since <date>] [--csv|--json] [--md|--html <file>]", "every member's 5-hour and weekly use, resets, points left to the weekly target, reading age and whether it's accurate"),
        ("login <who>", "sign that member's own login in (opens the browser: run it in your own terminal)"),
        ("members", "list the members; members add|set|remove change them"),
        ("start <repo> <who>", "start a CLI in <repo> on that member's login"),
        ("allow <who> [--until <reset|time>]", "let that member's login take work past its weekly target, until its weekly reset (the default) or the time given"),
        ("disallow <who>", "take that back: the login stops at its weekly target again"),
        ("limit [<who> --weekly <pct> --5h <pct> | --reset]", "each member's usage cutoffs (90% of the week and 98% of the 5-hour window by default); with <who>, set them or put the defaults back with --reset"),
        ("help", "this list"),
    ];

    public override int Execute(CommandContext context)
    {
        AnsiConsole.MarkupLine("[bold]orchestrator org[/] [grey](/org or /organization in a Claude CLI)[/]");
        foreach (var (use, what) in Subcommands)
            AnsiConsole.MarkupLine($"  [bold]{Markup.Escape(use)}[/]  {Markup.Escape(what)}");
        AnsiConsole.MarkupLine("[grey]<who> is a member's email, the part before the @, name, first name, login alias or initials (RD, E), any case.[/]");
        return 0;
    }

    /// <summary>The people <c>&lt;who&gt;</c> is matched against; tests give their own (never this machine's).</summary>
    internal static Func<IReadOnlyList<OrgPerson>>? PeopleOverride { get; set; }

    /// <summary>The one member <paramref name="who"/> names, or null with why said in red: nobody, or several (each listed).</summary>
    internal static OrgPerson? Resolve(string who)
    {
        var people = PeopleOverride?.Invoke() ?? MemberLookup.People(MembersCommand.Db(), LoginCommand.DirsOverride?.Invoke() ?? new AccountDirs());
        var found = MemberLookup.Match(people, who);
        if (found.Count == 1) return found[0];
        AnsiConsole.MarkupLine(found.Count > 1
            ? $"[red]\"{Markup.Escape(who.Trim())}\" could be more than one member: name one of them.[/]"
            : $"[red]No member is \"{Markup.Escape(who.Trim())}\": name one by email, the part before the @, name, first name, login alias or initials.[/]");
        foreach (var p in found.Count > 1 ? found : people)
            AnsiConsole.MarkupLine($"  {Markup.Escape(p.Label)}");
        return null;
    }
}

/// <summary><c>orchestrator org login &lt;who&gt; [--sso]</c>: <c>orchestrator login --account &lt;their email&gt;</c>.</summary>
internal sealed class OrgLoginCommand : Command<OrgLoginCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<WHO>")]
        [Description("The member: email, the part before the @, name (Ryan DeBraal, quoted or not), first name, login alias or initials.")]
        public string[] Who { get; init; } = [];

        [CommandOption("--sso")]
        [Description("Force the SSO login flow.")]
        public bool Sso { get; init; }
    }

    public override int Execute(CommandContext context, Settings settings) =>
        OrgHelpCommand.Resolve(string.Join(' ', settings.Who)) is { } who
            ? new LoginCommand().Execute(context, new LoginCommand.Settings { OwnLogin = who.Email, Sso = settings.Sso })
            : 1;
}

/// <summary>
/// <c>orchestrator org allow &lt;who&gt; [--until reset|&lt;time&gt;]</c> (M.ORCH-T694): a login with less than its weekly reserve
/// left takes no work and its CLIs stop until its weekly reset; this lets that one member's login past the weekly target, until
/// its weekly reset (the default) or the time given (never past the reset). Saved in orchestrator.db, logged, and shown in
/// <c>org usage</c>. Only the user does this: nothing else lifts the target.
/// </summary>
internal sealed class OrgAllowCommand : Command<OrgAllowCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<WHO>")]
        [Description("The member: email, the part before the @, name (quoted or not), first name, login alias or initials.")]
        public string[] Who { get; init; } = [];

        [CommandOption("--until <WHEN>")]
        [Description("\"reset\" (the default): until the login's weekly reset; or a local time (\"5:00 PM\", \"Tue 5:00 PM\", \"2026-10-27 17:00\"), never past the reset.")]
        public string? Until { get; init; }
    }

    /// <summary>The login's next weekly reset by email; tests give their own (never this machine's).</summary>
    internal static Func<string, DateTime, DateTime?>? WeekResetOverride { get; set; }

    /// <summary>The clock; tests set their own.</summary>
    internal static Func<DateTime>? NowOverride { get; set; }

    internal static DateTime Now => NowOverride?.Invoke() ?? DateTime.UtcNow;

    public override int Execute(CommandContext context, Settings settings)
    {
        if (OrgHelpCommand.Resolve(string.Join(' ', settings.Who)) is not { } who) return 1;
        var now = Now;
        var target = Ctx.Settings.WeeklyStopPercent;
        var name = OrgDisallowCommand.NameOf(who);
        var reset = (WeekResetOverride ?? WeekReset)(who.Email, now);
        DateTime until;
        if (settings.Until is null || settings.Until.Trim().Equals("reset", StringComparison.OrdinalIgnoreCase))
        {
            if (reset is not { } r)
            {
                AnsiConsole.MarkupLine($"[red]{Markup.Escape(name)}'s weekly reset isn't known here (its meter was never read): give --until <time>.[/]");
                return 1;
            }
            until = r;
        }
        else if (ParseUntil(settings.Until, now) is { } at)
        {
            if (at <= now) { AnsiConsole.MarkupLine($"[red]--until {Markup.Escape(settings.Until)} is already past.[/]"); return 1; }
            until = at;
        }
        else
        {
            AnsiConsole.MarkupLine($"[red]--until is \"reset\" or a local time (\"5:00 PM\", \"Tue 5:00 PM\", \"2026-10-27 17:00\"), not \"{Markup.Escape(settings.Until)}\".[/]");
            return 1;
        }
        var (allowance, line) = WeeklyAllowances.Allow(Ctx.Store(), who.Email, name, until, reset, target, now);
        AnsiConsole.MarkupLine(Markup.Escape(line));
        if (reset is { } wr && until > wr)
            AnsiConsole.MarkupLine($"[grey]It ends at its weekly reset ({Markup.Escape(WeeklyAllowances.When(allowance.EndsUtc))}), not later: the new week is held to the target again.[/]");
        AnsiConsole.MarkupLine($"[grey]Shown in /org usage; /org disallow {Markup.Escape(string.Join(' ', settings.Who))} takes it back.[/]");
        return 0;
    }

    /// <summary>A local time typed for --until, as UTC: a date and time, a weekday and time (the next one), or a time alone
    /// (today's, else tomorrow's once it has passed); null when it isn't one.</summary>
    internal static DateTime? ParseUntil(string text, DateTime nowUtc)
    {
        var t = text.Trim();
        var local = nowUtc.ToLocalTime();
        var styles = System.Globalization.DateTimeStyles.AssumeLocal | System.Globalization.DateTimeStyles.AllowWhiteSpaces;
        var parts = t.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 2 && DayOf(parts[0]) is { } day &&
            TimeOnly.TryParse(parts[1], System.Globalization.CultureInfo.InvariantCulture, out var dayTime))
        {
            var date = local.Date.AddDays(((int)day - (int)local.DayOfWeek + 7) % 7) + dayTime.ToTimeSpan();
            if (date <= local) date = date.AddDays(7);
            return DateTime.SpecifyKind(date, DateTimeKind.Local).ToUniversalTime();
        }
        if (TimeOnly.TryParse(t, System.Globalization.CultureInfo.InvariantCulture, out var time))
        {
            var date = local.Date + time.ToTimeSpan();
            if (date <= local) date = date.AddDays(1);
            return DateTime.SpecifyKind(date, DateTimeKind.Local).ToUniversalTime();
        }
        if (DateTime.TryParse(t, System.Globalization.CultureInfo.InvariantCulture, styles, out var at) ||
            DateTime.TryParse(t, System.Globalization.CultureInfo.CurrentCulture, styles, out at))
            return at.ToUniversalTime();
        return null;

        static DayOfWeek? DayOf(string word)
        {
            var w = word.TrimEnd(',', '.');
            if (w.Length < 3) return null;
            foreach (var d in Enum.GetValues<DayOfWeek>())
                if (d.ToString().StartsWith(w, StringComparison.OrdinalIgnoreCase)) return d;
            return null;
        }
    }

    /// <summary>The login's next weekly reset by Claude's meter: its own login's reading here, else the account roster's last
    /// (rolled on a week at a time), else the global login's when it's signed in as them; null when none was ever read.</summary>
    private static DateTime? WeekReset(string email, DateTime nowUtc)
    {
        var resets = new List<DateTime>();
        try
        {
            var dirs = LoginCommand.DirsOverride?.Invoke() ?? new AccountDirs();
            if (ReportedUsage.LoadAnyAge(dirs.For(email).UsagePath)?.Week?.ResetUtc is { } own) resets.Add(own);
            if (TicketPins.Same(LoginLook.GlobalEmail(dirs), email) && ReportedUsage.Load(nowUtc)?.Week?.ResetUtc is { } global) resets.Add(global);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException) { }
        try
        {
            foreach (var row in AccountRoster.Load(MembersCommand.Db()).Where(r => TicketPins.Same(r.Email, email)))
                if (row.WeekResetUtc is { } r) resets.Add(r);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Data.Common.DbException) { }
        if (resets.Count == 0) return null;
        // The latest reading's reset, rolled on by whole weeks once it has passed.
        return new AccountRow { WeekResetUtc = resets.Max() }.WeekResetAt(nowUtc);
    }
}

/// <summary><c>orchestrator org disallow &lt;who&gt;</c> (M.ORCH-T694): takes back <c>org allow</c>, so that member's login stops
/// at its weekly target again; logged.</summary>
internal sealed class OrgDisallowCommand : Command<OrgDisallowCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<WHO>")]
        [Description("The member: email, the part before the @, name (quoted or not), first name, login alias or initials.")]
        public string[] Who { get; init; } = [];
    }

    internal static string NameOf(OrgPerson who) => who.Name is { Length: > 0 } n ? n : LoginLook.DefaultAlias(who.Email) ?? who.Email;

    public override int Execute(CommandContext context, Settings settings)
    {
        if (OrgHelpCommand.Resolve(string.Join(' ', settings.Who)) is not { } who) return 1;
        var name = NameOf(who);
        var line = WeeklyAllowances.Disallow(Ctx.Store(), who.Email, name, Ctx.Settings.WeeklyStopPercent, OrgAllowCommand.Now);
        AnsiConsole.MarkupLine(line is null
            ? $"{Markup.Escape(name)}'s login isn't allowed past the weekly target: nothing to take back."
            : Markup.Escape(line));
        return 0;
    }
}

/// <summary>
/// <c>orchestrator org limit [&lt;who&gt; [--weekly &lt;pct&gt;] [--5h &lt;pct&gt;] | --reset]</c> (M.ORCH-T707): each member's usage
/// cutoffs. Alone it lists every member's; with a member it sets the cutoffs given, or <c>--reset</c> puts back the defaults (90% of
/// the week, 98% of the 5-hour window). The only way to change a cutoff. Below a cutoff nothing throttles; at it, access is cut off.
/// </summary>
internal sealed class OrgLimitCommand : Command<OrgLimitCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "[WHO]")]
        [Description("The member: email, the part before the @, name (quoted or not), first name, login alias or initials. Left out: list every member's cutoffs.")]
        public string[] Who { get; init; } = [];

        [CommandOption("--weekly <PERCENT>")]
        [Description("The percent of the member's week where its access is cut off (50 to 100; 90 by default).")]
        public int? Weekly { get; init; }

        [CommandOption("--five-hour <PERCENT>")] // typed --5h: see CliApp.Runner
        [Description("The percent of the member's 5-hour window where its access is cut off (1 to 100; 98 by default).")]
        public int? FiveHour { get; init; }

        [CommandOption("--reset")]
        [Description("Put the member's cutoffs back to the defaults, 90 (week) and 98 (5-hour).")]
        public bool Reset { get; init; }
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        var store = Ctx.Store();
        if (settings.Who.Length == 0)
        {
            if (settings.Weekly is not null || settings.FiveHour is not null || settings.Reset)
            {
                AnsiConsole.MarkupLine("[red]Name the member: org limit <who> --weekly <pct> --5h <pct>, or --reset.[/]");
                return 1;
            }
            var cutoffs = UsageCutoffs.Load(store);
            var people = OrgHelpCommand.PeopleOverride?.Invoke() ?? MemberLookup.People(MembersCommand.Db(), LoginCommand.DirsOverride?.Invoke() ?? new AccountDirs());
            foreach (var p in people)
            {
                var custom = UsageCutoffs.Custom(cutoffs, p.Email) is not null;
                AnsiConsole.MarkupLine($"  {Markup.Escape(OrgDisallowCommand.NameOf(p))} ({Markup.Escape(p.Email)}): " +
                    Markup.Escape(UsageCutoffs.Text(UsageCutoffs.WeeklyFor(cutoffs, p.Email), UsageCutoffs.FiveHourFor(cutoffs, p.Email))) + (custom ? " (set)" : " (default)"));
            }
            AnsiConsole.MarkupLine("[grey]Below a cutoff nothing throttles; at it, access is cut off and you are asked. org limit <who> --weekly <pct> --5h <pct> changes one; --reset puts back 90 and 98.[/]");
            return 0;
        }
        if (settings.Reset && (settings.Weekly is not null || settings.FiveHour is not null))
        {
            AnsiConsole.MarkupLine("[red]--reset takes no percents.[/]");
            return 1;
        }
        if (!settings.Reset && settings.Weekly is null && settings.FiveHour is null)
        {
            AnsiConsole.MarkupLine("[red]Give --weekly <pct>, --5h <pct> or --reset.[/]");
            return 1;
        }
        if (settings.Weekly is { } w && w is < UsageCutoffs.MinWeeklyPercent or > 100)
        {
            AnsiConsole.MarkupLine($"[red]--weekly is {UsageCutoffs.MinWeeklyPercent} to 100.[/]");
            return 1;
        }
        if (settings.FiveHour is { } f && f is < UsageCutoffs.MinFiveHourPercent or > 100)
        {
            AnsiConsole.MarkupLine($"[red]--5h is {UsageCutoffs.MinFiveHourPercent} to 100.[/]");
            return 1;
        }
        if (OrgHelpCommand.Resolve(string.Join(' ', settings.Who)) is not { } who) return 1;
        var name = OrgDisallowCommand.NameOf(who);
        var result = settings.Reset ? UsageCutoffs.Reset(store, who.Email, name) : UsageCutoffs.Set(store, who.Email, name, settings.Weekly, settings.FiveHour);
        var line = $"{name} ({who.Email}) cutoffs{(settings.Reset ? " back to the defaults" : "")}: {UsageCutoffs.Text(result.WeeklyPercent, result.FiveHourPercent)}.";
        AnsiConsole.MarkupLine(Markup.Escape(line));
        try { MindAttic.Orchestrator.OrchestratorLog.For("Org").Information("{Cutoff:l}", line); }
        catch (Exception ex) when (ex is IOException or InvalidOperationException) { }
        return 0;
    }
}

/// <summary><c>orchestrator org start &lt;repo&gt; &lt;who&gt; [--prompt]</c>: <c>orchestrator start &lt;repo&gt; --account &lt;their email&gt;</c>.</summary>
internal sealed class OrgStartCommand : Command<OrgStartCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<REPO>")]
        public string Repo { get; init; } = "";

        [CommandArgument(1, "<WHO>")]
        [Description("The member whose login the CLI runs on: email, the part before the @, name (quoted or not), first name, login alias or initials.")]
        public string[] Who { get; init; } = [];

        [CommandOption("--prompt <TEXT>")]
        public string? Prompt { get; init; }
    }

    public override int Execute(CommandContext context, Settings settings) =>
        OrgHelpCommand.Resolve(string.Join(' ', settings.Who)) is { } who
            ? new StartCommand().Execute(context, new StartCommand.Settings { Repo = settings.Repo, Account = who.Email, Prompt = settings.Prompt })
            : 1;
}
