using System.ComponentModel;
using MindAttic.Orchestrator.Budget;
using MindAttic.Orchestrator.Sessions;
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
        var topology = MachineTopology.From(role);
        AnsiConsole.MarkupLine($"This machine ({Markup.Escape(Environment.MachineName)}): [aqua]{Markup.Escape(role.ToString())}[/]");
        if (topology.IsHost && TermMemberKeys.List().Count(k => k.IsLive) is > 0 and var invited)
            AnsiConsole.MarkupLine($"Members invited: {invited} (orchestrator machine members)");
        AnsiConsole.MarkupLine($"Daemon: {(Installer.DaemonRunning() ? "[green]running[/]" : "[grey]not running[/]")}");
        AnsiConsole.MarkupLine($"Orchestrator data: {Markup.Escape(OrchestratorDb.LivePath)}");
        if (Environment.GetEnvironmentVariable("MINDATTIC_TICKETS_DB", EnvironmentVariableTarget.User) is { Length: > 0 } tickets)
            AnsiConsole.MarkupLine($"Ticket data: {Markup.Escape(tickets)} (the host's, refreshed by orchestrator machine pull)");
        if (TermGateway.Running() is { } gw)
            AnsiConsole.MarkupLine($"Gateway: [green]running[/] on {Markup.Escape(gw.Bind)}:{gw.Port}, certificate SHA-256 {gw.CertSha256}");
        if (topology.IsHost)
        {
            if (MachineSetup.ConnectLine() is { } line)
            {
                AnsiConsole.MarkupLine("On your PC, run (it holds this host's owner key: keep it to yourself):");
                AnsiConsole.WriteLine("  " + line);
            }
            else AnsiConsole.MarkupLine("[yellow]The gateway isn't running yet; the daemon starts it within a tick.[/]");
        }
        else if (topology.IsClient && role.GatewayUrl is null)
            AnsiConsole.MarkupLine("[yellow]No host connected yet: run setup-host.ps1 on the VM, then the orchestrator machine connect line it prints.[/]");
        else if (!topology.IsClient)
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

        [CommandOption("--member <EMAIL>")]
        [Description("An org member's own PC (hub phase 1): the member the host's orchestrator machine invite named.")]
        public string? Member { get; init; }

        [CommandOption("--member-key <KEY>")]
        [Description("With --member: the member key the host minted for them (\"-\" reads it from standard input). Never the owner key.")]
        public string? MemberKey { get; init; }

        public override ValidationResult Validate() =>
            OwnerKey is not null && (MemberKey is not null || Member is not null) ? ValidationResult.Error("--owner-key is the owner's PC; --member and --member-key a member's: give one or the other")
            : (Member is null) != (MemberKey is null) ? ValidationResult.Error("--member and --member-key go together")
            : ValidationResult.Success();
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        if (settings.Member is { } member && settings.MemberKey is { } memberKey)
        {
            var mk = memberKey == "-" ? Console.In.ReadLine()?.Trim() : memberKey.Trim();
            if (string.IsNullOrWhiteSpace(mk)) { AnsiConsole.MarkupLine("[red]No member key given.[/]"); return 1; }
            try { return await Machine.Setup().ConnectMemberAsync(settings.Host, settings.Pin, mk, member, Machine.Self) ? 0 : 1; }
            catch (ArgumentException ex) { AnsiConsole.MarkupLine($"[red]{Markup.Escape(ex.Message)}[/]"); return 1; }
        }
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

/// <summary><c>orchestrator machine tickets -- &lt;args&gt;</c> (M.ORCH-T1479): on a client, a tickets command run on the host. The
/// client's <c>tickets</c> shim calls it, so it's rarely typed.</summary>
internal sealed class MachineTicketsCommand : AsyncCommand
{
    public override async Task<int> ExecuteAsync(CommandContext context)
    {
        var args = context.Remaining.Raw.ToList();
        if (args.Count == 0) { Console.Error.WriteLine("Say what to run: orchestrator machine tickets -- ticket list"); return 2; }
        var (code, output) = await Machine.Setup().RunTicketsOnHostAsync(args, Environment.CurrentDirectory, CancellationToken.None);
        (code == 0 ? Console.Out : Console.Error).Write(output.EndsWith('\n') || output.Length == 0 ? output : output + Environment.NewLine);
        return code;
    }
}

/// <summary>Shared bits of <c>orchestrator machine invite | members | revoke</c> (hub phase 1, M.ORCH-T1640).</summary>
internal static class MachineMembers
{
    /// <summary>How long after the gateway last saw a member they still count as connected: the Vault's setting.</summary>
    public static TimeSpan ConnectedWindow()
    {
        var minutes = Ctx.Store().TryLoadSettings(out var s) ? s.MemberConnectedMinutes : OrchestratorSettings.DefaultMemberConnectedMinutes;
        return TimeSpan.FromMinutes(Math.Max(0.1, minutes));
    }
}

/// <summary>
/// <c>orchestrator machine invite &lt;member&gt;</c> (hub phase 1, M.ORCH-T1640): on the host, mints an active org member their own
/// key to this host's gateway (replacing any key they had) and prints the connect line for their PC. The key carries their email,
/// never a password or a Claude credential; only its SHA-256 is kept here.
/// </summary>
internal sealed class MachineInviteCommand : Command<MachineInviteCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<WHO>")]
        [Description("An active member: email, the part before the @, name, first name, login alias or initials.")]
        public string Who { get; init; } = "";
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        if (!MachineTopology.Current.IsHost)
        {
            AnsiConsole.MarkupLine("[red]Only the host invites members: run this on the host (orchestrator machine host makes a machine one; docs/REMOTE_HOST.md).[/]");
            return 1;
        }
        if (GatewayAcl.Member(settings.Who) is not { } email) return 1;
        if (MemberRoster.Find(GatewayAcl.Db(), email) is { } member && member.Status != MemberRoster.Statuses[0])
        {
            AnsiConsole.MarkupLine($"[red]{Markup.Escape(email)} is {Markup.Escape(member.Status)}, not an active member: orchestrator members set {Markup.Escape(email)} --status active first.[/]");
            return 1;
        }
        if (string.Equals(email, GatewayAcl.Owner(), StringComparison.OrdinalIgnoreCase))
        {
            AnsiConsole.MarkupLine("[red]That's this host's owner: the owner's PC connects with the owner key (orchestrator machine prints its line).[/]");
            return 1;
        }
        var replaced = TermMemberKeys.List().Any(k => k.IsLive && TermMemberKeys.Normalize(k.Email) == email);
        string key;
        try { key = TermMemberKeys.Invite(email, Environment.UserName, DateTime.UtcNow); }
        catch (IOException ex) { AnsiConsole.MarkupLine($"[red]{Markup.Escape(ex.Message)}[/]"); return 1; }
        // The gateway checks the member against the ACL snapshot: written now, so the key works at once.
        GatewayAcl.Publish();
        AnsiConsole.MarkupLine($"[green]Invited {Markup.Escape(email)}.[/]" + (replaced ? " [grey](Their previous key no longer works.)[/]" : ""));
        if (MachineSetup.MemberConnectLine(email, key) is { } line)
        {
            AnsiConsole.MarkupLine($"On their own PC, {Markup.Escape(email)} runs (it holds their member key: send it to them privately, and only to them):");
            AnsiConsole.WriteLine("  " + line);
        }
        else
        {
            AnsiConsole.MarkupLine("[yellow]The gateway isn't running yet (the daemon starts it within a tick); orchestrator machine shows its pin once it is. Their key:[/]");
            AnsiConsole.WriteLine("  " + key);
        }
        AnsiConsole.MarkupLine($"[grey]They watch their org's shared CLIs here, read-only, and run tickets ticket show|list|new|note|close as themselves. " +
            $"orchestrator machine revoke {Markup.Escape(email)} cuts them off.[/]");
        return 0;
    }
}

/// <summary><c>orchestrator machine members</c> (hub phase 1, M.ORCH-T1640): every member invited to this host, their key, when the
/// gateway last saw them, and who is connected now.</summary>
internal sealed class MachineMembersCommand : Command
{
    public override int Execute(CommandContext context)
    {
        var keys = TermMemberKeys.List();
        if (keys.Count == 0)
        {
            AnsiConsole.MarkupLine("[grey]No members invited to this host (orchestrator machine invite <member>).[/]");
            return 0;
        }
        var now = DateTime.UtcNow;
        var seen = TermMemberKeys.ReadSeen();
        var connected = TermMemberKeys.Connected(now, MachineMembers.ConnectedWindow()).ToHashSet(StringComparer.Ordinal);
        var table = new Table().Border(TableBorder.Rounded).AddColumns("Member", "Key", "Invited", "By", "Last seen", "Connected");
        foreach (var k in keys.OrderBy(k => k.Email, StringComparer.Ordinal))
        {
            var email = TermMemberKeys.Normalize(k.Email);
            table.AddRow(Markup.Escape(email),
                k.IsLive ? "[green]live[/]" : $"[grey]revoked {GatewayAcl.When(k.RevokedUtc)}[/]",
                GatewayAcl.When(k.CreatedUtc), Markup.Escape(k.CreatedBy ?? ""),
                seen.TryGetValue(email, out var at) ? GatewayAcl.When(at) : "[grey]never[/]",
                connected.Contains(email) ? "[green]yes[/]" : "");
        }
        AnsiConsole.Write(table);
        return 0;
    }
}

/// <summary><c>orchestrator machine revoke &lt;member&gt;</c> (hub phase 1, M.ORCH-T1640): cuts a member off this host at once. The
/// gateway refuses their key and every token minted for them within a second, and closes their open streams at its next access
/// check (within 10 seconds).</summary>
internal sealed class MachineRevokeCommand : Command<MachineRevokeCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<WHO>")]
        [Description("A member: email, the part before the @, name, first name, login alias or initials.")]
        public string Who { get; init; } = "";
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        // A key can outlive the member it names: their email is taken as given when no member matches.
        var found = MemberLookup.Match(OrgHelpCommand.People(), settings.Who);
        if (found.Count > 1)
        {
            AnsiConsole.MarkupLine($"[red]\"{Markup.Escape(settings.Who.Trim())}\" could be more than one member: name one of them.[/]");
            foreach (var p in found) AnsiConsole.MarkupLine($"  {Markup.Escape(p.Label)}");
            return 1;
        }
        var email = found is [var one] ? one.Email : AccountDirs.Normalize(settings.Who) ?? TermMemberKeys.Normalize(settings.Who);
        bool revoked;
        try { revoked = TermMemberKeys.Revoke(email, DateTime.UtcNow); }
        catch (IOException ex) { AnsiConsole.MarkupLine($"[red]{Markup.Escape(ex.Message)}[/]"); return 1; }
        AnsiConsole.MarkupLine(revoked
            ? $"[green]Revoked {Markup.Escape(email)}'s member key.[/] The host refuses it, and every token minted with it, within a second; their open streams close within 10 seconds."
            : $"[grey]{Markup.Escape(email)} had no live member key here.[/]");
        return revoked ? 0 : 1;
    }
}
