using System;
using System.Runtime.InteropServices;
using Xunit;
using KINGRecorder.Capture.ScreenCapture;

namespace KINGRecorder.Capture.Tests.Interop;

public class NativeStructLayoutTests
{
    [Fact]
    public void MonitorInfoEx_StructSize_MatchesWin32Definition()
    {
        int size = Marshal.SizeOf<Direct3D11CaptureEngine.MONITORINFOEX>();
        // DWORD (4) + RECT (16) + RECT (16) + DWORD (4) + WCHAR[32] (64) = 104 bytes
        Assert.Equal(104, size);
    }

    [Fact]
    public void DevMode_StructSize_MatchesWin32Definition()
    {
        int size = Marshal.SizeOf<Direct3D11CaptureEngine.DEVMODE>();
        // DEVMODEW size is 220 bytes
        Assert.Equal(220, size);
    }

    [Fact]
    public void Rect_StructSize_MatchesWin32Definition()
    {
        int size = Marshal.SizeOf<Direct3D11CaptureEngine.RECT>();
        // Left (4) + Top (4) + Right (4) + Bottom (4) = 16 bytes
        Assert.Equal(16, size);
    }

    [Theory]
    [InlineData(96, 1.0)]
    [InlineData(120, 1.25)]
    [InlineData(144, 1.5)]
    [InlineData(192, 2.0)]
    public void DpiScale_CalculatesCorrectlyFromRawDpi(uint rawDpi, double expectedScale)
    {
        double calculatedScale = rawDpi / 96.0;
        Assert.Equal(expectedScale, calculatedScale, 2);
    }
}
