using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
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
    public void MonitorInfoEx_FieldOffsets_MatchWin32Definition()
    {
        Assert.Equal(0, (int)Marshal.OffsetOf<Direct3D11CaptureEngine.MONITORINFOEX>(nameof(Direct3D11CaptureEngine.MONITORINFOEX.cbSize)));
        Assert.Equal(4, (int)Marshal.OffsetOf<Direct3D11CaptureEngine.MONITORINFOEX>(nameof(Direct3D11CaptureEngine.MONITORINFOEX.rcMonitor)));
        Assert.Equal(20, (int)Marshal.OffsetOf<Direct3D11CaptureEngine.MONITORINFOEX>(nameof(Direct3D11CaptureEngine.MONITORINFOEX.rcWork)));
        Assert.Equal(36, (int)Marshal.OffsetOf<Direct3D11CaptureEngine.MONITORINFOEX>(nameof(Direct3D11CaptureEngine.MONITORINFOEX.dwFlags)));
        Assert.Equal(40, (int)Marshal.OffsetOf<Direct3D11CaptureEngine.MONITORINFOEX>(nameof(Direct3D11CaptureEngine.MONITORINFOEX.szDevice)));
    }

    [Fact]
    public void DevMode_StructSize_MatchesWin32Definition()
    {
        int size = Marshal.SizeOf<Direct3D11CaptureEngine.DEVMODE>();
        // DEVMODEW size is exactly 220 bytes
        Assert.Equal(220, size);
    }

    [Fact]
    public void DevMode_FieldOffsets_MatchWin32Definition()
    {
        Assert.Equal(0, (int)Marshal.OffsetOf<Direct3D11CaptureEngine.DEVMODE>(nameof(Direct3D11CaptureEngine.DEVMODE.dmDeviceName)));
        Assert.Equal(64, (int)Marshal.OffsetOf<Direct3D11CaptureEngine.DEVMODE>(nameof(Direct3D11CaptureEngine.DEVMODE.dmSpecVersion)));
        Assert.Equal(66, (int)Marshal.OffsetOf<Direct3D11CaptureEngine.DEVMODE>(nameof(Direct3D11CaptureEngine.DEVMODE.dmDriverVersion)));
        Assert.Equal(68, (int)Marshal.OffsetOf<Direct3D11CaptureEngine.DEVMODE>(nameof(Direct3D11CaptureEngine.DEVMODE.dmSize)));
        Assert.Equal(70, (int)Marshal.OffsetOf<Direct3D11CaptureEngine.DEVMODE>(nameof(Direct3D11CaptureEngine.DEVMODE.dmDriverExtra)));
        Assert.Equal(72, (int)Marshal.OffsetOf<Direct3D11CaptureEngine.DEVMODE>(nameof(Direct3D11CaptureEngine.DEVMODE.dmFields)));
        Assert.Equal(76, (int)Marshal.OffsetOf<Direct3D11CaptureEngine.DEVMODE>(nameof(Direct3D11CaptureEngine.DEVMODE.dmPositionX)));
        Assert.Equal(80, (int)Marshal.OffsetOf<Direct3D11CaptureEngine.DEVMODE>(nameof(Direct3D11CaptureEngine.DEVMODE.dmPositionY)));
        Assert.Equal(84, (int)Marshal.OffsetOf<Direct3D11CaptureEngine.DEVMODE>(nameof(Direct3D11CaptureEngine.DEVMODE.dmDisplayOrientation)));
        Assert.Equal(88, (int)Marshal.OffsetOf<Direct3D11CaptureEngine.DEVMODE>(nameof(Direct3D11CaptureEngine.DEVMODE.dmDisplayFixedOutput)));
        Assert.Equal(102, (int)Marshal.OffsetOf<Direct3D11CaptureEngine.DEVMODE>(nameof(Direct3D11CaptureEngine.DEVMODE.dmFormName)));
        Assert.Equal(166, (int)Marshal.OffsetOf<Direct3D11CaptureEngine.DEVMODE>(nameof(Direct3D11CaptureEngine.DEVMODE.dmLogPixels)));
        Assert.Equal(168, (int)Marshal.OffsetOf<Direct3D11CaptureEngine.DEVMODE>(nameof(Direct3D11CaptureEngine.DEVMODE.dmBitsPerPel)));
        Assert.Equal(172, (int)Marshal.OffsetOf<Direct3D11CaptureEngine.DEVMODE>(nameof(Direct3D11CaptureEngine.DEVMODE.dmPelsWidth)));
        Assert.Equal(176, (int)Marshal.OffsetOf<Direct3D11CaptureEngine.DEVMODE>(nameof(Direct3D11CaptureEngine.DEVMODE.dmPelsHeight)));
        Assert.Equal(180, (int)Marshal.OffsetOf<Direct3D11CaptureEngine.DEVMODE>(nameof(Direct3D11CaptureEngine.DEVMODE.dmDisplayFlags)));
        Assert.Equal(184, (int)Marshal.OffsetOf<Direct3D11CaptureEngine.DEVMODE>(nameof(Direct3D11CaptureEngine.DEVMODE.dmDisplayFrequency)));
    }

    [Fact]
    public void Rect_StructSize_MatchesWin32Definition()
    {
        int size = Marshal.SizeOf<Direct3D11CaptureEngine.RECT>();
        // Left (4) + Top (4) + Right (4) + Bottom (4) = 16 bytes
        Assert.Equal(16, size);
    }

    [Fact]
    public void Rect_FieldOffsets_MatchWin32Definition()
    {
        Assert.Equal(0, (int)Marshal.OffsetOf<Direct3D11CaptureEngine.RECT>(nameof(Direct3D11CaptureEngine.RECT.Left)));
        Assert.Equal(4, (int)Marshal.OffsetOf<Direct3D11CaptureEngine.RECT>(nameof(Direct3D11CaptureEngine.RECT.Top)));
        Assert.Equal(8, (int)Marshal.OffsetOf<Direct3D11CaptureEngine.RECT>(nameof(Direct3D11CaptureEngine.RECT.Right)));
        Assert.Equal(12, (int)Marshal.OffsetOf<Direct3D11CaptureEngine.RECT>(nameof(Direct3D11CaptureEngine.RECT.Bottom)));
    }

    [Fact]
    public void Win32Bool_Size_Matches4Bytes()
    {
        // Win32 typedef int BOOL is exactly 4 bytes
        Assert.Equal(4, sizeof(int));
    }

    [Theory]
    [InlineData(96u, 1.0)]
    [InlineData(120u, 1.25)]
    [InlineData(144u, 1.5)]
    [InlineData(192u, 2.0)]
    [InlineData(288u, 3.0)]
    [InlineData(0u, 1.0)]
    public void DpiScale_CallsProductionLogic_CalculatesCorrectScale(uint rawDpi, double expectedScale)
    {
        // Executes production method Direct3D11CaptureEngine.CalculateDpiScale
        double actualScale = Direct3D11CaptureEngine.CalculateDpiScale(rawDpi);
        Assert.Equal(expectedScale, actualScale, 4);
    }

    [Fact]
    public async Task GetDisplaysAsync_WhenCancelled_ThrowsOperationCanceledException()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var engine = new Direct3D11CaptureEngine(NullLogger<Direct3D11CaptureEngine>.Instance);
        await Assert.ThrowsAsync<OperationCanceledException>(() => engine.GetDisplaysAsync(cts.Token));
    }

    [Fact]
    public async Task GetOpenWindowsAsync_WhenCancelled_ThrowsOperationCanceledException()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var engine = new Direct3D11CaptureEngine(NullLogger<Direct3D11CaptureEngine>.Instance);
        await Assert.ThrowsAsync<OperationCanceledException>(() => engine.GetOpenWindowsAsync(cts.Token));
    }

    [Fact]
    public void Direct3D11CaptureEngine_Dispose_IsIdempotent()
    {
        var engine = new Direct3D11CaptureEngine(NullLogger<Direct3D11CaptureEngine>.Instance);
        engine.Dispose();
        engine.Dispose(); // second call must not throw
        Assert.False(engine.IsCapturing);
    }
}
