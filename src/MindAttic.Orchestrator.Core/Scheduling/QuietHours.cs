using System.Globalization;

namespace MindAttic.Orchestrator.Scheduling;

/// <summary>Local-time windows ("22:00-07:00", "12:00-13:00") when auto-dispatch pauses; a window past midnight wraps.</summary>
public static class QuietHours
{
    public static bool TryParse(string text, out (TimeOnly From, TimeOnly To) window)
    {
        window = default;
        var parts = text.Split('-', 2, StringSplitOptions.TrimEntries);
        if (parts.Length != 2) return false;
        if (!TimeOnly.TryParse(parts[0], CultureInfo.InvariantCulture, out var from) ||
            !TimeOnly.TryParse(parts[1], CultureInfo.InvariantCulture, out var to) || from == to) return false;
        window = (from, to);
        return true;
    }

    /// <summary>The window <paramref name="localNow"/> falls in, or null. Unreadable windows are ignored.</summary>
    public static string? Active(IEnumerable<string> windows, DateTime localNow)
    {
        var now = TimeOnly.FromDateTime(localNow);
        foreach (var w in windows)
        {
            if (!TryParse(w, out var x)) continue;
            var inside = x.From < x.To ? now >= x.From && now < x.To : now >= x.From || now < x.To;
            if (inside) return w;
        }
        return null;
    }
}
