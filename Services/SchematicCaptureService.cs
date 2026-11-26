using CommunityToolkit.Maui.Storage;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace EEE.Services;

internal static class SchematicCaptureService
{
    public static async Task SavePngAsync(byte[] png, string? path)
    {
        if (!string.IsNullOrWhiteSpace(path))
        {
            string destination = EnsureExtension(path, ".png");
            string? directory = Path.GetDirectoryName(Path.GetFullPath(destination));
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
            await File.WriteAllBytesAsync(destination, png);
            return;
        }

        using var stream = new MemoryStream(png, writable: false);
        var result = await FileSaver.Default.SaveAsync("circuit.png", stream, CancellationToken.None);
        if (!result.IsSuccessful)
            throw result.Exception ?? new OperationCanceledException("Circuit image export was cancelled.");
    }

    public static string EnsureExtension(string path, string extension)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("A file path is required.", nameof(path));
        string actualExtension = Path.GetExtension(path);
        if (string.IsNullOrEmpty(actualExtension))
            return path + extension;
        if (!actualExtension.Equals(extension, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException($"The file path must use the {extension} extension.", nameof(path));
        return path;
    }
}

internal sealed class SchematicVideoRecorder : IAsyncDisposable
{
    private readonly string? _destinationPath;
    private readonly string _workingPath;
    private readonly int _frameRate;
    private readonly string _format;
    private readonly Func<EESimulator.App.Views.SchematicCapturedFrame> _captureFrame;
    private readonly FileStream? _stream;
    private readonly AnimatedGifWriter? _gifWriter;
    private readonly IPlatformVideoEncoder? _platformEncoder;
    private readonly CancellationTokenSource _cancellation = new();
    private readonly Task _recordingTask;
    private int _frameCount;
    private bool _stopped;

    public SchematicVideoRecorder(
        int width,
        int height,
        int frameRate,
        string format,
        string? destinationPath,
        Func<EESimulator.App.Views.SchematicCapturedFrame> captureFrame)
    {
        _frameRate = frameRate is >= 1 and <= 15
            ? frameRate
            : throw new ArgumentOutOfRangeException(nameof(frameRate), "Recording frame rate must be between 1 and 15 fps.");
        _format = format.Equals("GIF", StringComparison.OrdinalIgnoreCase) ? "gif" :
            format.Equals("MP4", StringComparison.OrdinalIgnoreCase) ? "mp4" :
            throw new ArgumentException("Video format must be GIF or MP4.", nameof(format));
        _destinationPath = string.IsNullOrWhiteSpace(destinationPath)
            ? null
            : SchematicCaptureService.EnsureExtension(destinationPath, "." + _format);
        _workingPath = _destinationPath ??
            Path.Combine(Path.GetTempPath(), $"eee-circuit-{Guid.NewGuid():N}.{_format}");
        if (_destinationPath != null)
        {
            string? directory = Path.GetDirectoryName(Path.GetFullPath(_destinationPath));
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
        }
        _captureFrame = captureFrame ?? throw new ArgumentNullException(nameof(captureFrame));
        if (_format == "gif")
        {
            var stream = new FileStream(_workingPath, FileMode.Create, FileAccess.Write, FileShare.Read);
            _stream = stream;
            _gifWriter = new AnimatedGifWriter(stream, width, height, frameRate);
        }
        else
            _platformEncoder = PlatformVideoEncoder.Create(width, height, frameRate, _workingPath);

        var firstFrame = _captureFrame();
        if (_gifWriter != null)
            _gifWriter.AddFrame(firstFrame.RgbPixels);
        else
            _platformEncoder!.AddFrameAsync(firstFrame.Png).GetAwaiter().GetResult();
        _frameCount = 1;
        _recordingTask = RecordFramesAsync();
    }

    public async Task StopAsync()
    {
        if (_stopped) return;
        _stopped = true;
        _cancellation.Cancel();
        try
        {
            try
            {
                await _recordingTask;
            }
            catch (OperationCanceledException) when (_cancellation.IsCancellationRequested)
            {
            }
            finally
            {
                if (_gifWriter != null)
                    _gifWriter.Dispose();
                else if (_platformEncoder != null)
                    await _platformEncoder.CompleteAsync();
                if (_stream != null)
                    await _stream.DisposeAsync();
                _cancellation.Dispose();
            }

            if (_frameCount < 2)
                throw new InvalidOperationException("The recording ended before a second frame could be captured.");

            if (_destinationPath == null)
            {
                await using var readStream = File.OpenRead(_workingPath);
                var result = await FileSaver.Default.SaveAsync($"circuit.{_format}", readStream, CancellationToken.None);
                if (!result.IsSuccessful)
                    throw result.Exception ?? new OperationCanceledException($"{_format.ToUpperInvariant()} export was cancelled.");
            }
        }
        finally
        {
            if (_destinationPath == null && File.Exists(_workingPath))
                File.Delete(_workingPath);
            _platformEncoder?.Dispose();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (!_stopped)
            await StopAsync();
    }

    private async Task RecordFramesAsync()
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(1000d / _frameRate));
        while (await timer.WaitForNextTickAsync(_cancellation.Token))
        {
            if (_frameCount >= _frameRate * 600)
                throw new InvalidOperationException("Recording reached the 10-minute frame limit.");
            var frame = _captureFrame();
            if (_gifWriter != null)
                _gifWriter.AddFrame(frame.RgbPixels);
            else
                await _platformEncoder!.AddFrameAsync(frame.Png);
            _frameCount++;
        }
    }
}
