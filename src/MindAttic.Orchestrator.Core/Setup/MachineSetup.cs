using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using MindAttic.Orchestrator.Settings;
using MindAttic.Orchestrator.Term;

namespace MindAttic.Orchestrator.Setup;

/// <summary>
/// Moving the Orchestrator to another machine (M.ORCH-T1473): the steps behind <c>orchestrator machine handoff | connect | host |
/// standalone | pull | kit</c>. The work machine (a VM, an HVD) is the <see cref="MachineRoleKind.Host"/>: it runs the daemon,
/// every CLI and the gateway, and is the one writer of the committed databases. The owner's PC becomes a
/// <see cref="MachineRoleKind.Client"/>: it runs none of it, keeps its databases off the git checkout so every pull is clean,
/// and its dashboard is the host's Team Wall, reached through the host's gateway with the host's owner key (tokens are minted
/// as needed, so the link never lapses). See docs/REMOTE_HOST.md.
/// </summary>
public sealed class MachineSetup(Action<string> say, string? workspaceRoot = null)
{
    /// <summary>The name of the inbound firewall rule the host opens for its gateway.</summary>
    public const string FirewallRule = "MindAttic Orchestrator Gateway";

    /// <summary>The kit's manifest file.</summary>
    public const string KitManifest = "kit.json";

    /// <summary>The commit subject the daemon's database sync uses, so a handoff's commit passes the commit-msg hook.</summary>
    public const string OrchestratorDbSyncSubject = "Sync data/orchestrator.db: the orchestrator's settings, state and history (M.ORCH-T524)";

    private static readonly TimeSpan GitTimeout = TimeSpan.FromMinutes(2);

    public string WorkspaceRoot { get; } = workspaceRoot ?? OrchestratorOptions.Load().WorkspaceRoot;

    private string OrchestratorRepo => Path.Combine(WorkspaceRoot, "MindAttic.Orchestrator");
    private string TicketsRepo => Path.Combine(WorkspaceRoot, "MindAttic.Tickets");

    // ---- the PC: hand off to the host, then watch it ----

    /// <summary>
    /// Makes this machine a client: copies the orchestrator's and the ticket database's data out of SQL Server into the committed
    /// SQLite files and pushes them (unless <paramref name="pushData"/> is false), so the host starts from everything this
    /// machine knew; writes the kit when <paramref name="kitFolder"/> is given; stops the daemon and the gateway; takes the pace
    /// hooks out; points the ticket tools at a machine-local mirror. Returns false when the data couldn't be pushed (nothing
    /// else changed then, so it can simply be run again).
    /// </summary>
    public bool Handoff(string? kitFolder, bool pushData, string orchestratorExe)
    {
        if (pushData && !PushData(orchestratorExe)) return false;
        if (kitFolder is not null) WriteKit(kitFolder);
        var before = MachineRole.Current;
        new MachineRole { Role = MachineRoleKind.Client, Host = before.IsClient ? before.Host : null, GatewayUrl = before.IsClient ? before.GatewayUrl : null }.Save();
        say("This machine is now a client: it runs no daemon, no pace hooks and no gateway.");
        StopLocalOrchestrator();
        if (AgentGateRemoval.Remove() is { } hooks) say("Pace hooks taken out: " + hooks);
        UseTicketsMirror();
        return true;
    }

    /// <summary>
    /// Points this client's dashboard at its host: the host's gateway (<paramref name="host"/>[:port]) pinned to
    /// <paramref name="pin"/>, with its <paramref name="ownerKey"/> sealed for this Windows user, as the Team Wall remote
    /// <see cref="MachineRole.HostRemoteName"/>. Makes this machine a client first if it isn't one. Returns whether the host
    /// answered.
    /// </summary>
    public async Task<bool> ConnectAsync(string host, string pin, string ownerKey, string orchestratorExe)
    {
        var url = GatewayRemotes.NormalizeUrl(host) ?? throw new ArgumentException($"\"{host}\" isn't a host name, address or https URL.");
        var normalizedPin = GatewayRemotes.NormalizePin(pin) ?? throw new ArgumentException("The pin is the gateway certificate's SHA-256: 64 hex characters.");
        if (!MachineRole.Current.IsClient)
        {
            say("Not a client yet: handing off first (data pushed, daemon stopped, hooks out).");
            if (!Handoff(null, pushData: true, orchestratorExe)) return false;
        }
        var hostName = new Uri(url).Host;
        new MachineRole { Role = MachineRoleKind.Client, Host = hostName, GatewayUrl = url }.Save();

        // The client's own store now: never the checkout's copy (OrchestratorDb.StatePath).
        var store = new JsonStore();
        var s = store.LoadSettingsToChange();
        var remote = new GatewayRemote
        {
            Name = MachineRole.HostRemoteName,
            Person = $"Host ({hostName})",
            Url = url,
            CertSha256 = normalizedPin,
            SealedOwnerKey = GatewayRemotes.Seal(ownerKey),
        };
        var remotes = s.Gateway.Remotes.Where(r => !string.Equals(r.Name, remote.Name, StringComparison.OrdinalIgnoreCase)).Append(remote).ToList();
        store.SaveSettings(s with { Gateway = s.Gateway with { Remotes = remotes } });
        say($"The dashboard now opens {hostName}'s Team Wall ({url}).");
        InstallTicketsShim(orchestratorExe);

        using var client = new Sessions.GatewayClient(remote);
        var listing = await client.ListAsync(CancellationToken.None).ConfigureAwait(false);
        if (listing.Online) say($"Connected: {hostName} has {listing.Sessions.Count} CLI session(s) running.");
        else say($"Saved, but {hostName} didn't answer: {listing.Problem}. Check the VM is on, that its daemon runs (orchestrator machine there) and that port {new Uri(url).Port} is open.");
        return listing.Online;
    }

    /// <summary>Back to everything on this machine: role standalone, the ticket tools on the checkout again, the daemon started
    /// (which puts the hooks back).</summary>
    public void Standalone(Installer installer)
    {
        new MachineRole { Role = MachineRoleKind.Standalone }.Save();
        if (Environment.GetEnvironmentVariable(TicketsDbVar, EnvironmentVariableTarget.User) is { Length: > 0 })
        {
            Environment.SetEnvironmentVariable(TicketsDbVar, null, EnvironmentVariableTarget.User);
            say($"{TicketsDbVar} cleared: the ticket tools use the checkout's database again (new terminals).");
        }
        RemoveTicketsShim();
        StartDaemon(installer);
        say("This machine is standalone again. Run orchestrator db migrate to go back to SQL Server, if it used one.");
    }

    // ---- the VM: become the host ----

    /// <summary>
    /// Makes this machine the host: role host, the gateway on and listening on <paramref name="bind"/> with a certificate naming
    /// this machine, the firewall opened for it (when elevated), the daemon started, and the line to run on the PC returned.
    /// </summary>
    public async Task<string?> HostAsync(Installer installer, string bind, int? port)
    {
        new MachineRole { Role = MachineRoleKind.Host }.Save();
        var store = new JsonStore();
        var s = store.LoadSettingsToChange();
        var names = s.Gateway.DevCertNames.Concat(MachineNames()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var gateway = s.Gateway with { Enabled = true, Bind = bind, Port = port ?? s.Gateway.Port, DevCertNames = names };
        store.SaveSettings(s with { Gateway = gateway });
        say($"Gateway on: https://{bind}:{gateway.Port}, certificate for {string.Join(", ", names)}.");
        OpenFirewall(gateway.Port);
        // The gateway lists only shared sessions; on the owner's own host every CLI is the owner's to watch from the PC.
        if (TermBroadcast.Update(b => b with { ShareAll = true }) is null) say("Couldn't turn on sharing every session (orchestrator broadcast share all).");
        else say("Every CLI on this host is shared with your PC's Team Wall.");
        StartDaemon(installer);

        say("Waiting for the daemon to start the gateway…");
        TermGatewayStatus? running = null;
        for (var i = 0; i < 90 && (running = TermGateway.Running()) is null; i++) await Task.Delay(1000).ConfigureAwait(false);
        if (running is null)
        {
            say("The gateway isn't up yet. Once it is, orchestrator machine prints the line for your PC.");
            return null;
        }
        return ConnectLine(running);
    }

    /// <summary>The command the PC runs to watch this host, or null when this machine's gateway isn't running.</summary>
    public static string? ConnectLine(TermGatewayStatus? running = null)
    {
        running ??= TermGateway.Running();
        if (running is null) return null;
        var key = TermGatewayTokens.Base64Url(TermGatewayTokens.LoadOrCreateKey(TermGateway.DefaultDirectory, TermGatewayTokens.OwnerKeyFile));
        var host = Environment.MachineName.ToLowerInvariant();
        var hostPort = running.Port == TermGateway.DefaultPort ? host : $"{host}:{running.Port}";
        return $"orchestrator machine connect {hostPort} --pin {running.CertSha256} --owner-key {key}";
    }

    /// <summary>This machine's names and addresses for the gateway's certificate: its name, its DNS name, and every IPv4 address
    /// of an interface that's up.</summary>
    public static IEnumerable<string> MachineNames()
    {
        yield return Environment.MachineName.ToLowerInvariant();
        string? fqdn = null;
        try { fqdn = Dns.GetHostEntry(Environment.MachineName).HostName; }
        catch (SocketException) { }
        if (fqdn is { Length: > 0 } && !fqdn.Equals(Environment.MachineName, StringComparison.OrdinalIgnoreCase)) yield return fqdn.ToLowerInvariant();
        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus != OperationalStatus.Up || nic.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
            foreach (var a in nic.GetIPProperties().UnicastAddresses)
                if (a.Address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(a.Address)) yield return a.Address.ToString();
        }
    }

    // ---- either: pull, kit ----

    /// <summary>
    /// <c>git pull --ff-only</c> in the workspace root and every repo under it, a few at once; a repo with local changes in the way
    /// or off <c>main</c> is skipped and named. On a client the ticket mirror is then refreshed from the pulled database. Returns
    /// the number of repos that failed or were skipped.
    /// </summary>
    public async Task<int> PullAsync(int parallel = 6)
    {
        var repos = new[] { WorkspaceRoot }
            .Concat(Directory.Exists(WorkspaceRoot) ? Directory.EnumerateDirectories(WorkspaceRoot) : [])
            .Where(d => Directory.Exists(Path.Combine(d, ".git")) || File.Exists(Path.Combine(d, ".git")))
            .ToList();
        var problems = 0;
        var gate = new SemaphoreSlim(parallel);
        var tasks = repos.Select(async repo =>
        {
            await gate.WaitAsync().ConfigureAwait(false);
            try
            {
                var r = await Task.Run(() => Run("git", ["-C", repo, "pull", "--ff-only", "-q"], GitTimeout)).ConfigureAwait(false);
                var name = repo == WorkspaceRoot ? "(workspace root)" : Path.GetFileName(repo);
                if (r.Code != 0)
                {
                    Interlocked.Increment(ref problems);
                    say($"  {name}: not pulled: {FirstLine(r.Output)}");
                }
                else if (!string.IsNullOrWhiteSpace(r.Output)) say($"  {name}: {FirstLine(r.Output)}");
            }
            finally { gate.Release(); }
        });
        await Task.WhenAll(tasks).ConfigureAwait(false);
        say($"Pulled {repos.Count - problems} of {repos.Count} repos.");
        if (MachineRole.Current.IsClient) RefreshTicketsMirror();
        return problems;
    }

    /// <summary>
    /// Writes the host kit to <paramref name="folder"/>: <c>C:\LocalNuGet</c> (the packages only this machine has, which every
    /// MindAttic app restores from), the app-level settings with no secrets (Vault Global and the Orchestrator's Vault tab),
    /// <c>setup-host.ps1</c>, and <see cref="KitManifest"/> (where it came from, the workspace, the git identity).
    /// </summary>
    public void WriteKit(string folder)
    {
        Directory.CreateDirectory(folder);
        var feed = @"C:\LocalNuGet";
        if (Directory.Exists(feed))
        {
            var copied = CopyNewer(feed, Path.Combine(folder, "LocalNuGet"), "*.nupkg");
            say($"Kit: {copied} package(s) copied from {feed}.");
        }
        else say($"Kit: no {feed} here; the host will have only what nuget.org has.");

        var settings = Path.Combine(folder, "settings");
        Directory.CreateDirectory(settings);
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        CopyIfExists(Path.Combine(roaming, "MindAttic", "Global", "settings.json"), Path.Combine(settings, "Global.settings.json"));
        CopyIfExists(Path.Combine(local, "MindAttic", "Orchestrator", "settings.json"), Path.Combine(settings, "Orchestrator.settings.json"));

        var script = Path.Combine(OrchestratorRepo, "setup-host.ps1");
        CopyIfExists(script, Path.Combine(folder, "setup-host.ps1"));

        var manifest = new
        {
            Kind = "MindAttic.Orchestrator host kit",
            SourceMachine = Environment.MachineName,
            CreatedUtc = DateTime.UtcNow,
            WorkspaceRoot,
            RootRemote = WorkspaceBootstrap.Load(WorkspaceRoot)?.Root,
            GitUserName = GitConfig("user.name"),
            GitUserEmail = GitConfig("user.email"),
        };
        File.WriteAllText(Path.Combine(folder, KitManifest), JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));
        say($"Kit written to {folder}. Copy the folder to the VM and run there, from a normal (not administrator) PowerShell: powershell -ExecutionPolicy Bypass -File \"<kit>\\setup-host.ps1\" -Kit \"<kit>\"");
    }

    // ---- the pieces ----

    /// <summary>The folder of the client's <c>tickets</c> shim, first on the user PATH.</summary>
    public static string ShimDirectory => Path.Combine(MachineRole.Directory, "client", "bin");

    /// <summary>
    /// Puts a <c>tickets</c> shim first on the user PATH (M.ORCH-T1479): <c>tickets.cmd</c> for cmd and PowerShell, and an
    /// extensionless script for Git Bash (which Claude Code's Bash tool runs), both handing the arguments to
    /// <c>orchestrator machine tickets --</c>, which runs them on the host. So every tickets command on this PC, an agent's
    /// included, reads and writes the host's tickets.
    /// </summary>
    private void InstallTicketsShim(string orchestratorExe)
    {
        var installed = Path.Combine(@"C:\Apps\MindAttic.Orchestrator\Cli", "orchestrator.exe");
        var exe = File.Exists(installed) ? installed : orchestratorExe;
        Directory.CreateDirectory(ShimDirectory);
        File.WriteAllText(Path.Combine(ShimDirectory, "tickets.cmd"), $"@\"{exe}\" machine tickets -- %*\r\n@exit /b %ERRORLEVEL%\r\n", new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(ShimDirectory, "tickets"), $"#!/bin/sh\nexec \"{exe.Replace('\\', '/')}\" machine tickets -- \"$@\"\n", new UTF8Encoding(false));
        var path = Environment.GetEnvironmentVariable("Path", EnvironmentVariableTarget.User) ?? "";
        var parts = path.Split(';', StringSplitOptions.RemoveEmptyEntries).Where(p => !p.TrimEnd('\\').Equals(ShimDirectory, StringComparison.OrdinalIgnoreCase));
        Environment.SetEnvironmentVariable("Path", string.Join(';', new[] { ShimDirectory }.Concat(parts)), EnvironmentVariableTarget.User);
        say($"tickets on this PC now runs on the host (shim in {ShimDirectory}, first on your PATH; new terminals pick it up).");
    }

    private void RemoveTicketsShim()
    {
        var path = Environment.GetEnvironmentVariable("Path", EnvironmentVariableTarget.User) ?? "";
        var parts = path.Split(';', StringSplitOptions.RemoveEmptyEntries).ToList();
        if (parts.RemoveAll(p => p.TrimEnd('\\').Equals(ShimDirectory, StringComparison.OrdinalIgnoreCase)) > 0)
            Environment.SetEnvironmentVariable("Path", string.Join(';', parts), EnvironmentVariableTarget.User);
        try { if (Directory.Exists(ShimDirectory)) Directory.Delete(ShimDirectory, recursive: true); }
        catch (IOException) { }
        say("tickets runs on this machine again.");
    }

    /// <summary>
    /// Runs a tickets command on the host (M.ORCH-T1479), in the workspace folder <paramref name="currentDirectory"/> is in.
    /// A <c>--description-file</c> is read here and sent as <c>--description</c>, since the host can't see this machine's files.
    /// </summary>
    public async Task<(int ExitCode, string Output)> RunTicketsOnHostAsync(IReadOnlyList<string> args, string currentDirectory, CancellationToken ct)
    {
        var role = MachineRole.Current;
        var store = new JsonStore();
        if (!role.IsClient || GatewayRemotes.Find(store.LoadSettings().Gateway, MachineRole.HostRemoteName) is not { } remote)
            return (1, "This machine isn't connected to a host (orchestrator machine connect).");
        var sent = new List<string>(args.Count);
        for (var i = 0; i < args.Count; i++)
        {
            if (args[i] == "--description-file" && i + 1 < args.Count)
            {
                var file = Path.GetFullPath(args[++i], currentDirectory);
                if (!File.Exists(file)) return (1, $"No such file: {file}");
                sent.Add("--description");
                sent.Add(File.ReadAllText(file));
                continue;
            }
            sent.Add(args[i]);
        }
        string? relative = null;
        var root = Path.GetFullPath(WorkspaceRoot);
        var here = Path.GetFullPath(currentDirectory);
        // Inside the workspace only: a sibling sharing its name's start (D:\Projects\MindAttic.worktrees) isn't in it.
        var trimmed = root.TrimEnd(Path.DirectorySeparatorChar);
        if (here.Equals(trimmed, StringComparison.OrdinalIgnoreCase) || here.StartsWith(trimmed + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            relative = Path.GetRelativePath(root, here);
        using var client = new Sessions.GatewayClient(remote);
        return await client.RunTicketsAsync(sent, relative == "." ? null : relative, ct).ConfigureAwait(false);
    }


    private const string TicketsDbVar = "MINDATTIC_TICKETS_DB";

    /// <summary>Copies SQL Server's data back into the committed SQLite files and pushes them, for both databases.</summary>
    private bool PushData(string orchestratorExe)
    {
        say("Copying the orchestrator's data into data/orchestrator.db (orchestrator db back)…");
        var back = Run(orchestratorExe, ["db", "back"], TimeSpan.FromMinutes(10));
        say("  " + FirstLine(back.Output));
        if (back.Code != 0) { say("orchestrator db back failed; nothing was changed. Fix it and run this again."); return false; }
        if (!CommitAndPush(OrchestratorRepo, [OrchestratorDb.RelativePath, OrchestratorDb.DumpRelativePath], OrchestratorDbSyncSubject)) return false;

        say("Copying the ticket data into data/Tickets.db (tickets db server back), then tickets db sync…");
        var tickets = Run("tickets", ["db", "server", "back"], TimeSpan.FromMinutes(10));
        say("  " + FirstLine(tickets.Output));
        var sync = Run("tickets", ["db", "sync"], TimeSpan.FromMinutes(5));
        say("  " + FirstLine(sync.Output));
        if (sync.Code != 0) { say("tickets db sync didn't push; run it again (tickets db sync) before setting up the host."); return false; }
        return true;
    }

    private bool CommitAndPush(string repo, string[] files, string subject)
    {
        if (!Directory.Exists(repo)) { say($"No {repo}; nothing to push."); return true; }
        if (Run("git", ["-C", repo, "diff", "--quiet", "HEAD", "--", .. files], GitTimeout).Code != 0)
        {
            var add = Run("git", ["-C", repo, "add", "--", .. files], GitTimeout);
            var commit = add.Code == 0 ? Run("git", ["-C", repo, "commit", "-q", "-m", subject, "--", .. files], GitTimeout) : add;
            if (commit.Code != 0)
            {
                Run("git", ["-C", repo, "reset", "-q", "--", .. files], GitTimeout);
                say($"Couldn't commit {string.Join(", ", files)} in {Path.GetFileName(repo)}: {FirstLine(commit.Output)}");
                return false;
            }
        }
        // Agents push to main from this checkout too: take theirs in first (a merge, as the daemon's sync does).
        Run("git", ["-C", repo, "fetch", "-q", "origin"], GitTimeout);
        var merge = Run("git", ["-C", repo, "merge", "-q", "--no-edit", "origin/main"], GitTimeout);
        if (merge.Code != 0)
        {
            Run("git", ["-C", repo, "merge", "--abort"], GitTimeout);
            say($"Couldn't merge origin/main in {Path.GetFileName(repo)}: {FirstLine(merge.Output)}");
            return false;
        }
        var push = Run("git", ["-C", repo, "push", "-q", "origin", "HEAD:main"], GitTimeout);
        if (push.Code != 0) { say($"Couldn't push {Path.GetFileName(repo)}: {FirstLine(push.Output)}"); return false; }
        say($"Pushed {string.Join(" and ", files)} from {Path.GetFileName(repo)}.");
        return true;
    }

    /// <summary>Stops this machine's daemon, dashboard and gateway. Its hosted CLIs (TermHost) keep running.</summary>
    private void StopLocalOrchestrator()
    {
        new Installer().StopInstalledCopies();
        foreach (var p in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(TermGateway.ExeName)))
        {
            try { p.Kill(); p.WaitForExit(3000); }
            catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception) { }
            finally { p.Dispose(); }
        }
        TermGateway.DeleteStatus();
        say("Stopped this machine's daemon, dashboard and gateway (CLIs already running keep going).");
    }

    private void StartDaemon(Installer installer)
    {
        if (!File.Exists(installer.InstalledExe)) { say($"No installed {installer.InstalledExe} yet: run.ps1 installs it."); return; }
        try { say(installer.EnsureDaemon(installer.InstalledExe) ?? "The daemon is running."); }
        catch (InvalidOperationException ex) { say(ex.Message); }
    }

    private void OpenFirewall(int port)
    {
        var rule = Run("netsh", ["advfirewall", "firewall", "show", "rule", $"name={FirewallRule}"], TimeSpan.FromSeconds(20));
        if (rule.Code == 0 && rule.Output.Contains(FirewallRule, StringComparison.OrdinalIgnoreCase))
            Run("netsh", ["advfirewall", "firewall", "delete", "rule", $"name={FirewallRule}"], TimeSpan.FromSeconds(20));
        var add = Run("netsh", ["advfirewall", "firewall", "add", "rule", $"name={FirewallRule}", "dir=in", "action=allow", "protocol=TCP", $"localport={port}"], TimeSpan.FromSeconds(20));
        say(add.Code == 0 ? $"Firewall: inbound TCP {port} allowed." : $"Firewall: couldn't open TCP {port} (run as administrator, or setup-host.ps1 does it): {FirstLine(add.Output)}");
    }

    /// <summary>Points the ticket tools (MindAttic.Tickets reads <c>MINDATTIC_TICKETS_DB</c>) at a machine-local mirror of the
    /// host's ticket database, so they never write the checkout's copy, which the host commits.</summary>
    private void UseTicketsMirror()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(MachineRole.ClientTicketsDb)!);
        RefreshTicketsMirror();
        Environment.SetEnvironmentVariable(TicketsDbVar, MachineRole.ClientTicketsDb, EnvironmentVariableTarget.User);
        say($"Ticket tools read {MachineRole.ClientTicketsDb}, refreshed from the host's commits on every orchestrator machine pull (new terminals pick it up).");
    }

    /// <summary>Copies the pulled <c>data/Tickets.db</c> over the mirror. A mirror changed here since the last copy (a ticket
    /// written on this machine) is kept beside it first, and said: the host's database is the one that counts.</summary>
    internal void RefreshTicketsMirror()
    {
        var source = Path.Combine(TicketsRepo, "data", "Tickets.db");
        var mirror = MachineRole.ClientTicketsDb;
        var stamp = mirror + ".copied";
        if (!File.Exists(source)) return;
        try
        {
            if (File.Exists(mirror) && File.Exists(stamp) && File.GetLastWriteTimeUtc(mirror).Ticks.ToString(System.Globalization.CultureInfo.InvariantCulture) != File.ReadAllText(stamp).Trim())
            {
                var kept = Path.ChangeExtension(mirror, $".local-{DateTime.Now:yyyyMMdd-HHmmss}.db");
                File.Copy(mirror, kept, overwrite: true);
                say($"Tickets written on this machine since the last pull were kept in {kept}; the host's tickets replace them. File them on the host.");
            }
            File.Copy(source, mirror, overwrite: true);
            File.WriteAllText(stamp, File.GetLastWriteTimeUtc(mirror).Ticks.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
        catch (IOException ex) { say($"The ticket mirror couldn't be refreshed (in use?): {ex.Message}"); }
    }

    private static int CopyNewer(string from, string to, string pattern)
    {
        Directory.CreateDirectory(to);
        var copied = 0;
        foreach (var file in Directory.EnumerateFiles(from, pattern, SearchOption.TopDirectoryOnly))
        {
            var target = Path.Combine(to, Path.GetFileName(file));
            if (File.Exists(target) && new FileInfo(target).Length == new FileInfo(file).Length) continue;
            File.Copy(file, target, overwrite: true);
            copied++;
        }
        return copied;
    }

    private static void CopyIfExists(string from, string to)
    {
        if (File.Exists(from)) File.Copy(from, to, overwrite: true);
    }

    private static string? GitConfig(string key) => Run("git", ["config", "--global", key], TimeSpan.FromSeconds(10)) is { Code: 0 } r ? r.Output.Trim() : null;

    private static string FirstLine(string text) =>
        text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault() ?? "";

    private static (int Code, string Output) Run(string file, string[] args, TimeSpan timeout)
    {
        var psi = new ProcessStartInfo(file)
        {
            RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (var a in args) psi.ArgumentList.Add(a);
        psi.Environment["GIT_TERMINAL_PROMPT"] = "0";
        try
        {
            using var p = Process.Start(psi)!;
            var stdout = p.StandardOutput.ReadToEndAsync();
            var stderr = p.StandardError.ReadToEndAsync();
            if (!p.WaitForExit(timeout))
            {
                try { p.Kill(entireProcessTree: true); } catch (Exception ex) when (ex is InvalidOperationException or AggregateException or System.ComponentModel.Win32Exception) { }
                return (-1, $"{file} timed out after {timeout.TotalMinutes:0} min");
            }
            // It exited, but a child that inherited its pipes (a git credential helper) can hold them open: the output is waited
            // for briefly, as ProcessRunner does, never without a limit (machine handoff/connect/pull hung on it).
            if (!Task.WaitAll([stdout, stderr], TimeSpan.FromSeconds(10)))
                return (p.ExitCode, ((stdout.IsCompletedSuccessfully ? stdout.Result : "") + (stderr.IsCompletedSuccessfully ? stderr.Result : "")).Trim());
            return (p.ExitCode, (stdout.Result + stderr.Result).Trim());
        }
        catch (System.ComponentModel.Win32Exception ex) { return (-1, $"{file}: {ex.Message}"); }
    }
}
