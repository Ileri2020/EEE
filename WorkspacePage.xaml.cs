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
    // UI-side capture timer: fires on the main thread so frame rendering never
    // needs InvokeOnMainThreadAsync (which would block background threads and
    // starve pan/gesture handling).
    private Microsoft.Maui.Dispatching.IDispatcherTimer? _captureTimer;
    private Microsoft.Maui.Dispatching.IDispatcherTimer? _progressTimer;
    private System.Diagnostics.Stopwatch? _recordingStopwatch;
    private double? _recordingDuration;
    private readonly SemaphoreSlim _recordingLifecycle = new(1, 1);

    public WorkspacePage(EEWorkspaceViewModel vm)
    {
        _vm = vm;
        InitializeComponent();
        BindingContext = vm;
        vm.CircuitImageSaveRequested += (path, view) => _ = SaveCircuitImageAsync(path, view);
        vm.VideoRecordingStartRequested += (path, frameRate, format, view, duration) => _ = StartVideoRecordingAsync(path, frameRate, format, view, duration);
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
                if (!MainWorkspace.SchematicCanvas.IsUserInteracting)
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

    private async Task StartVideoRecordingAsync(string? path, int frameRate, string format, string view = "recentre", double? duration = null)
    {
        await _recordingLifecycle.WaitAsync();
        try
        {
            if (_videoRecorder != null)
                throw new InvalidOperationException("A circuit video recording is already active.");

            // ── Force schematic-only view when triggered from Python script ───
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                _vm.IsAnalyticsViewActive = false;
                _vm.IsScriptViewActive = false;
            });

            bool useScreen = view.Equals("screen", StringComparison.OrdinalIgnoreCase);
            bool isGif = format.Equals("GIF", StringComparison.OrdinalIgnoreCase);

            // Grab dimensions on the main thread (we may already be there, but be safe).
            var dimensions = MainThread.IsMainThread
                ? GetCaptureDimensions(format, (float)MainWorkspace.SchematicCanvas.Width, (float)MainWorkspace.SchematicCanvas.Height)
                : await MainThread.InvokeOnMainThreadAsync(() =>
                    GetCaptureDimensions(format, (float)MainWorkspace.SchematicCanvas.Width, (float)MainWorkspace.SchematicCanvas.Height));

            string destinationPath = ResolveCapturePath(path, "." + format.ToLowerInvariant());
            var viewport = MainWorkspace.SchematicCanvas;

            // Create recorder (encoding-only — no capture task inside).
            _videoRecorder = new SchematicVideoRecorder(
                dimensions.Width,
                dimensions.Height,
                frameRate,
                format,
                destinationPath);

            // ── Duration + progress tracking ──────────────────────────────────
            _recordingDuration = duration;
            _recordingStopwatch = System.Diagnostics.Stopwatch.StartNew();

            // ── UI-side capture timer ─────────────────────────────────────────
            // This timer fires on the main thread at exactly 1000/fps ms intervals.
            var recorder = _videoRecorder;
            int maxFrames = duration.HasValue
                ? (int)Math.Ceiling(duration.Value * frameRate) + frameRate  // cap at duration + 1s buffer
                : frameRate * 600; // default 10-minute cap
            _captureTimer = Dispatcher.CreateTimer();
            _captureTimer.Interval = TimeSpan.FromMilliseconds(1000d / frameRate);
            _captureTimer.IsRepeating = true;
            _captureTimer.Tick += (_, _) =>
            {
                // Auto-stop when duration reached.
                if (_recordingDuration.HasValue && _recordingStopwatch != null &&
                    _recordingStopwatch.Elapsed.TotalSeconds >= _recordingDuration.Value)
                {
                    _captureTimer?.Stop();
                    _progressTimer?.Stop();
                    _ = StopVideoRecordingAsync();
                    return;
                }
                // Skip frame if user is interacting so gestures get full priority.
                if (viewport.IsUserInteracting)
                    return;
                // Stop automatically at the frame cap or if the recorder stopped.
                if (recorder.IsStopped || recorder.FrameCount >= maxFrames)
                {
                    _captureTimer?.Stop();
                    _progressTimer?.Stop();
                    return;
                }
                // Render a frame synchronously on this main-thread tick — fast, non-blocking.
                try
                {
                    var frame = useScreen
                        ? viewport.CaptureCurrentViewFrame(
                            dimensions.Width, dimensions.Height,
                            dimensions.ViewportWidth, dimensions.ViewportHeight,
                            includePng: !isGif)
                        : viewport.CaptureRecenteredFrame(
                            dimensions.Width, dimensions.Height,
                            includePng: !isGif);
                    // Push the pixel data to the encoding channel (non-blocking, drops
                    // oldest if the encoder cannot keep up — far better than blocking pan).
                    recorder.TryAddFrame(frame);
                }
                catch
                {
                    // Silently swallow individual frame errors so a single bad frame
                    // does not abort the whole recording session.
                }
            };
            _captureTimer.Start();

            // ── Console progress timer (1 Hz) ─────────────────────────────────
            _progressTimer = Dispatcher.CreateTimer();
            _progressTimer.Interval = TimeSpan.FromSeconds(1);
            _progressTimer.IsRepeating = true;
            _progressTimer.Tick += (_, _) =>
            {
                if (_recordingStopwatch == null || recorder.IsStopped) return;
                double elapsed = _recordingStopwatch.Elapsed.TotalSeconds;
                string durPart = _recordingDuration.HasValue ? $" / {_recordingDuration:F1}s" : "";
                _vm.ConsoleOutput += $"\n● REC {elapsed:F1}s{durPart} — {recorder.FrameCount} frames";
            };
            _progressTimer.Start();
            // ─────────────────────────────────────────────────────────────────

            _vm.SetVideoRecordingState(true, frameRate, format);
            string durInfo = duration.HasValue ? $", auto-stops at {duration:F1}s" : "";
            _vm.StatusText = $"Recording {view} circuit view as {format} at {frameRate} fps to {destinationPath}{durInfo}";
            _vm.ConsoleOutput += $"\n● REC started — {format} {frameRate} fps{durInfo}\n● Output: {destinationPath}";
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
            // Stop the UI-side capture timer and progress timer first so no more frames are submitted.
            var timer = _captureTimer;
            _captureTimer = null;
            timer?.Stop();

            var progressTimer = _progressTimer;
            _progressTimer = null;
            progressTimer?.Stop();

            _recordingStopwatch?.Stop();
            _recordingStopwatch = null;
            _recordingDuration = null;

            var recorder = _videoRecorder;
            _videoRecorder = null;
            if (recorder == null)
            {
                _vm.SetVideoRecordingState(false, _vm.VideoFrameRate, _vm.SelectedVideoFormat);
                return;
            }

            // StopAsync signals the encoding channel and flushes — run on a background
            // thread so we don't block the UI during the final file-write / FileSaver dialog.
            await Task.Run(async () => await recorder.StopAsync());
            _vm.SetVideoRecordingState(false, _vm.VideoFrameRate, _vm.SelectedVideoFormat);
            _vm.ConsoleOutput += $"\n● REC stopped — {recorder.FrameCount} frames saved";
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
        var d = GetCaptureDimensions(format, width, height);
        return (d.Width, d.Height);
    }

    /// <summary>
    /// Overload that accepts the raw canvas dimensions directly — safe to call
    /// from any context once the values have been read on the main thread.
    /// Returns clamped pixel dimensions AND the original viewport size for
    /// current-view scaling calculations.
    /// </summary>
    private static (int Width, int Height, float ViewportWidth, float ViewportHeight)
        GetCaptureDimensions(string format, float viewportWidth, float viewportHeight)
    {
        int width  = (int)Math.Round(viewportWidth);
        int height = (int)Math.Round(viewportHeight);
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
            width  = Math.Max(2, width  & ~1);
            height = Math.Max(2, height & ~1);
        }
        return (width, height, viewportWidth, viewportHeight);
    }
}
