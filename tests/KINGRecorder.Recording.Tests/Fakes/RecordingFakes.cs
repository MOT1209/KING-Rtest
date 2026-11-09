using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using KINGRecorder.Core.Enums;
using KINGRecorder.Core.Interfaces;
using KINGRecorder.Core.Models;

namespace KINGRecorder.Recording.Tests.Fakes;

/// <summary>
/// Deterministic test doubles for the pipeline boundaries. They are only used to verify engine
/// lifecycle behaviour; they do not claim that real capture or encoding exists.
/// </summary>
internal sealed class FakeScreenCaptureService : IScreenCaptureService
{
    public event EventHandler<FrameArrivedEventArgs>? FrameArrived
    {
        add { }
        remove { }
    }

    public bool IsCapturing { get; private set; }

    public bool IsDisposed { get; private set; }

    public int StartCount { get; private set; }

    public int StopCount { get; private set; }

    public Exception? StartFailure { get; set; }

    public int StopFailureCount { get; set; }

    public Task<IReadOnlyList<DisplayInfo>> GetDisplaysAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<DisplayInfo>>(Array.Empty<DisplayInfo>());

    public Task<IReadOnlyList<WindowInfo>> GetOpenWindowsAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<WindowInfo>>(Array.Empty<WindowInfo>());

    public Task StartCaptureAsync(RecordingOptions options, CancellationToken ct = default)
    {
        StartCount++;

        if (StartFailure is not null)
        {
            throw StartFailure;
        }

        IsCapturing = true;
        return Task.CompletedTask;
    }

    public Task StopCaptureAsync(CancellationToken ct = default)
    {
        StopCount++;
        IsCapturing = false;

        if (StopFailureCount > 0)
        {
            StopFailureCount--;
            throw new InvalidOperationException("Simulated screen capture stop failure.");
        }

        return Task.CompletedTask;
    }

    public void Dispose() => IsDisposed = true;
}

internal sealed class FakeAudioCaptureService : IAudioCaptureService
{
    public event EventHandler<AudioDataEventArgs>? AudioDataAvailable
    {
        add { }
        remove { }
    }

    public bool IsCapturing { get; private set; }

    public bool IsDisposed { get; private set; }

    public int StartCount { get; private set; }

    public int StopCount { get; private set; }

    public Task<IReadOnlyList<AudioDeviceInfo>> GetMicrophonesAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<AudioDeviceInfo>>(Array.Empty<AudioDeviceInfo>());

    public Task StartAsync(bool captureMic, string? micId, float micVol, bool captureSystem, float sysVol, CancellationToken ct = default)
    {
        StartCount++;
        IsCapturing = true;
        return Task.CompletedTask;
    }

    public void UpdateVolumes(float micVol, float sysVol)
    {
    }

    public Task StopAsync(CancellationToken ct = default)
    {
        StopCount++;
        IsCapturing = false;
        return Task.CompletedTask;
    }

    public void Dispose() => IsDisposed = true;
}

internal sealed class FakeEncoderService : IEncoderService
{
    public string? OutputPath { get; private set; }

    public int InitializeCount { get; private set; }

    public int FinalizeCount { get; private set; }

    public bool IsDisposed { get; private set; }

    /// <summary>When set, <see cref="InitializeAsync"/> throws this exception.</summary>
    public Exception? InitializeFailure { get; set; }

    /// <summary>When true, initialization creates the output file so a session can be finalized.</summary>
    public bool WriteOutputFile { get; set; }

    /// <summary>
    /// When set, the output file is written first and this exception is thrown afterwards, so a test can
    /// prove that a failed initialization removes the partially written file.
    /// </summary>
    public Exception? FailAfterWritingOutput { get; set; }

    /// <summary>Optional gate used to force deterministic interleaving in concurrency tests.</summary>
    public TaskCompletionSource? InitializeGate { get; set; }

    public async Task InitializeAsync(string outputPath, int width, int height, int fps, int bitrateBps, CancellationToken ct = default)
    {
        InitializeCount++;
        OutputPath = outputPath;

        if (InitializeGate is not null)
        {
            await InitializeGate.Task.ConfigureAwait(false);
        }

        if (InitializeFailure is not null)
        {
            throw InitializeFailure;
        }

        if (WriteOutputFile)
        {
            await File.WriteAllTextAsync(outputPath, "deterministic fake recording payload", ct).ConfigureAwait(false);
        }

        if (FailAfterWritingOutput is not null)
        {
            throw FailAfterWritingOutput;
        }
    }

    public void WriteVideoSample(IntPtr surfacePointer, TimeSpan timestamp)
    {
    }

    public void WriteAudioSample(byte[] audioBuffer, int length, TimeSpan timestamp)
    {
    }

    public Task FinalizeAsync(CancellationToken ct = default)
    {
        FinalizeCount++;
        return Task.CompletedTask;
    }

    public void Dispose() => IsDisposed = true;
}

internal sealed class FakeVideoCompositor : IVideoCompositor
{
    public bool IsDisposed { get; private set; }

    public void Initialize(int outputWidth, int outputHeight)
    {
    }

    public IntPtr Composite(IntPtr screenSurface, IntPtr? webcamSurface, WebcamOverlaySettings overlaySettings) => IntPtr.Zero;

    public void Dispose() => IsDisposed = true;
}

internal static class TestRecordingOptions
{
    public static RecordingOptions Create(string outputDirectory) => new(
        Mode: CaptureMode.FullScreen,
        TargetDisplay: null,
        TargetWindow: null,
        CustomRegion: null,
        WebcamSettings: new WebcamOverlaySettings(false, WebcamShape.Circle, CornerPlacement.BottomRight, null, 20, 2, "#D4AF37"),
        CaptureMicrophone: false,
        MicrophoneDeviceId: null,
        MicrophoneVolume: 0.8f,
        CaptureSystemAudio: false,
        SystemAudioVolume: 0.8f,
        Resolution: VideoResolutionPreset.P1080,
        FrameRate: VideoFrameRate.Fps60,
        Quality: VideoQuality.High,
        Acceleration: HardwareAccelerationMode.Auto,
        OutputDirectory: outputDirectory);
}
