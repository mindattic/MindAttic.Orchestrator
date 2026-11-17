using System.ComponentModel;
using MindAttic.Orchestrator;
using MindAttic.Orchestrator.Scheduling;
using Spectre.Console;
using Spectre.Console.Cli;

/// <summary><c>orchestrator branches ff [&lt;repo&gt;...]</c> (M.ORCH-T1636): on the PC, after the host merged a signed-off work branch
/// into main and pushed, fast-forwards main in the named repos' checkouts (every repo in the workspace when none is named). Only a
/// checkout on main and only a fast-forward: one with local commits or changes in the way is left as it is and named.</summary>
internal sealed class BranchesFastForwardCommand : Command<BranchesFastForwardCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "[repo]")]
        [Description("The repos to fast-forward, by folder name in the workspace or by path; none: every repo in the workspace.")]
        public string[] Repos { get; init; } = [];
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        var root = OrchestratorOptions.Load().WorkspaceRoot;
        var repos = settings.Repos.Length > 0
            ? settings.Repos.Select(r => Directory.Exists(Path.Combine(root, r)) ? Path.Combine(root, r) : Path.GetFullPath(r)).ToList()
            : (Directory.Exists(root) ? Directory.EnumerateDirectories(root) : []).Where(IsRepo).Order(StringComparer.OrdinalIgnoreCase).ToList();
        var problems = 0;
        foreach (var repo in repos)
        {
            var name = Path.GetFileName(repo.TrimEnd(Path.DirectorySeparatorChar));
            if (!IsRepo(repo))
            {
                problems++;
                AnsiConsole.MarkupLine($"[red]{Markup.Escape(name)}: not a git checkout ({Markup.Escape(repo)})[/]");
                continue;
            }
            var (ok, message) = BranchMerges.FastForward(repo);
            if (!ok) problems++;
            AnsiConsole.MarkupLine($"{(ok ? "" : "[yellow]")}{Markup.Escape(name)}: {Markup.Escape(message)}{(ok ? "" : "[/]")}");
        }
        if (repos.Count == 0) AnsiConsole.MarkupLine("[grey]No repos to fast-forward.[/]");
        return problems == 0 ? 0 : 1;
    }

    private static bool IsRepo(string dir) => Directory.Exists(Path.Combine(dir, ".git")) || File.Exists(Path.Combine(dir, ".git"));
}
