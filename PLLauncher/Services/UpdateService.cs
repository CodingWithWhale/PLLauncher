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

    public Version CurrentVersion => Assembly.GetExecutingAssembly().GetName().Version ?? new Version(1, 0, 0, 0);

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

            var startInfo = new ProcessStartInfo
            {
                FileName = installerPath,
                UseShellExecute = true,
                Arguments = "/SILENT"
            };
            Process.Start(startInfo);

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

        _notificationService.ShowNotification(
            "Update Available",
            $"PLLauncher v{update.Version} is available (you have v{CurrentVersion}).");
    }

    public async Task<bool> PromptUpdateAsync(Window? owner)
    {
        var update = await CheckForUpdatesAsync();
        if (update == null) return false;

        var loc = LocalizationService.Instance;
        var confirmed = await Helpers.DialogHelper.ShowConfirmAsync(
            owner,
            $"PLLauncher v{update.Version} is available (you have v{CurrentVersion}). Download and install?",
            "Update Available",
            "Update",
            "Later");

        if (!confirmed) return true; // true = update available but user declined (don't show again this session)

        return await DownloadAndInstallAsync(update.DownloadUrl);
    }

    private static Version? ParseVersion(string versionString)
    {
        var v = versionString.TrimStart('v', 'V', ' ');
        if (Version.TryParse(v, out var version))
            return version;
        return null;
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}
