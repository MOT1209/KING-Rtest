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

dotnet test KINGRecorder.sln -c Debug --no-build -p:Platform=x64 `
  --results-directory TestResults --logger "trx;LogFileName=test_results.trx"

dotnet build KINGRecorder.sln -c Release -p:Platform=x64
```

Notes:

- `--no-build` on `dotnet test` reuses the Debug x64 output produced by the previous step; drop it for a
  one-shot local run.
- `-p:Platform=x64` is required. Passing no platform lets MSBuild default to `AnyCPU`, which is not a declared
  solution configuration.
- TRX files land in `TestResults/` (git-ignored).
- `TreatWarningsAsErrors=true` is inherited from `Directory.Build.props`; warnings fail the build by design.

---

## 3. Validation status in the environment that produced this document

The machine used for Phase 0.2 has **no .NET SDK installed** (`dotnet` and `msbuild` are not on `PATH`, and no
SDK directory exists under `Program Files`, `ProgramData` or `%USERPROFILE%\.dotnet`). Every command above was
attempted and returned exit code `127` (`command not found`).

Consequently:

| Stage | Local result |
|---|---|
| SDK information | **Not executable** (exit 127) |
| Restore | **Not executable** (exit 127) |
| Debug x64 build | **Not executable** (exit 127) |
| Automated tests | **Not executed** — no pass/fail/skip counts exist |
| Release x64 build | **Not executable** (exit 127) |
| Workflow YAML syntax | **Verified locally** with a YAML parser: 1 job, 9 steps |

No build or test result is claimed for this phase. See `docs/PHASE_0_2_IMPLEMENTATION_REPORT.md` §C for the exact
commands and exit codes.

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

**Workflow run status: NOT VERIFIED.** No KING-Rtest checkout or remote is available in the environment that
produced this document, so the workflow has never been executed. See §5.

---

## 5. How to get the first verified run

1. In a real checkout of `https://github.com/MOT1209/KING-Rtest.git`, confirm the file exists at
   `.github/workflows/windows-build.yml` and commit it on a branch, e.g.:

   ```powershell
   git checkout -b phase-0.2-foundation-repair
   git add .github/workflows/windows-build.yml
   git commit -m "ci(windows): add verified Windows build and test workflow"
   git push -u origin phase-0.2-foundation-repair
   ```

   Pushing workflow files requires a credential with the `workflow` scope (or the GitHub web UI).

2. Open the **Actions** tab → `Windows Build & Test` → the run for that commit. Record the run URL and SHA.
3. Download `trx-test-results` and confirm the TRX files exist.
4. Only after a green run may CI be described as verified, and only then does
   `docs/PHASE_0_2_IMPLEMENTATION_REPORT.md` §E move beyond `PARTIALLY READY`.

---

## 6. Inspecting a failed run

| Symptom | Likely cause | What to check |
|---|---|---|
| `dotnet: command not found` / SDK mismatch | `global.json` version unavailable on the runner | Stage A logs (`dotnet --list-sdks`) |
| `NETSDK1005` / assets file errors | restore ran for a different platform | Stage B log; `-p:Platform=x64` present on build steps |
| `MSB4126` invalid solution configuration | `Platform` not passed | Stage C command line |
| XAML compiler / WinUI errors | WindowsAppSDK tooling missing for the target framework | Stage C log; `Microsoft.WindowsAppSDK` version in `Directory.Packages.props` |
| CS0067 `event is never used` | warnings-as-errors with an event that no implementation raises yet | Stage C log; see the risk note in `docs/PHASE_0_2_AUDIT.md` §11 |
| Tests build but none discovered | test project not marked `IsTestProject`, or runner package missing | Stage D log; `xunit.runner.visualstudio` in each test project |
| TRX missing from artifacts | `--results-directory` mismatch | Stage D log vs `TestResults/**/*.trx` artifact path |

Artifacts are retained for 14 days. Re-run a failed job with **Re-run failed jobs** after fixing the cause; do not
weaken assertions or add suppressions to obtain a green run.
