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
    }

    private void OnTimerTick(object? sender, PomodoroEventArgs e)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            UpdateTimerDisplay(e.Remaining, e.Phase);
            SessionsLabel.Text = $"Sessions: {App.PomodoroService.SessionsCompleted}";
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
        TimerDisplay.Text = $"{(int)rem.TotalMinutes:D2}:{rem.Seconds:D2}";
        PhaseLabel.Text = ph == PomodoroPhase.Work ? "Work" : "Break";
    }

    private void UpdateButtonStates(bool isRunning)
    {
        StartBtn.IsEnabled = !isRunning;
        PauseBtn.IsEnabled = isRunning;
    }
}
