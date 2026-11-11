using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using MindAttic.Orchestrator.Settings;

namespace MindAttic.Orchestrator.Setup;

/// <summary>
/// The daemon's daily SQL Server check (M.ORCH-T1488): keeps setup-host.ps1's SQL Server bootstrappers (its six
/// <c>$SseiDev*</c>/<c>$SseiExpr*</c> lines: the winget package, version and installer URL of SQL Server Developer, and of Express,
/// whose bootstrapper fetches LocalDB) as fresh as winget's. A newer version, or a new bootstrapper URL at the same version, of the
/// same package is written into the script, committed (that one path only) and pushed, and the admin is told. A newer major (another
/// year's package) is only told, once: the script's other SQL Server steps name 2025 and its 170 folder, so a port is a person's job.
/// Off unless <see cref="OrchestratorSettings.SqlFreshnessCheck"/>; runs once a day at <see cref="OrchestratorSettings.SqlFreshnessCheckAt"/>.
/// </summary>
public sealed partial class SqlServerFreshness(IProcessRunner runner, JsonStore store)
{
    public const string ScriptName = "setup-host.ps1";

    /// <summary>The store's record of the last day it ran and the newer majors already told.</summary>
    public const string StateName = "sql-freshness";

    internal const string CommitSubject = "setup-host.ps1: SQL Server bootstrappers updated from winget by the daily check (M.ORCH-T1488)";

    internal static readonly TimeSpan WingetTimeout = TimeSpan.FromMinutes(3);
    internal static readonly TimeSpan GitTimeout = TimeSpan.FromMinutes(2);

    public sealed class State
    {
        /// <summary>The local day (yyyy-MM-dd) the check last ran.</summary>
        public string? LastRunDay { get; set; }

        /// <summary>The newer-major package IDs the admin was told of already.</summary>
        public List<string> ToldMajors { get; set; } = [];
    }

    /// <summary>One bootstrapper the script pins: its winget package, version and installer URL.</summary>
    public sealed record Pin(string Package, string Version, string Url);

    /// <summary>What winget says of a package.</summary>
    public sealed record Listing(string Package, string Version, string Url);

    /// <summary>The script's two pins: SQL Server Developer (the localhost instance) and Express (LocalDB).</summary>
    public sealed record Pins(Pin Developer, Pin Express);

    /// <summary>A check's outcome: the rewritten script (null when nothing changed), what changed, the newer majors seen, and
    /// why it couldn't check, if it couldn't.</summary>
    public sealed record Outcome(string? Script, IReadOnlyList<string> Changes, IReadOnlyList<Listing> NewerMajors, string? Problem = null)
    {
        /// <summary>The year the script's Developer pin names (2025).</summary>
        public int Base { get; init; }

        internal string Installs => Base is > 0 and < int.MaxValue ? $"SQL Server {Base}" : "the pinned SQL Server";
    }

    /// <summary>Whether the check is due at <paramref name="nowLocal"/>: past today's <paramref name="at"/> (HH:mm; anything else is
    /// <see cref="OrchestratorSettings.DefaultSqlFreshnessCheckAt"/>) and not run yet today. False when the state is kept but can't
    /// be read; none kept yet (the first run ever) reads as never run.</summary>
    public bool Due(string? at, DateTime nowLocal)
    {
        if (nowLocal.TimeOfDay < TimeOfDay(at)) return false;
        if (!store.TryLoad<State>(StateName, out var state) && store.Has(StateName)) return false;
        return state.LastRunDay != Day(nowLocal);
    }

    internal static TimeSpan TimeOfDay(string? at) =>
        TimeOnly.TryParseExact((at ?? "").Trim(), ["H:mm", "HH:mm"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var t) ? t.ToTimeSpan()
            : TimeOnly.ParseExact(OrchestratorSettings.DefaultSqlFreshnessCheckAt, "HH:mm", CultureInfo.InvariantCulture).ToTimeSpan();

    private static string Day(DateTime local) => local.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>
    /// Runs the check against <paramref name="repo"/>'s setup-host.ps1 (the Orchestrator's main checkout): rewrites, commits and
    /// pushes it when winget has newer bootstrappers, and hands <paramref name="notify"/> (key, text) what the admin should hear.
    /// Today is recorded first, so a failure is tried again tomorrow, not every tick. Returns lines for the daemon's log
    /// (<c>Warn</c> marks a problem).
    /// </summary>
    public IReadOnlyList<(bool Warn, string Text)> Run(string repo, DateTime nowLocal, Action<string, string> notify)
    {
        var lines = new List<(bool, string)>();
        State state = null!;
        store.Update<State>(StateName, s => { s.LastRunDay = Day(nowLocal); state = s; });
        var path = Path.Combine(repo, ScriptName);
        if (!File.Exists(path)) return [(true, $"sql check: {path} isn't there; nothing checked")];
        byte[] bytes;
        try { bytes = File.ReadAllBytes(path); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return [(true, $"sql check: couldn't read {path}: {ex.Message}")]; }
        var bom = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
        var script = new UTF8Encoding(false).GetString(bytes, bom ? 3 : 0, bytes.Length - (bom ? 3 : 0));

        var outcome = Check(script);
        if (outcome.Problem is { } problem) return [(true, $"sql check: {problem}")];

        var newMajors = outcome.NewerMajors.Where(m => !state.ToldMajors.Contains(m.Package, StringComparer.OrdinalIgnoreCase)).ToList();
        foreach (var m in newMajors)
        {
            var text = $"{m.Package} {m.Version} is on winget; setup-host.ps1 still installs {outcome.Installs}. Porting it to a new major is by hand.";
            lines.Add((false, "sql check: " + text));
            notify("sql-major|" + m.Package, text);
        }
        if (newMajors.Count > 0)
            store.Update<State>(StateName, s => s.ToldMajors = [.. s.ToldMajors.Union(newMajors.Select(m => m.Package), StringComparer.OrdinalIgnoreCase)]);

        if (outcome.Script is null)
        {
            lines.Add((false, "sql check: setup-host.ps1's SQL Server bootstrappers are the newest winget has"));
            return lines;
        }

        ProcessResult Git(params string[] args) => runner.Run("git", ["-C", repo, .. args], GitTimeout);
        try
        {
            // Never over someone's work: a merge or rebase in progress, or uncommitted edits to the script, wait for tomorrow.
            if (Daemon.GitOperationInProgress(repo)) { lines.Add((true, $"sql check: {repo} is mid-merge or rebase; setup-host.ps1 not updated (tried again tomorrow)")); return lines; }
            var dirty = Git("status", "--porcelain", "--", ScriptName);
            if (dirty.ExitCode != 0 || dirty.TimedOut || dirty.Output.Trim().Length > 0)
            {
                lines.Add((true, $"sql check: setup-host.ps1 has uncommitted changes (or git status failed); not updated (tried again tomorrow): {string.Join("; ", outcome.Changes)}"));
                return lines;
            }
            File.WriteAllText(path, outcome.Script, new UTF8Encoding(bom));
            var add = Git("add", "--", ScriptName);
            var commit = add.ExitCode == 0 && !add.TimedOut ? Git("commit", "-q", "-m", CommitSubject, "--", ScriptName) : add;
            if (commit.ExitCode != 0 || commit.TimedOut)
            {
                Git("reset", "-q", "--", ScriptName);
                File.WriteAllBytes(path, bytes);
                lines.Add((true, $"sql check: couldn't commit setup-host.ps1, put back as it was: {(commit.Output + commit.Error).Trim()}"));
                return lines;
            }
            var hash = Git("rev-parse", "--short", "HEAD").Output.Trim();
            var pushed = Push(Git, out var why);
            var said = $"the host stack's dependencies changed: {string.Join("; ", outcome.Changes)}. setup-host.ps1 updated in {hash}" +
                (pushed ? " and pushed" : $" (not pushed yet: {why}; the daemon's next push carries it)") + ".";
            lines.Add((!pushed, "sql check: " + said));
            notify("sql-update|" + hash, char.ToUpperInvariant(said[0]) + said[1..]);
        }
        catch (System.ComponentModel.Win32Exception ex) { lines.Add((true, $"sql check: couldn't run git: {ex.Message}")); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { lines.Add((true, $"sql check: couldn't write {path}: {ex.Message}")); }
        return lines;
    }

    /// <summary>Integrates origin/main (a merge, as the database sync does: agents push to main from this checkout too) and pushes.</summary>
    private static bool Push(Func<string[], ProcessResult> git, out string why)
    {
        var fetch = git(["fetch", "-q", "origin"]);
        if (fetch.ExitCode == 0 && !fetch.TimedOut)
        {
            var merge = git(["merge", "-q", "--no-edit", "origin/main"]);
            if (merge.ExitCode != 0 || merge.TimedOut)
            {
                git(["merge", "--abort"]);
                why = "couldn't merge origin/main: " + (merge.Output + merge.Error).Trim();
                return false;
            }
        }
        var push = git(["push", "-q", "origin", "HEAD:main"]);
        why = (push.Output + push.Error).Trim();
        return push.ExitCode == 0 && !push.TimedOut;
    }

    /// <summary>What winget has against <paramref name="script"/>'s pins: the script rewritten where a pin is behind, and any newer major.</summary>
    public Outcome Check(string script)
    {
        if (ReadPins(script) is not { } pins) return new(null, [], [], "setup-host.ps1 has no $SseiDev*/$SseiExpr* pins to check");
        var changes = new List<string>();
        var updated = script;
        foreach (var (pin, prefix, what) in new[] { (pins.Developer, "SseiDev", "Developer"), (pins.Express, "SseiExpr", "Express (LocalDB)") })
        {
            var listing = Show(pin.Package);
            if (listing is null) return new(null, [], [], $"winget didn't describe {pin.Package}");
            if (!Newer(listing, pin)) continue;
            updated = Rewrite(updated, prefix + "Version", listing.Version);
            updated = Rewrite(updated, prefix + "Url", listing.Url);
            changes.Add(listing.Version == pin.Version
                ? $"SQL Server {what} {pin.Version} has a new bootstrapper URL ({listing.Url})"
                : $"SQL Server {what} {pin.Version} -> {listing.Version}");
        }
        var majors = NewerMajorIds(Search(), pins).Select(Show).OfType<Listing>().ToList();
        return new(changes.Count > 0 ? updated : null, changes, majors) { Base = YearOf(pins.Developer.Package) };
    }

    /// <summary>A listing newer than the pin: a higher version, or the same version at another URL. A lower one (winget rolled
    /// back) is left alone.</summary>
    internal static bool Newer(Listing listing, Pin pin)
    {
        if (listing.Url.Length == 0 || listing.Version.Length == 0) return false;
        if (Version.TryParse(listing.Version, out var have) && Version.TryParse(pin.Version, out var had))
            return have > had || (have == had && !string.Equals(listing.Url, pin.Url, StringComparison.Ordinal));
        return !string.Equals(listing.Version, pin.Version, StringComparison.Ordinal) || !string.Equals(listing.Url, pin.Url, StringComparison.Ordinal);
    }

    private Listing? Show(string package)
    {
        var r = runner.Run("winget", ["show", "--id", package, "-e", "--source", "winget", "--accept-source-agreements", "--disable-interactivity"], WingetTimeout);
        return r.ExitCode != 0 || r.TimedOut ? null : ParseShow(package, r.Output);
    }

    private string Search()
    {
        var r = runner.Run("winget", ["search", "Microsoft.SQLServer.", "--source", "winget", "--accept-source-agreements", "--disable-interactivity"], WingetTimeout);
        return r.ExitCode != 0 || r.TimedOut ? "" : r.Output;
    }

    /// <summary>The "Version:" and first "Installer Url:" of <c>winget show</c>; null when either is missing.</summary>
    internal static Listing? ParseShow(string package, string output)
    {
        string? Field(string label) => output.Split('\n').Select(l => l.Trim())
            .FirstOrDefault(l => l.StartsWith(label, StringComparison.OrdinalIgnoreCase))?[label.Length..].Trim();
        return Field("Version:") is { Length: > 0 } version && Field("Installer Url:") is { Length: > 0 } url && url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            ? new Listing(package, version, url) : null;
    }

    /// <summary>The Developer and Express package IDs in <c>winget search</c>'s output of a later year than the pinned ones.</summary>
    internal static IReadOnlyList<string> NewerMajorIds(string searchOutput, Pins pins)
    {
        var dev = YearOf(pins.Developer.Package);
        var expr = YearOf(pins.Express.Package);
        return [.. PackageId().Matches(searchOutput).Select(m => (Id: m.Value, Year: int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture), Edition: m.Groups[2].Value))
            .Where(p => p.Year > (p.Edition.Equals("Developer", StringComparison.OrdinalIgnoreCase) ? dev : expr))
            .Select(p => p.Id).Distinct(StringComparer.OrdinalIgnoreCase)];
    }

    private static int YearOf(string package) =>
        PackageId().Match(package) is { Success: true } m ? int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) : int.MaxValue;

    /// <summary>The script's six pins; null when any is missing.</summary>
    internal static Pins? ReadPins(string script)
    {
        string? Value(string name) => Assignment(name).Match(script) is { Success: true } m ? m.Groups["value"].Value : null;
        if (Value("SseiDevPackage") is not { } dp || Value("SseiDevVersion") is not { } dv || Value("SseiDevUrl") is not { } du ||
            Value("SseiExprPackage") is not { } ep || Value("SseiExprVersion") is not { } ev || Value("SseiExprUrl") is not { } eu) return null;
        return new Pins(new Pin(dp, dv, du), new Pin(ep, ev, eu));
    }

    /// <summary><paramref name="script"/> with <c>$name = '...'</c>'s quoted value replaced; every other byte, line endings included, kept.</summary>
    internal static string Rewrite(string script, string name, string value) =>
        Assignment(name).Replace(script, m => m.Value[..(m.Groups["value"].Index - m.Index)] + value.Replace("'", "''") + "'", 1);

    private static Regex Assignment(string name) =>
        new(@"^\$" + Regex.Escape(name) + @"[ \t]*=[ \t]*'(?<value>[^'\r\n]*)'", RegexOptions.Multiline | RegexOptions.CultureInvariant);

    [GeneratedRegex(@"\bMicrosoft\.SQLServer\.(\d{4})\.(Developer|Express)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PackageId();
}
