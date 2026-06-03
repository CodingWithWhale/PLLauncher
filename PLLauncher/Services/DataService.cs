using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using PLLauncher.Models;

namespace PLLauncher.Services;

public class DataService
{
    public static readonly string AppDataPath = GetDataPath();

    private static string GetDataPath()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\PLLauncher");
            if (key?.GetValue("DataDir") is string dir && !string.IsNullOrWhiteSpace(dir))
                return dir;
        }
        catch { }
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PLLauncher");
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(), new ReminderUrgencyConverter() }
    };

    // Semaphore to prevent concurrent file access
    private readonly SemaphoreSlim _fileLock = new(1, 1);

    public DataService()
    {
        Directory.CreateDirectory(AppDataPath);
    }

    public async Task<List<KeybindItem>> LoadKeybindsAsync()
        => await LoadAsync<List<KeybindItem>>("keybinds.json") ?? new();

    public async Task SaveKeybindsAsync(List<KeybindItem> keybinds)
        => await SaveAsync("keybinds.json", keybinds);

    public async Task<List<TaskItem>> LoadTasksAsync()
        => await LoadAsync<List<TaskItem>>("tasks.json") ?? new();

    public async Task SaveTasksAsync(List<TaskItem> tasks)
        => await SaveAsync("tasks.json", tasks);

    public async Task<List<TimeLimitItem>> LoadTimeLimitsAsync()
        => await LoadAsync<List<TimeLimitItem>>("timelimits.json") ?? new();

    public async Task SaveTimeLimitsAsync(List<TimeLimitItem> limits)
        => await SaveAsync("timelimits.json", limits);

    public async Task<List<ScheduleItem>> LoadSchedulesAsync()
        => await LoadAsync<List<ScheduleItem>>("schedules.json") ?? new();

    public async Task SaveSchedulesAsync(List<ScheduleItem> schedules)
        => await SaveAsync("schedules.json", schedules);

    public async Task<List<AppUsageRecord>> LoadAppUsageAsync()
        => await LoadAsync<List<AppUsageRecord>>("appusage.json") ?? new();

    public async Task SaveAppUsageAsync(List<AppUsageRecord> records)
        => await SaveAsync("appusage.json", records);

    public async Task<AppSettings> LoadSettingsAsync()
        => await LoadAsync<AppSettings>("settings.json") ?? new();

    public async Task SaveSettingsAsync(AppSettings settings)
        => await SaveAsync("settings.json", settings);

    public async Task<List<AppGroup>> LoadAppGroupsAsync()
        => await LoadAsync<List<AppGroup>>("appgroups.json") ?? new();

    public async Task SaveAppGroupsAsync(List<AppGroup> groups)
        => await SaveAsync("appgroups.json", groups);

    public async Task ExportAllAsync(string exportPath)
    {
        var data = new AppExportData
        {
            Keybinds = await LoadKeybindsAsync(),
            Tasks = await LoadTasksAsync(),
            TimeLimits = await LoadTimeLimitsAsync(),
            Schedules = await LoadSchedulesAsync(),
            Settings = await LoadSettingsAsync(),
            ExportedAt = DateTime.Now
        };
        var json = JsonSerializer.Serialize(data, JsonOptions);
        await _fileLock.WaitAsync();
        try { await File.WriteAllTextAsync(exportPath, json); }
        finally { _fileLock.Release(); }
    }

    public async Task ImportAllAsync(string importPath)
    {
        if (!File.Exists(importPath)) return;
        var json = await File.ReadAllTextAsync(importPath);
        var data = JsonSerializer.Deserialize<AppExportData>(json, JsonOptions);
        if (data == null) return;
        if (data.Keybinds != null) await SaveKeybindsAsync(data.Keybinds);
        if (data.Tasks != null) await SaveTasksAsync(data.Tasks);
        if (data.TimeLimits != null) await SaveTimeLimitsAsync(data.TimeLimits);
        if (data.Schedules != null) await SaveSchedulesAsync(data.Schedules);
        if (data.Settings != null) await SaveSettingsAsync(data.Settings);
    }

    public async Task ResetAllDataAsync()
    {
        await SaveKeybindsAsync(new());
        await SaveTasksAsync(new());
        await SaveTimeLimitsAsync(new());
        await SaveSchedulesAsync(new());
        await SaveAppGroupsAsync(new());
        await SaveSettingsAsync(new());
    }

    private async Task<T?> LoadAsync<T>(string fileName) where T : class
    {
        var filePath = Path.Combine(AppDataPath, fileName);
        if (!File.Exists(filePath)) return null;
        try
        {
            await _fileLock.WaitAsync();
            try { var json = await File.ReadAllTextAsync(filePath); return JsonSerializer.Deserialize<T>(json, JsonOptions); }
            finally { _fileLock.Release(); }
        }
        catch { return null; }
    }

    private async Task SaveAsync<T>(string fileName, T data)
    {
        var filePath = Path.Combine(AppDataPath, fileName);
        var json = JsonSerializer.Serialize(data, JsonOptions);
        await _fileLock.WaitAsync();
        try { await File.WriteAllTextAsync(filePath, json); }
        finally { _fileLock.Release(); }
    }
}

public class AppExportData
{
    public List<KeybindItem>? Keybinds { get; set; }
    public List<TaskItem>? Tasks { get; set; }
    public List<TimeLimitItem>? TimeLimits { get; set; }
    public List<ScheduleItem>? Schedules { get; set; }
    public List<AppGroup>? AppGroups { get; set; }
    public AppSettings? Settings { get; set; }
    public DateTime ExportedAt { get; set; }
}

/// <summary>
/// Handles backwards compatibility for ReminderUrgency enum rename:
/// Meh -> Low, DoIt -> Medium, Urgent -> High
/// </summary>
public class ReminderUrgencyConverter : JsonConverter<ReminderUrgency>
{
    public override ReminderUrgency Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var str = reader.GetString();
        return str switch
        {
            "Meh" => ReminderUrgency.Low,
            "DoIt" => ReminderUrgency.Medium,
            "Urgent" => ReminderUrgency.High,
            _ => Enum.TryParse<ReminderUrgency>(str, out var val) ? val : ReminderUrgency.Low
        };
    }

    public override void Write(Utf8JsonWriter writer, ReminderUrgency value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.ToString());
}
