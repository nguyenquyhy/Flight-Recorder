using ICSharpCode.SharpZipLib.Zip;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace FlightRecorder.Client.Logics;

public class StorageLogic : IStorageLogic
{
    private readonly ILogger<StorageLogic> logger;

    public StorageLogic(ILogger<StorageLogic> logger)
    {
        this.logger = logger;
    }

    /// <param name="filePath">This must not be null or empty</param>
    public async Task SaveAsync(string filePath, SavedData data)
    {
        using (var fileStream = new FileStream(filePath, FileMode.Create))
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

            logger.LogDebug("Saved file into {fileName}", filePath);
        }
    }

    public async Task<SavedData?> LoadAsync(Stream file)
    {
        using (file)
        {
            using var zipFile = new ZipFile(file);

            foreach (ZipEntry entry in zipFile)
            {
                if (entry.IsFile && entry.Name == "data.json")
                {
                    using var stream = zipFile.GetInputStream(entry);

                    var result = await JsonSerializer.DeserializeAsync<SavedData>(stream);

                    logger.LogDebug("Loaded file data from file stream");

                    return result;
                }
            }
        }
        return null;
    }
}
