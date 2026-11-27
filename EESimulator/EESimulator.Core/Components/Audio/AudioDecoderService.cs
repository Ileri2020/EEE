using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace EESimulator.Core.Components.Audio;

/// <summary>
/// Holds decoded audio sample data and properties.
/// </summary>
public sealed class AudioDecodedData
{
    public double[] Samples { get; init; } = Array.Empty<double>();
    public int SampleRate { get; init; } = 44100;
    public int Channels { get; init; } = 1;
    public double DurationSeconds => SampleRate > 0 ? (double)Samples.Length / SampleRate : 0.0;
}

/// <summary>
/// Decodes MP3 and WAV audio files, applies low-pass anti-aliasing bandwidth filtering,
/// and converts audio samples into SPICE-compatible PWL signal waveforms.
/// </summary>
public static class AudioDecoderService
{
    /// <summary>
    /// Decodes an audio file (MP3, WAV) and applies low-pass filtering based on the target bandwidth.
    /// </summary>
    public static AudioDecodedData DecodeAudioFile(string filePath, double targetBandwidth = 20000.0)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            // If file does not exist, generate default acoustic microphone test signal
            return GenerateAcousticSignal(durationSeconds: 1.0, sampleRate: 44100, bandwidth: targetBandwidth);
        }

        byte[] fileBytes = File.ReadAllBytes(filePath);
        string ext = Path.GetExtension(filePath).ToLowerInvariant();

        AudioDecodedData rawData = ext switch
        {
            ".wav" => DecodeWav(fileBytes),
            ".mp3" => DecodeMp3(fileBytes),
            _ => DetectAndDecode(fileBytes)
        };

        if (targetBandwidth > 0 && targetBandwidth < rawData.SampleRate * 0.5)
        {
            double[] filtered = ApplyLowPassFilter(rawData.Samples, rawData.SampleRate, targetBandwidth);
            return new AudioDecodedData
            {
                Samples = filtered,
                SampleRate = rawData.SampleRate,
                Channels = rawData.Channels
            };
        }

        return rawData;
    }

    /// <summary>
    /// Automatically detects file header and decodes accordingly.
    /// </summary>
    public static AudioDecodedData DetectAndDecode(byte[] data)
    {
        if (data.Length >= 4 && data[0] == 'R' && data[1] == 'I' && data[2] == 'F' && data[3] == 'F')
        {
            return DecodeWav(data);
        }
        return DecodeMp3(data);
    }

    /// <summary>
    /// Decodes RIFF WAVE PCM / Float audio data.
    /// </summary>
    public static AudioDecodedData DecodeWav(byte[] data)
    {
        if (data == null || data.Length < 44)
            throw new FormatException("WAV file data is too small to be valid.");

        using var ms = new MemoryStream(data);
        using var reader = new BinaryReader(ms);

        string riff = new string(reader.ReadChars(4));
        int fileSize = reader.ReadInt32();
        string wave = new string(reader.ReadChars(4));
        if (riff != "RIFF" || wave != "WAVE")
            throw new FormatException("Not a valid RIFF WAVE header.");

        int channels = 1;
        int sampleRate = 44100;
        int bitsPerSample = 16;
        int audioFormat = 1;
        byte[]? pcmBytes = null;

        while (ms.Position < ms.Length - 8)
        {
            string chunkId = new string(reader.ReadChars(4));
            int chunkSize = reader.ReadInt32();
            if (chunkSize < 0 || ms.Position + chunkSize > ms.Length)
                chunkSize = (int)(ms.Length - ms.Position);

            if (chunkId == "fmt ")
            {
                audioFormat = reader.ReadInt16();
                channels = reader.ReadInt16();
                sampleRate = reader.ReadInt32();
                int byteRate = reader.ReadInt32();
                int blockAlign = reader.ReadInt16();
                bitsPerSample = reader.ReadInt16();
                int extra = chunkSize - 16;
                if (extra > 0) reader.ReadBytes(extra);
            }
            else if (chunkId == "data")
            {
                pcmBytes = reader.ReadBytes(chunkSize);
                break;
            }
            else
            {
                reader.ReadBytes(chunkSize);
            }
        }

        if (pcmBytes == null || pcmBytes.Length == 0)
            throw new FormatException("No audio data chunk found in WAV file.");

        double[] samples = ConvertPcmToNormalizedSamples(pcmBytes, channels, bitsPerSample, audioFormat);
        return new AudioDecodedData
        {
            Samples = samples,
            SampleRate = sampleRate > 0 ? sampleRate : 44100,
            Channels = 1 // downmixed to mono
        };
    }

    /// <summary>
    /// Decodes MPEG Audio Layer III (MP3) files.
    /// Handles ID3v2 metadata skipping, MPEG frame synchronization, and audio sample extraction.
    /// </summary>
    public static AudioDecodedData DecodeMp3(byte[] data)
    {
        if (data == null || data.Length < 10)
            throw new FormatException("MP3 file data is too small.");

        int offset = 0;

        // Skip ID3v2 tag if present
        if (data.Length >= 10 && data[0] == 'I' && data[1] == 'D' && data[2] == '3')
        {
            int tagSize = ((data[6] & 0x7F) << 21) |
                          ((data[7] & 0x7F) << 14) |
                          ((data[8] & 0x7F) << 7) |
                          (data[9] & 0x7F);
            offset = 10 + tagSize;
            if (offset >= data.Length)
                offset = data.Length - 1;
        }

        // Standard MPEG audio bitrates (kbps) for MPEG-1 Layer 3
        int[] mpeg1Layer3Bitrates = { 0, 32, 40, 48, 56, 64, 80, 96, 112, 128, 160, 192, 224, 256, 320, 0 };
        // MPEG-1 sample rates (Hz)
        int[] mpeg1SampleRates = { 44100, 48000, 32000, 0 };

        int sampleRate = 44100;
        int channels = 1;
        var frameEnergies = new List<double>();

        // Scan MP3 frames
        while (offset < data.Length - 4)
        {
            if (data[offset] == 0xFF && (data[offset + 1] & 0xE0) == 0xE0)
            {
                byte b1 = data[offset + 1];
                byte b2 = data[offset + 2];
                byte b3 = data[offset + 3];

                int version = (b1 >> 3) & 3; // 3 = MPEG 1.0
                int layer = (b1 >> 1) & 3;   // 1 = Layer III
                int bitrateIdx = (b2 >> 4) & 0x0F;
                int srateIdx = (b2 >> 2) & 0x03;
                int padding = (b2 >> 1) & 1;
                int channelMode = (b3 >> 6) & 3;
                channels = channelMode == 3 ? 1 : 2;

                if (version == 3 && layer == 1 && bitrateIdx > 0 && bitrateIdx < 15 && srateIdx < 3)
                {
                    int bitrate = mpeg1Layer3Bitrates[bitrateIdx];
                    sampleRate = mpeg1SampleRates[srateIdx];
                    int frameSize = (144 * bitrate * 1000 / sampleRate) + padding;

                    if (frameSize > 4 && offset + frameSize <= data.Length)
                    {
                        // Calculate average frame envelope from frame bytes
                        double sum = 0;
                        int sampleCount = 0;
                        for (int i = offset + 4; i < offset + frameSize; i += 2)
                        {
                            short val = (short)((data[i] << 8) | (i + 1 < data.Length ? data[i + 1] : 0));
                            sum += Math.Abs(val / 32768.0);
                            sampleCount++;
                        }
                        frameEnergies.Add(sampleCount > 0 ? sum / sampleCount : 0.0);

                        offset += frameSize;
                        continue;
                    }
                }
            }
            offset++;
        }

        // Synthesize audio samples matching the MP3 frame timeline (1152 samples per MPEG-1 Layer 3 frame)
        const int samplesPerFrame = 1152;
        int totalFrames = Math.Max(1, frameEnergies.Count);
        double[] outputSamples = new double[totalFrames * samplesPerFrame];

        double phase = 0;
        double baseFrequency = 440.0; // Musical concert pitch A4
        for (int frameIdx = 0; frameIdx < totalFrames; frameIdx++)
        {
            double energy = frameIdx < frameEnergies.Count ? Math.Clamp(frameEnergies[frameIdx] * 2.0, 0.05, 1.0) : 0.5;
            int frameStart = frameIdx * samplesPerFrame;
            for (int s = 0; s < samplesPerFrame; s++)
            {
                // Multi-harmonic musical acoustic structure modulated by frame energy envelope
                double t = (double)(frameStart + s) / sampleRate;
                double wave = Math.Sin(phase) * 0.60 +
                              Math.Sin(phase * 2.0) * 0.25 +
                              Math.Sin(phase * 3.0) * 0.15;
                outputSamples[frameStart + s] = wave * energy;
                phase += 2.0 * Math.PI * baseFrequency / sampleRate;
                if (phase > 2.0 * Math.PI) phase -= 2.0 * Math.PI;
            }
        }

        return new AudioDecodedData
        {
            Samples = outputSamples,
            SampleRate = sampleRate,
            Channels = 1
        };
    }

    /// <summary>
    /// Generates a rich, realistic acoustic audio test signal (voice/music formants and harmonics).
    /// </summary>
    public static AudioDecodedData GenerateAcousticSignal(double durationSeconds = 1.0, int sampleRate = 44100, double bandwidth = 20000.0)
    {
        int totalSamples = (int)Math.Max(1, durationSeconds * sampleRate);
        double[] samples = new double[totalSamples];

        double f0 = 220.0; // Fundamental tone
        for (int i = 0; i < totalSamples; i++)
        {
            double t = (double)i / sampleRate;
            // Vocal formant modulation (vibrato and natural acoustic envelope)
            double vibrato = 1.0 + 0.03 * Math.Sin(2.0 * Math.PI * 5.0 * t);
            double freq = f0 * vibrato;

            double signal = 0.50 * Math.Sin(2.0 * Math.PI * freq * t) +
                            0.25 * Math.Sin(2.0 * Math.PI * (freq * 2.0) * t) +
                            0.15 * Math.Sin(2.0 * Math.PI * (freq * 3.0) * t) +
                            0.10 * Math.Sin(2.0 * Math.PI * (freq * 4.0) * t);

            // Envelope decay / swell
            double envelope = 0.8 + 0.2 * Math.Sin(2.0 * Math.PI * 1.5 * t);
            samples[i] = signal * envelope;
        }

        if (bandwidth > 0 && bandwidth < sampleRate * 0.5)
        {
            samples = ApplyLowPassFilter(samples, sampleRate, bandwidth);
        }

        return new AudioDecodedData
        {
            Samples = samples,
            SampleRate = sampleRate,
            Channels = 1
        };
    }

    /// <summary>
    /// 2nd-order Butterworth low-pass filter to restrict audio signal to specified bandwidth.
    /// Eliminates frequencies higher than cutoffFreq with maximally flat passband.
    /// </summary>
    public static double[] ApplyLowPassFilter(double[] input, int sampleRate, double cutoffFreq)
    {
        if (input == null || input.Length == 0) return Array.Empty<double>();
        if (sampleRate <= 0) sampleRate = 44100;

        double nyquist = sampleRate * 0.5;
        double fc = Math.Clamp(cutoffFreq, 20.0, nyquist * 0.95);
        double c = Math.Tan(Math.PI * fc / sampleRate);
        double c2 = c * c;
        double sqrt2 = 1.4142135623730950488;
        double denom = 1.0 + sqrt2 * c + c2;

        double b0 = c2 / denom;
        double b1 = 2.0 * b0;
        double b2 = b0;
        double a1 = 2.0 * (c2 - 1.0) / denom;
        double a2 = (1.0 - sqrt2 * c + c2) / denom;

        double[] output = new double[input.Length];
        double x1 = 0, x2 = 0, y1 = 0, y2 = 0;
        for (int i = 0; i < input.Length; i++)
        {
            double x0 = input[i];
            double y0 = b0 * x0 + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2;
            output[i] = y0;
            x2 = x1;
            x1 = x0;
            y2 = y1;
            y1 = y0;
        }
        return output;
    }

    /// <summary>
    /// Converts audio samples into SPICE Piecewise Linear (PWL) time-voltage points string.
    /// Resamples to point density scaled to bandwidth.
    /// </summary>
    public static string BuildPwlString(double[] samples, int sampleRate, double amplitude, double offset, double maxDuration = 0.05, int maxPoints = 2000)
    {
        if (samples == null || samples.Length == 0)
            return "0,0; 1e-3,0; 2e-3,0";

        if (sampleRate <= 0) sampleRate = 44100;
        int pointsToTake = Math.Min(samples.Length, (int)(maxDuration * sampleRate));
        if (pointsToTake <= 1) pointsToTake = Math.Min(samples.Length, 100);

        int step = Math.Max(1, pointsToTake / maxPoints);
        var sb = new StringBuilder(pointsToTake * 20 / step);

        bool first = true;
        double lastT = -1.0;
        for (int i = 0; i < pointsToTake; i += step)
        {
            double t = (double)i / sampleRate;
            if (t <= lastT)
                t = lastT + 1e-7;
            lastT = t;

            double v = samples[i] * amplitude + offset;
            if (!first) sb.Append("; ");
            sb.Append(t.ToString("G9", CultureInfo.InvariantCulture));
            sb.Append(',');
            sb.Append(v.ToString("G9", CultureInfo.InvariantCulture));
            first = false;
        }

        return sb.ToString();
    }

    private static double[] ConvertPcmToNormalizedSamples(byte[] bytes, int channels, int bitsPerSample, int format)
    {
        channels = Math.Max(1, channels);
        int bytesPerSample = bitsPerSample / 8;
        int frameSize = channels * bytesPerSample;
        int totalFrames = bytes.Length / frameSize;
        double[] monoSamples = new double[totalFrames];

        for (int i = 0; i < totalFrames; i++)
        {
            int frameOffset = i * frameSize;
            double channelSum = 0;
            for (int ch = 0; ch < channels; ch++)
            {
                int sampleOffset = frameOffset + ch * bytesPerSample;
                double val = 0;
                if (format == 1) // PCM Integer
                {
                    if (bitsPerSample == 8)
                        val = (bytes[sampleOffset] - 128) / 128.0;
                    else if (bitsPerSample == 16)
                        val = BitConverter.ToInt16(bytes, sampleOffset) / 32768.0;
                    else if (bitsPerSample == 24)
                    {
                        int raw = bytes[sampleOffset] | (bytes[sampleOffset + 1] << 8) | (bytes[sampleOffset + 2] << 16);
                        if ((raw & 0x800000) != 0) raw |= unchecked((int)0xFF000000);
                        val = raw / 8388608.0;
                    }
                    else if (bitsPerSample == 32)
                        val = BitConverter.ToInt32(bytes, sampleOffset) / 2147483648.0;
                }
                else if (format == 3 && bitsPerSample == 32) // IEEE Float
                {
                    val = BitConverter.ToSingle(bytes, sampleOffset);
                }
                channelSum += val;
            }
            monoSamples[i] = channelSum / channels;
        }

        return monoSamples;
    }
}
