using System.Text.Json;

namespace MindAttic.Orchestrator.Activity;

/// <summary>
/// Reads the shared runner's per-repo quicksave handoff (<c>.agent\agent-session.json</c>, written by
/// <c>prose-agent.ps1 save</c>) to tell when a <c>/quicksave</c> forced into a CLI's terminal has actually landed
/// (Save all &amp; close, M.ORCH-T1724). The handoff is a single slot per repo root, shared by every CLI checked out
/// there (this project never uses worktrees): <see cref="TryReadCode"/> only reports a save written at or after
/// <paramref name="baselineUtc"/>, so a prior, unconsumed save already sitting there isn't mistaken for the one just
/// asked for.
/// </summary>
public static class QuicksaveCapture
{
    /// <summary>The handoff file a <c>/quicksave</c> run from <paramref name="repoRoot"/> writes.</summary>
    public static string SessionPath(string repoRoot) => Path.Combine(repoRoot, ".agent", "agent-session.json");

    /// <summary>The handoff's shareable code (e.g. <c>lively-oxen</c>), when <paramref name="path"/> holds one saved at or
    /// after <paramref name="baselineUtc"/>; null while the file is missing, unreadable, or still the one from before it.</summary>
    public static string? TryReadCode(string path, DateTime baselineUtc)
    {
        try
        {
            if (!File.Exists(path)) return null;
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            var root = doc.RootElement;
            if (!root.TryGetProperty("updatedAt", out var updatedEl) || updatedEl.ValueKind != JsonValueKind.String
                || !DateTime.TryParse(updatedEl.GetString(), null, System.Globalization.DateTimeStyles.RoundtripKind, out var updated))
                return null;
            if (updated.ToUniversalTime() < baselineUtc) return null;
            return root.TryGetProperty("code", out var codeEl) && codeEl.ValueKind == JsonValueKind.String ? codeEl.GetString() : null;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException) { return null; }
    }
}
