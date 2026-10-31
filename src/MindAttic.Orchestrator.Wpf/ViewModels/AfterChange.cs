using System.Windows.Threading;

namespace MindAttic.Orchestrator.Wpf.ViewModels;

/// <summary>
/// Runs work once the current change notification has finished (M.ORCH-T1317): a handler of an ObservableCollection's
/// CollectionChanged can't add to or remove from that collection, so work that does (opening a person's replacement Launcher
/// after their last tab closed) is posted to the dispatcher instead of run inline.
/// </summary>
public static class AfterChange
{
    /// <summary>Queues <paramref name="work"/> on the current thread's dispatcher, after the notification being raised now.</summary>
    public static void Post(Action work) =>
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Normal, work);
}
