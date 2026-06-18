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
        LocalizationService.Instance.LanguageChanged += (_, _) => ApplyLocalizedText();
    }

    private async void OnLoaded(object? sender, RoutedEventArgs e)
    {
        _isLoaded = true;
        try
        {
            await App.TasksViewModel.LoadTasksCommand.ExecuteAsync(null);
            RefreshList();
        }
        catch (Exception ex) { Console.WriteLine($"Tasks load error: {ex.Message}"); }
        ApplyLocalizedText();
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
            var loc = LocalizationService.Instance;
            var tag = (TaskTypeCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString();
            bool needsApp = tag is "OpenApp" or "CloseApp";
            AppPickerCombo.IsVisible = needsApp;
            TargetAppBox.IsVisible = needsApp;

            if (tag == "CloseApp")
                TargetAppBox.Watermark = loc.Get("tasks.close_target_watermark");
            else if (tag == "OpenApp")
                TargetAppBox.Watermark = loc.Get("tasks.open_target_watermark");
            else
                TargetAppBox.Watermark = loc.Get("tasks.target_watermark");

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
          "LockPC" => TaskType.LockPC, "OpenApp" => TaskType.OpenApp,
          "CloseApp" => TaskType.CloseApp, _ => TaskType.Shutdown };

        var scheduleTag = (ScheduleTypeCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString();
        vm.NewScheduleType = scheduleTag == "SpecificTime"
            ? TaskScheduleType.SpecificTime : TaskScheduleType.Delayed;

        if (vm.NewScheduleType == TaskScheduleType.SpecificTime)
        {
            var selectedTime = TimePicker.SelectedTime ?? TimeSpan.FromHours(DateTime.Now.Hour + 1);
            var today = DateTime.Today;
            var scheduled = today.Add(selectedTime);
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

    private void ApplyLocalizedText()
    {
        var loc = LocalizationService.Instance;
        TasksTitle.Text = loc.Get("tasks.title");
        TasksSubtitle.Text = loc.Get("tasks.subtitle");
        AddTaskBtnText.Text = loc.Get("tasks.add_task");
        NewTaskTitle.Text = loc.Get("tasks.new_task");
        TaskNameBox.Watermark = loc.Get("tasks.name_watermark");

        TypeShutdown.Content = loc.Get("tasks.type_shutdown");
        TypeRestart.Content = loc.Get("tasks.type_restart");
        TypeSleep.Content = loc.Get("tasks.type_sleep");

        TypeLockPC.Content = loc.Get("tasks.type_lockpc");
        TypeOpenApp.Content = loc.Get("tasks.type_openapp");
        TypeCloseApp.Content = loc.Get("tasks.type_closeapp");

        ScheduleDelayed.Content = loc.Get("tasks.schedule_delayed");
        ScheduleSpecificTime.Content = loc.Get("tasks.schedule_time");

        DelayLabel.Text = loc.Get("tasks.delay_label");
        ScheduledTimeLabel.Text = loc.Get("tasks.scheduled_time");

        CancelBtnText.Text = loc.Get("tasks.cancel");
        CreateTaskBtnText.Text = loc.Get("tasks.create_task");

        // Note: "Delay 10m" button text inside the DataTemplate cannot be localized
        // via x:Name. Consider using a binding or a value converter for full localization.
    }
}
