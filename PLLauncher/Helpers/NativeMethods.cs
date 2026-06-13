using System;
using System.Runtime.InteropServices;

namespace PLLauncher.Helpers;

/// <summary>
/// Win32 API declarations for global hotkeys, process control, and system operations.
/// </summary>
public static class NativeMethods
{
    // === Window Messages ===
    public const int WM_HOTKEY = 0x0312;

    // === Hotkey Modifiers ===
    public const int MOD_ALT = 0x0001;
    public const int MOD_CONTROL = 0x0002;
    public const int MOD_SHIFT = 0x0004;
    public const int MOD_WIN = 0x0008;
    public const int MOD_NOREPEAT = 0x4000;

    // === Virtual Key Codes ===
    public const int VK_SPACE = 0x20;
    public const int VK_RETURN = 0x0D;
    public const int VK_ESCAPE = 0x1B;
    public const int VK_TAB = 0x09;
    public const int VK_F1 = 0x70;
    public const int VK_F12 = 0x7B;
    public const int VK_VOLUME_MUTE = 0xAD;
    public const int VK_VOLUME_UP = 0xAF;
    public const int VK_VOLUME_DOWN = 0xAE;

    // === Exit Windows Flags ===
    public const uint EWX_SHUTDOWN = 0x00000001;
    public const uint EWX_REBOOT = 0x00000002;
    public const uint EWX_FORCE = 0x00000004;
    public const uint EWX_POWEROFF = 0x00000008;

    // === Window Subclassing ===
    public const int GWLP_WNDPROC = -4;

    // === Lock WorkStation ===
    [DllImport("user32.dll")]
    public static extern bool LockWorkStation();

    // === Global Hotkey Registration ===
    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    // === Shutdown ===
    [DllImport("user32.dll")]
    public static extern bool ExitWindowsEx(uint uFlags, uint dwReason);

    [DllImport("advapi32.dll", SetLastError = true)]
    public static extern bool AdjustTokenPrivileges(
        IntPtr TokenHandle, bool DisableAllPrivileges,
        ref TOKEN_PRIVILEGES NewState, int BufferLength,
        IntPtr PreviousState, IntPtr ReturnLength);

    [DllImport("advapi32.dll", SetLastError = true)]
    public static extern bool OpenProcessToken(
        IntPtr ProcessHandle, uint DesiredAccess, ref IntPtr TokenHandle);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr GetCurrentProcess();

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool CloseHandle(IntPtr hObject);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, uint dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool TerminateProcess(IntPtr hProcess, uint uExitCode);

    public const uint PROCESS_TERMINATE = 0x0001;
    public const uint SYNCHRONIZE = 0x00100000;

    [DllImport("advapi32.dll", SetLastError = true)]
    public static extern bool LookupPrivilegeValue(
        string? lpSystemName, string lpName, out LUID lpLuid);

    // === Set Suspend State ===
    [DllImport("powrprof.dll", SetLastError = true)]
    public static extern bool SetSuspendState(bool hibernate, bool forceCritical, bool disableWakeEvent);

    // === Prevent Sleep (Anti-Sleep) ===
    public const uint ES_CONTINUOUS = 0x80000000;
    public const uint ES_SYSTEM_REQUIRED = 0x00000001;
    public const uint ES_DISPLAY_REQUIRED = 0x00000002;

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern uint SetThreadExecutionState(uint esFlags);

    // === Mouse Move (Anti-Sleep) ===
    [DllImport("user32.dll")]
    public static extern bool SetCursorPos(int X, int Y);

    [DllImport("user32.dll")]
    public static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    // === Get Last Input Info (user idle detection) ===
    [StructLayout(LayoutKind.Sequential)]
    public struct LASTINPUTINFO
    {
        public uint cbSize;
        public uint dwTime;
    }

    [DllImport("user32.dll")]
    public static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

    // === SendInput ===
    [DllImport("user32.dll")]
    public static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    // === Window Subclassing for WM_HOTKEY ===
    public delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    public static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
    public static extern IntPtr SetWindowLong(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll")]
    public static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    /// <summary>
    /// Sets the window procedure (subclass) - handles both 32-bit and 64-bit.
    /// </summary>
    public static IntPtr SetWindowProc(IntPtr hWnd, IntPtr newWndProc)
    {
        if (IntPtr.Size == 8)
            return SetWindowLongPtr(hWnd, GWLP_WNDPROC, newWndProc);
        else
            return SetWindowLong(hWnd, GWLP_WNDPROC, newWndProc);
    }

    // === Structs ===
    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct LUID
    {
        public uint LowPart;
        public int HighPart;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct TOKEN_PRIVILEGES
    {
        public uint PrivilegeCount;
        public LUID Luid;
        public uint Attributes;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct INPUT
    {
        public uint type;
        public InputUnion u;
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct InputUnion
    {
        [FieldOffset(0)]
        public MOUSEINPUT mi;
        [FieldOffset(0)]
        public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    public const uint INPUT_KEYBOARD = 1;

    /// <summary>
    /// Parses a key combo string like "Alt+Ctrl+Shift+M" into modifier flags and virtual key code.
    /// </summary>
    public static (int modifiers, int vk) ParseKeyCombo(string keyCombo)
    {
        int modifiers = 0;
        int vk = 0;

        var parts = keyCombo.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var part in parts)
        {
            switch (part.ToLowerInvariant())
            {
                case "alt": modifiers |= MOD_ALT; break;
                case "ctrl" or "control": modifiers |= MOD_CONTROL; break;
                case "shift": modifiers |= MOD_SHIFT; break;
                case "win" or "windows": modifiers |= MOD_WIN; break;
                default: vk = ParseVirtualKey(part); break;
            }
        }

        return (modifiers, vk);
    }

    public static int ParseVirtualKey(string keyName)
    {
        return keyName.ToUpperInvariant() switch
        {
            "SPACE" => VK_SPACE,
            "ENTER" or "RETURN" => VK_RETURN,
            "ESC" or "ESCAPE" => VK_ESCAPE,
            "TAB" => VK_TAB,
            var s when s.StartsWith("F") && int.TryParse(s[1..], out var n) && n is >= 1 and <= 12 => VK_F1 + n - 1,
            _ when keyName.Length == 1 => char.ToUpperInvariant(keyName[0]),
            _ => 0
        };
    }

    /// <summary>
    /// Enables the shutdown privilege for the current process.
    /// </summary>
    public static bool EnableShutdownPrivilege()
    {
        var tokenHandle = IntPtr.Zero;

        if (!OpenProcessToken(GetCurrentProcess(), 0x0028, ref tokenHandle))
            return false;

        if (!LookupPrivilegeValue(null, "SeShutdownPrivilege", out var luid))
        {
            CloseHandle(tokenHandle);
            return false;
        }

        var privileges = new TOKEN_PRIVILEGES
        {
            PrivilegeCount = 1,
            Luid = luid,
            Attributes = 2 // SE_PRIVILEGE_ENABLED
        };

        var result = AdjustTokenPrivileges(tokenHandle, false, ref privileges,
            Marshal.SizeOf<TOKEN_PRIVILEGES>(), IntPtr.Zero, IntPtr.Zero);
        CloseHandle(tokenHandle);
        return result;
    }
}
