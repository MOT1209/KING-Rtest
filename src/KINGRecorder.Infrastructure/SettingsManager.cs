using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using KINGRecorder.Core.Enums;
using KINGRecorder.Core.Models;

namespace KINGRecorder.Infrastructure;

public sealed class SettingsManager
{
    private readonly ILogger<SettingsManager> _logger;
    private readonly string _settingsFilePath;

    public SettingsManager(ILogger<SettingsManager> logger)
    {
        _logger = logger;
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var dir = Path.Combine(appData, "KINGRecorder");
        Directory.CreateDirectory(dir);
        _settingsFilePath = Path.Combine(dir, "settings.json");
    }

    public async Task<RecordingOptions> LoadSettingsAsync(CancellationToken ct = default)
    {
        try
        {
            if (File.Exists(_settingsFilePath))
            {
                var json = await File.ReadAllTextAsync(_settingsFilePath, ct);
                var options = JsonSerializer.Deserialize<RecordingOptions>(json);
                if (options != null) return options;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load settings file, fallback to defaults.");
        }

        var defaultVideos = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "KING Recorder");
        Directory.CreateDirectory(defaultVideos);

        return new RecordingOptions(
            Mode: CaptureMode.FullScreen,
            TargetDisplay: null,
            TargetWindow: null,
            CustomRegion: null,
            WebcamSettings: new WebcamOverlaySettings(
                IsEnabled: true,
                Shape: WebcamShape.RoundedRectangle,
                Corner: CornerPlacement.BottomRight,
                CustomPosition: null,
                SizePercent: 24,
                BorderWidth: 2,
                BorderColor: "#D4AF37"
            ),
            CaptureMicrophone: true,
            MicrophoneDeviceId: null,
            MicrophoneVolume: 0.85f,
            CaptureSystemAudio: true,
            SystemAudioVolume: 0.90f,
            Resolution: VideoResolutionPreset.P1080,
            FrameRate: VideoFrameRate.Fps60,
            Quality: VideoQuality.High,
            Acceleration: HardwareAccelerationMode.Auto,
            OutputDirectory: defaultVideos
        );
    }

    public async Task SaveSettingsAsync(RecordingOptions options, CancellationToken ct = default)
    {
        try
        {
            var json = JsonSerializer.Serialize(options, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_settingsFilePath, json, ct);
            _logger.LogInformation("Settings saved successfully to {Path}", _settingsFilePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save settings file.");
        }
    }
}
