# KING RECORDER — Build & Validation Guide

**Last updated:** Phase 0.2 (2026-11-09)

---

## 1. Required environment (authoritative)

| Requirement | Value |
|---|---|
| OS | Windows 10 build 19041+ or Windows 11 (x64) |
| .NET SDK | `10.0.100` exactly as pinned by `global.json` (`rollForward: latestMinor`); the CI step installs the SDK **from `global.json`** |
| IDE (optional) | Visual Studio 2022 17.10+ with *.NET Desktop Development* and the Windows App SDK C# templates |
| Windows SDK | Provided per-project by `Microsoft.Windows.SDK.BuildTools` 10.0.26100.1742 |
| Architecture | `x64` only; the solution declares `Debug|x64` and `Release|x64` |

No other SDK, runtime, workload or global tool is required for a command-line build.

---

## 2. Commands

Run from the repository root in PowerShell (or any shell where `dotnet` resolves):

```powershell
dotnet --info
dotnet --list-sdks

dotnet restore KINGRecorder.sln

dotnet build KINGRecorder.sln -c Debug -p:Platform=x64

dotnet test KINGRecorder.sln -c Debug --no-build -p:Platform=x64 --logger trx

dotnet build KINGRecorder.sln -c Release -p:Platform=x64
```

Notes:

- `--no-build` on `dotnet test` reuses the Debug x64 output produced by the previous step; drop it for a
  one-shot local run.
- `-p:Platform=x64` is required. Passing no platform lets MSBuild default to `AnyCPU`, which is not a declared
  solution configuration.
- Each test project writes its own TRX under `tests/<project>/TestResults/` (git-ignored). Do not force one
  shared file name: `--logger "trx;LogFileName=..."` makes the four projects overwrite each other's results.
- `TreatWarningsAsErrors=true` is inherited from `Directory.Build.props`; warnings fail the build by design.

---

## 3. Validation status

### 3.1 Windows CI — verified green

| Property | Value |
|---|---|
| Workflow | `Windows Build & Test` (`.github/workflows/windows-build.yml`) |
| Run | [42662083139](https://github.com/MOT1209/KING-Rtest/actions/runs/42662083139) — conclusion `success` |
| Branch / commit | `phase-0.2-foundation-repair` @ `0c40e28bc242` |
| Runner | `windows-latest` (Windows Server 2025, image `windows-2025-vs2026`) |
| SDK | installed from `global.json` (10.0.100, roll-forward to the newest 10.0.x) |

| Stage | Result |
|---|---|
| Restore | **Passed** |
| Debug x64 build | **Passed** |
| Automated tests | **Passed — 116 passed, 0 failed, 0 skipped** |
| Release x64 build | **Passed** |
| TRX artifacts | **Uploaded** — one file per test project |
| Release artifact | **Uploaded** — `kingrecorder-release-x64` |

| Test project | Passed | Failed | Skipped |
|---|---|---|---|
| `KINGRecorder.Core.Tests` | 59 | 0 | 0 |
| `KINGRecorder.Recording.Tests` | 29 | 0 | 0 |
| `KINGRecorder.Capture.Tests` | 22 | 0 | 0 |
| `KINGRecorder.Infrastructure.Tests` | 6 | 0 | 0 |

### 3.2 Local workstation — no SDK

The workstation used to author Phase 0.2 has **no .NET SDK installed** (`dotnet` and `msbuild` are not on `PATH`,
and no SDK directory exists under `Program Files`, `ProgramData` or `%USERPROFILE%\.dotnet`). Every command above
was attempted there and returned exit code `127` (`command not found`).

| Stage | Local result |
|---|---|
| SDK information | **Not executable** (exit 127) |
| Restore | **Not executable** (exit 127) |
| Debug x64 build | **Not executable** (exit 127) |
| Automated tests | **Not executed locally** — the counts above come from CI only |
| Release x64 build | **Not executable** (exit 127) |
| Workflow YAML syntax | **Verified locally** with a YAML parser: 1 job, 9 steps |

### 3.3 Defects the first CI runs found (all fixed)

None of these could have been discovered without executing the pipeline, which is why the phase stayed
`PARTIALLY READY` until the run above went green.

| Failure | Root cause | Fix |
|---|---|---|
| `NU1510` at restore | `System.Text.Json` referenced explicitly although .NET 10 ships it | Removed the `PackageReference`; the `using` still resolves against the in-box assembly |
| `CS1061`: `MMDevice` has no `Selected` | The audio project never compiled | Microphones are flagged against `GetDefaultAudioEndpoint(DataFlow.Capture, Role.Communications)` |
| `CS0169`/`CS0414`/`CS0649` | Unused capture item, size and frame counters; a null event assignment | Dead state removed; contract events keep a documented `CS0067` pragma |
| Duplicate `Program` / `Main` | WinUI generated an entry point beside `Program.cs` | `<DefineConstants>...;DISABLE_XAML_GENERATED_MAIN</DefineConstants>` |
| `TypeLoadException` on `DEVMODE` | `dmFormName` was a `string` at explicit offset 102 | The unread field is no longer projected; every other offset and the 220-byte size are unchanged |

---

## 4. GitHub Actions workflow

The single workflow is **`.github/workflows/windows-build.yml`** (the previous file
`.github/workflows/woindos-build-yml` was not YAML and has been removed).

| Property | Value |
|---|---|
| Name | `Windows Build & Test` |
| Runner | `windows-latest` |
| Triggers | `push` to `main`, `phase-0.2-foundation-repair`, `repair/**`; `pull_request` to `main`; `workflow_dispatch` |
| Permissions | `contents: read` only |
| Concurrency | one run per ref, `cancel-in-progress: true` |
| Timeout | 60 minutes |
| SDK install | `actions/setup-dotnet@v4` with `global-json-file: global.json` |
| Artifacts | `trx-test-results` (uploaded even when tests fail), `kingrecorder-release-x64` (Release output) |
| Retention | 14 days |

Pipeline stages, in order: checkout → install SDK from `global.json` → `dotnet --info` / `--list-sdks` →
`dotnet restore` → Debug x64 build → `dotnet test` (TRX) → Release x64 build → artifact uploads.

The workflow is not configured with `continue-on-error` or `|| true`: any restore, build or test failure fails the
job.

**Workflow run status: VERIFIED.** Run
[42662083139](https://github.com/MOT1209/KING-Rtest/actions/runs/42662083139) completed with conclusion `success` on
commit `0c40e28bc242`, including the Release x64 build. Earlier runs of the same workflow failed and drove the fixes
listed in §3.3.

---

## 5. Keeping the run verified

1. Every push to `main`, `phase-0.2-foundation-repair` or `repair/**`, and every pull request to `main`, runs the
   workflow. A red run is a real failure: fix the cause, never add `continue-on-error`, `|| true`, or a suppression
   that hides it.
2. Download `trx-test-results` after each run and confirm all four TRX files are present; a missing file means one
   project did not execute.
3. Record the run URL and commit SHA here (and in `docs/PHASE_0_2_IMPLEMENTATION_REPORT.md` §C) whenever the
   baseline commit changes, so no document claims a green state for a commit that was never built.
4. Pushing workflow files requires a credential with the `workflow` scope.

---

## 6. Inspecting a failed run

| Symptom | Likely cause | What to check |
|---|---|---|
| `dotnet: command not found` / SDK mismatch | `global.json` version unavailable on the runner | Stage A logs (`dotnet --list-sdks`) |
| `NETSDK1005` / assets file errors | restore ran for a different platform | Stage B log; `-p:Platform=x64` present on build steps |
| `MSB4126` invalid solution configuration | `Platform` not passed | Stage C command line |
| XAML compiler / WinUI errors | WindowsAppSDK tooling missing for the target framework | Stage C log; `Microsoft.WindowsAppSDK` version in `Directory.Packages.props` |
| `NU1510` at restore | a package is already part of the .NET 10 shared framework | Stage B log; remove the explicit `PackageReference` instead of disabling the warning |
| Duplicate `Program`/`Main` | WinUI generated an entry point beside a hand-written one | Stage C log; define `DISABLE_XAML_GENERATED_MAIN` |
| `TypeLoadException` at test time | a reference-type field on an unaligned explicit struct offset | Stage D log; project only the fields that are read |
| CS0067 `event is never used` | warnings-as-errors with an event no implementation raises yet | Stage C log; both current cases carry a documented pragma until Phase 1/3 raise them |
| Tests build but none discovered | test project not marked `IsTestProject`, or runner package missing | Stage D log; `xunit.runner.visualstudio` in each test project |
| Only one TRX in artifacts | all projects wrote the same file name | Stage D log; use `--logger trx` so each project writes its own file |

Artifacts are retained for 14 days. Re-run a failed job with **Re-run failed jobs** after fixing the cause; do not
weaken assertions or add suppressions to obtain a green run.
