using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using KINGRecorder.Core.Enums;
using KINGRecorder.Core.Exceptions;
using KINGRecorder.Core.Interfaces;
using KINGRecorder.Core.Models;
using KINGRecorder.Core.State;

namespace KINGRecorder.Recording;

/// <summary>
/// Lifecycle orchestrator for a recording session.
/// </summary>
/// <remarks>
/// <para>
/// This engine owns the <em>session lifecycle</em> only: state transitions, cancellation, partial
/// initialization cleanup, error recovery and shutdown. It does not own the injected pipeline services
/// (screen capture, audio capture, compositor, encoder); those are created and disposed by the
/// dependency-injection composition root.
/// </para>
/// <para>
/// Event subscribers are isolated from the lifecycle: the internal state is committed before any
/// subscriber runs, every subscriber is invoked inside its own <c>try</c>/<c>catch</c>, and a failing
/// subscriber can neither abort a transition nor replace the exception a recording operation is about to
/// throw. Subscriber failures are logged with the transition they were observing.
/// </para>
/// <para>
/// Shutdown is coordinated through both gates: <see cref="ShutdownAsync"/> waits for an in-flight
/// Start/Stop to finish, aborts whatever part of the pipeline was started, removes the incomplete output
/// file, then refuses all further operations. <see cref="Dispose"/> remains available as a bounded
/// best-effort path for callers that cannot await, and never disposes container-owned services.
/// </para>
/// <para>
/// Honest limitations in Phase 0.2.1, expressed in code rather than in comments alone:
/// <list type="bullet">
/// <item><description>Screen capture, audio capture, video composition and encoding are not implemented.
/// The registered implementations throw <see cref="NotSupportedException"/>.</description></item>
/// <item><description>Pause/resume are not supported, because there is no capture or encoding pipeline to suspend.
/// <see cref="PauseRecordingAsync"/> throws instead of only stopping a timer.</description></item>
/// <item><description><see cref="DurationUpdated"/> is raised once with the final duration when a session is
/// stopped; periodic ticking arrives together with the real capture pipeline.</description></item>
/// </list>
/// </para>
/// </remarks>
public sealed class RecordingEngine : IRecordingEngine, IAsyncDisposable
{
    /// <summary>Upper bound for the synchronous <see cref="Dispose"/> path, which cannot await.</summary>
    private const int BoundedDisposeWaitMilliseconds = 5_000;

    private const int LifecycleRunning = 0;
    private const int LifecycleShuttingDown = 1;
    private const int LifecycleShutdownCompleted = 2;

    private readonly ILogger<RecordingEngine> _logger;
    private readonly IScreenCaptureService _screenCapture;
    private readonly IAudioCaptureService _audioCapture;
    private readonly IVideoCompositor _compositor;
    private readonly IEncoderService _encoder;

    /// <summary>Serializes lifecycle operations so concurrent Start/Stop/Cancel calls cannot race.</summary>
    private readonly SemaphoreSlim _lifecycleGate = new(1, 1);

    /// <summary>Serializes shutdown so repeated or concurrent shutdown requests clean up exactly once.</summary>
    private readonly SemaphoreSlim _shutdownGate = new(1, 1);

    private readonly Stopwatch _stopwatch = new();

    private RecordingOptions? _currentOptions;
    private string? _currentOutputPath;
    private bool _encoderStarted;
    private bool _captureStarted;
    private bool _audioStarted;

    private int _state = (int)RecordingState.Idle;
    private int _lifecycle = LifecycleRunning;

    public RecordingState State => (RecordingState)Volatile.Read(ref _state);

    public TimeSpan ElapsedDuration => _stopwatch.Elapsed;

    public event EventHandler<RecordingStateChangedEventArgs>? StateChanged;

    public event EventHandler<TimeSpan>? DurationUpdated;

    public RecordingEngine(
        ILogger<RecordingEngine> logger,
        IScreenCaptureService screenCapture,
        IAudioCaptureService audioCapture,
        IVideoCompositor compositor,
        IEncoderService encoder)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _screenCapture = screenCapture ?? throw new ArgumentNullException(nameof(screenCapture));
        _audioCapture = audioCapture ?? throw new ArgumentNullException(nameof(audioCapture));
        _compositor = compositor ?? throw new ArgumentNullException(nameof(compositor));
        _encoder = encoder ?? throw new ArgumentNullException(nameof(encoder));
    }

    /// <summary>
    /// Commits the transition to the internal state machine first, then notifies subscribers. The commit
    /// cannot be rolled back or corrupted by a subscriber, and a throwing subscriber is logged and skipped.
    /// </summary>
    private void SetState(RecordingState newState, string? error = null)
    {
        var oldState = State;
        RecordingStateTransitions.EnsureTransition(oldState, newState);
        Volatile.Write(ref _state, (int)newState);

        if (error is null)
        {
            _logger.LogInformation("RecordingEngine state transition: {OldState} -> {NewState}", oldState, newState);
        }
        else
        {
            _logger.LogWarning("RecordingEngine state transition: {OldState} -> {NewState}. Reason: {Reason}", oldState, newState, error);
        }

        RaiseStateChangedSafely(new RecordingStateChangedEventArgs(oldState, newState, error));
    }

    /// <summary>
    /// Invokes every <see cref="StateChanged"/> subscriber in subscription order, isolating each one.
    /// </summary>
    private void RaiseStateChangedSafely(RecordingStateChangedEventArgs args)
    {
        var handler = StateChanged;
        if (handler is null)
        {
            return;
        }

        foreach (var subscriber in handler.GetInvocationList())
        {
            try
            {
                ((EventHandler<RecordingStateChangedEventArgs>)subscriber)(this, args);
            }
            catch (Exception ex)
            {
                // Notification never participates in the recording outcome, so the failure is logged and
                // the remaining subscribers are still notified.
                _logger.LogError(
                    ex,
                    "A StateChanged subscriber threw. Transition={OldState} -> {NewState}, Subscriber={Subscriber}. The exception was isolated from the recording lifecycle.",
                    args.OldState,
                    args.NewState,
                    DescribeSubscriber(subscriber));
            }
        }
    }

    /// <summary>
    /// Invokes every <see cref="DurationUpdated"/> subscriber in subscription order, isolating each one.
    /// </summary>
    private void RaiseDurationUpdatedSafely(TimeSpan duration)
    {
        var handler = DurationUpdated;
        if (handler is null)
        {
            return;
        }

        foreach (var subscriber in handler.GetInvocationList())
        {
            try
            {
                ((EventHandler<TimeSpan>)subscriber)(this, duration);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "A DurationUpdated subscriber threw. Duration={Duration}, Subscriber={Subscriber}. The exception was isolated from the recording lifecycle.",
                    duration,
                    DescribeSubscriber(subscriber));
            }
        }
    }

    private static string DescribeSubscriber(Delegate subscriber) =>
        subscriber.Method.DeclaringType?.FullName ?? subscriber.Method.Name;

    public async Task StartRecordingAsync(RecordingOptions options, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        ThrowIfShuttingDown();

        await _lifecycleGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            // A shutdown that began while this call was queued must win: nothing has been initialized yet.
            ThrowIfShuttingDown();

            var currentState = State;
            if (currentState is not (RecordingState.Idle or RecordingState.Error))
            {
                throw new InvalidOperationException(
                    $"Cannot start a recording from state {currentState}. A recording can only be started from {RecordingState.Idle} or {RecordingState.Error}. " +
                    "Stop or cancel the active session first.");
            }

            if (currentState == RecordingState.Error)
            {
                _logger.LogInformation("Recovering from a previously failed recording session before starting a new one.");
            }

            _currentOptions = options;
            _currentOutputPath = null;
            _encoderStarted = false;
            _captureStarted = false;
            _audioStarted = false;

            SetState(RecordingState.Initializing);

            try
            {
                if (string.IsNullOrWhiteSpace(options.OutputDirectory))
                {
                    throw new ArgumentException("A recording output directory is required.", nameof(options));
                }

                var filename = $"KING_REC_{DateTime.Now:yyyyMMdd_HHmmss}.mp4";
                _currentOutputPath = Path.Combine(options.OutputDirectory, filename);

                _logger.LogInformation(
                    "Initializing recording session. Mode={Mode}, Resolution={Resolution}, Fps={Fps}, Compositor={Compositor}, Encoder={Encoder}, Output={Output}",
                    options.Mode,
                    options.Resolution,
                    (int)options.FrameRate,
                    _compositor.GetType().Name,
                    _encoder.GetType().Name,
                    _currentOutputPath);

                // Throws NotSupportedException with the UnavailableEncoderService registered in Phase 0.2.
                await _encoder.InitializeAsync(_currentOutputPath, 1920, 1080, (int)options.FrameRate, 10000000, ct).ConfigureAwait(false);
                _encoderStarted = true;

                await _screenCapture.StartCaptureAsync(options, ct).ConfigureAwait(false);
                _captureStarted = true;

                await _audioCapture.StartAsync(
                    options.CaptureMicrophone,
                    options.MicrophoneDeviceId,
                    options.MicrophoneVolume,
                    options.CaptureSystemAudio,
                    options.SystemAudioVolume,
                    ct).ConfigureAwait(false);
                _audioStarted = true;

                ct.ThrowIfCancellationRequested();

                _stopwatch.Restart();
                SetState(RecordingState.Recording);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Recording session initialization was cancelled; releasing the partially initialized session.");

                var partialOutput = _currentOutputPath;
                await AbortPipelineAsync().ConfigureAwait(false);
                ResetSessionState();
                DeleteIncompleteOutput(partialOutput);

                // Idle is the normal outcome; fall back to the error path if a subscriber already moved the engine on.
                if (RecordingStateTransitions.CanTransition(State, RecordingState.Idle))
                {
                    SetState(RecordingState.Idle);
                }
                else
                {
                    MoveToErrorStateOrReset("Recording session initialization was cancelled after the session had started.");
                }

                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to start recording session. Mode={Mode}, Output={Output}",
                    options.Mode,
                    _currentOutputPath ?? options.OutputDirectory);

                // An encoder may have opened the output file before failing; a partial file must not be
                // left behind looking like a usable recording.
                var partialOutput = _currentOutputPath;
                await AbortPipelineAsync().ConfigureAwait(false);
                ResetSessionState();
                DeleteIncompleteOutput(partialOutput);
                MoveToErrorStateOrReset(ex.Message);

                // Preserve the original exception type, message, stack trace and inner exception: the
                // cleanup above and the subscriber notifications are not allowed to replace it.
                throw;
            }
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    /// <inheritdoc />
    /// <exception cref="NotSupportedException">
    /// Pause is not available until a real capture/encode pipeline exists (Phase 1+). The engine refuses
    /// to enter <see cref="RecordingState.Paused"/> and the recording keeps running.
    /// </exception>
    public Task PauseRecordingAsync(CancellationToken ct = default)
    {
        ThrowIfShuttingDown();

        var currentState = State;
        if (currentState != RecordingState.Recording)
        {
            throw new InvalidOperationException($"Cannot pause when in state {currentState}. Only an active recording can be paused.");
        }

        _logger.LogWarning("Pause requested while recording, but no capture or encoding pipeline exists to suspend.");

        throw new NotSupportedException(
            "Pausing a recording is not supported: no capture or encoding pipeline is implemented yet, so there is nothing to suspend. " +
            "Stop the recording and start a new one instead.");
    }

    /// <inheritdoc />
    public Task ResumeRecordingAsync(CancellationToken ct = default)
    {
        ThrowIfShuttingDown();

        var currentState = State;
        if (currentState != RecordingState.Paused)
        {
            throw new InvalidOperationException($"Cannot resume when in state {currentState}. Only a paused recording can be resumed.");
        }

        throw new NotSupportedException(
            "Resuming a recording is not supported: no capture or encoding pipeline is implemented yet, so a recording can never enter the paused state.");
    }

    public async Task<RecordingItem> StopRecordingAsync(CancellationToken ct = default)
    {
        ThrowIfShuttingDown();

        await _lifecycleGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            ThrowIfShuttingDown();

            var currentState = State;
            if (currentState is not (RecordingState.Recording or RecordingState.Paused))
            {
                throw new InvalidOperationException(
                    $"Cannot stop recording when in state {currentState}. Only an active recording can be stopped.");
            }

            SetState(RecordingState.Stopping);
            _stopwatch.Stop();

            var duration = _stopwatch.Elapsed;
            var outputPath = _currentOutputPath;
            var options = _currentOptions;

            try
            {
                SetState(RecordingState.Finalizing);

                if (_captureStarted)
                {
                    await _screenCapture.StopCaptureAsync(ct).ConfigureAwait(false);
                    _captureStarted = false;
                }

                if (_audioStarted)
                {
                    await _audioCapture.StopAsync(ct).ConfigureAwait(false);
                    _audioStarted = false;
                }

                if (_encoderStarted)
                {
                    await _encoder.FinalizeAsync(ct).ConfigureAwait(false);
                    _encoderStarted = false;
                }

                if (outputPath is null || !File.Exists(outputPath))
                {
                    throw new VideoEncoderException(
                        "Recording output file was not generated because the encoding pipeline is not implemented in Phase 0.2.");
                }

                var fileInfo = new FileInfo(outputPath);
                var result = new RecordingItem(
                    Id: Guid.NewGuid(),
                    Title: Path.GetFileNameWithoutExtension(fileInfo.Name),
                    Filename: fileInfo.Name,
                    FilePath: fileInfo.FullName,
                    Duration: duration,
                    FileSizeBytes: fileInfo.Length,
                    CreatedAtUtc: DateTime.UtcNow,
                    Resolution: options?.Resolution.ToString() ?? "1080p",
                    Fps: (int)(options?.FrameRate ?? VideoFrameRate.Fps60),
                    IsScreenshot: false);

                // A throwing subscriber cannot invalidate a recording that was finalized successfully.
                RaiseDurationUpdatedSafely(duration);
                SetState(RecordingState.Completed);
                ResetSessionState();
                SetState(RecordingState.Idle);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to finalize recording session. Output={Output}, Duration={Duration}",
                    outputPath ?? "(none)",
                    duration);

                await AbortPipelineAsync().ConfigureAwait(false);
                ResetSessionState();
                MoveToErrorStateOrReset(ex.Message);
                throw;
            }
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    public async Task CancelRecordingAsync(CancellationToken ct = default)
    {
        ThrowIfShuttingDown();

        await _lifecycleGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            ThrowIfShuttingDown();

            var currentState = State;
            if (currentState == RecordingState.Idle)
            {
                _logger.LogDebug("Cancel requested while idle; there is no session to cancel.");
                return;
            }

            if (currentState is RecordingState.Stopping or RecordingState.Finalizing)
            {
                throw new InvalidOperationException($"Cannot cancel a recording while it is in state {currentState}.");
            }

            _logger.LogInformation("Cancelling recording session from state {State}.", currentState);

            // Cancelling an active session aborts the pipeline without finalizing the output file.
            if (currentState is RecordingState.Recording or RecordingState.Paused)
            {
                SetState(RecordingState.Stopping);
            }

            var outputPath = _currentOutputPath;
            await AbortPipelineAsync().ConfigureAwait(false);
            ResetSessionState();
            DeleteIncompleteOutput(outputPath);

            SetState(RecordingState.Idle);
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    public Task<string> CaptureScreenshotAsync(RecordingOptions options, CancellationToken ct = default)
    {
        ThrowIfShuttingDown();
        throw new NotSupportedException("Screenshot capture is scheduled for Phase 5 implementation and will not generate fake outputs.");
    }

    /// <summary>
    /// Stops whatever part of the pipeline was actually started. Never throws; failures are logged so
    /// that the original error is not masked and the engine can still return to a usable state.
    /// </summary>
    private async Task AbortPipelineAsync()
    {
        if (_captureStarted)
        {
            try
            {
                await _screenCapture.StopCaptureAsync(CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Screen capture stop failed during session cleanup.");
            }

            _captureStarted = false;
        }

        if (_audioStarted)
        {
            try
            {
                await _audioCapture.StopAsync(CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Audio stop failed during session cleanup.");
            }

            _audioStarted = false;
        }

        if (_encoderStarted)
        {
            try
            {
                await _encoder.FinalizeAsync(CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Encoder finalization failed during session cleanup.");
            }

            _encoderStarted = false;
        }
    }

    /// <summary>
    /// Ends the current session (if any) and removes its incomplete output. Must be called while holding
    /// <see cref="_lifecycleGate"/>.
    /// </summary>
    private async Task CleanupActiveSessionAsync()
    {
        var outputPath = _currentOutputPath;

        await AbortPipelineAsync().ConfigureAwait(false);
        ResetSessionState();
        DeleteIncompleteOutput(outputPath);

        var currentState = State;
        if (currentState == RecordingState.Idle)
        {
            return;
        }

        if (RecordingStateTransitions.CanTransition(currentState, RecordingState.Idle))
        {
            SetState(RecordingState.Idle);
        }
        else
        {
            MoveToErrorStateOrReset("Recording session was aborted because the recording engine shut down.");
        }
    }

    /// <summary>Deletes a partially written output file, never throwing and never masking a prior failure.</summary>
    private void DeleteIncompleteOutput(string? outputPath)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            return;
        }

        try
        {
            if (File.Exists(outputPath))
            {
                File.Delete(outputPath);
                _logger.LogInformation("Deleted incomplete recording output {Output}.", outputPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete incomplete recording output {Output}.", outputPath);
        }
    }

    /// <summary>
    /// Moves the engine to <see cref="RecordingState.Error"/> when the state machine allows it, otherwise falls back
    /// to <see cref="RecordingState.Idle"/>. This keeps a failure (including one raised by a throwing event handler)
    /// from masking the original exception or leaving the engine in an unrecoverable state.
    /// </summary>
    private void MoveToErrorStateOrReset(string message)
    {
        var currentState = State;

        if (RecordingStateTransitions.CanTransition(currentState, RecordingState.Error))
        {
            SetState(RecordingState.Error, message);
            return;
        }

        if (RecordingStateTransitions.CanTransition(currentState, RecordingState.Idle))
        {
            SetState(RecordingState.Idle);
            return;
        }

        _logger.LogError("RecordingEngine cannot leave state {State} after a failure; manual reset required.", currentState);
    }

    private void ResetSessionState()
    {
        _stopwatch.Reset();
        _currentOptions = null;
        _currentOutputPath = null;
        _encoderStarted = false;
        _captureStarted = false;
        _audioStarted = false;
    }

    private void ThrowIfShuttingDown()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _lifecycle) != LifecycleRunning, this);
    }

    /// <summary>
    /// The coordinated asynchronous shutdown path used by the application. It is deliberately not
    /// cancellable: once shutdown starts, the pipeline resources must be released. No new lifecycle
    /// operation is accepted afterwards, an in-flight operation is awaited, and the active session is
    /// aborted before the DI container disposes the injected services.
    /// </summary>
    public async Task ShutdownAsync()
    {
        if (Interlocked.CompareExchange(ref _lifecycle, LifecycleShuttingDown, LifecycleRunning) == LifecycleShutdownCompleted)
        {
            return;
        }

        await _shutdownGate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (Volatile.Read(ref _lifecycle) == LifecycleShutdownCompleted)
            {
                return;
            }

            await _lifecycleGate.WaitAsync().ConfigureAwait(false);
            try
            {
                try
                {
                    await CleanupActiveSessionAsync().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    // Cleanup failures are reported, never rethrown: the process is exiting and the
                    // container will still dispose the remaining services.
                    _logger.LogError(ex, "Recording engine cleanup failed during shutdown.");
                }
            }
            finally
            {
                _lifecycleGate.Release();
            }

            ClearEventSubscribers();
            Volatile.Write(ref _lifecycle, LifecycleShutdownCompleted);
            _logger.LogInformation("RecordingEngine shutdown completed.");
        }
        finally
        {
            _shutdownGate.Release();
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await ShutdownAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Bounded, best-effort synchronous shutdown. Prefer <see cref="ShutdownAsync"/> or
    /// <see cref="DisposeAsync"/>: this overload cannot wait indefinitely for an in-flight lifecycle
    /// operation, so it logs and defers cleanup when the gate is still held after the timeout.
    /// </summary>
    public void Dispose()
    {
        if (Interlocked.CompareExchange(ref _lifecycle, LifecycleShuttingDown, LifecycleRunning) != LifecycleRunning)
        {
            // Shutdown already started or completed; a concurrent ShutdownAsync owns the cleanup.
            return;
        }

        try
        {
            if (!_shutdownGate.Wait(BoundedDisposeWaitMilliseconds))
            {
                _logger.LogWarning(
                    "RecordingEngine.Dispose could not acquire the shutdown gate within {TimeoutMs} ms; cleanup was left to the in-flight shutdown.",
                    BoundedDisposeWaitMilliseconds);
                return;
            }

            try
            {
                if (Volatile.Read(ref _lifecycle) == LifecycleShutdownCompleted)
                {
                    return;
                }

                if (!_lifecycleGate.Wait(BoundedDisposeWaitMilliseconds))
                {
                    _logger.LogWarning(
                        "RecordingEngine.Dispose timed out after {TimeoutMs} ms waiting for an in-flight lifecycle operation; pipeline cleanup could not be completed synchronously.",
                        BoundedDisposeWaitMilliseconds);
                }
                else
                {
                    try
                    {
                        CleanupActiveSessionAsync().GetAwaiter().GetResult();
                    }
                    finally
                    {
                        _lifecycleGate.Release();
                    }
                }
            }
            finally
            {
                ClearEventSubscribers();
                Volatile.Write(ref _lifecycle, LifecycleShutdownCompleted);
                _shutdownGate.Release();
            }

            _logger.LogInformation("RecordingEngine disposed.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RecordingEngine disposal failed; the DI container still owns the injected services.");
        }

        // The injected pipeline services are owned by the DI composition root and are deliberately
        // not disposed here: their lifetime belongs to the container, not to this engine.
    }

    private void ClearEventSubscribers()
    {
        StateChanged = null;
        DurationUpdated = null;
    }
}
