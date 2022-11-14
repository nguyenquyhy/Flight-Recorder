using FlightRecorder.Client.Logics;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using MessageBox = System.Windows.MessageBox;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;

namespace FlightRecorder.Client
{
    public class DialogLogic : IDialogLogic
    {
        private const string recorderFileFilter = "Recorded Flight|*.fltrec;*.flightrecorder";

        private readonly ILogger<DialogLogic> logger;

        public DialogLogic(ILogger<DialogLogic> logger)
        {
            this.logger = logger;
        }

        public bool Confirm(string message)
        {
            return MessageBox.Show(message, "Flight Recorder", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
        }

        public void Error(string error)
        {
            MessageBox.Show(error, "Flight Recorder", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        /// <returns>Full path of the selected file or null</returns>
        public Task<string?> PickSaveFileAsync()
        {
            var dialog = new SaveFileDialog
            {
                FileName = $"{DateTime.Now:yyyy-MM-dd-HH-mm}.fltrec",
                Filter = recorderFileFilter
            };
            if (dialog.ShowDialog() == true)
            {
                return Task.FromResult<string?>(dialog.FileName);
            }

            return Task.FromResult<string?>(null);
        }

        public async Task<(string filePath, Stream fileStream)?> PickOpenFileAsync()
        {
            var dialog = new OpenFileDialog
            {
                Filter = recorderFileFilter
            };

            if (dialog.ShowDialog() == true)
            {
                return (dialog.FileName, dialog.OpenFile());
            }
            return null;
        }

        public Task<string?> PickSaveFolderAsync()
        {
            var dialog = new FolderBrowserDialog();
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                return Task.FromResult<string?>(dialog.SelectedPath);
            }
            return Task.FromResult<string?>(null);
        }
    }
}
