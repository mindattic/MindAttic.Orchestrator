using System.Windows.Media;
using MindAttic.Orchestrator.Billing;

namespace MindAttic.Orchestrator.Wpf.ViewModels;

/// <summary>
/// The header's billing line (M.ORCH-T436): who every CLI bills ("Claude Team: you (org)" or "API KEY: sk-ant-...WXYZ",
/// masked), what's being done about the CLIs billing the other thing, and the toggle that switches every CLI.
/// </summary>
public sealed partial class MainViewModel
{
    public const string TeamChoice = "Claude Team";
    public const string ApiChoice = "API key";

    private BillingStatus? billing;
    private string billingChoice = TeamChoice;
    private string billingMessage = "";
    private DateTime billingMessageUtc;
    private bool switchingBilling;

    public IReadOnlyList<string> BillingChoices { get; } = [TeamChoice, ApiChoice];

    /// <summary>The pill lit: the mode in force, or the one being switched to.</summary>
    public string BillingChoice
    {
        get => billingChoice;
        set
        {
            // Ctrl+click on the selected pill deselects it, writing null: that's no switch (M.ORCH-T431, T432).
            if (value is null || value == billingChoice || switchingBilling) return;
            billingChoice = value;
            Raise();
            _ = SwitchBillingAsync(value == ApiChoice ? BillingModes.Api : BillingModes.Team);
        }
    }

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
        if (!switchingBilling && DateTime.UtcNow - billingMessageUtc > TimeSpan.FromMinutes(2)) billingMessage = "";
        if (!switchingBilling && billing is not null)
        {
            billingChoice = billing.Mode == BillingModes.Api ? ApiChoice : TeamChoice;
            Raise(nameof(BillingChoice));
        }
        Raise(nameof(BillingHeadline)); Raise(nameof(BillingBrush)); Raise(nameof(BillingDetail)); Raise(nameof(BillingClis));
    }

    private async Task SwitchBillingAsync(string mode)
    {
        switchingBilling = true;
        billingMessage = $"Switching every CLI to {BillingModes.Name(mode)}…";
        Raise(nameof(BillingDetail));
        try
        {
            var hosts = snapshot?.Repos.SelectMany(r => r.Hosts).ToList() ?? [];
            var result = await Task.Run(() => aggregator.BillingSwitch.Switch(mode, "dashboard", hosts, DateTime.UtcNow));
            billing = result.Status;
            billingMessage = result.Error ?? string.Join(" ", result.Lines.Take(1));
            billingMessageUtc = DateTime.UtcNow;
            if (result.Error is not null)
                Toasts.Insert(0, new ToastViewModel("Billing not switched", result.Error, Palette.Over, null));
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            billingMessage = $"Couldn't switch billing: {ex.Message}";
            billingMessageUtc = DateTime.UtcNow;
            OrchestratorLog.For("Dashboard").Error(ex, "billing switch failed");
        }
        finally
        {
            switchingBilling = false;
            if (billing is not null) billingChoice = billing.Mode == BillingModes.Api ? ApiChoice : TeamChoice;
            Raise(nameof(BillingChoice)); Raise(nameof(BillingHeadline)); Raise(nameof(BillingBrush)); Raise(nameof(BillingDetail)); Raise(nameof(BillingClis));
        }
        await RefreshAsync();
    }
}
