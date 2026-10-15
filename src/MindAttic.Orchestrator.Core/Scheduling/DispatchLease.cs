namespace MindAttic.Orchestrator.Scheduling;

/// <summary>
/// Lets only one orchestrator process feed the /autopilot CLIs: the logon daemon and an open dashboard would
/// otherwise both type the same story into a CLI. It's a lock file held open with no sharing, so it's
/// released when the holder exits, crashes included, and works from any thread.
/// </summary>
public sealed class DispatchLease(string directory) : IDisposable
{
    private FileStream? held;

    public string Path { get; } = System.IO.Path.Combine(directory, "dispatch.lock");

    public bool IsHeld => held is not null;

    /// <summary>True when this process holds the lease, taking it if it's free.</summary>
    public bool TryAcquire()
    {
        if (held is not null) return true;
        try
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
            var stream = new FileStream(Path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read);
            stream.SetLength(0);
            var pid = System.Text.Encoding.ASCII.GetBytes(Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture));
            stream.Write(pid);
            stream.Flush();
            held = stream;
            return true;
        }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
    }

    public void Dispose()
    {
        held?.Dispose();
        held = null;
    }
}
