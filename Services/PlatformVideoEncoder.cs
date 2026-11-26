using System;
using System.Threading.Tasks;

namespace EEE.Services;

internal interface IPlatformVideoEncoder : IDisposable
{
    Task AddFrameAsync(byte[] png);
    Task CompleteAsync();
}

internal static partial class PlatformVideoEncoder
{
    internal static partial IPlatformVideoEncoder Create(int width, int height, int frameRate, string outputPath);
}
