using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using KINGRecorder.Core.Enums;
using KINGRecorder.Core.Exceptions;
using KINGRecorder.Core.Interfaces;
using KINGRecorder.Core.Models;
using KINGRecorder.Recording;
using KINGRecorder.Recording.Stubs;
using Xunit;

namespace KINGRecorder.Recording.Tests.Lifecycle;

public class RecordingEngineLifecycleTests
{
    private class FakeCaptureService : IScreenCaptureService
    {
        public event EventHandler<FrameArrivedEventArgs>? FrameArrived;
        public bool IsCapturing => false;
        public Task<IReadOnlyList<DisplayInfo>> GetDisplaysAsync(System.Threading.CancellationToken ct = default) => Task.FromResult<IReadOnlyList<DisplayInfo>>(Array.Empty<DisplayInfo>());
        public Task<IReadOnlyList<WindowInfo>> GetOpenWindowsAsync(System.Threading.CancellationToken ct = default) => Task.FromResult<IReadOnlyList<WindowInfo>>(Array.Empty<WindowInfo>());
        public Task StartCaptureAsync(RecordingOptions options, System.Threading.CancellationToken ct = default) => Task.CompletedTask;
        public Task StopCaptureAsync(System.Threading.CancellationToken ct = default) => Task.CompletedTask;
        public void Dispose() { }
    }

    private class FakeAudioService : IAudioCaptureService
    {
        public event EventHandler<AudioDataEventArgs>? AudioDataAvailable;
        public bool IsCapturing => false;
        public Task<IReadOnlyList<AudioDeviceInfo>> GetMicrophonesAsync(System.Threading.CancellationToken ct = default) => Task.FromResult<IReadOnlyList<AudioDeviceInfo>>(Array.Empty<AudioDeviceInfo>());
        public Task StartAsync(bool captureMic, string? micId, float micVol, bool captureSystem, float sysVol, System.Threading.CancellationToken ct = default) => Task.CompletedTask;
        public void UpdateVolumes(float micVol, float sysVol) { }
        public Task StopAsync(System.Threading.CancellationToken ct = default) => Task.CompletedTask;
        public void Dispose() { }
    }

    [Fact]
    public void RecordingEngine_InitialState_IsIdle()
    {
        using var engine = new RecordingEngine(
            NullLogger<RecordingEngine>.Instance,
            new FakeCaptureService(),
            new FakeAudioService(),
            new StubVideoCompositor(),
            new StubEncoderService()
        );

        Assert.Equal(RecordingState.Idle, engine.State);
        Assert.Equal(TimeSpan.Zero, engine.ElapsedDuration);
    }

    [Fact]
    public async Task StartRecording_WhenEncoderStubThrows_TransitionsToErrorState()
    {
        using var engine = new RecordingEngine(
            NullLogger<RecordingEngine>.Instance,
            new FakeCaptureService(),
            new FakeAudioService(),
            new StubVideoCompositor(),
            new StubEncoderService()
        );

        var options = new RecordingOptions(
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
            OutputDirectory: System.IO.Path.GetTempPath()
        );

        await Assert.ThrowsAsync<VideoEncoderException>(() => engine.StartRecordingAsync(options));
        Assert.Equal(RecordingState.Error, engine.State);
    }
}
