using Avalonia.Controls;
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace PLLauncher.Services;

public class UpdateInfo
{
    [JsonPropertyName("version")]
    public string Version { get; set; } = "";

    [JsonPropertyName("downloadUrl")]
    public string DownloadUrl { get; set; } = "";

    [JsonPropertyName("changelog")]
    public string? Changelog { get; set; }
}

public class UpdateService
{
    private readonly HttpClient _httpClient;
    private readonly string _updateUrl;
    private readonly NotificationService _notificationService;

    public UpdateService(NotificationService notificationService, string repoOwner, string repoName)
    {
        _notificationService = notificationService;
        _updateUrl = $"https://raw.githubusercontent.com/{repoOwner}/{repoName}/main/update.json";
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("PLLauncher/1.0");
    }

    public Version CurrentVersion => NormalizeVersion(
        Assembly.GetExecutingAssembly().GetName().Version ?? new Version(1, 0, 0, 0));

    public async Task<UpdateInfo?> CheckForUpdatesAsync()
    {
        try
        {
            var info = await _httpClient.GetFromJsonAsync<UpdateInfo>(_updateUrl);
            if (info == null || string.IsNullOrWhiteSpace(info.Version)) return null;

            var latestVersion = ParseVersion(info.Version);
            if (latestVersion == null) return null;

            return latestVersion > CurrentVersion ? info : null;
        }
        catch
        {
            return null;
        }
    }

    private static Version NormalizeVersion(Version v)
    {
        var major = v.Major;
        var minor = v.Minor;
        var build = v.Build >= 0 ? v.Build : 0;
        var revision = v.Revision >= 0 ? v.Revision : 0;
        return new Version(major, minor, build, revision);
    }

    public async Task<bool> DownloadAndInstallAsync(string downloadUrl)
    {
        try
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "PLLauncherUpdate");
            Directory.CreateDirectory(tempDir);

            var fileName = Path.GetFileName(new Uri(downloadUrl).LocalPath);
            if (string.IsNullOrWhiteSpace(fileName)) fileName = "PLLauncher_Setup.exe";
            var installerPath = Path.Combine(tempDir, fileName);

            var response = await _httpClient.GetAsync(downloadUrl);
            response.EnsureSuccessStatusCode();

            using (var fs = new FileStream(installerPath, FileMode.Create, FileAccess.Write))
            {
                await response.Content.CopyToAsync(fs);
            }

            // Write a batch file that waits for the app to exit, installs silently, then launches the new version
            var installDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\');
            var batchPath = Path.Combine(tempDir, "update.bat");
            // In a .bat file: double-quote paths, use `start ""` for empty window title
            var batchContent = $@"@echo off
timeout /t 3 /nobreak >nul
""{installerPath}"" /SILENT
start """" ""{installDir}\PLLauncher.exe""
";
            await File.WriteAllTextAsync(batchPath, batchContent);

            Process.Start(new ProcessStartInfo
            {
                FileName = batchPath,
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Hidden,
            });

            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task CheckOnStartupAsync()
    {
        var update = await CheckForUpdatesAsync();
        if (update == null) return;

        var loc = LocalizationService.Instance;
        _notificationService.ShowNotification(
            loc.Get("update.title"),
            string.Format(loc.Get("settings.update_found"), update.Version));
    }

    public async Task<bool> PromptUpdateAsync(Window? owner)
    {
        var update = await CheckForUpdatesAsync();
        if (update == null) return false;

        var loc = LocalizationService.Instance;
        var confirmed = await Helpers.DialogHelper.ShowConfirmAsync(
            owner,
            string.Format(loc.Get("update.new_version"), update.Version, CurrentVersion),
            loc.Get("update.title"),
            loc.Get("update.download"),
            loc.Get("update.later"));

        if (!confirmed) return false;

        var progress = new Helpers.UpdateProgressDialog();
        Exception? downloadError = null;

        var downloadTask = DownloadAndInstallAsync(update.DownloadUrl);

        _ = downloadTask.ContinueWith(_ =>
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() => progress.Close());
        });

        progress.Start();
        await progress.ShowDialog(owner!);

        if (downloadTask.Exception != null)
        {
            downloadError = downloadTask.Exception.InnerException ?? downloadTask.Exception;
        }

        if (downloadError != null)
        {
            await Helpers.DialogHelper.ShowConfirmAsync(
                owner,
                loc.Get("update.error") + $"\n\n{downloadError.Message}",
                loc.Get("update.title"),
                "OK",
                "OK");
            return false;
        }

        return true;
    }

    private static Version? ParseVersion(string versionString)
    {
        var v = versionString.TrimStart('v', 'V', ' ');
        if (Version.TryParse(v, out var version))
            return NormalizeVersion(version);
        return null;
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}
