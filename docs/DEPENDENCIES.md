# KING RECORDER — Dependency Evaluation & Validation

This document records the official package and framework selections for **KING RECORDER v1.0.0**, based on Microsoft documentation, Long Term Support (LTS) policies, and performance requirements.

---

## 1. Core Framework & SDKs

| Technology | Selected Version | Target Platform | License | Justification |
|---|---|---|---|---|
| **.NET SDK** | `.NET 10.0 LTS` | Windows 10 (19041+) / Windows 11 | MIT | Active Long Term Support release with C# 13 default support, enhanced RyuJIT code generation, and low-allocation Span/Memory primitives for real-time frame buffering. |
| **Windows App SDK** | `1.5.240802000` | Windows 10/11 x64 | MIT | Modern native Windows UI and application lifecycle platform replacing legacy UWP and WPF. Delivers WinUI 3 with Mica/Acrylic materials. |
| **Windows SDK Tools** | `10.0.22621.3233` | Windows 11 22H2+ | Proprietary (MS) | Generates XAML markup compilers and WinRT projection stubs. |

---

## 2. NuGet Package Inventory

### A. Presentation & Architecture
- **`CommunityToolkit.Mvvm` (`8.3.2`)**:
  - *License*: MIT
  - *Usage*: High-performance source generators (`[ObservableProperty]`, `[RelayCommand]`) without runtime reflection overhead.
- **`Microsoft.Extensions.DependencyInjection` (`9.0.0`)**:
  - *License*: MIT
  - *Usage*: Composition root in `Program.cs`, enforcing loose coupling and testability.
- **`Microsoft.Extensions.Logging` & `Console` (`9.0.0`)**:
  - *License*: MIT
  - *Usage*: Structured diagnostic logging for Direct3D and WASAPI lifecycle events.

### B. Multimedia & Hardware Interop
- **`SharpDX.Direct3D11` & `SharpDX.DXGI` (`4.2.0`)**:
  - *License*: MIT
  - *Usage*: Direct GPU texture creation, render target management, and shader pipeline binding for `Windows.Graphics.Capture`.
  - *Note*: SharpDX is stable and zero-allocation. For future post-MVP releases, a migration path to `Vortice.Windows` or `CsWin32` is planned.
- **`SharpDX.MediaFoundation` (`4.2.0`)**:
  - *License*: MIT
  - *Usage*: Hardware-accelerated H.264 video encoding and AAC audio encoding through `IMFSinkWriter`.
- **`NAudio.Core` & `NAudio.Wasapi` (`2.2.1`)**:
  - *License*: MIT
  - *Usage*: Production-tested managed abstraction over Windows CoreAudio API and WASAPI Loopback client (`IMMDeviceEnumerator`, `IAudioClient`).

---

## 3. Platform & Hardware Compatibility Matrix

- **Minimum Supported OS**: Windows 10 Version 2004 (Build 19041).
- **Target OS**: Windows 11 Version 22H2 (Build 22621).
- **Architecture**: `x64` (Primary) and `ARM64` (Native Surface Pro X/Copilot+ PCs). `x86` is legacy only.
- **Hardware Acceleration**:
  - NVIDIA NVENC (GeForce GTX 600+ / RTX series).
  - Intel QuickSync Video (Intel Core 6th Gen+).
  - AMD AMF (Radeon GCN+).
  - Microsoft H.264 Software MFT (Universal CPU fallback).
