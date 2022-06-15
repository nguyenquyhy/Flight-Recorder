using System.Threading.Tasks;

namespace FlightRecorder.Client.Logics;

public interface ISettingsLogic
{
    Task<bool> IsShortcutKeysEnabledAsync();
    Task SetShortcutKeysEnabledAsync(bool value);
}
