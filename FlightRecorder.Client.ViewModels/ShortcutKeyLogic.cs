using FlightRecorder.Client.Logic;
using FlightRecorder.Client.Logics;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace FlightRecorder.Client;

public class ShortcutKeyLogic(
    ILogger<ShortcutKeyLogic> logger,
    IStateMachine stateMachine,
    IThreadLogic threadLogic,
    ISettingsLogic settingsLogic
)
{
    private const int HOTKEY_ID = 9000;
    private const uint MOD_ALT = 0x0001; //ALT
    private const uint MOD_CONTROL = 0x0002; //CTRL
    private const uint MOD_SHIFT = 0x0004; //SHIFT

    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    // https://docs.microsoft.com/en-us/windows/win32/inputdev/virtual-key-codes
    private static Dictionary<Shortcuts, ShortcutKey> defaultShortcuts = new()
    {
        [Shortcuts.Record] = new(true, true, true, "Home", 0x24),
        [Shortcuts.StopRecording] = new(true, true, true, "End", 0x23),
        [Shortcuts.Replay] = new(true, true, true, "R", 'R'),
        [Shortcuts.Pause] = new(true, true, true, ",", 0xBC),
        [Shortcuts.Resume] = new(true, true, true, ".", 0xBE),
        [Shortcuts.StopReplay] = new(true, true, true, "S", 'S'),
        [Shortcuts.SaveToDisk] = new(true, true, true, "C", 'C'),
    };

    public async Task<bool> RegisterAsync(IntPtr handle)
    {
        try
        {
            if (await settingsLogic.IsShortcutKeysEnabledAsync())
            {
                var shortcutKeys = await GetShortcutKeysAsync();
                foreach ((var shortcut, var shortcutKey) in shortcutKeys)
                {
                    uint mod = 0;
                    if (shortcutKey.Ctrl) mod |= MOD_CONTROL;
                    if (shortcutKey.Alt) mod |= MOD_ALT;
                    if (shortcutKey.Shift) mod |= MOD_SHIFT;
                    var key = shortcutKey.VirtualKey;
                    if (key > 0)
                    {
                        RegisterHotKey(handle, (int)shortcut, mod, key);
                    }
                }
                return true;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Cannot register shortcut keys!");
        }
        return false;
    }

    public async Task<Dictionary<Shortcuts, ShortcutKey>> GetShortcutKeysAsync()
    {
        var shortcutKeys = new Dictionary<Shortcuts, ShortcutKey>();
        var customShortcutKeys = await settingsLogic.GetShortcutKeysAsync() ?? [];
        foreach (Shortcuts shortcut in Enum.GetValues(typeof(Shortcuts)))
        {
            if (customShortcutKeys.TryGetValue(shortcut, out var customKey))
            {
                shortcutKeys[shortcut] = customKey;
            }
            else
            {
                shortcutKeys[shortcut] = defaultShortcuts[shortcut];
            }
        }
        return shortcutKeys;
    }

    public bool Unregister(IntPtr handle)
    {
        try
        {
            return UnregisterHotKey(handle, HOTKEY_ID);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Cannot unregister shortcut keys!");
            return false;
        }
    }

    public bool HandleWindowsEvent(int message, IntPtr wParam, IntPtr lParam)
    {
        try
        {
            if (message == 0x312)
            {
                logger.LogDebug("Shortcut key received {id} {key}", wParam, lParam);
                var id = (int)wParam;
                threadLogic.RunInUIThread(async () =>
                {
                    var shortcutKeys = await GetShortcutKeysAsync();
                    try
                    {
                        switch (id)
                        {
                            case (int)Shortcuts.Record:
                                await stateMachine.TransitFromShortcutAsync(StateMachine.Event.Record);
                                break;
                            case (int)Shortcuts.StopRecording:
                                await stateMachine.TransitFromShortcutAsync(StateMachine.Event.Stop);
                                break;
                            case (int)Shortcuts.Replay:
                                await stateMachine.TransitFromShortcutAsync(StateMachine.Event.Replay);
                                break;
                            case (int)Shortcuts.Pause:
                                await stateMachine.TransitFromShortcutAsync(StateMachine.Event.Pause);
                                break;
                            case (int)Shortcuts.Resume:
                                await stateMachine.TransitFromShortcutAsync(StateMachine.Event.Resume);
                                break;
                            case (int)Shortcuts.StopReplay:
                                await stateMachine.TransitFromShortcutAsync(StateMachine.Event.RequestStopping);
                                break;
                            case (int)Shortcuts.SaveToDisk:
                                await stateMachine.TransitFromShortcutAsync(StateMachine.Event.Save);
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Cannot trigger event from shortcut key {id}", id);
                    }
                });
                return true;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Cannot handle shortcut key event!");
        }
        return false;
    }

    public bool IsDuplicate(Shortcuts shortcut, ShortcutKey shortcutKey, Dictionary<Shortcuts, ShortcutKey> shortcutKeys)
        => shortcutKeys.Any(pair => pair.Key != shortcut && pair.Value == shortcutKey);
}
