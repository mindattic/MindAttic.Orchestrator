using System.Globalization;

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

    private void SetHostStats(double freeGb, double? ramPercent)
    {
        diskFreeGb = freeGb;
        ramUtilizationPercent = ramPercent;
        Raise(nameof(DiskFreeText));
        Raise(nameof(RamUtilizationText));
    }
}
