# KING RECORDER — Phase 0.1.2 Build & Validation Guide

## 1. Development Environment Requirements

- **Operating System**: Windows 11 (Recommended) or Windows 10 (Build 19041+).
- **IDE**: Visual Studio 2022 (Version 17.9 or newer).
- **Workloads**:
  - *.NET Desktop Development*
  - *Windows App SDK C# Templates / WinUI 3 tooling*
- **SDK**: .NET 10.0 SDK (LTS).

---

## 2. Solution Structure & Platform Targets

- **Primary Architecture Target**: `x64`
- **Configuration Mappings**:
  - `Debug|x64` ➔ Maps to `Debug|x64` across all 10 solution projects.
  - `Release|x64` ➔ Maps to `Release|x64` across all 10 solution projects.
- **Central Package Management**: Enforced via `Directory.Packages.props`.
- **SDK Pinning**: Enforced via `global.json` (`10.0.100`, `latestMinor`).

---

## 3. Command Line Build & Test Instructions

Open PowerShell or Visual Studio Developer Command Prompt at the repository root:

```powershell
# 1. Print environment & SDK information
dotnet --info
dotnet --list-sdks

# 2. Restore all solution packages centrally
dotnet restore KINGRecorder.sln

# 3. Build the solution in Debug configuration (x64)
dotnet build KINGRecorder.sln -c Debug -p:Platform=x64

# 4. Execute all unit and component test projects
dotnet test KINGRecorder.sln -c Debug -p:Platform=x64

# 5. Build in Release configuration for distribution
dotnet build KINGRecorder.sln -c Release -p:Platform=x64
```

---

## 4. Automated Windows CI / CD

Automated verification runs on GitHub Actions (`.github/workflows/windows-build.yml`), executing NuGet restore, Debug compilation, xUnit test execution, and Release packaging on `windows-latest` runners with .NET 10 SDK.
