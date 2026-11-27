using Android.Graphics;
using Android.Media;
using Android.Views;
using EEE.Services;
using System;
using System.Threading.Tasks;

namespace EEE.Services;

internal sealed class AndroidNativeVideoEncoder : IPlatformVideoEncoder
{
    private readonly MediaCodec _codec;
    private readonly MediaMuxer _muxer;
    private readonly Surface _inputSurface;
    private readonly MediaCodec.BufferInfo _bufferInfo = new();
    private readonly int _width;
    private readonly int _height;
    private int _videoTrack = -1;
    private bool _muxerStarted;
    private bool _completed;

    public AndroidNativeVideoEncoder(int width, int height, int frameRate, string outputPath)
    {
        _width = width;
        _height = height;
        const string videoMimeType = "video/avc";
        const int colorFormatSurface = 0x7F000789;
        var format = MediaFormat.CreateVideoFormat(videoMimeType, width, height);
        format.SetInteger(MediaFormat.KeyColorFormat, colorFormatSurface);
        format.SetInteger(MediaFormat.KeyBitRate, Math.Max(1_000_000, width * height * 5 * frameRate / 15));
        format.SetInteger(MediaFormat.KeyFrameRate, frameRate);
        format.SetInteger(MediaFormat.KeyIFrameInterval, 1);

        _codec = MediaCodec.CreateEncoderByType(videoMimeType);
        _muxer = new MediaMuxer(outputPath, MuxerOutputType.Mpeg4);
        _codec.Configure(format, null, null, MediaCodecConfigFlags.Encode);
        _inputSurface = _codec.CreateInputSurface();
        _codec.Start();
    }

    public Task AddFrameAsync(byte[] png)
    {
        if (_completed)
            throw new InvalidOperationException("The MP4 encoder has already been finalized.");

        using Bitmap? bitmap = BitmapFactory.DecodeByteArray(png, 0, png.Length);
        if (bitmap == null)
            throw new InvalidOperationException("SkiaSharp produced an image that Android could not decode.");

        Android.Graphics.Canvas? canvas = _inputSurface.LockCanvas(null);
        if (canvas == null)
            throw new InvalidOperationException("Android could not lock the encoder input surface.");
        try
        {
            canvas.DrawColor(Android.Graphics.Color.Black);
            canvas.DrawBitmap(bitmap, null, new Android.Graphics.Rect(0, 0, _width, _height), null);
        }
        finally
        {
            _inputSurface.UnlockCanvasAndPost(canvas);
        }

        DrainEncoder(endOfStream: false);
        return Task.CompletedTask;
    }

    public Task CompleteAsync()
    {
        if (_completed) return Task.CompletedTask;
        _codec.SignalEndOfInputStream();
        DrainEncoder(endOfStream: true);
        if (!_muxerStarted)
            throw new InvalidOperationException("Android did not produce an MP4 video track.");
        _muxer.Stop();
        _completed = true;
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _inputSurface.Dispose();
        _codec.Stop();
        _codec.Release();
        _muxer.Release();
    }

    private void DrainEncoder(bool endOfStream)
    {
        bool outputEnded = false;
        while (!outputEnded)
        {
            int outputIndex = _codec.DequeueOutputBuffer(_bufferInfo, endOfStream ? 10_000 : 0);
            if (outputIndex >= 0)
            {
                using var outputBuffer = _codec.GetOutputBuffer(outputIndex);
                if (_bufferInfo.Size > 0 && _muxerStarted && outputBuffer != null)
                {
                    outputBuffer.Position(_bufferInfo.Offset);
                    outputBuffer.Limit(_bufferInfo.Offset + _bufferInfo.Size);
                    _muxer.WriteSampleData(_videoTrack, outputBuffer, _bufferInfo);
                }

                outputEnded = (_bufferInfo.Flags & MediaCodecBufferFlags.EndOfStream) != 0;
                _codec.ReleaseOutputBuffer(outputIndex, false);
            }
            else if (outputIndex == (int)MediaCodecInfoState.OutputFormatChanged)
            {
                if (_muxerStarted)
                    throw new InvalidOperationException("Android reported multiple MP4 output formats.");
                _videoTrack = _muxer.AddTrack(_codec.OutputFormat);
                _muxer.Start();
                _muxerStarted = true;
            }
            else if (outputIndex == (int)MediaCodecInfoState.TryAgainLater && !endOfStream)
            {
                return;
            }
        }
    }
}

internal static partial class PlatformVideoEncoder
{
    internal static partial IPlatformVideoEncoder Create(int width, int height, int frameRate, string outputPath) =>
        new AndroidNativeVideoEncoder(width, height, frameRate, outputPath);
}
