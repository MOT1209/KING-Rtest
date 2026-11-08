using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Graphics;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;
using Microsoft.Extensions.Logging;
using KINGRecorder.Core.Enums;
using KINGRecorder.Core.Exceptions;
using KINGRecorder.Core.Interfaces;
using KINGRecorder.Core.Models;
using SharpDX.Direct3D11;
using Device = SharpDX.Direct3D11.Device;

namespace KINGRecorder.Capture.ScreenCapture;

public sealed class Direct3D11CaptureEngine : IScreenCaptureService
{
    private readonly ILogger<Direct3D11CaptureEngine> _logger;
    private GraphicsCaptureItem? _captureItem;
    private Direct3D11CaptureFramePool? _framePool;
    private GraphicsCaptureSession? _session;
    private Device? _d3dDevice;
    private IDirect3DDevice? _winRtDevice;
    private SizeInt32 _lastSize;
    private long _capturedFramesCount;

    public event EventHandler<FrameArrivedEventArgs>? FrameArrived;
    public bool IsCapturing { get; private set; }

    public Direct3D11CaptureEngine(ILogger<Direct3D11CaptureEngine> logger)
    {
        _logger = logger;
    }

    public Task<IReadOnlyList<DisplayInfo>> GetDisplaysAsync(CancellationToken ct = default)
    {
        var displays = new List<DisplayInfo>();

        try
        {
            bool success = EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData) =>
            {
                var mi = new MONITORINFOEX();
                mi.cbSize = (uint)Marshal.SizeOf(mi);
                if (GetMonitorInfo(hMonitor, ref mi))
                {
                    int width = mi.rcMonitor.Right - mi.rcMonitor.Left;
                    int height = mi.rcMonitor.Bottom - mi.rcMonitor.Top;
                    bool isPrimary = (mi.dwFlags & 0x00000001) != 0; // MONITORINFOF_PRIMARY

                    string deviceName = new string(mi.szDevice).TrimEnd('\0');
                    int refreshRate = 60;

                    // Query DEVMODE for refresh rate
                    var devMode = new DEVMODE();
                    devMode.dmSize = (short)Marshal.SizeOf(devMode);
                    if (EnumDisplaySettings(deviceName, -1, ref devMode))
                    {
                        if (devMode.dmDisplayFrequency > 0)
                        {
                            refreshRate = devMode.dmDisplayFrequency;
                        }
                    }

                    displays.Add(new DisplayInfo(
                        DeviceId: deviceName,
                        DisplayName: $"Display {displays.Count + 1}{(isPrimary ? " (Primary)" : "")}",
                        Width: width,
                        Height: height,
                        RefreshRateHz: refreshRate,
                        DpiScale: 1.0,
                        IsPrimary: isPrimary,
                        Bounds: new Rectangle(mi.rcMonitor.Left, mi.rcMonitor.Top, width, height)
                    ));
                }
                return true;
            }, IntPtr.Zero);

            if (!success || displays.Count == 0)
            {
                _logger.LogWarning("EnumDisplayMonitors returned no displays. Returning empty list without fake fallbacks.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "EnumDisplayMonitors failed or running in non-Windows environment.");
        }

        return Task.FromResult<IReadOnlyList<DisplayInfo>>(displays);
    }

    public Task<IReadOnlyList<WindowInfo>> GetOpenWindowsAsync(CancellationToken ct = default)
    {
        var windows = new List<WindowInfo>();

        try
        {
            EnumWindows((hWnd, lParam) =>
            {
                if (IsWindowVisible(hWnd) && !IsIconic(hWnd) && GetWindowTextLength(hWnd) > 0)
                {
                    // Filter cloaked UWP windows
                    if (DwmGetWindowAttribute(hWnd, DWMWA_CLOAKED, out bool isCloaked, sizeof(bool)) == 0 && isCloaked)
                    {
                        return true;
                    }

                    var sb = new StringBuilder(256);
                    GetWindowText(hWnd, sb, sb.Capacity);
                    string title = sb.ToString();

                    GetWindowRect(hWnd, out RECT rect);
                    int width = rect.Right - rect.Left;
                    int height = rect.Bottom - rect.Top;

                    if (width > 120 && height > 120)
                    {
                        GetWindowThreadProcessId(hWnd, out uint processId);
                        string processName = "unknown.exe";
                        try
                        {
                            var proc = Process.GetProcessById((int)processId);
                            processName = proc.ProcessName + ".exe";
                        }
                        catch
                        {
                            // Process may have exited or access denied
                        }

                        windows.Add(new WindowInfo(
                            Handle: hWnd,
                            Title: title,
                            ProcessName: processName,
                            Bounds: new Rectangle(rect.Left, rect.Top, width, height),
                            IsMinimized: false
                        ));
                    }
                }
                return true;
            }, IntPtr.Zero);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "EnumWindows failed or running in non-Windows environment.");
        }

        return Task.FromResult<IReadOnlyList<WindowInfo>>(windows);
    }

    public Task StartCaptureAsync(RecordingOptions options, CancellationToken ct = default)
    {
        _logger.LogInformation("Initializing Direct3D 11 screen capture in mode {Mode}", options.Mode);
        IsCapturing = false;

        throw new NotSupportedException("Windows.Graphics.Capture frame pool initialization is scheduled for Phase 1 implementation.");
    }

    public Task StopCaptureAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Stopping Direct3D 11 screen capture. Total frames: {Count}", _capturedFramesCount);
        IsCapturing = false;
        _session?.Dispose();
        _session = null;
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        StopCaptureAsync().GetAwaiter().GetResult();
        _framePool?.Dispose();
        _framePool = null;
    }

    // Win32 Interop P/Invoke
    private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData);
    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    private const uint DWMWA_CLOAKED = 14;

    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFOEX lpmi);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool EnumDisplaySettings(string lpszDeviceName, int iModeNum, ref DEVMODE lpDevMode);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int GetWindowTextLength(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(IntPtr hwnd, uint dwAttribute, out bool pvAttribute, int cbAttribute);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct MONITORINFOEX
    {
        public uint cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public char[] szDevice;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DEVMODE
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string dmDeviceName;
        public short dmSpecVersion;
        public short dmDriverVersion;
        public short dmSize;
        public short dmDriverExtra;
        public int dmFields;
        public int dmPositionX;
        public int dmPositionY;
        public int dmDisplayOrientation;
        public int dmDisplayFixedOutput;
        public short dmColor;
        public short dmDuplex;
        public short dmYResolution;
        public short dmTTOption;
        public short dmCollate;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string dmFormName;
        public short dmLogPixels;
        public int dmBitsPerPel;
        public int dmPelsWidth;
        public int dmPelsHeight;
        public int dmDisplayFlags;
        public int dmDisplayFrequency;
    }
}
