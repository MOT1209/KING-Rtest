using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using KINGRecorder.Core.Enums;
using KINGRecorder.Core.Models;
using KINGRecorder.Recording;
using KINGRecorder.Recording.Tests.Fakes;
using Xunit;

namespace KINGRecorder.Recording.Tests.Lifecycle;

/// <summary>
/// Proves that shutdown is coordinated with an in-flight lifecycle operation and that a failed or
/// cancelled initialization does not leave an output file behind.
/// </summary>
public sealed class EngineShutdownTests : IDisposable
{
    private readonly FakeScreenCaptureService _capture = new();
    private readonly FakeAudioCaptureService _audio = new();
    private readonly FakeVideoCompositor _compositor = new();
    private readonly FakeEncoderService _encoder = new();
    private readonly string _outputDirectory;

    public EngineShutdownTests()
    {
        _outputDirectory = Path.Combine(Path.GetTempPath(), "king_rec_shutdown_tests", Guid.NewGuid().ToString("N"));
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
    public async Task ShutdownAsync_WhenIdle_IsRepeatableAndBlocksFurtherOperations()
    {
        var engine = CreateEngine();

        await engine.ShutdownAsync();
        await engine.ShutdownAsync();
        engine.Dispose();

        Assert.Equal(RecordingState.Idle, engine.State);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => engine.StartRecordingAsync(CreateOptions()));
        await Assert.ThrowsAsync<ObjectDisposedException>(() => engine.StopRecordingAsync());
        await Assert.ThrowsAsync<ObjectDisposedException>(() => engine.CancelRecordingAsync());
    }

    [Fact]
    public async Task ShutdownAsync_WhileRecording_StopsThePipelineAndDeletesTheIncompleteOutput()
    {
        _encoder.WriteOutputFile = true;
        var engine = CreateEngine();
        await engine.StartRecordingAsync(CreateOptions());

        var outputPath = _encoder.OutputPath;
        Assert.NotNull(outputPath);
        Assert.True(File.Exists(outputPath));

        await engine.ShutdownAsync();

        Assert.Equal(1, _capture.StopCount);
        Assert.Equal(1, _audio.StopCount);
        Assert.Equal(1, _encoder.FinalizeCount);
        Assert.False(File.Exists(outputPath));
        Assert.Equal(TimeSpan.Zero, engine.ElapsedDuration);

        // Recording -> Idle is not a legal transition, so the aborted session is reported honestly as an error.
        Assert.Equal(RecordingState.Error, engine.State);
    }

    [Fact]
    public async Task ShutdownAsync_WhileInitializationIsInFlight_WaitsForItAndThenCleansUp()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _encoder.InitializeGate = gate;
        _encoder.WriteOutputFile = true;

        var engine = CreateEngine();
        var start = engine.StartRecordingAsync(CreateOptions());
        await WaitUntilAsync(() => _encoder.InitializeCount == 1);

        var shutdown = engine.ShutdownAsync();
        Assert.False(shutdown.IsCompleted);

        gate.SetResult();
        await start;
        await shutdown;

        Assert.Equal(1, _capture.StartCount);
        Assert.Equal(1, _capture.StopCount);
        Assert.Equal(1, _audio.StopCount);
        Assert.False(File.Exists(_encoder.OutputPath));
    }

    [Fact]
    public async Task ShutdownAsync_RejectsAStartThatWasQueuedBehindAnotherOperation()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _encoder.InitializeGate = gate;
        _encoder.WriteOutputFile = true;

        var engine = CreateEngine();
        var first = engine.StartRecordingAsync(CreateOptions());
        await WaitUntilAsync(() => _encoder.InitializeCount == 1);

        var queued = engine.StartRecordingAsync(CreateOptions());
        var shutdown = engine.ShutdownAsync();

        gate.SetResult();
        await first;

        await Assert.ThrowsAsync<ObjectDisposedException>(() => queued);
        await shutdown;

        Assert.Equal(1, _capture.StartCount);
    }

    [Fact]
    public async Task ShutdownAsync_WhenCleanupFails_StillCompletesWithoutThrowing()
    {
        _encoder.WriteOutputFile = true;
        var engine = CreateEngine();
        await engine.StartRecordingAsync(CreateOptions());

        _capture.StopFailureCount = 1;

        await engine.ShutdownAsync();

        Assert.Equal(1, _capture.StopCount);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => engine.StartRecordingAsync(CreateOptions()));
    }

    [Fact]
    public async Task FailedInitialization_DeletesThePartiallyWrittenOutput()
    {
        _encoder.WriteOutputFile = true;
        _encoder.FailAfterWritingOutput = new NotSupportedException("Encoder failed after opening the output file.");
        using var engine = CreateEngine();

        await Assert.ThrowsAsync<NotSupportedException>(() => engine.StartRecordingAsync(CreateOptions()));

        var outputPath = _encoder.OutputPath;
        Assert.NotNull(outputPath);
        Assert.False(File.Exists(outputPath));
        Assert.Equal(RecordingState.Error, engine.State);
    }

    [Fact]
    public async Task CancelledInitialization_DeletesThePartiallyWrittenOutput()
    {
        _encoder.WriteOutputFile = true;
        _capture.StartFailure = new OperationCanceledException("Simulated cancellation during initialization.");
        using var engine = CreateEngine();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => engine.StartRecordingAsync(CreateOptions()));

        var outputPath = _encoder.OutputPath;
        Assert.NotNull(outputPath);
        Assert.False(File.Exists(outputPath));
        Assert.Equal(RecordingState.Idle, engine.State);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var stopwatch = Stopwatch.StartNew();
        while (!condition())
        {
            if (stopwatch.Elapsed > TimeSpan.FromSeconds(10))
            {
                throw new TimeoutException("The awaited condition was not reached within 10 seconds.");
            }

            await Task.Delay(10).ConfigureAwait(false);
        }
    }
}
