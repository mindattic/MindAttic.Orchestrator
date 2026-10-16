using System.Windows.Media;

namespace MindAttic.Orchestrator.Wpf.ViewModels;

/// <summary>State colours, chosen to stay distinguishable on the McChrome/McSurface palette. Each is at least 4.5:1 against
/// McChrome, McSurface and the hover and detail-pane greys (WCAG 2.2 AA text contrast), and Quiet, used for indicators
/// and large text only, at least 3:1. Colour never carries meaning on its own: a word or glyph says the same.</summary>
public static class Palette
{
    public static readonly Brush Working = Freeze(0x2F, 0xBF, 0x71);
    public static readonly Brush Waiting = Freeze(0xF5, 0xA5, 0x24);
    public static readonly Brush Tickets = Freeze(0x7A, 0x93, 0xF0);
    public static readonly Brush Quiet = Freeze(0x8A, 0x8C, 0x96);
    public static readonly Brush Over = Freeze(0xFF, 0x7A, 0x7D);

    public static Brush For(RepoState state, bool hasTickets) => state switch
    {
        RepoState.Working => Working,
        RepoState.Waiting => Waiting,
        _ => hasTickets ? Tickets : Quiet,
    };

    private static Brush Freeze(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }
}
