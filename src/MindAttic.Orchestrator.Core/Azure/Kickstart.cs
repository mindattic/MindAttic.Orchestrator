using System.Text.Json;

namespace MindAttic.Orchestrator.Azure;

/// <summary>An Azure web app's database as kickstart.ps1 found it: its server, name, status (Online, Paused, ...) and sku.</summary>
public sealed record KickstartDatabase(string Server, string Fqdn, string Name, string Status, string Sku);

/// <summary>
/// One web app row: its name, Azure state (Running, Stopped), resource group and site URL, its database when one was found, and,
/// after a wake, what the app and its database answered (<see cref="AppResult"/>, <see cref="DbResult"/>; null for a plain list).
/// </summary>
public sealed record KickstartApp(string Name, string State, string ResourceGroup, string Url, KickstartDatabase? Database,
    string? AppResult = null, string? DbResult = null)
{
    /// <summary>The app answered its wake ("up, /healthz -> 200 (3s)").</summary>
    public bool AppUp => AppResult?.StartsWith("up", StringComparison.OrdinalIgnoreCase) == true;

    /// <summary>The database opened a connection on its wake ("online (12s)").</summary>
    public bool DbOnline => DbResult?.StartsWith("online", StringComparison.OrdinalIgnoreCase) == true;
}

/// <summary>
/// What a kickstart run came back with: its <see cref="Apps"/> rows, or why there are none (<see cref="Error"/>), with
/// <see cref="NeedsLogin"/> set when the cause is az not being signed in (or not installed), so the UI can offer <c>az login</c>.
/// </summary>
public sealed record KickstartResult(IReadOnlyList<KickstartApp> Apps, string? Error = null, bool NeedsLogin = false)
{
    /// <summary>Whether the run worked (a wake's per-app failures are in each row; this is about the run itself).</summary>
    public bool Ok => Error is null;

    /// <summary>A failed run.</summary>
    public static KickstartResult Failed(string error, bool needsLogin = false) => new([], error, needsLogin);
}

/// <summary>
/// The /kickstart engine callable from the UI (M.ORCH-T2054): runs the workspace's shared
/// <c>mindattic-agent-standard\kickstart.ps1</c> (the same script the /kickstart command runs) with <c>-Json</c> and parses its
/// document into rows. <see cref="List"/> lists every web app with its state and its database's status; <see cref="Wake"/> wakes
/// one app (exact name or a unique partial match) or <c>all</c> and waits until each answers. Read-only in Azure: the script
/// changes no setting or resource. Both block for as long as az (and, for a wake, the apps) take: run them off the UI thread.
/// Never throw.
/// </summary>
public sealed class Kickstart(string workspaceRoot, IProcessRunner? runner = null)
{
    private readonly IProcessRunner runner = runner ?? ProcessRunner.Default;

    /// <summary>How long a list may run: az lists every app's connection strings and settings, a few seconds each.</summary>
    public TimeSpan ListTimeout { get; init; } = TimeSpan.FromMinutes(5);

    /// <summary>How long a wake may run, past the script's own per-app wait (<see cref="WakeSecondsPerApp"/>, twice: app then database).</summary>
    public TimeSpan WakeTimeout { get; init; } = TimeSpan.FromMinutes(30);

    /// <summary>The script's -TimeoutSeconds: how long it waits for each app, and then its database, to answer.</summary>
    public int WakeSecondsPerApp { get; init; } = 180;

    /// <summary>The shared script's path under the workspace root.</summary>
    public string ScriptPath => Path.Combine(workspaceRoot, "mindattic-agent-standard", "kickstart.ps1");

    /// <summary>Every web app with its state and its database's status; wakes nothing.</summary>
    public KickstartResult List() => Run([], ListTimeout, ParseList);

    /// <summary>Wakes <paramref name="name"/> (a full app name, a unique partial one like <c>gadfly</c>, or <c>all</c>) and its database.</summary>
    public KickstartResult Wake(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return KickstartResult.Failed("Name an app to wake, or 'all'.");
        return Run([name.Trim(), "-TimeoutSeconds", WakeSecondsPerApp.ToString(System.Globalization.CultureInfo.InvariantCulture)], WakeTimeout, ParseWake);
    }

    /// <summary>Wakes every app and its database.</summary>
    public KickstartResult WakeAll() => Wake("all");

    private KickstartResult Run(string[] scriptArgs, TimeSpan timeout, Func<JsonElement, IReadOnlyList<KickstartApp>> parse)
    {
        var script = ScriptPath;
        if (!File.Exists(script)) return KickstartResult.Failed($"kickstart.ps1 not found at {script}.");
        ProcessResult r;
        try
        {
            r = runner.Run("powershell.exe",
                ["-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File", script, .. scriptArgs, "-Json"], timeout);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException) { return KickstartResult.Failed($"Couldn't run kickstart.ps1: {ex.Message}"); }
        return Interpret(r, parse);
    }

    /// <summary>Turns a finished run into a result: its JSON rows on success, else the error and whether az needs a login.</summary>
    internal static KickstartResult Interpret(ProcessResult r, Func<JsonElement, IReadOnlyList<KickstartApp>> parse)
    {
        if (r.TimedOut && !r.Exited) return KickstartResult.Failed("kickstart timed out.");
        var json = LastJsonLine(r.Output);
        if (r.ExitCode != 0 || json is null)
        {
            var why = FirstErrorLine(r.Error) ?? FirstErrorLine(r.Output) ?? $"kickstart exited {r.ExitCode} with no output.";
            return KickstartResult.Failed(why, NeedsLogin(r.Error + "\n" + r.Output));
        }
        try
        {
            using var doc = JsonDocument.Parse(json);
            var apps = parse(doc.RootElement);
            // A wake whose databases all came back "no token (az login?)" ran, but az's sign-in is what's missing.
            var login = apps.Count > 0 && apps.All(a => a.DbResult is { } d && NeedsLogin(d));
            return new KickstartResult(apps, login ? "az isn't signed in: databases couldn't be woken (az login)." : null, login);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or KeyNotFoundException)
        {
            return KickstartResult.Failed($"kickstart's output couldn't be read: {ex.Message}");
        }
    }

    /// <summary>Whether <paramref name="text"/> says az isn't signed in or isn't installed: the script's own "(az login)" hints,
    /// az's "Please run 'az login'", an expired token, or PowerShell not finding az at all.</summary>
    public static bool NeedsLogin(string? text)
    {
        if (string.IsNullOrEmpty(text)) return false;
        return text.Contains("az login", StringComparison.OrdinalIgnoreCase)
            || text.Contains("AADSTS", StringComparison.Ordinal)
            || text.Contains("refresh token has expired", StringComparison.OrdinalIgnoreCase)
            || (text.Contains("'az'", StringComparison.Ordinal) && text.Contains("not recognized", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Parses a list document: <c>[{ name, state, rg, url, database: {...} | null }]</c>.</summary>
    internal static IReadOnlyList<KickstartApp> ParseList(JsonElement root) =>
        [.. Items(root).Select(e => new KickstartApp(Str(e, "name"), Str(e, "state"), Str(e, "rg"), Str(e, "url"), Db(e)))];

    /// <summary>Parses a wake document: <c>{ apps: [{ name, state, url, app, db, database }], urls: [...] }</c>.</summary>
    internal static IReadOnlyList<KickstartApp> ParseWake(JsonElement root)
    {
        var apps = root.ValueKind == JsonValueKind.Object && root.TryGetProperty("apps", out var a) ? a : default;
        return [.. Items(apps).Select(e => new KickstartApp(Str(e, "name"), Str(e, "state"), Str(e, "rg"), Str(e, "url"), Db(e),
            StrOrNull(e, "app"), StrOrNull(e, "db")))];
    }

    // ConvertTo-Json of a one-element array can still come out as the bare object on Windows PowerShell; take either.
    private static IEnumerable<JsonElement> Items(JsonElement e) => e.ValueKind switch
    {
        JsonValueKind.Array => e.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.Object),
        JsonValueKind.Object => [e],
        _ => [],
    };

    private static KickstartDatabase? Db(JsonElement e) =>
        e.TryGetProperty("database", out var d) && d.ValueKind == JsonValueKind.Object
            ? new KickstartDatabase(Str(d, "server"), Str(d, "fqdn"), Str(d, "name"), Str(d, "status"), Str(d, "sku"))
            : null;

    private static string Str(JsonElement e, string name) => StrOrNull(e, name) ?? "";

    private static string? StrOrNull(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) ? v.ValueKind switch
        {
            JsonValueKind.String => v.GetString(),
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            _ => v.ToString(),
        } : null;

    // The document is the script's last line; anything a profile or az printed before it is skipped.
    private static string? LastJsonLine(string output) =>
        output.Split('\n').Select(l => l.Trim()).LastOrDefault(l => l.StartsWith('[') || l.StartsWith('{'));

    private static string? FirstErrorLine(string text) =>
        text.Split('\n').Select(l => l.Trim()).FirstOrDefault(l => l.Length > 0 && !l.StartsWith("At ", StringComparison.Ordinal)
            && !l.StartsWith('+') && !l.StartsWith("CategoryInfo", StringComparison.Ordinal));
}
