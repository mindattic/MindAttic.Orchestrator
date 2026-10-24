using MindAttic.Orchestrator.Tickets;
using MindAttic.Tickets.Log;
using NUnit.Framework;
using TicketHygiene = MindAttic.Orchestrator.Tickets.TicketHygiene; // not MindAttic.Tickets 4.0.0's own (M.TIX-T134)

namespace MindAttic.Orchestrator.Tests;

public sealed class TicketHygieneTests
{
    private static Ticket T(int n, string type, string status, string summary, string reference = "SomeTests") =>
        new($"RP-T{n}", "2026-10-01", type, status, summary, reference);

    [Test]
    public void Flags_closes_without_proof_and_fixes_that_came_back()
    {
        var findings = TicketHygiene.Check("Repo", [
            T(1, "fix", "closed", "Crash on save"),
            T(2, "fix", "closed", "Wrong total", "—"),
            T(3, "docs", "closed", "Readme", "—"),
            T(4, "fix", "open", "RP-T1 is back: crash on save still happens on large files"),
            T(5, "feature", "open", "Extend RP-T2 to cover refunds"),
        ]);

        Assert.That(findings.Select(f => (f.TicketId, f.Why[..8])), Is.EquivalentTo(new[] { ("RP-T2", "closed w"), ("RP-T1", "RP-T4 sa") }),
            "docs needn't cite a test; merely mentioning a ticket isn't a relapse");
    }
}
