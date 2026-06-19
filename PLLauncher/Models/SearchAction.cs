using System;

namespace PLLauncher.Models;

public record SearchAction(string Title, string Icon, string Keywords, Action Action, bool ClosesSearch = true);
