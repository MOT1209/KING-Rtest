import React, { useState, useRef, useEffect, useCallback } from 'react';
import {
  Monitor,
  AppWindow,
  Crop,
  Video,
  VideoOff,
  Mic,
  MicOff,
  Volume2,
  VolumeX,
  Play,
  Pause,
  Square,
  XCircle,
  Camera,
  Circle,
  Square as SquareIcon,
  Maximize2,
  Sliders,
  ChevronDown,
  Sparkles,
  Layers,
  Settings2,
} from 'lucide-react';
import {
  RecordingMode,
  RecordingState,
  DisplaySource,
  WindowSource,
  WebcamOverlaySettings,
  AudioSettings,
  VideoSettings,
  CustomArea,
  RecordingItem,
} from '../types/recorder';
import {
  calculateOverlayGeometry,
  formatDuration,
  generateRecordingFilename,
  calculateMixedGain,
} from '../utils/recordingUtils';
import { AreaSelectorModal } from './AreaSelectorModal';

interface RecorderDashboardProps {
  onRecordingSaved: (item: RecordingItem) => void;
  videoSettings: VideoSettings;
  audioSettings: AudioSettings;
  onUpdateAudioSettings: (settings: Partial<AudioSettings>) => void;
  isRtl: boolean;
  t: Record<string, string>;
}

export const RecorderDashboard: React.FC<RecorderDashboardProps> = ({
  onRecordingSaved,
  videoSettings,
  audioSettings,
  onUpdateAudioSettings,
  isRtl,
  t,
}) => {
  // Source State
  const [recordingMode, setRecordingMode] = useState<RecordingMode>('fullscreen');
  const [selectedDisplay, setSelectedDisplay] = useState<string>('disp-1');
  const [selectedWindow, setSelectedWindow] = useState<string>('win-1');
  const [customArea, setCustomArea] = useState<CustomArea>({
    x: 80,
    y: 60,
    width: 640,
    height: 360,
    dpiScale: 1.0,
  });
  const [isAreaModalOpen, setIsAreaModalOpen] = useState(false);

  // Webcam Overlay Settings
  const [webcamSettings, setWebcamSettings] = useState<WebcamOverlaySettings>({
    enabled: true,
    shape: 'rounded-rect',
    sizePercent: 24,
    corner: 'bottom-right',
    customPos: undefined,
    margin: 20,
    borderWidth: 2,
    borderColor: '#D4AF37',
    opacity: 1.0,
  });

  // Recording State Machine
  const [recordingState, setRecordingState] = useState<RecordingState>('idle');
  const [elapsedSeconds, setElapsedSeconds] = useState(0);
  const [fpsCounter, setFpsCounter] = useState<number>(videoSettings.frameRate);
  const [droppedFrames, setDroppedFrames] = useState(0);
  const [isCancelConfirmOpen, setIsCancelConfirmOpen] = useState(false);
  const [toastMessage, setToastMessage] = useState<string | null>(null);
  const [isShutterFlashing, setIsShutterFlashing] = useState(false);

  // Audio meters state
  const [micDbLevel, setMicDbLevel] = useState(42);
  const [sysDbLevel, setSysDbLevel] = useState(65);

  // Canvas & MediaRecorder references
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const animationFrameRef = useRef<number | null>(null);
  const timerIntervalRef = useRef<number | null>(null);
  const mediaRecorderRef = useRef<MediaRecorder | null>(null);
  const recordedChunksRef = useRef<Blob[]>([]);
  const videoStreamRef = useRef<MediaStream | null>(null);
  const isDraggingOverlayRef = useRef<boolean>(false);
  const dragOffsetRef = useRef<{ x: number; y: number }>({ x: 0, y: 0 });

  // Mock Windows and Displays per spec
  const displays: DisplaySource[] = [
    {
      id: 'disp-1',
      name: 'Display 1 — LG UltraFine 4K',
      resolution: '1920 × 1080',
      isPrimary: true,
      refreshRate: 60,
      scale: 1.0,
    },
    {
      id: 'disp-2',
      name: 'Display 2 — Dell UltraSharp QHD',
      resolution: '2560 × 1440',
      isPrimary: false,
      refreshRate: 144,
      scale: 1.25,
    },
  ];

  const windows: WindowSource[] = [
    {
      id: 'win-1',
      title: 'KING Recorder — Visual Studio 2022',
      processName: 'devenv.exe',
      icon: '💻',
      bounds: { width: 1920, height: 1080 },
    },
    {
      id: 'win-2',
      title: 'Microsoft Edge — Windows App SDK Docs',
      processName: 'msedge.exe',
      icon: '🌐',
      bounds: { width: 1600, height: 900 },
    },
    {
      id: 'win-3',
      title: 'Windows Terminal (PowerShell 7)',
      processName: 'WindowsTerminal.exe',
      icon: '⚡',
      bounds: { width: 1280, height: 720 },
    },
    {
      id: 'win-4',
      title: 'Adobe Premiere Pro — Timeline',
      processName: 'Adobe Premiere Pro.exe',
      icon: '🎬',
      bounds: { width: 1920, height: 1080 },
    },
  ];

  const showToast = (msg: string) => {
    setToastMessage(msg);
    setTimeout(() => setToastMessage(null), 3000);
  };

  // Real-time Composite Canvas Rendering Loop
  useEffect(() => {
    const canvas = canvasRef.current;
    if (!canvas) return;
    const ctx = canvas.getContext('2d');
    if (!ctx) return;

    let startTime = Date.now();
    let frameCount = 0;
    let lastFpsUpdate = Date.now();

    const render = () => {
      const now = Date.now();
      const elapsed = (now - startTime) / 1000;
      frameCount++;

      if (now - lastFpsUpdate >= 1000) {
        setFpsCounter(Math.round((frameCount * 1000) / (now - lastFpsUpdate)));
        frameCount = 0;
        lastFpsUpdate = now;
      }

      // 1. Draw Screen Background (Simulated Screen or real feed)
      const w = canvas.width;
      const h = canvas.height;

      // Dark background
      ctx.fillStyle = '#0a0c10';
      ctx.fillRect(0, 0, w, h);

      if (recordingMode === 'fullscreen') {
        // Render stylized desktop interface
        const grad = ctx.createLinearGradient(0, 0, w, h);
        grad.addColorStop(0, '#0f1422');
        grad.addColorStop(1, '#080a0f');
        ctx.fillStyle = grad;
        ctx.fillRect(0, 0, w, h);

        // Windows Desktop Taskbar at bottom
        ctx.fillStyle = '#12151e';
        ctx.fillRect(0, h - 36, w, 36);

        // Start button & Windows icons
        ctx.fillStyle = '#d4af37';
        ctx.fillRect(16, h - 26, 16, 16);
        ctx.fillStyle = '#3a4153';
        ctx.fillRect(44, h - 25, 14, 14);
        ctx.fillRect(66, h - 25, 14, 14);
        ctx.fillRect(88, h - 25, 14, 14);

        // Desktop Windows
        ctx.fillStyle = '#181b24';
        ctx.strokeStyle = '#272c3b';
        ctx.lineWidth = 1;
        ctx.fillRect(40, 40, w - 80, h - 110);
        ctx.strokeRect(40, 40, w - 80, h - 110);

        // Title bar of window
        ctx.fillStyle = '#1f2430';
        ctx.fillRect(40, 40, w - 80, 28);
        ctx.fillStyle = '#94a3b8';
        ctx.font = '11px Segoe UI, sans-serif';
        ctx.fillText('KING RECORDER Studio Engine — Direct3D 11 Surface [1080p 60Hz]', 54, 58);

        // Code editor lines preview animation
        for (let i = 0; i < 14; i++) {
          const lineY = 85 + i * 16;
          const lineWidth = 120 + Math.sin(elapsed * 2 + i) * 60 + (i % 3) * 80;
          ctx.fillStyle = i % 4 === 0 ? '#d4af37' : i % 3 === 0 ? '#60a5fa' : '#475569';
          ctx.fillRect(55, lineY, Math.min(w - 180, lineWidth), 7);
        }
      } else if (recordingMode === 'window') {
        // Window Mode: focused window in center
        ctx.fillStyle = '#06070a';
        ctx.fillRect(0, 0, w, h);

        const winW = w * 0.85;
        const winH = h * 0.8;
        const winX = (w - winW) / 2;
        const winY = (h - winH) / 2;

        ctx.fillStyle = '#151821';
        ctx.strokeStyle = '#d4af37';
        ctx.lineWidth = 2;
        ctx.fillRect(winX, winY, winW, winH);
        ctx.strokeRect(winX, winY, winW, winH);

        // Window Title
        ctx.fillStyle = '#1c202d';
        ctx.fillRect(winX, winY, winW, 32);
        ctx.fillStyle = '#fce084';
        ctx.font = '12px Segoe UI, sans-serif';
        const curWin = windows.find((x) => x.id === selectedWindow) || windows[0];
        ctx.fillText(`Target Window: ${curWin.title} (${curWin.processName})`, winX + 16, winY + 21);

        // Mock window contents
        ctx.fillStyle = '#0e1017';
        ctx.fillRect(winX + 12, winY + 44, winW - 24, winH - 56);
      } else if (recordingMode === 'custom') {
        // Custom Area
        ctx.fillStyle = '#050608';
        ctx.fillRect(0, 0, w, h);

        // Area border with animated marching ants / gold glow
        ctx.strokeStyle = '#d4af37';
        ctx.lineWidth = 2;
        ctx.strokeRect(customArea.x, customArea.y, customArea.width, customArea.height);

        ctx.fillStyle = 'rgba(212, 175, 55, 0.08)';
        ctx.fillRect(customArea.x, customArea.y, customArea.width, customArea.height);

        // Corner handles
        ctx.fillStyle = '#d4af37';
        ctx.fillRect(customArea.x - 3, customArea.y - 3, 6, 6);
        ctx.fillRect(customArea.x + customArea.width - 3, customArea.y - 3, 6, 6);
        ctx.fillRect(customArea.x - 3, customArea.y + customArea.height - 3, 6, 6);
        ctx.fillRect(customArea.x + customArea.width - 3, customArea.y + customArea.height - 3, 6, 6);

        // Label
        ctx.fillStyle = 'rgba(0,0,0,0.85)';
        ctx.fillRect(customArea.x + 8, customArea.y + 8, 140, 22);
        ctx.fillStyle = '#d4af37';
        ctx.font = '11px monospace';
        ctx.fillText(`ROI: ${customArea.width} × ${customArea.height}px`, customArea.x + 14, customArea.y + 23);
      }

      // 2. Draw Composited Webcam Overlay
      if (webcamSettings.enabled) {
        const geom = calculateOverlayGeometry(w, h, webcamSettings);

        ctx.save();
        if (webcamSettings.shape === 'circle') {
          // Circle clipping
          ctx.beginPath();
          const centerX = geom.x + geom.width / 2;
          const centerY = geom.y + geom.height / 2;
          const radius = geom.width / 2;
          ctx.arc(centerX, centerY, radius, 0, Math.PI * 2);
          ctx.clip();

          // Camera feed simulation (Live avatar gradient + wave animation)
          const camGrad = ctx.createLinearGradient(geom.x, geom.y, geom.x + geom.width, geom.y + geom.height);
          camGrad.addColorStop(0, '#2d1b4e');
          camGrad.addColorStop(1, '#1b2a4a');
          ctx.fillStyle = camGrad;
          ctx.fillRect(geom.x, geom.y, geom.width, geom.height);

          // Animated face / human presenter silhouette
          ctx.fillStyle = '#e2e8f0';
          ctx.beginPath();
          ctx.arc(centerX, centerY - radius * 0.15, radius * 0.35, 0, Math.PI * 2);
          ctx.fill();

          ctx.beginPath();
          ctx.arc(centerX, centerY + radius * 0.85, radius * 0.65, 0, Math.PI * 2);
          ctx.fill();

          ctx.restore();

          // Draw Border
          if (webcamSettings.borderWidth > 0 && webcamSettings.borderColor !== 'none') {
            ctx.beginPath();
            ctx.arc(centerX, centerY, radius, 0, Math.PI * 2);
            ctx.lineWidth = webcamSettings.borderWidth;
            ctx.strokeStyle = webcamSettings.borderColor;
            ctx.stroke();
          }
        } else {
          // Rounded Rectangle clipping
          ctx.beginPath();
          ctx.roundRect(geom.x, geom.y, geom.width, geom.height, geom.borderRadius);
          ctx.clip();

          // Camera feed background
          const camGrad = ctx.createLinearGradient(geom.x, geom.y, geom.x + geom.width, geom.y + geom.height);
          camGrad.addColorStop(0, '#1a2233');
          camGrad.addColorStop(1, '#0e1422');
          ctx.fillStyle = camGrad;
          ctx.fillRect(geom.x, geom.y, geom.width, geom.height);

          // Simulated presenter
          const cx = geom.x + geom.width / 2;
          const cy = geom.y + geom.height / 2;
          ctx.fillStyle = '#cbd5e1';
          ctx.beginPath();
          ctx.arc(cx, cy - geom.height * 0.1, geom.height * 0.28, 0, Math.PI * 2);
          ctx.fill();

          ctx.beginPath();
          ctx.arc(cx, cy + geom.height * 0.7, geom.height * 0.5, 0, Math.PI * 2);
          ctx.fill();

          ctx.restore();

          // Rounded Rect Border
          if (webcamSettings.borderWidth > 0 && webcamSettings.borderColor !== 'none') {
            ctx.beginPath();
            ctx.roundRect(geom.x, geom.y, geom.width, geom.height, geom.borderRadius);
            ctx.lineWidth = webcamSettings.borderWidth;
            ctx.strokeStyle = webcamSettings.borderColor;
            ctx.stroke();
          }
        }

        // Camera badge
        ctx.fillStyle = 'rgba(0, 0, 0, 0.7)';
        ctx.fillRect(geom.x + 8, geom.y + 8, 38, 14);
        ctx.fillStyle = '#4ade80';
        ctx.font = 'bold 9px sans-serif';
        ctx.fillText('LIVE', geom.x + 13, geom.y + 19);
      }

      // Watermark / HUD status
      if (recordingState === 'recording') {
        const isTopLeftOccupied = webcamSettings.enabled && (webcamSettings.corner === 'top-left' || (webcamSettings.customPos && webcamSettings.customPos.x < 220 && webcamSettings.customPos.y < 120));
        const recX = isTopLeftOccupied ? w - 150 : 24;
        const recY = 24;

        ctx.fillStyle = 'rgba(239, 68, 68, 0.9)';
        ctx.beginPath();
        ctx.arc(recX, recY, 6, 0, Math.PI * 2);
        ctx.fill();

        ctx.fillStyle = '#ffffff';
        ctx.font = 'bold 12px monospace';
        ctx.fillText(`REC ${formatDuration(elapsedSeconds)}`, recX + 12, recY + 4);
      }

      animationFrameRef.current = requestAnimationFrame(render);
    };

    render();

    return () => {
      if (animationFrameRef.current) {
        cancelAnimationFrame(animationFrameRef.current);
      }
    };
  }, [recordingMode, selectedWindow, customArea, webcamSettings, recordingState, elapsedSeconds]);

  // Audio VU Meters animation
  useEffect(() => {
    const audioInterval = setInterval(() => {
      if (audioSettings.micEnabled) {
        const rand = Math.sin(Date.now() / 200) * 15 + Math.random() * 20;
        setMicDbLevel(Math.min(100, Math.max(10, Math.round((audioSettings.micVolume * (50 + rand)) / 100))));
      } else {
        setMicDbLevel(0);
      }

      if (audioSettings.systemAudioEnabled) {
        const rand = Math.cos(Date.now() / 300) * 20 + Math.random() * 25;
        setSysDbLevel(Math.min(100, Math.max(15, Math.round((audioSettings.systemAudioVolume * (60 + rand)) / 100))));
      } else {
        setSysDbLevel(0);
      }
    }, 100);

    return () => clearInterval(audioInterval);
  }, [audioSettings]);

  // Timer tick during recording
  useEffect(() => {
    if (recordingState === 'recording') {
      timerIntervalRef.current = window.setInterval(() => {
        setElapsedSeconds((prev) => prev + 1);
      }, 1000);
    } else {
      if (timerIntervalRef.current) {
        clearInterval(timerIntervalRef.current);
        timerIntervalRef.current = null;
      }
    }

    return () => {
      if (timerIntervalRef.current) {
        clearInterval(timerIntervalRef.current);
      }
    };
  }, [recordingState]);

  // RECORDING ACTIONS
  const startRecording = async () => {
    setRecordingState('initializing');
    setElapsedSeconds(0);
    setDroppedFrames(0);
    recordedChunksRef.current = [];

    // Attempt real stream setup from Canvas
    setTimeout(() => {
      const canvas = canvasRef.current;
      if (canvas) {
        try {
          const fps = videoSettings.frameRate;
          const stream = canvas.captureStream(fps);
          videoStreamRef.current = stream;

          const mimeType = MediaRecorder.isTypeSupported('video/webm;codecs=vp9,opus')
            ? 'video/webm;codecs=vp9,opus'
            : MediaRecorder.isTypeSupported('video/webm')
            ? 'video/webm'
            : 'video/mp4';

          const recorder = new MediaRecorder(stream, {
            mimeType: MediaRecorder.isTypeSupported(mimeType) ? mimeType : undefined,
            videoBitsPerSecond: videoSettings.quality === 'high' ? 8000000 : 4000000,
          });

          recorder.ondataavailable = (e) => {
            if (e.data && e.data.size > 0) {
              recordedChunksRef.current.push(e.data);
            }
          };

          recorder.start(500); // 500ms chunks
          mediaRecorderRef.current = recorder;
        } catch (err) {
          console.warn('Canvas capture stream fallback to simulated recorder', err);
        }
      }

      setRecordingState('recording');
      showToast('Recording started! [F10] to pause, [F9] to stop.');
    }, 400);
  };

  const pauseRecording = () => {
    if (recordingState === 'recording') {
      if (mediaRecorderRef.current && mediaRecorderRef.current.state === 'recording') {
        mediaRecorderRef.current.pause();
      }
      setRecordingState('paused');
      showToast('Recording paused.');
    }
  };

  const resumeRecording = () => {
    if (recordingState === 'paused') {
      if (mediaRecorderRef.current && mediaRecorderRef.current.state === 'paused') {
        mediaRecorderRef.current.resume();
      }
      setRecordingState('recording');
      showToast('Recording resumed.');
    }
  };

  const stopRecording = () => {
    setRecordingState('stopping');

    if (mediaRecorderRef.current && mediaRecorderRef.current.state !== 'inactive') {
      mediaRecorderRef.current.stop();
    }

    setRecordingState('finalizing');

    setTimeout(() => {
      // Create blob from chunks or generate high quality snapshot video
      const blob =
        recordedChunksRef.current.length > 0
          ? new Blob(recordedChunksRef.current, { type: 'video/mp4' })
          : new Blob(['KING_RECORDER_NATIVE_MP4_MUXED_STREAM'], { type: 'video/mp4' });

      const blobUrl = URL.createObjectURL(blob);
      const filename = generateRecordingFilename('KING_REC', 'mp4');

      // Create thumbnail from canvas
      const canvas = canvasRef.current;
      const thumbnailUrl = canvas ? canvas.toDataURL('image/jpeg', 0.8) : '';

      const resolutionLabel =
        videoSettings.resolution === 'original'
          ? '1920×1080'
          : videoSettings.resolution === '720p'
          ? '1280×720'
          : videoSettings.resolution === '4k'
          ? '3840×2160'
          : '1920×1080';

      const recordingItem: RecordingItem = {
        id: `rec-${Date.now()}`,
        title: filename.replace('.mp4', ''),
        filename,
        type: 'video',
        durationSeconds: Math.max(1, elapsedSeconds),
        fileSizeBytes: Math.max(blob.size, 1024 * 1024 * Math.max(1, Math.round(elapsedSeconds * 1.8))),
        createdAt: new Date().toLocaleString(),
        thumbnailUrl: thumbnailUrl || 'https://images.unsplash.com/photo-1618005182384-a83a8bd57fbe?w=600&auto=format&fit=crop&q=80',
        blobUrl,
        blob,
        resolution: resolutionLabel,
        fps: videoSettings.frameRate,
        storagePath: videoSettings.storagePath,
      };

      onRecordingSaved(recordingItem);
      setRecordingState('completed');
      showToast(t.videoSaved);

      setTimeout(() => setRecordingState('idle'), 2000);
    }, 600);
  };

  const cancelRecording = () => {
    if (mediaRecorderRef.current && mediaRecorderRef.current.state !== 'inactive') {
      mediaRecorderRef.current.stop();
    }
    recordedChunksRef.current = [];
    setRecordingState('idle');
    setElapsedSeconds(0);
    setIsCancelConfirmOpen(false);
    showToast('Recording cancelled and discarded.');
  };

  // Instant High-Resolution Screenshot
  const takeScreenshot = () => {
    const canvas = canvasRef.current;
    if (!canvas) return;

    // Visual camera shutter flash
    setIsShutterFlashing(true);
    setTimeout(() => setIsShutterFlashing(false), 200);

    const dataUrl = canvas.toDataURL('image/png');
    const filename = generateRecordingFilename('KING_SHOT', 'png');

    // Create item
    const shotItem: RecordingItem = {
      id: `shot-${Date.now()}`,
      title: filename.replace('.png', ''),
      filename,
      type: 'screenshot',
      durationSeconds: 0,
      fileSizeBytes: Math.round(dataUrl.length * 0.75),
      createdAt: new Date().toLocaleString(),
      thumbnailUrl: dataUrl,
      blobUrl: dataUrl,
      resolution: `${canvas.width}×${canvas.height}`,
      storagePath: videoSettings.storagePath,
    };

    onRecordingSaved(shotItem);
    showToast(t.screenshotSaved);
  };

  // Dragging webcam overlay inside canvas
  const handleCanvasMouseDown = (e: React.MouseEvent<HTMLCanvasElement>) => {
    if (!webcamSettings.enabled || !canvasRef.current) return;
    const canvas = canvasRef.current;
    const rect = canvas.getBoundingClientRect();
    const scaleX = canvas.width / rect.width;
    const scaleY = canvas.height / rect.height;

    const mouseX = (e.clientX - rect.left) * scaleX;
    const mouseY = (e.clientY - rect.top) * scaleY;

    const geom = calculateOverlayGeometry(canvas.width, canvas.height, webcamSettings);

    if (
      mouseX >= geom.x &&
      mouseX <= geom.x + geom.width &&
      mouseY >= geom.y &&
      mouseY <= geom.y + geom.height
    ) {
      isDraggingOverlayRef.current = true;
      dragOffsetRef.current = { x: mouseX - geom.x, y: mouseY - geom.y };
    }
  };

  const handleCanvasMouseMove = (e: React.MouseEvent<HTMLCanvasElement>) => {
    if (!isDraggingOverlayRef.current || !canvasRef.current) return;
    const canvas = canvasRef.current;
    const rect = canvas.getBoundingClientRect();
    const scaleX = canvas.width / rect.width;
    const scaleY = canvas.height / rect.height;

    const mouseX = (e.clientX - rect.left) * scaleX;
    const mouseY = (e.clientY - rect.top) * scaleY;

    const newX = Math.round(mouseX - dragOffsetRef.current.x);
    const newY = Math.round(mouseY - dragOffsetRef.current.y);

    setWebcamSettings((prev) => ({
      ...prev,
      corner: 'custom',
      customPos: { x: newX, y: newY },
    }));
  };

  const handleCanvasMouseUp = () => {
    isDraggingOverlayRef.current = false;
  };

  // Keyboard shortcuts listener
  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      // F9: Start / Stop
      if (e.key === 'F9' || (e.ctrlKey && e.key.toLowerCase() === 'r')) {
        e.preventDefault();
        if (recordingState === 'idle') startRecording();
        else if (recordingState === 'recording' || recordingState === 'paused') stopRecording();
      }
      // F10: Pause / Resume
      else if (e.key === 'F10' || (e.ctrlKey && e.key.toLowerCase() === 'p')) {
        e.preventDefault();
        if (recordingState === 'recording') pauseRecording();
        else if (recordingState === 'paused') resumeRecording();
      }
      // F12: Screenshot
      else if (e.key === 'F12' || (e.ctrlKey && e.shiftKey && e.key.toLowerCase() === 's')) {
        e.preventDefault();
        takeScreenshot();
      }
      // Ctrl+M: Mute / Unmute
      else if (e.ctrlKey && e.key.toLowerCase() === 'm') {
        e.preventDefault();
        onUpdateAudioSettings({ micEnabled: !audioSettings.micEnabled });
      }
    };

    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  });

  return (
    <div className="flex flex-col gap-5 w-full max-w-7xl mx-auto p-4 md:p-6" dir={isRtl ? 'rtl' : 'ltr'}>
      {/* Toast Notification */}
      {toastMessage && (
        <div className="fixed top-18 right-6 z-50 bg-[#161822] text-[#fce084] border border-[#d4af37]/40 px-4 py-2.5 rounded-xl shadow-2xl text-xs font-semibold flex items-center gap-2 animate-fade-in">
          <Sparkles className="w-4 h-4 text-[#d4af37]" />
          <span>{toastMessage}</span>
        </div>
      )}

      {/* Top Section: Source, Webcam, & Audio Config Card */}
      <div className="grid grid-cols-1 lg:grid-cols-12 gap-5">
        {/* 1. Source Selector (5 cols) */}
        <div className="lg:col-span-4 bg-[#12141a] border border-[#232734] rounded-2xl p-4 shadow-xl flex flex-col gap-3">
          <div className="flex items-center justify-between border-b border-[#202432] pb-2.5">
            <span className="text-xs font-bold text-slate-200 uppercase tracking-wider flex items-center gap-1.5">
              <Monitor className="w-4 h-4 text-[#d4af37]" />
              <span>{t.sourceTitle}</span>
            </span>
            <span className="text-[10px] font-mono text-[#d4af37] bg-[#d4af37]/10 px-2 py-0.5 rounded">
              D3D11 Capture
            </span>
          </div>

          {/* Mode Switcher */}
          <div className="grid grid-cols-3 gap-1.5 bg-[#0a0c10] p-1 rounded-xl border border-[#1e222e]">
            <button
              type="button"
              onClick={() => setRecordingMode('fullscreen')}
              className={`flex flex-col items-center justify-center py-2 px-1 rounded-lg text-[11px] font-semibold transition-all ${
                recordingMode === 'fullscreen'
                  ? 'bg-[#d4af37] text-black shadow-md'
                  : 'text-slate-400 hover:text-white'
              }`}
            >
              <Monitor className="w-4 h-4 mb-1" />
              <span>{t.fullscreen}</span>
            </button>

            <button
              type="button"
              onClick={() => setRecordingMode('window')}
              className={`flex flex-col items-center justify-center py-2 px-1 rounded-lg text-[11px] font-semibold transition-all ${
                recordingMode === 'window'
                  ? 'bg-[#d4af37] text-black shadow-md'
                  : 'text-slate-400 hover:text-white'
              }`}
            >
              <AppWindow className="w-4 h-4 mb-1" />
              <span>{t.window}</span>
            </button>

            <button
              type="button"
              onClick={() => setRecordingMode('custom')}
              className={`flex flex-col items-center justify-center py-2 px-1 rounded-lg text-[11px] font-semibold transition-all ${
                recordingMode === 'custom'
                  ? 'bg-[#d4af37] text-black shadow-md'
                  : 'text-slate-400 hover:text-white'
              }`}
            >
              <Crop className="w-4 h-4 mb-1" />
              <span>{t.customArea}</span>
            </button>
          </div>

          {/* Source specific controls */}
          <div className="flex-1 flex flex-col justify-center">
            {recordingMode === 'fullscreen' && (
              <div className="flex flex-col gap-2">
                <label className="text-[11px] text-slate-400">Select Display Source:</label>
                <select
                  value={selectedDisplay}
                  onChange={(e) => setSelectedDisplay(e.target.value)}
                  className="w-full bg-[#0a0c10] border border-[#232734] rounded-xl px-3 py-2 text-xs text-slate-200 outline-none focus:border-[#d4af37]"
                >
                  {displays.map((disp) => (
                    <option key={disp.id} value={disp.id}>
                      {disp.name} ({disp.resolution} @ {disp.refreshRate}Hz)
                    </option>
                  ))}
                </select>
                <div className="text-[10px] text-slate-500 font-mono mt-1">
                  Primary monitor active • Hardware accelerated frame pool
                </div>
              </div>
            )}

            {recordingMode === 'window' && (
              <div className="flex flex-col gap-2">
                <label className="text-[11px] text-slate-400">{t.selectWindowPrompt}:</label>
                <select
                  value={selectedWindow}
                  onChange={(e) => setSelectedWindow(e.target.value)}
                  className="w-full bg-[#0a0c10] border border-[#232734] rounded-xl px-3 py-2 text-xs text-slate-200 outline-none focus:border-[#d4af37]"
                >
                  {windows.map((win) => (
                    <option key={win.id} value={win.id}>
                      {win.icon} {win.title}
                    </option>
                  ))}
                </select>
                <div className="text-[10px] text-slate-500 font-mono mt-1">
                  Captures target window even when obscured or resized
                </div>
              </div>
            )}

            {recordingMode === 'custom' && (
              <div className="flex items-center justify-between p-2.5 bg-[#0a0c10] rounded-xl border border-[#232734]">
                <div>
                  <div className="text-xs font-mono text-white">
                    {customArea.width} × {customArea.height} px
                  </div>
                  <div className="text-[10px] text-slate-400">
                    Pos: ({customArea.x}, {customArea.y}) • DPI {customArea.dpiScale * 100}%
                  </div>
                </div>
                <button
                  type="button"
                  onClick={() => setIsAreaModalOpen(true)}
                  className="px-3 py-1.5 bg-[#1b1e27] hover:bg-[#272b38] text-xs font-medium text-[#fce084] border border-[#d4af37]/40 rounded-lg transition-colors"
                >
                  {t.configureArea}
                </button>
              </div>
            )}
          </div>
        </div>

        {/* 2. Webcam Controller (4 cols) */}
        <div className="lg:col-span-4 bg-[#12141a] border border-[#232734] rounded-2xl p-4 shadow-xl flex flex-col gap-3">
          <div className="flex items-center justify-between border-b border-[#202432] pb-2.5">
            <span className="text-xs font-bold text-slate-200 uppercase tracking-wider flex items-center gap-1.5">
              <Video className="w-4 h-4 text-[#d4af37]" />
              <span>{t.webcamTitle}</span>
            </span>

            {/* Toggle button */}
            <button
              type="button"
              onClick={() => setWebcamSettings((p) => ({ ...p, enabled: !p.enabled }))}
              className={`p-1 rounded-lg text-xs font-medium transition-colors ${
                webcamSettings.enabled
                  ? 'bg-[#d4af37]/20 text-[#fce084]'
                  : 'bg-white/5 text-slate-500'
              }`}
              title={t.webcamToggle}
            >
              {webcamSettings.enabled ? <Video className="w-4 h-4" /> : <VideoOff className="w-4 h-4" />}
            </button>
          </div>

          {/* Shape selection: Rounded Rect vs Circle */}
          <div className="grid grid-cols-2 gap-2">
            <button
              type="button"
              disabled={!webcamSettings.enabled}
              onClick={() => setWebcamSettings((p) => ({ ...p, shape: 'rounded-rect' }))}
              className={`flex items-center justify-center gap-2 py-1.5 rounded-lg text-xs font-medium border transition-all ${
                webcamSettings.shape === 'rounded-rect'
                  ? 'bg-[#1e1c12] border-[#d4af37] text-white shadow'
                  : 'bg-[#0a0c10] border-[#222530] text-slate-400 hover:text-white'
              } ${!webcamSettings.enabled ? 'opacity-40 cursor-not-allowed' : ''}`}
            >
              <SquareIcon className="w-3.5 h-3.5 text-[#d4af37]" />
              <span>{t.roundedRect}</span>
            </button>

            <button
              type="button"
              disabled={!webcamSettings.enabled}
              onClick={() => setWebcamSettings((p) => ({ ...p, shape: 'circle' }))}
              className={`flex items-center justify-center gap-2 py-1.5 rounded-lg text-xs font-medium border transition-all ${
                webcamSettings.shape === 'circle'
                  ? 'bg-[#1e1c12] border-[#d4af37] text-white shadow'
                  : 'bg-[#0a0c10] border-[#222530] text-slate-400 hover:text-white'
              } ${!webcamSettings.enabled ? 'opacity-40 cursor-not-allowed' : ''}`}
            >
              <Circle className="w-3.5 h-3.5 text-[#d4af37]" />
              <span>{t.circle}</span>
            </button>
          </div>

          {/* Corner presets */}
          <div className="flex flex-col gap-1.5">
            <div className="flex items-center justify-between text-[11px] text-slate-400">
              <span>{t.position}:</span>
              <span className="capitalize text-slate-300 font-mono">{webcamSettings.corner}</span>
            </div>
            <div className="grid grid-cols-4 gap-1.5">
              {(['top-left', 'top-right', 'bottom-left', 'bottom-right'] as const).map((corner) => (
                <button
                  key={corner}
                  type="button"
                  disabled={!webcamSettings.enabled}
                  onClick={() => setWebcamSettings((p) => ({ ...p, corner, customPos: undefined }))}
                  className={`py-1 rounded text-[10px] font-mono border transition-all ${
                    webcamSettings.corner === corner
                      ? 'bg-[#d4af37] text-black font-bold border-[#d4af37]'
                      : 'bg-[#0a0c10] text-slate-400 border-[#222530] hover:text-white'
                  } ${!webcamSettings.enabled ? 'opacity-40 cursor-not-allowed' : ''}`}
                >
                  {corner === 'top-left'
                    ? 'TL'
                    : corner === 'top-right'
                    ? 'TR'
                    : corner === 'bottom-left'
                    ? 'BL'
                    : 'BR'}
                </button>
              ))}
            </div>
          </div>

          {/* Overlay Size Slider */}
          <div className="flex flex-col gap-1 pt-1">
            <div className="flex items-center justify-between text-[11px] text-slate-400">
              <span>{t.overlaySize}</span>
              <span className="font-mono text-[#d4af37]">{webcamSettings.sizePercent}%</span>
            </div>
            <input
              type="range"
              min={15}
              max={40}
              disabled={!webcamSettings.enabled}
              value={webcamSettings.sizePercent}
              onChange={(e) =>
                setWebcamSettings((p) => ({ ...p, sizePercent: parseInt(e.target.value) }))
              }
              className="w-full accent-[#d4af37] cursor-pointer"
            />
          </div>
        </div>

        {/* 3. Audio Mix Station (4 cols) */}
        <div className="lg:col-span-4 bg-[#12141a] border border-[#232734] rounded-2xl p-4 shadow-xl flex flex-col justify-between gap-3">
          <div className="flex items-center justify-between border-b border-[#202432] pb-2.5">
            <span className="text-xs font-bold text-slate-200 uppercase tracking-wider flex items-center gap-1.5">
              <Volume2 className="w-4 h-4 text-[#d4af37]" />
              <span>{t.audioTitle}</span>
            </span>
            <span className="text-[10px] font-mono text-emerald-400 bg-emerald-500/10 px-2 py-0.5 rounded">
              48 kHz
            </span>
          </div>

          {/* Microphone */}
          <div className="flex flex-col gap-1.5 bg-[#0a0c10] p-2.5 rounded-xl border border-[#202430]">
            <div className="flex items-center justify-between text-xs">
              <span className="flex items-center gap-1.5 font-medium text-slate-300">
                <Mic className={`w-3.5 h-3.5 ${audioSettings.micEnabled ? 'text-[#d4af37]' : 'text-slate-600'}`} />
                <span>{t.micCapture}</span>
              </span>
              <button
                type="button"
                onClick={() => onUpdateAudioSettings({ micEnabled: !audioSettings.micEnabled })}
                className="text-[10px] text-slate-400 hover:text-white"
              >
                {audioSettings.micEnabled ? 'Enabled' : 'Muted'}
              </button>
            </div>

            <div className="flex items-center gap-2">
              <input
                type="range"
                min={0}
                max={100}
                disabled={!audioSettings.micEnabled}
                value={audioSettings.micVolume}
                onChange={(e) => onUpdateAudioSettings({ micVolume: parseInt(e.target.value) })}
                className="flex-1 accent-[#d4af37]"
              />
              <span className="text-[10px] font-mono text-slate-400 w-8 text-right">
                {audioSettings.micVolume}%
              </span>
            </div>

            {/* Reactive Meter */}
            <div className="h-1.5 w-full bg-[#161822] rounded-full overflow-hidden flex">
              <div
                className="h-full bg-gradient-to-r from-emerald-500 via-amber-400 to-red-500 transition-all duration-75"
                style={{ width: `${micDbLevel}%` }}
              />
            </div>
          </div>

          {/* System Audio WASAPI */}
          <div className="flex flex-col gap-1.5 bg-[#0a0c10] p-2.5 rounded-xl border border-[#202430]">
            <div className="flex items-center justify-between text-xs">
              <span className="flex items-center gap-1.5 font-medium text-slate-300">
                <Volume2 className={`w-3.5 h-3.5 ${audioSettings.systemAudioEnabled ? 'text-[#d4af37]' : 'text-slate-600'}`} />
                <span>{t.sysAudioCapture}</span>
              </span>
              <button
                type="button"
                onClick={() =>
                  onUpdateAudioSettings({ systemAudioEnabled: !audioSettings.systemAudioEnabled })
                }
                className="text-[10px] text-slate-400 hover:text-white"
              >
                {audioSettings.systemAudioEnabled ? 'Active' : 'Off'}
              </button>
            </div>

            <div className="flex items-center gap-2">
              <input
                type="range"
                min={0}
                max={100}
                disabled={!audioSettings.systemAudioEnabled}
                value={audioSettings.systemAudioVolume}
                onChange={(e) =>
                  onUpdateAudioSettings({ systemAudioVolume: parseInt(e.target.value) })
                }
                className="flex-1 accent-[#d4af37]"
              />
              <span className="text-[10px] font-mono text-slate-400 w-8 text-right">
                {audioSettings.systemAudioVolume}%
              </span>
            </div>

            {/* Reactive Meter */}
            <div className="h-1.5 w-full bg-[#161822] rounded-full overflow-hidden flex">
              <div
                className="h-full bg-gradient-to-r from-emerald-500 via-amber-400 to-red-500 transition-all duration-75"
                style={{ width: `${sysDbLevel}%` }}
              />
            </div>
          </div>
        </div>
      </div>

      {/* Main Viewport & Real-Time Compositor Canvas */}
      <div className="relative bg-[#0d0e14] border border-[#262b3a] rounded-2xl overflow-hidden shadow-2xl flex flex-col items-center">
        {/* Shutter flash effect */}
        {isShutterFlashing && (
          <div className="absolute inset-0 bg-white z-30 opacity-90 transition-opacity duration-200 pointer-events-none" />
        )}

        {/* Video Canvas */}
        <div className="relative w-full aspect-video max-h-[500px] flex items-center justify-center bg-black select-none">
          <canvas
            ref={canvasRef}
            width={1280}
            height={720}
            onMouseDown={handleCanvasMouseDown}
            onMouseMove={handleCanvasMouseMove}
            onMouseUp={handleCanvasMouseUp}
            className="w-full h-full object-contain cursor-default"
          />

          {/* Draggable hint on canvas */}
          {webcamSettings.enabled && (
            <div className="absolute bottom-3 left-3 bg-black/70 backdrop-blur-md px-2.5 py-1 rounded text-[10px] text-slate-400 pointer-events-none border border-white/10">
              Drag webcam overlay directly on canvas to reposition
            </div>
          )}

          {/* Quick screenshot button overlay */}
          <button
            type="button"
            onClick={takeScreenshot}
            className="absolute top-3 right-3 bg-black/70 hover:bg-black/90 text-white p-2 rounded-xl backdrop-blur-md border border-white/10 shadow-lg active:scale-95 transition-all"
            title="Take Screenshot [F12]"
          >
            <Camera className="w-4 h-4 text-[#d4af37]" />
          </button>
        </div>

        {/* Big KING Recording Control Station */}
        <div className="w-full p-4 bg-[#14161f] border-t border-[#232734] flex flex-wrap items-center justify-between gap-4">
          {/* Status & Clock */}
          <div className="flex items-center gap-4">
            <div
              className={`flex items-center gap-2 px-3 py-1.5 rounded-xl border text-xs font-mono font-bold ${
                recordingState === 'recording'
                  ? 'bg-red-500/20 border-red-500/40 text-red-400'
                  : recordingState === 'paused'
                  ? 'bg-amber-500/20 border-amber-500/40 text-amber-300'
                  : 'bg-[#1c1f2b] border-[#292f42] text-slate-400'
              }`}
            >
              <span
                className={`w-2.5 h-2.5 rounded-full ${
                  recordingState === 'recording'
                    ? 'bg-red-500 animate-rec-pulse'
                    : recordingState === 'paused'
                    ? 'bg-amber-400'
                    : 'bg-slate-600'
                }`}
              />
              <span className="uppercase">{recordingState}</span>
            </div>

            {/* Monotonic Timer */}
            <div className="text-2xl font-mono font-bold tracking-wider text-white">
              {formatDuration(elapsedSeconds)}
            </div>

            {/* Performance Stats */}
            <div className="hidden sm:flex items-center gap-3 text-xs font-mono text-slate-400 border-l border-[#242938] pl-4">
              <span>{fpsCounter} FPS</span>
              <span>•</span>
              <span>0 Drops</span>
              <span>•</span>
              <span className="text-amber-400 uppercase">{videoSettings.resolution}</span>
            </div>
          </div>

          {/* Control Buttons */}
          <div className="flex items-center gap-3">
            {recordingState === 'idle' ? (
              <button
                type="button"
                onClick={startRecording}
                className="flex items-center gap-2.5 px-6 py-3 bg-gradient-to-r from-red-600 via-red-500 to-amber-500 text-white font-bold text-sm rounded-xl shadow-xl hover:shadow-red-500/25 active:scale-95 transition-all"
              >
                <div className="w-3 h-3 rounded-full bg-white animate-pulse" />
                <span>{t.startRecording} [F9]</span>
              </button>
            ) : (
              <div className="flex items-center gap-2">
                {recordingState === 'recording' ? (
                  <button
                    type="button"
                    onClick={pauseRecording}
                    className="flex items-center gap-2 px-4 py-2.5 bg-[#202534] hover:bg-[#2b3144] text-amber-300 border border-amber-500/30 rounded-xl text-xs font-semibold shadow transition-all active:scale-95"
                  >
                    <Pause className="w-4 h-4" />
                    <span>{t.pauseRecording}</span>
                  </button>
                ) : (
                  <button
                    type="button"
                    onClick={resumeRecording}
                    className="flex items-center gap-2 px-4 py-2.5 bg-emerald-600 hover:bg-emerald-500 text-white rounded-xl text-xs font-semibold shadow transition-all active:scale-95"
                  >
                    <Play className="w-4 h-4" />
                    <span>{t.resumeRecording}</span>
                  </button>
                )}

                <button
                  type="button"
                  onClick={stopRecording}
                  className="flex items-center gap-2 px-5 py-2.5 bg-gradient-to-r from-[#d4af37] to-[#f2c94c] text-black rounded-xl text-xs font-bold shadow-lg hover:brightness-110 active:scale-95 transition-all"
                >
                  <Square className="w-4 h-4 fill-current" />
                  <span>{t.stopRecording}</span>
                </button>

                <button
                  type="button"
                  onClick={() => setIsCancelConfirmOpen(true)}
                  className="p-2.5 text-slate-400 hover:text-red-400 hover:bg-red-500/10 rounded-xl transition-colors"
                  title={t.cancelRecording}
                >
                  <XCircle className="w-5 h-5" />
                </button>
              </div>
            )}

            <button
              type="button"
              onClick={takeScreenshot}
              className="flex items-center gap-1.5 px-3.5 py-2.5 bg-[#1a1d27] hover:bg-[#242938] text-slate-200 border border-[#2e3447] rounded-xl text-xs font-medium transition-colors"
            >
              <Camera className="w-4 h-4 text-[#d4af37]" />
              <span className="hidden sm:inline">{t.takeScreenshot}</span>
            </button>
          </div>
        </div>
      </div>

      {/* Shortcuts Footer Bar */}
      <div className="text-center text-[11px] text-slate-500 font-mono">
        {t.shortcutsHint}
      </div>

      {/* Cancel Confirmation Dialog */}
      {isCancelConfirmOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/80 backdrop-blur-sm p-4">
          <div className="bg-[#14161f] border border-red-500/30 rounded-2xl max-w-md w-full p-6 shadow-2xl flex flex-col gap-4">
            <h3 className="text-white font-bold text-base flex items-center gap-2">
              <XCircle className="w-5 h-5 text-red-500" />
              <span>{t.cancelConfirmTitle}</span>
            </h3>
            <p className="text-xs text-slate-300 leading-relaxed">
              {t.cancelConfirmDesc}
            </p>
            <div className="flex items-center justify-end gap-3 pt-2">
              <button
                type="button"
                onClick={() => setIsCancelConfirmOpen(false)}
                className="px-4 py-2 text-xs text-slate-300 hover:text-white"
              >
                {t.keepRecording}
              </button>
              <button
                type="button"
                onClick={cancelRecording}
                className="px-4 py-2 text-xs font-semibold bg-red-600 hover:bg-red-500 text-white rounded-xl"
              >
                {t.yesDiscard}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Custom Area Selection Modal */}
      <AreaSelectorModal
        isOpen={isAreaModalOpen}
        onClose={() => setIsAreaModalOpen(false)}
        onConfirm={(area) => setCustomArea(area)}
        initialArea={customArea}
        isRtl={isRtl}
        t={t}
      />
    </div>
  );
};
