using System.ComponentModel;
using System.Text.Json;

namespace MindAttic.Orchestrator.Reminders;

/// <summary>What <c>psst ping</c> says: whether Psst runs, whether it can send a text, and the owner's number (its own
/// configured recipient, where a reminder with no <c>--to</c> goes). <see cref="Problem"/> says what's wrong, if anything.</summary>
public sealed record PsstStatus(bool Installed, bool Configured, string? OwnerNumber, string? Problem);

/// <summary>
/// Texts through MindAttic.Psst's own CLI (<c>psst sms &lt;to&gt; &lt;text&gt;</c>), so its transport, contact book and
/// configuration chain (Vault files, settings.json, environment) stay Psst's alone; nothing here reads its secrets.
/// </summary>
public sealed class PsstClient(IProcessRunner runner, string exe = "psst")
{
    public static readonly TimeSpan PingTimeout = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan SendTimeout = TimeSpan.FromMinutes(2);

    /// <summary>Psst's contact book, for checking a <c>--to</c> name when the reminder is made rather than at its time.</summary>
    public static string DefaultContactsPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MindAttic", "Psst", "contacts.json");

    public PsstStatus Probe()
    {
        ProcessResult r;
        try { r = runner.Run(exe, ["ping"], PingTimeout); }
        catch (Win32Exception) { return new(false, false, null, "Psst isn't installed (no psst.exe on the PATH)"); }
        if (r.TimedOut) return new(true, false, null, $"psst ping didn't answer in {PingTimeout.TotalSeconds:0}s");
        if (r.ExitCode != 0) return new(true, false, null, $"psst ping failed (exit {r.ExitCode}): {FirstLine(r)}");
        return Parse(r.Output);
    }

    /// <summary>Reads <c>psst ping</c>'s "Email-to-SMS:" and "Recipient #:" lines.</summary>
    internal static PsstStatus Parse(string pingOutput)
    {
        var transport = Line(pingOutput, "Email-to-SMS:");
        var configured = transport is not null && !transport.StartsWith("not configured", StringComparison.OrdinalIgnoreCase);
        var owner = Line(pingOutput, "Recipient #:");
        if (owner is null || owner.StartsWith('<')) owner = null;
        return new(true, configured, owner,
            configured ? null : "Psst isn't configured to send texts (run psst ping to see what's missing)");
    }

    /// <summary>Sends <paramref name="text"/> to <paramref name="to"/>; Sent is psst's exit code 0, Detail what it said.</summary>
    public (bool Sent, string Detail) Send(string to, string text)
    {
        ProcessResult r;
        try { r = runner.Run(exe, ["sms", to, text], SendTimeout); }
        catch (Win32Exception) { return (false, "Psst isn't installed (no psst.exe on the PATH)"); }
        if (r.TimedOut) return (false, $"psst sms didn't finish in {SendTimeout.TotalMinutes:0} minutes");
        return (r.ExitCode == 0, FirstLine(r));
    }

    /// <summary>True when Psst would take <paramref name="to"/>: a US number (10 digits, optionally +1, no letters) or a name
    /// in its contact book (any case). Psst refuses anything else, so a typo is caught when the reminder is made.</summary>
    public static bool Addressable(string to, string? contactsPath = null)
    {
        if (!to.Any(char.IsLetter))
        {
            var digits = new string([.. to.Where(char.IsDigit)]);
            if (digits.Length == 10 || (digits.Length == 11 && digits[0] == '1')) return true;
        }
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(contactsPath ?? DefaultContactsPath));
            return doc.RootElement.TryGetProperty("contacts", out var list) && list.ValueKind == JsonValueKind.Array &&
                list.EnumerateArray().Any(c => c.TryGetProperty("name", out var n) && string.Equals(n.GetString(), to.Trim(), StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { return false; }
    }

    private static string? Line(string output, string label) => output.Split('\n')
        .Select(l => l.Trim())
        .FirstOrDefault(l => l.StartsWith(label, StringComparison.OrdinalIgnoreCase))?[label.Length..].Trim();

    private static string FirstLine(ProcessResult r)
    {
        var text = (r.ExitCode == 0 ? r.Output : (r.Error.Trim().Length > 0 ? r.Error : r.Output)).Trim();
        return text.Split('\n').Select(l => l.Trim()).FirstOrDefault(l => l.Length > 0) ?? (r.ExitCode == 0 ? "sent" : $"exit {r.ExitCode}");
    }
}
