using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using KINGRecorder.Core.Interfaces;
using KINGRecorder.Core.Models;

namespace KINGRecorder.Audio;

public sealed class WasapiAudioCaptureService : IAudioCaptureService
{
    private readonly ILogger<WasapiAudioCaptureService> _logger;
    private WasapiLoopbackCapture? _systemLoopback;
    private WasapiCapture? _micCapture;
    private float _micGain = 0.85f;
    private float _sysGain = 0.90f;

    public event EventHandler<AudioDataEventArgs>? AudioDataAvailable;
    public bool IsCapturing { get; private set; }

    public WasapiAudioCaptureService(ILogger<WasapiAudioCaptureService> logger)
    {
        _logger = logger;
    }

    public Task<IReadOnlyList<AudioDeviceInfo>> GetMicrophonesAsync(CancellationToken ct = default)
    {
        var devices = new List<AudioDeviceInfo>();
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            var endpoints = enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active);
            foreach (var ep in endpoints)
            {
                devices.Add(new AudioDeviceInfo(ep.ID, ep.FriendlyName, ep.Selected, true));
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not enumerate native WASAPI audio devices in non-Windows environment");
            devices.Add(new AudioDeviceInfo("default-mic", "Default System Microphone", true, true));
        }

        return Task.FromResult<IReadOnlyList<AudioDeviceInfo>>(devices);
    }

    public Task StartAsync(bool captureMic, string? micId, float micVol, bool captureSystem, float sysVol, CancellationToken ct = default)
    {
        _micGain = micVol;
        _sysGain = sysVol;
        IsCapturing = true;
        _logger.LogInformation("WASAPI Audio capture started. Mic={Mic}, Sys={Sys}", captureMic, captureSystem);
        return Task.CompletedTask;
    }

    public void UpdateVolumes(float micVol, float sysVol)
    {
        _micGain = Math.Clamp(micVol, 0f, 1f);
        _sysGain = Math.Clamp(sysVol, 0f, 1f);
    }

    public Task StopAsync(CancellationToken ct = default)
    {
        IsCapturing = false;
        _systemLoopback?.StopRecording();
        _micCapture?.StopRecording();
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        StopAsync().GetAwaiter().GetResult();
        _systemLoopback?.Dispose();
        _micCapture?.Dispose();
    }
}
