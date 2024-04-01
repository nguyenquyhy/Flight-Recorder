using FlightRecorder.Client.Logic;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FlightRecorder.Client.Logics;

public interface ISettingsLogic
{
    Task<bool> IsShortcutKeysEnabledAsync();
    Task SetShortcutKeysEnabledAsync(bool value);
    Task<Dictionary<Shortcuts, ShortcutKey>?> GetShortcutKeysAsync();
    Task SetShortcutKeysAsync(Dictionary<Shortcuts, ShortcutKey> shortcuts);
    Task<string?> GetDefaultSaveFolderAsync();
    Task SetDefaultSaveFolderAsync(string? folderPath);
}
