using MindAttic.Orchestrator.Term;

namespace MindAttic.Orchestrator.Gateway;

/// <summary>
/// Where the gateway's audit records go (M.ORCH-T934): every attach, detach and refusal, and remote input as counts. A record
/// never holds a token, a key, a header or a byte of what was typed or shown. Writing never throws.
/// </summary>
internal interface IGatewayAudit
{
    void Write(TermGatewayAuditRecord record);
}

/// <summary>The spool in the gateway's data folder (<see cref="TermGatewayAudit.Append"/>), which the daemon imports into
/// <c>orchestrator.db</c>'s <c>term_audit</c>. A record that can't be spooled goes to the log line instead, as counts.</summary>
internal sealed class SpoolAudit(string directory, GatewayLog log) : IGatewayAudit
{
    public void Write(TermGatewayAuditRecord record)
    {
        try
        {
            if (!TermGatewayAudit.Append(record, directory))
                log.Write($"audit record not spooled: {record.Event} {record.Session} by {record.Viewer} ({record.Level}), {record.Frames} frames, {record.Bytes} bytes");
        }
        catch (Exception ex) { log.Error(ex, "spooling an audit record failed; contained"); }
    }
}

/// <summary>No audit: the default for a test that doesn't look.</summary>
internal sealed class NoAudit : IGatewayAudit
{
    public static readonly NoAudit Instance = new();
    public void Write(TermGatewayAuditRecord record) { }
}

/// <summary>Counts a viewer's Input frames as they go to the host, and their payload bytes, never their content; the stream's
/// handler takes the counts since it last looked (<see cref="Take"/>) for an input audit record.</summary>
internal sealed class GatewayInputMeter
{
    private long frames, bytes;

    public void Add(int payloadBytes)
    {
        Interlocked.Increment(ref frames);
        Interlocked.Add(ref bytes, payloadBytes);
    }

    public (long Frames, long Bytes) Take() => (Interlocked.Exchange(ref frames, 0), Interlocked.Exchange(ref bytes, 0));
}
