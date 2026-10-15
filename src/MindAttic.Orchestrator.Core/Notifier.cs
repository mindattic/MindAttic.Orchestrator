using System.Net.Http.Headers;
using System.Text;

namespace MindAttic.Orchestrator;

/// <summary>
/// Sends an alert to <see cref="Settings.OrchestratorSettings.NotifyUrl"/>: a plain-text POST with a
/// Title header, which is ntfy.sh's format (subscribe to the topic on your phone and alerts follow you
/// off the desktop), and which any webhook receiver can read. Never throws: a dropped alert is logged.
/// </summary>
public sealed class Notifier(HttpClient? http = null)
{
    private readonly HttpClient http = http ?? new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
    private readonly Serilog.ILogger log = OrchestratorLog.For("Notify");

    public async Task<bool> SendAsync(string? url, string title, string message, bool urgent = false)
    {
        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("https" or "http")) return false;
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, uri) { Content = new StringContent(message, Encoding.UTF8, "text/plain") };
            // Header values must be ASCII; titles here are plain English, but strip anything else to be safe.
            request.Headers.TryAddWithoutValidation("Title", new string(title.Where(c => c < 128).ToArray()));
            request.Headers.TryAddWithoutValidation("Tags", "robot");
            if (urgent) request.Headers.TryAddWithoutValidation("Priority", "high");
            using var response = await http.SendAsync(request);
            if (!response.IsSuccessStatusCode) log.Warning("Alert to {Host:l} failed: {Status}", uri.Host, (int)response.StatusCode);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            log.Warning("Alert to {Host:l} failed: {Error:l}", uri.Host, ex.Message);
            return false;
        }
    }
}
