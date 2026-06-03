using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;

namespace PLLauncher.Services;

public class AppInfo
{
    public string DisplayName { get; set; } = "";
    public string ExecutablePath { get; set; } = "";
    public string ProcessName { get; set; } = "";

    public override string ToString() => DisplayName;
}

public class InstalledAppsService
{
    private List<AppInfo>? _cachedApps;
    private Dictionary<string, string>? _processDisplayNames;

    private static readonly string[] NoiseNameFragments =
    [
        "jdk", "jre", "temurin", "adoptium", "openjdk", "java se",
        "redistributable", "runtime", "visual c++", "vc++",
        "update for", "hotfix", "security update", "language pack",
        "sdk", "driver", "firmware", "installer", "setup",
        "microsoft .net", ".net framework", "windows software development",
        "documentation", "help pack", "click-to-run", "office 16",
        "vs_", "visual studio", "nuget", "python launcher",
        "node.js", "npm", "git version", "windows kit",
        "uninstall", "repair", "bootstrapper", "compatibility",
        "webview2 runtime", "edge update", "teams machine",
    ];

    public List<AppInfo> GetInstalledApps()
    {
        if (_cachedApps != null) return _cachedApps;

        var apps = new Dictionary<string, AppInfo>(StringComparer.OrdinalIgnoreCase);

        // Start Menu shortcuts are the cleanest source for launchable apps
        ScanStartMenu(apps);

        // Registry entries only when they have a real executable
        ReadUninstallKey(Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", apps);
        ReadUninstallKey(Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall", apps);
        ReadUninstallKey(Registry.CurrentUser, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", apps);

        _cachedApps = apps.Values
            .Where(IsLaunchableApp)
            .OrderBy(a => a.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        _processDisplayNames = _cachedApps
            .Where(a => !string.IsNullOrEmpty(a.ProcessName))
            .GroupBy(a => a.ProcessName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().DisplayName, StringComparer.OrdinalIgnoreCase);

        return _cachedApps;
    }

    public string? GetDisplayNameForProcess(string processName)
    {
        GetInstalledApps();
        if (_processDisplayNames != null &&
            _processDisplayNames.TryGetValue(processName, out var name))
            return name;
        return null;
    }

    public void RefreshCache()
    {
        _cachedApps = null;
        _processDisplayNames = null;
    }

    private static bool IsLaunchableApp(AppInfo app)
    {
        if (string.IsNullOrWhiteSpace(app.DisplayName)) return false;
        if (string.IsNullOrWhiteSpace(app.ExecutablePath)) return false;
        if (!app.ExecutablePath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) return false;
        if (!File.Exists(app.ExecutablePath)) return false;
        if (IsNoiseName(app.DisplayName)) return false;
        return true;
    }

    private static bool IsNoiseName(string displayName)
    {
        var lower = displayName.ToLowerInvariant();
        foreach (var fragment in NoiseNameFragments)
        {
            if (lower.Contains(fragment, StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    private void ReadUninstallKey(RegistryKey rootKey, string subKeyPath, Dictionary<string, AppInfo> apps)
    {
        try
        {
            using var key = rootKey.OpenSubKey(subKeyPath);
            if (key == null) return;

            foreach (var subKeyName in key.GetSubKeyNames())
            {
                try
                {
                    using var subKey = key.OpenSubKey(subKeyName);
                    if (subKey == null) continue;

                    var displayName = subKey.GetValue("DisplayName") as string;
                    if (string.IsNullOrWhiteSpace(displayName) || IsNoiseName(displayName)) continue;

                    var systemComponent = subKey.GetValue("SystemComponent") as int?;
                    if (systemComponent == 1) continue;
                    var parentKeyName = subKey.GetValue("ParentKeyName") as string;
                    if (!string.IsNullOrEmpty(parentKeyName)) continue;

                    var installLocation = subKey.GetValue("InstallLocation") as string;
                    var displayIcon = subKey.GetValue("DisplayIcon") as string;

                    var exePath = "";
                    if (!string.IsNullOrWhiteSpace(displayIcon))
                    {
                        var iconPath = displayIcon.Split(',')[0].Trim('"').Trim();
                        if (!string.IsNullOrEmpty(iconPath) && iconPath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && File.Exists(iconPath))
                            exePath = iconPath;
                    }

                    if (string.IsNullOrEmpty(exePath))
                        exePath = TryFindExecutable(installLocation, displayName);

                    if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath)) continue;

                    var processName = Path.GetFileNameWithoutExtension(exePath);

                    if (!apps.ContainsKey(displayName))
                    {
                        apps[displayName] = new AppInfo
                        {
                            DisplayName = displayName,
                            ExecutablePath = exePath,
                            ProcessName = processName
                        };
                    }
                }
                catch { }
            }
        }
        catch { }
    }

    private string TryFindExecutable(string? installLocation, string displayName)
    {
        if (string.IsNullOrWhiteSpace(installLocation) || !Directory.Exists(installLocation)) return "";

        try
        {
            var exes = Directory.GetFiles(installLocation, "*.exe", SearchOption.TopDirectoryOnly);
            if (exes.Length == 0) return "";

            var normalizedName = displayName.Replace(" ", "").Replace("-", "").Replace("_", "");
            foreach (var exe in exes)
            {
                var name = Path.GetFileNameWithoutExtension(exe).Replace(" ", "").Replace("-", "").Replace("_", "");
                if (normalizedName.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf(normalizedName, StringComparison.OrdinalIgnoreCase) >= 0)
                    return exe;
            }

            foreach (var exe in exes)
            {
                var name = Path.GetFileNameWithoutExtension(exe);
                if (!name.Contains("unins", StringComparison.OrdinalIgnoreCase) &&
                    !name.Contains("uninstall", StringComparison.OrdinalIgnoreCase) &&
                    !name.Contains("helper", StringComparison.OrdinalIgnoreCase) &&
                    !name.Contains("setup", StringComparison.OrdinalIgnoreCase))
                    return exe;
            }

            return exes[0];
        }
        catch { return ""; }
    }

    private void ScanStartMenu(Dictionary<string, AppInfo> apps)
    {
        var paths = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu), "Programs"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs")
        };

        foreach (var startMenuPath in paths)
        {
            if (!Directory.Exists(startMenuPath)) continue;
            try
            {
                foreach (var lnk in Directory.GetFiles(startMenuPath, "*.lnk", SearchOption.AllDirectories))
                {
                    try
                    {
                        var name = Path.GetFileNameWithoutExtension(lnk);
                        if (string.IsNullOrWhiteSpace(name) || IsNoiseName(name)) continue;

                        var targetPath = ResolveShortcut(lnk);
                        if (string.IsNullOrEmpty(targetPath) || !targetPath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                            continue;
                        if (!File.Exists(targetPath)) continue;

                        var processName = Path.GetFileNameWithoutExtension(targetPath);
                        var key = name;

                        if (!apps.ContainsKey(key) || string.IsNullOrEmpty(apps[key].ExecutablePath))
                        {
                            apps[key] = new AppInfo
                            {
                                DisplayName = name,
                                ExecutablePath = targetPath,
                                ProcessName = processName
                            };
                        }
                    }
                    catch { }
                }
            }
            catch { }
        }
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("000214F9-0000-0000-C000-000000000046")]
    private interface IShellLinkW
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cch, IntPtr pfd, uint fFlags);
        void GetIDList(out IntPtr ppidl);
        void SetIDList(IntPtr pidl);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cch);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cch);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cch);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
        void GetHotkey(out ushort pwHotkey);
        void SetHotkey(ushort wHotkey);
        void GetShowCmd(out int piShowCmd);
        void SetShowCmd(int iShowCmd);
        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, int cch, out int piIcon);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, uint dwReserved);
        void Resolve(IntPtr hwnd, uint fFlags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
    }

    [ComImport]
    [Guid("00021401-0000-0000-C000-000000000046")]
    private class CShellLink { }

    private string? ResolveShortcut(string shortcutPath)
    {
        try
        {
            var link = (IShellLinkW)new CShellLink();
            var persistFile = (IPersistFile)link;
            persistFile.Load(shortcutPath, 0);
            var sb = new StringBuilder(260);
            link.GetPath(sb, sb.Capacity, IntPtr.Zero, 0);
            return sb.Length > 0 ? sb.ToString() : null;
        }
        catch { return null; }
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("0000010b-0000-0000-C000-000000000046")]
    private interface IPersistFile
    {
        void GetClassID(out Guid pClassID);
        void IsDirty();
        void Load([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, uint dwMode);
        void Save([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, bool fRemember);
        void SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string pszFileName);
        void GetCurFile([MarshalAs(UnmanagedType.LPWStr)] out string ppszFileName);
    }
}
