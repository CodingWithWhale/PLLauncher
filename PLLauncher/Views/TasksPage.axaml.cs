using Avalonia.Controls;
using Avalonia.Interactivity;
using PLLauncher.Models;
using PLLauncher.Services;
using System;
using System.Collections.Generic;

namespace PLLauncher.Views;

public partial class TasksPage : UserControl
{
    private bool _isLoaded;
    private List<AppInfo>? _installedApps;

    public TasksPage()
    {
        InitializeComponent();
        this.Loaded += OnLoaded;
    }

    private async void OnLoaded(object? sender, RoutedEventArgs e)
    {
        _isLoaded = true;
        try
        {
            await App.TasksViewModel.LoadTasksCommand.ExecuteAsync(null);
            RefreshList();
            AntiSleepBtnText.Text = App.TasksViewModel.IsAntiSleepActive ? "Stop Anti-Sleep" : "Anti-Sleep";
        }
        catch (Exception ex) { Console.WriteLine($"Tasks load error: {ex.Message}"); }
    }

    private void RefreshList()
    {
        TasksList.ItemsSource = null;
        TasksList.ItemsSource = App.TasksViewModel.Tasks;
    }

    private void AddTask_Click(object? s, RoutedEventArgs e)
    {
        AddTaskPanel.IsVisible = true;
        TaskNameBox.Focus();
        TaskTypeCombo.SelectedIndex = 0;
        ScheduleTypeCombo.SelectedIndex = 0;
        _installedApps = null;
        UpdateScheduleFields();
    }

    private void CancelAddTask_Click(object? s, RoutedEventArgs e) => AddTaskPanel.IsVisible = false;

    private void TaskTypeCombo_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!_isLoaded) return;
        try
        {
            var tag = (TaskTypeCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString();
            bool needsApp = tag is "OpenApp" or "CloseApp";
            AppPickerCombo.IsVisible = needsApp;
            TargetAppBox.IsVisible = needsApp;

            if (tag == "CloseApp")
                TargetAppBox.Watermark = "Process name to close (e.g. notepad)";
            else if (tag == "OpenApp")
                TargetAppBox.Watermark = "App path (or select from list above)";
            else
                TargetAppBox.Watermark = "Target app (optional)";

            if (needsApp) LoadInstalledApps();
        }
        catch (Exception ex) { Console.WriteLine($"TaskType error: {ex.Message}"); }
    }

    private void ScheduleTypeCombo_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!_isLoaded) return;
        try
        {
            UpdateScheduleFields();
        }
        catch (Exception ex) { Console.WriteLine($"ScheduleType error: {ex.Message}"); }
    }

    private void UpdateScheduleFields()
    {
        var tag = (ScheduleTypeCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString();
        bool isSpecificTime = tag == "SpecificTime";

        DelayPanel.IsVisible = !isSpecificTime;
        TimePickerPanel.IsVisible = isSpecificTime;

        if (isSpecificTime)
        {
            // Set default time to next hour
            TimePicker.SelectedTime = new TimeSpan(DateTime.Now.Hour + 1, 0, 0);
        }
    }

    private void LoadInstalledApps()
    {
        try
        {
            App.InstalledAppsService.RefreshCache();
            _installedApps = App.InstalledAppsService.GetInstalledApps();
            AppPickerCombo.ItemsSource = _installedApps;
        }
        catch (Exception ex) { Console.WriteLine($"Failed to load apps: {ex.Message}"); }
    }

    private void AppPickerCombo_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!_isLoaded) return;
        try
        {
            if (AppPickerCombo.SelectedItem is AppInfo app)
            {
                var taskTag = (TaskTypeCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString();
                // For CloseApp, use ProcessName (not the executable path)
                // For OpenApp, use ExecutablePath
                if (taskTag == "CloseApp" && !string.IsNullOrEmpty(app.ProcessName))
                    TargetAppBox.Text = app.ProcessName;
                else if (!string.IsNullOrEmpty(app.ExecutablePath))
                    TargetAppBox.Text = app.ExecutablePath;
            }
        }
        catch (Exception ex) { Console.WriteLine($"AppPicker error: {ex.Message}"); }
    }

    private async void SaveTask_Click(object? s, RoutedEventArgs e)
    {
        var vm = App.TasksViewModel;
        vm.NewTaskName = TaskNameBox.Text ?? "";
        vm.NewTaskType = (TaskTypeCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() switch
        { "Shutdown" => TaskType.Shutdown, "Restart" => TaskType.Restart, "Sleep" => TaskType.Sleep,
          "Hibernate" => TaskType.Hibernate, "LockPC" => TaskType.LockPC, "OpenApp" => TaskType.OpenApp,
          "CloseApp" => TaskType.CloseApp, "RunCommand" => TaskType.RunCommand,
          "AntiSleep" => TaskType.AntiSleep, _ => TaskType.Shutdown };

        var scheduleTag = (ScheduleTypeCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString();
        vm.NewScheduleType = scheduleTag == "SpecificTime"
            ? TaskScheduleType.SpecificTime : TaskScheduleType.Delayed;

        // For SpecificTime: build the DateTime from today + the TimePicker value
        if (vm.NewScheduleType == TaskScheduleType.SpecificTime)
        {
            var selectedTime = TimePicker.SelectedTime ?? TimeSpan.FromHours(DateTime.Now.Hour + 1);
            var today = DateTime.Today;
            var scheduled = today.Add(selectedTime);
            // If the time has already passed today, schedule for tomorrow
            if (scheduled <= DateTime.Now)
                scheduled = scheduled.AddDays(1);
            vm.NewScheduledTime = scheduled;
        }
        else
        {
            vm.NewDelayMinutes = (double)(DelayBox.Value ?? 60);
        }

        vm.NewTargetApp = TargetAppBox.Text ?? "";
        await vm.AddTaskCommand.ExecuteAsync(null);
        AddTaskPanel.IsVisible = false;
        RefreshList();
        AntiSleepBtnText.Text = vm.IsAntiSleepActive ? "Stop Anti-Sleep" : "Anti-Sleep";
    }

    private async void CancelTask_Click(object? s, RoutedEventArgs e)
    {
        if (s is Button btn && btn.Tag is string id)
        {
            await App.TasksViewModel.CancelTaskCommand.ExecuteAsync(id);
            RefreshList();
        }
    }

    private async void DelayTask_Click(object? s, RoutedEventArgs e)
    {
        if (s is Button btn && btn.Tag is string id)
        {
            await App.TasksViewModel.DelayTaskCommand.ExecuteAsync(id);
            RefreshList();
        }
    }

    private void ToggleAntiSleep_Click(object? s, RoutedEventArgs e)
    {
        App.TasksViewModel.ToggleAntiSleepCommand.Execute(null);
        AntiSleepBtnText.Text = App.TasksViewModel.IsAntiSleepActive ? "Stop Anti-Sleep" : "Anti-Sleep";
    }
}
