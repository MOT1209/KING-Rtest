using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using KINGRecorder.Core.Enums;
using KINGRecorder.Infrastructure;
using KINGRecorder.Presentation.Tests.Fakes;
using KINGRecorder.Presentation.ViewModels;

namespace KINGRecorder.Presentation.Tests.ViewModels;

/// <summary>
/// Verifies that engine notifications reaching the ViewModel from a non-UI thread are marshalled through
/// the injected dispatcher, that ordering is preserved, and that disposal cannot race queued callbacks.
/// No WinUI window or DispatcherQueue is required.
/// </summary>
public sealed class MainViewModelThreadingTests
{
    private static (MainViewModel ViewModel, FakeRecordingEngine Engine, FakeUiDispatcher Dispatcher) Create()
    {
        var engine = new FakeRecordingEngine();
        var dispatcher = new FakeUiDispatcher();
        var settingsPath = Path.Combine(
            Path.GetTempPath(),
            "king_rec_vm_tests",
            Guid.NewGuid().ToString("N"),
            "settings.json");
        var settingsManager = new SettingsManager(NullLogger<SettingsManager>.Instance, settingsPath);

        var viewModel = new MainViewModel(
            engine,
            settingsManager,
            dispatcher,
            NullLogger<MainViewModel>.Instance);

        return (viewModel, engine, dispatcher);
    }

    [Fact]
    public async Task StateChangedFromABackgroundThread_IsOnlyAppliedByTheDispatcher()
    {
        var (viewModel, engine, dispatcher) = Create();

        await Task.Run(() => engine.RaiseStateChanged(RecordingState.Idle, RecordingState.Recording));

        // The engine's thread must not touch bindings: the update waits for the dispatcher.
        Assert.Equal(RecordingState.Idle, viewModel.CurrentState);
        Assert.False(viewModel.IsRecording);
        Assert.Equal(1, dispatcher.EnqueueCount);
        Assert.Equal(1, dispatcher.PendingCount);

        dispatcher.RunPending();

        Assert.Equal(RecordingState.Recording, viewModel.CurrentState);
        Assert.True(viewModel.IsRecording);
        Assert.False(viewModel.IsPaused);
    }

    [Fact]
    public void DurationUpdated_IsFormattedWhenTheQueuedUpdateRuns()
    {
        var (viewModel, engine, dispatcher) = Create();

        engine.RaiseDurationUpdated(TimeSpan.FromSeconds(75));
        Assert.Equal("00:00:00", viewModel.DurationText);

        dispatcher.RunPending();

        Assert.Equal("00:01:15", viewModel.DurationText);
    }

    [Fact]
    public void Notifications_AreAppliedInTheOrderTheEngineRaisedThem()
    {
        var (viewModel, engine, dispatcher) = Create();

        engine.RaiseStateChanged(RecordingState.Idle, RecordingState.Initializing);
        engine.RaiseStateChanged(RecordingState.Initializing, RecordingState.Recording);
        engine.RaiseStateChanged(RecordingState.Recording, RecordingState.Stopping);

        Assert.Equal(3, dispatcher.PendingCount);
        Assert.Equal(RecordingState.Idle, viewModel.CurrentState);

        Assert.True(dispatcher.RunNext());
        Assert.Equal(RecordingState.Initializing, viewModel.CurrentState);

        Assert.True(dispatcher.RunNext());
        Assert.Equal(RecordingState.Recording, viewModel.CurrentState);

        Assert.True(dispatcher.RunNext());
        Assert.Equal(RecordingState.Stopping, viewModel.CurrentState);
        Assert.False(dispatcher.RunNext());
    }

    [Fact]
    public void RejectedEnqueue_DoesNotThrowAndLeavesBindingsUntouched()
    {
        var (viewModel, engine, dispatcher) = Create();
        dispatcher.RejectEnqueue = true;

        var exception = Record.Exception(() => engine.RaiseStateChanged(RecordingState.Idle, RecordingState.Recording));

        Assert.Null(exception);
        Assert.Equal(1, dispatcher.RejectedCount);
        Assert.Equal(RecordingState.Idle, viewModel.CurrentState);
    }

    [Fact]
    public void ThrowingDispatcher_DoesNotEscapeIntoTheEngine()
    {
        var (_, engine, dispatcher) = Create();
        dispatcher.ThrowOnEnqueue = true;

        var exception = Record.Exception(() => engine.RaiseStateChanged(RecordingState.Idle, RecordingState.Recording));

        Assert.Null(exception);
    }

    [Fact]
    public void DisposedViewModel_IgnoresCallbacksThatWereAlreadyQueued()
    {
        var (viewModel, engine, dispatcher) = Create();
        engine.RaiseStateChanged(RecordingState.Idle, RecordingState.Recording);
        Assert.Equal(1, dispatcher.PendingCount);

        viewModel.Dispose();
        dispatcher.RunPending();

        Assert.Equal(RecordingState.Idle, viewModel.CurrentState);
        Assert.False(viewModel.IsRecording);
    }

    [Fact]
    public void DisposedViewModel_StopsQueueingNewUiWork()
    {
        var (viewModel, engine, dispatcher) = Create();

        viewModel.Dispose();
        engine.RaiseStateChanged(RecordingState.Idle, RecordingState.Recording);
        engine.RaiseDurationUpdated(TimeSpan.FromSeconds(5));

        Assert.Equal(0, dispatcher.EnqueueCount);
        Assert.Equal(0, dispatcher.PendingCount);
    }

    [Fact]
    public void Dispose_RemovesEachSubscriptionExactlyOnce()
    {
        var (viewModel, engine, _) = Create();

        viewModel.Dispose();
        viewModel.Dispose();

        Assert.Equal(1, engine.StateSubscriberAdds);
        Assert.Equal(1, engine.StateSubscriberRemoves);
        Assert.Equal(1, engine.DurationSubscriberAdds);
        Assert.Equal(1, engine.DurationSubscriberRemoves);
        Assert.True(viewModel.IsDisposed);
    }

    [Fact]
    public void EngineErrorMessage_IsSurfacedThroughTheDispatcher()
    {
        var (viewModel, engine, dispatcher) = Create();

        engine.RaiseStateChanged(RecordingState.Recording, RecordingState.Error, "Encoder unavailable.");
        dispatcher.RunPending();

        Assert.Equal(RecordingState.Error, viewModel.CurrentState);
        Assert.Equal("Encoder unavailable.", viewModel.ErrorMessage);
    }
}
