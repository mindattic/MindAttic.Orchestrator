using MindAttic.Orchestrator.Settings;
using Serilog.Events;

namespace MindAttic.Orchestrator.Reminders;

/// <summary>
/// Texts an owner alert (a login reaching its own cutoff, M.ORCH-T742) through <c>psst sms</c> to
/// <see cref="OrchestratorSettings.PsstAlertTo"/>. The text goes on its own thread, so a slow Psst never holds the
/// daemon's tick (a send can take up to <see cref="PsstClient.SendTimeout"/>).
/// </summary>
public sealed class PsstAlerts(PsstClient psst, Action<LogEventLevel, string> say)
{
    private readonly object gate = new();

    /// <summary>Keys whose text is still being sent: a second alert for one while its first is in flight isn't sent twice.</summary>
    private readonly HashSet<string> sending = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Texts <paramref name="to"/> <paramref name="text"/> on its own thread, one at a time per <paramref name="key"/>;
    /// <paramref name="what"/> says what it's about, for the log. Null when one for the key is still in flight.</summary>
    public Task? Send(string key, string to, string text, string what)
    {
        lock (gate)
            if (!sending.Add(key)) return null;
        return Task.Run(() =>
        {
            try
            {
                var (sent, detail, timedOut) = psst.Send(to.Trim(), text);
                say(sent ? LogEventLevel.Information : LogEventLevel.Warning, sent
                    ? $"psst: texted {to.Trim()} that {what}"
                    : $"psst: couldn't text {to.Trim()} that {what}{(timedOut ? " (timed out)" : "")}: {detail}");
            }
            catch (Exception ex)
            {
                say(LogEventLevel.Warning, $"psst: couldn't text {to.Trim()} that {what}: {ex.Message}");
            }
            finally
            {
                lock (gate) sending.Remove(key);
            }
        });
    }
}
