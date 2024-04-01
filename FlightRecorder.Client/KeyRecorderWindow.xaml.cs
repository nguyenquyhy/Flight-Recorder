using FlightRecorder.Client.Logic;
using FlightRecorder.Client.Logics;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;

namespace FlightRecorder.Client;

/// <summary>
/// Interaction logic for KeyRecorderWindow.xaml
/// </summary>
public partial class KeyRecorderWindow : Window
{
    public Shortcuts Shortcut { get; }
    public ShortcutKey? ShortcutKey { get; private set; }

    public KeyRecorderWindow(Shortcuts shortcut)
    {
        InitializeComponent();
        Shortcut = shortcut;
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        e.Handled = true;
        var keys = new List<string>();
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            keys.Add("Ctrl");
        }
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt))
        {
            keys.Add("Alt");
        }
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            keys.Add("Shift");
        }
        if (!new List<Key> {
                Key.LeftCtrl, Key.RightCtrl,
                Key.LeftAlt, Key.RightAlt,
                Key.LeftShift, Key.RightShift,
                Key.LWin, Key.RWin,
                Key.OemClear,
                Key.Apps
            }.Contains(e.Key))
        {
            keys.Add(e.Key.ToString());
            ShortcutKey = new ShortcutKey(
                Keyboard.Modifiers.HasFlag(ModifierKeys.Control),
                Keyboard.Modifiers.HasFlag(ModifierKeys.Alt),
                Keyboard.Modifiers.HasFlag(ModifierKeys.Shift),
                e.Key.ToString(),
                (uint)KeyInterop.VirtualKeyFromKey(e.Key)
            );
        }
        else
        {
            ShortcutKey = null;
        }
        TextKeyPressed.Text = string.Join(" + ", keys);
    }

    private void Window_KeyUp(object sender, KeyEventArgs e)
    {
        if (ShortcutKey == null)
        {
            TextKeyPressed.Text = "Press an unused key combination";
        }
        ButtonAccept.IsEnabled = ShortcutKey != null;
    }

    private void Button_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }
}
