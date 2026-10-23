using MindAttic.Orchestrator.Settings;
using Serilog.Events;

namespace MindAttic.Orchestrator.Reminders;

/// <summary>
/// Psst on outside use (M.ORCH-T702): when outside-use detection (M.ORCH-T682) sees someone start using Claude on a watched
/// login (<see cref="OrchestratorSettings.OutsideUsePsstLogins"/>), the recipient (<see cref="OrchestratorSettings.OutsideUsePsstTo"/>)
/// is texted through <c>psst sms</c>. Once per outside-use stretch: only its start is texted (the watch says it once, and a
/// stretch that goes on, or whose ceiling is lowered, isn't said again). The text goes on its own thread, so a slow Psst
/// never holds the daemon's tick (a send can take up to <see cref="PsstClient.SendTimeout"/>).
/// </summary>
public sealed class OutsideUsePsst(PsstClient psst, Action<LogEventLevel, string> say)
{
    private readonly object gate = new();

    /// <summary>Logins whose text is still being sent: a second start for one while its first is in flight isn't sent twice.</summary>
    private readonly HashSet<string> sending = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>True when <paramref name="email"/>'s login (named <paramref name="name"/>) is one of <paramref name="watched"/>:
    /// each entry is its email, the part before the @, or its name ("Jordan"), any case.</summary>
    public static bool Watched(IEnumerable<string> watched, string? email, string? name)
    {
        var local = email is { } e && e.IndexOf('@') is > 0 and var at ? e[..at] : null;
        foreach (var raw in watched)
        {
            var w = raw?.Trim();
            if (string.IsNullOrEmpty(w)) continue;
            if (string.Equals(w, email, StringComparison.OrdinalIgnoreCase) || string.Equals(w, local, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(w, name?.Trim(), StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    /// <summary>The text: "Jordan is using Claude outside the Orchestrator (the meter rose 6 points in 25 min; ...)".</summary>
    public static string Text(string name, string? seen) =>
        $"Orchestrator: {name} is using Claude outside the Orchestrator{(string.IsNullOrWhiteSpace(seen) ? "" : $" ({seen.Trim()})")}.";

    /// <summary>Texts the recipient that <paramref name="name"/> (<paramref name="email"/>) started using Claude elsewhere, when
    /// that login is watched and a recipient is set; the send runs on its own thread (the returned task, already complete when
    /// nothing is sent). Null when nothing was started.</summary>
    public Task? Started(string? email, string name, string? seen, OrchestratorSettings settings)
    {
        if (settings.OutsideUsePsstTo is not { Length: > 0 } to || string.IsNullOrWhiteSpace(to)) return null;
        if (!Watched(settings.OutsideUsePsstLogins, email, name)) return null;
        var key = email ?? name;
        lock (gate)
            if (!sending.Add(key)) return null;
        var text = Text(name, seen);
        return Task.Run(() =>
        {
            try
            {
                var (sent, detail, timedOut) = psst.Send(to.Trim(), text);
                say(sent ? LogEventLevel.Information : LogEventLevel.Warning, sent
                    ? $"psst: texted {to.Trim()} that {name} is using Claude outside the orchestrator"
                    : $"psst: couldn't text {to.Trim()} that {name} is using Claude outside the orchestrator{(timedOut ? " (timed out)" : "")}: {detail}");
            }
            catch (Exception ex)
            {
                say(LogEventLevel.Warning, $"psst: couldn't text {to.Trim()} that {name} is using Claude outside the orchestrator: {ex.Message}");
            }
            finally
            {
                lock (gate) sending.Remove(key);
            }
        });
    }
}
