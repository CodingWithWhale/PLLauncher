using CommunityToolkit.Mvvm.ComponentModel;
using System;

namespace PLLauncher.Models;

public partial class KeybindItem : ObservableObject
{
    [ObservableProperty]
    private string _id = Guid.NewGuid().ToString();

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _keyCombo = string.Empty;

    [ObservableProperty]
    private KeybindActionType _actionType = KeybindActionType.OpenApp;

    [ObservableProperty]
    private string _actionTarget = string.Empty;

    [ObservableProperty]
    private bool _isEnabled = true;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private DateTime _createdAt = DateTime.Now;

    [ObservableProperty]
    private int[] _virtualKeys = Array.Empty<int>();

    [ObservableProperty]
    private int _modifiers = 0;
}

public enum KeybindActionType
{
    OpenApp,
    RunCommand,
    SystemAction,
    OpenUrl,
    OpenFolder
}
