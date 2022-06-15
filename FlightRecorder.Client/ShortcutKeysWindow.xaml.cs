using FlightRecorder.Client.Logics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace FlightRecorder.Client
{
    /// <summary>
    /// Interaction logic for ShortcutKeysWindow.xaml
    /// </summary>
    public partial class ShortcutKeysWindow : Window
    {
        private readonly ISettingsLogic settingsLogic;

        public ShortcutKeysWindow(ISettingsLogic settingsLogic)
        {
            InitializeComponent();
            this.settingsLogic = settingsLogic;
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            IsShortcutKeysEnabled.IsChecked = await settingsLogic.IsShortcutKeysEnabledAsync();
        }

        private void CheckBox_Checked(object sender, RoutedEventArgs e)
        {
            settingsLogic.SetShortcutKeysEnabledAsync(true);
        }

        private void CheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            settingsLogic.SetShortcutKeysEnabledAsync(false);
        }
    }
}
