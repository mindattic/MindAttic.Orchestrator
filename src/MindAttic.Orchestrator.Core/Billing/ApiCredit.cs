using System.Globalization;
using MindAttic.Orchestrator.Settings;

namespace MindAttic.Orchestrator.Billing;

/// <summary>
/// Setting the API key person's starting credit (M.ORCH-T906, M.ORCH-T954): the one code path both
/// <c>orchestrator billing credit &lt;usd&gt;</c> and the dashboard's field on the API key's page write through, so the two
/// always mean the same thing. Only the <c>ApiCreditUsd</c>/<c>ApiCreditSinceUtc</c> settings change; the API key's
/// credentials are never touched.
/// </summary>
public static class ApiCredit
{
    /// <summary>Reads "50", "$50" or "12.5" as US dollars: a finite, non-negative amount (0 clears the credit); false otherwise.</summary>
    public static bool TryParse(string? text, out double usd)
    {
        usd = 0;
        if (string.IsNullOrWhiteSpace(text)) return false;
        return double.TryParse(text.Trim().TrimStart('$').Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out usd) &&
               usd >= 0 && !double.IsNaN(usd) && !double.IsInfinity(usd);
    }

    /// <summary>Sets the starting credit to <paramref name="usd"/> (spend counts against it from <paramref name="nowUtc"/>), or
    /// clears it at 0. Throws on an amount <see cref="TryParse"/> would refuse.</summary>
    public static void Set(JsonStore store, double usd, DateTime nowUtc)
    {
        if (usd < 0 || double.IsNaN(usd) || double.IsInfinity(usd))
            throw new ArgumentOutOfRangeException(nameof(usd), usd, "a starting credit is a non-negative amount of US dollars");
        // LoadSettingsToChange: a settings row that can't be read is never saved over with defaults (bughunt 2026-10-27).
        store.SaveSettings(store.LoadSettingsToChange() with
        {
            ApiCreditUsd = usd > 0 ? usd : null,
            ApiCreditSinceUtc = usd > 0 ? nowUtc : null,
        });
    }

    /// <summary>What setting <paramref name="usd"/> did, in words.</summary>
    public static string Said(double usd) => usd > 0
        ? $"The API key's starting credit is {ApiSpend.Usd(usd)}: what its CLIs spend from now on counts against it."
        : "The API key's starting credit is cleared: its page shows only what it spends.";
}
