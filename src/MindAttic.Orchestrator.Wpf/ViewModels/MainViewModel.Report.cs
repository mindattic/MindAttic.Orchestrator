using System.IO;
using System.Windows.Input;
using MindAttic.Components.Wpf.Mvvm;
using MindAttic.Orchestrator.Budget;
using MindAttic.Orchestrator.Sessions;
using MindAttic.Orchestrator.Settings;

namespace MindAttic.Orchestrator.Wpf.ViewModels;

/// <summary>The Accounts view's Report button (M.ORCH-T685): the usage report <c>orchestrator report</c> prints, written as an HTML
/// page and a Markdown file to share, the page opened in the browser.</summary>
public sealed partial class MainViewModel
{
    private bool reporting;
    private ICommand? usageReportCommand;

    /// <summary>Builds the usage report from the last refresh and writes it (<see cref="WriteUsageReport"/>), saying where in the
    /// Accounts view's message line.</summary>
    public ICommand UsageReportCommand => usageReportCommand ??= new RelayCommand(async () =>
    {
        if (reporting) return;
        if (Root.full is not { } snapshot)
        {
            AccountsMessage = "The usage report needs a first refresh: try again in a moment.";
            return;
        }
        reporting = true;
        CommandManager.InvalidateRequerySuggested();
        AccountsMessage = "Writing the usage report…";
        try
        {
            var folder = UsageReport.DefaultFolder(store.Directory);
            var dirs = OwnLoginDirs();
            var path = await Task.Run(() =>
            {
                OrchestratorSettings settings;
                try { settings = store.LoadSettings(); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException) { settings = new OrchestratorSettings(); }
                var now = DateTime.UtcNow;
                var report = UsageReport.Gather(aggregator.Db, snapshot, dirs, settings, now, null, ReportedUsage.Load(now), ReportedUsage.DefaultDb,
                    OutsideUseWatch.Stats(OutsideUseWatch.Load(store), settings), WeeklyAllowances.Active(store, now));
                return WriteUsageReport(report, folder);
            });
            AccountsMessage = $"Usage report written to {path} (and its Markdown beside it).";
            Opener.Open(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Data.Common.DbException or ArgumentException)
        {
            AccountsMessage = $"Couldn't write the usage report: {ex.Message}";
        }
        finally
        {
            reporting = false;
            CommandManager.InvalidateRequerySuggested();
        }
    }, () => !reporting);

    /// <summary>Writes <paramref name="report"/> into <paramref name="folder"/> as an HTML page and a Markdown file of the same
    /// name (<see cref="UsageReport.FileName"/>); returns the page's path.</summary>
    internal static string WriteUsageReport(UsageReport report, string folder)
    {
        var html = Path.Combine(folder, UsageReport.FileName(report.GeneratedUtc, ".html"));
        report.WriteFile(html);
        report.WriteFile(Path.ChangeExtension(html, ".md"));
        return html;
    }
}
