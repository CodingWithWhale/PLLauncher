using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using PLLauncher.Models;
using PLLauncher.Services;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PLLauncher.Views;

public partial class SchedulerPage : UserControl
{
    private bool _isLoaded;
    private List<AppInfo>? _installedApps;
    private bool _addingReminder;

    public SchedulerPage()
    {
        InitializeComponent();
        this.Loaded += OnLoaded;
    }

    private async void OnLoaded(object? sender, RoutedEventArgs e)
    {
        _isLoaded = true;
        try
        {
            await App.SchedulerViewModel.LoadSchedulesCommand.ExecuteAsync(null);
            RefreshList();

            // Delay dot update so calendar visual tree is fully built
            Avalonia.Threading.Dispatcher.UIThread.Post(() => UpdateCalendarDots(), Avalonia.Threading.DispatcherPriority.Background);

            // Re-apply dots when navigating months
            try
            {
                CalendarControl.DisplayDateChanged += (s, args) =>
                {
                    try { Avalonia.Threading.Dispatcher.UIThread.Post(() => UpdateCalendarDots(), Avalonia.Threading.DispatcherPriority.Background); }
                    catch { }
                };
            }
            catch { }
        }
        catch (Exception ex) { Console.WriteLine($"Scheduler load error: {ex.Message}"); }
    }

    private void RefreshList()
    {
        SchedulesList.ItemsSource = null;
        SchedulesList.ItemsSource = App.SchedulerViewModel.Schedules;
    }

    private void AddSchedule_Click(object? s, RoutedEventArgs e)
    {
        _addingReminder = false;
        ShowAddPanel(isReminder: false);
    }

    private void AddReminder_Click(object? s, RoutedEventArgs e)
    {
        _addingReminder = true;
        ShowAddPanel(isReminder: true);
    }

    private void ShowAddPanel(bool isReminder)
    {
        AddSchedulePanel.IsVisible = true;
        ScheduleNameBox.Focus();

        if (isReminder)
        {
            AddPanelTitle.Text = "New Reminder";
            ReminderFieldsPanel.IsVisible = true;
            ActionFieldsPanel.IsVisible = false;
            CreateButton.Content = "Create Reminder";
            ScheduleNameBox.Watermark = "Reminder title";
            UrgencyMeh.IsChecked = true;
            ReminderMessageBox.Text = "";
        }
        else
        {
            AddPanelTitle.Text = "New Schedule";
            ReminderFieldsPanel.IsVisible = false;
            ActionFieldsPanel.IsVisible = true;
            CreateButton.Content = "Create Schedule";
            ScheduleNameBox.Watermark = "Name (e.g. Open VSCode on weekdays)";
            ActionTypeCombo.SelectedIndex = 0;
            RecurrenceCombo.SelectedIndex = 2; // Weekdays
        }

        ScheduleDatePicker.SelectedDate = DateTime.Today;
        ScheduleTimePicker.SelectedTime = DateTime.Now.AddHours(1).TimeOfDay;

        _installedApps = null;
        CustomDaysPanel.IsVisible = false;
        DayMon.IsChecked = true;
        DayTue.IsChecked = true;
        DayWed.IsChecked = true;
        DayThu.IsChecked = true;
        DayFri.IsChecked = true;
        DaySat.IsChecked = false;
        DaySun.IsChecked = false;
    }

    private void CancelSchedule_Click(object? s, RoutedEventArgs e) => AddSchedulePanel.IsVisible = false;

    private void ActionTypeCombo_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!_isLoaded) return;
        try
        {
            var tag = (ActionTypeCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString();
            bool needsApp = tag is "OpenApp" or "CloseApp";
            AppPickerCombo.IsVisible = needsApp;
            ActionTargetBox.Watermark = tag switch
            {
                "OpenApp" => "App path (or select from list above)",
                "CloseApp" => "Process name to close",
                "RunCommand" => "Command to run",
                _ => "Target"
            };
            if (needsApp) LoadInstalledApps();
        }
        catch (Exception ex) { Console.WriteLine($"ActionType error: {ex.Message}"); }
    }

    private void RecurrenceCombo_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!_isLoaded) return;
        try
        {
            var tag = (RecurrenceCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString();
            CustomDaysPanel.IsVisible = tag == "Custom";
            if (tag == "Custom" && DayMon.IsChecked == null)
            {
                DayMon.IsChecked = true; DayTue.IsChecked = true; DayWed.IsChecked = true;
                DayThu.IsChecked = true; DayFri.IsChecked = true; DaySat.IsChecked = false; DaySun.IsChecked = false;
            }
        }
        catch (Exception ex) { Console.WriteLine($"RecurrenceCombo error: {ex.Message}"); }
    }

    private DayOfWeek[] GetSelectedCustomDays()
    {
        var days = new List<DayOfWeek>();
        if (DayMon.IsChecked == true) days.Add(DayOfWeek.Monday);
        if (DayTue.IsChecked == true) days.Add(DayOfWeek.Tuesday);
        if (DayWed.IsChecked == true) days.Add(DayOfWeek.Wednesday);
        if (DayThu.IsChecked == true) days.Add(DayOfWeek.Thursday);
        if (DayFri.IsChecked == true) days.Add(DayOfWeek.Friday);
        if (DaySat.IsChecked == true) days.Add(DayOfWeek.Saturday);
        if (DaySun.IsChecked == true) days.Add(DayOfWeek.Sunday);
        return days.ToArray();
    }

    private ReminderUrgency GetSelectedUrgency()
    {
        if (UrgencyUrgent.IsChecked == true) return ReminderUrgency.High;
        if (UrgencyDoIt.IsChecked == true) return ReminderUrgency.Medium;
        return ReminderUrgency.Low;
    }

    private void LoadInstalledApps()
    {
        if (_installedApps != null) return;
        try
        {
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
                var actionTag = (ActionTypeCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString();
                if (actionTag == "CloseApp" && !string.IsNullOrEmpty(app.ProcessName))
                    ActionTargetBox.Text = app.ProcessName;
                else if (!string.IsNullOrEmpty(app.ExecutablePath))
                    ActionTargetBox.Text = app.ExecutablePath;
            }
        }
        catch (Exception ex) { Console.WriteLine($"AppPicker error: {ex.Message}"); }
    }

    private async void SaveSchedule_Click(object? s, RoutedEventArgs e)
    {
        var vm = App.SchedulerViewModel;
        vm.NewScheduleName = ScheduleNameBox.Text ?? "";
        vm.IsNewItemReminder = _addingReminder;
        vm.NewScheduledDate = ScheduleDatePicker.SelectedDate;

        if (_addingReminder)
        {
            vm.NewReminderMessage = ReminderMessageBox.Text ?? "";
            vm.NewUrgency = GetSelectedUrgency();
            vm.NewRecurrence = ScheduleRecurrence.Once;
        }
        else
        {
            vm.NewActionTarget = ActionTargetBox.Text ?? "";
            vm.NewActionType = (ActionTypeCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() switch
            {
                "OpenApp" => ScheduleActionType.OpenApp, "CloseApp" => ScheduleActionType.CloseApp,
                "RunCommand" => ScheduleActionType.RunCommand, "Shutdown" => ScheduleActionType.Shutdown,
                "Restart" => ScheduleActionType.Restart, "Sleep" => ScheduleActionType.Sleep,
                "LockPC" => ScheduleActionType.LockPC, _ => ScheduleActionType.OpenApp
            };
            var recurrenceTag = (RecurrenceCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString();
            vm.NewRecurrence = recurrenceTag switch
            {
                "Once" => ScheduleRecurrence.Once, "Daily" => ScheduleRecurrence.Daily,
                "Weekday" => ScheduleRecurrence.Weekday, "Weekly" => ScheduleRecurrence.Weekly,
                "Custom" => ScheduleRecurrence.Custom, _ => ScheduleRecurrence.Weekday
            };
            if (vm.NewRecurrence == ScheduleRecurrence.Custom)
            {
                vm.NewRecurringDays = GetSelectedCustomDays();
                if (vm.NewRecurringDays.Length == 0) vm.NewRecurringDays = new[] { DayOfWeek.Monday };
            }
        }

        var selectedTime = ScheduleTimePicker.SelectedTime;
        if (selectedTime.HasValue) vm.NewTime = selectedTime.Value;

        await vm.AddScheduleCommand.ExecuteAsync(null);
        AddSchedulePanel.IsVisible = false;
        RefreshList();
        Avalonia.Threading.Dispatcher.UIThread.Post(() => UpdateCalendarDots(), Avalonia.Threading.DispatcherPriority.Background);

        if (CalendarControl.SelectedDate.HasValue)
            UpdateDayView(CalendarControl.SelectedDate.Value);
    }

    private void Calendar_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        try
        {
            if (CalendarControl.SelectedDate.HasValue)
                UpdateDayView(CalendarControl.SelectedDate.Value);
        }
        catch (Exception ex) { Console.WriteLine($"Calendar selection error: {ex.Message}"); }
    }

    private void UpdateDayView(DateTime date)
    {
        try
        {
            SelectedDateLabel.Text = date.ToString("dddd, MMMM d, yyyy");
            var items = App.SchedulerViewModel.GetSchedulesForDate(date);
            DayItemsList.ItemsSource = items;
            DayEmptyLabel.IsVisible = items.Count == 0;
        }
        catch (Exception ex) { Console.WriteLine($"Day view error: {ex.Message}"); }
    }

    /// <summary>
    /// Highlights calendar days that have scheduled items by styling CalendarDayButton directly.
    /// Handles Content being int, string, or any other type that can be parsed to a day number.
    /// </summary>
    private void UpdateCalendarDots()
    {
        try
        {
            // First clear any existing highlights by resetting Classes
            var allButtons = CalendarControl.GetVisualDescendants()
                .OfType<CalendarDayButton>()
                .ToList();

            foreach (var btn in allButtons)
            {
                btn.Classes.Remove("HasItems");
            }

            var schedules = App.SchedulerViewModel.Schedules;
            if (schedules == null || schedules.Count == 0) return;

            // Collect dates that have items for the currently displayed month
            var datesWithItems = new HashSet<int>(); // day-of-month numbers
            var displayDate = CalendarControl.DisplayDate;
            var displayYear = displayDate.Year;
            var displayMonth = displayDate.Month;

            // Check each day of the display month
            var daysInMonth = DateTime.DaysInMonth(displayYear, displayMonth);
            for (int day = 1; day <= daysInMonth; day++)
            {
                var date = new DateTime(displayYear, displayMonth, day);
                if (App.SchedulerViewModel.GetSchedulesForDate(date).Count > 0)
                    datesWithItems.Add(day);
            }

            if (datesWithItems.Count == 0) return;

            // Now find the buttons and style those whose Content matches a day with items
            var highlightBg = new SolidColorBrush(Color.FromArgb(0x33, 0x64, 0xB5, 0xF6));
            var highlightFg = new SolidColorBrush(Color.FromRgb(0x1E, 0x88, 0xE5));

            foreach (var btn in allButtons)
            {
                int? dayNum = null;

                // Content can be int, string, or other types depending on Avalonia version
                if (btn.Content is int i && i >= 1 && i <= 31)
                    dayNum = i;
                else if (btn.Content is string s && int.TryParse(s, out var parsed) && parsed >= 1 && parsed <= 31)
                    dayNum = parsed;
                else if (btn.Content != null)
                {
                    // Try converting via ToString
                    if (int.TryParse(btn.Content.ToString(), out var fromToString) && fromToString >= 1 && fromToString <= 31)
                        dayNum = fromToString;
                }

                if (dayNum.HasValue && datesWithItems.Contains(dayNum.Value))
                {
                    btn.Classes.Add("HasItems");
                    btn.Background = highlightBg;
                    btn.FontWeight = FontWeight.Bold;
                    btn.Foreground = highlightFg;
                }
            }
        }
        catch (Exception ex) { Console.WriteLine($"Calendar dots error: {ex.Message}"); }
    }

    private async void DeleteSchedule_Click(object? s, RoutedEventArgs e)
    {
        if (s is Button btn && btn.Tag is string id)
        {
            var sch = App.SchedulerViewModel.Schedules.FirstOrDefault(x => x.Id == id);
            if (sch != null)
            {
                await App.SchedulerViewModel.DeleteScheduleCommand.ExecuteAsync(sch);
                RefreshList();
                Avalonia.Threading.Dispatcher.UIThread.Post(() => UpdateCalendarDots(), Avalonia.Threading.DispatcherPriority.Background);
                if (CalendarControl.SelectedDate.HasValue)
                    UpdateDayView(CalendarControl.SelectedDate.Value);
            }
        }
    }
}
