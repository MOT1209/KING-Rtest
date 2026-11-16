using System;
using System.IO;
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

/// <summary>
/// Proves that engine-owned shutdown cancels in-flight recording work, releases partially initialized
/// resources and never deadlocks.
/// </summary>
public sealed class ShutdownCancellationTests : IDisposable
{
    private readonly FakeScreenCaptureService _capture = new();
    private readonly FakeAudioCaptureService _audio = new();
    private readonly FakeVideoCompositor _compositor = new();
    private readonly FakeEncoderService _encoder = new();
    private readonly string _outputDirectory;

    public ShutdownCancellationTests()
    {
        _outputDirectory = Path.Combine(Path.GetTempPath(), "king_rec_shutdown_cancellation_tests", Guid.NewGuid().ToString("N"));
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
    public async Task Shutdown_DuringActiveInitialization_CancelsTheInitializationAndExitsCleanly()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _encoder.InitializeGate = gate;
        _encoder.WriteOutputFile = true;

        using var engine = CreateEngine();
        var startTask = engine.StartRecordingAsync(CreateOptions());
        await _encoder.InitializationEntered.Task;

        var shutdownTask = engine.ShutdownAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => startTask);
        Assert.True(_encoder.CancellationObserved);

        await shutdownTask;

        Assert.Equal(RecordingState.Idle, engine.State);
        Assert.Equal(0, _capture.StartCount);
        Assert.Equal(0, _audio.StartCount);
    }

    [Fact]
    public async Task Shutdown_DuringActiveInitialization_ReleasesThePartiallyInitializedSession()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _encoder.InitializeGate = gate;
        _encoder.WriteOutputFile = true;

        using var engine = CreateEngine();
        var startTask = engine.StartRecordingAsync(CreateOptions());
        await _encoder.InitializationEntered.Task;

        var shutdownTask = engine.ShutdownAsync();
        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => startTask);
        await shutdownTask;

        Assert.IsAssignableFrom<OperationCanceledException>(exception);
        Assert.Equal(RecordingState.Idle, engine.State);
    }

    [Fact]
    public async Task ShutdownAsync_DuringInitialization_CompletesWithoutDeadlock()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _encoder.InitializeGate = gate;
        _encoder.WriteOutputFile = true;

        using var engine = CreateEngine();
        var startTask = engine.StartRecordingAsync(CreateOptions());
        await _encoder.InitializationEntered.Task;

        var shutdownTask = engine.ShutdownAsync();

        var shutdownCompleted = await Task.WhenAny(shutdownTask, Task.Delay(TimeSpan.FromSeconds(15)));
        Assert.Same(shutdownTask, shutdownCompleted);
        await shutdownTask;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => startTask);
    }

    [Fact]
    public async Task ShutdownAsync_CalledRepeatedly_IsSafe()
    {
        using var engine = CreateEngine();

        var first = engine.ShutdownAsync();
        var second = engine.ShutdownAsync();

        await first;
        await second;

        Assert.Equal(RecordingState.Idle, engine.State);
    }

    [Fact]
    public async Task ShutdownAsync_AfterShutdown_RejectsNewRecordingRequests()
    {
        using var engine = CreateEngine();

        await engine.ShutdownAsync();
        await engine.ShutdownAsync();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => engine.StartRecordingAsync(CreateOptions()));
        await Assert.ThrowsAsync<ObjectDisposedException>(() => engine.StopRecordingAsync());
        await Assert.ThrowsAsync<ObjectDisposedException>(() => engine.CancelRecordingAsync());
        await Assert.ThrowsAsync<ObjectDisposedException>(() => engine.CaptureScreenshotAsync(CreateOptions()));
    }
}
