using System.Windows.Media;
using MindAttic.Orchestrator.Billing;

namespace MindAttic.Orchestrator.Wpf.ViewModels;

/// <summary>
/// Who every CLI bills (M.ORCH-T436): "Claude Team: you (org)" or "API KEY: sk-ant-...WXYZ" (masked), in the tooltip of
/// the Accounts view's billing button, and what's being done about the CLIs billing the other thing, on the line under the
/// view's toolbar (M.ORCH-T628: they went with the account menu's switches to the Accounts view).
/// </summary>
public sealed partial class MainViewModel
{
    private BillingStatus? billing;
    private string billingMessage = "";
    private DateTime billingMessageUtc;

    /// <summary>"Billing: Claude Team: pdftransferjr@mindattic.com (MindAttic)" or "Billing: API KEY: sk-ant-...WXYZ".</summary>
    public string BillingHeadline => billing is null ? "" : $"Billing: {billing.Headline}";

    /// <summary>Amber while the API key bills (emergencies only), so it's never left on unnoticed.</summary>
    public Brush BillingBrush => billing?.Mode == BillingModes.Api ? Palette.Waiting : Palette.Working;

    /// <summary>What needs saying under it: what the last switch did, a problem, and the CLIs billing the other thing.</summary>
    public string BillingDetail => string.Join(" ", new[] { billingMessage, DetailOf(billing) }.Where(s => s.Length > 0));

    /// <summary>Every open CLI's own billing, one line each, for the tooltip.</summary>
    public string BillingClis => billing is null ? "" : string.Join(Environment.NewLine, BillingText.Lines(billing, DateTime.UtcNow));

    /// <summary>The problem, and how many open CLIs bill the other thing and how many of those only you can restart.</summary>
    internal static string DetailOf(BillingStatus? status)
    {
        if (status is null) return "";
        var parts = new List<string>();
        if (status.Problem is { } problem) parts.Add(problem);
        if (status.Mismatched is { Count: > 0 } off)
        {
            var other = BillingModes.Name(BillingModes.Other(status.Mode));
            var manual = off.Where(c => c.Restart is "manual" or "refused").ToList();
            parts.Add($"{off.Count} open CLI{(off.Count == 1 ? "" : "s")} still bill{(off.Count == 1 ? "s" : "")} {other}" +
                      (off.Count > manual.Count ? $"; {off.Count - manual.Count} restart{(off.Count - manual.Count == 1 ? "s" : "")} through the Launcher at the next idle point" : "") +
                      (manual.Count > 0 ? $"; restart by hand: {string.Join(", ", manual.Select(c => $"{c.Where} (pid {c.Pid})"))}" : "") + ".");
        }
        else if (status.Mode == BillingModes.Api) parts.Add("For emergencies only: switch back to Claude Team when it's over.");
        return string.Join(" ", parts);
    }

    private void ApplyBilling(Snapshot next)
    {
        billing = next.Billing;
        // What the last switch did stays up for a couple of minutes, as a toast would.
        if (DateTime.UtcNow - billingMessageUtc > TimeSpan.FromMinutes(2)) billingMessage = "";
        Raise(nameof(IsOnApiKey)); Raise(nameof(BillingHeadline)); Raise(nameof(BillingBrush)); Raise(nameof(BillingDetail)); Raise(nameof(BillingClis));
        RaiseTabStatus();
    }
}
