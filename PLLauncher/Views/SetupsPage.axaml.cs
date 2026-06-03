using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using PLLauncher.Models;
using PLLauncher.Services;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PLLauncher.Views;

public partial class SetupsPage : UserControl
{
    private static readonly string[] SetupEmojis =
        ["📦", "💼", "🎮", "📚", "🎨", "🎵", "💻", "🏠", "⚽", "🚗", "☕", "🔧", "🌟", "📝", "🎯"];

    private List<AppInfo>? _installedApps;
    private readonly List<ComboBox> _newGroupAppPickers = new();

    public SetupsPage()
    {
        InitializeComponent();
        GroupIconCombo.ItemsSource = SetupEmojis;
        this.Loaded += OnLoaded;
    }

    private async void OnLoaded(object? sender, RoutedEventArgs e)
    {
        try
        {
            await App.SetupsViewModel.LoadGroupsCommand.ExecuteAsync(null);
            RefreshList();
        }
        catch (Exception ex) { Console.WriteLine($"Setups load error: {ex.Message}"); }
    }

    private List<AppInfo> GetInstalledApps()
    {
        _installedApps ??= App.InstalledAppsService.GetInstalledApps();
        return _installedApps;
    }

    private void RefreshList()
    {
        GroupsList.ItemsSource = null;
        GroupsList.ItemsSource = App.SetupsViewModel.Groups;

        // Populate app pickers in group cards after layout
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            var newPanelPickers = NewGroupAppsPanel.GetVisualDescendants().OfType<ComboBox>().ToHashSet();
            foreach (var combo in GroupsList.GetVisualDescendants().OfType<ComboBox>())
            {
                if (!newPanelPickers.Contains(combo))
                    combo.ItemsSource = GetInstalledApps();
            }
        }, Avalonia.Threading.DispatcherPriority.Loaded);
    }

    private ComboBox CreateAppPickerCombo()
    {
        var combo = new ComboBox
        {
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
            Background = Avalonia.Application.Current?.Resources["SurfaceBrush"] as Avalonia.Media.IBrush,
            ItemsSource = GetInstalledApps(),
            ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<AppInfo>((app, _) =>
                new TextBlock { Text = app?.DisplayName ?? "" })
        };
        return combo;
    }

    private void ResetNewGroupAppFields()
    {
        NewGroupAppsPanel.Children.Clear();
        _newGroupAppPickers.Clear();
        AddAppPickerField();
    }

    private void AddAppPickerField()
    {
        var combo = CreateAppPickerCombo();
        _newGroupAppPickers.Add(combo);
        NewGroupAppsPanel.Children.Add(combo);
    }

    private void AddGroup_Click(object? s, RoutedEventArgs e)
    {
        AddGroupPanel.IsVisible = true;
        GroupNameBox.Text = "";
        GroupIconCombo.SelectedIndex = 0;
        ResetNewGroupAppFields();
        GroupNameBox.Focus();
    }

    private void AddAnotherAppField_Click(object? s, RoutedEventArgs e)
        => AddAppPickerField();

    private void CancelGroup_Click(object? s, RoutedEventArgs e)
        => AddGroupPanel.IsVisible = false;

    private async void SaveGroup_Click(object? s, RoutedEventArgs e)
    {
        var vm = App.SetupsViewModel;
        vm.NewGroupName = GroupNameBox.Text ?? "";
        vm.NewGroupIcon = GroupIconCombo.SelectedItem as string ?? "📦";
        vm.NewGroupAppPaths.Clear();

        foreach (var picker in _newGroupAppPickers)
        {
            if (picker.SelectedItem is AppInfo app && !string.IsNullOrEmpty(app.ExecutablePath))
            {
                if (!vm.NewGroupAppPaths.Contains(app.ExecutablePath, StringComparer.OrdinalIgnoreCase))
                    vm.NewGroupAppPaths.Add(app.ExecutablePath);
            }
        }

        await vm.AddGroupCommand.ExecuteAsync(null);
        AddGroupPanel.IsVisible = false;
        RefreshList();
    }

    private void LaunchGroup_Click(object? s, RoutedEventArgs e)
    {
        if (s is Button btn)
        {
            var group = FindGroupFromVisual(btn);
            if (group != null) App.SetupsViewModel.LaunchGroupCommand.Execute(group);
        }
    }

    private void ScheduleLaunch_Click(object? s, RoutedEventArgs e)
    {
        if (s is Button btn)
        {
            var group = FindGroupFromVisual(btn);
            if (group != null)
            {
                var parentBorder = btn.GetVisualAncestors().OfType<Border>().LastOrDefault();
                var delayBox = parentBorder?.GetVisualDescendants().OfType<NumericUpDown>().FirstOrDefault();
                App.SetupsViewModel.LaunchDelayMinutes = delayBox?.Value != null ? (int)delayBox.Value : 5;
                App.SetupsViewModel.ScheduleLaunchCommand.Execute(group);
            }
        }
    }

    private async void DeleteGroup_Click(object? s, RoutedEventArgs e)
    {
        if (s is Button btn && btn.Tag is string id)
        {
            var group = App.SetupsViewModel.Groups.FirstOrDefault(g => g.Id == id);
            if (group != null)
            {
                await App.SetupsViewModel.DeleteGroupCommand.ExecuteAsync(group);
                RefreshList();
            }
        }
    }

    private async void AddApp_Click(object? s, RoutedEventArgs e)
    {
        if (s is Button btn && btn.Tag is string groupId)
        {
            var group = App.SetupsViewModel.Groups.FirstOrDefault(g => g.Id == groupId);
            if (group == null) return;

            var parentGrid = btn.GetVisualAncestors().OfType<Grid>().FirstOrDefault();
            var picker = parentGrid?.GetVisualDescendants().OfType<ComboBox>().FirstOrDefault();
            if (picker?.SelectedItem is AppInfo app && !string.IsNullOrEmpty(app.ExecutablePath))
            {
                App.SetupsViewModel.TempAppPath = app.ExecutablePath;
                await App.SetupsViewModel.AddAppToGroupCommand.ExecuteAsync(group);
                picker.SelectedItem = null;
                RefreshList();
            }
        }
    }

    private async void RemoveApp_Click(object? s, RoutedEventArgs e)
    {
        if (s is Button btn && btn.Tag is string appPath)
        {
            var group = FindGroupFromVisual(btn);
            if (group != null)
            {
                await App.SetupsViewModel.RemoveAppFromGroupAsync(group, appPath);
                RefreshList();
            }
        }
    }

    private AppGroup? FindGroupFromVisual(Control ctrl)
    {
        var parent = ctrl.GetVisualAncestors().FirstOrDefault(a => a is Border b && b.DataContext is AppGroup);
        return (parent as Border)?.DataContext as AppGroup;
    }
}
