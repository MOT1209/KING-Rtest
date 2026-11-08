# KING RECORDER — Build & Validation Guide

## 1. Development Environment Requirements

- **Operating System**: Windows 11 (Recommended) or Windows 10 (Build 19041+).
- **IDE**: Visual Studio 2022 (Version 17.9 or newer).
- **Workloads**:
  - *.NET Desktop Development*
  - *Universal Windows Platform Development* (with Windows App SDK C# templates)
- **SDK**: .NET 8.0 SDK (or compatible LTS).

---

## 2. Command Line Build & Test Instructions

Open PowerShell or Visual Studio Developer Command Prompt at the repository root:

```powershell
# 1. Restore all solution packages
dotnet restore KINGRecorder.sln

# 2. Build the solution in Debug configuration (x64)
dotnet build KINGRecorder.sln -c Debug -p:Platform=x64

# 3. Execute all unit and component test projects
dotnet test KINGRecorder.sln -c Debug -p:Platform=x64

# 4. Build in Release configuration for distribution
dotnet build KINGRecorder.sln -c Release -p:Platform=x64
```

---

## 3. Automated CI / CD

Automated verification runs on GitHub Actions (`.github/workflows/windows-build.yml`), executing NuGet restore, Debug compilation, xUnit test execution, and Release packaging on `windows-latest` runners.
