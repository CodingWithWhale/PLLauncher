using Avalonia.Controls;
using PLLauncher.Services;
using PLLauncher.Views;
using System.Threading.Tasks;

namespace PLLauncher.Helpers;

public static class DialogHelper
{
    public static async Task<bool> ShowConfirmAsync(
        Window? owner,
        string message,
        string? title = null,
        string? confirmText = null,
        string? cancelText = null)
    {
        var loc = LocalizationService.Instance;
        title ??= loc.Get("confirm.title");
        confirmText ??= loc.Get("confirm.yes");
        cancelText ??= loc.Get("confirm.cancel");

        var dialog = new ConfirmDialog();
        dialog.Configure(message, title, confirmText, cancelText);

        owner ??= Avalonia.Application.Current?.ApplicationLifetime
            is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow
            : null;

        if (owner != null)
            await dialog.ShowDialog(owner);

        return dialog.Confirmed;
    }
}
