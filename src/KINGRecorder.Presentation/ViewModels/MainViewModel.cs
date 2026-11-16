using System;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using KINGRecorder.Core.Enums;
using KINGRecorder.Core.Interfaces;
using KINGRecorder.Core.Models;
using KINGRecorder.Infrastructure;
using KINGRecorder.Presentation.Dispatching;

namespace KINGRecorder.Presentation.ViewModels;

/// <summary>
/// Screen state for the recording dashboard.
/// </summary>
/// <remarks>
/// <para>
/// The recording engine raises its events from whatever thread finishes the operation, which is a pool
/// thread whenever the caller awaited. WinUI bindings must only be touched on the UI thread, so every
/// engine notification is marshalled through the injected <see cref="IUiDispatcher"/> instead of being
/// applied inside the event handler.
/// </para>
/// <para>
/// Notifications are always queued (never applied inline) so the UI observes the same order the engine
/// raised them in. A rejected enqueue is logged and dropped; it can never crash the application or the
/// engine. Callbacks that run after <see cref="Dispose"/> are ignored, so a closed window cannot be
/// updated by work that was queued before shutdown.
/// </para>
/// </remarks>
public sealed partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly IRecordingEngine _recordingEngine;
    private readonly SettingsManager _settingsManager;
    private readonly IUiDispatcher _dispatcher;
    private readonly ILogger<MainViewModel> _logger;

    private int _disposed;

    [ObservableProperty]
    private RecordingState _currentState = RecordingState.Idle;

    [ObservableProperty]
    private string _durationText = "00:00:00";

    [ObservableProperty]
    private bool _isRecording;

    [ObservableProperty]
    private bool _isPaused;

    [ObservableProperty]
    private RecordingOptions _currentOptions;

    [ObservableProperty]
    private string? _errorMessage;

    public MainViewModel(
        IRecordingEngine recordingEngine,
        SettingsManager settingsManager,
        IUiDispatcher dispatcher,
        ILogger<MainViewModel> logger)
    {
        _recordingEngine = recordingEngine ?? throw new ArgumentNullException(nameof(recordingEngine));
        _settingsManager = settingsManager ?? throw new ArgumentNullException(nameof(settingsManager));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Default initial options using dynamic MyVideos folder path without blocking constructor
        _currentOptions = SettingsManager.GetDefaultOptions();

        _recordingEngine.StateChanged += OnRecordingStateChanged;
        _recordingEngine.DurationUpdated += OnDurationUpdated;
    }

    /// <summary>True once the ViewModel stopped observing the engine.</summary>
    public bool IsDisposed => Volatile.Read(ref _disposed) != 0;

    /// <summary>
    /// Detaches from the engine exactly once. Callbacks that were already queued are still drained by the
    /// dispatcher but are ignored, so shutdown cannot race a binding update into a closed window.
    /// </summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _recordingEngine.StateChanged -= OnRecordingStateChanged;
        _recordingEngine.DurationUpdated -= OnDurationUpdated;
    }

    public async Task InitializeAsync()
    {
        if (IsDisposed)
        {
            return;
        }

        try
        {
            CurrentOptions = await _settingsManager.LoadSettingsAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to load settings: {ex.Message}";
        }
    }

    /// <summary>Engine notification entry point; runs on the engine's thread, never touches bindings.</summary>
    private void OnDurationUpdated(object? sender, TimeSpan elapsed)
    {
        if (IsDisposed)
        {
            return;
        }

        MarshalToUiThread(() => DurationText = elapsed.ToString(@"hh\:mm\:ss"));
    }

    /// <summary>Engine notification entry point; runs on the engine's thread, never touches bindings.</summary>
    private void OnRecordingStateChanged(object? sender, RecordingStateChangedEventArgs e)
    {
        if (IsDisposed)
        {
            return;
        }

        MarshalToUiThread(() =>
        {
            CurrentState = e.NewState;
            IsRecording = e.NewState == RecordingState.Recording;
            IsPaused = e.NewState == RecordingState.Paused;
            if (e.ErrorMessage is not null)
            {
                ErrorMessage = e.ErrorMessage;
            }
        });
    }

    private void MarshalToUiThread(Action update)
    {
        bool enqueued;
        try
        {
            enqueued = _dispatcher.TryEnqueue(() =>
            {
                // Disposal can happen between enqueueing and execution; the callback must then do nothing.
                if (IsDisposed)
                {
                    return;
                }

                update();
            });
        }
        catch (Exception ex)
        {
            // A dispatcher that is already torn down must not take the engine's thread down with it.
            _logger.LogError(ex, "The UI dispatcher threw while queueing a recording notification; the update was dropped.");
            return;
        }

        if (!enqueued)
        {
            _logger.LogWarning("The UI dispatcher rejected a recording notification; the update was dropped because the UI thread is shutting down.");
        }
    }

    [RelayCommand]
    private async Task StartRecordingAsync()
    {
        ErrorMessage = null;
        try
        {
            await _recordingEngine.StartRecordingAsync(CurrentOptions);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    [RelayCommand]
    private async Task PauseRecordingAsync()
    {
        try
        {
            await _recordingEngine.PauseRecordingAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    [RelayCommand]
    private async Task ResumeRecordingAsync()
    {
        try
        {
            await _recordingEngine.ResumeRecordingAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    [RelayCommand]
    private async Task StopRecordingAsync()
    {
        try
        {
            await _recordingEngine.StopRecordingAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    [RelayCommand]
    private async Task CancelRecordingAsync()
    {
        try
        {
            await _recordingEngine.CancelRecordingAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    [RelayCommand]
    private async Task CaptureScreenshotAsync()
    {
        try
        {
            await _recordingEngine.CaptureScreenshotAsync(CurrentOptions);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }
}
