using System.ComponentModel;
using MindAttic.Helpers.Ascii;
using Spectre.Console;
using Spectre.Console.Cli;

/// <summary>
/// Named Braille-dot wireframes of Orchestrator's own screens (M.ORCH-T1309), drawn with
/// MindAttic.Helpers.Ascii.AsciiMockup (M.HELP-T20) — the first cross-repo consumer of MindAttic.Helpers. A planning/
/// documentation tool: the panels and labels below are read off the real WPF layout (MainWindow.xaml), not invented,
/// so a mockup stays a quick, shareable picture of what the app actually shows.
/// </summary>
internal static class OrchestratorMockups
{
    /// <summary>
    /// The top-level window (MainWindow.xaml): the person/org tab strip (M.ORCH-T803) across the top, then the signed-in
    /// person's scrolling body — the terminal (M.ORCH-T1302), the pinned 5-hour/weekly pills and stat tiles, the 5-hour
    /// utilization chart, and their ticket queue — with the global login's own Workspace section (allocate, holds,
    /// reminders, finished work, warnings) standing in for a "Miscellaneous" panel (M.ORCH-T763, M.ORCH-T825).
    /// </summary>
    private static readonly MockupPanel[] Dashboard =
    [
        new(0, 0, 80, 3, "Users (person / org tabs)"),
        new(0, 3, 48, 10, "Terminal (CLI tabs)"),
        new(48, 3, 32, 5, "Stats"),
        new(48, 8, 32, 5, "5-Hour Chart"),
        new(0, 13, 48, 10, "Queue (this person's tickets)"),
        new(48, 13, 32, 10, "Workspace (Misc)"),
        new(0, 23, 80, 3, "Status Bar"),
    ];

    /// <summary>
    /// A person's ticket queue (MainWindow.xaml's QueueStatusTabs): Active, Pending, Closed and Won't do, each with its
    /// own count (M.ORCH-T849), the selected tab's assigned tickets below, and — only on the global login's tab — the
    /// unassigned queue (M.ORCH-T842). Every ticket id opens Tickets Desktop; this view never browses tickets itself
    /// (M.ORCH-T762).
    /// </summary>
    private static readonly MockupPanel[] Queue =
    [
        new(0, 0, 17, 3, "Active"),
        new(17, 0, 17, 3, "Pending"),
        new(34, 0, 17, 3, "Closed"),
        new(51, 0, 19, 3, "Won't do"),
        new(0, 3, 70, 10, "Assigned tickets"),
        new(0, 13, 70, 6, "Unassigned (global tab only)"),
    ];

    /// <summary>
    /// Settings since LAMP (M.ORCH-T1308, M.ORCH-T1311): the cog opens MindAttic.Vault.Desktop at its "MindAttic Orchestrator"
    /// tab — the app list on the left, the tab's settings grouped one full-width row per setting (Budget, Pacing, Scheduler,
    /// Logins, Reviews, Alerts, Dashboard, Folders, Gateway), the Claude key assignment, and Save.
    /// </summary>
    private static readonly MockupPanel[] Settings =
    [
        new(0, 0, 18, 16, "Apps (Vault tabs)"),
        new(18, 0, 52, 3, "MindAttic Orchestrator"),
        new(18, 3, 52, 8, "Grouped settings (one row each)"),
        new(18, 11, 52, 3, "Keys: Claude assignment"),
        new(18, 14, 52, 2, "Save"),
    ];

    /// <summary>
    /// The Logins modal (MainWindow.xaml's LoginsModal, button beside the cog): the org-wide login actions that sat on the old
    /// Settings dialog's Logins tab — Add login (email + button), Sign in all and Report — with their result lines below.
    /// </summary>
    private static readonly MockupPanel[] Logins =
    [
        new(0, 0, 50, 3, "Email"),
        new(50, 0, 20, 3, "Add login"),
        new(0, 3, 35, 3, "Sign in all"),
        new(35, 3, 35, 3, "Report"),
        new(0, 6, 70, 4, "Result + billing detail"),
    ];

    /// <summary>Every named mockup this command knows, in the order <c>mockup</c> with no argument lists them.</summary>
    public static readonly IReadOnlyList<(string Name, string Title, int Width, int Height, MockupPanel[] Panels)> All =
    [
        ("dashboard", "Orchestrator — Dashboard (MainWindow.xaml)", 80, 26, Dashboard),
        ("queue", "Orchestrator — Queue (a person's tab)", 70, 19, Queue),
        ("settings", "Orchestrator — Settings (MindAttic Vault tab)", 70, 16, Settings),
        ("logins", "Orchestrator — Logins (button beside the cog)", 70, 10, Logins),
    ];

    /// <summary>The rendered wireframe for <paramref name="name"/> (any casing), or <see langword="null"/> when there's no mockup by that name.</summary>
    public static string? Render(string name)
    {
        foreach (var m in All)
            if (string.Equals(m.Name, name, StringComparison.OrdinalIgnoreCase))
                return AsciiMockup.Render(m.Panels, m.Width, m.Height, m.Title);
        return null;
    }
}

/// <summary>
/// <c>orchestrator mockup &lt;SCREEN&gt;</c>: a Braille-dot wireframe of one of Orchestrator's own screens, drawn with
/// MindAttic.Helpers.Ascii.AsciiMockup (M.ORCH-T1309). With no screen, or one it doesn't know, it lists the names
/// instead of crashing.
/// </summary>
internal sealed class MockupCommand : Command<MockupCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "[SCREEN]")]
        [Description("Which screen to draw: dashboard, queue or settings. Left out, or not one of those, lists the names.")]
        public string? Screen { get; init; }
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        if (settings.Screen is { Length: > 0 } name && OrchestratorMockups.Render(name) is { } wireframe)
        {
            AnsiConsole.WriteLine(wireframe);
            return 0;
        }
        if (settings.Screen is { Length: > 0 } unknown)
            AnsiConsole.MarkupLine($"[red]No mockup named {Markup.Escape(unknown)}.[/]");
        AnsiConsole.MarkupLine("Mockups: " + string.Join(", ", OrchestratorMockups.All.Select(m => m.Name)));
        return settings.Screen is { Length: > 0 } ? 1 : 0;
    }
}
