namespace KINGRecorder.Core.Enums;

public enum RecordingState
{
    Idle,
    Initializing,
    Recording,
    Paused,
    Stopping,
    Finalizing,
    Completed,
    Error
}

public enum CaptureMode
{
    FullScreen,
    Window,
    CustomArea
}

public enum WebcamShape
{
    RoundedRectangle,
    Circle
}

public enum CornerPlacement
{
    TopLeft,
    TopRight,
    BottomLeft,
    BottomRight,
    Custom
}

public enum VideoResolutionPreset
{
    Original,
    P720,
    P1080,
    P4K
}

public enum VideoFrameRate
{
    Fps30 = 30,
    Fps60 = 60
}

public enum VideoQuality
{
    Low,
    Medium,
    High
}

public enum HardwareAccelerationMode
{
    Auto,
    Nvenc,
    QuickSync,
    Amf,
    SoftwareFallback
}
