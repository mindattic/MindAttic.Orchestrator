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
    /// The top-level window (MainWindow.xaml): the person/org tabs as a left sidebar (M.ORCH-T1431), then beside it the signed-in
    /// person's scrolling body — the terminal (M.ORCH-T1302), the pinned 5-hour/weekly pills and stat tiles, and the login
    /// with its already-loaded active CLIs (M.ORCH-T2225, in place of the old 5-hour chart and ticket queue) — with the
    /// global login's own Workspace section (allocate, holds, reminders, finished work, warnings) standing in for a
    /// "Miscellaneous" panel (M.ORCH-T763, M.ORCH-T825).
    /// </summary>
    private static readonly MockupPanel[] Dashboard =
    [
        new(0, 0, 16, 23, "Users (sidebar)"),
        new(16, 0, 38, 13, "Terminal (CLI tabs)"),
        new(54, 0, 26, 6, "Stats"),
        new(54, 6, 26, 7, "Active CLIs"),
        new(16, 13, 38, 10, "Workspace (continued)"),
        new(54, 13, 26, 10, "Workspace (Misc)"),
        new(0, 23, 80, 3, "Status Bar"),
    ];

    /// <summary>
    /// Settings since LAMP (M.ORCH-T1308, M.ORCH-T1311): the cog opens MindAttic.Vault.Desktop at its "MindAttic.Orchestrator"
    /// tab — the app list on the left, the tab's settings grouped one full-width row per setting (Budget, Pacing, Scheduler,
    /// Logins, Reviews, Alerts, Dashboard, Folders, Gateway), the Claude key assignment, and Save.
    /// </summary>
    private static readonly MockupPanel[] Settings =
    [
        new(0, 0, 18, 16, "Apps (Vault tabs)"),
        new(18, 0, 52, 3, "MindAttic.Orchestrator"),
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
        [Description("Which screen to draw: dashboard, settings or logins. Left out, or not one of those, lists the names.")]
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
