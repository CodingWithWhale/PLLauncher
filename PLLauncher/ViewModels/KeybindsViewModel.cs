using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PLLauncher.Models;
using PLLauncher.Services;

namespace PLLauncher.ViewModels;

public partial class KeybindsViewModel : ObservableObject
{
    private readonly DataService _dataService;
    private readonly HotkeyService _hotkeyService;

    [ObservableProperty] private ObservableCollection<KeybindItem> _keybinds = new();
    [ObservableProperty] private KeybindItem? _selectedKeybind;
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private bool _isAddingNew;
    [ObservableProperty] private string _newKeyName = string.Empty;
    [ObservableProperty] private string _newKeyCombo = string.Empty;
    [ObservableProperty] private KeybindActionType _newActionType = KeybindActionType.OpenApp;
    [ObservableProperty] private string _newActionTarget = string.Empty;
    [ObservableProperty] private string _conflictMessage = string.Empty;
    [ObservableProperty] private bool _hasConflict;

    public KeybindsViewModel(DataService ds, HotkeyService hs) { _dataService = ds; _hotkeyService = hs; }

    public ObservableCollection<KeybindItem> FilteredKeybinds
    {
        get
        {
            if (string.IsNullOrWhiteSpace(SearchText)) return Keybinds;
            return new(Keybinds.Where(k =>
                k.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                k.KeyCombo.Contains(SearchText, StringComparison.OrdinalIgnoreCase)));
        }
    }

    [RelayCommand]
    private async Task LoadKeybindsAsync()
    { Keybinds = new(await _dataService.LoadKeybindsAsync()); OnPropertyChanged(nameof(FilteredKeybinds)); }

    [RelayCommand]
    private async Task AddKeybindAsync()
    {
        if (string.IsNullOrWhiteSpace(NewKeyCombo) || string.IsNullOrWhiteSpace(NewKeyName)) return;
        if (_hotkeyService.IsConflict(NewKeyCombo))
        { ConflictMessage = "Key combination already in use!"; HasConflict = true; return; }

        var kb = new KeybindItem { Name = NewKeyName, KeyCombo = NewKeyCombo,
            ActionType = NewActionType, ActionTarget = NewActionTarget, IsEnabled = true };

        // Try to register the hotkey with the OS
        bool registered = _hotkeyService.RegisterHotkey(kb);

        if (!registered)
        {
            // Show the specific error from the hotkey service
            ConflictMessage = _hotkeyService.LastError;
            HasConflict = true;
            return;
        }

        // Always save the keybind to the list
        Keybinds.Add(kb);
        await _dataService.SaveKeybindsAsync(Keybinds.ToList());
        OnPropertyChanged(nameof(FilteredKeybinds));
        NewKeyName = ""; NewKeyCombo = ""; NewActionTarget = "";
        IsAddingNew = false; HasConflict = false; ConflictMessage = "";
    }

    [RelayCommand]
    private async Task DeleteKeybindAsync(KeybindItem? kb)
    {
        if (kb == null) return;
        _hotkeyService.UnregisterHotkey(kb);
        Keybinds.Remove(kb);
        await _dataService.SaveKeybindsAsync(Keybinds.ToList());
        OnPropertyChanged(nameof(FilteredKeybinds));
    }

    [RelayCommand]
    private async Task ToggleKeybindAsync(KeybindItem kb)
    {
        if (kb.IsEnabled) _hotkeyService.UnregisterHotkey(kb); else _hotkeyService.RegisterHotkey(kb);
        kb.IsEnabled = !kb.IsEnabled;
        await _dataService.SaveKeybindsAsync(Keybinds.ToList());
    }

    partial void OnSearchTextChanged(string value) => OnPropertyChanged(nameof(FilteredKeybinds));
}
