using System.Windows;

namespace FlightRecorder.Client
{
    /// <summary>
    /// Interaction logic for AircraftNameDialog.xaml
    /// </summary>
    public partial class AircraftNameDialog : Window
    {
        private readonly MainViewModel viewModel;

        public AircraftNameDialog(MainViewModel viewModel)
        {
            InitializeComponent();

            DataContext = viewModel;
            this.viewModel = viewModel;

            TextName.Text = viewModel.ReplayAircraftTitle;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            TextName.Focus();
        }

        private void ButtonSet_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        private void ButtonCurrent_Click(object sender, RoutedEventArgs e)
        {
            TextName.Text = viewModel.CurrentAircraftTitle;
        }

        private void ButtonRecorded_Click(object sender, RoutedEventArgs e)
        {
            TextName.Text = viewModel.AircraftTitle;
        }

        private void TextName_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            ButtonSet.IsEnabled = !string.IsNullOrWhiteSpace(TextName.Text);
        }
    }
}
