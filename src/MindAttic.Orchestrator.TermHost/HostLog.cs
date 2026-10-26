namespace MindAttic.Orchestrator.TermHost;

/// <summary>
/// A host's own log, <c>%LOCALAPPDATA%\MindAttic\MindAttic.Orchestrator\terms\&lt;id&gt;.log</c>. A plain file, not OrchestratorLog:
/// that lives in Core and writes through Vault and SQLite, which a small standalone host shouldn't load. Logging never throws.
/// </summary>
internal sealed class HostLog : IDisposable
{
    public static string Directory { get; } = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MindAttic", "MindAttic.Orchestrator", "terms");

    private readonly object gate = new();
    private StreamWriter? writer;

    public HostLog(string id)
    {
        try
        {
            System.IO.Directory.CreateDirectory(Directory);
            Path = System.IO.Path.Combine(Directory, Term.TermProtocol.Sanitize(id) + ".log");
            writer = new StreamWriter(new FileStream(Path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete)) { AutoFlush = true };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException) { writer = null; }
    }

    public string? Path { get; }

    public void Write(string message)
    {
        lock (gate)
        {
            try { writer?.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{Environment.ProcessId}] {message}"); }
            catch (Exception ex) when (ex is IOException or ObjectDisposedException) { }
        }
    }

    public void Error(Exception ex, string message) => Write($"{message}: {ex}");

    public void Dispose()
    {
        lock (gate)
        {
            try { writer?.Dispose(); } catch (IOException) { }
            writer = null;
        }
    }
}
