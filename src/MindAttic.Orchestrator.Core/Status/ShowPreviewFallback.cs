using System.Diagnostics;

namespace MindAttic.Orchestrator.Status;

/// <summary>
/// How the daemon shows a preview set no MindAttic.Media.Viewer is present to take (<see cref="AppBus.ShowPreview"/>, M.ORCH-T2133): it
/// starts the installed viewer on the set (<c>MindAttic.Media.Viewer.exe "&lt;set&gt;" [--select FILE]</c>), whose single-instance handoff
/// passes it on to a copy that is still starting; without the viewer installed it opens the set's first media file with its default
/// app. The daemon runs in the owner's interactive session, so either appears on the owner's desktop. Only a media file is ever
/// shell-opened. Never throws.
/// </summary>
public static class ShowPreviewFallback
{
    private static readonly Serilog.ILogger log = OrchestratorLog.For("Preview");

    /// <summary>Where run.ps1 deploys MindAttic.Media.Viewer.</summary>
    public const string ViewerExe = @"C:\Apps\MindAttic.Media.Viewer\MindAttic.Media.Viewer.exe";

    private static readonly HashSet<string> MediaExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp", ".tif", ".tiff", ".heic",
        ".mp4", ".mkv", ".webm", ".mov", ".avi", ".wmv", ".m4v",
        ".wav", ".mp3", ".flac", ".ogg", ".m4a", ".aac", ".wma",
    };

    /// <summary>Shows <paramref name="request"/>'s set: what it did (<c>launched</c>, <c>opened</c>) or <c>none</c>.</summary>
    public static string Show(ShowPreviewRequest request, string viewerExe = ViewerExe)
    {
        try
        {
            var set = request.Set.Trim();
            if (!Path.IsPathFullyQualified(set) || !Directory.Exists(set))
            {
                log.Warning("A preview from {From:l} names no set folder: {Set:l}", request.From ?? "?", set);
                return "none";
            }
            var select = SelectedFile(set, request.Select);
            if (File.Exists(viewerExe))
            {
                var start = new ProcessStartInfo(viewerExe) { UseShellExecute = false, WorkingDirectory = set };
                start.ArgumentList.Add(set);
                if (select is not null)
                {
                    start.ArgumentList.Add("--select");
                    start.ArgumentList.Add(Path.GetFileName(select));
                }
                using var _ = Process.Start(start);
                log.Information("Started the media viewer on {Set:l} for {From:l}", set, request.From ?? "?");
                return "launched";
            }
            if ((select ?? FirstMedia(set)) is not { } file)
            {
                log.Warning("A preview set from {From:l} holds no media yet: {Set:l}", request.From ?? "?", set);
                return "none";
            }
            using var opened = Process.Start(new ProcessStartInfo(file) { UseShellExecute = true });
            log.Information("No media viewer installed; opened {File:l} for {From:l}", file, request.From ?? "?");
            return "opened";
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            log.Warning(ex, "Couldn't show the preview set {Set:l}", request.Set);
            return "none";
        }
    }

    /// <summary>Whether <paramref name="file"/> is a media file a preview set shows (not a sidecar or a poster thumbnail).</summary>
    public static bool IsMedia(string file) =>
        MediaExtensions.Contains(Path.GetExtension(file)) && !file.EndsWith(".thumb.jpg", StringComparison.OrdinalIgnoreCase);

    /// <summary>The set's media file named <paramref name="select"/>, when it is one directly inside the set; else null.</summary>
    private static string? SelectedFile(string set, string? select)
    {
        if (string.IsNullOrWhiteSpace(select)) return null;
        var name = Path.GetFileName(select.Trim());
        var path = Path.Combine(set, name);
        return name.Length > 0 && IsMedia(name) && File.Exists(path) ? path : null;
    }

    /// <summary>The set's first media file by name (its files are numbered <c>NNN_tag.ext</c>); null when it has none.</summary>
    private static string? FirstMedia(string set) =>
        Directory.EnumerateFiles(set).Where(IsMedia).OrderBy(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase).FirstOrDefault();
}
