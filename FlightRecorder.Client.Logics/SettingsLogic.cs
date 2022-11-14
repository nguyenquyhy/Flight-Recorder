using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace FlightRecorder.Client.Logics;

public class FileSettingsLogic : ISettingsLogic
{
    private const string FileName = "settings.json";
    
    private readonly ILogger<FileSettingsLogic> logger;

    public FileSettingsLogic(ILogger<FileSettingsLogic> logger)
    {
        this.logger = logger;
    }
    
    public async Task<bool> IsShortcutKeysEnabledAsync()
    {
        var settings = await LoadAsync();
        return settings.ShortcutKeysEnabled;
    }

    public Task SetShortcutKeysEnabledAsync(bool value)
    {
        return SaveAsync(settings => settings.ShortcutKeysEnabled = value);
    }

    public async Task<string?> GetDefaultSaveFolderAsync()
    {
        var settings = await LoadAsync();
        return settings.DefaultSaveFolder;
    }

    public Task SetDefaultSaveFolderAsync(string? folderPath)
    {
        return SaveAsync(settings => settings.DefaultSaveFolder = folderPath);
    }

    private static SemaphoreSlim sm = new SemaphoreSlim(1);

    private Settings? settings = null;
    
    private async Task<Settings> LoadAsync()
    {
        await sm.WaitAsync();
        try
        {
            return await LoadNoLockAsync();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Cannot read setting");
            return settings ??= new Settings();
        }
        finally
        {
            sm.Release();
        }
    }

    private async Task SaveAsync(Action<Settings> update)
    {
        await sm.WaitAsync();
        try
        {
            var settings = await LoadNoLockAsync();
            update(settings);
            this.settings = settings;

            using var stream = File.Open(FileName, FileMode.Create);
            await JsonSerializer.SerializeAsync(stream, settings);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Cannot write setting");
        }
        finally
        {
            sm.Release();
        }
    }

    private async Task<Settings> LoadNoLockAsync()
    {
        if (!File.Exists(FileName))
        {
            return settings ??= new Settings();
        }
        using var stream = File.OpenRead(FileName);
        return settings ??= await JsonSerializer.DeserializeAsync<Settings>(stream) ?? new Settings();
    }
}

public class Settings
{
    public bool ShortcutKeysEnabled { get; set; } = false;
    public string? DefaultSaveFolder { get; set; } = null;
}