using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SharpDX;
using SharpDX.Direct3D11;
using SharpDX.MediaFoundation;
using KINGRecorder.Core.Exceptions;
using KINGRecorder.Core.Interfaces;

namespace KINGRecorder.Recording;

/// <summary>
/// Media Foundation based encoder: H.264 video + AAC audio into an MP4 container via IMF SinkWriter.
/// Uses a DXGI surface buffer for video frames (zero-copy from the capture texture) and a memory
/// buffer for audio. Software/hardware MFT selection is left to Media Foundation's default.
/// </summary>
public sealed class MediaFoundationEncoderService : IEncoderService, IDisposable
{
    private readonly ILogger<MediaFoundationEncoderService> _logger;
    private SinkWriter? _sinkWriter;
    private int _videoStreamIndex = -1;
    private int _audioStreamIndex = -1;
    private bool _writing;
    private bool _disposed;
    private readonly object _sync = new();

    private int _width;
    private int _height;
    private int _fps;
    private long _frameDuration100ns;
    private long _audioBytesPerSecond;

    public MediaFoundationEncoderService(ILogger<MediaFoundationEncoderService> logger)
    {
        _logger = logger;
    }

    public Task InitializeAsync(string outputPath, int width, int height, int fps, int bitrateBps, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            throw new ArgumentException("An output path is required.", nameof(outputPath));
        }

        lock (_sync)
        {
            if (_sinkWriter is not null)
            {
                throw new InvalidOperationException("The encoder is already initialized.");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

            MediaFactory.Startup(0x0001, 0x0000);

            MediaAttributes? attributes = null;
            try
            {
                attributes = new MediaAttributes(0);
            }
            catch
            {
                attributes = null;
            }

            _sinkWriter = MediaFactory.CreateSinkWriterFromURL(outputPath, null, attributes);

            _width = width > 0 ? width : 1920;
            _height = height > 0 ? height : 1080;
            _fps = fps > 0 ? fps : 30;
            _frameDuration100ns = 10_000_000L / _fps;
            _audioBytesPerSecond = 48000 * 2 * 2; // 48kHz, stereo, 16-bit

            // Video output type (H.264)
            var videoOut = new MediaType();
            videoOut.Set(MediaTypeAttributeKeys.MajorType, MediaTypeGuids.Video);
            videoOut.Set(MediaTypeAttributeKeys.Subtype, VideoFormatGuids.H264);
            videoOut.Set(MediaTypeAttributeKeys.AvgBitrate, bitrateBps > 0 ? bitrateBps : 10_000_000);
            videoOut.Set(MediaTypeAttributeKeys.InterlaceMode, 2); // progressive
            videoOut.Set(MediaTypeAttributeKeys.FrameSize, PackSize(_width, _height));
            videoOut.Set(MediaTypeAttributeKeys.FrameRate, PackRatio(_fps, 1));
            _sinkWriter.AddStream(videoOut, out _videoStreamIndex);

            // Video input type (RGB32, matches capture frames)
            var videoIn = new MediaType();
            videoIn.Set(MediaTypeAttributeKeys.MajorType, MediaTypeGuids.Video);
            videoIn.Set(MediaTypeAttributeKeys.Subtype, VideoFormatGuids.Rgb32);
            videoIn.Set(MediaTypeAttributeKeys.InterlaceMode, 2);
            videoIn.Set(MediaTypeAttributeKeys.FrameSize, PackSize(_width, _height));
            videoIn.Set(MediaTypeAttributeKeys.FrameRate, PackRatio(_fps, 1));
            _sinkWriter.SetInputMediaType(_videoStreamIndex, videoIn, null);

            // Audio output type (AAC)
            var audioOut = new MediaType();
            audioOut.Set(MediaTypeAttributeKeys.MajorType, MediaTypeGuids.Audio);
            audioOut.Set(MediaTypeAttributeKeys.Subtype, AudioFormatGuids.Aac);
            audioOut.Set(MediaTypeAttributeKeys.AudioSamplesPerSecond, 48000);
            audioOut.Set(MediaTypeAttributeKeys.AudioNumChannels, 2);
            audioOut.Set(MediaTypeAttributeKeys.AudioBitsPerSample, 16);
            audioOut.Set(MediaTypeAttributeKeys.AvgBitrate, 128000);
            _sinkWriter.AddStream(audioOut, out _audioStreamIndex);

            // Audio input type (PCM)
            var audioIn = new MediaType();
            audioIn.Set(MediaTypeAttributeKeys.MajorType, MediaTypeGuids.Audio);
            audioIn.Set(MediaTypeAttributeKeys.Subtype, AudioFormatGuids.Pcm);
            audioIn.Set(MediaTypeAttributeKeys.AudioSamplesPerSecond, 48000);
            audioIn.Set(MediaTypeAttributeKeys.AudioNumChannels, 2);
            audioIn.Set(MediaTypeAttributeKeys.AudioBitsPerSample, 16);
            audioIn.Set(MediaTypeAttributeKeys.AudioBlockAlignment, 4);
            audioIn.Set(MediaTypeAttributeKeys.AudioAvgBytesPerSecond, _audioBytesPerSecond);
            _sinkWriter.SetInputMediaType(_audioStreamIndex, audioIn, null);

            _sinkWriter.BeginWriting();
            _writing = true;

            _logger.LogInformation("Media Foundation encoder initialized: {Path} ({W}x{H} @ {Fps}fps).", outputPath, _width, _height, _fps);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize the Media Foundation encoder for {Path}.", outputPath);
            Cleanup();
            throw new VideoEncoderException("Media Foundation encoder initialization failed.", ex);
        }

        return Task.CompletedTask;
    }

    public void WriteVideoSample(IntPtr surfacePointer, TimeSpan timestamp)
    {
        if (surfacePointer == IntPtr.Zero || !_writing || _sinkWriter is null)
        {
            return;
        }

        try
        {
            var texture = new Texture2D(surfacePointer);
            var buffer = MediaFactory.CreateDXGISurfaceBuffer(typeof(Texture2D).GUID, texture, 0, new RawBool(false));
            var sample = MediaFactory.CreateSample();
            sample.AddBuffer(buffer);
            sample.SampleTime = (long)(timestamp.TotalSeconds * 10_000_000);
            sample.SampleDuration = _frameDuration100ns;
            _sinkWriter.WriteSample(_videoStreamIndex, sample);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to write a video sample.");
        }
    }

    public void WriteAudioSample(byte[] audioBuffer, int length, TimeSpan timestamp)
    {
        if (audioBuffer is null || length <= 0 || !_writing || _sinkWriter is null)
        {
            return;
        }

        try
        {
            var buffer = MediaFactory.CreateMemoryBuffer(length);
            IntPtr ptr = buffer.Lock(out int maxLen, out int curLen);
            System.Runtime.InteropServices.Marshal.Copy(audioBuffer, 0, ptr, Math.Min(length, maxLen));
            buffer.Unlock();
            buffer.CurrentLength = Math.Min(length, maxLen);

            var sample = MediaFactory.CreateSample();
            sample.AddBuffer(buffer);
            sample.SampleTime = (long)(timestamp.TotalSeconds * 10_000_000);
            sample.SampleDuration = (long)((double)length / _audioBytesPerSecond * 10_000_000);
            _sinkWriter.WriteSample(_audioStreamIndex, sample);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to write an audio sample.");
        }
    }

    public Task FinalizeAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        lock (_sync)
        {
            if (_sinkWriter is null || !_writing)
            {
                return Task.CompletedTask;
            }

            try
            {
                _sinkWriter.Finalize();
                _logger.LogInformation("Media Foundation encoder finalized.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to finalize the Media Foundation encoder.");
                throw new VideoEncoderException("Media Foundation encoder finalization failed.", ex);
            }
            finally
            {
                _writing = false;
                Cleanup();
            }
        }

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        try { FinalizeAsync().GetAwaiter().GetResult(); } catch { /* best effort */ }
        Cleanup();
    }

    private void Cleanup()
    {
        lock (_sync)
        {
            try { _sinkWriter?.Dispose(); } catch { /* ignore */ }
            _sinkWriter = null;
            _videoStreamIndex = -1;
            _audioStreamIndex = -1;
            _writing = false;
            try { MediaFactory.Shutdown(); } catch { /* ignore */ }
        }
    }

    private static long PackSize(int width, int height) =>
        ((long)width << 32) | (uint)height;

    private static long PackRatio(int numerator, int denominator) =>
        ((long)numerator << 32) | (uint)denominator;
}
