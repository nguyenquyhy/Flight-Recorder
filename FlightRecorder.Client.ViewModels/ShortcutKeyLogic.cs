using FlightRecorder.Client.Logics;
using Microsoft.Extensions.Logging;
using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace FlightRecorder.Client;

public class ShortcutKeyLogic
{
    private const int HOTKEY_ID = 9000;
    private const uint MOD_ALT = 0x0001; //ALT
    private const uint MOD_CONTROL = 0x0002; //CTRL
    private const uint MOD_SHIFT = 0x0004; //SHIFT
    // https://docs.microsoft.com/en-us/windows/win32/inputdev/virtual-key-codes
    private const uint KeyRecord = 0x24; // Home
    private const uint KeyStopRecording = 0x23; // End
    private const uint KeyReplay = 'R';
    private const uint KeyPause = 0xBC; // ,
    private const uint KeyResume = 0xBE; // .
    private const uint KeyStopReplaying = 'S';
    private const uint KeySave = 'C';

    private readonly ILogger<ShortcutKeyLogic> logger;
    private readonly StateMachine stateMachine;
    private readonly IThreadLogic threadLogic;
    private readonly ISettingsLogic settingsLogic;

    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    public ShortcutKeyLogic(
        ILogger<ShortcutKeyLogic> logger,
        StateMachine stateMachine,
        IThreadLogic threadLogic,
        ISettingsLogic settingsLogic
    )
    {
        this.logger = logger;
        this.stateMachine = stateMachine;
        this.threadLogic = threadLogic;
        this.settingsLogic = settingsLogic;
    }

    public async Task<bool> RegisterAsync(IntPtr handle)
    {
        try
        {
            if (await settingsLogic.IsShortcutKeysEnabledAsync())
            {
                RegisterHotKey(handle, 0, MOD_CONTROL | MOD_ALT | MOD_SHIFT, KeyRecord);
                RegisterHotKey(handle, 1, MOD_CONTROL | MOD_ALT | MOD_SHIFT, KeyStopRecording);
                RegisterHotKey(handle, 2, MOD_CONTROL | MOD_ALT | MOD_SHIFT, KeyReplay);
                RegisterHotKey(handle, 3, MOD_CONTROL | MOD_ALT | MOD_SHIFT, KeyPause);
                RegisterHotKey(handle, 4, MOD_CONTROL | MOD_ALT | MOD_SHIFT, KeyResume);
                RegisterHotKey(handle, 5, MOD_CONTROL | MOD_ALT | MOD_SHIFT, KeyStopReplaying);
                RegisterHotKey(handle, 5, MOD_CONTROL | MOD_ALT | MOD_SHIFT, KeySave);
                return true;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Cannot register shortcut keys!");
        }
        return false;
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
                var key = (uint)((int)lParam >> 16 & 0xFFFF);
                logger.LogDebug("Shortcut key received {key}", key);
                threadLogic.RunInUIThread(async () =>
                {
                    try
                    {
                        switch (key)
                        {
                            case KeyRecord:
                                await stateMachine.TransitFromShortcutAsync(StateMachine.Event.Record);
                                break;
                            case KeyStopRecording:
                                await stateMachine.TransitFromShortcutAsync(StateMachine.Event.Stop);
                                break;
                            case KeyReplay:
                                await stateMachine.TransitFromShortcutAsync(StateMachine.Event.Replay);
                                break;
                            case KeyPause:
                                await stateMachine.TransitFromShortcutAsync(StateMachine.Event.Pause);
                                break;
                            case KeyResume:
                                await stateMachine.TransitFromShortcutAsync(StateMachine.Event.Resume);
                                break;
                            case KeyStopReplaying:
                                await stateMachine.TransitFromShortcutAsync(StateMachine.Event.RequestStopping);
                                break;
                            case KeySave:
                                await stateMachine.TransitFromShortcutAsync(StateMachine.Event.Save);
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Cannot trigger event from shortcut key {key}", key);
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
}
