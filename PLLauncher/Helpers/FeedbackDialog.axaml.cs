using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace PLLauncher.Helpers;

public partial class FeedbackDialog : Window
{
    private string? _photoPath;

    private const string RecipientEmail = "nikita.22rocky+pllauncher@gmail.com";

    public FeedbackDialog()
    {
        InitializeComponent();
        UpdateSubmitEnabled();
    }

    private void TitleBox_TextChanged(object? sender, Avalonia.Controls.TextChangedEventArgs e) => UpdateSubmitEnabled();
    private void DescBox_TextChanged(object? sender, Avalonia.Controls.TextChangedEventArgs e) => UpdateSubmitEnabled();

    private void UpdateSubmitEnabled()
    {
        SubmitBtn.IsEnabled =
            !string.IsNullOrWhiteSpace(TitleBox?.Text) &&
            !string.IsNullOrWhiteSpace(DescBox?.Text);
        SubmitBtn.Content = SubmitBtn.IsEnabled ? "Submit" : "Fill in required fields";
    }

    private async void AttachPhoto_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        try
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Select a screenshot",
                AllowMultiple = false,
                FileTypeFilter = new List<FilePickerFileType>
                {
                    new("Images") { Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.gif", "*.bmp" } }
                }
            });

            if (files.Count == 0) return;
            _photoPath = files[0].Path.LocalPath;

            try
            {
                PhotoPreview.Source = new Bitmap(_photoPath);
                PhotoPreview.IsVisible = true;
            }
            catch
            {
                PhotoPreview.IsVisible = false;
            }

            RemovePhotoBtn.IsVisible = true;
        }
        catch (Exception ex)
        {
            StatusMsg.Text = $"Could not open file picker: {ex.Message}";
            StatusMsg.IsVisible = true;
        }
    }

    private void RemovePhoto_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _photoPath = null;
        PhotoPreview.Source = null;
        PhotoPreview.IsVisible = false;
        RemovePhotoBtn.IsVisible = false;
    }

    private async void Submit_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var name = string.IsNullOrWhiteSpace(NameBox.Text) ? "Anonymous" : NameBox.Text.Trim();
        var title = TitleBox.Text.Trim();
        var type = (TypeCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Other";
        var description = DescBox.Text.Trim();

        // Save photo to temp folder for easy attachment
        string? savedPhotoPath = null;
        if (!string.IsNullOrEmpty(_photoPath) && File.Exists(_photoPath))
        {
            try
            {
                var ts = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var dir = Path.Combine(Path.GetTempPath(), "PLLauncher", "BugReports", ts);
                Directory.CreateDirectory(dir);
                var ext = Path.GetExtension(_photoPath);
                savedPhotoPath = Path.Combine(dir, $"screenshot{ext}");
                File.Copy(_photoPath, savedPhotoPath, true);
            }
            catch { }
        }

        var body = new StringBuilder();
        body.AppendLine($"{name} — PLLauncher");
        body.AppendLine();
        body.AppendLine(description);

        if (savedPhotoPath != null)
        {
            body.AppendLine();
            body.AppendLine("---");
            body.AppendLine($"Screenshot: {savedPhotoPath}");
        }

        var subject = $"[{type}] {title}";
        var mailto = $"mailto:{RecipientEmail}?subject={Uri.EscapeDataString(subject)}&body={Uri.EscapeDataString(body.ToString())}";

        try
        {
            Process.Start(new ProcessStartInfo(mailto) { UseShellExecute = true });

            StatusMsg.Text = savedPhotoPath != null
                ? $"Opened your email client. The screenshot is saved at:\n{savedPhotoPath}\n\nPlease drag it into the email before sending."
                : "Opened your email client with the pre-filled feedback.";

            StatusMsg.Foreground = new SolidColorBrush(Color.FromRgb(76, 175, 80));
            StatusMsg.IsVisible = true;
            CancelBtn.Content = "Close";
            SubmitBtn.IsVisible = false;
        }
        catch (Exception ex)
        {
            StatusMsg.Text = $"Could not open email client: {ex.Message}";
            StatusMsg.Foreground = new SolidColorBrush(Color.FromRgb(244, 67, 54));
            StatusMsg.IsVisible = true;
        }
    }

    private void Cancel_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close();
    }
}
