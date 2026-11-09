# KING RECORDER — Phase 0.2 Foundation Audit

**Date:** 2026-11-09
**Scope:** Foundation repair only. No recording capability was implemented in this phase.
**Method:** Static inspection of every project, source file, test file, build property file, workflow file and
document in the repository, plus execution of every validation command the environment supports.

> This audit is deliberately conservative. A file that exists is not a feature. An interface is not an
> implementation. A documented build is not a build.

---

## 1. Repository and working-tree state at audit time

| Item | Finding |
|---|---|
| Git working root | `C:/Users/aihmo/alle folder von code` — **not** the KING RECORDER project directory |
| Project directory | `opencode/KING-Rtest` (untracked inside that working root) |
| Branch / HEAD reported by git | `main` @ `844b354c` (the enclosing repository's branch, not the KING-Rtest repository) |
| KING-Rtest remote present | **No.** The enclosing repository's remotes are `King2-MODLEN`, `mein-wep`, `king-colab-MCP` |
| `KINGRecorder*` files tracked by git | **0** |
| Pre-existing modified files | 16, all under `King-MODLEN/` (unrelated project) |
| Pre-existing staged/working deletions | 2 797, all under `.agents/`, `.claude/` skill folders (unrelated) |
| Untracked content | 49 unrelated project folders plus this project directory |
| `.github/workflows/` | Contained one file: **`woindos-build-yml`** (no extension). Its content was a *prompt document*, not GitHub Actions YAML. GitHub Actions silently ignores it because it does not end in `.yml`/`.yaml` |
| Pre-existing KING RECORDER file modifications | none — the audit and repairs did not overwrite any user change in `src/`, `tests/`, `docs/` |

**Consequence recorded for the final report:** branch creation, focused commits and commit hashes could not be
produced for the KING RECORDER repository from this environment, because this directory is not a checkout of
`https://github.com/MOT1209/KING-Rtest.git`. A temporary branch was briefly created in the enclosing unrelated
repository to test this, then removed (`git branch -D`) with zero unique commits after restoring `main`.

---

## 2. Solution and project inventory (verified on disk)

The solution contains exactly **10 projects**: 6 product projects and 4 test projects, all targeting
`net10.0-windows10.0.19041.0`, `x64` only, with central package management and `TreatWarningsAsErrors=true`
inherited from `Directory.Build.props`.

| Project | Type | Project references | Notable packages | Verdict |
|---|---|---|---|---|
| `KINGRecorder.Core` | library | none | none (after repair) | Correct layering. Previously referenced `CommunityToolkit.Mvvm` and `Microsoft.Extensions.Logging.Abstractions` without using either |
| `KINGRecorder.Capture` | library | Core | SharpDX.Direct3D11, SharpDX.DXGI, Logging.Abstractions | P/Invoke enumeration real; frame capture **not implemented** (`NotSupportedException`) |
| `KINGRecorder.Audio` | library | Core | NAudio.Core, NAudio.Wasapi, Logging.Abstractions | Device enumeration real; capture **not implemented** (`NotSupportedException`) |
| `KINGRecorder.Recording` | library | Core, Capture, Audio | SharpDX.MediaFoundation, SharpDX.Direct3D11, Logging.Abstractions | Engine lifecycle real; compositor/encoder are explicit unavailable stubs |
| `KINGRecorder.Infrastructure` | library | Core | Logging.Abstractions, System.Text.Json | Real atomic settings persistence |
| `KINGRecorder.App` | WinExe (WinUI 3) | all product projects | WindowsAppSDK, CommunityToolkit.Mvvm, MS.Extensions DI/Logging | Shell only: window, MVVM wiring, DI composition |
| `KINGRecorder.Core.Tests` | xunit | Core | Test SDK, xunit | State machine tests (previously did not exercise production code) |
| `KINGRecorder.Capture.Tests` | xunit | Core, Capture | + DI (added) | Struct layout, DPI, cancellation, disposal |
| `KINGRecorder.Recording.Tests` | xunit | Core, Recording | + DI (added) | Engine lifecycle (expanded this phase) |
| `KINGRecorder.Infrastructure.Tests` | xunit | Core, Infrastructure | + DI (added) | Settings persistence and lifetimes |

### Project-integrity findings

| Finding | Severity | Status |
|---|---|---|
| Workflow file misnamed `woindos-build-yml` and containing prompt text | High (CI never ran) | **Fixed** — replaced by `.github/workflows/windows-build.yml` |
| `.gitignore` did not exclude `bin/`, `obj/`, `TestResults/`, `.vs/` | Medium | **Fixed** |
| No branch/config mismatch in `KINGRecorder.sln` (all ten projects map `Debug|x64` and `Release|x64`) | — | Correct |
| `global.json` pins `10.0.100` with `rollForward: latestMinor`; no project overrides it | — | Correct |
| `Core` carried two unused package references | Low | **Fixed** (removed) |
| `Recording` references `Capture` and `Audio` projects but only consumes `Core` interfaces | Low | Left as-is; documented as an unnecessary but harmless dependency |
| `Recording` references `SharpDX.MediaFoundation` while the encoder is unimplemented | Low | Left as-is for Phase 4; the stub makes the absence explicit |
| `<Platforms>`/`<RuntimeIdentifier>` not declared in any project; solution platform is `x64` only | Medium (unverified) | Left as-is; flagged as a risk because no local SDK was available to confirm `-p:Platform=x64` behaviour for the WinUI head |

---

## 3. Capability classification

Classification used: **Fully implemented** · **Partially implemented** · **Not implemented** · **Placeholder/stub** · **Broken or inconsistent**.

| Capability | Evidence | Classification |
|---|---|---|
| Display enumeration (`EnumDisplayMonitors` + `MONITORINFOEX` + `DEVMODE` refresh + DPI) | `src/KINGRecorder.Capture/ScreenCapture/Direct3D11CaptureEngine.cs` | **Fully implemented** (Win32 path), unit tested for layout/DPI |
| Window enumeration (`EnumWindows`, cloaked filter, minimized, process name) | same file | **Fully implemented** (Win32 path) |
| Enumeration cancellation | same file | **Fully implemented**, unit tested |
| Atomic settings persistence + validation | `src/KINGRecorder.Infrastructure/SettingsManager.cs` | **Fully implemented**, unit tested |
| Explicit unavailable pipeline stubs | `src/KINGRecorder.Recording/Stubs/RecordingStubs.cs` | **Placeholder/stub** — intentionally throws `NotSupportedException`, never fabricates output |
| Dependency injection composition | `Program.cs` (before repair: inline, untestable, no validation) | Was **Partially implemented / inconsistent** → repaired |
| Recording lifecycle state machine | `src/KINGRecorder.Recording/RecordingEngine.cs` | Was **Broken/inconsistent** (no transition validation, no recovery, no serialization) → repaired |
| Pause / resume | previous implementation only stopped a `Stopwatch` | Was **Broken/pretend** → now explicitly **Not implemented** (`NotSupportedException`) |
| Duration reporting | `DurationUpdated` declared but never raised | Was **Broken** (dead contract) → now raised on stop; periodic ticks still absent |
| Windows Graphics Capture (`Direct3D11CaptureFramePool`, `FrameArrived`) | `StartCaptureAsync` throws | **Not implemented** (Phase 1) |
| Webcam capture (`IWebcamService`) | interface only, no implementation, not registered | **Not implemented** (Phase 2) |
| GPU compositor (`IVideoCompositor`) | `UnavailableVideoCompositor` | **Placeholder/stub** (Phase 2) |
| WASAPI system/mic capture | `WasapiAudioCaptureService.StartAsync` throws | **Not implemented** (Phase 3) |
| Audio mixing / 48 kHz limiter | only arithmetic inside a test file, no production mixer | **Not implemented** — the existing test validates a formula copy, not shipped code |
| H.264/AAC/MP4 encoding | `UnavailableEncoderService` | **Placeholder/stub** (Phase 4) |
| Screenshot capture | `CaptureScreenshotAsync` throws | **Not implemented** (Phase 5) |
| Recording library UI, hotkeys, localization | not present in `src/` (only in the React prototype) | **Not implemented** |
| GitHub Actions Windows CI | misnamed non-YAML file | Was **Broken** → workflow authored, **not yet executed** |

---

## 4. Dependency-injection audit

Findings before repair:

1. All registrations lived inline in `Program.cs`, which references WinUI types, so no unit test could verify them.
2. `ServiceProvider` was built without `ValidateOnBuild`/`ValidateScopes`, so misconfiguration surfaced at first resolution instead of startup.
3. `SettingsManager` was registered as a concrete singleton and injected as a concrete type — acceptable, but unverified.
4. `IWebcamService` had no implementation and no registration; the omission was silent (documented nowhere).
5. Ownership was contradictory: `RecordingEngine` (singleton) disposed the capture/audio/compositor/encoder singletons, and `ServiceProvider` also owns them — two owners, double disposal.
6. The `ServiceProvider` built in `Program.Main` was **never disposed**, so capture/audio/encoder resources would not be released deterministically on exit.
7. `MainViewModel` (transient) subscribed to engine events and was never disposed when the window closed.

Findings after repair (see implementation report for evidence): per-layer registration extensions, registry validation
at startup, single ownership in the container, deterministic container disposal, ViewModel unsubscription.

---

## 5. RecordingEngine audit (before repair)

| # | Defect | Consequence |
|---|---|---|
| 1 | `SetState` accepted **any** transition | Invalid sequences were representable; `Idle -> Completed` etc. possible |
| 2 | No serialization of Start/Stop/Cancel | Two concurrent `StartRecordingAsync` calls could both pass the `State != Idle` guard and start two sessions |
| 3 | On failure the engine stayed in `Error` forever | No recovery path; an initialization failure locked recording until process restart |
| 4 | `catch (Exception)` rewrapped **everything** as `VideoEncoderException` with a Phase-0.1 encoder message | Lost the real exception type/message (e.g. capture failures were reported as encoder failures) |
| 5 | `OperationCanceledException` was converted into `VideoEncoderException` | Cancellation semantics destroyed |
| 6 | Start failure did not stop a partially started capture/audio pipeline | Leaked capture/audio sessions |
| 7 | `StopRecordingAsync` had no `try/finally`/error path | A throwing `StopCaptureAsync`/`FinalizeAsync` left the engine stuck in `Finalizing` forever |
| 8 | `StopRecordingAsync` threw after `Finalize` when no file existed, leaving `Finalizing` | Permanent lock; no `Idle` recovery |
| 9 | `PauseRecordingAsync` only stopped a `Stopwatch` | Treated as implemented pause; it suspends nothing |
| 10 | `CancelRecordingAsync` could throw from `StopCaptureAsync` | Cancel could fail and leave a non-idle state |
| 11 | `Dispose` disposed injected services and had no guard for an active session | Double ownership with DI; active session not stopped |
| 12 | No `ObjectDisposedException` guard on public operations | Use-after-dispose silently touched disposed collaborators |
| 13 | `DurationUpdated` never raised | Dead event contract |
| 14 | No useful diagnostic context in logs (no mode/output/duration correlation) | Hard to diagnose failures |

---

## 6. Resource-management audit

| Resource | Owner before | Finding | Status |
|---|---|---|---|
| `Direct3D11CaptureFramePool`, `GraphicsCaptureSession`, `Device`, `IDirect3DDevice` | `Direct3D11CaptureEngine` | Idempotent dispose implemented and unit tested; fields are only ever null today | Acceptable for Phase 0.2; real pooling arrives Phase 1 |
| `WasapiLoopbackCapture` / `WasapiCapture` | `WasapiAudioCaptureService` | Idempotent dispose, null-safe stop; never instantiated yet | Acceptable |
| `SemaphoreSlim` lifecycle gate | `RecordingEngine` | **New**; serializes lifecycle operations | Implemented; holds no wait handle so it is intentionally not disposed |
| `CancellationTokenSource` | none | Engine never creates one; callers supply tokens | Correct |
| Event subscriptions | `MainViewModel` → engine | Unsubscribed in `Dispose`, which is now called on window close | Fixed |
| `ServiceProvider` | `Program` | Never disposed | Fixed (`finally`) |
| Output file handles | engine | Only `File.Exists`/`FileInfo`, no open handles | Correct |

---

## 7. Error-handling and logging audit

- A typed exception taxonomy exists in `KINGRecorder.Core/Exceptions/CoreExceptions.cs`
  (`KingRecorderException`, `CaptureDeviceException`, `AudioCaptureException`, `VideoEncoderException`,
  `StoragePermissionException`), but before repair it was bypassed by the engine's blanket rewrap.
- Logging used `Microsoft.Extensions.Logging` correctly in capture/audio/infrastructure; the engine logged with
  little context and logged failures at `Error` without structured fields.
- Silent swallowing existed in two places: `catch { }` around temp-file deletion in `SettingsManager`
  (acceptable, cleanup only) and `catch { }` around output deletion in `CancelRecordingAsync` (now logged).
- No secrets, tokens or personal paths are logged; output paths under `%LOCALAPPDATA%`/`%USERPROFILE%` are logged
  only as absolute local paths, which is standard for desktop diagnostics.

---

## 8. CI audit

- Expected `.github/workflows/windows-build.yml`: **absent**.
- Present `.github/workflows/woindos-build-yml`: contains a Phase 0.1.5 master prompt. It is not YAML, has no
  workflow extension, and therefore **never ran**. Prior documents claiming "workflow authored" refer to a file
  that GitHub Actions did not recognize.
- No artifacts, no TRX reports, no run history exists in this working copy.
- Therefore the pre-existing claim "CI blocked by PAT scope" is partially inaccurate: the blocking issue visible
  in the repository is the missing/misnamed workflow file itself.

---

## 9. Test audit

Existing tests were mostly genuine, with three notable weaknesses:

1. `RecordingStateMachineTests.ValidateStateTransitions` **duplicated the switch statement inside the test** and
   asserted against its own local copy. It could not fail if production transition logic were wrong, because no
   production transition logic existed. Effectively a no-op test.
2. `AudioLimiterGainTests` computes the limiter formula **inside the test** — there is no production mixer, so the
   test proves arithmetic, not shipped behaviour.
3. `RecordingEngineLifecycleTests` asserted that `Dispose` disposes injected services, encoding the double-ownership
   bug (and the wrong exception type `VideoEncoderException` for an unavailable encoder).

Coverage gaps: no recovery tests, no concurrent start/stop tests, no cancellation tests, no
disposal-ownership tests, no DI-configuration tests, no test that `Pause` is honest.

Test doubles already existed for the boundaries and are deterministic; they never touch capture hardware.

---

## 10. Environment blockers for validation

| Blocker | Evidence |
|---|---|
| No .NET SDK/Microsoft Build Tools installed | `dotnet` and `msbuild` are not on `PATH`; `/c/Program Files/dotnet`, `/c/Program Files (x86)/dotnet`, `%USERPROFILE%/.dotnet`, `/c/ProgramData/dotnet` do not exist |
| No Windows build/test execution possible | all five validation commands exit `127` (`command not found`) |
| No KING-Rtest git checkout | see section 1 |

Environment: `MINGW64_NT-10.0-26200` (Windows 11 build 26200) with Git Bash; Python 3.14 available.

---

## 11. Unresolved risks recorded for the next phase

1. **`CS0067` was real and is now documented:** `FrameArrived` and `AudioDataAvailable` are declared and never raised,
   so warnings-as-errors did fail the build. Both now carry a one-line pragma that names the Phase 1/3 delivery which
   will raise them; deleting the pragma is part of that work, not of this phase.
2. **RESOLVED — WinUI 3 head build on CI:** the unpackaged (`WindowsPackageType=None`) XAML build now compiles and
   links on `windows-latest` once the XAML-generated entry point is disabled
   (`DISABLE_XAML_GENERATED_MAIN`); see `docs/BUILD_VALIDATION.md` §3.1.
3. **RESOLVED — `dotnet test` against a WinUI 3 solution:** `--no-build` after the Debug x64 build works; all four
   test projects run on the runner (116 passed). TRX files must be per project, not one shared file name.
4. **RESOLVED — `DEVMODE` could not be loaded at all:** `dmFormName` as a `string` at explicit offset 102 threw
   `TypeLoadException`, so display enumeration silently returned an empty list. The field is no longer projected; no
   CI runner has a monitor topology that would let a test assert a non-empty list.
4. **SharpDX 4.2.0** is maintenance-only; documented as a future migration to Vortice/CsWin32, not changed now.
5. **Capture/audio projects reference SharpDX/NAudio but never use them at runtime yet** — dead weight until
   Phase 1/3.
