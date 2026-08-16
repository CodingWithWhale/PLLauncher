using Avalonia.Controls;
using Avalonia.Interactivity;
using PLLauncher.Services;
using System;
using System.IO;
using System.Reflection;
using System.Text;

namespace PLLauncher.Views;

/// <summary>
/// Displays a readable document (Terms &amp; Conditions, Privacy Policy, etc.)
/// in the current UI language.
/// </summary>
public partial class DocumentDialog : Window
{
    /// <summary>
    /// Default constructor required for the Avalonia runtime loader.
    /// Shows the Terms &amp; Conditions by default.
    /// </summary>
    public DocumentDialog() : this("terms", "terms.title") { }

    /// <param name="documentBase">Embedded resource base name, e.g. "terms" or "privacy".</param>
    /// <param name="titleKey">Localization key for the dialog title and heading.</param>
    public DocumentDialog(string documentBase, string titleKey)
    {
        InitializeComponent();
        Title = LocalizationService.Instance.Get(titleKey);
        TitleText.Text = LocalizationService.Instance.Get(titleKey);
        CloseBtn.Content = LocalizationService.Instance.Get("terms.close");
        DocumentText.Text = LoadDocument(documentBase);
    }

    private static string LoadDocument(string documentBase)
    {
        var resourceName = LocalizationService.Instance.CurrentLanguage switch
        {
            "ru-RU" => $"PLLauncher.Assets.{documentBase}_ru.txt",
            "zh-CN" => $"PLLauncher.Assets.{documentBase}_zh.txt",
            "es-ES" => $"PLLauncher.Assets.{documentBase}_es.txt",
            _ => $"PLLauncher.Assets.{documentBase}.txt"
        };
        try
        {
            var asm = Assembly.GetExecutingAssembly();
            using var stream = asm.GetManifestResourceStream(resourceName);
            if (stream == null) return string.Empty;
            using var reader = new StreamReader(stream, Encoding.UTF8);
            return reader.ReadToEnd();
        }
        catch
        {
            return string.Empty;
        }
    }

    private void Close_Click(object? sender, RoutedEventArgs e) => Close();
}
