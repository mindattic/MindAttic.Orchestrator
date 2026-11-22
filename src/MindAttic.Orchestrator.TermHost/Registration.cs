using System.Diagnostics;
using MindAttic.Orchestrator.Term;

namespace MindAttic.Orchestrator.TermHost;

/// <summary>
/// The host's entry in the session registry and its announcements on the status bus (M.ORCH-T925). <see cref="Start"/> writes the
/// registry file before the CLI starts (so a launcher can wait for it) and announces <c>started</c>; <see cref="Update"/> is cheap
/// and safe under the session's lock, because the file write and the announce of a change happen on a worker thread, coalesced
/// to the latest record; <see cref="End"/> deletes the file and announces <c>ended</c>. Nothing here throws, and the bus never
/// blocks the host: a send waits at most <see cref="TermBus.ConnectTimeoutMs"/>, off the CLI's path.
/// </summary>
internal sealed class Registration
{
    private readonly HostLog log;

    /// <summary>The registry folder (<c>--registry</c>, a member's host), else null for <see cref="TermSessions.DefaultDirectory"/>.</summary>
    private readonly string? directory;

    /// <summary>Whether to announce on the status bus: not for a member's host (hub phase 2b), whose peer's bus pipe is its user's alone.</summary>
    private readonly bool announce;

    private readonly object gate = new();
    private readonly AutoResetEvent wake = new(false);
    private TermSessionRecord current;
    private Thread? worker;
    private bool dirty, ended;

    public Registration(HostOptions options, string commandLine, ushort cols, ushort rows, HostLog log)
    {
        this.log = log;
        directory = options.RegistryDir;
        announce = !options.IsMemberHost;
        DateTime hostStart;
        using (var self = Process.GetCurrentProcess()) hostStart = self.StartTime.ToUniversalTime();
        current = new TermSessionRecord
        {
            Id = options.Id, HostPid = Environment.ProcessId, HostStartUtc = hostStart, Pipe = options.PipeName, Title = options.Title,
            Login = options.Login, Repo = options.Repo, Cwd = options.Cwd ?? Environment.CurrentDirectory,
            ConfigDir = Environment.GetEnvironmentVariable("CLAUDE_CONFIG_DIR") is { Length: > 0 } dir ? dir : null,
            CommandLine = commandLine, Cols = cols, Rows = rows, State = TermSessionStates.Running, StartedUtc = DateTime.UtcNow,
            Protocol = TermProtocol.Version, IsMenu = options.IsMenu, Account = AccountName(),
            // The agent it runs as (M.ORCH-T1743), handed down by the launcher in the environment, as CLAUDE_CONFIG_DIR is.
            AgentId = AgentIds.Current(),
        };
    }

    public TermSessionRecord Current { get { lock (gate) return current; } }

    /// <summary>Registers the session and starts the worker that follows its changes.</summary>
    public void Start()
    {
        TermSessionRecord record;
        lock (gate) record = current;
        if (!TermSessions.Write(record, directory)) log.Write($"couldn't write the registry file {TermSessions.PathOf(record.Id, directory)}");
        Announce(record, TermSessionEvents.Started);
        worker = new Thread(Follow) { IsBackground = true, Name = "registry" };
        worker.Start();
    }

    /// <summary>Applies <paramref name="change"/> to the record; written and announced shortly, off the caller's thread.</summary>
    public void Update(Func<TermSessionRecord, TermSessionRecord> change)
    {
        lock (gate)
        {
            if (ended) return;
            var next = change(current);
            if (next == current) return;
            current = next;
            dirty = true;
        }
        wake.Set();
    }

    /// <summary>Unregisters the session as the host exits: its file goes, and <c>ended</c> is announced.</summary>
    public void End()
    {
        TermSessionRecord record;
        lock (gate)
        {
            if (ended) return;
            ended = true;
            record = current;
        }
        wake.Set();
        // A write in flight finishes first, so it can't put the file back after the delete. If it doesn't finish in time, that
        // write can still land after we delete and recreate the file, so the delete is skipped rather than raced (bughunt 2026-10-28).
        var joined = true;
        try { joined = worker is null || worker.Join(TimeSpan.FromSeconds(2)); } catch (ThreadStateException) { }
        if (!joined) log.Write("the registry worker didn't finish within 2s; leaving the registry file in place rather than risk it being recreated after a delete");
        else if (!TermSessions.Delete(record.Id, directory)) log.Write("couldn't delete the registry file");
        Announce(record, TermSessionEvents.Ended);
    }

    private void Follow()
    {
        while (true)
        {
            wake.WaitOne();
            TermSessionRecord record;
            lock (gate)
            {
                if (ended) return;
                if (!dirty) continue;
                dirty = false;
                record = current;
            }
            try
            {
                if (!TermSessions.Write(record, directory)) log.Write("couldn't rewrite the registry file");
                Announce(record, TermSessionEvents.Changed);
            }
            catch (Exception ex) { log.Error(ex, "updating the registry failed; contained"); }
        }
    }

    /// <summary>The Windows account this host runs under (<c>DOMAIN\user</c>), reported in its record (hub phase 2b); null when it can't
    /// be told.</summary>
    private static string? AccountName()
    {
        try
        {
            using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
            return identity.Name is { Length: > 0 } name ? name : null;
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException) { return null; }
    }

    private void Announce(TermSessionRecord record, string sessionEvent)
    {
        if (!announce) return;
        try { TermBus.TrySend(new TermSessionEvent(record, sessionEvent)); }
        catch (Exception ex) { log.Error(ex, "announcing on the status bus failed; contained"); }
    }
}
