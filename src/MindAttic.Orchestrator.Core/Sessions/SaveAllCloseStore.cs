using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MindAttic.Orchestrator.Sessions;

/// <summary>A Save all &amp; close run's tabs (<see cref="RestoreState"/>), alongside each one's quicksave handoff code,
/// aligned by index with <see cref="RestoreState.Tabs"/>; null where that tab's capture didn't land before it was
/// closed.</summary>
public sealed record SaveAllCloseState
{
    public required RestoreState State { get; init; }
    public IReadOnlyList<string?> Codes { get; init; } = [];
}

/// <summary>
/// Save all &amp; close's own one-shot handoff (M.ORCH-T1724), separate from the crash-recovery session-restore file
/// (<see cref="SessionRestoreStore"/>). A Save all &amp; close exit deliberately closes every tab - that's what actually
/// frees memory, since TermHost keeps each CLI alive across an ordinary clean dashboard exit by design (M.ORCH-T926) - so
/// the normal restore file would capture nothing left to reopen. This file is read once at the next start and deleted
/// (consumed) whether or not it restored anything, so a start that fails partway through never replays a stale reopen.
/// </summary>
public sealed class SaveAllCloseStore(string? directory = null)
{
    public const string FileName = "pending.json";

    public string Directory { get; } = directory ?? Path.Combine(Settings.JsonStore.LiveDirectory, "save-all-close");

    public string PendingPath => Path.Combine(Directory, FileName);

    private static readonly JsonSerializerOptions Json = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    /// <summary>Writes <paramref name="state"/> as the pending file: a temp file first, then moved over it, so a crash
    /// mid-write never leaves a half-written one to be read at the next start.</summary>
    public bool Save(SaveAllCloseState state)
    {
        string? temp = null;
        try
        {
            System.IO.Directory.CreateDirectory(Directory);
            temp = Path.Combine(Directory, $"{FileName}.{Environment.ProcessId}.tmp");
            File.WriteAllText(temp, JsonSerializer.Serialize(state, Json), new UTF8Encoding(false));
            File.Move(temp, PendingPath, overwrite: true);
            return true;
        }
        catch (Exception ex)
        {
            try { if (temp is not null && File.Exists(temp)) File.Delete(temp); } catch (Exception) { }
            try { OrchestratorLog.For("Restore").Error(ex, "Couldn't write the save-all-close file {Path:l}", PendingPath); } catch (Exception) { }
            return false;
        }
    }

    /// <summary>The pending file, when one exists and reads back; null otherwise. Never throws.</summary>
    public SaveAllCloseState? Load()
    {
        try { return File.Exists(PendingPath) ? JsonSerializer.Deserialize<SaveAllCloseState>(File.ReadAllText(PendingPath), Json) : null; }
        catch (Exception ex)
        {
            try { OrchestratorLog.For("Restore").Warning(ex, "Couldn't read the save-all-close file {Path:l}", PendingPath); } catch (Exception) { }
            return null;
        }
    }

    /// <summary>Deletes the pending file (consumed): missing already is not an error.</summary>
    public void Delete()
    {
        try { File.Delete(PendingPath); }
        catch (Exception ex) { try { OrchestratorLog.For("Restore").Warning(ex, "Couldn't delete the save-all-close file {Path:l}", PendingPath); } catch (Exception) { } }
    }
}
