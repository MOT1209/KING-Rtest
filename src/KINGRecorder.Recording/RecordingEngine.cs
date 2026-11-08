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

namespace KINGRecorder.Recording;

public sealed class RecordingEngine : IRecordingEngine
{
    private readonly ILogger<RecordingEngine> _logger;
    private readonly IScreenCaptureService _screenCapture;
    private readonly IAudioCaptureService _audioCapture;
    private readonly IVideoCompositor _compositor;
    private readonly IEncoderService _encoder;

    private readonly Stopwatch _stopwatch = new();
    private RecordingOptions? _currentOptions;
    private string? _currentOutputPath;

    public RecordingState State { get; private set; } = RecordingState.Idle;
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
        _logger = logger;
        _screenCapture = screenCapture;
        _audioCapture = audioCapture;
        _compositor = compositor;
        _encoder = encoder;
    }

    private void SetState(RecordingState newState, string? error = null)
    {
        var oldState = State;
        State = newState;
        _logger.LogInformation("RecordingEngine state transition: {Old} -> {New}", oldState, newState);
        StateChanged?.Invoke(this, new RecordingStateChangedEventArgs(oldState, newState, error));
    }

    public async Task StartRecordingAsync(RecordingOptions options, CancellationToken ct = default)
    {
        if (State != RecordingState.Idle)
            throw new InvalidOperationException($"Cannot start recording from state {State}.");

        SetState(RecordingState.Initializing);
        _currentOptions = options;

        try
        {
            var filename = $"KING_REC_{DateTime.Now:yyyyMMdd_HHmmss}.mp4";
            _currentOutputPath = Path.Combine(options.OutputDirectory, filename);

            // Verify encoder readiness (will throw NotSupportedException if unavailable in Phase 0.1)
            await _encoder.InitializeAsync(_currentOutputPath, 1920, 1080, (int)options.FrameRate, 10000000, ct);

            await _screenCapture.StartCaptureAsync(options, ct);
            await _audioCapture.StartAsync(
                options.CaptureMicrophone,
                options.MicrophoneDeviceId,
                options.MicrophoneVolume,
                options.CaptureSystemAudio,
                options.SystemAudioVolume,
                ct);

            _stopwatch.Restart();
            SetState(RecordingState.Recording);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start recording session.");
            SetState(RecordingState.Error, ex.Message);
            throw new VideoEncoderException("Recording engine initialization failed because the encoder pipeline is not yet implemented in Phase 0.1.", ex);
        }
    }

    public Task PauseRecordingAsync(CancellationToken ct = default)
    {
        if (State != RecordingState.Recording)
            throw new InvalidOperationException($"Cannot pause when in state {State}.");

        _stopwatch.Stop();
        SetState(RecordingState.Paused);
        return Task.CompletedTask;
    }

    public Task ResumeRecordingAsync(CancellationToken ct = default)
    {
        if (State != RecordingState.Paused)
            throw new InvalidOperationException($"Cannot resume when in state {State}.");

        _stopwatch.Start();
        SetState(RecordingState.Recording);
        return Task.CompletedTask;
    }

    public async Task<RecordingItem> StopRecordingAsync(CancellationToken ct = default)
    {
        if (State != RecordingState.Recording && State != RecordingState.Paused)
            throw new InvalidOperationException($"Cannot stop recording when in state {State}.");

        SetState(RecordingState.Stopping);
        _stopwatch.Stop();

        SetState(RecordingState.Finalizing);

        await _screenCapture.StopCaptureAsync(ct);
        await _audioCapture.StopAsync(ct);
        await _encoder.FinalizeAsync(ct);

        if (_currentOutputPath == null || !File.Exists(_currentOutputPath))
        {
            throw new VideoEncoderException("Recording output file was not generated because encoding is not implemented in Phase 0.1.");
        }

        var fileInfo = new FileInfo(_currentOutputPath);
        var result = new RecordingItem(
            Id: Guid.NewGuid(),
            Title: Path.GetFileNameWithoutExtension(fileInfo.Name),
            Filename: fileInfo.Name,
            FilePath: fileInfo.FullName,
            Duration: _stopwatch.Elapsed,
            FileSizeBytes: fileInfo.Length,
            CreatedAtUtc: DateTime.UtcNow,
            Resolution: _currentOptions?.Resolution.ToString() ?? "1080p",
            Fps: (int)(_currentOptions?.FrameRate ?? VideoFrameRate.Fps60),
            IsScreenshot: false
        );

        SetState(RecordingState.Completed);
        SetState(RecordingState.Idle);

        return result;
    }

    public async Task CancelRecordingAsync(CancellationToken ct = default)
    {
        _stopwatch.Reset();
        await _screenCapture.StopCaptureAsync(ct);
        await _audioCapture.StopAsync(ct);

        if (_currentOutputPath != null && File.Exists(_currentOutputPath))
        {
            try { File.Delete(_currentOutputPath); } catch { }
        }

        SetState(RecordingState.Idle);
    }

    public Task<string> CaptureScreenshotAsync(RecordingOptions options, CancellationToken ct = default)
    {
        throw new NotSupportedException("Screenshot capture is scheduled for Phase 5 implementation and will not generate fake outputs.");
    }

    public void Dispose()
    {
        _screenCapture.Dispose();
        _audioCapture.Dispose();
        _compositor.Dispose();
        _encoder.Dispose();
    }
}
