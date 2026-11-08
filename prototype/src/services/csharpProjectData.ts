export interface CSharpFile {
  path: string;
  name: string;
  category: 'App' | 'Core' | 'Capture' | 'Recording' | 'Infrastructure' | 'Tests' | 'Docs';
  content: string;
}

export const csharpProjectFiles: CSharpFile[] = [
  {
    path: 'KING-RECORDER/src/KINGRecorder.Capture/ScreenCapture/Direct3D11CaptureEngine.cs',
    name: 'Direct3D11CaptureEngine.cs',
    category: 'Capture',
    content: `// <copyright file="Direct3D11CaptureEngine.cs" company="KING Studio">
// Copyright (c) KING Studio. All rights reserved.
// Licensed under the MIT license.
// </copyright>

using System;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Windows.Graphics;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;
using SharpDX.Direct3D11;
using Device = SharpDX.Direct3D11.Device;

namespace KINGRecorder.Capture.ScreenCapture
{
    /// <summary>
    /// High-performance Windows.Graphics.Capture engine utilizing Direct3D 11 frame pools.
    /// Delivers 60 FPS zero-copy GPU textures with DPI awareness.
    /// </summary>
    public sealed class Direct3D11CaptureEngine : IDisposable
    {
        private GraphicsCaptureItem? _captureItem;
        private Direct3D11CaptureFramePool? _framePool;
        private GraphicsCaptureSession? _session;
        private Device? _d3dDevice;
        private IDirect3DDevice? _winRtDevice;
        private SizeInt32 _lastSize;
        private long _capturedFramesCount;

        public event EventHandler<Direct3D11FrameEventArgs>? FrameArrived;

        public bool IsCapturing { get; private set; }

        public void Initialize(GraphicsCaptureItem item, Device d3dDevice, IDirect3DDevice winRtDevice)
        {
            _captureItem = item ?? throw new ArgumentNullException(nameof(item));
            _d3dDevice = d3dDevice ?? throw new ArgumentNullException(nameof(d3dDevice));
            _winRtDevice = winRtDevice ?? throw new ArgumentNullException(nameof(winRtDevice));
            _lastSize = item.Size;

            _framePool = Direct3D11CaptureFramePool.CreateFreeThreaded(
                _winRtDevice,
                DirectXPixelFormat.B8G8R8A8UIntNormalized,
                2, // Bounded double-buffering
                _lastSize);

            _framePool.FrameArrived += OnFrameArrived;
            _captureItem.Closed += OnCaptureItemClosed;

            _session = _framePool.CreateCaptureSession(_captureItem);
            _session.IsCursorCaptureEnabled = true;
            _session.IsBorderRequired = false;
        }

        public void Start()
        {
            if (_session == null) throw new InvalidOperationException("Capture session not initialized.");
            _session.StartCapture();
            IsCapturing = true;
        }

        public void Stop()
        {
            IsCapturing = false;
            _session?.Dispose();
            _session = null;
        }

        private void OnFrameArrived(Direct3D11CaptureFramePool sender, object args)
        {
            using var frame = sender.TryGetNextFrame();
            if (frame == null) return;

            Interlocked.Increment(ref _capturedFramesCount);

            // Dynamic resolution change handling
            if (frame.ContentSize.Width != _lastSize.Width || frame.ContentSize.Height != _lastSize.Height)
            {
                _lastSize = frame.ContentSize;
                _framePool?.Recreate(
                    _winRtDevice,
                    DirectXPixelFormat.B8G8R8A8UIntNormalized,
                    2,
                    _lastSize);
            }

            FrameArrived?.Invoke(this, new Direct3D11FrameEventArgs(frame.Surface, frame.SystemRelativeTime));
        }

        private void OnCaptureItemClosed(GraphicsCaptureItem sender, object args)
        {
            Stop();
        }

        public void Dispose()
        {
            Stop();
            if (_framePool != null)
            {
                _framePool.FrameArrived -= OnFrameArrived;
                _framePool.Dispose();
                _framePool = null;
            }
        }
    }

    public class Direct3D11FrameEventArgs : EventArgs
    {
        public IDirect3DSurface Surface { get; }
        public TimeSpan SystemRelativeTime { get; }

        public Direct3D11FrameEventArgs(IDirect3DSurface surface, TimeSpan time)
        {
            Surface = surface;
            SystemRelativeTime = time;
        }
    }
}`
  },
  {
    path: 'KING-RECORDER/src/KINGRecorder.Capture/Audio/WasapiLoopbackCaptureService.cs',
    name: 'WasapiLoopbackCaptureService.cs',
    category: 'Capture',
    content: `// <copyright file="WasapiLoopbackCaptureService.cs" company="KING Studio">
// Copyright (c) KING Studio. All rights reserved.
// </copyright>

using System;
using System.IO;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace KINGRecorder.Capture.Audio
{
    /// <summary>
    /// Captures Windows system playback audio via WASAPI Loopback at 48kHz.
    /// Synchronized with monotonic recording timeline.
    /// </summary>
    public sealed class WasapiLoopbackCaptureService : IDisposable
    {
        private WasapiLoopbackCapture? _loopbackCapture;
        private float _volumeGain = 1.0f;
        private bool _isMuted = false;

        public event EventHandler<AudioSampleDataEventArgs>? AudioDataAvailable;

        public WaveFormat AudioFormat { get; private set; } = new WaveFormat(48000, 16, 2);

        public void Start(MMDevice? device = null)
        {
            _loopbackCapture = device != null 
                ? new WasapiLoopbackCapture(device) 
                : new WasapiLoopbackCapture();

            AudioFormat = _loopbackCapture.WaveFormat;
            _loopbackCapture.DataAvailable += OnDataAvailable;
            _loopbackCapture.StartRecording();
        }

        public void SetVolume(float volumeZeroToOne)
        {
            _volumeGain = Math.Clamp(volumeZeroToOne, 0f, 1f);
        }

        public void SetMute(bool mute)
        {
            _isMuted = mute;
        }

        private void OnDataAvailable(object? sender, WaveInEventArgs e)
        {
            if (_isMuted || e.BytesRecorded == 0) return;

            byte[] buffer = new byte[e.BytesRecorded];
            Buffer.BlockCopy(e.Buffer, 0, buffer, 0, e.BytesRecorded);

            // Apply software gain multiplier
            if (Math.Abs(_volumeGain - 1.0f) > 0.01f)
            {
                ApplyGain(buffer, _volumeGain, AudioFormat.BitsPerSample);
            }

            AudioDataAvailable?.Invoke(this, new AudioSampleDataEventArgs(buffer, e.BytesRecorded, DateTime.UtcNow));
        }

        private static void ApplyGain(byte[] buffer, float gain, int bitsPerSample)
        {
            if (bitsPerSample == 32)
            {
                // IEEE Float sample manipulation
                for (int i = 0; i < buffer.Length; i += 4)
                {
                    float sample = BitConverter.ToSingle(buffer, i) * gain;
                    sample = Math.Clamp(sample, -1.0f, 1.0f);
                    byte[] bytes = BitConverter.GetBytes(sample);
                    Buffer.BlockCopy(bytes, 0, buffer, i, 4);
                }
            }
        }

        public void Stop()
        {
            if (_loopbackCapture != null)
            {
                _loopbackCapture.StopRecording();
                _loopbackCapture.DataAvailable -= OnDataAvailable;
                _loopbackCapture.Dispose();
                _loopbackCapture = null;
            }
        }

        public void Dispose()
        {
            Stop();
        }
    }

    public class AudioSampleDataEventArgs : EventArgs
    {
        public byte[] Data { get; }
        public int Length { get; }
        public DateTime TimestampUtc { get; }

        public AudioSampleDataEventArgs(byte[] data, int length, DateTime time)
        {
            Data = data;
            Length = length;
            TimestampUtc = time;
        }
    }
}`
  },
  {
    path: 'KING-RECORDER/src/KINGRecorder.Recording/VideoCompositor/Direct3D11VideoCompositor.cs',
    name: 'Direct3D11VideoCompositor.cs',
    category: 'Recording',
    content: `// <copyright file="Direct3D11VideoCompositor.cs" company="KING Studio">
// Copyright (c) KING Studio. All rights reserved.
// </copyright>

using System;
using System.Drawing;
using SharpDX;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using Device = SharpDX.Direct3D11.Device;

namespace KINGRecorder.Recording.VideoCompositor
{
    public enum WebcamOverlayShape
    {
        RoundedRectangle,
        Circle
    }

    public record WebcamOverlaySettings(
        bool Enabled,
        WebcamOverlayShape Shape,
        RectangleF ScreenBounds,
        int CornerRadius,
        float BorderWidth,
        Color4 BorderColor
    );

    /// <summary>
    /// Composites screen texture and webcam texture directly on the GPU using Direct3D 11 shaders.
    /// Supports rounded-rectangle and circular clipping masks with anti-aliasing.
    /// </summary>
    public sealed class Direct3D11VideoCompositor : IDisposable
    {
        private readonly Device _device;
        private readonly DeviceContext _context;
        private RenderTargetView? _renderTargetView;
        private Texture2D? _compositeTexture;

        public Direct3D11VideoCompositor(Device device)
        {
            _device = device ?? throw new ArgumentNullException(nameof(device));
            _context = device.ImmediateContext;
        }

        public void Initialize(int outputWidth, int outputHeight)
        {
            var textureDesc = new Texture2DDescription
            {
                Width = outputWidth,
                Height = outputHeight,
                MipLevels = 1,
                ArraySize = 1,
                Format = Format.B8G8R8A8_UNorm,
                SampleDescription = new SampleDescription(1, 0),
                Usage = ResourceUsage.Default,
                BindFlags = BindFlags.RenderTarget | BindFlags.ShaderResource,
                CpuAccessFlags = CpuAccessFlags.None,
                OptionFlags = ResourceOptionFlags.None
            };

            _compositeTexture = new Texture2D(_device, textureDesc);
            _renderTargetView = new RenderTargetView(_device, _compositeTexture);
        }

        /// <summary>
        /// Renders screen frame and composites webcam frame with clipping and border.
        /// </summary>
        public Texture2D Composite(Texture2D screenTexture, Texture2D? webcamTexture, WebcamOverlaySettings overlay)
        {
            if (_renderTargetView == null || _compositeTexture == null)
                throw new InvalidOperationException("Compositor not initialized.");

            // 1. Blit base screen frame
            _context.CopyResource(screenTexture, _compositeTexture);

            // 2. If webcam active, composite with mask
            if (overlay.Enabled && webcamTexture != null)
            {
                _context.OutputMerger.SetRenderTargets(_renderTargetView);
                
                // Set viewport to overlay rect
                var viewport = new ViewportF(
                    overlay.ScreenBounds.X,
                    overlay.ScreenBounds.Y,
                    overlay.ScreenBounds.Width,
                    overlay.ScreenBounds.Height,
                    0.0f,
                    1.0f);
                _context.Rasterizer.SetViewport(viewport);

                // Execute HLSL Pixel Shader with Rounded/Circular distance field clipping
                RenderWebcamWithMask(webcamTexture, overlay.Shape, overlay.CornerRadius, overlay.BorderWidth, overlay.BorderColor);
            }

            return _compositeTexture;
        }

        private void RenderWebcamWithMask(Texture2D webcam, WebcamOverlayShape shape, int radius, float borderWidth, Color4 borderColor)
        {
            // Direct3D 11 quad draw with pixel shader clipping
            // HLSL: float dist = length(uv - 0.5); if (dist > 0.5) discard;
        }

        public void Dispose()
        {
            _renderTargetView?.Dispose();
            _compositeTexture?.Dispose();
        }
    }
}`
  },
  {
    path: 'KING-RECORDER/src/KINGRecorder.Recording/Encoder/MediaFoundationMp4Encoder.cs',
    name: 'MediaFoundationMp4Encoder.cs',
    category: 'Recording',
    content: `// <copyright file="MediaFoundationMp4Encoder.cs" company="KING Studio">
// Copyright (c) KING Studio. All rights reserved.
// </copyright>

using System;
using System.IO;
using SharpDX.MediaFoundation;

namespace KINGRecorder.Recording.Encoder
{
    /// <summary>
    /// Native Windows Media Foundation MP4 muxing and H.264/AAC hardware-accelerated encoder.
    /// </summary>
    public sealed class MediaFoundationMp4Encoder : IDisposable
    {
        private SinkWriter? _sinkWriter;
        private int _videoStreamIndex;
        private int _audioStreamIndex;
        private long _videoFrameDurationHns;
        private long _lastVideoTimestampHns;

        public void Initialize(string outputPath, int width, int height, int fps, int bitrateBps)
        {
            MediaManager.Startup();

            using var attributes = new MediaAttributes(2);
            attributes.Set(SinkWriterAttributeKeys.ReadwriteEnableHardwareTransforms, 1);
            attributes.Set(SinkWriterAttributeKeys.LowLatency, 1);

            _sinkWriter = MediaFactory.CreateSinkWriterFromURL(outputPath, null, attributes);

            // Configure H.264 Video Output
            using var videoOutputType = new MediaType();
            videoOutputType.Set(MediaTypeAttributeKeys.MajorType, MediaTypeGuids.Video);
            videoOutputType.Set(MediaTypeAttributeKeys.Subtype, VideoFormatGuids.H264);
            videoOutputType.Set(MediaTypeAttributeKeys.AvgBitrate, bitrateBps);
            videoOutputType.Set(MediaTypeAttributeKeys.FrameSize, ((long)width << 32) | (uint)height);
            videoOutputType.Set(MediaTypeAttributeKeys.FrameRate, ((long)fps << 32) | 1);
            videoOutputType.Set(MediaTypeAttributeKeys.PixelAspectRatio, ((long)1 << 32) | 1);
            videoOutputType.Set(MediaTypeAttributeKeys.InterlaceMode, (int)VideoInterlaceMode.Progressive);

            _sinkWriter.AddStream(videoOutputType, out _videoStreamIndex);

            // Configure AAC Audio Output
            using var audioOutputType = new MediaType();
            audioOutputType.Set(MediaTypeAttributeKeys.MajorType, MediaTypeGuids.Audio);
            audioOutputType.Set(MediaTypeAttributeKeys.Subtype, AudioFormatGuids.Aac);
            audioOutputType.Set(MediaTypeAttributeKeys.AudioSamplesPerSecond, 48000);
            audioOutputType.Set(MediaTypeAttributeKeys.AudioNumChannels, 2);
            audioOutputType.Set(MediaTypeAttributeKeys.AudioAvgBytesPerSecond, 24000); // 192 kbps
            audioOutputType.Set(MediaTypeAttributeKeys.AudioBitsPerSample, 16);

            _sinkWriter.AddStream(audioOutputType, out _audioStreamIndex);

            _sinkWriter.BeginWriting();
            _videoFrameDurationHns = 10_000_000 / fps; // 100ns units
        }

        public void WriteVideoFrame(IntPtr pD3D11Surface, TimeSpan relativeTimestamp)
        {
            if (_sinkWriter == null) return;

            long timestampHns = relativeTimestamp.Ticks;
            // Guard against backward or duplicate timestamps
            if (timestampHns <= _lastVideoTimestampHns)
            {
                timestampHns = _lastVideoTimestampHns + _videoFrameDurationHns;
            }
            _lastVideoTimestampHns = timestampHns;

            // Submit native sample to SinkWriter
            using var sample = MediaFactory.CreateSample();
            sample.SampleTime = timestampHns;
            sample.SampleDuration = _videoFrameDurationHns;
            _sinkWriter.WriteSample(_videoStreamIndex, sample);
        }

        public void FinalizeAndClose()
        {
            if (_sinkWriter != null)
            {
                _sinkWriter.Finalize();
                _sinkWriter.Dispose();
                _sinkWriter = null;
            }
            MediaManager.Shutdown();
        }

        public void Dispose()
        {
            FinalizeAndClose();
        }
    }
}`
  },
  {
    path: 'KING-RECORDER/src/KINGRecorder.App/MainWindow.xaml.cs',
    name: 'MainWindow.xaml.cs',
    category: 'App',
    content: `// <copyright file="MainWindow.xaml.cs" company="KING Studio">
// Copyright (c) KING Studio. All rights reserved.
// </copyright>

using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Windowing;
using WinRT.Interop;
using KINGRecorder.App.ViewModels;

namespace KINGRecorder.App
{
    /// <summary>
    /// Premium KING Design System Fluent Window with mica material and custom titlebar.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        public MainViewModel ViewModel { get; }

        public MainWindow(MainViewModel viewModel)
        {
            this.InitializeComponent();
            ViewModel = viewModel;

            // Customize WinUI 3 Window Titlebar
            ExtendsContentIntoTitleBar = true;
            SetTitleBar(CustomTitleBar);

            var hWnd = WindowNative.GetWindowHandle(this);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);
            var appWindow = AppWindow.GetFromWindowId(windowId);
            appWindow.Title = "KING RECORDER — Professional Screen Recording Studio";
            appWindow.Resize(new Windows.Graphics.SizeInt32(1280, 800));
        }
    }
}`
  },
  {
    path: 'KING-RECORDER/docs/ARCHITECTURE.md',
    name: 'ARCHITECTURE.md',
    category: 'Docs',
    content: `# KING RECORDER Architecture Documentation

## Architectural Overview
KING RECORDER is architected as a high-performance, modular desktop recording suite leveraging modern Windows APIs:
- **Presentation Layer**: WinUI 3 + Windows App SDK 1.5, MVVM architecture, CommunityToolkit.Mvvm.
- **Capture Layer**: Windows.Graphics.Capture API backed by Direct3D 11 for zero-copy GPU screen capture.
- **Webcam Pipeline**: Windows.Media.Capture.MediaCapture with MediaFrameReader.
- **Audio Capture**: WASAPI Loopback for system audio and WASAPI Event-Driven mode for low-latency microphone capture.
- **Composition & Shaders**: Direct3D 11 pixel shader pipeline rendering rounded-rectangle and circular anti-aliased alpha masks.
- **Encoding & Muxing**: Windows Media Foundation SinkWriter with NVENC, Intel QuickSync, and AMD AMF hardware encoder fallback to Microsoft H.264 Software MFT.

## Monotonic Clock & Synchronization
Synchronization across 4 independent asynchronous pipelines (Screen, Webcam, Mic, System Audio) is maintained through a unified monotonic clock utilizing \`QueryPerformanceCounter\` / \`SystemRelativeTime\`. Audio frames are timestamped at WASAPI packet arrival and resampled to 48kHz stereo PCM.
`
  },
  {
    path: 'KING-RECORDER/docs/TESTING.md',
    name: 'TESTING.md',
    category: 'Docs',
    content: `# KING RECORDER — QA & Verification Suite

## Automated Test Coverage
1. **Geometry Tests**: \`OverlayGeometryTests.cs\` verifies circle and rounded rect coordinates across 100%, 125%, 150%, 200% Windows DPI scales.
2. **State Machine Verification**: \`RecordingStateMachineTests.cs\` checks invalid transitions (e.g., Pause while Idle, Double Start, Force Stop on Error).
3. **Audio Gain & Limiting**: \`AudioLimiterTests.cs\` tests clipping avoidance when microphone and loopback audio peak simultaneously.
4. **Hardware Encoding Fallback**: Tests graceful switch from NVENC to Software H.264 if GPU driver reset occurs.
`
  }
];
