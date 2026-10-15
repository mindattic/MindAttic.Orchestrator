using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

public sealed class SourceTextTests
{
    // UTF-8 read once as Windows-1252 and saved: '·' becomes "Â·", '→' "â†’", '…' "â€¦", '─' "â”€".
    private static readonly string[] Mojibake = ["Â·", "Â ", "â€", "â†", "â”", "Ã©"];

    [Test]
    public void No_source_or_doc_file_carries_utf8_mangled_as_latin1()
    {
        // Found live (MAO-T99): `orchestrator settings` printed "Â·" between every field.
        var root = TestContext.CurrentContext.TestDirectory;
        while (!Directory.Exists(Path.Combine(root, ".git"))) root = Path.GetDirectoryName(root) ?? throw new AssertionException("no repo root above the tests");
        var bad = new[] { "src", "tests" }.SelectMany(d => Directory.EnumerateFiles(Path.Combine(root, d), "*.cs", SearchOption.AllDirectories))
            .Append(Path.Combine(root, "README.md"))
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .Where(f => !f.EndsWith(nameof(SourceTextTests) + ".cs"))
            .SelectMany(f => File.ReadLines(f).Select((line, i) => (f, i + 1, line)))
            .Where(x => Mojibake.Any(x.line.Contains))
            .Select(x => $"{Path.GetRelativePath(root, x.f)}:{x.Item2}")
            .ToList();
        Assert.That(bad, Is.Empty);
    }
}
