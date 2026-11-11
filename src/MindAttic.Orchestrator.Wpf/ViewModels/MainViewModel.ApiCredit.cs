using System.Windows.Input;
using MindAttic.Components.Wpf.Mvvm;
using MindAttic.Orchestrator.Billing;

namespace MindAttic.Orchestrator.Wpf.ViewModels;

/// <summary>
/// The API key person's starting credit, set from its page (M.ORCH-T954) as well as with <c>orchestrator billing credit
/// &lt;usd&gt;</c>: both write through <see cref="ApiCredit.Set"/>, so the credit means the same thing whichever set it (the
/// burndown, and the hard stop once it's spent, read it). Only the credit settings change, never the key's credentials.
/// </summary>
public sealed partial class MainViewModel
{
    private string apiCreditText = "";
    private string apiCreditMessage = "";
    private string apiCreditError = "";
    private ICommand? setApiCreditCommand;

    /// <summary>What's typed in the starting-credit field, in US dollars ("50", "$50"); 0 clears the credit.</summary>
    public string ApiCreditText { get => apiCreditText; set => Set(ref apiCreditText, value ?? ""); }

    /// <summary>What the last Set did; empty before one, or when it was refused.</summary>
    public string ApiCreditMessage { get => apiCreditMessage; private set => Set(ref apiCreditMessage, value); }

    /// <summary>Why the last Set was refused (shown in the library's bad colour); empty otherwise.</summary>
    public string ApiCreditError { get => apiCreditError; private set => Set(ref apiCreditError, value); }

    /// <summary>The field's placeholder: the credit set now, or an example.</summary>
    public string ApiCreditPlaceholder => Spend?.CreditUsd is { } c ? $"now {ApiSpend.Usd(c)}" : "e.g. 50";

    /// <summary>Sets the starting credit from <see cref="ApiCreditText"/>, then refreshes so the page shows it.</summary>
    public ICommand SetApiCreditCommand => setApiCreditCommand ??= new RelayCommand(async () =>
    {
        if (SetApiCredit(DateTime.UtcNow)) await RefreshAsync();
    });

    /// <summary>Validates and saves the typed credit; true when it was saved.</summary>
    internal bool SetApiCredit(DateTime nowUtc)
    {
        ApiCreditMessage = "";
        if (!ApiCredit.TryParse(ApiCreditText, out var usd))
        {
            ApiCreditError = $"\"{ApiCreditText.Trim()}\" isn't an amount of US dollars: type a number of dollars, 0 or more (0 clears it).";
            return false;
        }
        try
        {
            ApiCredit.Set(store, usd, nowUtc);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            ApiCreditError = $"Couldn't save the starting credit: {ex.Message}";
            return false;
        }
        ApiCreditError = "";
        ApiCreditMessage = ApiCredit.Said(usd);
        ApiCreditText = "";
        return true;
    }
}
