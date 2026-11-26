namespace MindAttic.Orchestrator.Sessions;

/// <summary>
/// Splices the Vault-stored Kimi API key into a provider entry of <c>~/.kimi-code/config.toml</c> (M.ORCH-T2042, ported from
/// MindAttic.Launcher's own <c>KimiConfigSync</c>): a targeted text edit (locate the table, replace its <c>api_key</c> line)
/// rather than a TOML parse-and-reserialize, so every other table, model and comment in the user's file survives untouched.
/// Kimi reads its key only from that file, never an environment variable, so a Kimi session is authenticated by syncing it
/// before the CLI starts. Idempotent: no write when the key already matches.
/// </summary>
public static class KimiConfigSync
{
    /// <summary>The provider table whose <c>api_key</c> is kept in sync with the Vault.</summary>
    public const string ProviderTable = "managed:kimi-code";

    /// <summary>The user's real Kimi config, <c>~\.kimi-code\config.toml</c>.</summary>
    public static string DefaultConfigPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".kimi-code", "config.toml");

    /// <summary>
    /// Locates <c>[providers."{table}"]</c> in <paramref name="contents"/> and rewrites its <c>api_key = "..."</c> line to
    /// <paramref name="apiKey"/>. False (with <paramref name="result"/> the original contents) when the table or its
    /// <c>api_key</c> line isn't there (no config structure the CLI didn't already declare is invented), or when the provider
    /// already has an <c>.oauth</c> sub-table: Kimi refuses a provider with both an <c>api_key</c> and <c>oauth</c> set, so one
    /// on OAuth stays on it. True means "now correct", whether or not anything changed.
    /// </summary>
    public static bool TrySetApiKey(string contents, string table, string apiKey, out string result)
    {
        result = contents;
        if (string.IsNullOrEmpty(contents)) return false;

        var header = $"[providers.\"{table}\"]";
        var headerIdx = contents.IndexOf(header, StringComparison.Ordinal);
        if (headerIdx < 0) return false;

        if (contents.Contains($"providers.\"{table}\".oauth", StringComparison.Ordinal) ||
            contents.Contains($"providers.{table}.oauth", StringComparison.Ordinal))
            return false;

        // The table's block runs until the next top-level "[" header or the end.
        var blockStart = headerIdx + header.Length;
        var nextHeaderIdx = contents.IndexOf("\n[", blockStart, StringComparison.Ordinal);
        var blockEnd = nextHeaderIdx < 0 ? contents.Length : nextHeaderIdx;

        // The line whose key is exactly api_key, not a comment or env_api_key mentioning it (M.ORCH-T2292).
        var keyIdx = -1;
        for (var lineStart = contents.IndexOf('\n', blockStart) + 1; lineStart > 0 && lineStart < blockEnd;
             lineStart = contents.IndexOf('\n', lineStart) + 1)
        {
            var i = lineStart;
            while (i < blockEnd && contents[i] is ' ' or '\t') i++;
            if (!contents.AsSpan(i, blockEnd - i).StartsWith("api_key", StringComparison.Ordinal)) continue;
            var j = i + "api_key".Length;
            while (j < blockEnd && contents[j] is ' ' or '\t') j++;
            if (j < blockEnd && contents[j] == '=') { keyIdx = i; break; }
        }
        if (keyIdx < 0) return false;

        var lineEnd = contents.IndexOf('\n', keyIdx);
        if (lineEnd < 0 || lineEnd > blockEnd) lineEnd = blockEnd;

        var oldLine = contents[keyIdx..lineEnd].TrimEnd('\r');
        var newLine = $"api_key = \"{apiKey}\"";
        if (oldLine == newLine) return true;

        // Keep a CRLF file's '\r' where the old line had one.
        var tail = contents[keyIdx..lineEnd].EndsWith('\r') ? "\r" : "";
        result = contents[..keyIdx] + newLine + tail + contents[lineEnd..];
        return true;
    }

    /// <summary>True when the file was rewritten; false on a no-op (the key already matched) or any failure: a blank key, no
    /// file, the table or line not found, or an IO error.</summary>
    public static bool EnsureApiKey(string configPath, string table, string apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey) || !File.Exists(configPath)) return false;
        try
        {
            var contents = File.ReadAllText(configPath);
            if (!TrySetApiKey(contents, table, apiKey, out var updated) || updated == contents) return false;
            var temp = configPath + ".tmp";
            File.WriteAllText(temp, updated);
            File.Move(temp, configPath, overwrite: true);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
