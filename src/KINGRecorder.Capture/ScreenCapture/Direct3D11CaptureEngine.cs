using System;
using System.Collections.Generic;
using System.ComponentModel;
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
using Windows.Foundation;
using WinRT;
using Microsoft.Extensions.Logging;
using KINGRecorder.Core.Enums;
using KINGRecorder.Core.Exceptions;
using KINGRecorder.Core.Interfaces;
using KINGRecorder.Core.Models;
using SharpDX.Direct3D11;
using Device = SharpDX.Direct3D11.Device;

namespace KINGRecorder.Capture.ScreenCapture;

public sealed class Direct3D11CaptureEngine : IScreenCaptureService, IAsyncDisposable
{
    private readonly ILogger<Direct3D11CaptureEngine> _logger;
    private readonly object _syncLock = new();
    private Direct3D11CaptureFramePool? _framePool;
    private GraphicsCaptureSession? _session;
    private Device? _d3dDevice;
    private IDirect3DDevice? _winRtDevice;
    private bool _disposed;

    // Phase 1: real Windows.Graphics.Capture frame pool + capture session state.
    private GraphicsCaptureItem? _captureItem;
    private SizeInt32 _lastSize;
    private long _capturedFramesCount;

    // Declared by IScreenCaptureService; raised by the Phase 1 frame pool when frames arrive.
    public event EventHandler<FrameArrivedEventArgs>? FrameArrived;
    public bool IsCapturing { get; private set; }

    public Direct3D11CaptureEngine(ILogger<Direct3D11CaptureEngine> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Translates a capture region expressed in logical (DIP) coordinates into physical
    /// pixel bounds using the region's DPI scale. Pure, side-effect-free helper used by
    /// the custom-area (selection rectangle) capture path (Phase 1 task 4).
    /// </summary>
    public static Rectangle TranslateCaptureRegionToPixels(CaptureRegion region)
    {
        ArgumentNullException.ThrowIfNull(region);
        if (region.DpiScale <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(region), region.DpiScale, "DPI scale must be positive.");
        }

        return new Rectangle(
            (int)Math.Round(region.X * region.DpiScale),
            (int)Math.Round(region.Y * region.DpiScale),
            (int)Math.Round(region.Width * region.DpiScale),
            (int)Math.Round(region.Height * region.DpiScale));
    }

    /// <summary>
    /// Calculates DPI scale factor from raw DPI value.
    /// Baseline 96 DPI is 1.0 (100% scaling).
    /// </summary>
    public static double CalculateDpiScale(uint dpi)
    {
        if (dpi == 0)
        {
            return 1.0;
        }

        return Math.Round((double)dpi / 96.0, 4);
    }

    public Task<IReadOnlyList<DisplayInfo>> GetDisplaysAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var displays = new List<DisplayInfo>();

        try
        {
            bool success = EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData) =>
            {
                if (ct.IsCancellationRequested)
                {
                    return false; // Stop enumeration immediately on cancellation
                }

                var mi = new MONITORINFOEX();
                mi.cbSize = (uint)Marshal.SizeOf<MONITORINFOEX>();

                if (GetMonitorInfo(hMonitor, ref mi))
                {
                    int width = mi.rcMonitor.Right - mi.rcMonitor.Left;
                    int height = mi.rcMonitor.Bottom - mi.rcMonitor.Top;
                    bool isPrimary = (mi.dwFlags & MONITORINFOF_PRIMARY) != 0;

                    string deviceName = string.IsNullOrWhiteSpace(mi.szDevice)
                        ? $"\\\\.\\DISPLAY{displays.Count + 1}"
                        : mi.szDevice.Trim();

                    int refreshRate = 0; // Default to 0 (unknown/unavailable) unless verified

                    // Query DEVMODE for actual display frequency
                    var devMode = new DEVMODE();
                    devMode.dmSize = (ushort)Marshal.SizeOf<DEVMODE>();
                    if (EnumDisplaySettings(deviceName, ENUM_CURRENT_SETTINGS, ref devMode))
                    {
                        if ((devMode.dmFields & DM_DISPLAYFREQUENCY) != 0 && devMode.dmDisplayFrequency > 0)
                        {
                            refreshRate = (int)devMode.dmDisplayFrequency;
                        }
                    }
                    else
                    {
                        int err = Marshal.GetLastWin32Error();
                        _logger.LogDebug("EnumDisplaySettings returned false for device {Device} (Win32 error: {Error})", deviceName, err);
                    }

                    // Query real monitor DPI via shcore GetDpiForMonitor / user32 GetDpiForSystem
                    double dpiScale = 1.0;
                    try
                    {
                        int hresult = GetDpiForMonitor(hMonitor, MDT_EFFECTIVE_DPI, out uint dpiX, out _);
                        if (hresult == 0 && dpiX > 0)
                        {
                            dpiScale = CalculateDpiScale(dpiX);
                        }
                        else
                        {
                            _logger.LogDebug("GetDpiForMonitor returned HRESULT 0x{HResult:X8} for hMonitor {Handle}. Falling back to system DPI.", hresult, hMonitor);
                            uint systemDpi = GetDpiForSystem();
                            if (systemDpi > 0)
                            {
                                dpiScale = CalculateDpiScale(systemDpi);
                            }
                            else
                            {
                                int systemDpiErr = Marshal.GetLastWin32Error();
                                _logger.LogDebug("GetDpiForSystem returned 0 (Win32 error: {Error}). Defaulting DPI scale to 1.0.", systemDpiErr);
                                dpiScale = 1.0;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "DPI query threw an exception for hMonitor {Handle}. Defaulting DPI scale to 1.0.", hMonitor);
                        dpiScale = 1.0;
                    }

                    displays.Add(new DisplayInfo(
                        DeviceId: deviceName,
                        DisplayName: $"Display {displays.Count + 1}{(isPrimary ? " (Primary)" : "")}",
                        Width: width,
                        Height: height,
                        RefreshRateHz: refreshRate,
                        DpiScale: dpiScale,
                        IsPrimary: isPrimary,
                        Bounds: new Rectangle(mi.rcMonitor.Left, mi.rcMonitor.Top, width, height)
                    ));
                }
                else
                {
                    int err = Marshal.GetLastWin32Error();
                    _logger.LogDebug("GetMonitorInfo failed for hMonitor {Handle} with Win32 error {Error}", hMonitor, err);
                }

                return !ct.IsCancellationRequested;
            }, IntPtr.Zero);

            ct.ThrowIfCancellationRequested();

            if (!success && displays.Count == 0)
            {
                int err = Marshal.GetLastWin32Error();
                _logger.LogWarning("EnumDisplayMonitors returned false with Win32 error {Error}. Returning empty list without fake fallbacks.", err);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "EnumDisplayMonitors failed or running in non-Windows environment.");
        }

        return Task.FromResult<IReadOnlyList<DisplayInfo>>(displays);
    }

    public Task<IReadOnlyList<WindowInfo>> GetOpenWindowsAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var windows = new List<WindowInfo>();

        try
        {
            EnumWindows((hWnd, lParam) =>
            {
                if (ct.IsCancellationRequested)
                {
                    return false; // Stop enumeration immediately on cancellation
                }

                if (IsWindowVisible(hWnd) && GetWindowTextLength(hWnd) > 0)
                {
                    bool isMinimized = IsIconic(hWnd);

                    // Filter cloaked UWP windows (using 4-byte BOOL marshalling)
                    int dwmResult = DwmGetWindowAttribute(hWnd, DWMWA_CLOAKED, out bool isCloaked, sizeof(int));
                    if (dwmResult == 0 && isCloaked)
                    {
                        return !ct.IsCancellationRequested;
                    }

                    var sb = new StringBuilder(256);
                    int textLen = GetWindowText(hWnd, sb, sb.Capacity);
                    if (textLen <= 0)
                    {
                        return !ct.IsCancellationRequested;
                    }

                    string title = sb.ToString().Trim();
                    if (string.IsNullOrEmpty(title))
                    {
                        return !ct.IsCancellationRequested;
                    }

                    // Explicitly check GetWindowRect return value
                    if (!GetWindowRect(hWnd, out RECT rect))
                    {
                        int err = Marshal.GetLastWin32Error();
                        _logger.LogDebug("GetWindowRect failed for hWnd {Handle} with Win32 error {Error}", hWnd, err);
                        return !ct.IsCancellationRequested;
                    }

                    int width = rect.Right - rect.Left;
                    int height = rect.Bottom - rect.Top;

                    if (width > 50 && height > 50)
                    {
                        GetWindowThreadProcessId(hWnd, out uint processId);
                        string processName = "Unknown Process";

                        if (processId != 0)
                        {
                            try
                            {
                                using var proc = Process.GetProcessById((int)processId);
                                processName = proc.ProcessName + ".exe";
                            }
                            catch (ArgumentException ex)
                            {
                                _logger.LogDebug(ex, "Process ID {ProcessId} for hWnd {Handle} is no longer running.", processId, hWnd);
                            }
                            catch (InvalidOperationException ex)
                            {
                                _logger.LogDebug(ex, "Process ID {ProcessId} for hWnd {Handle} has terminated.", processId, hWnd);
                            }
                            catch (Win32Exception ex)
                            {
                                _logger.LogDebug(ex, "Access denied querying process ID {ProcessId} for hWnd {Handle}.", processId, hWnd);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogDebug(ex, "Unexpected error retrieving process metadata for PID {ProcessId}.", processId);
                            }
                        }

                        windows.Add(new WindowInfo(
                            Handle: hWnd,
                            Title: title,
                            ProcessName: processName,
                            Bounds: new Rectangle(rect.Left, rect.Top, width, height),
                            IsMinimized: isMinimized
                        ));
                    }
                }

                return !ct.IsCancellationRequested;
            }, IntPtr.Zero);

            ct.ThrowIfCancellationRequested();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "EnumWindows failed or running in non-Windows environment.");
        }

        return Task.FromResult<IReadOnlyList<WindowInfo>>(windows);
    }

    public Task StartCaptureAsync(RecordingOptions options, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        _logger.LogInformation("Initializing Direct3D 11 screen capture in mode {Mode}", options.Mode);
        IsCapturing = false;

        // Phase 1 (task 3): build the Windows.Graphics.Capture frame pool and capture session.
        // The frame pool delivers zero-copy GPU frames through FrameArrived.
        var captureItem = CreateCaptureItem(options);
        var d3dDevice = CreateD3d11Device();
        var winRtDevice = CreateWinRtDevice(d3dDevice);

        _captureItem = captureItem;
        _d3dDevice = d3dDevice;
        _winRtDevice = winRtDevice;
        _lastSize = captureItem.Size;

        _framePool = Direct3D11CaptureFramePool.CreateFreeThreaded(
            winRtDevice,
            DirectXPixelFormat.B8G8R8A8UIntNormalized,
            2,
            _lastSize);

        _framePool.FrameArrived += OnFrameArrived;
        captureItem.Closed += OnCaptureItemClosed;

        _session = _framePool.CreateCaptureSession(captureItem);
        _session.IsCursorCaptureEnabled = true;

        _session.StartCapture();
        IsCapturing = true;

        return Task.CompletedTask;
    }

    public Task StopCaptureAsync(CancellationToken ct = default)
    {
        lock (_syncLock)
        {
            _logger.LogInformation("Stopping Direct3D 11 screen capture.");
            IsCapturing = false;

            _session?.Dispose();
            _session = null;

            _framePool?.Dispose();
            _framePool = null;

            _captureItem = null;
        }

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        lock (_syncLock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            IsCapturing = false;

            _session?.Dispose();
            _session = null;

            _framePool?.Dispose();
            _framePool = null;

            _captureItem = null;

            _d3dDevice?.Dispose();
            _d3dDevice = null;

            _winRtDevice?.Dispose();
            _winRtDevice = null;
        }

        GC.SuppressFinalize(this);
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }

    private void OnFrameArrived(Direct3D11CaptureFramePool sender, object args)
    {
        using var frame = sender.TryGetNextFrame();
        if (frame is null)
        {
            return;
        }

        Interlocked.Increment(ref _capturedFramesCount);

        var contentSize = frame.ContentSize;
        if (contentSize.Width != _lastSize.Width || contentSize.Height != _lastSize.Height)
        {
            _lastSize = contentSize;
            _framePool?.Recreate(_winRtDevice, DirectXPixelFormat.B8G8R8A8UIntNormalized, 2, _lastSize);
        }

        FrameArrived?.Invoke(this, new FrameArrivedEventArgs(
            frame.Surface is null ? IntPtr.Zero : ((WinRT.IWinRTObject)frame.Surface).NativeObject.ThisPtr,
            frame.SystemRelativeTime,
            contentSize.Width,
            contentSize.Height));
    }

    private void OnCaptureItemClosed(GraphicsCaptureItem sender, object args)
    {
        StopCaptureAsync(CancellationToken.None);
    }

    private GraphicsCaptureItem CreateCaptureItem(RecordingOptions options)
    {
        // Windows.Graphics.Capture.GraphicsCaptureItem in the Windows 10 2004 SDK projection only exposes
        // CreateFromVisual; display/window-targeted capture (CreateForDisplay/CreateForWindow) requires a
        // newer Windows SDK (>= 10.0.22000.0). Surface that limitation as a structured error instead of
        // silently producing a null item or swallowing the unsupported target.
        throw new NotSupportedException(
            "Screen/window capture items cannot be created with the Windows 10 2004 SDK projection " +
            "(GraphicsCaptureItem only exposes CreateFromVisual). Display- and window-targeted capture " +
            "requires a Windows SDK >= 10.0.22000.0 (CreateForDisplay/CreateForWindow). " +
            $"Requested mode: {options.Mode}.");
    }

    private Device CreateD3d11Device()
    {
        return new Device(
            SharpDX.Direct3D.DriverType.Hardware,
            SharpDX.Direct3D11.DeviceCreationFlags.BgraSupport,
            new[] { SharpDX.Direct3D.FeatureLevel.Level_11_0 });
    }

    private IDirect3DDevice CreateWinRtDevice(Device d3dDevice)
    {
        using var dxgiDevice = d3dDevice.QueryInterface<SharpDX.DXGI.Device>();
        int hr = CreateDirect3D11DeviceFromDXGIDevice(dxgiDevice.NativePointer, out IntPtr graphicsDevicePtr);
        if (hr < 0)
        {
            throw new System.ComponentModel.Win32Exception(hr);
        }

        try
        {
            return WinRT.MarshalInterface<Windows.Graphics.DirectX.Direct3D11.IDirect3DDevice>.FromAbi(graphicsDevicePtr);
        }
        finally
        {
            System.Runtime.InteropServices.Marshal.Release(graphicsDevicePtr);
        }
    }

    // Native Constants & Interop Declarations
    [DllImport("d3d11.dll", EntryPoint = "CreateDirect3D11DeviceFromDXGIDevice", PreserveSig = true)]
    private static extern int CreateDirect3D11DeviceFromDXGIDevice(IntPtr dxgiDevice, out IntPtr graphicsDevice);
    private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData);
    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    private const uint MONITORINFOF_PRIMARY = 0x00000001;
    private const uint DWMWA_CLOAKED = 14;
    private const uint DM_DISPLAYFREQUENCY = 0x00400000;
    private const int ENUM_CURRENT_SETTINGS = -1;
    private const int MDT_EFFECTIVE_DPI = 0;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFOEX lpmi);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool EnumDisplaySettings(string lpszDeviceName, int iModeNum, ref DEVMODE lpDevMode);

    [DllImport("shcore.dll", SetLastError = true)]
    private static extern int GetDpiForMonitor(IntPtr hMonitor, int dpiType, out uint dpiX, out uint dpiY);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetDpiForSystem();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int GetWindowTextLength(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("dwmapi.dll", SetLastError = true)]
    private static extern int DwmGetWindowAttribute(IntPtr hwnd, uint dwAttribute, [MarshalAs(UnmanagedType.Bool)] out bool pvAttribute, int cbAttribute);

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct MONITORINFOEX
    {
        public uint cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string szDevice;
    }

    [StructLayout(LayoutKind.Explicit, CharSet = CharSet.Unicode)]
    public struct DEVMODE
    {
        [FieldOffset(0)]
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string dmDeviceName;

        [FieldOffset(64)]
        public ushort dmSpecVersion;

        [FieldOffset(66)]
        public ushort dmDriverVersion;

        [FieldOffset(68)]
        public ushort dmSize;

        [FieldOffset(70)]
        public ushort dmDriverExtra;

        [FieldOffset(72)]
        public uint dmFields;

        [FieldOffset(76)]
        public int dmPositionX;

        [FieldOffset(80)]
        public int dmPositionY;

        [FieldOffset(84)]
        public uint dmDisplayOrientation;

        [FieldOffset(88)]
        public uint dmDisplayFixedOutput;

        [FieldOffset(92)]
        public short dmColor;

        [FieldOffset(94)]
        public short dmDuplex;

        [FieldOffset(96)]
        public short dmYResolution;

        [FieldOffset(98)]
        public short dmTTOption;

        [FieldOffset(100)]
        public short dmCollate;

        // DEVMODEW holds dmFormName (WCHAR[32]) at 102..166. It is deliberately not projected: a reference
        // field at that unaligned explicit offset makes the CLR reject the whole type with TypeLoadException,
        // and display enumeration only needs dmFields and dmDisplayFrequency.

        [FieldOffset(166)]
        public ushort dmLogPixels;

        [FieldOffset(168)]
        public uint dmBitsPerPel;

        [FieldOffset(172)]
        public uint dmPelsWidth;

        [FieldOffset(176)]
        public uint dmPelsHeight;

        [FieldOffset(180)]
        public uint dmDisplayFlags;

        [FieldOffset(184)]
        public uint dmDisplayFrequency;

        [FieldOffset(188)]
        public uint dmICMMethod;

        [FieldOffset(192)]
        public uint dmICMIntent;

        [FieldOffset(196)]
        public uint dmMediaType;

        [FieldOffset(200)]
        public uint dmDTType;

        [FieldOffset(204)]
        public uint dmReserved1;

        [FieldOffset(208)]
        public uint dmReserved2;

        [FieldOffset(212)]
        public uint dmPanningWidth;

        [FieldOffset(216)]
        public uint dmPanningHeight;
    }
}
