using System.IO.Pipes;
using MindAttic.Orchestrator.Term;

namespace MindAttic.Orchestrator.Gateway;

/// <summary>What a remote viewer may do with a session. Ordered: each level includes the ones below it.</summary>
internal enum GatewayAccess
{
    /// <summary>Not listed, not attachable.</summary>
    None = 0,

    /// <summary>Read-only: it sees the stream; its Input and Resize frames are dropped and its Hello never sets the size.</summary>
    Watch = 1,

    /// <summary>Types into the session and owns its size, as a local viewer does.</summary>
    Interact = 2,
}

/// <summary>
/// Who may watch or interact with which session (M.ORCH-T933). This is the seam M.ORCH-T934 fills with real per-member grants
/// (<c>term_grants(owner, viewer, level, expires_utc)</c> from <c>orchestrator.db</c>) and its audit log.
/// </summary>
internal interface IGatewayGrants
{
    /// <summary>The most <paramref name="viewer"/> (a token's subject) may do with <paramref name="session"/>.</summary>
    GatewayAccess AccessTo(string viewer, TermSessionRecord session);
}

/// <summary>For now only the machine owner's own identity gets in, with full access; everyone else gets nothing.</summary>
internal sealed class OwnerOnlyGrants(string owner) : IGatewayGrants
{
    public GatewayAccess AccessTo(string viewer, TermSessionRecord session) =>
        string.Equals(viewer, owner, StringComparison.OrdinalIgnoreCase) ? GatewayAccess.Interact : GatewayAccess.None;

    /// <summary>The access a request gets: the least of the grant, the token's scope and what the request asked for
    /// (<c>?mode=watch</c> asks for read-only).</summary>
    public static GatewayAccess Effective(GatewayAccess granted, string tokenScope, string? requestedMode)
    {
        var access = (GatewayAccess)Math.Min((int)granted, (int)GatewayScopes.ToAccess(tokenScope));
        if (string.Equals(requestedMode, GatewayScopes.Watch, StringComparison.OrdinalIgnoreCase)) access = (GatewayAccess)Math.Min((int)access, (int)GatewayAccess.Watch);
        return access;
    }
}

/// <summary>The hosted sessions the gateway can offer: the registry (<see cref="TermSessions"/>) by default, a test's own otherwise.</summary>
internal interface ISessionSource
{
    IReadOnlyList<TermSessionRecord> Enumerate();
    TermSessionRecord? Get(string id);
}

internal sealed class RegistrySessions : ISessionSource
{
    public IReadOnlyList<TermSessionRecord> Enumerate() => TermSessions.Enumerate();
    public TermSessionRecord? Get(string id) => TermSessions.Get(id);
}

/// <summary>Opens a session's local pipe: <see cref="TermSessionRecord.Pipe"/> on this machine by default, an in-memory stream in tests.</summary>
internal interface IPipeConnector
{
    Task<Stream> ConnectAsync(TermSessionRecord session, CancellationToken ct);
}

internal sealed class NamedPipeConnector : IPipeConnector
{
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    public async Task<Stream> ConnectAsync(TermSessionRecord session, CancellationToken ct)
    {
        var name = string.IsNullOrWhiteSpace(session.Pipe) ? TermProtocol.PipeName(session.Id) : session.Pipe;
        var pipe = new NamedPipeClientStream(".", name, PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(Timeout);
            await pipe.ConnectAsync(timeout.Token).ConfigureAwait(false);
            return pipe;
        }
        catch
        {
            await pipe.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }
}
