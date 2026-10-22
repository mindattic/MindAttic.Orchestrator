using System.Text;
using Microsoft.Win32;

namespace MindAttic.Orchestrator.Sessions;

/// <summary>
/// Each account's own login screen (M.ORCH-T610). <c>claude auth login</c> opens its sign-in page through <c>BROWSER</c> when
/// it's set (the URL is its one argument, carrying the account's email as its <c>login_hint</c>), else in the default browser,
/// which is already signed in to claude.ai as somebody and would authorize them without asking. So a per-CLI login's sign-in
/// goes through a small script in the account's folder (<see cref="AccountDir.LoginOpenerPath"/>) that opens the page in a
/// private window of a browser profile of the account's own (<see cref="AccountDir.BrowserProfileDirectory"/>): no one's
/// claude.ai session is in it, so the page asks for that person's email and code.
/// </summary>
public static class LoginBrowser
{
    /// <summary>A Chromium browser: its exe and the switch that opens a private window.</summary>
    public sealed record Browser(string Name, string Exe, string PrivateSwitch);

    /// <summary>The browser the sign-in opens in: the default one when it's Chrome, Edge or Brave, else the first of those
    /// installed. Null when none is (the sign-in then opens in the default browser, as before). Tests replace it.</summary>
    public static Func<Browser?> Find { get; set; } = FindInstalled;

    /// <summary>
    /// Writes <paramref name="account"/>'s opener script and returns its path, for <c>BROWSER</c>; null (with what to say in
    /// <paramref name="note"/>) when no browser it can open privately is installed or the script can't be written.
    /// </summary>
    public static string? Prepare(AccountDir account, out string? note)
    {
        note = null;
        if (Find() is not { } browser)
        {
            // An opener left from before would start a browser that's gone.
            try { File.Delete(account.LoginOpenerPath); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            note = $"No Chrome, Edge or Brave found to open {account.Email}'s sign-in privately: it opens in your default browser, so sign in there as {account.Email}.";
            return null;
        }
        try
        {
            Directory.CreateDirectory(account.Directory);
            var text = Script(browser, account);
            if (!File.Exists(account.LoginOpenerPath) || File.ReadAllText(account.LoginOpenerPath) != text)
                File.WriteAllText(account.LoginOpenerPath, text, new UTF8Encoding(false));
            return account.LoginOpenerPath;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            note = $"Couldn't write {account.LoginOpenerPath} ({ex.Message}): {account.Email}'s sign-in opens in your default browser.";
            return null;
        }
    }

    /// <summary>
    /// The opener: <c>claude</c> runs it with the sign-in URL (already quoted) as <c>%1</c>, and it starts the browser on the
    /// account's own profile in a private window. A profile of its own is a separate browser process, so the window never
    /// joins one already open, and the private window keeps nothing once it's closed.
    /// </summary>
    public static string Script(Browser browser, AccountDir account) =>
        "@echo off\r\n" +
        $"rem Written by MindAttic.Orchestrator for {Cmd(account.Email)}'s own sign-in (M.ORCH-T610); edits are overwritten.\r\n" +
        $"start \"\" \"{browser.Exe}\" --user-data-dir=\"{account.BrowserProfileDirectory}\" --no-first-run --no-default-browser-check " +
        $"--new-window {browser.PrivateSwitch} %1\r\n";

    /// <summary>Text safe inside a cmd comment line.</summary>
    private static string Cmd(string text) => text.Replace("%", "%%").Replace("\r", "").Replace("\n", "");

    private static Browser? FindInstalled()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string[] programs = [.. new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            local,
        }.Where(p => p.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase)];
        (string Name, string ProgIdStart, string RelativeExe, string Switch)[] known =
        [
            ("Chrome", "ChromeHTML", @"Google\Chrome\Application\chrome.exe", "--incognito"),
            ("Edge", "MSEdge", @"Microsoft\Edge\Application\msedge.exe", "--inprivate"),
            ("Brave", "BraveHTML", @"BraveSoftware\Brave-Browser\Application\brave.exe", "--incognito"),
        ];
        string? defaultProgId = null;
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\Shell\Associations\UrlAssociations\https\UserChoice");
            defaultProgId = key?.GetValue("ProgId") as string;
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or IOException or UnauthorizedAccessException) { }
        Browser? Installed((string Name, string ProgIdStart, string RelativeExe, string Switch) b) =>
            programs.Select(p => Path.Combine(p, b.RelativeExe)).FirstOrDefault(File.Exists) is { } exe ? new Browser(b.Name, exe, b.Switch) : null;
        if (defaultProgId is not null && known.FirstOrDefault(b => defaultProgId.StartsWith(b.ProgIdStart, StringComparison.OrdinalIgnoreCase)) is { Name: not null } preferred
            && Installed(preferred) is { } chosen)
            return chosen;
        return known.Select(Installed).FirstOrDefault(b => b is not null);
    }
}
