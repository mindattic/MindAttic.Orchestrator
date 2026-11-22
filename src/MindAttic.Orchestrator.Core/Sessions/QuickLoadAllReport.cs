using System.Text.Json;
using System.Text.RegularExpressions;

namespace MindAttic.Orchestrator.Sessions;

/// <summary>
/// What a Quick Load All asked for from a CLI did (M.ORCH-T2070): <c>/quickload all</c> runs <c>orchestrator quickload-all</c>,
/// which hands the running dashboard <c>--quickload-all &lt;nonce&gt;</c> and waits; the dashboard runs Quick Load All and
/// writes this report as <c>quickload-all-&lt;nonce&gt;.json</c> in <see cref="Directory"/>, which the CLI reads, prints and
/// deletes. Nothing here throws.
/// </summary>
public sealed partial record QuickLoadAllReport(string Nonce, DateTime FinishedUtc, double ElapsedSeconds, int Acted, int Total, string Note,
    IReadOnlyList<string> Resumed)
{
    /// <summary>Where reports land: next to the save states they restore from.</summary>
    public static string Directory { get; } = Path.Combine(Settings.JsonStore.LiveDirectory, "save-states", "quickload-all");

    /// <summary>A fresh nonce for one request.</summary>
    public static string NewNonce() => Guid.NewGuid().ToString("N");

    /// <summary>Whether <paramref name="nonce"/> is one this side could have made: 8 to 64 letters and digits, so it can never
    /// name a path or an option.</summary>
    public static bool IsNonce(string? nonce) => nonce is { Length: >= 8 and <= 64 } && NonceShape().IsMatch(nonce);

    /// <summary>The report file for <paramref name="nonce"/>.</summary>
    public static string PathOf(string nonce, string? directory = null) => Path.Combine(directory ?? Directory, $"quickload-all-{nonce}.json");

    /// <summary>Writes the report (atomically, so a reader never sees half of it); false when it couldn't be written.</summary>
    public bool Write(string? directory = null)
    {
        try
        {
            var path = PathOf(Nonce, directory);
            System.IO.Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var temp = path + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(this));
            File.Move(temp, path, overwrite: true);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            try { OrchestratorLog.For("SaveState").Warning(ex, "Couldn't write the Quick Load All report {Nonce:l}", Nonce); } catch (Exception) { }
            return false;
        }
    }

    /// <summary>The report for <paramref name="nonce"/>, or null while there's none (or it can't be read yet).</summary>
    public static QuickLoadAllReport? Read(string nonce, string? directory = null)
    {
        try
        {
            var path = PathOf(nonce, directory);
            return File.Exists(path) ? JsonSerializer.Deserialize<QuickLoadAllReport>(File.ReadAllText(path)) : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or NotSupportedException) { return null; }
    }

    /// <summary>Deletes the report for <paramref name="nonce"/> once it's been read.</summary>
    public static void Delete(string nonce, string? directory = null)
    {
        try { File.Delete(PathOf(nonce, directory)); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    [GeneratedRegex("^[A-Za-z0-9]+$", RegexOptions.CultureInvariant)]
    private static partial Regex NonceShape();
}
