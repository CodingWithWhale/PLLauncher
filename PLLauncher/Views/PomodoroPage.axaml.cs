using Avalonia.Controls;
using Avalonia.Interactivity;
using PLLauncher.Services;
using System;

namespace PLLauncher.Views;

public partial class PomodoroPage : UserControl
{
    public PomodoroPage()
    {
        InitializeComponent();
        UpdateTimerDisplay();
        
        // Subscribe to timer events
        App.PomodoroService.TimerTick += OnTimerTick;
        App.PomodoroService.PhaseChanged += OnPhaseChanged;
        
        // Health toggle
        HealthToggle.IsCheckedChanged += HealthToggle_Changed;

        LocalizationService.Instance.LanguageChanged += (_, _) => ApplyLocalizedText();
    }

    private void OnTimerTick(object? sender, PomodoroEventArgs e)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            UpdateTimerDisplay(e.Remaining, e.Phase);
            var loc = LocalizationService.Instance;
            SessionsLabel.Text = $"{loc.Get("pomodoro.sessions")}: {App.PomodoroService.SessionsCompleted}";
        });
    }

    private void OnPhaseChanged(object? sender, PomodoroPhase phase)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            UpdateTimerDisplay(App.PomodoroService.Remaining, phase);
            UpdateButtonStates(false);
            // Auto-start next phase
            App.PomodoroService.Start();
            UpdateButtonStates(true);
        });
    }

    private void Start_Click(object? s, RoutedEventArgs e)
    {
        ApplySettings();
        App.PomodoroService.Start();
        UpdateButtonStates(true);
    }

    private void Pause_Click(object? s, RoutedEventArgs e)
    {
        App.PomodoroService.Pause();
        UpdateButtonStates(false);
    }

    private void ApplyLocalizedText()
    {
        var loc = LocalizationService.Instance;
        PomodoroTitle.Text = loc.Get("pomodoro.title");
        PomodoroSubtitle.Text = loc.Get("pomodoro.subtitle");
        StartBtnText.Text = loc.Get("pomodoro.start");
        PauseBtnText.Text = loc.Get("pomodoro.pause");
        ResetBtnText.Text = loc.Get("pomodoro.reset");
        SkipBtnText.Text = loc.Get("pomodoro.skip");
        TimerSettingsTitle.Text = loc.Get("pomodoro.timer_settings");
        WorkMinutesLabel.Text = loc.Get("pomodoro.work_minutes");
        BreakMinutesLabel.Text = loc.Get("pomodoro.break_minutes");
        HealthReminderTitle.Text = loc.Get("pomodoro.health_reminder");
        HealthReminderDesc.Text = loc.Get("pomodoro.health_desc");
        ReminderIntervalLabel.Text = loc.Get("pomodoro.reminder_interval");
    }

    private void Reset_Click(object? s, RoutedEventArgs e)
    {
        ApplySettings();
        App.PomodoroService.Reset();
        UpdateTimerDisplay();
        UpdateButtonStates(false);
    }

    private void Skip_Click(object? s, RoutedEventArgs e)
    {
        App.PomodoroService.Skip();
        UpdateButtonStates(false);
    }

    private void ApplySettings()
    {
        App.PomodoroService.UpdateWorkMinutes((int)(WorkMinutesBox.Value ?? 25m));
        App.PomodoroService.UpdateBreakMinutes((int)(BreakMinutesBox.Value ?? 5m));
    }

    private void HealthToggle_Changed(object? sender, RoutedEventArgs e)
    {
        if (HealthToggle.IsChecked == true)
        {
            var interval = (int)(HealthIntervalBox.Value ?? 60m);
            App.HealthReminderService.Enable(interval);
        }
        else
        {
            App.HealthReminderService.Disable();
        }
    }

    private void UpdateTimerDisplay(TimeSpan? remaining = null, PomodoroPhase? phase = null)
    {
        var rem = remaining ?? App.PomodoroService.Remaining;
        var ph = phase ?? App.PomodoroService.CurrentPhase;
        var loc = LocalizationService.Instance;
        TimerDisplay.Text = $"{(int)rem.TotalMinutes:D2}:{rem.Seconds:D2}";
        PhaseLabel.Text = ph == PomodoroPhase.Work ? loc.Get("pomodoro.work") : loc.Get("pomodoro.break");
    }

    private void UpdateButtonStates(bool isRunning)
    {
        StartBtn.IsEnabled = !isRunning;
        PauseBtn.IsEnabled = isRunning;
    }
}
