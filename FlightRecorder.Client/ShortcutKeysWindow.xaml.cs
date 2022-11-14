using FlightRecorder.Client.Logics;
using System.Windows;
using System.Windows.Controls;

namespace FlightRecorder.Client
{
    /// <summary>
    /// Interaction logic for ShortcutKeysWindow.xaml
    /// </summary>
    public partial class ShortcutKeysWindow : Window
    {
        private readonly ISettingsLogic settingsLogic;
        private readonly IDialogLogic dialogLogic;

        public ShortcutKeysWindow(ISettingsLogic settingsLogic, IDialogLogic dialogLogic)
        {
            InitializeComponent();
            this.settingsLogic = settingsLogic;
            this.dialogLogic = dialogLogic;
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
    }
}
