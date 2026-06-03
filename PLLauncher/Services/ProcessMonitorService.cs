using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace PLLauncher.Services;

public class ProcessMonitorService : IDisposable
{
    private readonly HashSet<string> _lockedProcesses = new(StringComparer.OrdinalIgnoreCase);

    public bool IsProcessRunning(string processName)
    {
        try
        {
            var procs = Process.GetProcessesByName(processName);
            var running = procs.Length > 0;
            foreach (var p in procs) p.Dispose();
            return running;
        }
        catch { return false; }
    }

    public List<ProcessInfo> GetRunningProcesses()
    {
        var result = new List<ProcessInfo>();
        try
        {
            foreach (var process in Process.GetProcesses())
            {
                try { result.Add(new() { ProcessName = process.ProcessName, Id = process.Id,
                    MainWindowTitle = process.MainWindowTitle }); }
                catch { }
                finally { process.Dispose(); }
            }
        }
        catch { }
        return result;
    }

    public void LockApp(string processName) { _lockedProcesses.Add(processName); TerminateProcess(processName); }
    public void UnlockApp(string processName) { _lockedProcesses.Remove(processName); }
    public bool IsProcessLocked(string processName) => _lockedProcesses.Contains(processName);

    public void TerminateProcess(string processName)
    {
        try
        {
            foreach (var p in Process.GetProcessesByName(processName))
            { try { p.CloseMainWindow(); if (!p.WaitForExit(3000)) p.Kill(); } catch { } finally { p.Dispose(); } }
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
