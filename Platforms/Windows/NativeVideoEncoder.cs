using EEE.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Windows.Media.Editing;
using Windows.Media.MediaProperties;
using Windows.Media.Transcoding;
using Windows.Storage;

namespace EEE.Services;

internal sealed class WindowsNativeVideoEncoder : IPlatformVideoEncoder
{
    private readonly string _outputPath;
    private readonly string _frameDirectory;
    private readonly int _width;
    private readonly int _height;
    private readonly int _frameRate;
    private readonly List<string> _frames = new();
    private bool _completed;

    public WindowsNativeVideoEncoder(int width, int height, int frameRate, string outputPath)
    {
        _width = width;
        _height = height;
        _frameRate = frameRate;
        _outputPath = outputPath;
        _frameDirectory = Path.Combine(Path.GetTempPath(), $"eee-video-frames-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_frameDirectory);
    }

    public async Task AddFrameAsync(byte[] png)
    {
        if (_completed)
            throw new InvalidOperationException("The MP4 encoder has already been finalized.");
        string framePath = Path.Combine(_frameDirectory, $"frame-{_frames.Count:D6}.png");
        await File.WriteAllBytesAsync(framePath, png);
        _frames.Add(framePath);
    }

    public async Task CompleteAsync()
    {
        if (_completed) return;
        _completed = true;
        if (_frames.Count == 0)
            throw new InvalidOperationException("No video frames were captured.");

        try
        {
            var composition = new MediaComposition();
            TimeSpan frameDuration = TimeSpan.FromSeconds(1d / _frameRate);
            foreach (string framePath in _frames)
            {
                StorageFile frameFile = await StorageFile.GetFileFromPathAsync(framePath);
                MediaClip clip = await MediaClip.CreateFromImageFileAsync(frameFile, frameDuration);
                composition.Clips.Add(clip);
            }

            string directory = Path.GetDirectoryName(Path.GetFullPath(_outputPath))
                ?? throw new InvalidOperationException("The MP4 output directory is invalid.");
            string fileName = Path.GetFileName(_outputPath);
            StorageFolder outputFolder = await StorageFolder.GetFolderFromPathAsync(directory);
            StorageFile outputFile = await outputFolder.CreateFileAsync(
                fileName, CreationCollisionOption.ReplaceExisting);
            MediaEncodingProfile profile = MediaEncodingProfile.CreateMp4(VideoEncodingQuality.HD720p);
            profile.Video.Width = (uint)_width;
            profile.Video.Height = (uint)_height;
            TranscodeFailureReason result = await composition.RenderToFileAsync(
                outputFile,
                MediaTrimmingPreference.Precise,
                profile);
            if (result != TranscodeFailureReason.None)
                throw new InvalidOperationException($"Windows MP4 encoding failed: {result}.");
        }
        finally
        {
            if (Directory.Exists(_frameDirectory))
                Directory.Delete(_frameDirectory, recursive: true);
        }
    }

    public void Dispose()
    {
        if (!_completed && Directory.Exists(_frameDirectory))
            Directory.Delete(_frameDirectory, recursive: true);
    }
}

internal static partial class PlatformVideoEncoder
{
    internal static partial IPlatformVideoEncoder Create(int width, int height, int frameRate, string outputPath) =>
        new WindowsNativeVideoEncoder(width, height, frameRate, outputPath);
}
