using Microsoft.Maui.Graphics;

namespace EEE;

public sealed class EELoadingPage : ContentPage
{
    private readonly Label _progressLabel;
    private readonly CircularProgressIndicator _progressIndicator;
    private readonly IServiceProvider _services;
    private IDispatcherTimer? _loaderTimer;
    private double _progress;

    public EELoadingPage(IServiceProvider services)
    {
        _services = services;
        BackgroundColor = Color.FromArgb("#0D1117");

        _progressLabel = new Label
        {
            Text = "Preparing workspace",
            TextColor = Color.FromArgb("#8B949E"),
            FontSize = 13,
            HorizontalTextAlignment = TextAlignment.Center
        };

        _progressIndicator = new CircularProgressIndicator
        {
            WidthRequest = 92,
            HeightRequest = 92,
            HorizontalOptions = LayoutOptions.Center
        };

        Content = new VerticalStackLayout
        {
            Spacing = 20,
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.Center,
            Children =
            {
                new Image { Source = "ee_engine.svg", WidthRequest = 220, HeightRequest = 220, Aspect = Aspect.AspectFit },
                _progressIndicator,
                _progressLabel
            }
        };
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (_loaderTimer != null) return;

        _loaderTimer = Dispatcher.CreateTimer();
        _loaderTimer.Interval = TimeSpan.FromMilliseconds(45);
        _loaderTimer.Tick += (_, _) =>
        {
            _progress = Math.Min(0.9, _progress + 0.025);
            _progressIndicator.Progress = _progress;

            if (_progress >= 0.9)
            {
                _loaderTimer.Stop();
                _progressLabel.Text = "Opening workspace";
                var shell = _services.GetRequiredService<AppShell>();
                _progress = 1;
                _progressIndicator.Progress = _progress;
                _progressLabel.Text = "Ready";
                var window = Application.Current?.Windows.FirstOrDefault(w => ReferenceEquals(w.Page, this))
                    ?? throw new InvalidOperationException("The startup loading page is not attached to an application window.");
                window.Page = shell;
            }
        };
        _loaderTimer.Start();
    }

    protected override void OnDisappearing()
    {
        _loaderTimer?.Stop();
        base.OnDisappearing();
    }

    private sealed class CircularProgressIndicator : GraphicsView, IDrawable
    {
        private double _progress;

        public double Progress
        {
            get => _progress;
            set
            {
                _progress = Math.Clamp(value, 0, 1);
                Invalidate();
            }
        }

        public CircularProgressIndicator()
        {
            Drawable = this;
        }

        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            float size = Math.Min(dirtyRect.Width, dirtyRect.Height);
            float inset = 7;
            float diameter = size - inset * 2;
            float x = (dirtyRect.Width - diameter) / 2;
            float y = (dirtyRect.Height - diameter) / 2;

            canvas.StrokeSize = 6;
            canvas.StrokeColor = Color.FromArgb("#30363D");
            canvas.DrawCircle(dirtyRect.Center.X, dirtyRect.Center.Y, diameter / 2);

            canvas.StrokeColor = Color.FromArgb("#58A6FF");
            canvas.DrawArc(x, y, diameter, diameter, -90, -90 + (float)(360 * Progress), false, false);

            canvas.FontColor = Color.FromArgb("#E6EDF3");
            canvas.FontSize = 18;
            canvas.DrawString($"{Progress * 100:0}%", 0, 0, dirtyRect.Width, dirtyRect.Height,
                HorizontalAlignment.Center, VerticalAlignment.Center);
        }
    }
}
