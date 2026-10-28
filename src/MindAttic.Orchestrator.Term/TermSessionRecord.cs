using System.Text.Json.Serialization;

namespace MindAttic.Orchestrator.Term;

/// <summary>A hosted CLI's <see cref="TermSessionRecord.State"/>.</summary>
public static class TermSessionStates
{
    /// <summary>The CLI is running.</summary>
    public const string Running = "running";

    /// <summary>The CLI has exited (or never started): <see cref="TermSessionRecord.ExitCode"/> says how. The host lingers for a
    /// viewer to see it, then ends and its record goes.</summary>
    public const string Exited = "exited";
}

/// <summary>
/// One live TermHost session (M.ORCH-T925): what its registry file,
/// <c>%LOCALAPPDATA%\MindAttic\MindAttic.Orchestrator\terms\&lt;id&gt;.json</c>, holds and what a <c>term-session</c> bus message
/// carries (<see cref="TermSessionEvent"/>). The host writes it when it starts, rewrites it when its title, size or state
/// changes, and deletes it when it exits. <see cref="HostPid"/> with <see cref="HostStartUtc"/> names the host process exactly, so
/// a reader can tell a dead host, or a reused pid, from a live one (<see cref="TermSessions.Enumerate()"/>).
/// <para>A field added keeps <see cref="TermBus.Version"/>; property names are read case-insensitively.</para>
/// </summary>
public record TermSessionRecord
{
    /// <summary>The session id, which names its pipe, log and registry file.</summary>
    [JsonPropertyName("id")] public string Id { get; init; } = "";

    /// <summary>The TermHost process's pid.</summary>
    [JsonPropertyName("hostPid")] public int HostPid { get; init; }

    /// <summary>When the TermHost process started (UTC), as Windows reports it: a process with <see cref="HostPid"/> that started
    /// at another time is a reused pid, not this host.</summary>
    [JsonPropertyName("hostStartUtc")] public DateTime HostStartUtc { get; init; }

    /// <summary>The pipe a viewer attaches to (<see cref="TermProtocol.PipeName"/>).</summary>
    [JsonPropertyName("pipe")] public string Pipe { get; init; } = "";

    /// <summary>The window title: the CLI's last OSC 0/2, else the host's <c>--title</c>.</summary>
    [JsonPropertyName("title")] public string? Title { get; init; }

    /// <summary>The login it runs on (an email), when the launcher said (<c>--login</c>); null for the global login.</summary>
    [JsonPropertyName("login")] public string? Login { get; init; }

    /// <summary>The CLI's <c>CLAUDE_CONFIG_DIR</c>, when it has one.</summary>
    [JsonPropertyName("configDir")] public string? ConfigDir { get; init; }

    /// <summary>What it runs for (<c>--repo</c>), the tab's name without badge and provider.</summary>
    [JsonPropertyName("repo")] public string? Repo { get; init; }

    /// <summary>The folder the CLI runs in.</summary>
    [JsonPropertyName("cwd")] public string? Cwd { get; init; }

    /// <summary>The command line the host started the CLI with. Never a secret: variables go through the environment.</summary>
    [JsonPropertyName("commandLine")] public string CommandLine { get; init; } = "";

    [JsonPropertyName("cols")] public int Cols { get; init; }
    [JsonPropertyName("rows")] public int Rows { get; init; }

    /// <summary><see cref="TermSessionStates.Running"/> or <see cref="TermSessionStates.Exited"/>.</summary>
    [JsonPropertyName("state")] public string State { get; init; } = TermSessionStates.Running;

    /// <summary>The CLI's exit code once <see cref="State"/> is <see cref="TermSessionStates.Exited"/>; null while it runs.</summary>
    [JsonPropertyName("exitCode")] public int? ExitCode { get; init; }

    /// <summary>When the host started the session (UTC).</summary>
    [JsonPropertyName("startedUtc")] public DateTime StartedUtc { get; init; }

    /// <summary>The pipe protocol the host speaks (<see cref="TermProtocol.Version"/>): a viewer opens with this in its Hello.</summary>
    [JsonPropertyName("protocol")] public int Protocol { get; init; } = TermProtocol.Version;

    /// <summary>This is the Launcher's own menu, not a CLI it opened (<c>--menu</c>, M.LNCH-T77, M.ORCH-T1256): the Team Wall
    /// includes or excludes it per the ExcludeLauncherFromTeamWall setting. A field added since a reader may not have it yet,
    /// so it's read case-insensitively and defaults false, same as every other optional field here.</summary>
    [JsonPropertyName("isMenu")] public bool IsMenu { get; init; }

    [JsonIgnore] public bool Exited => State == TermSessionStates.Exited;
}

/// <summary>What happened to a session: <see cref="TermSessionEvent.Event"/>'s values.</summary>
public static class TermSessionEvents
{
    /// <summary>A host registered: its CLI is starting.</summary>
    public const string Started = "started";

    /// <summary>Its title, size or state changed (the CLI exiting is a change to <see cref="TermSessionStates.Exited"/>).</summary>
    public const string Changed = "changed";

    /// <summary>The host is ending and its registry file is gone: close any viewer of it.</summary>
    public const string Ended = "ended";
}

/// <summary>The <c>term-session</c> bus payload (<see cref="TermBus.Kind"/>, v1): the session's record as it stands, plus
/// <see cref="Event"/>.</summary>
public sealed record TermSessionEvent : TermSessionRecord
{
    public TermSessionEvent() { }

    public TermSessionEvent(TermSessionRecord session, string sessionEvent) : base(session) => Event = sessionEvent;

    /// <summary><see cref="TermSessionEvents.Started"/>, <see cref="TermSessionEvents.Changed"/> or <see cref="TermSessionEvents.Ended"/>.</summary>
    [JsonPropertyName("event")] public string Event { get; init; } = "";

    /// <summary>The record alone, without the event.</summary>
    public TermSessionRecord ToRecord() => new TermSessionRecord() with
    {
        Id = Id, HostPid = HostPid, HostStartUtc = HostStartUtc, Pipe = Pipe, Title = Title, Login = Login, ConfigDir = ConfigDir,
        Repo = Repo, Cwd = Cwd, CommandLine = CommandLine, Cols = Cols, Rows = Rows, State = State, ExitCode = ExitCode,
        StartedUtc = StartedUtc, Protocol = Protocol, IsMenu = IsMenu,
    };
}
