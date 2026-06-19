using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using System;

namespace PLLauncher.Helpers;

public partial class ColorPickerDialog : Window
{
    private bool _isDragging;

    public bool IsConfirmed { get; private set; }
    public Color SelectedColor { get; private set; } = Color.FromRgb(0x60, 0xCD, 0xFF);
    public string SelectedColorName { get; private set; } = "";

    private double _hueX;
    private double _satY;
    private double _brightness = 1.0;

    public ColorPickerDialog()
    {
        InitializeComponent();
        DrawRainbowGradient();
    }

    private void DrawRainbowGradient()
    {
        Dispatcher.UIThread.Post(() =>
        {
            var canvas = ColorCanvas;
            if (canvas == null) return;
            canvas.Background = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0.5, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 0.5, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop { Color = Colors.Red, Offset = 0.0 },
                    new GradientStop { Color = Colors.Yellow, Offset = 0.17 },
                    new GradientStop { Color = Colors.Lime, Offset = 0.33 },
                    new GradientStop { Color = Colors.Cyan, Offset = 0.50 },
                    new GradientStop { Color = Colors.Blue, Offset = 0.67 },
                    new GradientStop { Color = Colors.Magenta, Offset = 0.83 },
                    new GradientStop { Color = Colors.Red, Offset = 1.0 },
                }
            };
        });
    }

    private Color GetColorAtPosition(Point pos)
    {
        var w = ColorCanvas.Bounds.Width;
        var h = ColorCanvas.Bounds.Height;
        if (w <= 0 || h <= 0) return SelectedColor;

        var hueRatio = Math.Clamp(pos.X / w, 0, 1);
        var satRatio = 1.0 - Math.Clamp(pos.Y / h, 0, 1);

        var hue = (int)(hueRatio * 360);
        var sat = satRatio;
        var val = _brightness;

        return ColorFromHSV(hue, sat, val);
    }

    private static Color ColorFromHSV(int hue, double saturation, double value)
    {
        var h = hue % 360;
        var s = Math.Clamp(saturation, 0.0, 1.0);
        var v = Math.Clamp(value, 0.0, 1.0);

        var c = v * s;
        var x = c * (1 - Math.Abs((h / 60.0) % 2 - 1));
        var m = v - c;

        double r, g, b;
        if (h < 60) { r = c; g = x; b = 0; }
        else if (h < 120) { r = x; g = c; b = 0; }
        else if (h < 180) { r = 0; g = c; b = x; }
        else if (h < 240) { r = 0; g = x; b = c; }
        else if (h < 300) { r = x; g = 0; b = c; }
        else { r = c; g = 0; b = x; }

        return Color.FromRgb(
            (byte)((r + m) * 255),
            (byte)((g + m) * 255),
            (byte)((b + m) * 255));
    }

    private void UpdatePreview(Color color)
    {
        SelectedColor = color;
        ColorPreview.Background = new SolidColorBrush(color);
        var canvas = ColorCanvas;
        if (canvas != null)
            canvas.Background = CreateSaturationOverlay(color);
    }

    private Brush CreateSaturationOverlay(Color hueColor)
    {
        var w = ColorCanvas.Bounds.Width;
        var h = ColorCanvas.Bounds.Height;
        if (w <= 0 || h <= 0) return new SolidColorBrush(hueColor);

        return new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
            EndPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop { Color = Colors.White, Offset = 0.0 },
                new GradientStop { Color = hueColor, Offset = 1.0 }
            }
        };
    }

    private void ColorCanvas_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        _isDragging = true;
        var pos = e.GetPosition(ColorCanvas);
        _hueX = pos.X;
        _satY = pos.Y;
        var color = GetColorAtPosition(pos);
        color = ApplyBrightness(color, _brightness);
        UpdatePreview(color);
    }

    private void ColorCanvas_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isDragging) return;
        var pos = e.GetPosition(ColorCanvas);
        _hueX = pos.X;
        _satY = pos.Y;
        var color = GetColorAtPosition(pos);
        color = ApplyBrightness(color, _brightness);
        UpdatePreview(color);
    }

    private void ColorCanvas_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _isDragging = false;
    }

    private void BrightnessSlider_ValueChanged(object? sender, Avalonia.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        _brightness = e.NewValue;
        if (_hueX > 0 || _satY > 0)
        {
            var pos = new Point(_hueX, _satY);
            var color = GetColorAtPosition(pos);
            color = ApplyBrightness(color, _brightness);
            UpdatePreview(color);
        }
        else
        {
            var baseColor = Color.FromRgb(0x60, 0xCD, 0xFF);
            var color = ApplyBrightness(baseColor, _brightness);
            UpdatePreview(color);
        }
    }

    private static Color ApplyBrightness(Color color, double brightness)
    {
        return Color.FromRgb(
            (byte)(color.R * brightness),
            (byte)(color.G * brightness),
            (byte)(color.B * brightness));
    }

    private void Cancel_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        IsConfirmed = false;
        Close();
    }

    private void Ok_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        IsConfirmed = true;
        Close();
    }
}
