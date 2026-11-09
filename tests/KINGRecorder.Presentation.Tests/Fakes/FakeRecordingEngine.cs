using System;
using System.Threading;
using System.Threading.Tasks;
using KINGRecorder.Core.Enums;
using KINGRecorder.Core.Interfaces;
using KINGRecorder.Core.Models;

namespace KINGRecorder.Presentation.Tests.Fakes;

/// <summary>
/// Recording engine double that counts subscriptions and lets a test raise engine events from any thread.
/// </summary>
internal sealed class FakeRecordingEngine : IRecordingEngine
{
    private EventHandler<RecordingStateChangedEventArgs>? _stateChanged;
    private EventHandler<TimeSpan>? _durationUpdated;

    public int StateSubscriberAdds { get; private set; }

    public int StateSubscriberRemoves { get; private set; }

    public int DurationSubscriberAdds { get; private set; }

    public int DurationSubscriberRemoves { get; private set; }

    public RecordingState State { get; set; } = RecordingState.Idle;

    public TimeSpan ElapsedDuration => TimeSpan.Zero;

    public bool IsDisposed { get; private set; }

    public event EventHandler<RecordingStateChangedEventArgs>? StateChanged
    {
        add
        {
            StateSubscriberAdds++;
            _stateChanged += value;
        }

        remove
        {
            StateSubscriberRemoves++;
            _stateChanged -= value;
        }
    }

    public event EventHandler<TimeSpan>? DurationUpdated
    {
        add
        {
            DurationSubscriberAdds++;
            _durationUpdated += value;
        }

        remove
        {
            DurationSubscriberRemoves++;
            _durationUpdated -= value;
        }
    }

    public void RaiseStateChanged(RecordingState oldState, RecordingState newState, string? errorMessage = null)
    {
        State = newState;
        _stateChanged?.Invoke(this, new RecordingStateChangedEventArgs(oldState, newState, errorMessage));
    }

    public void RaiseDurationUpdated(TimeSpan elapsed)
    {
        _durationUpdated?.Invoke(this, elapsed);
    }

    public Task StartRecordingAsync(RecordingOptions options, CancellationToken ct = default) => Task.CompletedTask;

    public Task PauseRecordingAsync(CancellationToken ct = default) => Task.CompletedTask;

    public Task ResumeRecordingAsync(CancellationToken ct = default) => Task.CompletedTask;

    public Task<RecordingItem> StopRecordingAsync(CancellationToken ct = default) =>
        throw new NotSupportedException("The ViewModel threading tests never stop a session through the engine.");

    public Task CancelRecordingAsync(CancellationToken ct = default) => Task.CompletedTask;

    public Task<string> CaptureScreenshotAsync(RecordingOptions options, CancellationToken ct = default) =>
        Task.FromResult(string.Empty);

    public Task ShutdownAsync() => Task.CompletedTask;

    public void Dispose() => IsDisposed = true;

    public ValueTask DisposeAsync()
    {
        IsDisposed = true;
        return ValueTask.CompletedTask;
    }
}
