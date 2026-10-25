using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MindAttic.Orchestrator.Sessions;

/// <summary>
/// A CLI to open as a tab of a person's Terminals panel on the dashboard (M.ORCH-T810): a live ConPTY terminal (the Windows
/// Terminal renderer, through EasyWindowsTerminalControl) running <see cref="Command"/> in <see cref="Directory"/>. It goes on
/// the page of the login it runs on (<see cref="Login"/>: <see cref="Email"/>, else the email its <see cref="ConfigDir"/> folder
/// is named for, else the global login), titled and colored as that login's Windows Terminal tab would be.
/// <para>MindAttic.Launcher sends one down <see cref="PaneBroker.PipeName"/> (M.LNCH-T77) instead of opening a standalone
/// Windows Terminal window: the same <c>MindAttic.Launcher host …</c> command its wt tab would run, as JSON on one line.</para>
/// </summary>
/// <param name="Title">The tab's whole title, "RD: Repo [Claude]".</param>
/// <param name="Directory">The folder it runs in.</param>
public sealed record PaneRequest(string Title, string Directory)
{
    /// <summary>The process as its arguments, the executable first (a wt tab's <c>Command</c>): each is passed exactly.</summary>
    public IReadOnlyList<string>? Command { get; init; }

    /// <summary>Or the process as one Windows command line, run as it is, when <see cref="Command"/> isn't given.</summary>
    public string? CommandLine { get; init; }

    /// <summary>What it runs for, the tab's name without its badge and provider; the folder's name when not given.</summary>
    public string? Repo { get; init; }

    public string Provider { get; init; } = "Claude";

    /// <summary>The per-CLI login folder (<c>CLAUDE_CONFIG_DIR</c>) its CLI runs on, when known; null for none or the global login.</summary>
    public string? ConfigDir { get; init; }

    /// <summary>The login's email, when known: it picks the person's page over <see cref="ConfigDir"/>.</summary>
    public string? Email { get; init; }

    /// <summary>The login's badge, "RD".</summary>
    public string? Initials { get; init; }

    /// <summary>The tab's color, "#RRGGBB"; null for none.</summary>
    public string? TabColor { get; init; }

    /// <summary>This is the Launcher's own menu, not a CLI it opened (M.LNCH-T77): its tab sorts first among a login's
    /// Terminals panel tabs, and it's shown without a provider bracket.</summary>
    public bool IsMenu { get; init; }

    /// <summary>The person whose page the tab goes on: the login's email, or null for the global login's.</summary>
    [JsonIgnore]
    public string? Login => AccountDirs.Normalize(Email) ?? LoginOf(ConfigDir);

    /// <summary>The name its tab shows: <see cref="Repo"/>, else its folder's name.</summary>
    [JsonIgnore]
    public string Name => Repo is { Length: > 0 } repo ? repo
        : Path.GetFileName(Path.TrimEndingDirectorySeparator(Directory ?? "")) is { Length: > 0 } folder ? folder : Title;

    /// <summary>The login a <c>CLAUDE_CONFIG_DIR</c> folder is: its folder's name when that's an email (a per-CLI login under
    /// <c>~/.claude-accounts</c>); null for none, or for the global login's <c>~/.claude</c>.</summary>
    public static string? LoginOf(string? configDir) =>
        string.IsNullOrWhiteSpace(configDir) ? null : AccountDirs.Normalize(Path.GetFileName(Path.TrimEndingDirectorySeparator(configDir.Trim())));

    /// <summary>Why it can't be opened, or null when it can.</summary>
    public string? Problem()
    {
        if (string.IsNullOrWhiteSpace(Title)) return "No title.";
        if (string.IsNullOrWhiteSpace(Directory) || !System.IO.Directory.Exists(Directory)) return $"Folder not found: {Directory}";
        if (Command is { Count: > 0 } command) return string.IsNullOrWhiteSpace(command[0]) ? "No program to run." : null;
        return string.IsNullOrWhiteSpace(CommandLine) ? "No command to run." : null;
    }

    /// <summary>
    /// The command line the terminal starts: <see cref="Command"/> run by a PowerShell that first clears a stale
    /// <c>NO_COLOR</c> (which would strip every color from the CLI), sets <c>COLORTERM=truecolor</c> (the terminal renders
    /// 24-bit color) and <see cref="PaneBroker.InsideVariable"/> (so a Launcher inside it knows it runs in the Orchestrator's
    /// panel and sends its new CLIs here too, M.LNCH-T77). Encoded, so no quote, space or ';' in an argument is parsed on the
    /// way. A bare <see cref="CommandLine"/> is run by <c>cmd /d /c</c> after the same settings.
    /// </summary>
    public string ProcessCommandLine(string? pipeName = null)
    {
        var setup = $"Remove-Item Env:NO_COLOR -ErrorAction SilentlyContinue; $env:COLORTERM = 'truecolor'; " +
                    $"$env:{PaneBroker.InsideVariable} = {AccountDirs.Ps(pipeName ?? PaneBroker.PipeName)}; ";
        var run = Command is { Count: > 0 } command
            ? "& " + string.Join(' ', command.Select(AccountDirs.Ps))
            : $"& cmd.exe /d /c {AccountDirs.Ps(CommandLine ?? "")}";
        return JoinCommandLine(["powershell.exe", "-NoLogo", "-NoProfile", "-ExecutionPolicy", "Bypass", "-EncodedCommand",
            Convert.ToBase64String(Encoding.Unicode.GetBytes(setup + run + "; exit $LASTEXITCODE"))]);
    }

    /// <summary>Arguments joined into one command line that <c>CommandLineToArgvW</c> splits back into the same arguments.</summary>
    internal static string JoinCommandLine(IEnumerable<string> args) => string.Join(' ', args.Select(QuoteArgument));

    internal static string QuoteArgument(string arg)
    {
        if (arg.Length > 0 && arg.IndexOfAny([' ', '\t', '\n', '\v', '"']) < 0) return arg;
        var quoted = new StringBuilder("\"");
        var slashes = 0;
        foreach (var c in arg)
        {
            if (c == '\\') { slashes++; continue; }
            // Backslashes before a quote are doubled, and the quote escaped; anywhere else they stand as they are.
            quoted.Append('\\', c == '"' ? slashes * 2 + 1 : slashes).Append(c);
            slashes = 0;
        }
        // Before the closing quote they're doubled too, or the last one would escape it.
        return quoted.Append('\\', slashes * 2).Append('"').ToString();
    }
}

/// <summary>
/// The hosting entry point of the dashboard's Terminals panels (M.ORCH-T810): hands a <see cref="PaneRequest"/> to the open
/// dashboard, which opens it as a new tab on its person's page. The dashboard listens on <see cref="PipeName"/>, a pipe only
/// this Windows user can open; a client (MindAttic.Launcher, M.LNCH-T77) writes the request as one line of JSON and reads back
/// one line: empty when the tab opened, else why not. Nobody listening means no dashboard is open, and the caller opens its
/// CLI its own way (a Windows Terminal window).
/// </summary>
public static class PaneBroker
{
    /// <summary>The dashboard's pipe; one per Windows user, since each has their own dashboard.</summary>
    public static string PipeName { get; } = "mindattic-orchestrator-terminals-" +
        new string(Environment.UserName.Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray());

    /// <summary>Set inside every terminal of the panel to <see cref="PipeName"/>: a process that sees it runs inside the
    /// Orchestrator's panel, and opens its own new CLIs there too.</summary>
    public const string InsideVariable = "MINDATTIC_ORCHESTRATOR_TERMINALS";

    private static readonly JsonSerializerOptions Json = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>The request as the one line the pipe carries.</summary>
    public static string Serialize(PaneRequest request) => JsonSerializer.Serialize(request, Json);

    /// <summary>A line off the pipe as a request; null when it isn't one.</summary>
    public static PaneRequest? Deserialize(string line) => JsonSerializer.Deserialize<PaneRequest>(line, Json);

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
            writer.WriteLine(Serialize(request));
            var reply = reader.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(30)).GetAwaiter().GetResult();
            error = string.IsNullOrEmpty(reply) ? null : reply;
        }
        // Taken but not answered: the dashboard has it, and may still open it, so it isn't opened a second time elsewhere.
        catch (Exception ex) when (ex is TimeoutException or IOException) { error = $"The dashboard took the terminal but didn't answer: {ex.Message}"; }
        return true;
    }

    /// <summary>Whether the dashboard's Terminals pipe currently has a listener: a cheap connect-and-drop, no request sent.
    /// <c>orchestrator dock</c>'s pre-flight check (M.ORCH-Tnnn), so a missing dashboard is caught before anything
    /// destructive (ending the CLI it would have reopened) happens.</summary>
    public static bool IsListening(string? pipeName = null, int connectMs = 500)
    {
        try
        {
            using var pipe = new NamedPipeClientStream(".", pipeName ?? PipeName, PipeDirection.InOut, PipeOptions.CurrentUserOnly);
            pipe.Connect(connectMs);
            return true;
        }
        catch (Exception ex) when (ex is TimeoutException or IOException or UnauthorizedAccessException) { return false; }
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
                OrchestratorLog.For("Terminals").Error(ex, "The terminals pipe couldn't listen; retrying");
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
                    error = line is null ? "No request." : Deserialize(line) is { } request
                        ? request.Problem() ?? await open(request).ConfigureAwait(false) : "No request.";
                }
                catch (JsonException ex) { error = $"Unreadable request: {ex.Message}"; }
                catch (Exception ex) when (ex is not (IOException or OperationCanceledException or ObjectDisposedException))
                {
                    // Anything else open() throws still gets an answer, so the caller falls back to its own window at once.
                    OrchestratorLog.For("Terminals").Error(ex, "Opening a terminal tab failed");
                    error = $"The dashboard couldn't open the terminal: {ex.Message}";
                }
                await writer.WriteLineAsync((error ?? "").ReplaceLineEndings(" ")).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is IOException or OperationCanceledException or ObjectDisposedException) { }
        }
    }
}
