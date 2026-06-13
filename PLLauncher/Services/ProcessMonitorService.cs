using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using PLLauncher.Helpers;

namespace PLLauncher.Services;

public class ProcessMonitorService : IDisposable
{
    private readonly HashSet<string> _lockedProcesses = new(StringComparer.OrdinalIgnoreCase);

    private static string NormalizeName(string name)
    {
        if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            return name[..^4];
        return name;
    }

    public bool IsProcessRunning(string processName)
    {
        try
        {
            var procs = Process.GetProcessesByName(NormalizeName(processName));
            var running = procs.Length > 0;
            foreach (var p in procs) p.Dispose();
            return running;
        }
        catch { return false; }
    }

    public string? GetForegroundProcessName()
    {
        try
        {
            var hwnd = NativeMethods.GetForegroundWindow();
            if (hwnd == IntPtr.Zero) return null;
            NativeMethods.GetWindowThreadProcessId(hwnd, out var pid);
            if (pid == 0) return null;
            using var process = Process.GetProcessById((int)pid);
            return process.ProcessName;
        }
        catch { return null; }
    }

    public List<ProcessInfo> GetRunningProcesses()
    {
        var result = new List<ProcessInfo>();
        try
        {
            foreach (var process in Process.GetProcesses())
            {
                try
                {
                    var title = process.MainWindowTitle;
                    if (string.IsNullOrEmpty(title)) continue;
                    result.Add(new() { ProcessName = process.ProcessName, Id = process.Id,
                        MainWindowTitle = title });
                }
                catch { }
                finally { process.Dispose(); }
            }
        }
        catch { }
        return result;
    }

    public void LockApp(string processName) { processName = NormalizeName(processName); _lockedProcesses.Add(processName); TerminateProcess(processName); }
    public void UnlockApp(string processName) { _lockedProcesses.Remove(NormalizeName(processName)); }
    public bool IsProcessLocked(string processName) => _lockedProcesses.Contains(NormalizeName(processName));

    public void TerminateProcess(string processName)
    {
        try
        {
            var name = NormalizeName(processName);
            foreach (var p in Process.GetProcessesByName(name))
            {
                try
                {
                    p.CloseMainWindow();
                    if (!p.WaitForExit(2000))
                        p.Kill();
                    if (!p.WaitForExit(1000))
                        p.Kill(entireProcessTree: true);
                }
                catch { }
                finally { p.Dispose(); }
            }
        }
        catch { }
        // Fallback: Win32 TerminateProcess for stubborn/system processes (completely silent)
        try
        {
            var name = NormalizeName(processName);
            foreach (var p in Process.GetProcessesByName(name))
            {
                try
                {
                    var hProcess = NativeMethods.OpenProcess(NativeMethods.PROCESS_TERMINATE | NativeMethods.SYNCHRONIZE, false, (uint)p.Id);
                    if (hProcess != IntPtr.Zero)
                    {
                        NativeMethods.TerminateProcess(hProcess, 1);
                        NativeMethods.CloseHandle(hProcess);
                    }
                }
                catch { }
                finally { p.Dispose(); }
            }
        }
        catch { }
    }

    public void EnforceLockedProcesses()
    {
        foreach (var name in _lockedProcesses)
            if (IsProcessRunning(name)) TerminateProcess(name);
    }

    public bool LaunchApp(string path)
    {
        try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); return true; }
        catch { return false; }
    }

    public void Dispose() { _lockedProcesses.Clear(); GC.SuppressFinalize(this); }
}

public class ProcessInfo
{
    public string ProcessName { get; set; } = "";
    public int Id { get; set; }
    public string MainWindowTitle { get; set; } = "";
}
