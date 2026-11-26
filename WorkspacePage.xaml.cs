using EEE.ViewModels;
using EEE.Services;
using EESimulator.Core.Models;
using EESimulator.Core.Components.Electrical;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Storage;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace EEE;

public partial class WorkspacePage : ContentPage
{
    private readonly EEWorkspaceViewModel _vm;
    private SchematicVideoRecorder? _videoRecorder;
    private readonly SemaphoreSlim _recordingLifecycle = new(1, 1);

    public WorkspacePage(EEWorkspaceViewModel vm)
    {
        _vm = vm;
        InitializeComponent();
        BindingContext = vm;
        vm.CircuitImageSaveRequested += (path, view) => _ = SaveCircuitImageAsync(path, view);
        vm.VideoRecordingStartRequested += (path, frameRate, format, view) => _ = StartVideoRecordingAsync(path, frameRate, format, view);
        vm.VideoRecordingStopRequested += () => _ = StopVideoRecordingAsync();

        // Wire canvas selection events to ViewModel
        MainWorkspace.SchematicCanvas.ComponentSelected += (_, comp) =>
            vm.OnComponentSelected(comp);
        MainWorkspace.SchematicCanvas.LogicInputToggled += (_, comp) =>
            vm.OnLogicInputToggled(comp);
        MainWorkspace.SchematicCanvas.ConnectionSelected += (_, connection) =>
            vm.OnConnectionSelected(connection);
        vm.ViewportRecenterRequested += MainWorkspace.SchematicCanvas.RecenterToCircuit;
        vm.ViewportZoomRequested += MainWorkspace.SchematicCanvas.ZoomBy;
        vm.ViewportFitToScreenRequested += MainWorkspace.SchematicCanvas.FitToScreen;
        vm.ViewportComponentPlacementRequested += MainWorkspace.SchematicCanvas.GetVisibleTopRightWorldPosition;
        MainWorkspace.SchematicCanvas.ComponentMoveCommitted += (_, move) =>
        {
            MainWorkspace.SchematicCanvas.InvalidateWireGeometryCache();
            vm.CommitComponentMove(move.Component, move.OldX, move.OldY, move.NewX, move.NewY);
        };

        // Wire pin/node selection event
        MainWorkspace.SchematicCanvas.PinSelected += (_, tuple) =>
            vm.OnPinSelected(tuple.Component, tuple.Pin);

        // Wire completed wire connection → add ConnectionNode to circuit
        MainWorkspace.SchematicCanvas.WireConnected += (_, args) =>
        {
            MainWorkspace.SchematicCanvas.InvalidateWireGeometryCache();
            var (fromComp, fromPin, toComp, toPin) = args;
            vm.ConnectPins(fromComp, fromPin, toComp, toPin);
        };

        // Wire cut wire turning into node
        MainWorkspace.SchematicCanvas.WireCutIntoNode += (_, junc) =>
        {
            MainWorkspace.SchematicCanvas.InvalidateWireGeometryCache();
            vm.StatusText = $"Cut wire → Created node {junc.Reference}";
            vm.SelectedComponent = junc;
        };

        // Wire CircuitChanged to refresh the schematic, waveforms, and selected icon after solver results.
        vm.CircuitChanged += () =>
            Dispatcher.Dispatch(() =>
            {
                MainWorkspace.SchematicCanvas.IsSimulationPlaying = vm.IsSimulationPlaying;
                MainWorkspace.SchematicCanvas.ElectricityAnimPhase = vm.ElectricityAnimPhase;
                MainWorkspace.SchematicCanvas.Invalidate();
                if (vm.IsDescriptionSidebarVisible)
                    MainWorkspace.SelectedComponentIcon.Invalidate();
            });

        vm.AnimationFrameRequested += () =>
        {
            // If the user is actively panning, pinching, or moving, give full priority to the interaction
            // so canvas zoom and pan never lag or stutter.
            if (MainWorkspace.SchematicCanvas.IsUserInteracting) return;

            MainWorkspace.SchematicCanvas.IsSimulationPlaying = vm.IsSimulationPlaying;
            MainWorkspace.SchematicCanvas.ElectricityAnimPhase = vm.ElectricityAnimPhase;
            MainWorkspace.SchematicCanvas.Invalidate();
        };

        // Initialize ActiveTool
        MainWorkspace.SchematicCanvas.SetActiveTool(vm.ActiveTool);

        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(EEWorkspaceViewModel.ActiveTool))
            {
                MainWorkspace.SchematicCanvas.SetActiveTool(vm.ActiveTool);
            }
        };
    }

    private async Task SaveCircuitImageAsync(string? path, string view = "recentre")
    {
        try
        {
            byte[] png = await MainThread.InvokeOnMainThreadAsync(() =>
            {
                var (width, height) = GetCaptureDimensions();
                bool useScreen = view.Equals("screen", StringComparison.OrdinalIgnoreCase);
                return useScreen
                    ? MainWorkspace.SchematicCanvas.CaptureCurrentViewPng(width, height)
                    : MainWorkspace.SchematicCanvas.CaptureRecenteredPng(width, height);
            });
            string destinationPath = ResolveCapturePath(path, ".png");
            await SchematicCaptureService.SavePngAsync(png, destinationPath);
            _vm.StatusText = $"Circuit image saved ({view} view) to {destinationPath}";
        }
        catch (Exception exception)
        {
            _vm.StatusText = $"Circuit image export failed: {exception.Message}";
        }
    }

    private async Task StartVideoRecordingAsync(string? path, int frameRate, string format, string view = "recentre")
    {
        await _recordingLifecycle.WaitAsync();
        try
        {
            if (_videoRecorder != null)
                throw new InvalidOperationException("A circuit video recording is already active.");

            bool useScreen = view.Equals("screen", StringComparison.OrdinalIgnoreCase);
            bool isGif = format.Equals("GIF", StringComparison.OrdinalIgnoreCase);
            var dimensions = await MainThread.InvokeOnMainThreadAsync(() =>
            {
                var (width, height) = GetCaptureDimensions(format);
                return (Width: width, Height: height,
                    ViewportWidth: (float)MainWorkspace.SchematicCanvas.Width,
                    ViewportHeight: (float)MainWorkspace.SchematicCanvas.Height);
            });
            string destinationPath = ResolveCapturePath(path, "." + format.ToLowerInvariant());
            var viewport = MainWorkspace.SchematicCanvas;

            Func<Task<EESimulator.App.Views.SchematicCapturedFrame>> captureFrameAsync = () =>
                MainThread.InvokeOnMainThreadAsync(() =>
                    useScreen
                        ? viewport.CaptureCurrentViewFrame(
                            dimensions.Width, dimensions.Height,
                            dimensions.ViewportWidth, dimensions.ViewportHeight,
                            includePng: !isGif)
                        : viewport.CaptureRecenteredFrame(
                            dimensions.Width, dimensions.Height,
                            includePng: !isGif));

            _videoRecorder = new SchematicVideoRecorder(
                dimensions.Width,
                dimensions.Height,
                frameRate,
                format,
                destinationPath,
                captureFrameAsync);

            _vm.SetVideoRecordingState(true, frameRate, format);
            _vm.StatusText = $"Recording {view} circuit view as {format} at {frameRate} fps to {destinationPath}";
        }
        catch (Exception exception)
        {
            _vm.SetVideoRecordingState(false, frameRate, format);
            _vm.StatusText = $"{format} recording could not start: {exception.Message}";
        }
        finally
        {
            _recordingLifecycle.Release();
        }
    }

    private async Task StopVideoRecordingAsync()
    {
        await _recordingLifecycle.WaitAsync();
        try
        {
            var recorder = _videoRecorder;
            _videoRecorder = null;
            if (recorder == null)
            {
                _vm.SetVideoRecordingState(false, _vm.VideoFrameRate, _vm.SelectedVideoFormat);
                return;
            }

            await Task.Run(async () => await recorder.StopAsync());
            _vm.SetVideoRecordingState(false, _vm.VideoFrameRate, _vm.SelectedVideoFormat);
            _vm.StatusText = $"Circuit {_vm.SelectedVideoFormat} recording saved";
        }
        catch (Exception exception)
        {
            _vm.SetVideoRecordingState(false, _vm.VideoFrameRate, _vm.SelectedVideoFormat);
            _vm.StatusText = $"{_vm.SelectedVideoFormat} recording failed: {exception.Message}";
        }
        finally
        {
            _recordingLifecycle.Release();
        }
    }

    private string ResolveCapturePath(string? path, string extension)
    {
        string defaultDir = SchematicCaptureService.GetDefaultMediaDirectory(extension);
        Directory.CreateDirectory(defaultDir);

        if (!string.IsNullOrWhiteSpace(path))
        {
            string candidate = path.Trim();
            if (Path.IsPathRooted(candidate))
            {
                string? candidateDir = Path.GetDirectoryName(Path.GetFullPath(candidate));
                if (!string.IsNullOrEmpty(candidateDir))
                    Directory.CreateDirectory(candidateDir);
                return SchematicCaptureService.EnsureExtension(candidate, extension);
            }
            return SchematicCaptureService.EnsureExtension(Path.Combine(defaultDir, candidate), extension);
        }

        string destination = Path.Combine(defaultDir, $"circuit-{DateTime.Now:yyyyMMdd-HHmmss-fff}{extension}");
        return SchematicCaptureService.EnsureExtension(destination, extension);
    }

    private (int Width, int Height) GetCaptureDimensions(string format = "GIF")
    {
        int width = (int)Math.Round(MainWorkspace.SchematicCanvas.Width);
        int height = (int)Math.Round(MainWorkspace.SchematicCanvas.Height);
        if (width <= 0 || height <= 0)
        {
            width = 800;
            height = 600;
        }

        if (format.Equals("GIF", StringComparison.OrdinalIgnoreCase))
        {
            const int maxDim = 640;
            if (width > maxDim || height > maxDim)
            {
                if (width >= height)
                {
                    height = Math.Max(2, (int)Math.Round((double)height * maxDim / width));
                    width = maxDim;
                }
                else
                {
                    width = Math.Max(2, (int)Math.Round((double)width * maxDim / height));
                    height = maxDim;
                }
            }
        }
        else if (format.Equals("MP4", StringComparison.OrdinalIgnoreCase))
        {
            const int maxDim = 1280;
            if (width > maxDim || height > maxDim)
            {
                if (width >= height)
                {
                    height = Math.Max(2, (int)Math.Round((double)height * maxDim / width));
                    width = maxDim;
                }
                else
                {
                    width = Math.Max(2, (int)Math.Round((double)width * maxDim / height));
                    height = maxDim;
                }
            }
            width = Math.Max(2, width & ~1);
            height = Math.Max(2, height & ~1);
        }
        return (width, height);
    }
}
