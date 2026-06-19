using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace PLLauncher.Helpers;

public partial class FeedbackDialog : Window
{
    private string? _photoPath;
    private string? _savedPassword;
    private readonly TextBox _smtpPassBox;

    private const string DataFolder = "PLLauncher";
    private const string MailFile = "mail.dat";

    public FeedbackDialog()
    {
        InitializeComponent();
        _smtpPassBox = this.FindControl<TextBox>("SmtpPassBox")!;
        UpdateSubmitEnabled();
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        _savedPassword = LoadPassword();
        if (_savedPassword != null)
        {
            SmtpSection.IsVisible = false;
            StatusMsg.Text = "✓ Email configured";
            StatusMsg.Foreground = new SolidColorBrush(Color.FromRgb(76, 175, 80));
            StatusMsg.IsVisible = true;
        }
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
            SmtpError.Text = $"Could not open file picker: {ex.Message}";
            SmtpError.IsVisible = true;
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
        var password = _savedPassword;

        if (password == null)
        {
            SmtpSection.IsVisible = true;
            password = _smtpPassBox.Text;
            if (string.IsNullOrWhiteSpace(password))
            {
                SmtpError.Text = "Enter your Gmail App Password to send feedback.";
                SmtpError.IsVisible = true;
                return;
            }
        }

        await SendEmail(password);
    }

    private async Task SendEmail(string appPassword)
    {
        SubmitBtn.IsEnabled = false;
        SubmitBtn.Content = "Sending...";
        SmtpError.IsVisible = false;

        try
        {
            var name = string.IsNullOrWhiteSpace(NameBox.Text) ? "Anonymous" : NameBox.Text.Trim();
            var title = TitleBox.Text.Trim();
            var type = (TypeCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Other";
            var description = DescBox.Text.Trim();
            var email = "nikita.22rocky@gmail.com";

            var subject = $"[PLLauncher] [{type}] {title}";

            var body = new StringBuilder();
            body.AppendLine($"From: {name}");
            body.AppendLine($"Type: {type}");
            body.AppendLine();
            body.AppendLine(description);

            using var msg = new MailMessage(email, email, subject, body.ToString());

            if (!string.IsNullOrEmpty(_photoPath) && File.Exists(_photoPath))
            {
                var attachment = new Attachment(_photoPath);
                msg.Attachments.Add(attachment);
            }

            using var client = new SmtpClient("smtp.gmail.com", 587);
            client.EnableSsl = true;
            client.UseDefaultCredentials = false;
            client.Credentials = new NetworkCredential(email, appPassword);

            await client.SendMailAsync(msg);

            if (_savedPassword == null)
                SavePassword(appPassword);

            StatusMsg.Text = "Feedback sent! Thank you.";
            StatusMsg.Foreground = new SolidColorBrush(Color.FromRgb(76, 175, 80));
            StatusMsg.IsVisible = true;
            SmtpSection.IsVisible = false;
            SubmitBtn.IsVisible = false;
            CancelBtn.Content = "Close";
        }
        catch (SmtpException ex)
        {
            SmtpError.Text = $"Failed to send: {ex.Message}";
            SmtpError.Foreground = new SolidColorBrush(Color.FromRgb(244, 67, 54));
            SmtpError.IsVisible = true;
        }
        catch (Exception ex)
        {
            SmtpError.Text = $"Error: {ex.Message}";
            SmtpError.Foreground = new SolidColorBrush(Color.FromRgb(244, 67, 54));
            SmtpError.IsVisible = true;
        }
        finally
        {
            SubmitBtn.IsEnabled = true;
            SubmitBtn.Content = "Submit";
        }
    }

    private static string GetDataDir()
    {
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), DataFolder);
    }

    private string? LoadPassword()
    {
        try
        {
            var path = Path.Combine(GetDataDir(), MailFile);
            if (!File.Exists(path)) return null;
            var encrypted = File.ReadAllBytes(path);
            var decrypted = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(decrypted);
        }
        catch
        {
            return null;
        }
    }

    private void SavePassword(string password)
    {
        try
        {
            var dir = GetDataDir();
            Directory.CreateDirectory(dir);
            var encrypted = ProtectedData.Protect(Encoding.UTF8.GetBytes(password), null, DataProtectionScope.CurrentUser);
            File.WriteAllBytes(Path.Combine(dir, MailFile), encrypted);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Feedback] Failed to save password: {ex.Message}");
        }
    }

    private void Cancel_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close();
    }
}