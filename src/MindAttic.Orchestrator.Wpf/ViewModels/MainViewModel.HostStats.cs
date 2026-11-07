using System.Globalization;
using MindAttic.Orchestrator.Activity;

namespace MindAttic.Orchestrator.Wpf.ViewModels;

public sealed partial class MainViewModel
{
    // ── The menu bar's host readout (M.ORCH-T1499) ─────────────────────────
    // This machine's own C: free space and RAM load, not any login's: read once a refresh (RefreshAsync, every 5s) off
    // the UI thread, alongside the snapshot build, then set here on the root tab only. MainWindow.xaml binds the menu
    // bar to the root tab's DataContext (like OrgStrip), so these two figures show the same regardless of which login
    // tab is selected.

    private double diskFreeGb;
    private double? ramUtilizationPercent;

    /// <summary>Free space on the system drive, two decimals: "16.45 GB free (C:)". Empty before the first read.</summary>
    public string DiskFreeText => diskFreeGb <= 0 ? "" : $"{diskFreeGb.ToString("0.00", CultureInfo.InvariantCulture)} GB free (C:)";

    /// <summary>Physical RAM in use right now, Windows' own reading: "RAM 62%". Empty before the first read, or when
    /// Windows can't say.</summary>
    public string RamUtilizationText => ramUtilizationPercent is { } p ? $"RAM {p.ToString("0", CultureInfo.InvariantCulture)}%" : "";

    private MemoryState? memory;

    /// <summary>The RAM readout while the memory guard (M.ORCH-T1498) is at ok, in the muted style; empty otherwise.</summary>
    public string RamCalmText => memory is { Level: > MemoryLevel.Ok } ? "" : RamUtilizationText;

    /// <summary>The RAM readout and the guard's level while it holds new work (tight), shown in MaWarn; empty otherwise.</summary>
    public string RamWarnText => memory is { Level: MemoryLevel.Tight } ?$"{RamUtilizationText} · holding new work".TrimStart(' ', '·') : "";

    /// <summary>The RAM readout and the guard's level while it sheds subagents or refuses tool calls, shown in MaBad; empty otherwise.</summary>
    public string RamBadText => memory is { Level: >= MemoryLevel.Critical } m
        ? $"{RamUtilizationText} · RAM {m.Key}".TrimStart(' ', '·') : "";

    /// <summary>What the RAM readout's tooltip says: the use, and what the memory guard is doing about it.</summary>
    public string RamToolTip => memory switch
    {
        null => "Physical RAM in use on this machine.",
        { Level: MemoryLevel.Ok } m => $"Physical RAM in use on this machine ({m.Reading.Describe()}). The memory guard holds new work under {m.Lines.TightGb:0.#} GB free.",
        { Level: MemoryLevel.Tight } m => $"{m.Reading.Describe()}: no new subagents, CLIs or test runs start; running work finishes. New work resumes once it's {m.Lines.TightGb + MemoryGuard.ResumeMarginGb:0.#} GB free for a minute.",
        { Level: MemoryLevel.Critical } m => $"{m.Reading.Describe()}: nothing new starts, and the newest /allocate subagent is stopped each minute (its ticket requeued) until it recovers.",
        var m => $"{m.Reading.Describe()}: every agent's tool calls are refused so turns end, until memory recovers. Close what you can.",
    };

    private void SetHostStats(double freeGb, double? ramPercent, MemoryState? memoryNow = null)
    {
        diskFreeGb = freeGb;
        ramUtilizationPercent = ramPercent;
        memory = memoryNow;
        Raise(nameof(DiskFreeText));
        Raise(nameof(RamUtilizationText));
        Raise(nameof(RamCalmText));
        Raise(nameof(RamWarnText));
        Raise(nameof(RamBadText));
        Raise(nameof(RamToolTip));
    }
}
