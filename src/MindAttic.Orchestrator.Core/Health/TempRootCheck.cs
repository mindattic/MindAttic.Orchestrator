using Microsoft.Win32;
using MindAttic.Orchestrator.Term;

namespace MindAttic.Orchestrator.Health;

/// <summary>
/// The machine probe's <c>temp:root</c> check (M.ORCH-T2219): the TEMP Windows hands a new process of the user
/// (<c>HKCU\Environment</c>), of the machine (<c>HKLM\...\Session Manager\Environment</c>) and of SYSTEM
/// (<c>HKEY_USERS\S-1-5-18\Environment</c>), each degraded when it isn't under the temp root, so a C: temp that every startup
/// would have to correct shows up. Reads only; a value that can't be read (access denied) is degraded with the reason.
/// </summary>
public static class TempRootCheck
{
    /// <summary>The check id.</summary>
    public const string Check = "temp:root";

    /// <summary>Each scope's registry key under its hive.</summary>
    private static readonly (string Scope, RegistryKey Hive, string Key)[] Scopes =
    [
        ("user", Registry.CurrentUser, "Environment"),
        ("machine", Registry.LocalMachine, @"SYSTEM\CurrentControlSet\Control\Session Manager\Environment"),
        ("system", Registry.Users, @"S-1-5-18\Environment"),
    ];

    /// <summary>One result per scope, read from the registry now.</summary>
    public static IReadOnlyList<HealthResult> Results(string? root, DateTimeOffset now) =>
        [.. Scopes.Select(s => Grade(s.Scope, Read(s.Hive, s.Key, out var error), error, root, now))];

    /// <summary>One scope's result: healthy when <paramref name="temp"/> is under <paramref name="root"/>, degraded otherwise or
    /// when it couldn't be read (<paramref name="error"/>). A scope with no TEMP of its own (null, no error) inherits the
    /// machine's, so it is healthy.</summary>
    public static HealthResult Grade(string scope, string? temp, string? error, string? root, DateTimeOffset now)
    {
        var top = TempRoot.Normalize(root);
        if (error is not null)
            return new HealthResult(Check, scope, "system", null, "state", HealthGrade.Degraded, now, Output: "no reading: " + error, ObservedState: "unreadable");
        if (temp is null)
            return new HealthResult(Check, scope, "system", null, "state", HealthGrade.Healthy, now, Output: "not set; inherits the machine's", ObservedState: "inherited");
        return TempRoot.IsUnder(temp, top)
            ? new HealthResult(Check, scope, "system", null, "state", HealthGrade.Healthy, now, ObservedState: temp)
            : new HealthResult(Check, scope, "system", null, "state", HealthGrade.Degraded, now,
                Output: $"TEMP isn't under the temp root {top}", ObservedState: temp);
    }

    /// <summary>The key's TEMP, unexpanded (a user's %USERPROFILE% isn't this process's), or null when it has none.</summary>
    private static string? Read(RegistryKey hive, string key, out string? error)
    {
        error = null;
        try
        {
            using var k = hive.OpenSubKey(key, writable: false);
            return k?.GetValue("TEMP", null, RegistryValueOptions.DoNotExpandEnvironmentNames) as string;
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            error = ex.Message;
            return null;
        }
    }
}
