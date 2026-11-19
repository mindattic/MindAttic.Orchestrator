using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MindAttic.Orchestrator.Budget;

/// <summary>
/// How long a login has been signed in and how long until its saved sign-in lapses (M.ORCH-T1854). The end is the saved
/// refresh token's <c>refreshTokenExpiresAt</c>. Claude saves no sign-in time, so the start is when the Orchestrator first saw
/// that expiry, kept in a sidecar beside the credentials: a new /login brings a new expiry and so a new start.
/// </summary>
public sealed record SignInAge(DateTime? SinceUtc, DateTime ExpiresUtc)
{
    /// <summary>The sidecar's name, beside <c>.credentials.json</c>.</summary>
    public const string SidecarName = "orchestrator-signin.json";

    /// <summary>Under this much left, the line says to sign in again soon.</summary>
    public static readonly TimeSpan WarnUnder = TimeSpan.FromDays(2);

    private static readonly ConcurrentDictionary<string, (DateTime Stamp, SignInAge? Age)> Cache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The age of the sign-in saved at <paramref name="credentialsPath"/>, recording its start on first sight; null
    /// with none saved, no refresh expiry, or an unreadable file. Re-read only when the credentials file changes.</summary>
    public static SignInAge? Of(string credentialsPath, DateTime nowUtc)
    {
        try
        {
            var stamp = File.Exists(credentialsPath) ? File.GetLastWriteTimeUtc(credentialsPath) : default;
            if (Cache.TryGetValue(credentialsPath, out var hit) && hit.Stamp == stamp) return hit.Age;
            var age = Read(credentialsPath, nowUtc);
            Cache[credentialsPath] = (stamp, age);
            return age;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { return null; }
    }

    private static SignInAge? Read(string credentialsPath, DateTime nowUtc)
    {
        if (ClaudeMeterProbe.ReadCredentials(credentialsPath)?["claudeAiOauth"] is not JsonObject oauth
            || oauth["refreshTokenExpiresAt"] is not JsonValue v || !v.TryGetValue<long>(out var ms)) return null;
        var expires = DateTimeOffset.FromUnixTimeMilliseconds(ms).UtcDateTime;
        var sidecar = Path.Combine(Path.GetDirectoryName(credentialsPath) ?? ".", SidecarName);
        if (File.Exists(sidecar) && JsonNode.Parse(File.ReadAllText(sidecar)) is JsonObject seen
            && seen["refreshTokenExpiresAt"] is JsonValue sv && sv.TryGetValue<long>(out var seenMs) && seenMs == ms
            && seen["firstSeenUtc"] is JsonValue fv && fv.TryGetValue<DateTime>(out var first))
            return new SignInAge(first.ToUniversalTime(), expires);
        try
        {
            File.WriteAllText(sidecar, new JsonObject { ["refreshTokenExpiresAt"] = ms, ["firstSeenUtc"] = nowUtc }.ToJsonString());
            return new SignInAge(nowUtc, expires);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return new SignInAge(null, expires); }
    }

    /// <summary>"Sign-in: 1.3 days in, 18.7 / 20.0 days left"; just the days left with no known start; a sign-in-again
    /// note under <see cref="WarnUnder"/> and once lapsed.</summary>
    public string Line(DateTime nowUtc)
    {
        static string D(double days) => days.ToString("0.0", CultureInfo.InvariantCulture);
        var left = ExpiresUtc - nowUtc;
        if (left <= TimeSpan.Zero) return "Sign-in: expired, sign in again";
        var text = SinceUtc is { } since && since <= nowUtc
            ? $"Sign-in: {D((nowUtc - since).TotalDays)} days in, {D(left.TotalDays)} / {D((ExpiresUtc - since).TotalDays)} days left"
            : $"Sign-in: {D(left.TotalDays)} days left";
        return left < WarnUnder ? text + " (sign in again soon)" : text;
    }
}
