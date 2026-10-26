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
}
