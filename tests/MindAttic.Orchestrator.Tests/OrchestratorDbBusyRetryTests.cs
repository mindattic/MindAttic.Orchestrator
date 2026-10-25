using MindAttic.Orchestrator.Settings;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>
/// M.ORCH-T811: a write to the orchestrator's SQL Server database retries (rather than failing the command outright) when
/// <see cref="OrchestratorServer.Lock"/> finds its write lock busy past its timeout, as it does under heavy concurrent load
/// (many CLIs and subagents writing at once, orchestrator next --claim among them). These test <see cref="OrchestratorDb.WithBusyRetry{T}"/>
/// directly, against a fake attempt, so they run without a real SQL Server; <see cref="OrchestratorServerTests"/> covers the
/// same behavior end to end against one.
/// </summary>
public sealed class OrchestratorDbBusyRetryTests
{
    private int retriesBefore;
    private TimeSpan delayBefore;

    [SetUp]
    public void SetUp()
    {
        retriesBefore = OrchestratorDb.BusyRetries;
        delayBefore = OrchestratorDb.BusyRetryDelay;
        OrchestratorDb.BusyRetryDelay = TimeSpan.FromMilliseconds(1); // these tests only care about the retry count, not the wait
    }

    [TearDown]
    public void TearDown()
    {
        OrchestratorDb.BusyRetries = retriesBefore;
        OrchestratorDb.BusyRetryDelay = delayBefore;
    }

    private static IOException Busy() => new("The orchestrator's SQL Server database stayed busy past its timeout.");

    [Test]
    public void A_try_that_finds_the_lock_busy_once_retries_and_succeeds()
    {
        OrchestratorDb.BusyRetries = 3;
        var tries = 0;
        var result = OrchestratorDb.WithBusyRetry(() =>
        {
            tries++;
            if (tries < 3) throw Busy();
            return "ok";
        });
        Assert.That(result, Is.EqualTo("ok"));
        Assert.That(tries, Is.EqualTo(3), "two failed tries, then the one that went through");
    }

    [Test]
    public void A_lock_that_stays_busy_past_every_retry_throws_the_last_tries_exception()
    {
        OrchestratorDb.BusyRetries = 2;
        var tries = 0;
        var ex = Assert.Throws<IOException>(() => OrchestratorDb.WithBusyRetry<string>(() => { tries++; throw Busy(); }));
        Assert.That(tries, Is.EqualTo(3), "the first try and 2 retries, no more");
        Assert.That(ex!.Message, Does.Contain("stayed busy past its timeout"));
    }

    [Test]
    public void No_retries_means_the_first_busy_try_fails_the_write()
    {
        OrchestratorDb.BusyRetries = 0;
        var tries = 0;
        Assert.Throws<IOException>(() => OrchestratorDb.WithBusyRetry<string>(() => { tries++; throw Busy(); }));
        Assert.That(tries, Is.EqualTo(1));
    }

    [Test]
    public void A_failure_thats_not_the_lock_staying_busy_isnt_retried()
    {
        OrchestratorDb.BusyRetries = 5;
        var tries = 0;
        Assert.Throws<InvalidOperationException>(() => OrchestratorDb.WithBusyRetry<string>(() =>
        {
            tries++;
            throw new InvalidOperationException("some other problem");
        }));
        Assert.That(tries, Is.EqualTo(1), "only the lock staying busy is worth retrying");
    }
}
