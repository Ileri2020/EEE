using CommunityToolkit.Maui.Storage;
using System;
using System.IO;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace EEE.Services;

internal static class SchematicCaptureService
{
    public static string GetDefaultMediaDirectory(string extension)
    {
        bool isVideo = extension.Equals(".mp4", StringComparison.OrdinalIgnoreCase);
#if ANDROID
        string publicDir = isVideo
            ? Android.OS.Environment.DirectoryMovies!
            : Android.OS.Environment.DirectoryPictures!;
        var storageDir = Android.OS.Environment.GetExternalStoragePublicDirectory(publicDir);
        string basePath = storageDir?.AbsolutePath ?? Microsoft.Maui.Storage.FileSystem.AppDataDirectory;
        return Path.Combine(basePath, "EEEngine");
#else
        var folder = isVideo
            ? Environment.SpecialFolder.MyVideos
            : Environment.SpecialFolder.MyPictures;
        string basePath = Environment.GetFolderPath(folder);
        if (string.IsNullOrWhiteSpace(basePath))
            basePath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        if (string.IsNullOrWhiteSpace(basePath))
            basePath = Microsoft.Maui.Storage.FileSystem.AppDataDirectory;
        return Path.Combine(basePath, "EEEngine");
#endif
    }

    public static void NotifyMediaSaved(string path)
    {
#if ANDROID
        try
        {
            var context = Android.App.Application.Context;
            Android.Media.MediaScannerConnection.ScanFile(context, new[] { path }, null, null);
        }
        catch
        {
        }
#endif
    }

    public static async Task SavePngAsync(byte[] png, string? path)
    {
        if (!string.IsNullOrWhiteSpace(path))
        {
            string destination = EnsureExtension(path, ".png");
            string? directory = Path.GetDirectoryName(Path.GetFullPath(destination));
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
            await File.WriteAllBytesAsync(destination, png);
            NotifyMediaSaved(destination);
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
    private readonly Func<Task<EESimulator.App.Views.SchematicCapturedFrame>> _captureFrameAsync;
    private readonly FileStream? _stream;
    private readonly AnimatedGifWriter? _gifWriter;
    private readonly IPlatformVideoEncoder? _platformEncoder;
    private readonly CancellationTokenSource _cancellation = new();
    private readonly Channel<EESimulator.App.Views.SchematicCapturedFrame> _frameChannel;
    private readonly Task _captureTask;
    private readonly Task _encodingTask;
    private int _frameCount;
    private bool _stopped;
    private Exception? _recordingException;

    public SchematicVideoRecorder(
        int width,
        int height,
        int frameRate,
        string format,
        string? destinationPath,
        Func<Task<EESimulator.App.Views.SchematicCapturedFrame>> captureFrameAsync)
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
        _captureFrameAsync = captureFrameAsync ?? throw new ArgumentNullException(nameof(captureFrameAsync));

        if (_format == "gif")
        {
            var stream = new FileStream(_workingPath, FileMode.Create, FileAccess.Write, FileShare.Read);
            _stream = stream;
            _gifWriter = new AnimatedGifWriter(stream, width, height, frameRate);
        }
        else
        {
            _platformEncoder = PlatformVideoEncoder.Create(width, height, frameRate, _workingPath);
        }

        _frameChannel = Channel.CreateBounded<EESimulator.App.Views.SchematicCapturedFrame>(
            new BoundedChannelOptions(10)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
                SingleWriter = true
            });

        _encodingTask = Task.Run(ProcessFramesAsync);
        _captureTask = Task.Run(CaptureFramesLoopAsync);
    }

    public SchematicVideoRecorder(
        int width,
        int height,
        int frameRate,
        string format,
        string? destinationPath,
        Func<EESimulator.App.Views.SchematicCapturedFrame> captureFrame)
        : this(width, height, frameRate, format, destinationPath, () => Task.FromResult(captureFrame()))
    {
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
                await _captureTask;
            }
            catch (OperationCanceledException) { }
            catch { }

            _frameChannel.Writer.TryComplete();

            try
            {
                await _encodingTask;
            }
            catch (OperationCanceledException) { }
            catch { }

            if (_gifWriter != null)
                _gifWriter.Dispose();
            else if (_platformEncoder != null)
                await _platformEncoder.CompleteAsync();

            if (_stream != null)
                await _stream.DisposeAsync();

            if (_recordingException != null)
                throw new InvalidOperationException($"Recording encountered an error: {_recordingException.Message}", _recordingException);

            if (_frameCount < 2)
                throw new InvalidOperationException("The recording ended before sufficient frames could be captured.");

            if (_destinationPath != null)
            {
                SchematicCaptureService.NotifyMediaSaved(_destinationPath);
            }
            else
            {
                await using var readStream = File.OpenRead(_workingPath);
                var result = await FileSaver.Default.SaveAsync($"circuit.{_format}", readStream, CancellationToken.None);
                if (!result.IsSuccessful)
                    throw result.Exception ?? new OperationCanceledException($"{_format.ToUpperInvariant()} export was cancelled.");
            }
        }
        finally
        {
            _cancellation.Dispose();
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

    private async Task CaptureFramesLoopAsync()
    {
        try
        {
            // Initial frame snapshot
            var initial = await _captureFrameAsync();
            _frameChannel.Writer.TryWrite(initial);

            using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(1000d / _frameRate));
            while (await timer.WaitForNextTickAsync(_cancellation.Token))
            {
                if (_frameCount >= _frameRate * 600)
                    break;

                var frame = await _captureFrameAsync();
                _frameChannel.Writer.TryWrite(frame);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _recordingException = ex;
        }
        finally
        {
            _frameChannel.Writer.TryComplete();
        }
    }

    private async Task ProcessFramesAsync()
    {
        try
        {
            var reader = _frameChannel.Reader;
            while (await reader.WaitToReadAsync(_cancellation.Token))
            {
                while (reader.TryRead(out var frame))
                {
                    if (_gifWriter != null)
                    {
                        _gifWriter.AddFrame(frame.RgbPixels);
                    }
                    else if (_platformEncoder != null)
                    {
                        await _platformEncoder.AddFrameAsync(frame.Png);
                    }
                    _frameCount++;
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _recordingException ??= ex;
        }
    }
}
