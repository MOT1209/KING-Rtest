# KING RECORDER — Professional Native Windows Screen Recording Studio

**Version:** 1.0.0 MVP
**Target Platform:** Windows 10 (Build 19041+) / Windows 11, x64
**Architecture:** Native Windows Desktop (WinUI 3 + Windows App SDK 1.5 + .NET 10)
**Design Identity:** Premium KING (charcoal black, gold, recording red)
**Engineering phase:** 0.2 — Foundation Repair & Stabilization (in progress: verification pending)

> **Honest status:** the native application shell, native device enumeration and the recording **session state
> machine** exist. Screen capture, webcam capture, audio capture, video composition and MP4 encoding **do not exist
> yet** and throw `NotSupportedException` rather than producing fake recordings. No successful Windows CI run has
> been observed for this phase.

---

## 1. What exists today

| Capability | Status |
|---|---|
| Solution & projects (10 projects, .NET 10 LTS, x64, central package management) | Verified by inspection |
| Win32 display enumeration (monitors, refresh rate, per-monitor DPI) | Implemented, unit tested |
| Win32 window enumeration (visible/titled/non-cloaked, process name, minimized) | Implemented, unit tested |
| Settings persistence (atomic JSON, validated volumes, `MyVideos` default) | Implemented, unit tested |
| Recording lifecycle state machine (validated transitions, recovery, cancellation, serialized Start/Stop) | Implemented in Phase 0.2; tests authored, **not yet executed** |
| Dependency injection composition (per-layer registrations, provider validation, deterministic shutdown) | Implemented in Phase 0.2; layer registrations unit tested (authored) |
| Pause / resume | **Not supported** — no pipeline exists to suspend; engine reports this explicitly |
| Windows Graphics Capture (`GraphicsCaptureItem`, `Direct3D11CaptureFramePool`) | **Not implemented** (Phase 1) |
| Webcam capture and GPU overlay compositor | **Not implemented** (Phase 2) |
| WASAPI system/microphone capture and mixing | **Not implemented** (Phase 3) |
| H.264/AAC/MP4 encoding (Media Foundation) | **Not implemented** (Phase 4) |
| Recording dashboard, library, hotkeys, native RTL localization | **Not implemented** (Phase 5) |

### Engineering standards

- `.NET 10` (`net10.0-windows10.0.19041.0`) with `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`.
- SDK pinned in `global.json`; package versions centralized in `Directory.Packages.props`.
- Strict P/Invoke layout: `MONITORINFOEX` 104 B, `DEVMODE` 220 B, `RECT` 16 B, 4-byte `BOOL` for `DWMWA_CLOAKED`.
- Explicit unavailable implementations; no fabricated output files, meters or success reports.
- Single resource ownership: the DI container disposes services, `RecordingEngine` disposes only its own state.

---

## 2. Solution structure

```text
KINGRecorder.sln
├── Directory.Build.props            # Shared .NET 10 / warnings-as-errors properties
├── Directory.Packages.props         # Central package versions
├── global.json                      # SDK 10.0.100 (rollForward: latestMinor)
├── src/
│   ├── KINGRecorder.App/            # WinUI 3 shell, MVVM, composition root
│   ├── KINGRecorder.Core/           # Models, enums, interfaces, exceptions, state table
│   ├── KINGRecorder.Capture/        # Win32 enumeration today; Windows.Graphics.Capture in Phase 1
│   ├── KINGRecorder.Audio/          # Device enumeration today; WASAPI in Phase 3
│   ├── KINGRecorder.Recording/      # Session lifecycle engine; explicit unavailable stubs
│   └── KINGRecorder.Infrastructure/ # Settings persistence
├── tests/
│   ├── KINGRecorder.Core.Tests/            # State machine
│   ├── KINGRecorder.Capture.Tests/         # Interop layout, DPI, cancellation, DI
│   ├── KINGRecorder.Recording.Tests/       # Engine lifecycle, DI
│   └── KINGRecorder.Infrastructure.Tests/  # Settings persistence, DI
├── docs/
│   ├── PHASE_0_2_AUDIT.md                  # Phase 0.2 audit (current)
│   ├── PHASE_0_2_IMPLEMENTATION_REPORT.md  # Phase 0.2 changes, evidence, readiness
│   ├── ARCHITECTURE.md                     # Implemented vs target architecture
│   ├── BUILD_VALIDATION.md                 # Build, test and CI instructions
│   ├── DEPENDENCIES.md
│   ├── MIGRATION_PLAN.md
│   ├── FINAL_ENGINEERING_AUDIT.md          # Earlier audit (see its Phase 0.2 correction note)
│   └── PHASE_0_1*_REPORT.md
├── .github/workflows/windows-build.yml     # Windows CI (authored, not yet run)
└── prototype/                              # Preserved React prototype (not part of the product build)
```

---

## 3. Building and testing

### Prerequisites

- Windows 10 build 19041+ or Windows 11 (x64).
- .NET SDK `10.0.100` (as pinned by `global.json`) or newer `10.0.x` minor.
- Optional: Visual Studio 2022 17.10+ with Windows App SDK C# templates.

```powershell
dotnet --info
dotnet restore KINGRecorder.sln
dotnet build KINGRecorder.sln -c Debug -p:Platform=x64
dotnet test  KINGRecorder.sln -c Debug --no-build -p:Platform=x64 `
  --results-directory TestResults --logger "trx;LogFileName=test_results.trx"
dotnet build KINGRecorder.sln -c Release -p:Platform=x64
```

### Validation status

The environment that produced Phase 0.2 had no .NET SDK; every `dotnet` command exited `127`. **No build or test
result is claimed.** Details and the exact commands are in
[docs/BUILD_VALIDATION.md](docs/BUILD_VALIDATION.md) and
[docs/PHASE_0_2_IMPLEMENTATION_REPORT.md](docs/PHASE_0_2_IMPLEMENTATION_REPORT.md).

### CI

`.github/workflows/windows-build.yml` runs on `windows-latest` with `contents: read`, installs the SDK from
`global.json`, restores, builds Debug x64, runs tests with TRX output, builds Release x64 and uploads artifacts.
**It has not been executed yet**, so CI is unverified.

---

## 4. Documentation index

- [Phase 0.2 Audit](docs/PHASE_0_2_AUDIT.md) — what is implemented, partial, missing, or broken.
- [Phase 0.2 Implementation Report](docs/PHASE_0_2_IMPLEMENTATION_REPORT.md) — changes, validation evidence, readiness.
- [Architecture](docs/ARCHITECTURE.md) — layers, DI lifetimes, ownership rules, state machine, target pipelines.
- [Build Validation](docs/BUILD_VALIDATION.md) — commands, environment requirements, CI stages, failure triage.
- [Dependencies](docs/DEPENDENCIES.md) — package evaluation.
- [Migration Plan](docs/MIGRATION_PLAN.md) — phased roadmap.

---

## 5. License

MIT — see [LICENSE](LICENSE).
