# KING RECORDER — Professional Windows Screen Recording Studio

**Version:** 1.0.0 MVP  
**Target Platform:** Windows 10 (19041+) / Windows 11  
**Architecture:** Native Windows Desktop (WinUI 3 + Windows App SDK 1.5 + .NET 10 LTS)  
**Design Identity:** Premium KING Design System (Charcoal Black, Gold, Recording Red)

---

## 1. Solution Overview

KING RECORDER is a high-performance Windows desktop screen recording application designed to capture screen, webcam, microphone, and system audio with hardware acceleration and monotonic timeline synchronization.

The repository is structured as a native Visual Studio solution with an accompanying design prototype:

```text
KING-Rtest/
├── KINGRecorder.sln                     # Visual Studio Solution
├── Directory.Build.props                # Global .NET 10 LTS & Windows App SDK properties
├── Directory.Packages.props             # Central Package Version Management
├── global.json                          # SDK Pinning (.NET 10)
│
├── src/
│   ├── KINGRecorder.App/                # WinUI 3 Application Entry & Views
│   ├── KINGRecorder.Core/               # Domain Models, Interfaces & Contracts
│   ├── KINGRecorder.Capture/            # Screen Capture & Direct3D 11 P/Invoke
│   ├── KINGRecorder.Audio/              # WASAPI System Loopback & Microphone
│   ├── KINGRecorder.Recording/          # Compositor & Media Foundation Encoder
│   └── KINGRecorder.Infrastructure/     # Local JSON Settings & Storage
│
├── tests/
│   ├── KINGRecorder.Core.Tests/         # State machine & domain tests
│   ├── KINGRecorder.Capture.Tests/      # Native layout, DPI scaling & bounds tests
│   ├── KINGRecorder.Recording.Tests/    # Audio limiter & engine lifecycle tests
│   └── KINGRecorder.Infrastructure.Tests/ # Settings persistence & MyVideos tests
│
├── docs/
│   ├── FINAL_ENGINEERING_AUDIT.md       # Comprehensive final engineering audit
│   ├── REPAIR_AUDIT.md                  # Comprehensive capability & risk audit
│   ├── MIGRATION_PLAN.md                # Multi-phase engineering roadmap
│   ├── ARCHITECTURE.md                  # Detailed architectural specification
│   ├── DEPENDENCIES.md                  # Evaluated packages & licensing
│   └── BUILD_VALIDATION.md              # Build & test verification guide
│
└── prototype/                           # Preserved interactive React prototype
```

---

## 2. Building the Native Windows Application

### Prerequisites
- Windows 10 (Build 19041+) or Windows 11.
- **Visual Studio** with:
  - *.NET Desktop Development* workload.
  - *Windows App SDK C# Templates*.
  - *.NET 10.0 SDK*.

### Command Line Build
Open a Visual Studio Developer Command Prompt or PowerShell:

```powershell
# Restore NuGet dependencies
dotnet restore KINGRecorder.sln

# Build the solution in Debug configuration
dotnet build KINGRecorder.sln -c Debug

# Run automated tests
dotnet test KINGRecorder.sln -c Debug
```

---

## 3. Running the Interactive Prototype

The original React 19 / TypeScript prototype is preserved in the `prototype/` directory:

```bash
# Install dependencies
npm install

# Start Vite preview server
npm run dev
```

---

## 4. Documentation Index

- [Repair Audit Report](docs/REPAIR_AUDIT.md): Detailed evidence-based classification of existing functionality.
- [Migration Plan](docs/MIGRATION_PLAN.md): Step-by-step roadmap for native implementation.
- [Architecture Guide](docs/ARCHITECTURE.md): Class responsibilities, GPU compositor, and WASAPI pipelines.
- [Dependencies Guide](docs/DEPENDENCIES.md): Package justifications and licensing.

---

## 5. License

This project is licensed under the MIT License — see the [LICENSE](LICENSE) file for details.
