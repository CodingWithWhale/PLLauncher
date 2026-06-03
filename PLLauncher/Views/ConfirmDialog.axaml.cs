using Avalonia.Controls;
using Avalonia.Interactivity;

namespace PLLauncher.Views;

public partial class ConfirmDialog : Window
{
    public bool Confirmed { get; private set; }

    public ConfirmDialog()
    {
        InitializeComponent();
    }

    public void Configure(string message, string title, string confirmText, string cancelText)
    {
        Title = title;
        MessageText.Text = message;
        ConfirmBtn.Content = confirmText;
        CancelBtn.Content = cancelText;
    }

    private void Confirm_Click(object? sender, RoutedEventArgs e)
    {
        Confirmed = true;
        Close();
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e)
    {
        Confirmed = false;
        Close();
    }
}
