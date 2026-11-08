using System;

namespace KINGRecorder.Core.Exceptions;

public class KingRecorderException : Exception
{
    public KingRecorderException(string message) : base(message) { }
    public KingRecorderException(string message, Exception innerException) : base(message, innerException) { }
}

public class CaptureDeviceException : KingRecorderException
{
    public CaptureDeviceException(string message) : base(message) { }
    public CaptureDeviceException(string message, Exception innerException) : base(message, innerException) { }
}

public class AudioCaptureException : KingRecorderException
{
    public AudioCaptureException(string message) : base(message) { }
    public AudioCaptureException(string message, Exception innerException) : base(message, innerException) { }
}

public class VideoEncoderException : KingRecorderException
{
    public VideoEncoderException(string message) : base(message) { }
    public VideoEncoderException(string message, Exception innerException) : base(message, innerException) { }
}

public class StoragePermissionException : KingRecorderException
{
    public StoragePermissionException(string message) : base(message) { }
    public StoragePermissionException(string message, Exception innerException) : base(message, innerException) { }
}
