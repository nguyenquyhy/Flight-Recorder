using FlightRecorder.Client.Logics;
using ICSharpCode.SharpZipLib.Zip;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace FlightRecorder.Client;

public class CrashLogic : ICrashLogic
{
    private const string CrashFileName = "crashed_flight.dat";
    private readonly ILogger<CrashLogic> logger;
    private readonly IRecorderLogic recorderLogic;
    private readonly IDialogLogic dialogLogic;
    private readonly VersionLogic versionLogic;

    public CrashLogic(ILogger<CrashLogic> logger, IRecorderLogic recorderLogic, IDialogLogic dialogLogic, VersionLogic versionLogic)
    {
        this.logger = logger;
        this.recorderLogic = recorderLogic;
        this.dialogLogic = dialogLogic;
        this.versionLogic = versionLogic;
    }

    public async Task LoadDataAsync(StateMachine stateMachine, IReplayLogic replayLogic)
    {
        if (File.Exists(CrashFileName))
        {
            logger.LogInformation("Crash file detected");

            if (!dialogLogic.Confirm("An auto-save from a previous Flight Recorder crash was found. Do you want to load it?"))
            {
                CleanUp();
                return;
            }

            try
            {
                using (var file = File.OpenRead(CrashFileName))
                {
                    using var zipFile = new ZipFile(file);

                    foreach (ZipEntry entry in zipFile)
                    {
                        if (entry.IsFile && entry.Name == "data.json")
                        {
                            var stream = zipFile.GetInputStream(entry);

                            var crashData = await JsonSerializer.DeserializeAsync<SavedData>(stream);

                            if (crashData == null)
                            {
                                logger.LogWarning("Crash data is null.");
                                dialogLogic.Error("Cannot load auto-save flight!");
                                return;
                            }

                            replayLogic.FromData(null, crashData);
                            logger.LogDebug("Loaded crash file");

                            await stateMachine.TransitAsync(StateMachine.Event.RestoreCrashData);

                            break;
                        }
                    }
                }
                CleanUp();
            } 
            catch (ZipException ex)
            {
                logger.LogError(ex, "Cannot load recovery file!");
                dialogLogic.Error("Cannot load auto-save flight!");
            }
        }
    }

    public void SaveData()
    {
        recorderLogic.StopRecording();
        var data = recorderLogic.ToData(versionLogic.GetVersion());
        using var fileStream = new FileStream(CrashFileName, FileMode.Create);
        using var outStream = new ZipOutputStream(fileStream);

        outStream.SetLevel(9);

        var entry = new ZipEntry("data.json") { DateTime = DateTime.Now };
        outStream.PutNextEntry(entry);

        JsonSerializer.Serialize(outStream, data);

        outStream.Finish();
    }

    private void CleanUp()
    {
        try
        {
            File.Delete(CrashFileName);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to delete crash file.");
        }
    }
}
