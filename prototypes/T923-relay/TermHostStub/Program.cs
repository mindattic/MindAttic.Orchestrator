using TermHostStub;

// TermHostStub --id <name> [--cwd <dir>] [--linger <seconds>] [--launch] -- <command line>   (default command: pwsh)
// --launch re-spawns this host detached (no console, own process group, outside the caller's job), prints its pid and exits.
string? id = null, cwd = null;
var launch = false;
var linger = 60;
var rest = new List<string>();
var passThrough = new List<string>();
for (var i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--":
            rest.AddRange(args[(i + 1)..]);
            i = args.Length;
            break;
        case "--id" when i + 1 < args.Length: id = args[++i]; passThrough.AddRange(["--id", id]); break;
        case "--cwd" when i + 1 < args.Length: cwd = args[++i]; passThrough.AddRange(["--cwd", cwd]); break;
        case "--linger" when i + 1 < args.Length && int.TryParse(args[i + 1], out var s): linger = s; i++; passThrough.AddRange(["--linger", s.ToString()]); break;
        case "--launch": launch = true; break;
        default:
            Console.Error.WriteLine("usage: TermHostStub --id <name> [--cwd <dir>] [--linger <s>] [--launch] -- <command line>");
            return 2;
    }
}
if (id is null) { Console.Error.WriteLine("--id is required"); return 2; }
if (rest.Count == 0) rest.Add("pwsh");

if (launch)
{
    var self = Environment.ProcessPath!;
    var line = Native.Quote(self) + " " + string.Join(' ', passThrough.Select(Native.Quote)) + " -- " + string.Join(' ', rest.Select(Native.Quote));
    Console.WriteLine(Native.SpawnDetached(line, cwd ?? Environment.CurrentDirectory));
    return 0;
}

return new Host(id, ResolveCommandLine(rest), cwd, TimeSpan.FromSeconds(linger)).Run();

// CreateProcess runs only .exe files by bare name: a command that is a .cmd or .bat on PATH (npm's claude.cmd) goes through cmd.
static string ResolveCommandLine(List<string> command)
{
    var first = command[0];
    var line = string.Join(' ', command.Select(Native.Quote));
    if (first.IndexOfAny(['\\', '/', ':']) >= 0 || Path.HasExtension(first)) return line;
    var dirs = (Environment.GetEnvironmentVariable("PATH") ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries);
    foreach (var dir in dirs)
        foreach (var ext in new[] { ".exe", ".com", ".cmd", ".bat" })
            if (File.Exists(Path.Combine(dir, first + ext)))
                return ext is ".cmd" or ".bat" ? "cmd.exe /d /c " + line : line;
    return line;
}
