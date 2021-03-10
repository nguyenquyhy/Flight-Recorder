using FlightRecorder.Client.Logics;
using ICSharpCode.SharpZipLib.Zip;
using Microsoft.Win32;
using System.IO;
using System.Text.Json;
using System.Windows;

namespace FlightRecorder.Client
{
    public class DialogLogic : IDialogLogic
    {
        public bool Confirm(string message)
        {
            return MessageBox.Show(message, "Flight Recorder", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
        }

        public void Error(string error)
        {
            MessageBox.Show(error, "Flight Recorder", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        public SavedData Load()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Recorded Flight|*.flightrecorder"
            };

            if (dialog.ShowDialog() == true)
            {
                using var file = dialog.OpenFile();
                using var zipFile = new ZipFile(file);

                foreach (ZipEntry entry in zipFile)
                {
                    if (entry.IsFile && entry.Name == "data.json")
                    {
                        using var stream = zipFile.GetInputStream(entry);

                        var reader = new StreamReader(stream);
                        var dataString = reader.ReadToEnd();

                        return JsonSerializer.Deserialize<SavedData>(dataString);
                    }
                }
            }
            return null;
        }
    }
}
