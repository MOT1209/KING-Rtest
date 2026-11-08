using System.Drawing;
using KINGRecorder.Core.Enums;

namespace KINGRecorder.Core.Models;

public record CaptureRegion(int X, int Y, int Width, int Height, double DpiScale);

public record DisplayInfo(
    string DeviceId,
    string DisplayName,
    int Width,
    int Height,
    int RefreshRateHz,
    double DpiScale,
    bool IsPrimary,
    Rectangle Bounds
);

public record WindowInfo(
    IntPtr Handle,
    string Title,
    string ProcessName,
    Rectangle Bounds,
    bool IsMinimized
);

public record AudioDeviceInfo(
    string Id,
    string Name,
    bool IsDefault,
    bool IsInput
);

public record WebcamOverlaySettings(
    bool IsEnabled,
    WebcamShape Shape,
    CornerPlacement Corner,
    Point? CustomPosition,
    int SizePercent,
    int BorderWidth,
    string BorderColor
);

public record RecordingOptions(
    CaptureMode Mode,
    DisplayInfo? TargetDisplay,
    WindowInfo? TargetWindow,
    CaptureRegion? CustomRegion,
    WebcamOverlaySettings WebcamSettings,
    bool CaptureMicrophone,
    string? MicrophoneDeviceId,
    float MicrophoneVolume,
    bool CaptureSystemAudio,
    float SystemAudioVolume,
    VideoResolutionPreset Resolution,
    VideoFrameRate FrameRate,
    VideoQuality Quality,
    HardwareAccelerationMode Acceleration,
    string OutputDirectory
);

public record RecordingItem(
    Guid Id,
    string Title,
    string Filename,
    string FilePath,
    TimeSpan Duration,
    long FileSizeBytes,
    DateTime CreatedAtUtc,
    string Resolution,
    int Fps,
    bool IsScreenshot
);
