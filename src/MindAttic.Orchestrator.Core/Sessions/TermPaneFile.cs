using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using MindAttic.Orchestrator.Term;

namespace MindAttic.Orchestrator.Sessions;

/// <summary>
/// The dashboard tab a hosted CLI belongs to (M.ORCH-T926): its <see cref="PaneRequest"/>, kept beside the session's registry
/// record as <c>terms\&lt;id&gt;.pane</c>, so a dashboard that restarts reattaches the tab with the same title, badge, colour and
/// login it was opened with (the record's own title follows the CLI's OSC title). Written by whoever starts the host for a tab,
/// before it launches; a session started without one (the Launcher, M.LNCH-T82) gets a request built from its record
/// (<see cref="FromRecord"/>). Never throws on an I/O failure. The extension isn't <c>.json</c>, so the registry's pruning of
/// unreadable records never takes it; <see cref="Prune"/> removes the ones whose session is gone.
/// </summary>
public static partial class TermPaneFile
{
    public const string Extension = ".pane";

    /// <summary>Session <paramref name="id"/>'s file.</summary>
    public static string PathOf(string id, string? directory = null) =>
        Path.Combine(directory ?? TermSessions.DefaultDirectory, TermProtocol.Sanitize(id) + Extension);

    /// <summary>Writes <paramref name="request"/> as session <paramref name="id"/>'s tab; false when it couldn't.</summary>
    public static bool Write(string id, PaneRequest request, string? directory = null)
    {
        var path = PathOf(id, directory);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var temp = path + "." + Guid.NewGuid().ToString("N")[..8] + ".tmp";
            File.WriteAllText(temp, PaneBroker.Serialize(request), new UTF8Encoding(false));
            File.Move(temp, path, overwrite: true);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException) { return false; }
    }

    /// <summary>Session <paramref name="id"/>'s tab; null when it has none or it can't be read.</summary>
    public static PaneRequest? Read(string id, string? directory = null)
    {
        try
        {
            var path = PathOf(id, directory);
            return File.Exists(path) ? PaneBroker.Deserialize(File.ReadAllText(path)) : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException) { return null; }
    }

    public static void Delete(string id, string? directory = null)
    {
        try { File.Delete(PathOf(id, directory)); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException) { }
    }

    /// <summary>Deletes every tab file whose session isn't in <paramref name="live"/> (ids), once a minute old (one being written
    /// for a host that hasn't registered yet is younger).</summary>
    public static void Prune(IEnumerable<string> live, string? directory = null)
    {
        var keep = live.Select(TermProtocol.Sanitize).ToHashSet(StringComparer.OrdinalIgnoreCase);
        try
        {
            var dir = directory ?? TermSessions.DefaultDirectory;
            if (!Directory.Exists(dir)) return;
            foreach (var file in Directory.GetFiles(dir, "*" + Extension))
            {
                if (keep.Contains(Path.GetFileNameWithoutExtension(file))) continue;
                if (DateTime.UtcNow - File.GetLastWriteTimeUtc(file) < TimeSpan.FromMinutes(1)) continue;
                try { File.Delete(file); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException) { }
    }

    /// <summary>The tab of a session started without one: its record's title, folder, command and repo, on its login (its email,
    /// else its config dir's), with that login's badge and colour (<see cref="LoginLook"/>); a title like "RD: Repo [Claude]"
    /// gives its badge and provider.</summary>
    public static PaneRequest FromRecord(TermSessionRecord record, Func<string?, LoginLook>? look = null)
    {
        var title = record.Title is { Length: > 0 } t ? t : record.Repo ?? record.Id;
        var match = TitleShape().Match(title);
        LoginLook? style = null;
        try { style = (look ?? LoginLook.ForConfigDir)(record.ConfigDir); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException) { }
        return new PaneRequest(title, record.Cwd ?? "")
        {
            CommandLine = record.CommandLine,
            Repo = record.Repo is { Length: > 0 } repo ? repo : match.Success ? match.Groups["repo"].Value : null,
            Provider = match.Success && match.Groups["provider"].Success ? match.Groups["provider"].Value : "Claude",
            ConfigDir = record.ConfigDir,
            Email = record.Login,
            Initials = match.Success ? match.Groups["badge"].Value : style?.Initials,
            Alias = style?.Alias,
            TabColor = style?.TabColor,
        };
    }

    [GeneratedRegex(@"^(?<badge>[A-Za-z0-9]{1,4}):\s+(?<repo>.+?)(?:\s+\[(?<provider>[^\]]+)\])?$")]
    private static partial Regex TitleShape();
}
