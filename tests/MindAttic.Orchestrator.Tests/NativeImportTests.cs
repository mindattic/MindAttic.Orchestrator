using System.Reflection;
using System.Runtime.InteropServices;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>
/// M.ORCH-T1315: a [LibraryImport] binds its exact name (no A/W probing), so a bare "MapVirtualKey" threw EntryPointNotFoundException on
/// every terminal key. These call the fixed imports for real, and check every native import in the orchestrator's assemblies names an
/// export its DLL really has.
/// </summary>
[TestFixture]
public sealed class NativeImportTests
{
    [Test]
    public void TerminalKeyboard_MapVirtualKey_resolves_and_maps_A_to_its_scan_code() =>
        Assert.That(MindAttic.Orchestrator.Wpf.TerminalKeyboard.MapVirtualKey(0x41, 0), Is.EqualTo(0x1Eu)); // 'A' -> scan code 0x1E

    [Test]
    public void RelayInput_MapVirtualKey_resolves_and_maps_A_to_its_scan_code() =>
        Assert.That(MindAttic.Orchestrator.Term.Wpf.RelayInput.MapVirtualKey(0x41, 0), Is.EqualTo(0x1Eu));

    private static IEnumerable<TestCaseData> Imports()
    {
        var assemblies = new[]
        {
            typeof(ProcessPath).Assembly,                                    // Core
            typeof(MindAttic.Orchestrator.Wpf.TerminalKeyboard).Assembly,    // Wpf
            typeof(MindAttic.Orchestrator.Term.Wpf.RelayTerminal).Assembly,  // Term.Wpf
        };
        foreach (var asm in assemblies)
        foreach (var type in asm.GetTypes())
        foreach (var m in type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
        {
            // LibraryImport generates a [DllImport] stub; DllImport ones carry the attribute directly.
            var di = m.GetCustomAttribute<DllImportAttribute>();
            if (di is null) continue;
            var entry = di.EntryPoint ?? m.Name;
            yield return new TestCaseData(di.Value, entry, di.ExactSpelling).SetName($"{type.Name}.{m.Name} -> {di.Value}!{entry}");
        }
    }

    [TestCaseSource(nameof(Imports))]
    public void Every_native_import_names_a_real_export(string dll, string entry, bool exactSpelling)
    {
        if (!NativeLibrary.TryLoad(dll, typeof(NativeImportTests).Assembly, DllImportSearchPath.System32, out var lib))
            Assert.Ignore($"{dll} isn't on this machine");
        try
        {
            var found = NativeLibrary.TryGetExport(lib, entry, out _)
                        || (!exactSpelling && (NativeLibrary.TryGetExport(lib, entry + "W", out _) || NativeLibrary.TryGetExport(lib, entry + "A", out _)));
            Assert.That(found, Is.True, $"{dll} has no export {entry}{(exactSpelling ? " (exact spelling, so no A/W suffix is tried)" : "")}");
        }
        finally { NativeLibrary.Free(lib); }
    }
}
