using System.ComponentModel;
using MindAttic.Orchestrator.Settings;
using MindAttic.Orchestrator.Setup;
using MindAttic.Orchestrator.Term;
using Spectre.Console;
using Spectre.Console.Cli;

/// <summary>Shared bits of <c>orchestrator machine ...</c> (M.ORCH-T1473).</summary>
internal static class Machine
{
    public static MachineSetup Setup() => new(line => AnsiConsole.MarkupLine(Markup.Escape(line)));

    /// <summary>This CLI's own exe, so a step can run another of its commands (<c>db back</c>).</summary>
    public static string Self => Environment.ProcessPath ?? "orchestrator";
}

/// <summary><c>orchestrator machine</c>: this machine's role, and what runs here because of it.</summary>
internal sealed class MachineShowCommand : Command
{
    public override int Execute(CommandContext context)
    {
        var role = MachineRole.Current;
        AnsiConsole.MarkupLine($"This machine ({Markup.Escape(Environment.MachineName)}): [aqua]{Markup.Escape(role.ToString())}[/]");
        AnsiConsole.MarkupLine($"Daemon: {(Installer.DaemonRunning() ? "[green]running[/]" : "[grey]not running[/]")}");
        AnsiConsole.MarkupLine($"Orchestrator data: {Markup.Escape(OrchestratorDb.LivePath)}");
        if (Environment.GetEnvironmentVariable("MINDATTIC_TICKETS_DB", EnvironmentVariableTarget.User) is { Length: > 0 } tickets)
            AnsiConsole.MarkupLine($"Ticket data: {Markup.Escape(tickets)} (the host's, refreshed by orchestrator machine pull)");
        if (TermGateway.Running() is { } gw)
            AnsiConsole.MarkupLine($"Gateway: [green]running[/] on {Markup.Escape(gw.Bind)}:{gw.Port}, certificate SHA-256 {gw.CertSha256}");
        if (role.IsHost)
        {
            if (MachineSetup.ConnectLine() is { } line)
            {
                AnsiConsole.MarkupLine("On your PC, run (it holds this host's owner key: keep it to yourself):");
                AnsiConsole.WriteLine("  " + line);
            }
            else AnsiConsole.MarkupLine("[yellow]The gateway isn't running yet; the daemon starts it within a tick.[/]");
        }
        else if (role.IsClient && role.GatewayUrl is null)
            AnsiConsole.MarkupLine("[yellow]No host connected yet: run setup-host.ps1 on the VM, then the orchestrator machine connect line it prints.[/]");
        else if (!role.IsClient)
            AnsiConsole.MarkupLine("[grey]To move the work to a VM: orchestrator machine handoff --kit <folder> here, then setup-host.ps1 on the VM (docs/REMOTE_HOST.md).[/]");
        return 0;
    }
}

/// <summary><c>orchestrator machine handoff [--kit &lt;folder&gt;] [--no-push]</c>: on the PC, before the VM is set up.</summary>
internal sealed class MachineHandoffCommand : Command<MachineHandoffCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("--kit <FOLDER>")]
        [Description("Also write the host kit there (C:\\LocalNuGet, settings without secrets, setup-host.ps1) to copy to the VM.")]
        public string? Kit { get; init; }

        [CommandOption("--no-push")]
        [Description("Don't copy the data out of SQL Server and push it first (it's already pushed).")]
        public bool NoPush { get; init; }
    }

    public override int Execute(CommandContext context, Settings settings) =>
        Machine.Setup().Handoff(settings.Kit is { } k ? Path.GetFullPath(k) : null, !settings.NoPush, Machine.Self) ? 0 : 1;
}

/// <summary><c>orchestrator machine connect &lt;host&gt; --pin &lt;sha256&gt; --owner-key &lt;key&gt;</c>: on the PC, the line the host printed.</summary>
internal sealed class MachineConnectCommand : AsyncCommand<MachineConnectCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<HOST>")]
        [Description("The VM: its name or address, host:port, or https://host:port.")]
        public string Host { get; init; } = "";

        [CommandOption("--pin <SHA256>")]
        public string Pin { get; init; } = "";

        [CommandOption("--owner-key <KEY>")]
        [Description("The host's owner key; \"-\" reads it from standard input, and it's asked for (hidden) when left out.")]
        public string? OwnerKey { get; init; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        var key = settings.OwnerKey switch
        {
            "-" => Console.In.ReadLine()?.Trim(),
            { Length: > 0 } k => k.Trim(),
            _ => AnsiConsole.Prompt(new TextPrompt<string>("Owner key:").Secret()).Trim(),
        };
        if (string.IsNullOrWhiteSpace(key)) { AnsiConsole.MarkupLine("[red]No owner key given.[/]"); return 1; }
        try { return await Machine.Setup().ConnectAsync(settings.Host, settings.Pin, key, Machine.Self) ? 0 : 1; }
        catch (ArgumentException ex) { AnsiConsole.MarkupLine($"[red]{Markup.Escape(ex.Message)}[/]"); return 1; }
    }
}

/// <summary><c>orchestrator machine host [--bind &lt;address&gt;] [--port &lt;n&gt;]</c>: on the VM (setup-host.ps1 runs it).</summary>
internal sealed class MachineHostCommand : AsyncCommand<MachineHostCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("--bind <ADDRESS>")]
        [Description("Where the gateway listens: 0.0.0.0 (default, every network), or one address such as a Tailscale 100.x one.")]
        public string Bind { get; init; } = "0.0.0.0";

        [CommandOption("--port <PORT>")]
        public int? Port { get; init; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        var line = await Machine.Setup().HostAsync(new Installer(), settings.Bind, settings.Port);
        if (line is null) return 1;
        AnsiConsole.MarkupLine("[green]This machine is the host.[/] On your PC, run (it holds this host's owner key: keep it to yourself):");
        AnsiConsole.WriteLine("  " + line);
        return 0;
    }
}

/// <summary><c>orchestrator machine standalone</c>: everything on this machine again.</summary>
internal sealed class MachineStandaloneCommand : Command
{
    public override int Execute(CommandContext context)
    {
        Machine.Setup().Standalone(new Installer());
        return 0;
    }
}

/// <summary><c>orchestrator machine pull</c>: the newest code (and, on a client, the host's tickets) in every repo.</summary>
internal sealed class MachinePullCommand : AsyncCommand
{
    public override async Task<int> ExecuteAsync(CommandContext context) => await Machine.Setup().PullAsync() == 0 ? 0 : 1;
}

/// <summary><c>orchestrator machine kit &lt;folder&gt;</c>: only the host kit.</summary>
internal sealed class MachineKitCommand : Command<MachineKitCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<FOLDER>")]
        public string Folder { get; init; } = "";
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        Machine.Setup().WriteKit(Path.GetFullPath(settings.Folder));
        return 0;
    }
}
