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

    public SettingsManager(ILogger<SettingsManager> logger, string customSettingsFilePath)
    {
        _logger = logger;
        _settingsFilePath = customSettingsFilePath;
        var dir = Path.GetDirectoryName(customSettingsFilePath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }
    }

    public async Task<RecordingOptions> LoadSettingsAsync(CancellationToken ct = default)
    {
        try
        {
            if (File.Exists(_settingsFilePath))
            {
                var json = await File.ReadAllTextAsync(_settingsFilePath, ct);
                var options = JsonSerializer.Deserialize<RecordingOptions>(json);
                if (options != null)
                {
                    ValidateOptions(options);
                    return options;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load settings file from {Path}, fallback to defaults.", _settingsFilePath);
        }

        return GetDefaultOptions();
    }

    public async Task SaveSettingsAsync(RecordingOptions options, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        ValidateOptions(options);

        var tempPath = _settingsFilePath + ".tmp";
        try
        {
            var json = JsonSerializer.Serialize(options, new JsonSerializerOptions { WriteIndented = true });
            
            var dir = Path.GetDirectoryName(_settingsFilePath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            await File.WriteAllTextAsync(tempPath, json, ct);
            
            if (File.Exists(_settingsFilePath))
            {
                File.Replace(tempPath, _settingsFilePath, null);
            }
            else
            {
                File.Move(tempPath, _settingsFilePath);
            }

            _logger.LogInformation("Settings saved atomically to {Path}", _settingsFilePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save settings file to {Path}.", _settingsFilePath);
            if (File.Exists(tempPath))
            {
                try { File.Delete(tempPath); } catch { }
            }
            throw;
        }
    }

    private static RecordingOptions GetDefaultOptions()
    {
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

    private static void ValidateOptions(RecordingOptions options)
    {
        if (options.MicrophoneVolume is < 0f or > 1f)
            throw new ArgumentOutOfRangeException(nameof(options), "Microphone volume must be between 0.0 and 1.0.");
        if (options.SystemAudioVolume is < 0f or > 1f)
            throw new ArgumentOutOfRangeException(nameof(options), "System audio volume must be between 0.0 and 1.0.");
        if (string.IsNullOrWhiteSpace(options.OutputDirectory))
            throw new ArgumentException("Output directory cannot be empty.", nameof(options));
    }
}
