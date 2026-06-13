using System;
using System.IO;
using System.Runtime.InteropServices;
using PLLauncher.Services;

namespace PLLauncher.Helpers;

public static class ToastHelper
{
    private static uint _uid;
    private static readonly object Lock = new();

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern bool Shell_NotifyIconW(uint cmd, ref NOTIFYICONDATAW data);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr LoadImage(IntPtr hInst, string lpszName, uint uType, int cxDesired, int cyDesired, uint fuLoad);

    private const uint NIM_ADD = 0x00000000;
    private const uint NIM_DELETE = 0x00000002;
    private const uint NIM_SETVERSION = 0x00000004;

    private const uint NIF_MESSAGE = 0x00000001;
    private const uint NIF_ICON = 0x00000002;
    private const uint NIF_TIP = 0x00000004;
    private const uint NIF_INFO = 0x00000010;

    private const uint NIIF_INFO = 0x00000001;
    private const uint NIIF_WARNING = 0x00000002;
    private const uint NIIF_ERROR = 0x00000003;

    private const uint IMAGE_ICON = 1;
    private const uint LR_LOADFROMFILE = 0x00000010;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NOTIFYICONDATAW
    {
        public int cbSize;
        public IntPtr hWnd;
        public uint uID;
        public uint uFlags;
        public uint uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szTip;
        public uint dwState;
        public uint dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szInfo;
        public uint uTimeoutOrVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string szInfoTitle;
        public uint dwInfoFlags;
        public Guid guidItem;
    }

    public static void Show(string title, string message, NotificationType type = NotificationType.Info)
    {
        try
        {
            var iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icon.ico");
            var hIcon = IntPtr.Zero;
            uint infoFlag = NIIF_INFO;

            switch (type)
            {
                case NotificationType.Warning: infoFlag = NIIF_WARNING; break;
                case NotificationType.Error: infoFlag = NIIF_ERROR; break;
                default: infoFlag = NIIF_INFO; break;
            }

            if (File.Exists(iconPath))
            {
                hIcon = LoadImage(IntPtr.Zero, iconPath, IMAGE_ICON, 32, 32, LR_LOADFROMFILE);
            }

            lock (Lock)
            {
                _uid++;
                var data = new NOTIFYICONDATAW
                {
                    cbSize = Marshal.SizeOf<NOTIFYICONDATAW>(),
                    uID = _uid,
                    uFlags = NIF_INFO | NIF_ICON | NIF_MESSAGE,
                    hIcon = hIcon,
                    szInfoTitle = title.Length > 63 ? title[..63] : title,
                    szInfo = message.Length > 255 ? message[..255] : message,
                    dwInfoFlags = infoFlag,
                    uTimeoutOrVersion = 5000
                };

                Shell_NotifyIconW(NIM_ADD, ref data);
                Shell_NotifyIconW(NIM_DELETE, ref data);
            }
        }
        catch { }
    }
}
