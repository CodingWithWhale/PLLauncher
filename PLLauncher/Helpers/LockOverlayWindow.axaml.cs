using Avalonia.Controls;
using Avalonia.Threading;
using PLLauncher.Models;
using System;

namespace PLLauncher.Helpers;

public partial class LockOverlayWindow : Window
{
    public bool IsClosed { get; private set; }
    public bool DismissedByUser { get; private set; }
    private readonly DispatcherTimer _countdownTimer;
    private readonly TimeLimitItem _limit;

    public LockOverlayWindow(TimeLimitItem limit)
    {
        InitializeComponent();
        _limit = limit;

        var msg = $"The limit for \"{limit.AppName}\" of {FormatLimit(limit.DailyLimitMinutes)} is hit.";
        TitleText.Text = msg;
        MessageText.Text = $"\"{limit.AppName}\" is locked";

        CloseBtn.Click += (_, _) =>
        {
            _limit.SuppressAutoLaunch = true;
            Close();
        };
        DismissBtn.Click += (_, _) =>
        {
            _limit.SuppressAutoLaunch = true;
            DismissedByUser = true;
            Close();
        };

        _countdownTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _countdownTimer.Tick += OnCountdownTick;
        _countdownTimer.Start();
        UpdateCountdown();
    }

    private void OnCountdownTick(object? sender, EventArgs e)
    {
        if (IsClosed) return;
        if (!_limit.IsInCooldown || !_limit.CooldownEndAt.HasValue || DateTime.Now >= _limit.CooldownEndAt.Value)
        {
            IsClosed = true;
            _countdownTimer.Stop();
            Close();
            return;
        }
        UpdateCountdown();
    }

    private void UpdateCountdown()
    {
        if (!_limit.CooldownEndAt.HasValue) return;
        var remaining = _limit.CooldownEndAt.Value - DateTime.Now;
        if (remaining.TotalSeconds <= 0)
        {
            CountdownText.Text = "Unlocking...";
            return;
        }
        var parts = new System.Collections.Generic.List<string>();
        if ((int)remaining.TotalHours > 0)
            parts.Add($"{(int)remaining.TotalHours} Hour(s)");
        if (remaining.Minutes > 0)
            parts.Add($"{remaining.Minutes} Minute(s)");
        parts.Add($"{remaining.Seconds} Second(s)");
        CountdownText.Text = string.Join(" ", parts) + " left";
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        IsClosed = true;
        _countdownTimer.Stop();
    }

    private static string FormatLimit(double minutes)
    {
        var totalMinutes = (int)Math.Ceiling(minutes);
        var h = totalMinutes / 60;
        var m = totalMinutes % 60;
        if (h > 0 && m > 0) return $"{h}h {m}m";
        if (h > 0) return $"{h}h";
        return $"{m}m";
    }
}
