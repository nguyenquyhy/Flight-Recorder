using FlightRecorder.Client.Logics;
using ICSharpCode.SharpZipLib.Zip;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;

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

        public async Task<string> SaveAsync(SavedData data)
        {
            var dialog = new SaveFileDialog
            {
                FileName = $"{DateTime.Now:yyyy-MM-dd-HH-mm}.fltrec",
                Filter = recorderFileFilter
            };
            if (dialog.ShowDialog() == true)
            {
                using (var fileStream = new FileStream(dialog.FileName, FileMode.Create))
                {
                    using var outStream = new ZipOutputStream(fileStream);

                    outStream.SetLevel(9);

                    var entry = new ZipEntry("data.json")
                    {
                        DateTime = DateTime.Now
                    };
                    outStream.PutNextEntry(entry);

                    await JsonSerializer.SerializeAsync(outStream, data);

                    outStream.Finish();
                }

                logger.LogDebug("Saved file into {fileName}", dialog.FileName);

                return Path.GetFileName(dialog.FileName);
            }

            return null;
        }

        public async Task<(string fileName, SavedData data)> LoadAsync()
        {
            var dialog = new OpenFileDialog
            {
                Filter = recorderFileFilter
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

                        var result = await JsonSerializer.DeserializeAsync<SavedData>(stream);

                        logger.LogDebug("Loaded file from {fileName}", dialog.FileName);

                        return (Path.GetFileName(dialog.FileName), result);
                    }
                }
            }
            return (null, null);
        }
    }
}
