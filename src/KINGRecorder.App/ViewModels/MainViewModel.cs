using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KINGRecorder.Core.Enums;
using KINGRecorder.Core.Interfaces;
using KINGRecorder.Core.Models;
using KINGRecorder.Infrastructure;

namespace KINGRecorder.App.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    private readonly IRecordingEngine _recordingEngine;
    private readonly SettingsManager _settingsManager;

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

    public MainViewModel(IRecordingEngine recordingEngine, SettingsManager settingsManager)
    {
        _recordingEngine = recordingEngine;
        _settingsManager = settingsManager;

        // Default initial options without blocking async calls in constructor
        _currentOptions = new RecordingOptions(
            Mode: CaptureMode.FullScreen,
            TargetDisplay: null,
            TargetWindow: null,
            CustomRegion: null,
            WebcamSettings: new WebcamOverlaySettings(true, WebcamShape.RoundedRectangle, CornerPlacement.BottomRight, null, 24, 2, "#D4AF37"),
            CaptureMicrophone: true,
            MicrophoneDeviceId: null,
            MicrophoneVolume: 0.85f,
            CaptureSystemAudio: true,
            SystemAudioVolume: 0.90f,
            Resolution: VideoResolutionPreset.P1080,
            FrameRate: VideoFrameRate.Fps60,
            Quality: VideoQuality.High,
            Acceleration: HardwareAccelerationMode.Auto,
            OutputDirectory: "C:\\Users\\User\\Videos\\KING Recorder\\"
        );

        _recordingEngine.StateChanged += OnRecordingStateChanged;
    }

    public async Task InitializeAsync()
    {
        try
        {
            CurrentOptions = await _settingsManager.LoadSettingsAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to load settings: {ex.Message}";
        }
    }

    private void OnRecordingStateChanged(object? sender, RecordingStateChangedEventArgs e)
    {
        CurrentState = e.NewState;
        IsRecording = e.NewState == RecordingState.Recording;
        IsPaused = e.NewState == RecordingState.Paused;
        if (e.ErrorMessage != null)
        {
            ErrorMessage = e.ErrorMessage;
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
