namespace MindAttic.Orchestrator.Billing;

/// <summary>Billing in words, the same in <c>orchestrator status</c>, <c>orchestrator billing</c> and the dashboard.</summary>
public static class BillingText
{
    /// <summary>What one CLI bills, and what's being done about it when that's the other mode: "Claude Team", "API key:
    /// outside the Launcher, restart it by hand to bill Claude Team".</summary>
    public static string Of(CliBilling cli)
    {
        if (cli.Bills is not { } bills) return cli.Auth is { } other ? $"bills {other}, not Claude Team or the API key" : "billing not known yet";
        var name = BillingModes.Name(bills);
        var to = BillingModes.Name(BillingModes.Other(bills));
        return cli.Restart switch
        {
            "requested" => $"{name}: the Launcher restarts it at its next idle point to bill {to}",
            "pending" => $"{name}: the Launcher is being asked to restart it to bill {to}",
            "refused" => $"{name}: its Launcher tab couldn't be asked, so close it and reopen it to bill {to}",
            "manual" => $"{name}: outside the Launcher, so restart it by hand to bill {to}",
            _ => name,
        };
    }

    /// <summary>Who's billed, then each open Claude CLI's own billing, one line each.</summary>
    public static IReadOnlyList<string> Lines(BillingStatus status, DateTime nowUtc)
    {
        var lines = new List<string>
        {
            $"Billing: {status.Headline}" + (status.SinceUtc is { } since && since > DateTime.UnixEpoch
                ? $" (since {Formatting.ResetTime(since, nowUtc)})" : ""),
        };
        if (status.Problem is { } problem) lines.Add($"  ! {problem}");
        foreach (var c in status.Clis)
            lines.Add($"  {c.Where} (pid {c.Pid}{(c.Host is null ? ", outside the Launcher" : ", Launcher tab")}): {Of(c)}");
        if (status.Mismatched is { Count: > 0 } off)
            lines.Add($"  {off.Count} of {status.Clis.Count} open CLIs bill {BillingModes.Name(BillingModes.Other(status.Mode))}" +
                      (off.Count(c => c.Restart == "manual") is var manual and > 0 ? $"; {manual} outside the Launcher need a manual restart" : ""));
        return lines;
    }
}
