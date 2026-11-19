using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using EEE.Services;
using EEE.ViewModels;
using EESimulator.Core.Components;
using EESimulator.Engine.Simulation;

namespace EEE;

public sealed class EELoadingPage : ContentPage
{
    private readonly Label _progressLabel;
    private readonly CircularProgressIndicator _progressIndicator;
    private readonly IServiceProvider _services;
    private bool _loadingStarted;
    private double _progress;

    public EELoadingPage(IServiceProvider services)
    {
        _services = services;
        BackgroundColor = Color.FromArgb("#0D1117");

        _progressLabel = new Label
        {
            Text = "Preparing workspace…",
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

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_loadingStarted) return;
        _loadingStarted = true;

        await LoadApplicationResourcesAsync();
    }

    private async Task UpdateProgressAsync(double targetProgress, string statusText)
    {
        _progressLabel.Text = statusText;
        double start = _progress;
        int steps = 5;
        for (int i = 1; i <= steps; i++)
        {
            _progress = start + (targetProgress - start) * (i / (double)steps);
            _progressIndicator.Progress = _progress;
            await Task.Delay(10);
        }
        _progress = targetProgress;
        _progressIndicator.Progress = _progress;
    }

    private async Task LoadApplicationResourcesAsync()
    {
        try
        {
            // Stage 1: Component definitions & catalog registry
            await UpdateProgressAsync(0.12, "Loading electrical component registry…");
            await Task.Run(() =>
            {
                _ = ComponentRegistry.GetAllDefinitions().Count();
            });

            // Stage 2: Simulation engine & performance profiles
            await UpdateProgressAsync(0.28, "Initializing simulation solvers…");
            await Task.Run(() =>
            {
                _ = SimulationPerformanceProfile.Current;
                _ = new EESimulator.Engine.SimulationManager();
            });

            // Stage 3: Built-in circuit examples catalog
            await UpdateProgressAsync(0.44, "Loading circuit examples catalog…");
            await Task.Run(() =>
            {
                var examples = ExampleCircuitService.GetExamples();
                _ = examples.Count;
            });

            // Stage 4: User custom circuits from device storage
            await UpdateProgressAsync(0.58, "Loading custom circuit libraries…");
            var customCircuits = await CustomCircuitLibrary.LoadAsync();

            // Stage 5: Workspace settings, themes & timing configuration
            await UpdateProgressAsync(0.72, "Applying workspace configuration…");
            var settingsVm = _services.GetRequiredService<SettingsViewModel>();
            _ = settingsVm.SelectedCanvasTheme;

            // Stage 6: Workspace ViewModel & script synchronization
            await UpdateProgressAsync(0.86, "Initializing workspace view model…");
            var workspaceVm = _services.GetRequiredService<EEWorkspaceViewModel>();
            _ = workspaceVm.GroupedPalette.Count;

            // Stage 7: Pre-inflating Workspace Page and App Shell (prevents Android blank-screen delay)
            await UpdateProgressAsync(0.95, "Inflating workspace UI & canvas…");
            await Task.Yield();
            await Task.Delay(25);

            var shell = _services.GetRequiredService<AppShell>();
            var workspacePage = _services.GetRequiredService<WorkspacePage>();
            shell.AttachWorkspacePage(workspacePage);

            // Pre-measure workspace page so Android view hierarchy is inflated before transition
            try
            {
                workspacePage.Measure(double.PositiveInfinity, double.PositiveInfinity);
            }
            catch { }

            // Stage 8: Complete
            await UpdateProgressAsync(1.0, "Ready");
            await Task.Delay(60);

            var window = Application.Current?.Windows.FirstOrDefault(w => ReferenceEquals(w.Page, this))
                ?? throw new InvalidOperationException("The startup loading page is not attached to an application window.");
            window.Page = shell;
        }
        catch (Exception ex)
        {
            _progressLabel.Text = $"Startup notice: {ex.Message}";
            await Task.Delay(800);
            var shell = _services.GetRequiredService<AppShell>();
            var window = Application.Current?.Windows.FirstOrDefault(w => ReferenceEquals(w.Page, this))
                ?? Application.Current?.Windows.FirstOrDefault();
            if (window != null)
                window.Page = shell;
        }
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
            if (Progress >= 0.999)
            {
                // Full circle at 100%
                canvas.DrawCircle(dirtyRect.Center.X, dirtyRect.Center.Y, diameter / 2);
            }
            else if (Progress > 0.001)
            {
                float sweep = (float)(360 * Progress);
                // clockwise = true ensures the progress arc sweeps clockwise from 12 o'clock (-90 degrees)
                canvas.DrawArc(x, y, diameter, diameter, -90, -90 + sweep, true, false);
            }

            canvas.FontColor = Color.FromArgb("#E6EDF3");
            canvas.FontSize = 18;
            canvas.DrawString($"{Progress * 100:0}%", 0, 0, dirtyRect.Width, dirtyRect.Height,
                HorizontalAlignment.Center, VerticalAlignment.Center);
        }
    }
}
