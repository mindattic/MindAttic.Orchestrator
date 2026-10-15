using System.Net;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

public sealed class NotifierTests
{
    [Test]
    public async Task An_alert_is_a_plain_text_post_with_a_title_and_a_dead_endpoint_is_not_a_crash()
    {
        // A random port sometimes collided with one in use when the whole suite ran in parallel (MAO-T94):
        // ask the OS for a free one, and try again if something grabs it before the listener does.
        var (listener, url) = Listen();
        using var _ = listener;
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

    private static (HttpListener Listener, string Url) Listen()
    {
        for (var attempt = 0; ; attempt++)
        {
            var probe = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            var port = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();
            var url = $"http://localhost:{port}/orch-test/";
            var listener = new HttpListener();
            listener.Prefixes.Add(url);
            try
            {
                listener.Start();
                return (listener, url);
            }
            catch (HttpListenerException) when (attempt < 5)
            {
                listener.Close();
            }
        }
    }
}
