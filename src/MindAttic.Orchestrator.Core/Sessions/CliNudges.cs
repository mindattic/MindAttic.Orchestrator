namespace MindAttic.Orchestrator.Sessions;

/// <summary>
/// What the Orchestrator wants a CLI to know or do next, handed over without typing (M.ORCH-T864). The Orchestrator used to type
/// these nudges into the CLI's Launcher tab, straight into the input box, mid-sentence over whatever the user was writing. Now a
/// nudge is left as a <c>nudge-&lt;pid&gt;-&lt;kind&gt;.txt</c> file beside the context gate's state, and the UserPromptSubmit hook adds
/// it to the turn's additionalContext (the same place the "Subagents: up to N may run at once" line goes) and removes it, so it
/// reaches the model as context and never as typed input. A CLI that is idle takes it at its next prompt; nothing wakes it.
/// </summary>
public static class CliNudges
{
    public const string Prefix = "nudge-";

    /// <summary>Leaves (replacing any of the same kind) the nudge for the CLI process <paramref name="pid"/>.</summary>
    public static bool Leave(string directory, int pid, string kind, string text)
    {
        try
        {
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, $"{Prefix}{pid}-{kind}.txt");
            var temp = path + ".tmp";
            File.WriteAllText(temp, text);
            File.Move(temp, path, overwrite: true);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return false; }
    }

    /// <summary>Drops every <paramref name="kind"/> nudge left for a CLI no longer in <paramref name="pids"/>, so a stale one never
    /// tells a CLI to start work it no longer has.</summary>
    public static void Retain(string directory, string kind, IEnumerable<int> pids)
    {
        var keep = pids.ToHashSet();
        try
        {
            if (!Directory.Exists(directory)) return;
            foreach (var f in Directory.EnumerateFiles(directory, $"{Prefix}*-{kind}.txt"))
            {
                var name = Path.GetFileNameWithoutExtension(f)[Prefix.Length..];
                var dash = name.IndexOf('-');
                if (dash > 0 && int.TryParse(name[..dash], out var pid) && keep.Contains(pid)) continue;
                try { File.Delete(f); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    /// <summary>Drops every nudge (the global pause is on: nothing is told to carry on until it's lifted).</summary>
    public static void Clear(string directory)
    {
        try
        {
            if (!Directory.Exists(directory)) return;
            foreach (var f in Directory.EnumerateFiles(directory, $"{Prefix}*.txt"))
                try { File.Delete(f); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }
}
