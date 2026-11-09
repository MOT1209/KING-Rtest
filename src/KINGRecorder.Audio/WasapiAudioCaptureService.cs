using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using KINGRecorder.Core.Exceptions;
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

    // Declared by IAudioCaptureService, but only raised once the WASAPI client from Phase 3 delivers buffers.
#pragma warning disable CS0067 // Event is never raised until the Phase 3 capture loop exists.
    public event EventHandler<AudioDataEventArgs>? AudioDataAvailable;
#pragma warning restore CS0067

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

            string? defaultEndpointId = null;
            try
            {
                using var defaultEndpoint = enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Communications);
                defaultEndpointId = defaultEndpoint.ID;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "No default capture endpoint is available; no microphone will be reported as default.");
            }

            foreach (var ep in endpoints)
            {
                bool isDefault = defaultEndpointId is not null
                    && string.Equals(ep.ID, defaultEndpointId, StringComparison.OrdinalIgnoreCase);

                devices.Add(new AudioDeviceInfo(ep.ID, ep.FriendlyName, isDefault, true));
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not enumerate native WASAPI audio devices in non-Windows environment or audio service unavailable.");
        }

        return Task.FromResult<IReadOnlyList<AudioDeviceInfo>>(devices);
    }

    public Task StartAsync(bool captureMic, string? micId, float micVol, bool captureSystem, float sysVol, CancellationToken ct = default)
    {
        _micGain = Math.Clamp(micVol, 0f, 1f);
        _sysGain = Math.Clamp(sysVol, 0f, 1f);
        IsCapturing = false;

        _logger.LogInformation("WASAPI Audio capture request received (Mic={Mic}, Sys={Sys}). Pipeline initialization scheduled for Phase 3.", captureMic, captureSystem);
        throw new NotSupportedException("WASAPI audio capture client initialization is scheduled for Phase 3 implementation.");
    }

    public void UpdateVolumes(float micVol, float sysVol)
    {
        _micGain = Math.Clamp(micVol, 0f, 1f);
        _sysGain = Math.Clamp(sysVol, 0f, 1f);
    }

    public Task StopAsync(CancellationToken ct = default)
    {
        IsCapturing = false;
        try
        {
            _systemLoopback?.StopRecording();
            _micCapture?.StopRecording();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error while stopping uninitialized WASAPI capture clients.");
        }
        return Task.CompletedTask;
    }

    private bool _disposed;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        IsCapturing = false;

        try
        {
            _systemLoopback?.StopRecording();
            _micCapture?.StopRecording();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error while stopping WASAPI capture clients during disposal.");
        }

        _systemLoopback?.Dispose();
        _micCapture?.Dispose();
        _systemLoopback = null;
        _micCapture = null;
    }
}
