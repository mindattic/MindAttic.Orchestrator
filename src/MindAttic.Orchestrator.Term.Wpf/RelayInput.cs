using System.Runtime.InteropServices;
using System.Windows.Input;
using Microsoft.Terminal.Wpf;

namespace MindAttic.Orchestrator.Term.Wpf;

/// <summary>
/// What a viewer of a hosted CLI sends for the keys WPF would otherwise take, and for a paste (M.ORCH-T926, shared with the
/// standalone TermViewer, M.ORCH-T960). WPF's keyboard navigation swallows the arrow, Home, End and Page keys before the renderer
/// sees them, so they go through the connection as the win32-input-mode events the renderer itself produces (Phase 0 lesson 3).
/// A paste goes as typed input: newlines as CR, bracketed when the host says the CLI turned that mode on.
/// </summary>
public static partial class RelayInput
{
    /// <summary>Keys WPF would otherwise act on (scroll, move focus) before the CLI sees them.</summary>
    public static bool IsForwarded(Key key, ModifierKeys modifiers) =>
        (modifiers & (ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Windows)) == 0
        && key is Key.Left or Key.Right or Key.Up or Key.Down or Key.Home or Key.End or Key.PageUp or Key.PageDown;

    /// <summary>A navigation key as the win32-input-mode events the renderer itself produces (ESC[Vk;Sc;Uc;Kd;Cs;Rc_), down then
    /// up, with ENHANCED_KEY (256: the arrows, Home, End and Page keys are the grey ones) and SHIFT_PRESSED (16) when held.</summary>
    public static string Win32Key(int vk, uint scanCode, bool shift)
    {
        var state = 256 | (shift ? 16 : 0);
        return $"\u001b[{vk};{scanCode};0;1;{state};1_\u001b[{vk};{scanCode};0;0;{state};1_";
    }

    /// <summary><paramref name="key"/> as <see cref="Win32Key"/> encodes it.</summary>
    public static string NavigationKey(Key key, bool shift)
    {
        var vk = KeyInterop.VirtualKeyFromKey(key);
        return Win32Key(vk, MapVirtualKey((uint)vk, 0), shift);
    }

    /// <summary>Ctrl+V or Shift+Insert.</summary>
    public static bool IsPasteChord(Key key, ModifierKeys modifiers) =>
        (key == Key.V && modifiers == ModifierKeys.Control) || (key == Key.Insert && modifiers == ModifierKeys.Shift);

    /// <summary>What to send the CLI for pasted <paramref name="text"/>: CRLF and LF as CR, bracketed when asked.</summary>
    public static string PastePayload(string text, bool bracketed)
    {
        if (text.Length == 0) return "";
        text = text.Replace("\r\n", "\r").Replace('\n', '\r');
        return bracketed ? "\u001b[200~" + text + "\u001b[201~" : text;
    }

    [LibraryImport("user32.dll")]
    private static partial uint MapVirtualKey(uint code, uint mapType);
}

/// <summary>The renderer's colour schemes.</summary>
public static class TerminalThemes
{
    /// <summary>Windows Terminal's default Campbell scheme, as the renderer's COLORREFs (0x00BBGGRR).</summary>
    public static readonly TerminalTheme Campbell = new()
    {
        DefaultBackground = Ref(0x0C0C0C),
        DefaultForeground = Ref(0xCCCCCC),
        DefaultSelectionBackground = Ref(0xFFFFFF),
        CursorStyle = CursorStyle.BlinkingBar,
        ColorTable =
        [
            Ref(0x0C0C0C), Ref(0xC50F1F), Ref(0x13A10E), Ref(0xC19C00), Ref(0x0037DA), Ref(0x881798), Ref(0x3A96DD), Ref(0xCCCCCC),
            Ref(0x767676), Ref(0xE74856), Ref(0x16C60C), Ref(0xF9F1A5), Ref(0x3B78FF), Ref(0xB4009E), Ref(0x61D6D6), Ref(0xF2F2F2),
        ],
    };

    /// <summary>"#RRGGBB" as a Win32 COLORREF.</summary>
    public static uint Ref(int rgb) => (uint)(((rgb & 0xFF) << 16) | (rgb & 0xFF00) | ((rgb >> 16) & 0xFF));
}
