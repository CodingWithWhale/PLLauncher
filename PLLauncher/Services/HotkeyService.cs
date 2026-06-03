using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using PLLauncher.Helpers;
using PLLauncher.Models;

namespace PLLauncher.Services;

public class HotkeyService : IDisposable
{
    private readonly Dictionary<int, KeybindItem> _registeredHotkeys = new();
    private int _nextHotkeyId = 1;
    private IntPtr _windowHandle;
    private bool _isInitialized;

    // WNDPROC subclass fields - MUST stay alive to prevent GC of the delegate
    private IntPtr _originalWndProc;
    private NativeMethods.WndProcDelegate? _subclassWndProc;

    // Last registration error for UI feedback
    private string _lastError = string.Empty;
    public string LastError => _lastError;

    public event EventHandler<KeybindItem>? HotkeyPressed;

    public void Initialize(IntPtr windowHandle)
    {
        if (_isInitialized && windowHandle == _windowHandle) return;
        _windowHandle = windowHandle;
        _isInitialized = true;
        Console.WriteLine($"[HotkeyService] Initialized with HWND={windowHandle}");
    }

    /// <summary>
    /// Subclass the main window to intercept WM_HOTKEY messages.
    /// Must be called from the UI thread after the window is opened.
    /// </summary>
    public void SubclassWindow(IntPtr windowHandle)
    {
        if (_subclassWndProc != null) return; // Already subclassed

        try
        {
            _subclassWndProc = WndProc;
            var wndProcPtr = Marshal.GetFunctionPointerForDelegate(_subclassWndProc);
            _originalWndProc = NativeMethods.SetWindowProc(windowHandle, wndProcPtr);
            Console.WriteLine($"[HotkeyService] Window subclassed. Original WNDPROC={_originalWndProc}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[HotkeyService] Failed to subclass window: {ex.Message}");
        }
    }

    private IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == NativeMethods.WM_HOTKEY)
        {
            try
            {
                ProcessHotkeyMessage(wParam.ToInt32());
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[HotkeyService] Error processing hotkey: {ex.Message}");
            }
        }
        return NativeMethods.CallWindowProc(_originalWndProc, hWnd, msg, wParam, lParam);
    }

    public bool RegisterHotkey(KeybindItem keybind)
    {
        _lastError = string.Empty;

        if (string.IsNullOrEmpty(keybind.KeyCombo))
        {
            _lastError = "No key combination specified.";
            return false;
        }

        if (!_isInitialized)
        {
            _lastError = "Hotkey service not initialized yet. Keybind will be registered on next app start.";
            Console.WriteLine("[HotkeyService] Cannot register - not initialized yet.");
            return false;
        }

        if (IsConflict(keybind.KeyCombo, keybind.Id))
        {
            _lastError = "This key combination is already registered.";
            return false;
        }

        var (modifiers, vk) = NativeMethods.ParseKeyCombo(keybind.KeyCombo);
        if (vk == 0 && modifiers == 0)
        {
            _lastError = "Invalid key combination - could not parse.";
            return false;
        }

        // Windows RegisterHotKey requires at least one modifier (Alt, Ctrl, Shift, or Win)
        // for global hotkeys. Without a modifier, registration will fail.
        if (modifiers == 0)
        {
            _lastError = "Global hotkeys require at least one modifier key (Alt, Ctrl, Shift, or Win). Please add a modifier.";
            Console.WriteLine($"[HotkeyService] No modifier in combo '{keybind.KeyCombo}'. Global hotkeys need at least one modifier.");
            return false;
        }

        var hotkeyId = _nextHotkeyId++;
        try
        {
            if (NativeMethods.RegisterHotKey(_windowHandle, hotkeyId,
                (uint)(modifiers | NativeMethods.MOD_NOREPEAT), (uint)vk))
            {
                _registeredHotkeys[hotkeyId] = keybind;
                keybind.VirtualKeys = new[] { vk };
                keybind.Modifiers = modifiers;
                Console.WriteLine($"[HotkeyService] Registered hotkey: {keybind.KeyCombo} (ID={hotkeyId})");
                return true;
            }
            else
            {
                var err = Marshal.GetLastWin32Error();
                _lastError = err switch
                {
                    1409 => "This key combination is already in use by another application.",
                    1400 => "Invalid window handle. Try restarting the app.",
                    _ => $"Failed to register hotkey (Windows error {err}). Another app may be using this combination."
                };
                Console.WriteLine($"[HotkeyService] RegisterHotKey failed for {keybind.KeyCombo}. Win32 error={err}");
            }
        }
        catch (Exception ex)
        {
            _lastError = $"Registration error: {ex.Message}";
            Console.WriteLine($"[HotkeyService] RegisterHotKey exception: {ex.Message}");
        }
        return false;
    }

    public bool UnregisterHotkey(KeybindItem keybind)
    {
        // Fix: Don't modify collection during iteration - find key first, then remove
        int? keyToRemove = null;
        foreach (var kvp in _registeredHotkeys)
        {
            if (kvp.Value.Id == keybind.Id)
            {
                keyToRemove = kvp.Key;
                break;
            }
        }

        if (keyToRemove.HasValue)
        {
            try { NativeMethods.UnregisterHotKey(_windowHandle, keyToRemove.Value); } catch { }
            _registeredHotkeys.Remove(keyToRemove.Value);
            return true;
        }
        return false;
    }

    public void ProcessHotkeyMessage(int hotkeyId)
    {
        if (_registeredHotkeys.TryGetValue(hotkeyId, out var keybind) && keybind.IsEnabled)
        {
            HotkeyPressed?.Invoke(this, keybind);
            ExecuteKeybindAction(keybind);
        }
    }

    private void ExecuteKeybindAction(KeybindItem keybind)
    {
        try
        {
            switch (keybind.ActionType)
            {
                case KeybindActionType.OpenApp:
                    if (!string.IsNullOrEmpty(keybind.ActionTarget))
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                            { FileName = keybind.ActionTarget, UseShellExecute = true });
                    break;
                case KeybindActionType.RunCommand:
                    if (!string.IsNullOrEmpty(keybind.ActionTarget))
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                            { FileName = "cmd.exe", Arguments = $"/c {keybind.ActionTarget}",
                              UseShellExecute = false, CreateNoWindow = true });
                    break;
                case KeybindActionType.SystemAction:
                    ExecuteSystemAction(keybind.ActionTarget);
                    break;
                case KeybindActionType.OpenUrl:
                    if (!string.IsNullOrEmpty(keybind.ActionTarget))
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                            { FileName = keybind.ActionTarget, UseShellExecute = true });
                    break;
                case KeybindActionType.OpenFolder:
                    if (!string.IsNullOrEmpty(keybind.ActionTarget))
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                            { FileName = "explorer.exe", Arguments = keybind.ActionTarget, UseShellExecute = true });
                    break;
            }
        }
        catch (Exception ex) { Console.WriteLine($"[HotkeyService] Action failed: {ex.Message}"); }
    }

    private void ExecuteSystemAction(string action)
    {
        switch (action.ToLowerInvariant())
        {
            case "mute" or "volumemute": SendKey(NativeMethods.VK_VOLUME_MUTE); break;
            case "volumeup": SendKey(NativeMethods.VK_VOLUME_UP); break;
            case "volumedown": SendKey(NativeMethods.VK_VOLUME_DOWN); break;
            case "lockpc": NativeMethods.LockWorkStation(); break;
            case "sleep": NativeMethods.SetSuspendState(false, false, false); break;
            case "hibernate": NativeMethods.SetSuspendState(true, false, false); break;
        }
    }

    private void SendKey(int vk)
    {
        var inputs = new NativeMethods.INPUT[2];
        inputs[0].type = NativeMethods.INPUT_KEYBOARD;
        inputs[0].u.ki.wVk = (ushort)vk;
        inputs[1].type = NativeMethods.INPUT_KEYBOARD;
        inputs[1].u.ki.wVk = (ushort)vk;
        inputs[1].u.ki.dwFlags = 0x0002;
        NativeMethods.SendInput(2, inputs, Marshal.SizeOf(typeof(NativeMethods.INPUT)));
    }

    public bool IsConflict(string keyCombo, string? excludeId = null)
        => _registeredHotkeys.Values.Any(k => k.Id != excludeId &&
            k.KeyCombo.Equals(keyCombo, StringComparison.OrdinalIgnoreCase));

    public void RegisterAll(IEnumerable<KeybindItem> keybinds)
    {
        if (!_isInitialized) return;
        foreach (var kb in keybinds.Where(k => k.IsEnabled))
            RegisterHotkey(kb);
    }

    public void UnregisterAll()
    {
        foreach (var kvp in _registeredHotkeys.ToList())
        { try { NativeMethods.UnregisterHotKey(_windowHandle, kvp.Key); } catch { } }
        _registeredHotkeys.Clear();
    }

    public void Dispose()
    {
        UnregisterAll();
        GC.SuppressFinalize(this);
    }
}
