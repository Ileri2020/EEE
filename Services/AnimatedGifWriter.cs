using System;
using System.Collections.Generic;
using System.IO;

namespace EEE.Services;

internal sealed class AnimatedGifWriter : IDisposable
{
    private readonly BinaryWriter _writer;
    private readonly int _width;
    private readonly int _height;
    private readonly int _frameDelay;
    private bool _completed;

    public AnimatedGifWriter(Stream stream, int width, int height, int frameRate)
    {
        if (width is < 1 or > ushort.MaxValue || height is < 1 or > ushort.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(width), "GIF dimensions must be between 1 and 65535 pixels.");
        if (frameRate is < 1 or > 15)
            throw new ArgumentOutOfRangeException(nameof(frameRate), "GIF frame rate must be between 1 and 15 fps.");

        _width = width;
        _height = height;
        _frameDelay = Math.Max(1, 100 / frameRate);
        _writer = new BinaryWriter(stream, System.Text.Encoding.ASCII, leaveOpen: true);
        WriteHeader();
    }

    public void AddFrame(byte[] rgbPixels)
    {
        if (_completed)
            throw new InvalidOperationException("The GIF recorder has already been finalized.");
        if (rgbPixels.Length != _width * _height * 3)
            throw new ArgumentException("Frame dimensions do not match the GIF canvas.", nameof(rgbPixels));

        _writer.Write((byte)0x21);
        _writer.Write((byte)0xF9);
        _writer.Write((byte)4);
        _writer.Write((byte)0);
        _writer.Write((ushort)_frameDelay);
        _writer.Write((byte)0);
        _writer.Write((byte)0);

        _writer.Write((byte)0x2C);
        _writer.Write((ushort)0);
        _writer.Write((ushort)0);
        _writer.Write((ushort)_width);
        _writer.Write((ushort)_height);
        _writer.Write((byte)0);
        _writer.Write((byte)8);
        WriteImageData(EncodeLzw(Quantize(rgbPixels)));
    }

    public void Complete()
    {
        if (_completed) return;
        _writer.Write((byte)0x3B);
        _writer.Flush();
        _completed = true;
    }

    public void Dispose()
    {
        Complete();
        _writer.Dispose();
    }

    private void WriteHeader()
    {
        _writer.Write(System.Text.Encoding.ASCII.GetBytes("GIF89a"));
        _writer.Write((ushort)_width);
        _writer.Write((ushort)_height);
        _writer.Write((byte)0xF7);
        _writer.Write((byte)0);
        _writer.Write((byte)0);

        for (int index = 0; index < 256; index++)
        {
            int red = ((index >> 5) & 7) * 255 / 7;
            int green = ((index >> 2) & 7) * 255 / 7;
            int blue = (index & 3) * 255 / 3;
            _writer.Write((byte)red);
            _writer.Write((byte)green);
            _writer.Write((byte)blue);
        }

        _writer.Write(new byte[] { 0x21, 0xFF, 0x0B });
        _writer.Write(System.Text.Encoding.ASCII.GetBytes("NETSCAPE2.0"));
        _writer.Write(new byte[] { 0x03, 0x01, 0x00, 0x00, 0x00 });
    }

    private static byte[] Quantize(byte[] rgbPixels)
    {
        var indexed = new byte[rgbPixels.Length / 3];
        for (int source = 0, destination = 0; source < rgbPixels.Length; source += 3)
        {
            indexed[destination++] = (byte)((rgbPixels[source] >> 5) << 5 |
                (rgbPixels[source + 1] >> 5) << 2 |
                (rgbPixels[source + 2] >> 6));
        }
        return indexed;
    }

    private static byte[] EncodeLzw(byte[] pixels)
    {
        const int clearCode = 256;
        const int endCode = 257;
        var output = new List<byte>(pixels.Length);
        var dictionary = new Dictionary<int, int>(4096);
        int codeSize = 9;
        int nextCode = 258;
        uint bitBuffer = 0;
        int bufferedBits = 0;

        void WriteCode(int code)
        {
            bitBuffer |= (uint)code << bufferedBits;
            bufferedBits += codeSize;
            while (bufferedBits >= 8)
            {
                output.Add((byte)bitBuffer);
                bitBuffer >>= 8;
                bufferedBits -= 8;
            }
        }

        void ResetDictionary()
        {
            dictionary.Clear();
            codeSize = 9;
            nextCode = 258;
        }

        WriteCode(clearCode);
        if (pixels.Length == 0)
        {
            WriteCode(endCode);
        }
        else
        {
            int prefix = pixels[0];
            for (int index = 1; index < pixels.Length; index++)
            {
                int symbol = pixels[index];
                int key = (prefix << 8) | symbol;
                if (dictionary.TryGetValue(key, out int combined))
                {
                    prefix = combined;
                    continue;
                }

                WriteCode(prefix);
                if (nextCode < 4096)
                {
                    dictionary[key] = nextCode++;
                    if (nextCode > (1 << codeSize) && codeSize < 12)
                        codeSize++;
                }
                else
                {
                    WriteCode(clearCode);
                    ResetDictionary();
                }
                prefix = symbol;
            }
            WriteCode(prefix);
            WriteCode(endCode);
        }

        if (bufferedBits > 0)
            output.Add((byte)bitBuffer);
        return output.ToArray();
    }

    private void WriteImageData(byte[] data)
    {
        for (int offset = 0; offset < data.Length;)
        {
            int count = Math.Min(255, data.Length - offset);
            _writer.Write((byte)count);
            _writer.Write(data, offset, count);
            offset += count;
        }
        _writer.Write((byte)0);
    }
}
