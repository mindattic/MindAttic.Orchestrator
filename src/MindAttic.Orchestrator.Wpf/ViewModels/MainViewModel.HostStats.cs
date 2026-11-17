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
        { Level: MemoryLevel.Tight } m => $"{m.Reading.Describe()}: no new subagents, CLIs or test runs start; running work finishes. New work resumes once it's {m.Lines.TightGb + m.Lines.ResumeMarginGb:0.#} GB free for a minute.",
        { Level: MemoryLevel.Critical } m => $"{m.Reading.Describe()}: nothing new starts, and the newest /allocate subagent is stopped each minute (its ticket requeued) until it recovers.",
        var m => $"{m.Reading.Describe()}: every agent's tool calls are refused so turns end, until memory recovers. Close what you can.",
    };

    private DiskHold? diskHold;

    /// <summary>The menu bar's banner while the disk floor (M.ORCH-T1679/T1692) holds new work, shown in MaBad beside the free-space
    /// readout: which drive and how little is free. Empty while the disk has room or the floor is off.</summary>
    public string DiskHoldText => DiskHoldLine(diskHold);

    internal static string DiskHoldLine(DiskHold? hold) => hold is null ? ""
        : $"Disk full · holding new work ({hold.Drive.Root} {hold.Drive.FreeGb.ToString("0.0", CultureInfo.InvariantCulture)} GB free)";

    /// <summary>What the banner's tooltip says: why nothing new starts and how much space lifts it.</summary>
    public string DiskHoldToolTip => diskHold is { } h
        ? $"{h.Why}: no new subagents, CLIs, tickets or test runs start until then. orchestrator disk-cleanup frees agent temp; the floor is the Disk floor setting in MindAttic Vault's Orchestrator page."
        : "";

    private bool allocatePaused;

    /// <summary>The menu bar's banner while allocation is paused globally (<c>orchestrator settings --allocate false</c>,
    /// M.ORCH-T1491), shown in MaWarn beside the memory guard's readout: every CLI's /allocate stays recorded, but
    /// orchestrator next hands none of them anything. Empty while it's on.</summary>
    public string AllocatePausedText => AllocatePausedLine(allocatePaused);

    internal static string AllocatePausedLine(bool paused) => paused ? MindAttic.Orchestrator.Scheduling.Allocate.PausedGlobally : "";

    /// <summary>What the banner's tooltip says: why nothing is handed out, and how to resume it.</summary>
    public string AllocatePausedToolTip =>
        "The master switch for /allocate is off: the CLIs that typed /allocate keep it, but orchestrator next hands none of them a ticket. " +
        "orchestrator settings --allocate true resumes it, or the Allocate setting in MindAttic Vault's Orchestrator page.";

    // ── Connected members (hub phase 1, M.ORCH-T1640) ──────────────────────
    // On the host, the org members whose own PCs are connected to its gateway with their member keys: read with the rest of
    // the refresh, off the UI thread (ConnectedMembersNow), from the gateway's members-seen.json. Empty on every other machine.

    private volatile IReadOnlyList<string> connectedMembersRead = [];
    private IReadOnlyList<string> connectedMembers = [];

    /// <summary>The members connected to this host now, by email; empty unless this machine is the host. Never throws.</summary>
    internal static IReadOnlyList<string> ConnectedMembersNow(MindAttic.Orchestrator.Settings.OrchestratorSettings? settings)
    {
        try
        {
            if (!MindAttic.Orchestrator.Setup.MachineTopology.Current.IsHost) return [];
            var minutes = settings?.MemberConnectedMinutes ?? MindAttic.Orchestrator.Settings.OrchestratorSettings.DefaultMemberConnectedMinutes;
            return MindAttic.Orchestrator.Term.TermMemberKeys.Connected(DateTime.UtcNow, TimeSpan.FromMinutes(Math.Max(0.1, minutes)));
        }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException or System.Text.Json.JsonException) { return []; }
    }

    /// <summary>The menu bar's chip on the host: "2 members connected"; empty when none is (or this isn't the host).</summary>
    public string ConnectedMembersText => ConnectedMembersLine(connectedMembers);

    internal static string ConnectedMembersLine(IReadOnlyList<string> members) => members.Count switch
    {
        0 => "",
        1 => "1 member connected",
        var n => $"{n} members connected",
    };

    /// <summary>Who they are, for the chip's tooltip.</summary>
    public string ConnectedMembersToolTip => connectedMembers.Count == 0 ? ""
        : $"Connected to this host from their own PCs: {string.Join(", ", connectedMembers)}. They watch their org's shared CLIs, read-only " +
          "(orchestrator machine members lists every invited member; orchestrator machine revoke cuts one off).";

    // ── Consent toasts (hub phase 2, M.ORCH-T1641) ─────────────────────────
    // On a member's own PC connected to the host: every HubConsentPollSeconds (Vault), read with the rest of the refresh, off the
    // UI thread (ConsentsNow), the host's consent requests on this member's CLIs; each new one is a toast naming who asks and the
    // command that answers it. No answer within the host's consent wait leaves them watching. Nothing on any other machine.

    private volatile IReadOnlyList<MindAttic.Orchestrator.Sessions.RemoteConsent> consentsRead = [];
    private readonly HashSet<string> consentsToasted = new(StringComparer.Ordinal);
    private MindAttic.Orchestrator.Sessions.GatewayClient? consentClient;
    private DateTime consentsPolledUtc = DateTime.MinValue;

    /// <summary>The host's consent requests waiting on this member, when a look is due; the last ones read otherwise, and none on
    /// any machine but a member's client. Never throws; bounded to a few seconds.</summary>
    internal IReadOnlyList<MindAttic.Orchestrator.Sessions.RemoteConsent> ConsentsNow(MindAttic.Orchestrator.Settings.OrchestratorSettings? settings)
    {
        try
        {
            if (!MindAttic.Orchestrator.Setup.MachineTopology.Current.IsMemberClient) return [];
            var every = TimeSpan.FromSeconds(Math.Max(1, settings?.HubConsentPollSeconds ?? MindAttic.Orchestrator.Settings.OrchestratorSettings.DefaultHubConsentPollSeconds));
            if (DateTime.UtcNow - consentsPolledUtc < every) return consentsRead;
            consentsPolledUtc = DateTime.UtcNow;
            consentClient ??= MindAttic.Orchestrator.Setup.MachineSetup.HostClient();
            if (consentClient is null) return [];
            using var bounded = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            return consentClient.ConsentsAsync(bounded.Token).GetAwaiter().GetResult() ?? [];
        }
        catch (Exception ex) when (ex is OperationCanceledException or System.IO.IOException or UnauthorizedAccessException or InvalidOperationException
                                       or System.Text.Json.JsonException or System.Net.Http.HttpRequestException) { return []; }
    }

    /// <summary>The toast for a consent request: who asks to type into which CLI, and how to answer.</summary>
    internal static (string Title, string Body) ConsentToast(MindAttic.Orchestrator.Sessions.RemoteConsent c) =>
        ($"{c.Viewer} asks to type into your CLI",
         $"Session {c.Session} on the host. orchestrator machine consent {c.Id} allow (or deny). No answer by {c.DeadlineUtc.ToLocalTime():HH:mm:ss} leaves them watching.");

    private void ShowConsentToasts()
    {
        var now = DateTime.UtcNow;
        foreach (var c in consentsRead)
        {
            if (c.DeadlineUtc <= now || !consentsToasted.Add(c.Id)) continue;
            var (title, body) = ConsentToast(c);
            Toasts.Insert(0, new ToastViewModel(title, body, Palette.Waiting, null) { Lifetime = c.DeadlineUtc - now });
        }
        if (consentsToasted.Count > 512) consentsToasted.Clear();
    }

    private void SetHostStats(double freeGb, double? ramPercent, MemoryState? memoryNow = null, bool allocatePausedNow = false, DiskHold? diskHoldNow = null)
    {
        ShowConsentToasts();
        diskFreeGb = freeGb;
        ramUtilizationPercent = ramPercent;
        memory = memoryNow;
        allocatePaused = allocatePausedNow;
        diskHold = diskHoldNow;
        Raise(nameof(DiskHoldText));
        Raise(nameof(DiskHoldToolTip));
        connectedMembers = connectedMembersRead;
        Raise(nameof(ConnectedMembersText));
        Raise(nameof(ConnectedMembersToolTip));
        Raise(nameof(AllocatePausedText));
        Raise(nameof(DiskFreeText));
        Raise(nameof(RamUtilizationText));
        Raise(nameof(RamCalmText));
        Raise(nameof(RamWarnText));
        Raise(nameof(RamBadText));
        Raise(nameof(RamToolTip));
    }
}
