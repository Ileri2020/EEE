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
    private readonly string _format;
    private readonly FileStream? _stream;
    private readonly AnimatedGifWriter? _gifWriter;
    private readonly IPlatformVideoEncoder? _platformEncoder;
    private readonly Channel<EESimulator.App.Views.SchematicCapturedFrame> _frameChannel;
    private readonly Task _encodingTask;
    private int _frameCount;
    private bool _stopped;
    private Exception? _recordingException;

    /// <summary>
    /// How many frames have been submitted so far. The UI-side capture timer
    /// checks this to enforce the 10-minute (600-second) maximum.
    /// </summary>
    public int FrameCount => _frameCount;

    /// <summary>
    /// Whether the recorder has been stopped or finished.
    /// The UI-side capture timer polls this to know when to stop ticking.
    /// </summary>
    public bool IsStopped => _stopped;

    public SchematicVideoRecorder(
        int width,
        int height,
        int frameRate,
        string format,
        string? destinationPath)
    {
        if (frameRate is < 1 or > 15)
            throw new ArgumentOutOfRangeException(nameof(frameRate), "Recording frame rate must be between 1 and 15 fps.");
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

        // Use a larger channel so the UI-side timer can keep submitting frames
        // without stalling even if the encoder is momentarily busy.
        _frameChannel = Channel.CreateBounded<EESimulator.App.Views.SchematicCapturedFrame>(
            new BoundedChannelOptions(16)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
                // Multiple writers allowed: UI timer + any fallback paths
                SingleWriter = false
            });

        _encodingTask = Task.Run(ProcessFramesAsync);
        // No background capture task — frames are pushed by the UI-side
        // IDispatcherTimer in WorkspacePage, which runs on the main thread and
        // therefore never blocks gesture handling with InvokeOnMainThreadAsync.
    }

    /// <summary>
    /// Called by the UI-side capture timer (on the main thread) to push a rendered frame
    /// into the encoding pipeline. Non-blocking: drops the frame if the encoder is backlogged.
    /// </summary>
    public void TryAddFrame(EESimulator.App.Views.SchematicCapturedFrame frame)
    {
        if (!_stopped)
            _frameChannel.Writer.TryWrite(frame);
    }

    public async Task StopAsync()
    {
        if (_stopped) return;
        _stopped = true;
        try
        {
            _frameChannel.Writer.TryComplete();
            await _encodingTask;

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

    private async Task ProcessFramesAsync()
    {
        try
        {
            var reader = _frameChannel.Reader;
            while (await reader.WaitToReadAsync())
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
        catch (Exception ex)
        {
            _recordingException ??= ex;
        }
    }
}
