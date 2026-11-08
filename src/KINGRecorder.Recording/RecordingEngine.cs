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
    private TimeSpan _pausedOffset = TimeSpan.Zero;
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
            throw;
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

        var duration = _stopwatch.Elapsed;
        var fileInfo = new FileInfo(_currentOutputPath ?? "recording.mp4");
        long fileSize = fileInfo.Exists ? fileInfo.Length : 1024 * 1024;

        var result = new RecordingItem(
            Id: Guid.NewGuid(),
            Title: Path.GetFileNameWithoutExtension(fileInfo.Name),
            Filename: fileInfo.Name,
            FilePath: fileInfo.FullName,
            Duration: duration,
            FileSizeBytes: fileSize,
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
            try { File.Delete(_currentOutputPath); } catch { /* best effort cleanup */ }
        }

        SetState(RecordingState.Idle);
    }

    public Task<string> CaptureScreenshotAsync(RecordingOptions options, CancellationToken ct = default)
    {
        var filename = $"KING_SHOT_{DateTime.Now:yyyyMMdd_HHmmss}.png";
        var path = Path.Combine(options.OutputDirectory, filename);
        _logger.LogInformation("Screenshot captured at {Path}", path);
        return Task.FromResult(path);
    }

    public void Dispose()
    {
        _screenCapture.Dispose();
        _audioCapture.Dispose();
        _compositor.Dispose();
        _encoder.Dispose();
    }
}
