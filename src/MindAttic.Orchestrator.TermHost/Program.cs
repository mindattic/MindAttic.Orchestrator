using System.Text.Json;
using MindAttic.Orchestrator.Term;
using MindAttic.Orchestrator.TermHost;

// TermHost (M.ORCH-T924): one CLI on a ConPTY, served on mindattic-term-<user>-<id>. See HostOptions for the command line.
var options = HostOptions.Parse(args, out var error);
if (options is null)
{
    Console.Error.WriteLine(error);
    Console.Error.WriteLine(HostOptions.Usage);
    return 2;
}

using var log = new HostLog(options.Id);
AppDomain.CurrentDomain.UnhandledException += (_, e) => log.Write($"unhandled exception (terminating={e.IsTerminating}): {e.ExceptionObject}");
TaskScheduler.UnobservedTaskException += (_, e) => { log.Error(e.Exception, "unobserved task exception; contained"); e.SetObserved(); };

try
{
    if (!options.EnvPrepared) PrepareEnvironment(options);

    if (options.Launch)
    {
        // Started from the host's own folder, never the CLI's, so the long-lived host holds no handle on a repo or worktree.
        var self = Environment.ProcessPath ?? throw new InvalidOperationException("The host can't find its own executable.");
        var line = string.Join(' ', new[] { self }.Concat(options.DetachedArguments()).Select(Native.Quote));
        var pid = Native.SpawnDetached(line, AppContext.BaseDirectory);
        log.Write($"launched detached host pid={pid}");
        Console.WriteLine(JsonSerializer.Serialize(new { id = options.Id, pid, pipe = options.PipeName }));
        return 0;
    }

    return new TermSession(options, log).Run();
}
catch (Exception ex)
{
    log.Error(ex, "the host failed; exiting");
    Console.Error.WriteLine($"TermHost failed: {ex.Message}");
    return 1;
}

// The CLI's environment, set on this process (which runs only it), so CreateProcess passes it on: truecolor, no stale
// NO_COLOR (as PaneRequest.ProcessCommandLine does), the session's id and pipe, then each --env (KEY= removes KEY).
static void PrepareEnvironment(HostOptions options)
{
    Environment.SetEnvironmentVariable("NO_COLOR", null);
    Environment.SetEnvironmentVariable("COLORTERM", "truecolor");
    Environment.SetEnvironmentVariable("MINDATTIC_TERM_ID", options.Id);
    Environment.SetEnvironmentVariable("MINDATTIC_TERM_PIPE", options.PipeName);
    foreach (var (key, value) in options.Env) Environment.SetEnvironmentVariable(key, value);
}
