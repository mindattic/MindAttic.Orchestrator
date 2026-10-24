namespace MindAttic.Orchestrator.Wpf.ViewModels;

/// <summary>What a person tab's status icon shows (M.ORCH-T757): idle (a pause icon), busy (a green play arrow that blinks once
/// for every N tokens its login spends) or disabled (a gray icon: past its 5-hour or weekly cutoff, or signed out; on the
/// 'API key' tab, API billing off).</summary>
public enum TabActivity
{
    Idle,
    Busy,
    Disabled,
}

/// <summary>
/// One tab's blinks (M.ORCH-T757): once per <c>N</c> tokens its login spends. Each refresh hands it the login's token count, the
/// one the dashboard already read; every multiple of <c>N</c> crossed since the last refresh queues a blink, and the queue is
/// spread evenly over the next refresh interval, so a login spending hard blinks fast and a slow one now and then. Each tab
/// starts its blinks at its own phase (from its key), so two tabs spending at the same rate still don't blink in unison. Pure
/// logic on a clock it's handed: the view model drives it from one shared timer that runs only while a blink is queued.
/// </summary>
public sealed class TabBlinker(string key)
{
    /// <summary>How long a blink keeps the icon dimmed.</summary>
    public static readonly TimeSpan Flash = TimeSpan.FromMilliseconds(140);

    /// <summary>The shortest gap from one blink to the next, so each one still reads as a blink.</summary>
    public static readonly TimeSpan MinSpacing = TimeSpan.FromMilliseconds(320);

    /// <summary>The span a refresh's blinks are spread over: the dashboard's refresh interval.</summary>
    public static readonly TimeSpan Spread = TimeSpan.FromSeconds(5);

    /// <summary>The most blinks one refresh can queue: as many as fit in <see cref="Spread"/>. Past that the rate is pegged.</summary>
    public static int MaxQueued => (int)(Spread.Ticks / MinSpacing.Ticks);

    /// <summary>This tab's own phase: where in a blink gap its first blink falls, so tabs at one rate never line up.</summary>
    private readonly double phase = PhaseOf(key);

    private long? last;
    private int pending;
    private TimeSpan spacing = Spread;
    private DateTime nextAt, dimUntil;

    /// <summary>True while a blink has the icon dimmed.</summary>
    public bool IsDim { get; private set; }

    /// <summary>Blinks still to show from the last refresh.</summary>
    public int Pending => pending;

    /// <summary>Whether the last count handed in was more than the one before: the login spent tokens since the last refresh.</summary>
    public bool Grew { get; private set; }

    /// <summary>Whether the timer is still needed: a blink queued or one on screen.</summary>
    public bool IsActive => pending > 0 || IsDim;

    /// <summary>A stable 0..1 from <paramref name="key"/> (not string.GetHashCode, which changes every run).</summary>
    internal static double PhaseOf(string key)
    {
        uint h = 2166136261;
        foreach (var c in key) h = (h ^ c) * 16777619;
        return (h % 1000) / 1000d;
    }

    /// <summary>Takes the login's token count now, <paramref name="tokens"/>, and queues a blink for every multiple of
    /// <paramref name="per"/> it crossed since the last count; returns how many. The first count, and one lower than the last (a
    /// new day), only set where counting starts.</summary>
    public int Count(long tokens, long per, DateTime now)
    {
        per = Math.Max(1, per);
        var before = last;
        last = tokens;
        Grew = before is { } b && tokens > b;
        if (before is not { } was || tokens <= was) return 0;
        var crossed = (int)Math.Min(int.MaxValue, tokens / per - was / per);
        if (crossed <= 0) return 0;
        var idle = !IsActive;
        pending = Math.Min(MaxQueued, pending + crossed);
        spacing = TimeSpan.FromTicks(Math.Max(MinSpacing.Ticks, Spread.Ticks / pending));
        if (idle) nextAt = now + TimeSpan.FromTicks((long)(spacing.Ticks * phase));
        return crossed;
    }

    /// <summary>Moves the blink on to <paramref name="now"/>; true when the icon's dimming changed.</summary>
    public bool Tick(DateTime now)
    {
        if (IsDim)
        {
            if (now < dimUntil) return false;
            IsDim = false;
            return true;
        }
        if (pending == 0 || now < nextAt) return false;
        IsDim = true;
        dimUntil = now + Flash;
        pending--;
        nextAt = now + spacing;
        return true;
    }

    /// <summary>Drops any queued blink (the tab went idle or disabled) and forgets nothing else.</summary>
    public void Clear()
    {
        pending = 0;
        IsDim = false;
    }
}
