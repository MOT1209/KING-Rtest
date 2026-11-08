# KING RECORDER — Native Windows Migration Plan

## 1. Architectural Evolution

```text
CURRENT (Prototype):
Browser Sandbox (React 19 / TypeScript / Vite)
  └── HTML5 Canvas 2D Emulation
  └── Simulated Periodic Trigs Audio Meter
  └── MediaRecorder WebM -> MP4 Rename
  └── In-Memory React State Storage

TARGET (Native Windows 10/11 Desktop):
KINGRecorder.App (WinUI 3 + Windows App SDK 1.5 + MVVM)
  ├── KINGRecorder.Core (Domain Contracts, State Machine, Models)
  ├── KINGRecorder.Capture (Windows.Graphics.Capture + Direct3D 11 Zero-Copy)
  ├── KINGRecorder.Audio (WASAPI System Loopback + Mic Mixer @ 48kHz)
  ├── KINGRecorder.Recording (Compositor Pixel Shader + Media Foundation H.264 MFT)
  └── KINGRecorder.Infrastructure (JSON Settings, Filesystem, Event Logging)
```

---

## 2. Migration Stages

### Phase 0: Repository Audit & Native Foundation (Current Phase)
- **Goal**: Full audit, architecture documentation, creation of Visual Studio solution (`KINGRecorder.sln`), 6 modular projects in `src/`, 3 test projects in `tests/`, and dependency configuration.
- **Deliverables**: Audit report, migration plan, architecture guide, dependency inventory, compilable project files, preserved React prototype.

### Phase 1: Core Engine & Native Screen Capture
- **Goal**: Implement real `Windows.Graphics.Capture` and `Direct3D11CaptureFramePool` across displays and windows.
- **Tasks**:
  - Implement `EnumDisplayMonitors` monitor detection with native DPI scaling.
  - Implement `EnumWindows` window enumeration filtering invisible and cloaked windows.
  - Implement D3D11 device creation and frame pool swapchains.
  - Wire custom selection rectangle coordinate translation to screen capture bounds.

### Phase 2: Webcam System & GPU Compositor
- **Goal**: Real webcam capture via `Windows.Media.Capture` and GPU pixel shader compositor.
- **Tasks**:
  - Implement camera enumeration via `DeviceInformation.FindAllAsync(DeviceClass.VideoCapture)`.
  - Compile HLSL pixel shaders for circle and rounded rectangle anti-aliased alpha masks.
  - Implement Direct3D 11 render target texture blending (screen blit + webcam overlay).
  - Handle camera disconnection and format changes gracefully.

### Phase 3: Dual-Channel WASAPI Audio & 48kHz Mixer
- **Goal**: Real-time system audio loopback capture and microphone input.
- **Tasks**:
  - Implement WASAPI Loopback capture client (`AUDCLNT_STREAMFLAGS_LOOPBACK`).
  - Implement silence generator when system playback is idle.
  - Implement microphone capture with WASAPI Event-Driven low-latency mode.
  - Resample and mix audio streams to 48kHz 16-bit stereo PCM with soft limiter damping.

### Phase 4: Media Foundation Hardware Encoder & MP4 Muxer
- **Goal**: Real hardware-accelerated H.264 video encoding and AAC audio muxing.
- **Tasks**:
  - Initialize `IMFSinkWriter` with video and audio streams.
  - Bind D3D11 device manager to SinkWriter for hardware transforms (NVENC/QSV/AMF).
  - Enforce monotonic timeline synchronization between frame timestamps and audio samples.
  - Implement pause/resume timestamp offset calculation.
  - Finalize MP4 containers with valid index tables (`moov` atom).

### Phase 5: WinUI 3 Presentation, Recordings Library & Polishing
- **Goal**: Complete Fluent desktop interface under the KING design system.
- **Tasks**:
  - Bind WinUI 3 dashboard views to ViewModels via `CommunityToolkit.Mvvm`.
  - Connect live Direct3D 11 preview swapchain to WinUI `SwapChainPanel`.
  - Build the recording library view querying `%USERPROFILE%\Videos\KING Recorder\`.
  - Implement bilingual English / Arabic RTL layout resources.
  - Add global hotkey hooks (`RegisterHotKey` for F9, F10, F12).

---

## 3. Definition of Done (DoD) per Phase

| Phase | Exit Criteria |
|---|---|
| **Phase 0** | Solution and project files created; audit documented; dependencies verified; React prototype preserved; zero broken references. |
| **Phase 1** | Displays and windows enumerated; real Direct3D 11 frames received in frame pool; frame drop rate < 0.1% at 1080p 60fps. |
| **Phase 2** | Physical webcams detected; live camera textures composited into D3D11 swapchain with both circular and rounded-rect borders. |
| **Phase 3** | WASAPI Loopback and microphone audio recorded simultaneously at 48kHz without buffer underruns; no audio clipping. |
| **Phase 4** | Complete MP4 video generated with H.264 video and AAC audio; plays in Windows Media Player with synchronized audio and video. |
| **Phase 5** | Standalone installer built; UI responsive with Mica/Dark theme; Arabic RTL verified; all keyboard shortcuts functional. |
