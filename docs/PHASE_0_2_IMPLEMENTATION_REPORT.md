# KING RECORDER — Phase 0.2 Implementation Report

**Phase:** 0.2 — Foundation Repair & Engineering Stabilization
**Date:** 2026-11-09
**Scope boundary respected:** no screen/camera/microphone/system-audio capture, no compositing, no encoding,
no MP4 generation, no MCP/AI/device-control work was added. Placeholders remain explicit placeholders.

---

## A. Audit summary

### Already correct

- Ten-project solution layout, all targeting `net10.0-windows10.0.19041.0`, x64-only, with valid non-zero solution
  GUIDs and correct `Debug|x64` / `Release|x64` mappings.
- `global.json` SDK pinning and `Directory.Packages.props` central version management, with no version drift.
- `Core` is a genuine leaf layer (models, enums, interfaces, exception taxonomy, now the state table).
- Win32 enumeration in `Direct3D11CaptureEngine`: correct `MONITORINFOEX` (104 B), `DEVMODE` (220 B), `RECT` (16 B),
  DPI query with fallback, cloaked-window filtering, cancellation — all covered by real tests.
- `SettingsManager`: atomic write via temp file + `File.Replace`, JSON validation, NaN/Infinity rejection, dynamic
  `MyVideos` output directory.
- Pipeline stubs (`UnavailableVideoCompositor`, `UnavailableEncoderService`) throw instead of fabricating output.

### Broken or inconsistent

1. `.github/workflows/woindos-build-yml` was a prompt document, not YAML — CI never existed.
2. `RecordingEngine` performed **no** transition validation; `SetState` accepted any state pair.
3. No serialization of lifecycle operations — concurrent `Start` could double-start.
4. Failure left the engine stuck in `Error`; stop failure left it stuck in `Finalizing`. No recovery path.
5. All exceptions were rewrapped as `VideoEncoderException` with a false "encoder not implemented" message,
   destroying cancellation (`OperationCanceledException`) and capture-failure semantics.
6. Failed/cancelled initialization left a partially started capture/audio pipeline running.
7. `PauseRecordingAsync` only stopped a `Stopwatch` — a pretend pause.
8. `RecordingEngine.Dispose()` disposed DI-owned singletons; the `ServiceProvider` itself was never disposed;
   the transient `MainViewModel` was never disposed or unsubscribed.
9. `DurationUpdated` was declared but never raised (dead contract).
10. DI was inline in a WinUI file, untestable, with no `ValidateOnBuild`/`ValidateScopes`.
11. `RecordingStateMachineTests` duplicated the switch statement inside the test and asserted on its own copy — it
    could not detect a production defect.
12. `.gitignore` did not exclude `bin/`, `obj/`, `TestResults/`, `.vs/`.

### Incomplete (unchanged, by phase boundary)

`Windows.Graphics.Capture` frame pool, webcam capture, GPU compositor, WASAPI capture, audio mixer, Media Foundation
encoder/MP4 muxer, screenshots, library UI, hotkeys, localization in the native app.

---

## B. Changes made

### Created

| File | Why |
|---|---|
| `src/KINGRecorder.Core/State/RecordingStateTransitions.cs` | Single production source of truth for valid states/transitions, reused by the engine and the tests |
| `src/KINGRecorder.Capture/CaptureServiceCollectionExtensions.cs` | UI-agnostic DI registration → testable; concrete + interface share one singleton |
| `src/KINGRecorder.Audio/AudioServiceCollectionExtensions.cs` | Same, for audio |
| `src/KINGRecorder.Recording/RecordingServiceCollectionExtensions.cs` | Same, for the engine and the explicit unavailable pipeline stubs |
| `src/KINGRecorder.Infrastructure/InfrastructureServiceCollectionExtensions.cs` | Same, for settings persistence |
| `.github/workflows/windows-build.yml` | Real GitHub Actions workflow (Windows runner, SDK from `global.json`, restore → Debug build → tests → Release build → artifacts, `contents: read`) |
| `tests/KINGRecorder.Recording.Tests/Fakes/RecordingFakes.cs` | Deterministic test doubles for pipeline boundaries (no hardware) |
| `tests/KINGRecorder.Recording.Tests/DependencyInjection/RecordingServiceRegistrationTests.cs` | Verifies registrations, lifetimes and `TryAdd` semantics |
| `tests/KINGRecorder.Capture.Tests/DependencyInjection/CaptureServiceRegistrationTests.cs` | Verifies single shared singleton and honest idle state |
| `tests/KINGRecorder.Infrastructure.Tests/DependencyInjection/InfrastructureServiceRegistrationTests.cs` | Verifies settings singleton lifetime |
| `docs/PHASE_0_2_AUDIT.md`, `docs/PHASE_0_2_IMPLEMENTATION_REPORT.md` | Required deliverables |

### Deleted

| File | Why |
|---|---|
| `.github/workflows/woindos-build-yml` | Not YAML, not a valid workflow path; replaced by `windows-build.yml`. Removing it prevents anyone from believing CI exists |

### Modified

| File | Change | Problem fixed |
|---|---|---|
| `src/KINGRecorder.Recording/RecordingEngine.cs` | Validated transitions; `SemaphoreSlim` lifecycle gate; partial-pipeline tracking; `try`/`finally` around start/stop/finalize; cancellation rethrown as `OperationCanceledException`; original exceptions preserved; recovery via `Error → Idle/Initializing`; failure fallback (`MoveToErrorStateOrReset`) so a throwing event subscriber cannot mask the error or lock the engine; `ObjectDisposedException` guards; richer structured logs; honest `Pause`/`Resume` (`NotSupportedException`); `DurationUpdated` raised on stop; disposal no longer touches DI-owned services | Audit defects 2–10, 14 |
| `src/KINGRecorder.App/Program.cs` | Uses per-layer registrations; provider built with `ValidateOnBuild`/`ValidateScopes`; provider disposed on exit | Untestable/inline DI, missing shutdown disposal |
| `src/KINGRecorder.App/App.xaml.cs` | Disposes the transient ViewModel on `Window.Closed` | Event-handler leak, undisposed ViewModel |
| `src/KINGRecorder.App/ViewModels/MainViewModel.cs` | Subscribes/unsubscribes `DurationUpdated`, updates `DurationText` | Dead event contract |
| `src/KINGRecorder.App/MainWindow.xaml` | Phase label updated to 0.2; status line now also states `Pause: Not Supported` | Stale/optimistic UI text |
| `src/KINGRecorder.Core/KINGRecorder.Core.csproj` | Removed unused `CommunityToolkit.Mvvm` and `Logging.Abstractions` | Domain layer purity |
| `src/KINGRecorder.{Capture,Audio,Recording,Infrastructure}/*.csproj` | Added `Microsoft.Extensions.DependencyInjection.Abstractions` | Required by the new registration extensions |
| `Directory.Packages.props` | Added pinned `Microsoft.Extensions.DependencyInjection.Abstractions` 9.0.0 | Central version management kept intact |
| `tests/KINGRecorder.Core.Tests/StateMachine/RecordingStateMachineTests.cs` | Now asserts the **production** transition table: 18 allowed, 17 forbidden, self-transitions, target sets, recovery paths, reachability | Fake test that duplicated the switch |
| `tests/KINGRecorder.Recording.Tests/Lifecycle/RecordingEngineLifecycleTests.cs` | 22 tests: failure, recovery, capture failure, duplicate start, concurrent start, cancellation, pause honesty, stop/finalize, double stop, error→idle, cancel, event ordering, duration, disposal ownership, dispose-with-active-session, use-after-dispose, screenshot honesty | Missing lifecycle coverage |
| `tests/KINGRecorder.{Capture,Recording,Infrastructure}.Tests/*.csproj` | Added `Microsoft.Extensions.DependencyInjection` | DI tests |
| `.gitignore` | Ignores `bin/`, `obj/`, `TestResults/`, `*.trx`, `.vs/`, user files | Build output pollution |
| `docs/ARCHITECTURE.md` | Rewritten: implemented vs target, DI/lifetime table, ownership rules, state diagram, interop table, risks | Document claimed pipelines as if implemented |
| `docs/BUILD_VALIDATION.md` | Rewritten to the real workflow path, exact commands, honest local results, failure triage | Documented a workflow that did not exist |
| `docs/FINAL_ENGINEERING_AUDIT.md` | Added a Phase 0.2 correction note (workflow file was misnamed; CI unverified) | Stale CI claims |
| `README.md` | Implementation matrix, validation status, docs index, workflow path | Stale status and structure claims |

### Fixed while getting the first green CI run

| File | Change | Failure it removed |
|---|---|---|
| `src/KINGRecorder.Infrastructure/KINGRecorder.Infrastructure.csproj`, `Directory.Packages.props` | Dropped the redundant `System.Text.Json` reference and its central version | `NU1510` at restore (warnings-as-errors) |
| `src/KINGRecorder.Audio/WasapiAudioCaptureService.cs` | Microphones are flagged against `GetDefaultAudioEndpoint`; the `AudioDataAvailable` pragma is documented | `CS1061` (`MMDevice.Selected` does not exist) — the project had never compiled |
| `src/KINGRecorder.Capture/ScreenCapture/Direct3D11CaptureEngine.cs` | Removed the unused capture item, size and frame counters; `DEVMODE.dmFormName` is no longer projected; `FrameArrived` pragma documented | `CS0169`/`CS0414`/`CS0649`, and a `TypeLoadException` that made `GetDisplaysAsync` silently return no displays |
| `src/KINGRecorder.App/KINGRecorder.App.csproj` | `<DefineConstants>...;DISABLE_XAML_GENERATED_MAIN</DefineConstants>` | Duplicate `Program`/`Main` from the XAML-generated entry point |
| `tests/KINGRecorder.Recording.Tests/Lifecycle/RecordingEngineLifecycleTests.cs` | `await Assert.ThrowsAsync<ObjectDisposedException>` | xUnit analyzer 2014 / obsolete `Assert.Throws(Func<Task>)` |
| `tests/KINGRecorder.Capture.Tests/Interop/NativeStructLayoutTests.cs` | Asserts `dmCollate` at 100 and `dmLogPixels` at 166 around the 64-byte `dmFormName` region | Kept the layout test meaningful without the unloadable field |
| `.github/workflows/windows-build.yml` | `--logger trx` instead of one shared `test_results.trx`; artifact glob `**/TestResults/**/*.trx` | Three of four test projects' results were overwritten by the last one |

---

## C. Validation evidence

### C.1 Windows CI — executed and green

| Property | Value |
|---|---|
| Workflow | `Windows Build & Test` (`.github/workflows/windows-build.yml`) |
| Run | [42662083139](https://github.com/MOT1209/KING-Rtest/actions/runs/42662083139) — conclusion `success` |
| Branch / commit | `phase-0.2-foundation-repair` @ `0c40e28bc242` |
| Runner | `windows-latest`, Windows Server 2025, image `windows-2025-vs2026` |

| Stage | Result |
|---|---|
| Restore | **Passed** |
| Debug x64 build | **Passed** |
| Automated tests | **Passed — 116 passed, 0 failed, 0 skipped** |
| Release x64 build | **Passed** |
| Artifacts | `trx-test-results` (4 files) and `kingrecorder-release-x64` uploaded |

| Test project | Passed | Failed | Skipped |
|---|---|---|---|
| `KINGRecorder.Core.Tests` (state machine, transitions) | 59 | 0 | 0 |
| `KINGRecorder.Recording.Tests` (engine lifecycle, DI) | 29 | 0 | 0 |
| `KINGRecorder.Capture.Tests` (interop layout, DPI, cancellation, DI) | 22 | 0 | 0 |
| `KINGRecorder.Infrastructure.Tests` (settings, DI) | 6 | 0 | 0 |

Four earlier runs of the same workflow failed and each failure produced a real fix (see §B, "Fixed while getting the
first green CI run"): `NU1510` at restore, `MMDevice.Selected` (the audio project had never compiled), unused
capture state plus a never-raised event under warnings-as-errors, a duplicate XAML-generated `Main`, and a
`TypeLoadException` on `DEVMODE` that silently broke display enumeration.

### C.2 Authoring workstation — no SDK

Environment: Windows 11 (build 26200), Git Bash (MINGW64). **No .NET SDK and no MSBuild installed** — verified at
`/c/Program Files/dotnet`, `/c/Program Files (x86)/dotnet`, `%USERPROFILE%/.dotnet`, `/c/ProgramData/dotnet`, and on
`PATH`. All five commands (`dotnet --info`, `restore`, Debug build, `test`, Release build) exited **127**
(`bash: dotnet: command not found`). No local test counts exist; every number above comes from the CI run.

Other checks actually performed:

| Check | Tool | Result |
|---|---|---|
| Workflow YAML parses | PyYAML 6 (`yaml.safe_load`) | **OK** — 1 job `build-and-test`, 9 steps, triggers/permissions present |
| `.github/workflows/` contents | `ls` | only `windows-build.yml` remains |
| Central package version present exactly once | `grep -c` | 1 in each of the four csproj files and in `Directory.Packages.props` |
| No user changes overwritten in `src/`, `tests/`, `docs/` | `git status` before edits | confirmed none |

### C.3 Git branch / commit evidence

Branch `phase-0.2-foundation-repair` on `https://github.com/MOT1209/KING-Rtest.git`, branched from
`main` @ `a320059116d81bc8ac84c99ec425935fef4e0d93` ("merge: Phase 0.1.5 final foundation closure"). The workspace used
for the edits is not itself a clone of that repository, so the change set was staged in a clean clone of `origin/main`
and only the 34 intended paths were committed there; `main` was untouched while the phase was in progress and no
force-push was ever used.

Merged into `main` through pull request #1 as merge commit `72ac35bed77d`, and run
[42664717105](https://github.com/MOT1209/KING-Rtest/actions/runs/42664717105) is green on that commit.

| Commit | Subject |
|---|---|
| `2d52019` | fix(recording): validate the session state machine and serialize the recording lifecycle |
| `5926b56` | ci(windows): replace the workflow stub with a real Windows build and test pipeline |
| `dba416a` | fix(packages): drop the redundant System.Text.Json reference |
| `09a746d` | fix(capture,audio): repair the MMDevice enumeration bug and drop dead capture state |
| `5ae92de` | fix(app,test): stop the XAML generated Main from colliding with the composition root |
| `0c40e28` | fix(capture): make DEVMODE loadable so display enumeration works |

`0c40e28bc242` is the commit the green run above was produced from.

---

## D. Remaining problems

1. **No local build/test evidence on the authoring machine.** The SDK is absent there, so every result in this report
   comes from CI; a contributor without an SDK cannot reproduce them locally.
2. **CS0067 is handled by a documented pragma, not by behaviour.** `FrameArrived` and `AudioDataAvailable` are still
   never raised; the pragmas must be removed when Phase 1/3 deliver frames and audio.
3. **`GetDisplaysAsync`/`GetOpenWindowsAsync` only swallow-and-log.** They return whatever Win32 enumeration produced,
   but a marshalling failure now surfaces as one warning line rather than an exception; there is no test that asserts
   a non-empty display list, because CI runners have no reliable monitor topology.
4. **Composition root still inside `KINGRecorder.App/Program.cs`** (WinUI-dependent). Only the four per-layer
   registration extensions are covered by tests; the final `AddTransient<MainViewModel>()` wiring is verified by
   inspection and by `ValidateOnBuild` at startup, not by a test.
5. **`Recording` → `Capture`/`Audio` project references are unused**; kept to avoid churn.
6. **`SharpDX.MediaFoundation` referenced but unused** (encoder stub); SharpDX is maintenance-only.
7. **No `<Platforms>`/`RuntimeIdentifier>` metadata**; builds rely on `-p:Platform=x64`. CI emits the RID-usage
   notice for `Microsoft.WindowsAppSDK` on every restore; it is informational, not an error.
8. **`actions/checkout`, `setup-dotnet` and `upload-artifact` @v4 emit a Node 20 deprecation notice** on the runner.
   Harmless today; bump to the current major versions before Node 20 support is withdrawn.
9. **Phase 1+ functionality still absent** (capture, audio, compositor, encoder) — by design, not as a defect.
10. **`IWebcamService` remains unimplemented and unregistered**; documented, not silently faked.
11. **UI buttons for Pause/Resume will surface a `NotSupportedException` message.** Intentional honesty; the real
    dashboard arrives in Phase 5.

---

## E. Readiness assessment

### **FOUNDATION READY**

Rationale: the foundation defects from the audit are repaired, the repaired logic is exercised by 116 automated tests
that ran on a real Windows runner, both Debug and Release x64 build with `TreatWarningsAsErrors`, and every defect the
first CI runs exposed (restore, compile, XAML entry point, P/Invoke layout) has been fixed and re-verified on the same
commit that is green.

Promotion criteria (all met):

- [x] Windows CI run green: restore, Debug x64 build, all tests, Release x64 build — [run 42662083139](https://github.com/MOT1209/KING-Rtest/actions/runs/42662083139).
- [x] TRX artifacts downloaded and test counts recorded with real numbers: 116 passed, 0 failed, 0 skipped.
- [x] `CS0067` confirmed real, then narrowed to two documented pragmas that Phase 1/3 must remove.
- [x] Every CI-surfaced compile error fixed and re-verified (see §B and §C.1).
- [x] Commits and SHAs recorded from the real repository (see §C.3).

This verdict covers the *foundation* only. No capture, audio, compositing or encoding capability exists, and
`FOUNDATION READY` must not be read as "recordings work".

---

## F. Next phase

**Phase 1 — Real Windows Screen Capture Engine can be planned, but implementation should not start until a green
Windows CI run exists**, because Phase 1 will add Direct3D 11 / `Windows.Graphics.Capture` code whose failures are
far harder to distinguish from infrastructure noise without a working baseline build.

Recommended first Phase 1 steps, in order: (1) obtain the green CI run; (2) replace `StartCaptureAsync`
`NotSupportedException` with a real `GraphicsCaptureItem` + `Direct3D11CaptureFramePool` that actually raises
`FrameArrived`; (3) remove the unused `Recording → Capture/Audio` references once the engine consumes frames through
those abstractions; (4) keep `Paused` unreachable until a real pipeline can be suspended.
