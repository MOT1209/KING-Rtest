using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using KINGRecorder.Core.Enums;
using KINGRecorder.Core.Models;

namespace KINGRecorder.Core.Interfaces;

public interface IScreenCaptureService : IDisposable
{
    event EventHandler<FrameArrivedEventArgs>? FrameArrived;
    bool IsCapturing { get; }
    Task<IReadOnlyList<DisplayInfo>> GetDisplaysAsync(CancellationToken ct = default);
    Task<IReadOnlyList<WindowInfo>> GetOpenWindowsAsync(CancellationToken ct = default);
    Task StartCaptureAsync(RecordingOptions options, CancellationToken ct = default);
    Task StopCaptureAsync(CancellationToken ct = default);
}

public interface IWebcamService : IDisposable
{
    event EventHandler<WebcamFrameEventArgs>? WebcamFrameArrived;
    bool IsPreviewing { get; }
    Task<IReadOnlyList<string>> GetAvailableCamerasAsync(CancellationToken ct = default);
    Task StartCameraAsync(string? deviceId = null, CancellationToken ct = default);
    Task StopCameraAsync(CancellationToken ct = default);
}

public interface IAudioCaptureService : IDisposable
{
    event EventHandler<AudioDataEventArgs>? AudioDataAvailable;
    bool IsCapturing { get; }
    Task<IReadOnlyList<AudioDeviceInfo>> GetMicrophonesAsync(CancellationToken ct = default);
    Task StartAsync(bool captureMic, string? micId, float micVol, bool captureSystem, float sysVol, CancellationToken ct = default);
    void UpdateVolumes(float micVol, float sysVol);
    Task StopAsync(CancellationToken ct = default);
}

public interface IVideoCompositor : IDisposable
{
    void Initialize(int outputWidth, int outputHeight);
    IntPtr Composite(IntPtr screenSurface, IntPtr? webcamSurface, WebcamOverlaySettings overlaySettings);
}

public interface IEncoderService : IDisposable
{
    Task InitializeAsync(string outputPath, int width, int height, int fps, int bitrateBps, CancellationToken ct = default);
    void WriteVideoSample(IntPtr surfacePointer, TimeSpan timestamp);
    void WriteAudioSample(byte[] audioBuffer, int length, TimeSpan timestamp);
    Task FinalizeAsync(CancellationToken ct = default);
}

public interface IRecordingEngine : IDisposable
{
    RecordingState State { get; }
    TimeSpan ElapsedDuration { get; }
    event EventHandler<RecordingStateChangedEventArgs>? StateChanged;
    event EventHandler<TimeSpan>? DurationUpdated;
    Task StartRecordingAsync(RecordingOptions options, CancellationToken ct = default);
    Task PauseRecordingAsync(CancellationToken ct = default);
    Task ResumeRecordingAsync(CancellationToken ct = default);
    Task<RecordingItem> StopRecordingAsync(CancellationToken ct = default);
    Task CancelRecordingAsync(CancellationToken ct = default);
    Task<string> CaptureScreenshotAsync(RecordingOptions options, CancellationToken ct = default);
}

public class FrameArrivedEventArgs : EventArgs
{
    public IntPtr SurfacePointer { get; }
    public TimeSpan Timestamp { get; }
    public int Width { get; }
    public int Height { get; }

    public FrameArrivedEventArgs(IntPtr surfacePointer, TimeSpan timestamp, int width, int height)
    {
        SurfacePointer = surfacePointer;
        Timestamp = timestamp;
        Width = width;
        Height = height;
    }
}

public class WebcamFrameEventArgs : EventArgs
{
    public IntPtr SurfacePointer { get; }
    public TimeSpan Timestamp { get; }

    public WebcamFrameEventArgs(IntPtr surfacePointer, TimeSpan timestamp)
    {
        SurfacePointer = surfacePointer;
        Timestamp = timestamp;
    }
}

public class AudioDataEventArgs : EventArgs
{
    public byte[] Buffer { get; }
    public int BytesRecorded { get; }
    public TimeSpan Timestamp { get; }

    public AudioDataEventArgs(byte[] buffer, int bytesRecorded, TimeSpan timestamp)
    {
        Buffer = buffer;
        BytesRecorded = bytesRecorded;
        Timestamp = timestamp;
    }
}

public class RecordingStateChangedEventArgs : EventArgs
{
    public RecordingState OldState { get; }
    public RecordingState NewState { get; }
    public string? ErrorMessage { get; }

    public RecordingStateChangedEventArgs(RecordingState oldState, RecordingState newState, string? errorMessage = null)
    {
        OldState = oldState;
        NewState = newState;
        ErrorMessage = errorMessage;
    }
}
