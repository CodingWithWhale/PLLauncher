using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PLLauncher.Models;
using PLLauncher.Services;

namespace PLLauncher.ViewModels;

public partial class SetupsViewModel : ObservableObject
{
    private readonly DataService _dataService;
    private readonly ProcessMonitorService _processMonitor;

    [ObservableProperty] private ObservableCollection<AppGroup> _groups = new();
    [ObservableProperty] private AppGroup? _selectedGroup;
    [ObservableProperty] private string _newGroupName = string.Empty;
    [ObservableProperty] private string _newGroupIcon = "📦";
    [ObservableProperty] private string _newAppPath = string.Empty;
    [ObservableProperty] private bool _isAddingNew;
    [ObservableProperty] private string _tempAppPath = string.Empty;
    [ObservableProperty] private List<string> _newGroupAppPaths = new();

    // Timer launch fields
    [ObservableProperty] private int _launchDelayMinutes;

    public SetupsViewModel(DataService ds, ProcessMonitorService pm)
    {
        _dataService = ds;
        _processMonitor = pm;
    }

    [RelayCommand]
    private async Task LoadGroupsAsync()
    {
        var groups = await _dataService.LoadAppGroupsAsync();
        Groups = new(groups);
    }

    [RelayCommand]
    private async Task AddGroupAsync()
    {
        if (string.IsNullOrWhiteSpace(NewGroupName)) return;

        var group = new AppGroup
        {
            Name = NewGroupName,
            Icon = NewGroupIcon,
            AppPaths = NewGroupAppPaths.Where(p => !string.IsNullOrWhiteSpace(p)).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            CreatedAt = DateTime.Now
        };

        Groups.Add(group);
        await SaveAsync();
        NewGroupName = string.Empty;
        NewGroupIcon = "📦";
        NewGroupAppPaths = new();
        IsAddingNew = false;
    }

    [RelayCommand]
    private async Task DeleteGroupAsync(AppGroup? group)
    {
        if (group == null) return;
        Groups.Remove(group);
        await SaveAsync();
    }

    [RelayCommand]
    private async Task AddAppToGroupAsync(AppGroup? group)
    {
        if (group == null || string.IsNullOrWhiteSpace(TempAppPath)) return;
        group.AppPaths.Add(TempAppPath);
        TempAppPath = string.Empty;
        await SaveAsync();
    }

    public async Task RemoveAppFromGroupAsync(AppGroup group, string path)
    {
        group.AppPaths.Remove(path);
        await SaveAsync();
    }

    [RelayCommand]
    private void LaunchGroup(AppGroup? group)
    {
        if (group == null) return;
        foreach (var path in group.AppPaths)
        {
            try { _processMonitor.LaunchApp(path); }
            catch { }
        }
    }

    [RelayCommand]
    private void ScheduleLaunch(AppGroup? group)
    {
        if (group == null || LaunchDelayMinutes <= 0) return;
        var delay = TimeSpan.FromMinutes(LaunchDelayMinutes);

        _ = Task.Run(async () =>
        {
            await Task.Delay(delay);
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                foreach (var path in group.AppPaths)
                {
                    try { _processMonitor.LaunchApp(path); }
                    catch { }
                }
            });
        });
    }

    private async Task SaveAsync()
        => await _dataService.SaveAppGroupsAsync(Groups.ToList());
}
