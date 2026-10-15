using System.Net;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

public sealed class NotifierTests
{
    [Test]
    public async Task An_alert_is_a_plain_text_post_with_a_title_and_a_dead_endpoint_is_not_a_crash()
    {
        var port = Random.Shared.Next(20000, 60000);
        var url = $"http://localhost:{port}/orch-test/";
        using var listener = new HttpListener();
        listener.Prefixes.Add(url);
        listener.Start();
        var received = Task.Run(async () =>
        {
            var ctx = await listener.GetContextAsync();
            using var reader = new StreamReader(ctx.Request.InputStream);
            var result = (ctx.Request.HttpMethod, ctx.Request.Headers["Title"], ctx.Request.Headers["Priority"], await reader.ReadToEndAsync());
            ctx.Response.StatusCode = 200;
            ctx.Response.Close();
            return result;
        });

        var ok = await new Notifier().SendAsync(url, "Needs you", "Prose: the agent is waiting.", urgent: true);

        Assert.That(ok, Is.True);
        Assert.That(await received, Is.EqualTo(("POST", "Needs you", "high", "Prose: the agent is waiting.")));
        listener.Stop();
        Assert.That(await new Notifier().SendAsync(url, "x", "y"), Is.False, "nobody listening: reported, not thrown");
        Assert.That(await new Notifier().SendAsync(null, "x", "y"), Is.False);
        Assert.That(await new Notifier().SendAsync("file:///c:/x", "x", "y"), Is.False, "only http(s)");
    }
}
