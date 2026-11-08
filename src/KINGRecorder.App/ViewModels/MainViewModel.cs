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

    public MainViewModel(IRecordingEngine recordingEngine, SettingsManager settingsManager)
    {
        _recordingEngine = recordingEngine;
        _settingsManager = settingsManager;

        _currentOptions = _settingsManager.LoadSettingsAsync().GetAwaiter().GetResult();
        _recordingEngine.StateChanged += OnRecordingStateChanged;
    }

    private void OnRecordingStateChanged(object? sender, RecordingStateChangedEventArgs e)
    {
        CurrentState = e.NewState;
        IsRecording = e.NewState == RecordingState.Recording;
        IsPaused = e.NewState == RecordingState.Paused;
    }

    [RelayCommand]
    private async Task StartRecordingAsync()
    {
        await _recordingEngine.StartRecordingAsync(CurrentOptions);
    }

    [RelayCommand]
    private async Task PauseRecordingAsync()
    {
        await _recordingEngine.PauseRecordingAsync();
    }

    [RelayCommand]
    private async Task ResumeRecordingAsync()
    {
        await _recordingEngine.ResumeRecordingAsync();
    }

    [RelayCommand]
    private async Task StopRecordingAsync()
    {
        await _recordingEngine.StopRecordingAsync();
    }

    [RelayCommand]
    private async Task CancelRecordingAsync()
    {
        await _recordingEngine.CancelRecordingAsync();
    }

    [RelayCommand]
    private async Task CaptureScreenshotAsync()
    {
        await _recordingEngine.CaptureScreenshotAsync(CurrentOptions);
    }
}
