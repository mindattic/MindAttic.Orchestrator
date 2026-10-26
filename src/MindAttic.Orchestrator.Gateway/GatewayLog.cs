namespace MindAttic.Orchestrator.Gateway;

/// <summary>
/// The gateway's own log, <c>gateway.log</c> in its data folder (rolled at 4 MB to <c>gateway.log.1</c>). Lines say who attached
/// to what and how it ended. Never a token, a key, a request header or a byte of terminal output goes here. Writing never throws.
/// </summary>
internal sealed class GatewayLog
{
    private const long RollBytes = 4L * 1024 * 1024;
    private readonly object gate = new();
    private readonly string? path;
    private readonly Action<string>? sink;

    public GatewayLog(string? directory, Action<string>? sink = null)
    {
        this.sink = sink;
        if (directory is null) return;
        try
        {
            Directory.CreateDirectory(directory);
            path = Path.Combine(directory, "gateway.log");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    public void Write(string line)
    {
        var text = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{Environment.ProcessId}] {line}";
        try { sink?.Invoke(text); } catch (Exception) { }
        if (path is null) return;
        lock (gate)
        {
            try
            {
                if (File.Exists(path) && new FileInfo(path).Length > RollBytes) File.Move(path, path + ".1", overwrite: true);
                File.AppendAllText(path, text + Environment.NewLine);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
    }

    /// <summary>The exception's type and message only, never its data: a message can't carry a token the gateway never puts in one.</summary>
    public void Error(Exception ex, string what) => Write($"{what}: {ex.GetType().Name}: {ex.Message}");
}
