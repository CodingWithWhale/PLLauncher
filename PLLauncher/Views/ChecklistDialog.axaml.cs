using Avalonia.Controls;
using Avalonia.Interactivity;
using System.Collections.Generic;
using System.Linq;

namespace PLLauncher.Views;

public partial class ChecklistDialog : Window
{
    private readonly Dictionary<string, CheckBox> _checkboxes = new();

    public List<string> SelectedItems { get; private set; } = new();

    public ChecklistDialog()
    {
        InitializeComponent();
    }

    public void Configure(string title, Dictionary<string, string> items, List<string>? preSelected = null)
    {
        Title = title;
        TitleText.Text = title;
        CheckboxPanel.Children.Clear();
        _checkboxes.Clear();

        foreach (var kvp in items)
        {
            var cb = new CheckBox
            {
                Content = kvp.Value,
                IsChecked = preSelected?.Contains(kvp.Key) ?? true,
                Margin = new Avalonia.Thickness(0, 2, 0, 2)
            };
            _checkboxes[kvp.Key] = cb;
            CheckboxPanel.Children.Add(cb);
        }
    }

    private void Confirm_Click(object? sender, RoutedEventArgs e)
    {
        SelectedItems = _checkboxes
            .Where(kvp => kvp.Value.IsChecked == true)
            .Select(kvp => kvp.Key)
            .ToList();
        Close();
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e)
    {
        SelectedItems = new();
        Close();
    }
}
