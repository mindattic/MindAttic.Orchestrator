using MindAttic.Orchestrator.Term;

namespace MindAttic.Orchestrator.TermHost;

/// <summary>
/// Who may type and who owns the size when several viewers share one session (M.ORCH-T930, spike section 3, "Resize ownership
/// with several viewers"). Pure bookkeeping, no I/O: the session calls it under its lock and acts on what it returns.
/// <list type="bullet">
/// <item>Input from interactive viewers is merged (tmux semantics); from read-only viewers it is dropped.</item>
/// <item>The size belongs to the interactive viewer that last sent input or TakeSize. The first interactive viewer to attach
/// takes it while nobody holds it. Read-only viewers never do.</item>
/// <item>Each viewer's Resize is kept as the size it would show; it applies at once only from the owner, and the rest's
/// apply when they take the size.</item>
/// <item>When the owner leaves, the interactive viewer that was active most recently takes over.</item>
/// <item>Input from two viewers within <see cref="TermProtocol.TypingWindow"/> of each other is a collision: both are told.</item>
/// </list>
/// </summary>
internal sealed class ViewerPolicy
{
    private sealed class Entry
    {
        public bool Interactive;
        public ushort Cols, Rows;
        public DateTime LastInput = DateTime.MinValue;
        public long Active;
    }

    private readonly Dictionary<long, Entry> viewers = [];
    private long clock;

    /// <summary>The largest size a viewer may ask for (bughunt 2026-10-27): past it the ConPTY's short COORD wraps negative and
    /// the emulator is asked for over a billion cells.</summary>
    public const ushort MaxCols = 1000, MaxRows = 500;

    /// <summary>A viewer's wanted size, capped at <see cref="MaxCols"/> x <see cref="MaxRows"/>; 0 stays 0 (no size).</summary>
    public static (ushort Cols, ushort Rows) Clamp(ushort cols, ushort rows) => (Math.Min(cols, MaxCols), Math.Min(rows, MaxRows));

    /// <summary>The viewer whose size applies; null while no interactive viewer is attached.</summary>
    public long? Owner { get; private set; }

    public int Count => viewers.Count;

    /// <summary>The size the owner wants (its last Resize, else its Hello); null without an owner.</summary>
    public (ushort Cols, ushort Rows)? OwnerSize =>
        Owner is { } id && viewers.TryGetValue(id, out var e) && e.Cols > 0 && e.Rows > 0 ? (e.Cols, e.Rows) : null;

    public bool IsInteractive(long id) => viewers.TryGetValue(id, out var e) && e.Interactive;

    /// <summary>Adds a viewer with the size its Hello asked for. True when it took the size (nobody held it). A 0x0 Hello (the
    /// gateway's watcher, even at protocol 1) never takes it.</summary>
    public bool Join(long id, bool interactive, ushort cols, ushort rows)
    {
        (cols, rows) = Clamp(cols, rows);
        viewers[id] = new Entry { Interactive = interactive, Cols = cols, Rows = rows, Active = ++clock };
        if (Owner is not null || !interactive || cols == 0 || rows == 0) return false;
        Owner = id;
        return true;
    }

    /// <summary>Removes a viewer. True when it owned the size: <see cref="Owner"/> is then the most recently active interactive
    /// viewer left that has told its size, or null.</summary>
    public bool Leave(long id)
    {
        if (!viewers.Remove(id) || Owner != id) return false;
        Owner = viewers.Where(v => v.Value.Interactive && v.Value.Cols > 0 && v.Value.Rows > 0).OrderByDescending(v => v.Value.Active).Select(v => (long?)v.Key).FirstOrDefault();
        return true;
    }

    /// <summary>What to do with one Input frame.</summary>
    /// <param name="Accept">Write it to the CLI (false: a read-only or unknown viewer's, dropped).</param>
    /// <param name="OwnerChanged">The sender took the size: apply <see cref="OwnerSize"/> and tell every viewer.</param>
    /// <param name="Colliding">The viewers (the sender among them) that sent input within the typing window of each other.</param>
    public readonly record struct InputVerdict(bool Accept, bool OwnerChanged, IReadOnlyList<long> Colliding);

    /// <summary>One Input frame from <paramref name="id"/> at <paramref name="now"/>.</summary>
    public InputVerdict Input(long id, DateTime now)
    {
        if (!viewers.TryGetValue(id, out var sender) || !sender.Interactive) return new(false, false, []);
        var others = viewers.Where(v => v.Key != id && v.Value.Interactive && now - v.Value.LastInput <= TermProtocol.TypingWindow)
            .Select(v => v.Key).ToList();
        sender.LastInput = now;
        sender.Active = ++clock;
        var changed = Owner != id;
        Owner = id;
        if (others.Count > 0) others.Insert(0, id);
        return new(true, changed, others);
    }

    /// <summary>An explicit TakeSize from <paramref name="id"/>. True when the owner changed; a read-only viewer never takes it.</summary>
    public bool TakeSize(long id)
    {
        if (!viewers.TryGetValue(id, out var e) || !e.Interactive) return false;
        e.Active = ++clock;
        if (Owner == id) return false;
        Owner = id;
        return true;
    }

    /// <summary>A Resize from <paramref name="id"/>: kept as its wanted size. True when it should apply now (it owns the size).</summary>
    public bool Resize(long id, ushort cols, ushort rows)
    {
        if (cols == 0 || rows == 0 || !viewers.TryGetValue(id, out var e) || !e.Interactive) return false;
        (e.Cols, e.Rows) = Clamp(cols, rows);
        return Owner == id;
    }
}
