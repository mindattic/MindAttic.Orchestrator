using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace MindAttic.Orchestrator.Wpf;

/// <summary>
/// Keeps keystrokes for the CLI in the selected Terminals tab (M.ORCH-T852). A terminal is a native child window hosted by an
/// <see cref="HwndHost"/>, and WPF routes its keys through the visual tree first: the page's ScrollViewer scrolls on the arrow,
/// Home, End and Page keys and marks them handled, which swallows them before the terminal's window ever sees them (Claude's
/// /status screen couldn't be paged left and right); the window's Escape binding likewise took Esc. While a terminal holds
/// focus, the navigation keys are handled once, here, and posted straight to the terminal's own window; Esc is left alone by
/// the window's binding (<see cref="HoldsFocus"/>). Ctrl or Alt chords are not touched, except Ctrl+Left/Right (M.ORCH-T824): with
/// Ctrl alone, those switch to the previous/next CLI tab of the shown login even while its terminal holds focus, so the CLI gives
/// up its own Ctrl+Left/Right word jump. Ctrl+Shift and Alt variants still go to the CLI.
/// </summary>
internal static partial class TerminalKeyboard
{
    private const uint WmKeyDown = 0x0100;

    public static void Attach(Window window, FrameworkElement terminals) =>
        window.PreviewKeyDown += (_, e) =>
        {
            OnTabSwitch(window, terminals, e);
            OnPreviewKeyDown(terminals, e);
        };

    /// <summary>Ctrl+Left / Ctrl+Right, Ctrl alone (M.ORCH-T824): -1 / +1, the previous / next CLI tab; otherwise 0.</summary>
    internal static int TabStep(Key key, ModifierKeys modifiers) =>
        modifiers != ModifierKeys.Control ? 0 : key switch { Key.Left => -1, Key.Right => +1, _ => 0 };

    /// <summary>Takes Ctrl+Left/Right from a focused terminal before its native window sees them, and steps the shown login's
    /// CLI tabs. Elsewhere the window's own KeyBindings run the same commands (a text box keeps its word jump).</summary>
    private static void OnTabSwitch(Window window, FrameworkElement terminals, KeyEventArgs e)
    {
        if (e.Handled || TabStep(e.Key, Keyboard.Modifiers) is not (var step and not 0) || !HoldsFocus(terminals)) return;
        if (window.DataContext is not ViewModels.MainViewModel { Terminals: { } panes }) return;
        var command = step > 0 ? panes.NextCommand : panes.PreviousCommand;
        if (command.CanExecute(null)) command.Execute(null);
        e.Handled = true;
    }

    /// <summary>True while a terminal has the keyboard: the window's own key bindings (Esc) should stand aside.</summary>
    public static bool HoldsFocus(FrameworkElement terminals) =>
        terminals.IsVisible && terminals.IsKeyboardFocusWithin && Keyboard.FocusedElement is HwndHost;

    /// <summary>Keys WPF would otherwise act on (scroll, move focus) before the CLI sees them.</summary>
    internal static bool IsForwarded(Key key, ModifierKeys modifiers) =>
        (modifiers & (ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Windows)) == 0
        && key is Key.Left or Key.Right or Key.Up or Key.Down or Key.Home or Key.End or Key.PageUp or Key.PageDown;

    private static void OnPreviewKeyDown(FrameworkElement terminals, KeyEventArgs e)
    {
        if (e.Handled || !IsForwarded(e.Key, Keyboard.Modifiers) || !HoldsFocus(terminals)) return;
        var target = GetFocus();
        if (target == IntPtr.Zero) return;
        var vk = KeyInterop.VirtualKeyFromKey(e.Key);
        // lParam: repeat count 1, the scan code, the extended-key bit (the arrows, Home, End and Page keys are the grey
        // ones), and the previous-state bit on a repeat.
        var lParam = (nint)(1 | (MapVirtualKey((uint)vk, 0) << 16) | (1u << 24) | (e.IsRepeat ? 1u << 30 : 0));
        if (PostMessage(target, WmKeyDown, (nint)vk, lParam)) e.Handled = true;
    }

    [LibraryImport("user32.dll")]
    private static partial IntPtr GetFocus();

    [LibraryImport("user32.dll")]
    private static partial uint MapVirtualKey(uint code, uint mapType);

    [LibraryImport("user32.dll", EntryPoint = "PostMessageW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool PostMessage(IntPtr hwnd, uint msg, nint wParam, nint lParam);
}
