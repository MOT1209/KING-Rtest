# KING RECORDER — Build & Validation Guide

## 1. Development Environment Requirements

- **Operating System**: Windows 11 (Recommended) or Windows 10 (Build 19041+).
- **IDE**: Visual Studio 2022 (Version 17.10 or newer).
- **Workloads**:
  - *.NET Desktop Development*
  - *Windows App SDK C# Templates / WinUI 3 tooling*
- **SDK**: .NET 10.0 SDK (`10.0.100` pinned via `global.json`).

---

## 2. Solution Structure & Platform Targets

- **Primary Architecture Target**: `x64`
- **Compiler Warnings**: `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` enforced across all projects.
- **Configuration Mappings**:
  - `Debug|x64` ➔ Maps to `Debug|x64` across all 10 solution projects.
  - `Release|x64` ➔ Maps to `Release|x64` across all 10 solution projects.
- **Central Package Management**: Enforced via `Directory.Packages.props`.
- **SDK Pinning**: Enforced via `global.json` (`10.0.100`, `rollForward: latestMinor`).

---

## 3. Command Line Build & Test Instructions

Execute in PowerShell or Developer Command Prompt at the repository root on Windows:

```powershell
# 1. Print environment & SDK information (Stage A)
dotnet --info
dotnet --list-sdks

# 2. Restore all solution packages centrally (Stage B)
dotnet restore KINGRecorder.sln

# 3. Build the solution in Debug configuration (x64) (Stage C)
dotnet build KINGRecorder.sln -c Debug --no-restore -p:Platform=x64

# 4. Execute all unit and component test projects (Stage D)
dotnet test KINGRecorder.sln -c Debug --no-build -p:Platform=x64 --logger "trx;LogFileName=test_results.trx"

# 5. Build in Release configuration for distribution (Stage E)
dotnet build KINGRecorder.sln -c Release --no-restore -p:Platform=x64
```

---

## 4. Automated Windows CI / CD Pipeline

The automated verification workflow is defined in `.github/workflows/windows-build.yml`:
- **Runner**: `windows-latest`
- **Triggers**:
  - Push to `main` and `repair/**`
  - Pull request targeting `main`
  - Manual `workflow_dispatch`
- **Minimal Permissions**: `contents: read`
- **Stages**:
  - Stage A: Environment & SDK Information (`dotnet --info`)
  - Stage B: NuGet Restore (`dotnet restore KINGRecorder.sln`)
  - Stage C: Debug x64 Build (`dotnet build KINGRecorder.sln -c Debug -p:Platform=x64`)
  - Stage D: Automated Tests (`dotnet test ... --logger "trx;LogFileName=test_results.trx"`) and TRX Artifact Upload
  - Stage E: Release x64 Build (`dotnet build KINGRecorder.sln -c Release -p:Platform=x64`)
  - Stage F: Release Binary Artifact Upload (`actions/upload-artifact@v4`)
- **Repository Permission Requirement**: Pushing workflow modifications to GitHub requires a Personal Access Token with the `workflow` scope enabled.
