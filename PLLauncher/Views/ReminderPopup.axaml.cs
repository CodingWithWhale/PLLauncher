using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using PLLauncher.Models;
using System;

namespace PLLauncher.Views;

public partial class ReminderPopup : Window
{
    public ReminderPopup() : this(new ScheduleItem()) { }

    public ReminderPopup(ScheduleItem reminder)
    {
        InitializeComponent();

        // Fill in details
        ReminderTitle.Text = reminder.Name;

        if (!string.IsNullOrEmpty(reminder.ReminderMessage))
        {
            ReminderMessage.Text = reminder.ReminderMessage;
            MessageBorder.IsVisible = true;
        }

        // Style based on urgency
        switch (reminder.Urgency)
        {
            case ReminderUrgency.High:
                UrgencyLabel.Text = "🔴 High Priority";
                UrgencyLabel.Foreground = new SolidColorBrush(Color.FromRgb(0xF4, 0x43, 0x36));
                RootBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(0xF4, 0x43, 0x36));
                UrgencyIcon.Background = new SolidColorBrush(Color.FromArgb(0x33, 0xF4, 0x43, 0x36));
                UrgencyIconText.Foreground = new SolidColorBrush(Color.FromRgb(0xF4, 0x43, 0x36));
                UrgencyIconText.Text = "\uE783"; // warning shield
                break;
            case ReminderUrgency.Medium:
                UrgencyLabel.Text = "🟡 Medium Priority";
                UrgencyLabel.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x98, 0x00));
                RootBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0x98, 0x00));
                UrgencyIcon.Background = new SolidColorBrush(Color.FromArgb(0x33, 0xFF, 0x98, 0x00));
                UrgencyIconText.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x98, 0x00));
                UrgencyIconText.Text = "\uE7BA"; // warning
                break;
            default: // Low
                UrgencyLabel.Text = "🟢 Low Priority";
                UrgencyLabel.Foreground = new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50));
                RootBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50));
                UrgencyIcon.Background = new SolidColorBrush(Color.FromArgb(0x33, 0x4C, 0xAF, 0x50));
                UrgencyIconText.Foreground = new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50));
                UrgencyIconText.Text = "\uE73E"; // checkmark
                break;
        }

        // Position in top-right corner
        PositionInTopRight();
    }

    private void PositionInTopRight()
    {
        try
        {
            var screen = Screens.Primary;
            if (screen != null)
            {
                var wa = screen.WorkingArea;
                var x = wa.X + wa.Width - (int)Width - 20;
                var y = wa.Y + 20;
                Position = new PixelPoint(x, y);
            }
        }
        catch { }
    }

    private void Dismiss_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        AutoCloseAfterDelay();
    }

    private async void AutoCloseAfterDelay()
    {
        try
        {
            await System.Threading.Tasks.Task.Delay(TimeSpan.FromMinutes(10));
            if (this.IsVisible)
                Close();
        }
        catch { }
    }
}
