using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using KINGRecorder.Core.Enums;
using KINGRecorder.Core.Exceptions;
using KINGRecorder.Core.Interfaces;
using KINGRecorder.Core.Models;
using KINGRecorder.Core.Session;
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
/// Shutdown is coordinated through both gates: <see cref="ShutdownAsync"/> signals cancellation through the
/// engine-owned source, waits for an in-flight Start/Stop to finish (bounded by
/// <see cref="ShutdownGateTimeout"/>), aborts whatever part of the pipeline was started, removes the
/// incomplete output file, then refuses all further operations. <see cref="Dispose"/> remains available as a
/// best-effort synchronous path for callers that cannot await, and never disposes container-owned services.
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
    /// <summary>
    /// Upper bound for how long <see cref="ShutdownAsync"/> waits for an in-flight lifecycle operation before
    /// releasing the managed resources it was holding. A native pipeline stage that ignores cancellation is
    /// therefore bounded instead of potentially hanging process shutdown forever.
    /// </summary>
    private static readonly TimeSpan ShutdownGateTimeout = TimeSpan.FromSeconds(10);

    private const int LifecycleRunning = 0;
    private const int LifecycleShuttingDown = 1;
    private const int LifecycleShutdownCompleted = 2;

    /// <summary>Engine-owned cancellation source; shutdown signals it so in-flight recording work aborts.</summary>
    private readonly CancellationTokenSource _shutdownCts = new();

    /// <summary>Marks that shutdown already requested cancellation on the engine-owned source.</summary>
    private int _shutdownSignalled;

    /// <summary>Marks that the engine-owned cancellation source was released during shutdown.</summary>
    private int _shutdownCtsReleased;

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

    /// <summary>Owns the output files created by the current recording session and decides who may delete them.</summary>
    private RecordingSessionContext? _session;

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
            _session = null;
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

                // The output path is resolved from a per-session GUID, so two sessions can never collide.
                var session = RecordingSessionContext.CreateUnique(options.OutputDirectory);
                _session = session;

                _logger.LogInformation(
                    "Initializing recording session. SessionId={SessionId}, Mode={Mode}, Resolution={Resolution}, Fps={Fps}, Compositor={Compositor}, Encoder={Encoder}, Output={Output}",
                    session.SessionId,
                    options.Mode,
                    options.Resolution,
                    (int)options.FrameRate,
                    _compositor.GetType().Name,
                    _encoder.GetType().Name,
                    session.OutputPath);

                // The caller token is linked with the engine-owned shutdown token: either source cancels the
                // in-flight initialization, and the session must abort the partial resources it had opened.
                using var linkedCancellation = CreateLinkedCancellationToken(ct);

                // Throws NotSupportedException with the UnavailableEncoderService registered in Phase 0.2.
                await _encoder.InitializeAsync(session.OutputPath, 1920, 1080, (int)options.FrameRate, 10000000, linkedCancellation.Token).ConfigureAwait(false);
                _encoderStarted = true;
                session.RegisterCreatedFile(session.OutputPath);

                await _screenCapture.StartCaptureAsync(options, linkedCancellation.Token).ConfigureAwait(false);
                _captureStarted = true;
                _screenCapture.FrameArrived += OnCaptureFrameArrived;

                await _audioCapture.StartAsync(
                    options.CaptureMicrophone,
                    options.MicrophoneDeviceId,
                    options.MicrophoneVolume,
                    options.CaptureSystemAudio,
                    options.SystemAudioVolume,
                    linkedCancellation.Token).ConfigureAwait(false);
                _audioStarted = true;
                _audioCapture.AudioDataAvailable += OnAudioDataArrived;

                linkedCancellation.Token.ThrowIfCancellationRequested();

                _stopwatch.Restart();
                SetState(RecordingState.Recording);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Recording session initialization was cancelled; releasing the partially initialized session. SessionId={SessionId}", _session?.SessionId);

                var partialSession = _session;
                await AbortPipelineAsync().ConfigureAwait(false);
                DeleteSessionArtifacts(partialSession);
                ResetSessionState();

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
                    "Failed to start recording session. SessionId={SessionId}, Mode={Mode}, Output={Output}",
                    _session?.SessionId,
                    options.Mode,
                    _session?.OutputPath ?? options.OutputDirectory);

                // An encoder may have opened the output file before failing; a partial file must not be
                // left behind looking like a usable recording, and only files owned by this session may go.
                var partialSession = _session;
                await AbortPipelineAsync().ConfigureAwait(false);
                DeleteSessionArtifacts(partialSession);
                ResetSessionState();
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
            var session = _session;
            var outputPath = session?.OutputPath;
            var options = _currentOptions;

            try
            {
                SetState(RecordingState.Finalizing);

                // The caller token is linked with the engine-owned shutdown token so an engine shutdown can
                // interrupt a stop that is in the middle of finalizing.
                using var linkedCancellation = CreateLinkedCancellationToken(ct);

                if (_captureStarted)
                {
                    _screenCapture.FrameArrived -= OnCaptureFrameArrived;
                    await _screenCapture.StopCaptureAsync(linkedCancellation.Token).ConfigureAwait(false);
                    _captureStarted = false;
                }

                if (_audioStarted)
                {
                    _audioCapture.AudioDataAvailable -= OnAudioDataArrived;
                    await _audioCapture.StopAsync(linkedCancellation.Token).ConfigureAwait(false);
                    _audioStarted = false;
                }

                if (_encoderStarted)
                {
                    await _encoder.FinalizeAsync(linkedCancellation.Token).ConfigureAwait(false);
                    _encoderStarted = false;

                    // Finalization succeeded: the output file now represents a complete recording and must
                    // survive any later cleanup instead of being removed as an incomplete artifact.
                    session?.MarkFinalized();
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
                    "Failed to finalize recording session. SessionId={SessionId}, Output={Output}, Duration={Duration}",
                    session?.SessionId,
                    outputPath ?? "(none)",
                    duration);

                // The finalization of this session failed, so the file it was writing is incomplete: remove
                // it, but only when the session still owns it and never once it was finalized successfully.
                var failedSession = _session;
                await AbortPipelineAsync().ConfigureAwait(false);
                DeleteSessionArtifacts(failedSession);
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

            var outputPath = _session?.OutputPath;
            await AbortPipelineAsync().ConfigureAwait(false);
            DeleteSessionArtifacts(_session);
            ResetSessionState();

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
    private void OnCaptureFrameArrived(object? sender, FrameArrivedEventArgs e)
    {
        if (_encoderStarted)
        {
            _encoder.WriteVideoSample(e.SurfacePointer, e.Timestamp);
        }
    }

    private void OnAudioDataArrived(object? sender, AudioDataEventArgs e)
    {
        if (_encoderStarted)
        {
            _encoder.WriteAudioSample(e.Buffer, e.BytesRecorded, e.Timestamp);
        }
    }

    private async Task AbortPipelineAsync()
    {
        if (_captureStarted)
        {
            _screenCapture.FrameArrived -= OnCaptureFrameArrived;
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
            _audioCapture.AudioDataAvailable -= OnAudioDataArrived;
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
        var session = _session;

        await AbortPipelineAsync().ConfigureAwait(false);
        DeleteSessionArtifacts(session);
        ResetSessionState();

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

    /// <summary>
    /// Removes the incomplete output files owned by <paramref name="session"/>. Only session-owned files are
    /// considered, finalized files are preserved, and the helper never throws so that cleanup cannot mask the
    /// original failure.
    /// </summary>
    private void DeleteSessionArtifacts(RecordingSessionContext? session)
    {
        if (session is null)
        {
            return;
        }

        foreach (var outputPath in session.DiscoverOwnedFiles())
        {
            if (session.TryDeleteOwnedFile(outputPath, out var failure))
            {
                _logger.LogInformation(
                    "Deleted incomplete recording output {Output} owned by session {SessionId}.",
                    outputPath,
                    session.SessionId);
                continue;
            }

            if (failure is not null)
            {
                _logger.LogWarning(
                    failure,
                    "Failed to delete incomplete recording output {Output} owned by session {SessionId}.",
                    outputPath,
                    session.SessionId);
                continue;
            }

            _logger.LogInformation(
                "Preserved recording output {Output} of session {SessionId} because the session was finalized successfully.",
                outputPath,
                session.SessionId);
        }
    }

    /// <summary>
    /// Releases the per-session bookkeeping. Called from every session-end path so a session can never delete
    /// files for a session that no longer exists.
    /// </summary>
    private void ResetSessionState()
    {
        _stopwatch.Reset();
        _currentOptions = null;
        _session?.Dispose();
        _session = null;
        _encoderStarted = false;
        _captureStarted = false;
        _audioStarted = false;
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

    private void ThrowIfShuttingDown()
    {
        var lifecycle = Volatile.Read(ref _lifecycle);
        if (lifecycle == LifecycleRunning)
        {
            return;
        }

        throw new ObjectDisposedException(
            nameof(RecordingEngine),
            lifecycle == LifecycleShuttingDown
                ? "The recording engine is shutting down and no longer accepts recording requests."
                : "The recording engine has been shut down and no longer accepts recording requests.");
    }

    /// <summary>
    /// Creates a cancellation token that is cancelled by either the caller's token or the engine-owned shutdown
    /// source. The engine owns one shutdown source for its lifetime; the linked source is released by the caller
    /// as soon as the lifecycle operation that received it completes.
    /// </summary>
    private CancellationTokenSource CreateLinkedCancellationToken(CancellationToken callerToken)
    {
        if (callerToken.CanBeCanceled)
        {
            return CancellationTokenSource.CreateLinkedTokenSource(callerToken, _shutdownCts.Token);
        }

        return CancellationTokenSource.CreateLinkedTokenSource(_shutdownCts.Token);
    }

    /// <summary>Requests cancellation of in-flight recording work. Idempotent; never blocks.</summary>
    private void SignalShutdown()
    {
        if (Interlocked.Exchange(ref _shutdownSignalled, 1) == 1)
        {
            return;
        }

        try
        {
            _shutdownCts.Cancel();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "The recording engine shutdown cancellation could not be signalled.");
        }
    }

    /// <summary>Releases the engine-owned cancellation source. Safe to call more than once.</summary>
    private void ReleaseShutdownCancellation()
    {
        if (Interlocked.Exchange(ref _shutdownCtsReleased, 1) == 1)
        {
            return;
        }

        try
        {
            _shutdownCts.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "The recording engine shutdown cancellation source could not be disposed.");
        }
    }

    /// <summary>
    /// The coordinated asynchronous shutdown path used by the application. It refuses new operations, waits
    /// for an in-flight Start/Stop to finish (bounded by <see cref="ShutdownGateTimeout"/>), signals cancellation
    /// immediately so any running initialization releases its partial resources, aborts the active session, and
    /// removes its incomplete output. Safe to call repeatedly; never disposes container-owned services.
    /// </summary>
    public async Task ShutdownAsync()
    {
        // Mark shutdown first, then signal: an in-flight operation that observes the token must be cancelled
        // before the waiting caller proceeds to release the session.
        Interlocked.CompareExchange(ref _lifecycle, LifecycleShuttingDown, LifecycleRunning);
        SignalShutdown();

        await _shutdownGate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (Volatile.Read(ref _lifecycle) == LifecycleShutdownCompleted)
            {
                return;
            }

            var gateAcquired = await _lifecycleGate.WaitAsync(ShutdownGateTimeout).ConfigureAwait(false);

            if (gateAcquired)
            {
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
            }
            else
            {
                _logger.LogWarning(
                    "Recording engine shutdown did not acquire the lifecycle gate within {TimeoutSeconds} seconds; " +
                    "the following pipeline resources could not be safely terminated: {UnterminatedResources}.",
                    ShutdownGateTimeout.TotalSeconds,
                    DescribeUnterminatedResources());
            }

            ClearEventSubscribers();
            Volatile.Write(ref _lifecycle, LifecycleShutdownCompleted);

            if (gateAcquired)
            {
                ReleaseShutdownCancellation();
            }
            else
            {
                _logger.LogWarning(
                    "Recording engine shutdown completed without releasing the cancellation source because an " +
                    "in-flight lifecycle operation was still holding the lifecycle gate.");
            }

            _logger.LogInformation("RecordingEngine shutdown completed.");
        }
        finally
        {
            _shutdownGate.Release();
        }
    }

    /// <summary>
    /// Reports which pipeline resources could not be safely terminated because the shutdown deadline was hit.
    /// </summary>
    private string DescribeUnterminatedResources()
    {
        var resources = new List<string>();
        if (_captureStarted)
        {
            resources.Add("screen capture");
        }

        if (_audioStarted)
        {
            resources.Add("audio capture");
        }

        if (_encoderStarted)
        {
            resources.Add("encoder");
        }

        return string.Join(", ", resources);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await ShutdownAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Bounded, best-effort synchronous shutdown. Prefer <see cref="ShutdownAsync"/> or
    /// <see cref="DisposeAsync"/>: this overload cannot await, so it blocks on the coordinated shutdown, which
    /// itself is bounded by <see cref="ShutdownGateTimeout"/>. If the shutdown could not be completed it is
    /// logged and the caller is expected to retry or to rely on the async path.
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
            ShutdownAsync().GetAwaiter().GetResult();
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
