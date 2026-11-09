using System;
using System.Collections.Generic;
using System.Threading;
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
        public bool IsDisposed { get; private set; }
        public Task<IReadOnlyList<DisplayInfo>> GetDisplaysAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<DisplayInfo>>(Array.Empty<DisplayInfo>());
        public Task<IReadOnlyList<WindowInfo>> GetOpenWindowsAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<WindowInfo>>(Array.Empty<WindowInfo>());
        public Task StartCaptureAsync(RecordingOptions options, CancellationToken ct = default) => Task.CompletedTask;
        public Task StopCaptureAsync(CancellationToken ct = default) => Task.CompletedTask;
        public void Dispose() { IsDisposed = true; }
    }

    private class FakeAudioService : IAudioCaptureService
    {
        public event EventHandler<AudioDataEventArgs>? AudioDataAvailable;
        public bool IsCapturing => false;
        public bool IsDisposed { get; private set; }
        public Task<IReadOnlyList<AudioDeviceInfo>> GetMicrophonesAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<AudioDeviceInfo>>(Array.Empty<AudioDeviceInfo>());
        public Task StartAsync(bool captureMic, string? micId, float micVol, bool captureSystem, float sysVol, CancellationToken ct = default) => Task.CompletedTask;
        public void UpdateVolumes(float micVol, float sysVol) { }
        public Task StopAsync(CancellationToken ct = default) => Task.CompletedTask;
        public void Dispose() { IsDisposed = true; }
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

    [Fact]
    public async Task PauseRecording_WhenIdle_ThrowsInvalidOperationException()
    {
        using var engine = new RecordingEngine(
            NullLogger<RecordingEngine>.Instance,
            new FakeCaptureService(),
            new FakeAudioService(),
            new StubVideoCompositor(),
            new StubEncoderService()
        );

        await Assert.ThrowsAsync<InvalidOperationException>(() => engine.PauseRecordingAsync());
    }

    [Fact]
    public async Task ResumeRecording_WhenIdle_ThrowsInvalidOperationException()
    {
        using var engine = new RecordingEngine(
            NullLogger<RecordingEngine>.Instance,
            new FakeCaptureService(),
            new FakeAudioService(),
            new StubVideoCompositor(),
            new StubEncoderService()
        );

        await Assert.ThrowsAsync<InvalidOperationException>(() => engine.ResumeRecordingAsync());
    }

    [Fact]
    public async Task StopRecording_WhenIdle_ThrowsInvalidOperationException()
    {
        using var engine = new RecordingEngine(
            NullLogger<RecordingEngine>.Instance,
            new FakeCaptureService(),
            new FakeAudioService(),
            new StubVideoCompositor(),
            new StubEncoderService()
        );

        await Assert.ThrowsAsync<InvalidOperationException>(() => engine.StopRecordingAsync());
    }

    [Fact]
    public async Task CancelRecording_WhenIdle_ResetsToIdleWithoutError()
    {
        using var engine = new RecordingEngine(
            NullLogger<RecordingEngine>.Instance,
            new FakeCaptureService(),
            new FakeAudioService(),
            new StubVideoCompositor(),
            new StubEncoderService()
        );

        await engine.CancelRecordingAsync();
        Assert.Equal(RecordingState.Idle, engine.State);
    }

    [Fact]
    public void RecordingEngine_Dispose_DisposesInnerServices()
    {
        var fakeCapture = new FakeCaptureService();
        var fakeAudio = new FakeAudioService();

        var engine = new RecordingEngine(
            NullLogger<RecordingEngine>.Instance,
            fakeCapture,
            fakeAudio,
            new StubVideoCompositor(),
            new StubEncoderService()
        );

        engine.Dispose();

        Assert.True(fakeCapture.IsDisposed);
        Assert.True(fakeAudio.IsDisposed);
    }

    [Fact]
    public void RecordingEngine_DoubleDispose_IsIdempotent()
    {
        var engine = new RecordingEngine(
            NullLogger<RecordingEngine>.Instance,
            new FakeCaptureService(),
            new FakeAudioService(),
            new StubVideoCompositor(),
            new StubEncoderService()
        );

        engine.Dispose();
        engine.Dispose(); // second call must not throw
    }
}
