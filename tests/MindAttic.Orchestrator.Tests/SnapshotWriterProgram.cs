namespace MindAttic.Orchestrator.Tests;

/// <summary>
/// The test assembly's entry point. The test host never calls it; <see cref="SnapshotFileTests"/> starts the
/// assembly as child processes with <see cref="Verb"/> to get two separate processes publishing snapshots at once.
/// </summary>
public static class SnapshotWriterProgram
{
    public const string Verb = "write-snapshots";

    public static int Main(string[] args)
    {
        if (args.Length != 4 || args[0] != Verb) return 2;
        var file = new SnapshotFile(args[1]);
        var count = int.Parse(args[3]);
        for (var k = 0; k < count; k++)
        {
            // Vary the size so a torn mix of two writes couldn't line up by chance.
            file.Write(SnapshotFileTests.Sample(DateTime.UtcNow, repos: 1 + k % 4, marker: $"{args[2]}-{k}"));
        }
        return 0;
    }
}
