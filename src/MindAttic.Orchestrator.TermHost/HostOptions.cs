using MindAttic.Orchestrator.Term;

namespace MindAttic.Orchestrator.TermHost;

/// <summary>
/// TermHost's command line (M.ORCH-T924):
/// <code>MindAttic.Orchestrator.TermHost [--launch] [--id &lt;id&gt;] [--cwd &lt;dir&gt;] [--env KEY=VALUE]... [--title &lt;text&gt;]
///     [--linger &lt;seconds&gt;] [--unlisted] [-- &lt;command line&gt;]</code>
/// <list type="bullet">
/// <item><c>--launch</c> re-spawns the host detached and prints <c>{"id","pid","pipe"}</c> on one line.</item>
/// <item><c>--id</c> is the session id, which names the pipe and the log; a new 12-hex-character id when not given.</item>
/// <item><c>--env KEY=VALUE</c> sets a variable for the CLI (<c>KEY=</c> removes it); repeatable.</item>
/// <item><c>--login</c> and <c>--repo</c> (M.ORCH-T925) go in the session's registry file for the dashboard: the login (an
/// email) it runs on and what it runs for.</item>
/// <item><c>--linger</c> is how long the host stays after the CLI exits while no viewer has seen the exit (default 600).</item>
/// <item><c>--unlisted</c> (M.ORCH-T930) writes no registry file and announces nothing on the status bus: only a viewer told the
/// pipe can attach, and no dashboard opens a tab for it. For smoke tests beside a running dashboard.</item>
/// <item>After <c>--</c>: the CLI, as arguments, or as one whole command line when it is a single argument with a space in it
/// (a PaneRequest's ProcessCommandLine). With none, <c>pwsh</c>, else <c>powershell.exe</c> when pwsh isn't installed.</item>
/// </list>
/// </summary>
internal sealed record HostOptions
{
    public const string Usage =
        "usage: MindAttic.Orchestrator.TermHost [--launch] [--id <id>] [--cwd <dir>] [--env KEY=VALUE]... [--title <text>] " +
        "[--login <email>] [--repo <name>] [--linger <seconds>] [--unlisted] [-- <command line>]";

    /// <summary>How long a host stays after its CLI exits when no viewer has seen the exit (spike section 1).</summary>
    public static readonly TimeSpan DefaultLinger = TimeSpan.FromMinutes(10);

    public string Id { get; init; } = "";
    public string? Cwd { get; init; }
    public IReadOnlyList<KeyValuePair<string, string?>> Env { get; init; } = [];
    public string? Title { get; init; }
    public string? Login { get; init; }
    public string? Repo { get; init; }
    public TimeSpan Linger { get; init; } = DefaultLinger;
    public bool Launch { get; init; }

    /// <summary>No registry file and no term-session announcement (<c>--unlisted</c>).</summary>
    public bool Unlisted { get; init; }

    /// <summary>Set on the host <c>--launch</c> starts: the launching process already applied <see cref="Env"/>, and the
    /// host inherits it, so no variable (a token) is ever on the long-lived host's command line.</summary>
    public bool EnvPrepared { get; init; }

    public IReadOnlyList<string> Command { get; init; } = [];

    public string PipeName => TermProtocol.PipeName(Id);

    /// <summary>The options in <paramref name="args"/>, or null with <paramref name="error"/> set.</summary>
    public static HostOptions? Parse(IReadOnlyList<string> args, out string? error)
    {
        error = null;
        string? id = null, cwd = null, title = null, login = null, repo = null;
        var env = new List<KeyValuePair<string, string?>>();
        var linger = DefaultLinger;
        bool launch = false, prepared = false, unlisted = false;
        var command = new List<string>();
        for (var i = 0; i < args.Count; i++)
        {
            var arg = args[i];
            var hasValue = i + 1 < args.Count;
            switch (arg)
            {
                case "--":
                    command.AddRange(args.Skip(i + 1));
                    i = args.Count;
                    break;
                case "--launch": launch = true; break;
                case "--env-prepared": prepared = true; break;
                case "--unlisted": unlisted = true; break;
                case "--id" when hasValue: id = args[++i]; break;
                case "--cwd" when hasValue: cwd = args[++i]; break;
                case "--title" when hasValue: title = args[++i]; break;
                case "--login" when hasValue: login = args[++i]; break;
                case "--repo" when hasValue: repo = args[++i]; break;
                case "--env" when hasValue:
                    var pair = args[++i];
                    var eq = pair.IndexOf('=');
                    if (eq <= 0) { error = $"--env wants KEY=VALUE, not '{pair}'."; return null; }
                    env.Add(new(pair[..eq], eq == pair.Length - 1 ? null : pair[(eq + 1)..]));
                    break;
                case "--linger" when hasValue:
                    if (!int.TryParse(args[++i], out var seconds) || seconds < 0) { error = $"--linger wants seconds, not '{args[i]}'."; return null; }
                    linger = TimeSpan.FromSeconds(seconds);
                    break;
                default:
                    error = $"Unknown or incomplete argument '{arg}'.";
                    return null;
            }
        }
        if (id is not null && TermProtocol.Sanitize(id).Trim('-').Length == 0) { error = $"--id '{id}' has no letter or digit."; return null; }
        return new HostOptions
        {
            Id = id ?? TermProtocol.NewSessionId(), Cwd = cwd, Env = env, Title = title, Login = login, Repo = repo, Linger = linger, Launch = launch,
            EnvPrepared = prepared, Unlisted = unlisted, Command = command,
        };
    }

    /// <summary>The arguments the detached host is started with by <c>--launch</c>: these options without <c>--launch</c>
    /// and without <c>--env</c> (applied to the launching process's environment instead, which the host inherits).</summary>
    public IReadOnlyList<string> DetachedArguments()
    {
        var args = new List<string> { "--id", Id, "--env-prepared", "--linger", ((int)Linger.TotalSeconds).ToString() };
        if (Cwd is not null) args.AddRange(["--cwd", Cwd]);
        if (Title is not null) args.AddRange(["--title", Title]);
        if (Login is not null) args.AddRange(["--login", Login]);
        if (Repo is not null) args.AddRange(["--repo", Repo]);
        if (Unlisted) args.Add("--unlisted");
        if (Command.Count > 0) { args.Add("--"); args.AddRange(Command); }
        return args;
    }

    /// <summary>The CLI's command line for CreateProcess. <paramref name="findOnPath"/> finds a bare program name on PATH
    /// (with .exe, .com, .cmd or .bat), returning its extension, or null. Throws <see cref="ArgumentException"/> when an
    /// argument for a .cmd or .bat can't be passed through cmd.exe safely (<see cref="Native.CmdQuote"/>).</summary>
    public string CommandLine(Func<string, string?> findOnPath)
    {
        if (Command.Count == 0) return findOnPath("pwsh") is not null ? "pwsh" : "powershell.exe";
        // One argument with a space in it that names no file is a whole command line, run as it is.
        if (Command.Count == 1 && Command[0].IndexOfAny([' ', '\t']) >= 0 && !File.Exists(Command[0])) return Command[0];
        var line = string.Join(' ', Command.Select(Native.Quote));
        var first = Command[0];
        if (first.IndexOfAny(['\\', '/', ':']) >= 0 || Path.HasExtension(first)) return line;
        // CreateProcess runs only .exe files by bare name: a .cmd or .bat on PATH (npm's claude.cmd) goes through cmd, with
        // cmd's metacharacters made literal (M.ORCH-T996), or an argument with & | ^ % would run more than the CLI.
        return findOnPath(first) is ".cmd" or ".bat" ? "cmd.exe /d /c " + string.Join(' ', Command.Select(Native.CmdQuote)) : line;
    }

    /// <summary>The extension <paramref name="name"/> has on PATH (.exe, .com, .cmd or .bat, in that order), or null.</summary>
    public static string? FindOnPath(string name)
    {
        var dirs = (Environment.GetEnvironmentVariable("PATH") ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var ext in new[] { ".exe", ".com", ".cmd", ".bat" })
            foreach (var dir in dirs)
            {
                try { if (File.Exists(Path.Combine(dir, name + ext))) return ext; }
                catch (ArgumentException) { }
            }
        return null;
    }
}
