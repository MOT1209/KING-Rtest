using System;
using System.IO;
using KINGRecorder.Core.Interfaces;
using KINGRecorder.Core.Models;

namespace KINGRecorder.Recording.Stubs;

public sealed class UnavailableVideoCompositor : IVideoCompositor
{
    public void Initialize(int outputWidth, int outputHeight)
    {
        throw new NotSupportedException("Direct3D 11 Video Compositor is scheduled for Phase 2 implementation.");
    }

    public IntPtr Composite(IntPtr screenSurface, IntPtr? webcamSurface, WebcamOverlaySettings overlaySettings)
    {
        throw new NotSupportedException("Direct3D 11 Video Compositor is scheduled for Phase 2 implementation.");
    }

    public void Dispose()
    {
    }
}

public sealed class UnavailableEncoderService : IEncoderService
{
    public Task InitializeAsync(string outputPath, int width, int height, int fps, int bitrateBps, CancellationToken ct = default)
    {
        throw new NotSupportedException("Media Foundation H.264 Encoder is scheduled for Phase 4 implementation.");
    }

    public void WriteVideoSample(IntPtr surfacePointer, TimeSpan timestamp)
    {
        throw new NotSupportedException("Media Foundation H.264 Encoder is scheduled for Phase 4 implementation.");
    }

    public void WriteAudioSample(byte[] audioBuffer, int length, TimeSpan timestamp)
    {
        throw new NotSupportedException("Media Foundation H.264 Encoder is scheduled for Phase 4 implementation.");
    }

    public Task FinalizeAsync(CancellationToken ct = default)
    {
        throw new NotSupportedException("Media Foundation H.264 Encoder is scheduled for Phase 4 implementation.");
    }

    public void Dispose()
    {
    }
}
