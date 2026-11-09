using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using KINGRecorder.Core.Enums;
using KINGRecorder.Core.Interfaces;
using KINGRecorder.Core.Models;
using KINGRecorder.Recording;
using KINGRecorder.Recording.Tests.Fakes;
using Xunit;

namespace KINGRecorder.Recording.Tests.Lifecycle;

/// <summary>
/// Proves that a misbehaving event subscriber cannot change the outcome of a recording operation.
/// </summary>
public sealed class EventNotificationIsolationTests : IDisposable
{
    private readonly FakeScreenCaptureService _capture = new();
    private readonly FakeAudioCaptureService _audio = new();
    private readonly FakeVideoCompositor _compositor = new();
    private readonly FakeEncoderService _encoder = new();
    private readonly string _outputDirectory;

    public EventNotificationIsolationTests()
    {
        _outputDirectory = Path.Combine(Path.GetTempPath(), "king_rec_event_tests", Guid.NewGuid().ToString("N"));
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
    public async Task ThrowingStateChangedSubscriber_StillLetsOtherSubscribersReceiveTransitions()
    {
        _encoder.WriteOutputFile = true;
        using var engine = CreateEngine();
        var received = new List<RecordingState>();

        engine.StateChanged += (_, _) => throw new InvalidOperationException("Simulated subscriber failure.");
        engine.StateChanged += (_, e) => received.Add(e.NewState);

        await engine.StartRecordingAsync(CreateOptions());

        Assert.Equal(RecordingState.Recording, engine.State);
        Assert.Equal(new[] { RecordingState.Initializing, RecordingState.Recording }, received);
    }

    [Fact]
    public async Task ThrowingStateChangedSubscriber_CannotCorruptTheStateMachine()
    {
        _encoder.WriteOutputFile = true;
        using var engine = CreateEngine();
        var transitions = new List<(RecordingState From, RecordingState To)>();

        engine.StateChanged += (_, _) => throw new InvalidOperationException("Simulated subscriber failure.");
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
        Assert.Equal(RecordingState.Idle, engine.State);
    }

    [Fact]
    public async Task Subscribers_ObserveTheCommittedStateWhileTheHandlerRuns()
    {
        _encoder.WriteOutputFile = true;
        using var engine = CreateEngine();
        var observations = new List<(RecordingState Reported, RecordingState Actual)>();

        engine.StateChanged += (_, e) => observations.Add((e.NewState, engine.State));

        await engine.StartRecordingAsync(CreateOptions());
        await engine.StopRecordingAsync();

        Assert.NotEmpty(observations);
        Assert.All(observations, observation => Assert.Equal(observation.Reported, observation.Actual));
    }

    [Fact]
    public async Task ThrowingSubscriber_DoesNotReplaceTheOriginalStartFailure()
    {
        _encoder.InitializeFailure = new NotSupportedException("Encoder pipeline is scheduled for Phase 4.");
        using var engine = CreateEngine();
        engine.StateChanged += (_, _) => throw new InvalidOperationException("Simulated subscriber failure.");

        var exception = await Assert.ThrowsAsync<NotSupportedException>(() => engine.StartRecordingAsync(CreateOptions()));

        Assert.Equal("Encoder pipeline is scheduled for Phase 4.", exception.Message);
        Assert.Equal(RecordingState.Error, engine.State);
    }

    [Fact]
    public async Task ThrowingSubscriber_DoesNotReplaceTheOriginalStopFailure()
    {
        _encoder.WriteOutputFile = true;
        using var engine = CreateEngine();
        await engine.StartRecordingAsync(CreateOptions());
        _capture.StopFailureCount = 1;
        engine.StateChanged += (_, _) => throw new InvalidOperationException("Simulated subscriber failure.");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => engine.StopRecordingAsync());

        Assert.Equal("Simulated screen capture stop failure.", exception.Message);
        Assert.Equal(RecordingState.Error, engine.State);
    }

    [Fact]
    public async Task ThrowingDurationUpdatedSubscriber_CannotInvalidateAFinalizedRecording()
    {
        _encoder.WriteOutputFile = true;
        using var engine = CreateEngine();
        await engine.StartRecordingAsync(CreateOptions());
        var secondSubscriberNotified = false;

        engine.DurationUpdated += (_, _) => throw new InvalidOperationException("Simulated subscriber failure.");
        engine.DurationUpdated += (_, _) => secondSubscriberNotified = true;

        var item = await engine.StopRecordingAsync();

        Assert.True(secondSubscriberNotified);
        Assert.True(File.Exists(item.FilePath));
        Assert.True(item.FileSizeBytes > 0);
        Assert.Equal(RecordingState.Idle, engine.State);
        Assert.True(item.Duration >= TimeSpan.Zero);
    }
}
