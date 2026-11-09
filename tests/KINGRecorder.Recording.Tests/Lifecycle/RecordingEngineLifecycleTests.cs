using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using KINGRecorder.Core.Enums;
using KINGRecorder.Core.Exceptions;
using KINGRecorder.Core.Interfaces;
using KINGRecorder.Core.Models;
using KINGRecorder.Recording;
using KINGRecorder.Recording.Tests.Fakes;
using Xunit;

namespace KINGRecorder.Recording.Tests.Lifecycle;

public sealed class RecordingEngineLifecycleTests : IDisposable
{
    private readonly FakeScreenCaptureService _capture = new();
    private readonly FakeAudioCaptureService _audio = new();
    private readonly FakeVideoCompositor _compositor = new();
    private readonly FakeEncoderService _encoder = new();
    private readonly string _outputDirectory;

    public RecordingEngineLifecycleTests()
    {
        _outputDirectory = Path.Combine(Path.GetTempPath(), "king_rec_engine_tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_outputDirectory);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_outputDirectory))
            {
                Directory.Delete(_outputDirectory, recursive: true);
            }
        }
        catch (IOException)
        {
            // Best-effort cleanup of the temp directory only.
        }
    }

    private RecordingEngine CreateEngine() =>
        new(NullLogger<RecordingEngine>.Instance, _capture, _audio, _compositor, _encoder);

    private RecordingOptions CreateOptions() => TestRecordingOptions.Create(_outputDirectory);

    [Fact]
    public void InitialState_IsIdle()
    {
        using var engine = CreateEngine();

        Assert.Equal(RecordingState.Idle, engine.State);
        Assert.Equal(TimeSpan.Zero, engine.ElapsedDuration);
    }

    [Fact]
    public async Task StartRecording_WhenEncoderUnavailable_PreservesExceptionTypeAndEntersErrorState()
    {
        _encoder.InitializeFailure = new NotSupportedException("Encoder pipeline is scheduled for Phase 4.");
        using var engine = CreateEngine();

        var exception = await Assert.ThrowsAsync<NotSupportedException>(() => engine.StartRecordingAsync(CreateOptions()));

        Assert.Equal("Encoder pipeline is scheduled for Phase 4.", exception.Message);
        Assert.Equal(RecordingState.Error, engine.State);
        Assert.Equal(0, _capture.StartCount);
        Assert.Equal(0, _audio.StartCount);
    }

    [Fact]
    public async Task StartRecording_AfterInitializationFailure_CanRecoverAndRecordAgain()
    {
        _encoder.InitializeFailure = new NotSupportedException("Encoder unavailable in Phase 0.2.");
        using var engine = CreateEngine();

        await Assert.ThrowsAsync<NotSupportedException>(() => engine.StartRecordingAsync(CreateOptions()));
        Assert.Equal(RecordingState.Error, engine.State);

        // Recovery path: a failed session must not lock the engine permanently.
        _encoder.InitializeFailure = null;
        _encoder.WriteOutputFile = true;

        await engine.StartRecordingAsync(CreateOptions());

        Assert.Equal(RecordingState.Recording, engine.State);
        Assert.Equal(1, _capture.StartCount);
        Assert.Equal(1, _audio.StartCount);
    }

    [Fact]
    public async Task StartRecording_WhenCaptureStartFails_ReleasesEncoderAndEntersErrorState()
    {
        _capture.StartFailure = new InvalidOperationException("Simulated capture start failure.");
        using var engine = CreateEngine();

        await Assert.ThrowsAsync<InvalidOperationException>(() => engine.StartRecordingAsync(CreateOptions()));

        Assert.Equal(RecordingState.Error, engine.State);
        Assert.Equal(1, _encoder.FinalizeCount);
        Assert.Equal(0, _audio.StartCount);
    }

    [Fact]
    public async Task StartRecording_WhileRecording_ThrowsAndKeepsSession()
    {
        _encoder.WriteOutputFile = true;
        using var engine = CreateEngine();
        await engine.StartRecordingAsync(CreateOptions());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => engine.StartRecordingAsync(CreateOptions()));

        Assert.Contains("Cannot start a recording", exception.Message, StringComparison.Ordinal);
        Assert.Equal(RecordingState.Recording, engine.State);
        Assert.Equal(1, _capture.StartCount);
    }

    [Fact]
    public async Task StartRecording_ConcurrentCalls_StartsExactlyOneSession()
    {
        _encoder.WriteOutputFile = true;
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _encoder.InitializeGate = gate;

        using var engine = CreateEngine();
        var options = CreateOptions();

        var first = engine.StartRecordingAsync(options);
        var second = engine.StartRecordingAsync(options);

        gate.SetResult();

        var outcomes = await Task.WhenAll(CaptureOutcomeAsync(first), CaptureOutcomeAsync(second));

        Assert.Single(outcomes, outcome => outcome is null);
        Assert.Single(outcomes, outcome => outcome is InvalidOperationException);
        Assert.Equal(RecordingState.Recording, engine.State);
        Assert.Equal(1, _capture.StartCount);
    }

    [Fact]
    public async Task StartRecording_WithCancelledToken_ThrowsAndLeavesEngineIdle()
    {
        using var engine = CreateEngine();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => engine.StartRecordingAsync(CreateOptions(), cts.Token));

        Assert.Equal(RecordingState.Idle, engine.State);
        Assert.Equal(0, _capture.StartCount);
        Assert.Equal(0, _encoder.InitializeCount);
    }

    [Fact]
    public async Task PauseRecording_WhileRecording_ReportsNotSupportedAndKeepsRecording()
    {
        _encoder.WriteOutputFile = true;
        using var engine = CreateEngine();
        await engine.StartRecordingAsync(CreateOptions());

        await Assert.ThrowsAsync<NotSupportedException>(() => engine.PauseRecordingAsync());

        Assert.Equal(RecordingState.Recording, engine.State);
        Assert.NotEqual(TimeSpan.Zero, engine.ElapsedDuration);
    }

    [Fact]
    public async Task ResumeRecording_WhenIdle_ThrowsInvalidOperationException()
    {
        using var engine = CreateEngine();

        await Assert.ThrowsAsync<InvalidOperationException>(() => engine.ResumeRecordingAsync());
    }

    [Fact]
    public async Task StopRecording_WhenIdle_ThrowsInvalidOperationException()
    {
        using var engine = CreateEngine();

        await Assert.ThrowsAsync<InvalidOperationException>(() => engine.StopRecordingAsync());
    }

    [Fact]
    public async Task StopRecording_ActiveSession_FinalizesAndReturnsRecordingItem()
    {
        _encoder.WriteOutputFile = true;
        using var engine = CreateEngine();
        await engine.StartRecordingAsync(CreateOptions());

        var item = await engine.StopRecordingAsync();

        Assert.Equal(RecordingState.Idle, engine.State);
        Assert.True(File.Exists(item.FilePath));
        Assert.Equal(Path.GetFileName(item.FilePath), item.Filename);
        Assert.True(item.FileSizeBytes > 0);
        Assert.Equal(60, item.Fps);
        Assert.Equal("P1080", item.Resolution);
        Assert.False(item.IsScreenshot);
        Assert.Equal(1, _capture.StopCount);
        Assert.Equal(1, _audio.StopCount);
        Assert.Equal(1, _encoder.FinalizeCount);
        Assert.Equal(TimeSpan.Zero, engine.ElapsedDuration);
    }

    [Fact]
    public async Task StopRecording_Twice_SecondCallThrowsAndLeavesEngineUsable()
    {
        _encoder.WriteOutputFile = true;
        using var engine = CreateEngine();
        await engine.StartRecordingAsync(CreateOptions());
        await engine.StopRecordingAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => engine.StopRecordingAsync());

        Assert.Equal(RecordingState.Idle, engine.State);
        Assert.Equal(1, _capture.StopCount);
    }

    [Fact]
    public async Task StopRecording_WhenEncoderProducedNoFile_EntersErrorAndCanBeCancelledBackToIdle()
    {
        _encoder.WriteOutputFile = false;
        using var engine = CreateEngine();
        await engine.StartRecordingAsync(CreateOptions());

        await Assert.ThrowsAsync<VideoEncoderException>(() => engine.StopRecordingAsync());
        Assert.Equal(RecordingState.Error, engine.State);

        await engine.CancelRecordingAsync();

        Assert.Equal(RecordingState.Idle, engine.State);
    }

    [Fact]
    public async Task StopRecording_WhenEncoderFinalizeThrows_EntersErrorState()
    {
        _encoder.WriteOutputFile = true;
        using var engine = CreateEngine();
        await engine.StartRecordingAsync(CreateOptions());
        _capture.StopFailureCount = 1;

        await Assert.ThrowsAsync<InvalidOperationException>(() => engine.StopRecordingAsync());

        Assert.Equal(RecordingState.Error, engine.State);
        await engine.CancelRecordingAsync();
        Assert.Equal(RecordingState.Idle, engine.State);
    }

    [Fact]
    public async Task CancelRecording_WhileRecording_StopsPipelineAndDeletesPartialOutput()
    {
        _encoder.WriteOutputFile = true;
        using var engine = CreateEngine();
        await engine.StartRecordingAsync(CreateOptions());
        var partialPath = _encoder.OutputPath;
        Assert.NotNull(partialPath);
        Assert.True(File.Exists(partialPath));

        await engine.CancelRecordingAsync();

        Assert.Equal(RecordingState.Idle, engine.State);
        Assert.False(File.Exists(partialPath));
        Assert.Equal(1, _capture.StopCount);
        Assert.Equal(1, _audio.StopCount);
        Assert.Equal(TimeSpan.Zero, engine.ElapsedDuration);
    }

    [Fact]
    public async Task CancelRecording_WhenIdle_IsANoOp()
    {
        using var engine = CreateEngine();

        await engine.CancelRecordingAsync();

        Assert.Equal(RecordingState.Idle, engine.State);
        Assert.Equal(0, _capture.StopCount);
    }

    [Fact]
    public async Task StateChanged_ReportsOrderedLifecycleTransitions()
    {
        _encoder.WriteOutputFile = true;
        using var engine = CreateEngine();
        var transitions = new List<(RecordingState From, RecordingState To)>();
        engine.StateChanged += (_, e) => transitions.Add((e.OldState, e.NewState));

        await engine.StartRecordingAsync(CreateOptions());
        await engine.StopRecordingAsync();

        Assert.Equal(
            new (RecordingState From, RecordingState To)[]
            {
                (RecordingState.Idle, RecordingState.Initializing),
                (RecordingState.Initializing, RecordingState.Recording),
                (RecordingState.Recording, RecordingState.Stopping),
                (RecordingState.Stopping, RecordingState.Finalizing),
                (RecordingState.Finalizing, RecordingState.Completed),
                (RecordingState.Completed, RecordingState.Idle),
            },
            transitions);
    }

    [Fact]
    public async Task DurationUpdated_IsRaisedWithFinalDurationOnStop()
    {
        _encoder.WriteOutputFile = true;
        using var engine = CreateEngine();
        await engine.StartRecordingAsync(CreateOptions());
        TimeSpan? reported = null;
        engine.DurationUpdated += (_, duration) => reported = duration;

        var item = await engine.StopRecordingAsync();

        Assert.NotNull(reported);
        Assert.Equal(item.Duration, reported.Value);
    }

    [Fact]
    public void Dispose_IsIdempotentAndDoesNotDisposeExternallyOwnedServices()
    {
        var engine = CreateEngine();

        engine.Dispose();
        engine.Dispose();

        Assert.False(_capture.IsDisposed);
        Assert.False(_audio.IsDisposed);
        Assert.False(_compositor.IsDisposed);
        Assert.False(_encoder.IsDisposed);
    }

    [Fact]
    public async Task Dispose_WhileRecording_PerformsBestEffortPipelineShutdown()
    {
        _encoder.WriteOutputFile = true;
        var engine = CreateEngine();
        await engine.StartRecordingAsync(CreateOptions());

        engine.Dispose();

        Assert.Equal(1, _capture.StopCount);
        Assert.Equal(1, _audio.StopCount);
    }

    [Fact]
    public async Task Operations_AfterDispose_ThrowObjectDisposedException()
    {
        var engine = CreateEngine();
        engine.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => engine.StartRecordingAsync(CreateOptions()));
        await Assert.ThrowsAsync<ObjectDisposedException>(() => engine.StopRecordingAsync());
        await Assert.ThrowsAsync<ObjectDisposedException>(() => engine.CancelRecordingAsync());
        await Assert.ThrowsAsync<ObjectDisposedException>(() => engine.CaptureScreenshotAsync(CreateOptions()));
        await Assert.ThrowsAsync<ObjectDisposedException>(() => engine.PauseRecordingAsync());
    }

    [Fact]
    public async Task CaptureScreenshot_IsHonestlyNotImplemented()
    {
        using var engine = CreateEngine();

        await Assert.ThrowsAsync<NotSupportedException>(() => engine.CaptureScreenshotAsync(CreateOptions()));
    }

    private static async Task<Exception?> CaptureOutcomeAsync(Task task)
    {
        try
        {
            await task.ConfigureAwait(false);
            return null;
        }
        catch (Exception ex)
        {
            return ex;
        }
    }
}
