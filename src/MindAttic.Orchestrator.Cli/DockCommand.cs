using MindAttic.Orchestrator;
using MindAttic.Orchestrator.Sessions;
using Spectre.Console;
using Spectre.Console.Cli;

/// <summary>
/// <c>orchestrator dock</c> (M.ORCH-Tnnn): pulls this standalone Claude tab into the dashboard's Terminals panel, resuming it
/// there (<see cref="Relocate"/>) rather than restarting it fresh. No arguments: it's run as a shell tool call from inside the
/// very CLI being docked, so its own process ancestry already names the Launcher host tab to relocate.
/// </summary>
internal sealed class DockCommand : Command<DockCommand.Settings>
{
    public sealed class Settings : CommandSettings { }

    /// <summary>What it reads and writes to find, reach and relocate the live CLI; tests point every piece elsewhere (never a
    /// real ancestry walk, a real snapshot scan, a real pipe, or the live lock store).</summary>
    internal sealed record Deps(
        Func<IReadOnlyList<(int Pid, string Exe)>> Ancestors,
        Func<IReadOnlyList<HostSession>> Hosts,
        AccountMove Move,
        Func<bool> Listening,
        Func<HostSession, string, Task<bool>> Send,
        RelocateLocks Locks);

    internal static Func<Deps>? Override { get; set; }

    private const string NotYetInSnapshot = "This tab's session isn't in the orchestrator's snapshot yet; try again in a few seconds.";

    public override int Execute(CommandContext context, Settings settings)
    {
        var deps = Override?.Invoke() ?? Live();
        var now = DateTime.UtcNow;

        // Its own process ancestry already says which Launcher host tab runs it (ProcessTree.ClaudeAncestor's own exe-name idiom).
        var hostPid = deps.Ancestors()
            .Where(a => a.Exe.StartsWith("MindAttic.Launcher.exe", StringComparison.OrdinalIgnoreCase))
            .Select(a => (int?)a.Pid)
            .FirstOrDefault();
        if (hostPid is not { } hp)
        {
            AnsiConsole.MarkupLine("[red]This isn't running inside a Launcher-hosted tab (MindAttic.Launcher.exe host), so there's nothing to dock.[/]");
            return 1;
        }

        var hosts = deps.Hosts();
        var host = hosts.FirstOrDefault(h => h.ProcessId == hp);
        if (host is null)
        {
            AnsiConsole.MarkupLine($"[red]{NotYetInSnapshot}[/]");
            return 1;
        }
        if (!host.Provider.Equals("Claude", StringComparison.OrdinalIgnoreCase))
        {
            AnsiConsole.MarkupLine($"[red]Dock only supports Claude tabs, not {Markup.Escape(host.Provider)}.[/]");
            return 1;
        }

        var cli = deps.Move.Live(hosts, now).FirstOrDefault(c => c.Host?.ProcessId == hp);
        if (cli is null)
        {
            AnsiConsole.MarkupLine($"[red]{NotYetInSnapshot}[/]");
            return 1;
        }
        // The per-CLI login it already runs on (null: the global one), carried through unchanged by the relocation.
        var email = deps.Move.CurrentLogin(cli);
        var configDir = email is null ? null : deps.Move.Dirs.For(email).Directory;
        var loginLabel = email ?? AccountMove.GlobalLabel.ToLowerInvariant();

        // Before anything destructive: a CLI ended here with nowhere to reopen would simply be lost.
        if (!deps.Listening())
        {
            AnsiConsole.MarkupLine("[red]MindAttic.Orchestrator isn't running, so there's nowhere for this session to dock: " +
                "open the dashboard first, then run /dock again. Nothing was changed.[/]");
            return 1;
        }

        var look = LoginLook.ForConfigDir(configDir);
        var move = new RelocateLock
        {
            SessionId = cli.SessionId,
            Pid = cli.Pid,
            HostPid = hp,
            Repo = cli.Where,
            Cwd = cli.Cwd,
            ConfigDir = configDir,
            Email = email,
            Initials = look.Initials,
            TabColor = look.TabColor,
            Direction = RelocateDirection.Dock,
            StartedUtc = now,
        };
        if (!deps.Locks.TryBegin(move, out _))
        {
            AnsiConsole.MarkupLine("[red]This session is already being relocated.[/]");
            return 1;
        }

        bool sent;
        try { sent = deps.Send(host, Relocate.Message(cli.SessionId, cli.Pid, cli.Cwd, now)).GetAwaiter().GetResult(); }
        catch (Exception ex) when (ex is IOException or TimeoutException or UnauthorizedAccessException or OperationCanceledException) { sent = false; }
        if (!sent)
        {
            deps.Locks.End(cli.SessionId);
            AnsiConsole.MarkupLine($"[red]Couldn't reach this tab's control pipe, so it can't be docked (a Launcher from before this feature?): " +
                $"close it and reopen the session yourself under {Markup.Escape(loginLabel)} (claude --resume {cli.SessionId}).[/]");
            return 1;
        }

        AnsiConsole.MarkupLine($"Docking requested: this session ends here once idle and should reopen in {Markup.Escape(loginLabel)}'s dashboard tab " +
            "within the next few seconds. If it doesn't show up within half an hour, check the dashboard's Terminals panel.");
        return 0;
    }

    private static Deps Live() => new(
        () => ProcessTree.Ancestors(Environment.ProcessId),
        () => new HostSessionScanner(OrchestratorOptions.Load()).Scan(),
        AccountMove.Default(),
        () => PaneBroker.IsListening(),
        (host, message) => SessionControl.SendAsync(SessionControl.ControlPipeName(host), message, submit: true),
        new RelocateLocks(Ctx.Store()));
}
