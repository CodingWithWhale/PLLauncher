using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using System;

namespace PLLauncher.Helpers;

public partial class UpdateProgressDialog : Window
{
    private readonly DispatcherTimer _timer;
    private readonly RotateTransform _rotation;

    public UpdateProgressDialog()
    {
        InitializeComponent();
        _rotation = new RotateTransform();
        Spinner.RenderTransform = _rotation;
        Spinner.RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative);

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _timer.Tick += (_, _) =>
        {
            _rotation.Angle = (_rotation.Angle + 6) % 360;
        };
    }

    public void Start()
    {
        _rotation.Angle = 0;
        _timer.Start();
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        _timer.Stop();
    }
}