# KING RECORDER — Professional Native Windows Screen Recording Studio

**Version:** 1.0.0 MVP  
**Target Platform:** Windows 10 (Build 19041+) / Windows 11  
**Architecture:** Native Windows Desktop (WinUI 3 + Windows App SDK 1.5 + .NET 10 LTS)  
**Design Identity:** Premium KING Design System (Charcoal Black, Gold, Recording Red)  
**Localization:** Full Arabic (RTL) & English (LTR) Support

---

## 1. Project Overview

**KING RECORDER** is an enterprise-grade, high-performance native Windows desktop screen recording application. Built with WinUI 3, C#, .NET 10 LTS, Direct3D 11, and WASAPI, it delivers zero-lag screen, webcam, and system audio recording.

### Key Architectural Standards
- **Framework & Runtime**: Target framework `.NET 10 LTS` (`net10.0-windows10.0.19041.0`) with `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`.
- **SDK & Dependencies**: Pinned via `global.json` and managed centrally using `Directory.Packages.props`.
- **Native Win32 Interop**: Strict P/Invoke definitions for `MONITORINFOEX` (104 bytes), `DEVMODE` (220 bytes with explicit field offsets), `DwmGetWindowAttribute` (4-byte BOOL cloaked window check), and dynamic monitor DPI queries (`GetDpiForMonitor` / `GetDpiForSystem`).
- **Dynamic File Management**: Output directory dynamically uses `Environment.SpecialFolder.MyVideos` without hardcoded user paths.
- **Truthful Hardware Reporting**: Explicit state reporting; un-implemented pipelines throw `NotSupportedException` while maintaining `IsCapturing = false`. Zero fake files or simulated hardware meters.

### Implementation Status Matrix
- **Implemented & Verified in Phase 0.1.4**:
  - Native Windows application solution foundation (10 projects, .NET 10 LTS, x64).
  - Win32 Display & Monitor Enumeration (`EnumDisplayMonitors`, `MONITORINFOEX`, `DEVMODE` refresh rate, `GetDpiForMonitor`).
  - Win32 Window Enumeration (`EnumWindows`, title, process name, iconic minimized check, `DWMWA_CLOAKED`).
  - Atomic Settings persistence & validation (`SettingsManager` with dynamic `MyVideos` folder).
  - MVVM Presentation architecture & clean unsubscription lifecycle (`MainViewModel`).
  - xUnit production-backed unit tests (layout offsets, DPI math, state transitions, lifecycle disposal).
- **Scheduled for Subsequent Implementation Phases**:
  - **Phase 1**: Real `Windows.Graphics.Capture` item creation, `Direct3D11CaptureFramePool`, and swapchain texture rendering.
  - **Phase 2**: Webcam device capture & GPU alpha mask composition (circle / rounded rectangle).
  - **Phase 3**: WASAPI system audio loopback & microphone capture with 48kHz audio limiter.
  - **Phase 4**: Windows Media Foundation hardware-accelerated H.264/AAC MP4 multiplexing (`IMFSinkWriter`).
  - **Phase 5**: Complete recording dashboard & library management.

---

## 2. Solution Structure

```text
KING-Rtest/
├── KINGRecorder.sln                     # Visual Studio Solution
├── Directory.Build.props                # Global .NET 10 LTS & Compiler properties
├── Directory.Packages.props             # Central Package Version Management
├── global.json                          # SDK Pinning (.NET 10)
│
├── src/
│   ├── KINGRecorder.App/                # WinUI 3 Application Views & ViewModels
│   ├── KINGRecorder.Core/               # Domain Models, Interfaces & Contracts
│   ├── KINGRecorder.Capture/            # Screen Capture & Direct3D 11 Interop
│   ├── KINGRecorder.Audio/              # WASAPI System Loopback & Microphone
│   ├── KINGRecorder.Recording/          # Recording Engine & Media Pipeline
│   └── KINGRecorder.Infrastructure/     # Atomic Settings Persistence & Storage
│
├── tests/
│   ├── KINGRecorder.Core.Tests/         # State machine & domain tests
│   ├── KINGRecorder.Capture.Tests/      # Native layout, DPI scaling & bounds tests
│   ├── KINGRecorder.Recording.Tests/    # Audio limiter & engine lifecycle tests
│   └── KINGRecorder.Infrastructure.Tests/ # Settings persistence & MyVideos tests
│
├── docs/
│   ├── FINAL_ENGINEERING_AUDIT.md       # Comprehensive final engineering audit
│   ├── PHASE_0_1_3_REPORT.md            # Phase 0.1.3 Interop & CI Report
│   ├── REPAIR_AUDIT.md                  # Evidence-based capability audit
│   ├── MIGRATION_PLAN.md                # Multi-phase engineering roadmap
│   ├── ARCHITECTURE.md                  # Detailed architectural specification
│   ├── DEPENDENCIES.md                  # Evaluated packages & licensing
│   └── BUILD_VALIDATION.md              # Build & test verification guide
│
└── prototype/                           # Interactive React 19 / TypeScript prototype
```

---

## 3. Building & Testing

### Prerequisites
- Windows 10 (Build 19041+) or Windows 11.
- **Visual Studio** (17.10+) or **.NET 10 SDK** (`10.0.100`).
- *Windows App SDK C# Templates*.

### Command Line Verification Commands

```powershell
# Check .NET SDK environment
dotnet --info
dotnet --list-sdks

# Restore NuGet dependencies
dotnet restore KINGRecorder.sln

# Build Solution in Debug x64 Configuration
dotnet build KINGRecorder.sln -c Debug -p:Platform=x64

# Run Automated Unit Test Suite
dotnet test KINGRecorder.sln -c Debug -p:Platform=x64 --logger "trx;LogFileName=test_results.trx"

# Build Solution in Release x64 Configuration
dotnet build KINGRecorder.sln -c Release -p:Platform=x64
```

---

## 4. Documentation Index

- [Final Engineering Audit](docs/FINAL_ENGINEERING_AUDIT.md): Final engineering audit & verification report.
- [Phase 0.1.3 Interop Report](docs/PHASE_0_1_3_REPORT.md): Native Win32 interop layout & DPI audit.
- [Build Validation Guide](docs/BUILD_VALIDATION.md): Environment requirements & build execution steps.
- [Dependencies Guide](docs/DEPENDENCIES.md): Package evaluation matrix & justification.
- [Architecture Guide](docs/ARCHITECTURE.md): Class responsibilities, GPU compositor & WASAPI pipelines.
- [Migration Roadmap](docs/MIGRATION_PLAN.md): Multi-phase native development roadmap.

---

## 5. License

This project is licensed under the MIT License — see the [LICENSE](LICENSE) file for details.
