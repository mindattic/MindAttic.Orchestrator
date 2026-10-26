using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Data;
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
/// <para>The window's own key bindings stand aside too (M.ORCH-T846): while a terminal holds focus only the dashboard's kept
/// chords (<see cref="IsKeptChord"/>) still act; every other KeyBinding on the window (the zoom keys Ctrl+Plus, Ctrl+Minus and
/// Ctrl+0, and Esc) is gated by <see cref="GateBindings"/> so its key reaches the CLI instead.</para>
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

    /// <summary>The chords the dashboard keeps even while a terminal holds focus (M.ORCH-T846): Ctrl+Tab and Ctrl+Shift+Tab
    /// (login tabs), Ctrl+Left/Right (CLI tabs), Ctrl+V and Shift+Insert (paste) and F5 (refresh). Every other key, Ctrl+C and
    /// Ctrl+X among them, belongs to the CLI (Ctrl+C is its interrupt; with a selection, <see cref="TerminalCopy"/> copies
    /// or cuts it instead).</summary>
    internal static bool IsKeptChord(Key key, ModifierKeys modifiers) =>
        (key == Key.F5 && modifiers == ModifierKeys.None)
        || (key == Key.Tab && modifiers is ModifierKeys.Control or (ModifierKeys.Control | ModifierKeys.Shift))
        || (key is Key.Left or Key.Right && modifiers == ModifierKeys.Control)
        || (key == Key.V && modifiers == ModifierKeys.Control)
        || (key == Key.Insert && modifiers == ModifierKeys.Shift);

    /// <summary>Gates every KeyBinding on <paramref name="window"/> that isn't a kept chord (M.ORCH-T846): its command can't
    /// execute while a terminal holds focus, so WPF leaves the key unhandled and it goes on to the CLI. Call once the window's
    /// bindings are all added (the XAML's and the zoom's). A data-bound command is left alone: its binding would put the
    /// ungated command back (none today, as every bound one is a kept chord).</summary>
    public static void GateBindings(Window window, FrameworkElement terminals)
    {
        foreach (var binding in window.InputBindings.OfType<KeyBinding>())
        {
            if (IsKeptChord(binding.Key, binding.Modifiers) || binding.Command is null or GatedCommand
                || BindingOperations.IsDataBound(binding, InputBinding.CommandProperty)) continue;
            binding.Command = new GatedCommand(binding.Command, terminals, window);
        }
    }

    /// <summary>A window key's command that stands aside while a terminal holds focus. A routed one (Esc) is routed from the
    /// window, as its KeyBinding did.</summary>
    private sealed class GatedCommand(ICommand inner, FrameworkElement terminals, IInputElement target) : ICommand
    {
        public event EventHandler? CanExecuteChanged
        {
            add => inner.CanExecuteChanged += value;
            remove => inner.CanExecuteChanged -= value;
        }

        public bool CanExecute(object? parameter) => !HoldsFocus(terminals)
            && (inner is RoutedCommand routed ? routed.CanExecute(parameter, target) : inner.CanExecute(parameter));

        public void Execute(object? parameter)
        {
            if (!CanExecute(parameter)) return;
            if (inner is RoutedCommand routed) routed.Execute(parameter, target);
            else inner.Execute(parameter);
        }
    }

    /// <summary>True while a terminal has the keyboard: the window's own key bindings (Esc) should stand aside. The focused
    /// element is the renderer's HwndHost, inside a tab's RelayTerminal (M.ORCH-T926) or its in-process fallback.</summary>
    public static bool HoldsFocus(FrameworkElement terminals) =>
        terminals.IsVisible && terminals.IsKeyboardFocusWithin && Keyboard.FocusedElement is HwndHost host
        && ViewModels.TerminalPaneViewModel.Of(host) is not null;

    /// <summary>Keys WPF would otherwise act on (scroll, move focus) before the CLI sees them.</summary>
    internal static bool IsForwarded(Key key, ModifierKeys modifiers) =>
        (modifiers & (ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Windows)) == 0
        && key is Key.Left or Key.Right or Key.Up or Key.Down or Key.Home or Key.End or Key.PageUp or Key.PageDown;

    /// <summary>A navigation key as the win32-input-mode events the renderer itself produces (ESC[Vk;Sc;Uc;Kd;Cs;Rc_), down then
    /// up, with ENHANCED_KEY (256: the arrows, Home, End and Page keys are the grey ones) and SHIFT_PRESSED (16) when held.</summary>
    internal static string Win32Key(int vk, uint scanCode, bool shift)
    {
        var state = 256 | (shift ? 16 : 0);
        return $"\u001b[{vk};{scanCode};0;1;{state};1_\u001b[{vk};{scanCode};0;0;{state};1_";
    }

    private static void OnPreviewKeyDown(FrameworkElement terminals, KeyEventArgs e)
    {
        if (e.Handled || !IsForwarded(e.Key, Keyboard.Modifiers) || !HoldsFocus(terminals)) return;
        // A hosted tab (M.ORCH-T926, Phase 0 lesson 3): the key goes straight to the CLI through its relay connection, encoded
        // as the renderer encodes every other key. Posting WM_KEYDOWN to the renderer lost keys and reordered them against
        // typed text.
        if (TerminalPaste.Focused(terminals) is { Relay.Connection: { } connection })
        {
            var key = KeyInterop.VirtualKeyFromKey(e.Key);
            connection.WriteInput(Win32Key(key, MapVirtualKey((uint)key, 0), (Keyboard.Modifiers & ModifierKeys.Shift) != 0));
            e.Handled = true;
            return;
        }
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
