using System.Diagnostics;
using System.Globalization;

namespace MindAttic.Orchestrator.Backup;

/// <summary>Where and how a backup's <c>Databases</c> folder is uploaded off the machine.</summary>
public sealed record AzureUploadOptions(
    bool Enabled, string StorageAccount, string Container, int KeepNights, TimeSpan Timeout, TimeSpan ProgressInterval)
{
    /// <summary>The Launcher's defaults: on, its storage account and container, seven nights, a 22-hour limit, progress every 5 minutes.</summary>
    public static AzureUploadOptions Default { get; } = new(
        true,
        AzureBlobUpload.DefaultStorageAccount,
        AzureBlobUpload.DefaultContainer,
        AzureBlobUpload.DefaultKeepNights,
        TimeSpan.FromHours(AzureBlobUpload.DefaultTimeoutHours),
        TimeSpan.FromMinutes(AzureBlobUpload.DefaultProgressMinutes));

    /// <summary>
    /// Options from raw setting values, as the Launcher's <c>From(AppSettings)</c> read them: names trimmed, a timeout of 0 or less
    /// meaning no limit, and a progress interval of 0 or less falling back to <see cref="AzureBlobUpload.DefaultProgressMinutes"/>.
    /// </summary>
    public static AzureUploadOptions From(
        bool enabled, string? storageAccount, string? container, int keepNights, double timeoutHours, double progressMinutes) => new(
        enabled,
        (storageAccount ?? "").Trim(),
        (container ?? "").Trim(),
        keepNights,
        timeoutHours > 0 ? TimeSpan.FromHours(timeoutHours) : TimeSpan.Zero,
        TimeSpan.FromMinutes(progressMinutes > 0 ? progressMinutes : AzureBlobUpload.DefaultProgressMinutes));
}

/// <summary>Outcome of the off-machine upload. <see cref="Skipped"/> (upload turned off) counts as OK.</summary>
public sealed record AzureUploadResult(
    bool Ok, bool Skipped, string Message, int Files = 0, long Bytes = 0, int PrefixesPruned = 0, TimeSpan Elapsed = default);

/// <summary>
/// Off-machine copy of a backup's <c>Databases</c> folder, ported from MindAttic.Launcher's AzureBlobBackupService
/// (M.ORCH-T2048, part of M.ORCH-T1983): the backup disk is inside the same PC as the databases, so on its own it survives
/// nothing that takes the PC. Uploads the folder to an Azure blob container under the dated folder's name as the prefix
/// (<c>{yyyy-MM-dd}/{server}/{db}.sql</c> and <c>{yyyy-MM-dd}/SQLite/{project}/{db}.sql</c>) by shelling out to the signed-in
/// Azure CLI (<c>--auth-mode login</c>: no keys, nothing secret in settings), checks what arrived against what was sent, and only
/// then deletes dated prefixes beyond the newest N. A failed, partial, timed-out or interrupted upload deletes nothing.
/// The az runner and resolver are injectable, so tests never need az or a storage account.
/// </summary>
public sealed class AzureBlobUpload
{
    public const string DefaultStorageAccount = "stmindatticdbf65744";
    public const string DefaultContainer = "db-backups";
    public const int DefaultKeepNights = 7;
    public const double DefaultTimeoutHours = 22;
    public const double DefaultProgressMinutes = 5;

    /// <summary>
    /// Runs az with the given arguments. <paramref name="timeout"/> zero means no limit;
    /// <paramref name="onProgress"/> (elapsed, az's latest output line) fires every
    /// <paramref name="progressEvery"/> while it runs.
    /// </summary>
    public delegate (int ExitCode, string Output, bool TimedOut) AzRunner(
        IReadOnlyList<string> args, TimeSpan timeout, TimeSpan progressEvery, Action<TimeSpan, string>? onProgress);

    private readonly AzRunner run;
    private readonly Func<string?> resolveAz;

    public AzureBlobUpload() : this(null, null) { }

    public AzureBlobUpload(AzRunner? runner, Func<string?>? resolveAz)
    {
        this.resolveAz = resolveAz ?? ResolveAz;
        run = runner ?? DefaultRun;
    }

    /// <summary>
    /// Full path of the Azure CLI (az.cmd on Windows) from the process, user and machine PATH with PATHEXT, as cmd.exe would find
    /// it (CreateProcess alone never finds a .cmd shim), or null when it isn't installed.
    /// </summary>
    public static string? ResolveAz()
    {
        var extensions = (Environment.GetEnvironmentVariable("PATHEXT") ?? ".COM;.EXE;.BAT;.CMD")
            .Split(';', StringSplitOptions.RemoveEmptyEntries);
        var dirs = new[]
            {
                Environment.GetEnvironmentVariable("PATH"),
                Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User),
                Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Machine)
            }
            .SelectMany(p => (p ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Select(Environment.ExpandEnvironmentVariables)
            .Distinct(StringComparer.OrdinalIgnoreCase);
        foreach (var dir in dirs)
            foreach (var ext in extensions)
            {
                string candidate;
                try { candidate = Path.Combine(dir, "az" + ext); }
                catch (ArgumentException) { continue; } // a malformed PATH entry mustn't abort the search
                if (File.Exists(candidate)) return candidate;
            }
        return null;
    }

    private static string[] Auth(AzureUploadOptions o) => ["--account-name", o.StorageAccount, "--auth-mode", "login"];

    public static IReadOnlyList<string> BuildUploadArguments(AzureUploadOptions o, string sourceFolder, string prefix) =>
        ["storage", "blob", "upload-batch", .. Auth(o), "-d", o.Container, "--destination-path", prefix,
         "-s", sourceFolder, "--overwrite", "true", "--output", "none"];

    /// <summary>Lists <c>name&lt;TAB&gt;size</c> for every blob, or only those under <paramref name="prefix"/>.</summary>
    public static IReadOnlyList<string> BuildListArguments(AzureUploadOptions o, string? prefix)
    {
        List<string> args = ["storage", "blob", "list", .. Auth(o), "-c", o.Container, "--num-results", "*",
            "--query", "[].[name,properties.contentLength]", "--output", "tsv"];
        if (!string.IsNullOrEmpty(prefix)) args.AddRange(["--prefix", prefix + "/"]);
        return args;
    }

    public static IReadOnlyList<string> BuildDeletePrefixArguments(AzureUploadOptions o, string prefix) =>
        ["storage", "blob", "delete-batch", .. Auth(o), "-s", o.Container, "--pattern", prefix + "/*", "--output", "none"];

    /// <summary>Parses the list output into (blob name, size); lines that aren't <c>name&lt;TAB&gt;number</c> are ignored.</summary>
    public static IReadOnlyList<(string Name, long Size)> ParseBlobList(string output)
    {
        var blobs = new List<(string, long)>();
        foreach (var raw in output.Split('\n'))
        {
            var parts = raw.Trim('\r', ' ').Split('\t');
            if (parts.Length == 2 && parts[0].Length > 0
                && long.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var size))
                blobs.Add((parts[0], size));
        }
        return blobs;
    }

    /// <summary>
    /// The dated top-level prefixes to delete so the newest <paramref name="keep"/> nights remain. Never
    /// includes <paramref name="justUploaded"/>, never anything that isn't a <c>yyyy-MM-dd[_x]</c> prefix,
    /// and nothing at all when <paramref name="keep"/> is below 1 (a setting typo must not empty the container).
    /// </summary>
    public static IReadOnlyList<string> ResolveStalePrefixes(IEnumerable<string> blobNames, int keep, string justUploaded)
    {
        if (keep < 1) return [];
        var prefixes = blobNames
            .Select(n => n.Split('/')[0])
            .Where(p => p.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        return BackupFolders.ResolveStale(prefixes, keep)
            .Where(p => !string.Equals(p, justUploaded, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    /// <summary>
    /// Uploads a finished backup run's <c>Databases</c> folder under the dated folder's name (<c>2026-11-22</c>, or
    /// <c>2026-11-22_a</c> for a second run that day), verifies it, then prunes older nights. Never throws.
    /// </summary>
    public AzureUploadResult UploadAndPrune(string backupFolder, AzureUploadOptions o, Action<string>? log = null) =>
        UploadAndPrune(
            Path.Combine(backupFolder, SqliteBackup.DatabasesSubfolder),
            Path.GetFileName(backupFolder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)),
            o, log);

    /// <summary>
    /// Uploads <paramref name="databasesFolder"/> under <paramref name="prefix"/>, verifies it, then prunes
    /// older nights. <paramref name="log"/> receives progress lines. Never throws.
    /// </summary>
    public AzureUploadResult UploadAndPrune(string databasesFolder, string prefix, AzureUploadOptions o, Action<string>? log = null)
    {
        log ??= _ => { };
        if (!o.Enabled) return new AzureUploadResult(true, true, "Azure upload is turned off.");

        var sw = Stopwatch.StartNew();
        try
        {
            if (o.StorageAccount.Length == 0 || o.Container.Length == 0)
                return Fail("the storage account or container setting is empty");
            if (string.IsNullOrWhiteSpace(prefix))
                return Fail("the blob prefix (the dated folder's name) is empty");

            var local = Directory.Exists(databasesFolder)
                ? new DirectoryInfo(databasesFolder).GetFiles("*", SearchOption.AllDirectories)
                : [];
            if (local.Length == 0)
                return Fail($"there is nothing to upload in {databasesFolder}");
            var localBytes = local.Sum(f => f.Length);

            if (resolveAz() is null)
                return Fail("the Azure CLI (az) was not found on PATH. Install it (winget install Microsoft.AzureCLI) and sign in with 'az login'");

            log($"Azure upload starting: {local.Length} file(s), {FormatBytes(localBytes)} -> {o.StorageAccount}/{o.Container}/{prefix}/");
            var (code, output, timedOut) = run(
                BuildUploadArguments(o, databasesFolder, prefix), o.Timeout, o.ProgressInterval,
                (elapsed, last) => log($"Azure upload still running after {Elapsed(elapsed)}{(last.Length > 0 ? ": " + last : "")}"));
            if (timedOut)
                return Fail($"the upload was stopped after the {Elapsed(o.Timeout)} time limit");
            if (code != 0)
                return Fail($"az storage blob upload-batch exited {code}: {BackupFolders.Tail(output, 1500)}");

            // az can exit 0 having skipped a file; only a matching count and byte total is a complete copy.
            var (listCode, listOutput, _) = run(BuildListArguments(o, prefix), TimeSpan.Zero, o.ProgressInterval, null);
            if (listCode != 0)
                return Fail($"the upload finished but could not be verified (az storage blob list exited {listCode}): {BackupFolders.Tail(listOutput, 1500)}");
            var remote = ParseBlobList(listOutput);
            var remoteBytes = remote.Sum(b => b.Size);
            if (remote.Count != local.Length || remoteBytes != localBytes)
                return Fail($"the upload is incomplete: {remote.Count} blob(s), {remoteBytes} bytes in Azure against {local.Length} file(s), {localBytes} bytes on disk");
            log($"Azure upload verified: {remote.Count} blob(s), {FormatBytes(remoteBytes)} in {Elapsed(sw.Elapsed)}");

            var (pruned, pruneError) = Prune(o, prefix, log);
            sw.Stop();
            return pruneError is null
                ? new AzureUploadResult(true, false, "uploaded and verified", local.Length, localBytes, pruned, sw.Elapsed)
                : new AzureUploadResult(false, false, $"uploaded and verified, but pruning older nights failed: {pruneError}",
                    local.Length, localBytes, pruned, sw.Elapsed);
        }
        catch (Exception ex)
        {
            return Fail(ex.Message);
        }

        AzureUploadResult Fail(string why) =>
            new(false, false, $"Azure upload FAILED: {why}. Nothing was deleted in Azure or on disk.", Elapsed: sw.Elapsed);
    }

    private (int Pruned, string? Error) Prune(AzureUploadOptions o, string justUploaded, Action<string> log)
    {
        if (o.KeepNights < 1) return (0, null);

        var (code, output, _) = run(BuildListArguments(o, null), TimeSpan.Zero, o.ProgressInterval, null);
        if (code != 0) return (0, $"az storage blob list exited {code}: {BackupFolders.Tail(output, 1000)}");

        var pruned = 0;
        string? error = null;
        foreach (var stale in ResolveStalePrefixes(ParseBlobList(output).Select(b => b.Name), o.KeepNights, justUploaded))
        {
            var (delCode, delOutput, _) = run(BuildDeletePrefixArguments(o, stale), TimeSpan.Zero, o.ProgressInterval, null);
            if (delCode == 0)
            {
                pruned++;
                log($"Azure prune: deleted {stale}/ (keeping the newest {o.KeepNights} night(s))");
            }
            else
            {
                error ??= $"could not delete {stale}/ (az exited {delCode}): {BackupFolders.Tail(delOutput, 1000)}";
            }
        }
        return (pruned, error);
    }

    private (int, string, bool) DefaultRun(
        IReadOnlyList<string> args, TimeSpan timeout, TimeSpan progressEvery, Action<TimeSpan, string>? onProgress)
    {
        var az = resolveAz() ?? throw new FileNotFoundException("The Azure CLI (az) was not found on PATH.");
        var psi = new ProcessStartInfo(az)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        foreach (var a in args) psi.ArgumentList.Add(a);

        var gate = new object();
        var lines = new List<string>();
        var last = "";
        void Collect(string? line)
        {
            if (string.IsNullOrWhiteSpace(line)) return;
            lock (gate)
            {
                last = line.Trim();
                lines.Add(line);
            }
        }

        using var p = new Process { StartInfo = psi };
        p.OutputDataReceived += (_, e) => Collect(e.Data);
        p.ErrorDataReceived += (_, e) => Collect(e.Data);
        if (!p.Start()) throw new InvalidOperationException("Failed to start az.");
        p.BeginOutputReadLine();
        p.BeginErrorReadLine();

        var sw = Stopwatch.StartNew();
        var nextProgress = progressEvery;
        var timedOut = false;
        while (!p.WaitForExit(1000))
        {
            if (timeout > TimeSpan.Zero && sw.Elapsed >= timeout)
            {
                timedOut = true;
                // az.cmd is cmd.exe -> python: kill the whole tree or the upload carries on without us.
                try { p.Kill(entireProcessTree: true); } catch { /* already gone */ }
                break;
            }
            if (onProgress is not null && progressEvery > TimeSpan.Zero && sw.Elapsed >= nextProgress)
            {
                nextProgress += progressEvery;
                string snapshot;
                lock (gate) snapshot = last;
                try { onProgress(sw.Elapsed, snapshot); } catch { /* logging must not stop the upload */ }
            }
        }
        try { p.WaitForExit(10000); } catch { /* bounded */ }

        int code;
        try { code = p.ExitCode; }
        catch { code = -1; }
        string output;
        lock (gate) output = string.Join("\n", lines);
        return (code, output, timedOut);
    }

    private static string Elapsed(TimeSpan t) =>
        t.TotalHours >= 1 ? $"{(int)t.TotalHours}h {t.Minutes:00}m" : $"{t.Minutes}m {t.Seconds:00}s";

    /// <summary>Bytes as B, KB, MB or GB with two decimals, invariant culture.</summary>
    public static string FormatBytes(long bytes) =>
        bytes switch
        {
            < 1024L => $"{bytes} B",
            < 1024L * 1024 => string.Create(CultureInfo.InvariantCulture, $"{bytes / 1024.0:F2} KB"),
            < 1024L * 1024 * 1024 => string.Create(CultureInfo.InvariantCulture, $"{bytes / (1024.0 * 1024):F2} MB"),
            _ => string.Create(CultureInfo.InvariantCulture, $"{bytes / (1024.0 * 1024 * 1024):F2} GB")
        };
}
