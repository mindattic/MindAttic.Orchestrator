using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using MindAttic.Components.Wpf.Mvvm;
using MindAttic.Orchestrator.Budget;

namespace MindAttic.Orchestrator.Wpf.ViewModels;

/// <summary>
/// One roster member's row in the User Management modal (M.ORCH-T1898, renamed from Logins): their name and email, and their
/// stored email-account credential (<see cref="RosterCredentialStore"/>), to view, add, edit or delete right on the row. The
/// password shows plainly, no reveal toggle - Ryan asked to review it directly, not hide it behind masking.
/// </summary>
public sealed class UserCredentialRowViewModel(string email, string name) : ObservableObject
{
    private string password = "";
    private bool hasCredential;
    private string message = "";
    private ICommand? saveCommand, deleteCommand;

    /// <summary>The member's email: the credential store's own key.</summary>
    public string Email { get; } = email;

    /// <summary>The member's display name (their tab's title).</summary>
    public string Name { get; } = name;

    /// <summary>The password as typed or last loaded.</summary>
    public string Password { get => password; set => Set(ref password, value ?? ""); }

    /// <summary>Whether a credential is already saved for this member (Delete is disabled otherwise).</summary>
    public bool HasCredential { get => hasCredential; private set => Set(ref hasCredential, value); }

    /// <summary>What the last Save or Delete on this row did, or why it couldn't.</summary>
    public string Message { get => message; private set => Set(ref message, value); }

    /// <summary>Re-reads this row's stored credential (the modal's own open, and right after Save/Delete).</summary>
    public void Load()
    {
        var creds = RosterCredentialStore.Default.Get(Email);
        Password = creds?.Password ?? "";
        HasCredential = creds is not null;
    }

    /// <summary>Saves (adds, or edits in place) this member's password.</summary>
    public ICommand SaveCommand => saveCommand ??= new RelayCommand(() =>
    {
        try
        {
            RosterCredentialStore.Default.Set(Email, Password);
            HasCredential = true;
            Message = "Saved.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            Message = $"Couldn't save: {ex.Message}";
        }
    });

    /// <summary>Deletes this member's stored credential entirely (not just clears the field).</summary>
    public ICommand DeleteCommand => deleteCommand ??= new RelayCommand(() =>
    {
        try
        {
            RosterCredentialStore.Default.Remove(Email);
            Password = "";
            HasCredential = false;
            Message = "Deleted.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Message = $"Couldn't delete: {ex.Message}";
        }
    }, () => HasCredential);
}

/// <summary>The User Management modal's credential list (M.ORCH-T1898): one <see cref="UserCredentialRowViewModel"/> per
/// roster member, rebuilt from the roster each time the modal opens.</summary>
public sealed partial class MainViewModel
{
    private ObservableCollection<UserCredentialRowViewModel>? userCredentials;

    /// <summary>One row per roster member, their stored email-account credential to view, add, edit or delete.</summary>
    public ObservableCollection<UserCredentialRowViewModel> UserCredentials => Root.userCredentials ??= [];

    /// <summary>Rebuilds <see cref="UserCredentials"/> from the roster (<see cref="MainViewModel.lastMembers"/>) and each
    /// member's stored credential, read fresh. Called when the modal opens, not kept live while it's shut.</summary>
    private void RebuildUserCredentials()
    {
        var rows = Root.lastMembers
            .OrderBy(m => m.Email, StringComparer.OrdinalIgnoreCase)
            .Select(m => new UserCredentialRowViewModel(m.Email, MemberRoster.TitleOf(m, null)))
            .ToList();
        foreach (var row in rows) row.Load();
        Root.userCredentials = new ObservableCollection<UserCredentialRowViewModel>(rows);
        Raise(nameof(UserCredentials));
    }
}
