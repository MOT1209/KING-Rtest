# KING RECORDER — Professional Native Windows Screen Recording Studio

**Version:** 1.0.0 MVP
**Target Platform:** Windows 10 (Build 19041+) / Windows 11, x64
**Architecture:** Native Windows Desktop (WinUI 3 + Windows App SDK 1.5 + .NET 10)
**Design Identity:** Premium KING (charcoal black, gold, recording red)
**Engineering phase:** 0.2.1 — Lifecycle Reliability & UI Thread Safety (**FOUNDATION READY**, Windows CI green)

> **Honest status:** the native application shell, native device enumeration and the recording **session state
> machine** exist, and 138 automated tests pass on Windows CI. Screen capture, webcam capture, audio capture, video
> composition and MP4 encoding **do not exist yet** and throw `NotSupportedException` rather than producing fake
> recordings.

---

## 1. What exists today

| Capability | Status |
|---|---|
| Solution & projects (12 projects, .NET 10 LTS, x64, central package management) | Verified by inspection |
| Win32 display enumeration (monitors, refresh rate, per-monitor DPI) | Implemented, unit tested |
| Win32 window enumeration (visible/titled/non-cloaked, process name, minimized) | Implemented, unit tested |
| Settings persistence (atomic JSON, validated volumes, `MyVideos` default) | Implemented, unit tested |
| Recording lifecycle state machine (validated transitions, recovery, cancellation, serialized Start/Stop) | Implemented in Phase 0.2; verified by the Core and Recording test suites in CI |
| Dependency injection composition (per-layer registrations, provider validation, deterministic shutdown) | Implemented in Phase 0.2; layer registrations unit tested in CI |
| Event notification isolation (a throwing subscriber cannot abort a transition or mask the recording exception) | Implemented in Phase 0.2.1; unit tested in CI |
| UI thread dispatching (`IUiDispatcher`, WinUI adapter in `App`, none in `Core`/`Recording`) | Implemented in Phase 0.2.1; unit tested with a fake dispatcher, not against a real `DispatcherQueue` |
| Coordinated asynchronous shutdown (`ShutdownAsync`, `IAsyncDisposable`, before container disposal) | Implemented in Phase 0.2.1; unit tested in CI |
| Incomplete output cleanup on failed or cancelled initialization | Implemented in Phase 0.2.1; unit tested with real file assertions |
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
│   ├── KINGRecorder.App/            # WinUI 3 shell, DispatcherQueue adapter, composition root
│   ├── KINGRecorder.Presentation/   # MainViewModel, IUiDispatcher (no WinUI reference; unit tested)
│   ├── KINGRecorder.Core/           # Models, enums, interfaces, exceptions, state table
│   ├── KINGRecorder.Capture/        # Win32 enumeration today; Windows.Graphics.Capture in Phase 1
│   ├── KINGRecorder.Audio/          # Device enumeration today; WASAPI in Phase 3
│   ├── KINGRecorder.Recording/      # Session lifecycle engine; explicit unavailable stubs
│   └── KINGRecorder.Infrastructure/ # Settings persistence
├── tests/
│   ├── KINGRecorder.Core.Tests/            # State machine
│   ├── KINGRecorder.Capture.Tests/         # Interop layout, DPI, cancellation, DI
│   ├── KINGRecorder.Recording.Tests/       # Engine lifecycle, event isolation, shutdown, DI
│   ├── KINGRecorder.Presentation.Tests/    # ViewModel threading with a fake dispatcher
│   └── KINGRecorder.Infrastructure.Tests/  # Settings persistence, DI
├── docs/
│   ├── PHASE_0_2_1_REPORT.md               # Phase 0.2.1 lifecycle/threading report (current)
│   ├── PHASE_0_2_AUDIT.md                  # Phase 0.2 audit
│   ├── PHASE_0_2_IMPLEMENTATION_REPORT.md  # Phase 0.2 changes, evidence, readiness
│   ├── ARCHITECTURE.md                     # Implemented vs target architecture
│   ├── BUILD_VALIDATION.md                 # Build, test and CI instructions
│   ├── DEPENDENCIES.md
│   ├── MIGRATION_PLAN.md
│   ├── FINAL_ENGINEERING_AUDIT.md          # Earlier audit (see its Phase 0.2 correction note)
│   └── PHASE_0_1*_REPORT.md
├── .github/workflows/windows-build.yml     # Windows CI (green on phase-0.2.1-lifecycle-hardening)
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
dotnet test  KINGRecorder.sln -c Debug --no-build -p:Platform=x64 --logger trx
dotnet build KINGRecorder.sln -c Release -p:Platform=x64
```

### Validation status

Windows CI is green on commit `9542c1b9a091`:
[run 42677180920](https://github.com/MOT1209/KING-Rtest/actions/runs/42677180920) — restore, Debug x64 build,
**138 passed / 0 failed / 0 skipped** across five test projects, Release x64 build. The workstation that authored
this work has no .NET SDK (every `dotnet` command exits `127`), so all numbers come from CI. Details:
[docs/BUILD_VALIDATION.md](docs/BUILD_VALIDATION.md).

### CI

`.github/workflows/windows-build.yml` runs on `windows-latest` with `contents: read`, installs the SDK from
`global.json`, restores, builds Debug x64, runs the tests (one TRX per project), builds Release x64 and uploads both
artifacts. It triggers on `main`, `phase-*`, `repair/**`, and on pull requests to `main`; `workflow_dispatch` is
enabled.

---

## 4. Documentation index

- [Phase 0.2.1 Report](docs/PHASE_0_2_1_REPORT.md) — lifecycle, UI-threading and shutdown repairs with evidence.
- [Phase 0.2 Audit](docs/PHASE_0_2_AUDIT.md) — what is implemented, partial, missing, or broken.
- [Phase 0.2 Implementation Report](docs/PHASE_0_2_IMPLEMENTATION_REPORT.md) — changes, validation evidence, readiness.
- [Architecture](docs/ARCHITECTURE.md) — layers, DI lifetimes, ownership rules, state machine, target pipelines.
- [Build Validation](docs/BUILD_VALIDATION.md) — commands, environment requirements, CI stages, failure triage.
- [Dependencies](docs/DEPENDENCIES.md) — package evaluation.
- [Migration Plan](docs/MIGRATION_PLAN.md) — phased roadmap.

---

## 5. License

MIT — see [LICENSE](LICENSE).
