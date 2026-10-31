using MindAttic.Vault.Paths;
using NUnit.Framework;

/// <summary>
/// Points MindAttic Vault's roaming and local roots at temp folders for the whole test assembly (LAMP, M.ORCH-T1308;
/// M.VALT-LAW-6): no test, nor any process a test starts (they inherit the variables), ever reads or writes the real
/// <c>%APPDATA%\MindAttic</c> or <c>%LOCALAPPDATA%\MindAttic</c>: the settings file, the named keys, their assignments and
/// health, or the log database. In the global namespace so it runs before every fixture in the assembly.
/// </summary>
[SetUpFixture]
public sealed class VaultIsolation
{
    private string? root, roamingBefore, localBefore;

    /// <summary>The temp roaming root every test runs under.</summary>
    public static string RoamingRoot { get; private set; } = "";

    /// <summary>The temp local root every test runs under.</summary>
    public static string LocalRoot { get; private set; } = "";

    [OneTimeSetUp]
    public void PointTheVaultAtTempFolders()
    {
        roamingBefore = Environment.GetEnvironmentVariable(VaultPaths.RoamingRootEnvVar);
        localBefore = Environment.GetEnvironmentVariable(VaultPaths.LocalRootEnvVar);
        root = Path.Combine(Path.GetTempPath(), "orchestrator-tests-vault-" + Guid.NewGuid().ToString("N"));
        RoamingRoot = Path.Combine(root, "Roaming", "MindAttic");
        LocalRoot = Path.Combine(root, "Local", "MindAttic");
        Directory.CreateDirectory(RoamingRoot);
        Directory.CreateDirectory(LocalRoot);
        Environment.SetEnvironmentVariable(VaultPaths.RoamingRootEnvVar, RoamingRoot);
        Environment.SetEnvironmentVariable(VaultPaths.LocalRootEnvVar, LocalRoot);
    }

    [OneTimeTearDown]
    public void PutTheVaultBack()
    {
        Environment.SetEnvironmentVariable(VaultPaths.RoamingRootEnvVar, roamingBefore);
        Environment.SetEnvironmentVariable(VaultPaths.LocalRootEnvVar, localBefore);
        try { if (root is not null && Directory.Exists(root)) Directory.Delete(root, recursive: true); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }
}
