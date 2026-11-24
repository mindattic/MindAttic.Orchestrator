using MindAttic.Orchestrator.Sessions;

namespace MindAttic.Orchestrator.Wpf.ViewModels;

/// <summary>What Settings &gt; Users (<c>UsersWindow</c>, M.ORCH-T2184) reads of each login's tab.</summary>
public sealed partial class MainViewModel
{
    /// <summary>The login's email for its Users row: this tab's own, or the global login's on its own tab; null for the API key.</summary>
    internal string? UsersEmail => IsApiKeyTab ? null : AccountDirs.Normalize(LockEmail);

    /// <summary>The titles of this per-CLI login's running project CLIs: live panes on its folder, not its Launcher or General
    /// Prompt, which Delete closes on its own. Users refuses to delete a login while any run, so no work is cut off unsaved.</summary>
    internal IReadOnlyList<string> RunningCliTitles()
    {
        if (IsApiKeyTab || AccountDirs.Normalize(ScopeEmail) is not { } login) return [];
        var root = Root;
        return root.AllTerminals.Panes
            .Where(p => !p.Ended && !p.Request.IsMenu && string.Equals(TreeLoginKey(p), login, StringComparison.OrdinalIgnoreCase)
                && !IsGeneralPromptOf(p, login, root.WorkspaceRoot))
            .Select(p => p.Title)
            .ToList();
    }
}
