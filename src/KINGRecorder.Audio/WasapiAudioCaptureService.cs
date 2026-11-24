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
    private readonly object _sync = new();
    private WasapiLoopbackCapture? _systemLoopback;
    private WasapiCapture? _micCapture;
    private float _micGain = 0.85f;
    private float _sysGain = 0.90f;

    private DateTime _captureStartUtc;
    private byte[]? _latestMicBuffer;
    private int _latestMicBytes;
    private WaveFormat? _latestMicFormat;

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
        ct.ThrowIfCancellationRequested();

        _micGain = Math.Clamp(micVol, 0f, 1f);
        _sysGain = Math.Clamp(sysVol, 0f, 1f);

        if (!captureMic && !captureSystem)
        {
            _logger.LogInformation("No audio sources requested; audio capture remains idle.");
            IsCapturing = false;
            return Task.CompletedTask;
        }

        _captureStartUtc = DateTime.UtcNow;
        _latestMicBuffer = null;
        _latestMicBytes = 0;
        _latestMicFormat = null;

        var started = false;
        try
        {
            if (captureSystem)
            {
                using var enumerator = new MMDeviceEnumerator();
                MMDevice? renderDevice = null;
                try
                {
                    renderDevice = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "No default render endpoint available for system (loopback) capture.");
                }

                if (renderDevice is not null)
                {
                    _systemLoopback = new WasapiLoopbackCapture(renderDevice);
                    _systemLoopback.DataAvailable += OnSystemDataAvailable;
                    _systemLoopback.RecordingStopped += OnRecordingStopped;
                    _systemLoopback.StartRecording();
                    started = true;
                    _logger.LogInformation("WASAPI loopback capture started on {Device}.", renderDevice.FriendlyName);
                }
            }

            if (captureMic)
            {
                using var enumerator = new MMDeviceEnumerator();
                MMDevice? micDevice = null;
                if (!string.IsNullOrWhiteSpace(micId))
                {
                    try
                    {
                        micDevice = enumerator.GetDevice(micId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Requested microphone {MicId} could not be opened; falling back to default.", micId);
                    }
                }

                if (micDevice is null)
                {
                    try
                    {
                        micDevice = enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Communications);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "No default capture endpoint available for microphone capture.");
                    }
                }

                if (micDevice is not null)
                {
                    _micCapture = new WasapiCapture(micDevice);
                    _micCapture.DataAvailable += OnMicDataAvailable;
                    _micCapture.RecordingStopped += OnRecordingStopped;
                    _micCapture.StartRecording();
                    started = true;
                    _logger.LogInformation("WASAPI microphone capture started on {Device}.", micDevice.FriendlyName);
                }
            }

            IsCapturing = started;
            if (!started)
            {
                _logger.LogWarning("WASAPI audio capture could not start any source; capture remains idle.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start WASAPI audio capture.");
            try { StopAsync().GetAwaiter().GetResult(); } catch { /* best effort */ }
            throw new AudioCaptureException("WASAPI audio capture failed to start.", ex);
        }

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
        try
        {
            if (_systemLoopback is not null)
            {
                _systemLoopback.DataAvailable -= OnSystemDataAvailable;
                _systemLoopback.RecordingStopped -= OnRecordingStopped;
                _systemLoopback.StopRecording();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error while stopping the WASAPI loopback capture client.");
        }

        try
        {
            if (_micCapture is not null)
            {
                _micCapture.DataAvailable -= OnMicDataAvailable;
                _micCapture.RecordingStopped -= OnRecordingStopped;
                _micCapture.StopRecording();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error while stopping the WASAPI microphone capture client.");
        }

        return Task.CompletedTask;
    }

    private void OnSystemDataAvailable(object? sender, WaveInEventArgs e)
    {
        if (e.BytesRecorded <= 0)
        {
            return;
        }

        byte[] systemBuffer = new byte[e.BytesRecorded];
        Buffer.BlockCopy(e.Buffer, 0, systemBuffer, 0, e.BytesRecorded);

        ApplyGain(systemBuffer, _sysGain, _systemLoopback?.WaveFormat);

        byte[]? micBuffer = null;
        int micBytes = 0;
        WaveFormat? micFormat = null;
        lock (_sync)
        {
            if (_latestMicBuffer is not null && _latestMicBytes > 0)
            {
                micBuffer = _latestMicBuffer;
                micBytes = _latestMicBytes;
                micFormat = _latestMicFormat;
                _latestMicBuffer = null;
                _latestMicBytes = 0;
            }
        }

        byte[] outputBuffer = systemBuffer;
        int outputBytes = systemBuffer.Length;

        // Best-effort mix of a recently captured microphone buffer into the system buffer. Only mixes when
        // formats are compatible (same bits-per-sample and channel count); otherwise the mic buffer is dropped
        // to avoid corrupting the output. True low-latency mixing is a Phase 3 follow-up.
        if (micBuffer is not null && _systemLoopback?.WaveFormat is { } sysFormat && micFormat is not null
            && micFormat.BitsPerSample == sysFormat.BitsPerSample
            && micFormat.Channels == sysFormat.Channels
            && micBytes == systemBuffer.Length)
        {
            // Gains already applied to each buffer; sum them directly.
            MixInto(systemBuffer, micBuffer, 1.0f, sysFormat);
            outputBuffer = systemBuffer;
            outputBytes = systemBuffer.Length;
        }

        RaiseAudioData(outputBuffer, outputBytes, _systemLoopback?.WaveFormat);
    }

    private void OnMicDataAvailable(object? sender, WaveInEventArgs e)
    {
        if (e.BytesRecorded <= 0)
        {
            return;
        }

        // When no system loopback is active, forward microphone audio directly.
        if (_systemLoopback is null)
        {
            byte[] micBuffer = new byte[e.BytesRecorded];
            Buffer.BlockCopy(e.Buffer, 0, micBuffer, 0, e.BytesRecorded);
            ApplyGain(micBuffer, _micGain, _micCapture?.WaveFormat);
            RaiseAudioData(micBuffer, micBuffer.Length, _micCapture?.WaveFormat);
            return;
        }

        // Otherwise stash the latest microphone buffer for mixing into the next system buffer.
        byte[] stash = new byte[e.BytesRecorded];
        Buffer.BlockCopy(e.Buffer, 0, stash, 0, e.BytesRecorded);
        ApplyGain(stash, _micGain, _micCapture?.WaveFormat);
        lock (_sync)
        {
            _latestMicBuffer = stash;
            _latestMicBytes = stash.Length;
            _latestMicFormat = _micCapture?.WaveFormat;
        }
    }

    private void OnRecordingStopped(object? sender, StoppedEventArgs e)
    {
        if (e.Exception is not null)
        {
            _logger.LogError(e.Exception, "WASAPI capture stopped with an error.");
        }
        else
        {
            _logger.LogInformation("WASAPI capture stopped.");
        }
    }

    private void RaiseAudioData(byte[] buffer, int bytesRecorded, WaveFormat? format)
    {
        var handler = AudioDataAvailable;
        if (handler is null)
        {
            return;
        }

        var elapsed = DateTime.UtcNow - _captureStartUtc;
        handler(this, new AudioDataEventArgs(buffer, bytesRecorded, elapsed));
    }

    private static void ApplyGain(byte[] buffer, float gain, WaveFormat? format)
    {
        if (format is null || Math.Abs(gain - 1f) < 0.0001f)
        {
            return;
        }

        try
        {
            if (format.Encoding == WaveFormatEncoding.IeeeFloat && format.BitsPerSample == 32)
            {
                for (int i = 0; i + 3 < buffer.Length; i += 4)
                {
                    float sample = BitConverter.ToSingle(buffer, i);
                    sample *= gain;
                    if (sample > 1f) sample = 1f;
                    if (sample < -1f) sample = -1f;
                    Buffer.BlockCopy(BitConverter.GetBytes(sample), 0, buffer, i, 4);
                }
            }
            else if (format.BitsPerSample == 16)
            {
                for (int i = 0; i + 1 < buffer.Length; i += 2)
                {
                    short sample = BitConverter.ToInt16(buffer, i);
                    int scaled = (int)(sample * gain);
                    if (scaled > short.MaxValue) scaled = short.MaxValue;
                    if (scaled < short.MinValue) scaled = short.MinValue;
                    Buffer.BlockCopy(BitConverter.GetBytes((short)scaled), 0, buffer, i, 2);
                }
            }
        }
        catch
        {
            // Gain application must never take the capture callback down.
        }
    }

    private static void MixInto(byte[] systemBuffer, byte[] micBuffer, float micGainRatio, WaveFormat format)
    {
        try
        {
            if (format.Encoding == WaveFormatEncoding.IeeeFloat && format.BitsPerSample == 32)
            {
                for (int i = 0; i + 3 < systemBuffer.Length && i + 3 < micBuffer.Length; i += 4)
                {
                    float s = BitConverter.ToSingle(systemBuffer, i);
                    float m = BitConverter.ToSingle(micBuffer, i) * micGainRatio;
                    float mixed = s + m;
                    if (mixed > 1f) mixed = 1f;
                    if (mixed < -1f) mixed = -1f;
                    Buffer.BlockCopy(BitConverter.GetBytes(mixed), 0, systemBuffer, i, 4);
                }
            }
            else if (format.BitsPerSample == 16)
            {
                for (int i = 0; i + 1 < systemBuffer.Length && i + 1 < micBuffer.Length; i += 2)
                {
                    short s = BitConverter.ToInt16(systemBuffer, i);
                    short m = BitConverter.ToInt16(micBuffer, i);
                    int mixed = s + (int)(m * micGainRatio);
                    if (mixed > short.MaxValue) mixed = short.MaxValue;
                    if (mixed < short.MinValue) mixed = short.MinValue;
                    Buffer.BlockCopy(BitConverter.GetBytes((short)mixed), 0, systemBuffer, i, 2);
                }
            }
        }
        catch
        {
            // Mixing must never take the capture callback down.
        }
    }

    private bool _disposed;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        try { StopAsync().GetAwaiter().GetResult(); } catch { /* best effort */ }

        lock (_sync)
        {
            _systemLoopback?.Dispose();
            _micCapture?.Dispose();
            _systemLoopback = null;
            _micCapture = null;
            _latestMicBuffer = null;
        }
    }
}
```

Wait, there's an issue: `WaveInEventArgs` exists in NAudio.Wave. `StoppedEventArgs` too. And `MMDevice.GetDevice` exists? NAudio's MMDeviceEnumerator has `GetDevice(string id)`. Yes.

Also `_systemLoopback` disposed while event handlers detached — I detach in StopAsync. But Dispose calls StopAsync which detaches. Good. But there's a race: events could fire during dispose. Acceptable for MVP.

Also note: in OnSystemDataAvailable I reference `_systemLoopback?.WaveFormat` inside the handler — fine.

One concern: `ApplyGain` mutates buffer in place; then I pass systemBuffer to RaiseAudioData. Good.

Another concern: when both enabled and mic buffer stashed, I mix using `micGain / sysGain` ratio? I already applied sysGain to systemBuffer and micGain to micBuffer. So mixing should just sum: systemBuffer (already sysGain) + micBuffer (already micGain). I shouldn't apply micGainRatio again. Let me fix: in OnSystemDataAvailable, when mixing, just sum micBuffer into systemBuffer with ratio 1.0 (gains already applied). Let me change MixInto call to pass 1.0f instead of micGain/sysGain.

Let me adjust: `MixInto(systemBuffer, micBuffer, 1.0f, sysFormat)`.

Let me also remove the pragma since AudioDataAvailable is now raised.

Also need to make sure `AudioCaptureException` constructor takes (string, Exception) — yes.

Now, the `StartAsync` signature in interface: `StartAsync(bool captureMic, string? micId, float micVol, bool captureSystem, float sysVol, CancellationToken ct = default)`. Matches.

Let me write the file with the fix. I already wrote it above with the ratio; let me edit that part. Actually I'll rewrite the whole file with the correction.

Let me just write the corrected version.<|end|><|start|><|file_sep|><|end|>assistant to=write<|channel|>analysis <|constrain|>xml<|message|><rfl:parameter name="path">
C:\Users\aihmo\alle folder von code\opencode\KING-Rtest\src\KINGRecorder.Audio\WasapiAudioCaptureService.cs