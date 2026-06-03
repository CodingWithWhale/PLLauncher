using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;

namespace PLLauncher.Models;

public partial class AppGroup : ObservableObject
{
    [ObservableProperty]
    private string _id = Guid.NewGuid().ToString();

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _icon = string.Empty; // Emoji or Segoe MDL2 icon code

    [ObservableProperty]
    private List<string> _appPaths = new();

    [ObservableProperty]
    private DateTime _createdAt = DateTime.Now;
}
