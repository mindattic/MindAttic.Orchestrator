using System.Text.RegularExpressions;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>No test pops a window onto the user's screen (M.ORCH-T530): agents run these tests every few minutes, and
/// windows shown on-screen turned up as blank 'testhost' windows. Every window a test shows goes through
/// <see cref="OffScreenWindow"/>; this scans the test sources for any other way in.</summary>
public sealed class TestWindowHygieneTests
{
    private const string Helper = nameof(OffScreenWindow) + ".cs";

    /// <summary>Every WPF window type the tests could build: System.Windows.Window and the dashboard's own.</summary>
    private static readonly string[] WindowTypes =
        new[] { typeof(System.Windows.Window).Assembly, typeof(MindAttic.Orchestrator.Wpf.MainWindow).Assembly }
            .SelectMany(a => { try { return a.GetTypes(); } catch (System.Reflection.ReflectionTypeLoadException e) { return e.Types.OfType<Type>().ToArray(); } })
            .Where(t => typeof(System.Windows.Window).IsAssignableFrom(t))
            .Select(t => t.Name).Distinct().ToArray();

    private static readonly (Regex Pattern, string Why)[] Rules =
    [
        (new(@"(?<!\bOffScreenWindow)\.\s*(Show|ShowDialog|Activate)\s*\(\s*\)"),"shows or activates a window; use OffScreenWindow.Show"),
        (new(@"\bnew\s+(?:[\w.]+\.)?(" + string.Join("|", WindowTypes.Select(Regex.Escape)) + @")\s*[({]"), "builds a window; use OffScreenWindow"),
        (new(@"\b(?:System\.Windows\.)?Window\??\s+\w+\s*(?:=|\([^)]*\)\s*=>)\s*new\s*\("), "builds a window; use OffScreenWindow"),
        (new(@"DashboardWindow\.Restore\(\s*\w+\s*\)"), "activates the window for real; pass recording activate/focus calls"),
        (new(@"UseShellExecute\s*=\s*true"), "may launch a GUI process onto the screen"),
        (new(@"Orchestrator\.Wpf\.exe"), "launches the real dashboard"),
    ];

    private static IEnumerable<string> Violations(string file, string source) =>
        source.Split('\n').Select((line, i) => (line, i))
            .Where(l => !l.line.TrimStart().StartsWith("//") && !l.line.TrimStart().StartsWith("///"))
            .SelectMany(l => Rules.Where(r => r.Pattern.IsMatch(l.line)).Select(r => $"{file}:{l.i + 1}: {r.Why}: {l.line.Trim()}"));

    [Test]
    public void No_test_shows_a_window_except_off_screen_through_the_shared_helper()
    {
        var root = TestContext.CurrentContext.TestDirectory;
        while (!Path.Exists(Path.Combine(root, ".git"))) root = Path.GetDirectoryName(root) ?? throw new AssertionException("no repo root above the tests");
        var tests = Path.Combine(root, "tests");
        var files = Directory.EnumerateFiles(tests, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Split(Path.DirectorySeparatorChar).Any(d => d is "bin" or "obj"))
            .Where(f => Path.GetFileName(f) is not (Helper or nameof(TestWindowHygieneTests) + ".cs"))
            .ToList();

        Assert.That(files, Has.Some.EndsWith(nameof(DashboardWindowTests) + ".cs"), "the scan sees the window tests");
        Assert.That(files.SelectMany(f => Violations(Path.GetRelativePath(root, f), File.ReadAllText(f))), Is.Empty);
    }

    [Test]
    public void The_scan_catches_the_windows_that_popped_up()
    {
        string[] bad =
        [
            "    private static Window NewWindow() => new() { Width = 200, Height = 120, ShowActivated = false, ShowInTaskbar = false };",
            "        window.Show();",
            "        dialog.ShowDialog();",
            "        window.Activate();",
            "        var window = new Window { Content = spinner };",
            "        var main = new MainWindow();",
            "        Window w = new() { Width = 1 };",
            "        DashboardWindow.Restore(window);",
            "        var psi = new ProcessStartInfo(exe) { UseShellExecute = true };",
            "        Process.Start(Path.Combine(dir, \"MindAttic.Orchestrator.Wpf.exe\"));",
        ];
        foreach (var line in bad) Assert.That(Violations("x.cs", line), Is.Not.Empty, line);

        string[] fine =
        [
            "        var window = OffScreenWindow.Show(120, 120, spinner);",
            "        var window = OffScreenWindow.Show();",
            "        var reported = new ReportedWindow(24, Day.AddHours(15));",
            "        private static LedgerWindow Window(int n, string? account = null) =>",
            "        DashboardWindow.Restore(window, w => calls.Add(\"a\"), w => calls.Add(\"f\"));",
            "        var psi = new ProcessStartInfo(\"python\") { UseShellExecute = false, CreateNoWindow = true };",
        ];
        foreach (var line in fine) Assert.That(Violations("x.cs", line), Is.Empty, line);
    }
}
