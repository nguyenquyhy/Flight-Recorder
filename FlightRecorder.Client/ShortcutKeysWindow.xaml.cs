using FlightRecorder.Client.Logics;
using System;
using System.Windows;
using System.Windows.Controls;

namespace FlightRecorder.Client;

/// <summary>
/// Interaction logic for ShortcutKeysWindow.xaml
/// </summary>
public partial class ShortcutKeysWindow : Window
{
    private readonly ISettingsLogic settingsLogic;
    private readonly IDialogLogic dialogLogic;
    private readonly ShortcutKeyLogic shortcutKeyLogic;

    public ShortcutKeysWindow(ISettingsLogic settingsLogic, IDialogLogic dialogLogic, ShortcutKeyLogic shortcutKeyLogic)
    {
        InitializeComponent();
        this.settingsLogic = settingsLogic;
        this.dialogLogic = dialogLogic;
        this.shortcutKeyLogic = shortcutKeyLogic;
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        var isShortcutKeysEnabled = await settingsLogic.IsShortcutKeysEnabledAsync();
        IsShortcutKeysEnabled.IsChecked =
            TextDefaultSaveFolder.IsEnabled =
            ButtonPickDefaultSaveFolder.IsEnabled = isShortcutKeysEnabled;

        var defaultSaveFolder = await settingsLogic.GetDefaultSaveFolderAsync();
        TextDefaultSaveFolder.Text = defaultSaveFolder ?? "";
        ButtonRemoveDefaultSaveFolder.IsEnabled = isShortcutKeysEnabled &&
            !string.IsNullOrEmpty(defaultSaveFolder);

        var shortcutKeys = await shortcutKeyLogic.GetShortcutKeysAsync();
        foreach ((var shortcut, var shortcutKey) in shortcutKeys)
        {
            var textBlock = ShortcutToTextBlock(shortcut);
            if (textBlock != null)
            {
                textBlock.Text = shortcutKey.ToString();
            }
        }
    }

    private async void CheckBox_Checked(object sender, RoutedEventArgs e)
    {
        await settingsLogic.SetShortcutKeysEnabledAsync(true);
        var defaultSaveFolder = await settingsLogic.GetDefaultSaveFolderAsync();
        TextDefaultSaveFolder.IsEnabled =
            ButtonPickDefaultSaveFolder.IsEnabled = true;
        ButtonRemoveDefaultSaveFolder.IsEnabled = !string.IsNullOrEmpty(defaultSaveFolder);
    }

    private async void CheckBox_Unchecked(object sender, RoutedEventArgs e)
    {
        await settingsLogic.SetShortcutKeysEnabledAsync(false);
        TextDefaultSaveFolder.IsEnabled =
            ButtonPickDefaultSaveFolder.IsEnabled =
            ButtonRemoveDefaultSaveFolder.IsEnabled = false;
    }

    private async void ButtonPickDefaultSaveFolder_Click(object sender, RoutedEventArgs e)
    {
        var folder = await dialogLogic.PickSaveFolderAsync();
        if (!string.IsNullOrEmpty(folder))
        {
            await settingsLogic.SetDefaultSaveFolderAsync(folder);
            ButtonRemoveDefaultSaveFolder.IsEnabled = true;
            TextDefaultSaveFolder.Text = folder;
        }
    }

    private async void ButtonRemoveDefaultSaveFolder_Click(object sender, RoutedEventArgs e)
    {
        await settingsLogic.SetDefaultSaveFolderAsync(null);
        ButtonRemoveDefaultSaveFolder.IsEnabled = false;
        TextDefaultSaveFolder.Text = string.Empty;
    }

    private async void ButtonChange_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string tag && Enum.TryParse<Shortcuts>(tag, out var shortcut))
        {
            button.IsEnabled = false;
            try
            {
                var dialog = new KeyRecorderWindow(shortcut);
                dialog.Owner = this;
                if (dialog.ShowDialog() == true && dialog.ShortcutKey != null)
                {
                    var shortcutKeys = await settingsLogic.GetShortcutKeysAsync() ?? [];
                    if (shortcutKeyLogic.IsDuplicate(dialog.Shortcut, dialog.ShortcutKey, shortcutKeys))
                    {
                        MessageBox.Show($"This key combination {dialog.ShortcutKey} is already used for another shortcut!", "Cannot set shortcut");
                    }
                    else
                    {
                        var textBlock = ShortcutToTextBlock(dialog.Shortcut);
                        if (textBlock != null)
                        {
                            textBlock.Text = dialog.ShortcutKey.ToString();
                            shortcutKeys[shortcut] = dialog.ShortcutKey;
                            await settingsLogic.SetShortcutKeysAsync(shortcutKeys);
                        }
                    }
                }
            }
            finally
            {
                button.IsEnabled = true;
            }
        }
    }

    private TextBlock? ShortcutToTextBlock(Shortcuts shortcut) => shortcut switch
    {
        Shortcuts.Record => TextRecord,
        Shortcuts.StopRecording => TextStopRecording,
        Shortcuts.Replay => TextReplay,
        Shortcuts.StopReplay => TextStopReplay,
        Shortcuts.Pause => TextPause,
        Shortcuts.Resume => TextResume,
        Shortcuts.SaveToDisk => TextSaveToDisk,
        _ => null
    };
}
