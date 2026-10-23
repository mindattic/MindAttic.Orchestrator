using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using MindAttic.Orchestrator.Settings;

namespace MindAttic.Orchestrator.Sessions;

/// <summary>
/// A CLI to open as a terminal pane inside the dashboard (M.ORCH-T618) rather than a Windows Terminal tab: the same
/// <c>MindAttic.Launcher host</c> command a tab runs (<see cref="CommandLine"/>, the login's <c>CLAUDE_CONFIG_DIR</c> set
/// inside it), with the tab's title, initials badge and color (M.ORCH-T616).
/// </summary>
/// <param name="Repo">What the CLI was started for: the repo's name (the tab's title without its badge and provider).</param>
/// <param name="Title">The tab's whole title, "RD: Repo [Claude]".</param>
/// <param name="CommandLine">The pane's process, as one Windows command line.</param>
public sealed record PaneRequest(string Repo, string Title, string Directory, string Provider, string CommandLine)
{
    /// <summary>The per-CLI login it runs on; null for the global login.</summary>
    public string? ConfigDir { get; init; }

    public string? Initials { get; init; }

    /// <summary>"#RRGGBB"; null for a provider with no login.</summary>
    public string? TabColor { get; init; }

    /// <summary>The Claude conversation it resumes (<c>claude --resume</c>); null for a new one.</summary>
    public string? ResumeSession { get; init; }

    /// <summary>A login's own Launcher tab (M.ORCH-T699): its menu, idle, rather than a repo's CLI. One per login: asked for
    /// again, the one open is reused; and never recorded to be offered again, since each login gets its own anew.</summary>
    public bool IsLauncher { get; init; }
}

/// <summary>A pane the dashboard has open, as it keeps it so the pane can be offered again once the dashboard (and every
/// pane with it) has closed: its repo, its login and its Claude session.</summary>
public sealed record PaneRecord(string Id, string Repo, string Directory, string Provider, DateTime OpenedUtc)
{
    public string? ConfigDir { get; init; }

    /// <summary>The Claude session its CLI was last seen on; null until the dashboard has seen one.</summary>
    public string? SessionId { get; init; }

    /// <summary>The login in words: the per-CLI login's email, else the global login.</summary>
    public string Login => ConfigDir is { } dir ? Path.GetFileName(Path.TrimEndingDirectorySeparator(dir)) : "global login";
}

/// <summary>The panes kept in the store (a <c>state</c> row of this machine's own, never synced).</summary>
public static class PaneRecords
{
    public const string StateName = "terminal-panes";

    public static List<PaneRecord> Load(JsonStore store) => store.Load<List<PaneRecord>>(StateName);

    /// <summary>Adds <paramref name="record"/>, or replaces the one with its id.</summary>
    public static void Put(JsonStore store, PaneRecord record) =>
        store.Update<List<PaneRecord>>(StateName, list => { list.RemoveAll(r => r.Id == record.Id); list.Add(record); });

    public static void Remove(JsonStore store, IReadOnlyCollection<string> ids) =>
        store.Update<List<PaneRecord>>(StateName, list => list.RemoveAll(r => ids.Contains(r.Id)));
}

/// <summary>
/// Hands a <see cref="PaneRequest"/> to the dashboard that holds the panes. In the dashboard itself <see cref="Local"/> opens
/// it; any other process (the daemon's autopilot, <c>orchestrator start</c>) sends it down <see cref="PipeName"/>, a pipe
/// only this user can open, and reads back one line: empty when the pane opened, else why not.
/// </summary>
public static class PaneBroker
{
    /// <summary>The dashboard's pipe; one per Windows user, since each has their own dashboard.</summary>
    public static string PipeName { get; } = "mindattic-orchestrator-panes-" +
        new string(Environment.UserName.Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray());

    /// <summary>Set by the dashboard in its own process: opens a pane, returning an error or null.</summary>
    public static Func<PaneRequest, string?>? Local { get; set; }

    private static readonly JsonSerializerOptions Json = new() { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull };

    /// <summary>Sends <paramref name="request"/> to the dashboard; false when none is listening (none is open), so the caller
    /// opens it some other way. True with <paramref name="error"/> set when the dashboard took it but couldn't open it.</summary>
    public static bool TrySend(PaneRequest request, out string? error, string? pipeName = null, int connectMs = 1000)
    {
        error = null;
        using var pipe = new NamedPipeClientStream(".", pipeName ?? PipeName, PipeDirection.InOut, PipeOptions.CurrentUserOnly);
        try { pipe.Connect(connectMs); }
        catch (Exception ex) when (ex is TimeoutException or IOException or UnauthorizedAccessException) { return false; }
        try
        {
            using var writer = new StreamWriter(pipe, new UTF8Encoding(false), leaveOpen: true) { AutoFlush = true };
            using var reader = new StreamReader(pipe, new UTF8Encoding(false), leaveOpen: true);
            writer.WriteLine(JsonSerializer.Serialize(request, Json));
            var reply = reader.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(30)).GetAwaiter().GetResult();
            error = string.IsNullOrEmpty(reply) ? null : reply;
        }
        // Taken but not answered: the dashboard has it, and may still open it, so it isn't opened a second time elsewhere.
        catch (Exception ex) when (ex is TimeoutException or IOException) { error = $"The dashboard took the pane but didn't answer: {ex.Message}"; }
        return true;
    }

    /// <summary>Takes requests on <paramref name="pipeName"/> until <paramref name="ct"/> ends, opening each with
    /// <paramref name="open"/> (which answers with an error or null).</summary>
    public static async Task ServeAsync(Func<PaneRequest, Task<string?>> open, CancellationToken ct, string? pipeName = null)
    {
        while (!ct.IsCancellationRequested)
        {
            NamedPipeServerStream? pipe = null;
            try
            {
                pipe = new NamedPipeServerStream(pipeName ?? PipeName, PipeDirection.InOut, NamedPipeServerStream.MaxAllowedServerInstances,
                    PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await pipe.WaitForConnectionAsync(ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { pipe?.Dispose(); return; }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                pipe?.Dispose();
                OrchestratorLog.For("Terminals").Error(ex, "The terminal pane pipe couldn't listen; retrying");
                try { await Task.Delay(TimeSpan.FromSeconds(5), ct).ConfigureAwait(false); } catch (OperationCanceledException) { return; }
                continue;
            }
            _ = Task.Run(() => AnswerAsync(pipe, open, ct), ct);
        }
    }

    private static async Task AnswerAsync(NamedPipeServerStream pipe, Func<PaneRequest, Task<string?>> open, CancellationToken ct)
    {
        await using (pipe.ConfigureAwait(false))
        {
            try
            {
                using var reader = new StreamReader(pipe, new UTF8Encoding(false), leaveOpen: true);
                using var writer = new StreamWriter(pipe, new UTF8Encoding(false), leaveOpen: true) { AutoFlush = true };
                var line = await reader.ReadLineAsync(ct).ConfigureAwait(false);
                string? error;
                try
                {
                    error = line is null ? "No request." : JsonSerializer.Deserialize<PaneRequest>(line, Json) is { } request
                        ? await open(request).ConfigureAwait(false) : "No request.";
                }
                catch (JsonException ex) { error = $"Unreadable request: {ex.Message}"; }
                catch (Exception ex) when (ex is not (IOException or OperationCanceledException or ObjectDisposedException))
                {
                    // Anything else open() throws still gets an answer, so the caller falls back to Windows Terminal at once.
                    OrchestratorLog.For("Terminals").Error(ex, "Opening a terminal pane failed");
                    error = $"The dashboard couldn't open the pane: {ex.Message}";
                }
                await writer.WriteLineAsync((error ?? "").ReplaceLineEndings(" ")).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is IOException or OperationCanceledException or ObjectDisposedException) { }
        }
    }
}
