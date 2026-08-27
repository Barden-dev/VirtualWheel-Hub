# 🏎️ vWheel Hub (Open Source / Free Version)

---
[![Version](https://img.shields.io/badge/version-v1.1-blue.svg)](https://github.com/Barden-dev/vWheel-Hub)
[![License](https://img.shields.io/badge/license-All_Rights_Reserved-red.svg)](LICENSE)
[![Boosty](https://img.shields.io/badge/Support-Boosty-orange.svg?style=flat&logo=boosty)](https://boosty.to/barden_dev)
[![Discord](https://img.shields.io/badge/Community-Discord-5865F2.svg?style=flat&logo=discord)](https://discord.gg/YQn5eh2vfc)
[![Wiki](https://img.shields.io/badge/Documentation-Wiki-blueviolet.svg)](https://github.com/Barden-dev/vWheel-Hub/wiki)

**vWheel Hub** is a high-performance Windows desktop application designed for **Mouse Steering & Keyboard Control** in PC Sim Racing titles via the **vJoy** virtual joystick driver.

It translates real-time mouse movement into ultra-smooth virtual wheel angles and maps keyboard keypresses to dynamic gas and brake axes with physical curve customization (attack, decay, gamma correction).

---

### 🎥 Interface Preview

<img width="1276" alt="vWheel Hub Interface Walkthrough" src="https://github.com/user-attachments/assets/60a134f7-a1d7-4510-9f06-c3b84921a486"/>

---

### 🎮 Game Compatibility & Telemetry Support

> 💡 **Universal Compatibility:** You can play **ANY racing game** (Assetto Corsa, Automobilista 2, iRacing, BeamNG, Forza, EA Sports WRC, Dirt Rally, etc.) using base mouse steering and keyboard pedal controls without needing telemetry.
>
> 🏎️ **Telemetry & Auto-Detection:** Telemetry features (ABS/TC Helpers, Speed-Sensitive Steering, Trail Braking, Audio Slip Cues, In-Menu Control Suspension) and automatic game detection are supported for:
> * **Assetto Corsa Competizione (ACC)** — Full Support (Native Shared Memory Telemetry & Auto-Detect)
> * **Assetto Corsa EVO (AC EVO)** — Full Support (Native Shared Memory Telemetry & Auto-Detect)
> * **Le Mans Ultimate (LMU)** — Full Support (Shared Memory Plugin & Auto-Detect, supports 1000 Hz with DirectInput Fix)
> * **BeamNG.drive** — Full Support (Telemetry Mod & Auto-Detect)
> * **rFactor 2** — Experimental Support (Shared Memory Plugin & Auto-Detect)
> * **iRacing** — Experimental Support (Auto-Detect Only)
> * **Assetto Corsa** — Experimental Support (Auto-Detect Only)
> * **Dirt Rally 2.0** — Experimental Support (Auto-Detect Only)
> * **EA Sports WRC** — Experimental Support (Auto-Detect Only)
> * **Richard Burns Rally** — Experimental Support (Auto-Detect Only)
>
> *Once detected and plugin status is clear, you can customize sensitivity, curves, and keybindings to your exact liking.*

---

### 📌 Key Features

* **High-Precision Mouse Steering:** Converts raw mouse movement into virtual joystick output at up to **1000 Hz**, utilizing Windows high-resolution waitable timers and non-blocking accumulated Raw Input deltas.
* **Keyboard Steering Mode:** Full keyboard-only steering support with configurable ramp-up/ramp-down rates, instant full-lock snap, and a dedicated center-steering key.
* **Schema V2 dt-Based Pedal Physics:** Frame-rate and polling-rate independent gas and brake dynamics calculated via true delta-time physics, with seconds-based segment tuning and Turbo dual-key attack.
* **Smart In-Menu Controls & Cursor Locking:** In-menu control suspension, auto-unlocking cursor in menus, hold-to-unlock hotkey, and automatic game-exit cursor release.
* **Hierarchical Profile System:** Multi-tiered context profile inheritance (`Game` ➔ `Car Class` ➔ `Car Model`) with real-time undo/redo support (`Ctrl+Z` / `Ctrl+Y`).
* **Self-Learning Grip Engine (Pro):** Adaptive grip learning continuously learns dry and wet tire grip limits per vehicle.
* **1-Click vJoy Auto-Configuration & LMU Fix:** Automatically configures vJoy Device #1 via CLI and patches LMU DirectInput Fallback in one click.

---

### 📊 Free (Open Source) vs Pro Feature Comparison

| Feature / Capability | Free (Open Source) | Pro Version (Boosty) |
| :--- | :---: | :---: |
| **Mouse Steering & Custom Curves (up to 1000 Hz)** | ✅ | ✅ |
| **Keyboard Steering Mode (Ramp-up/down, snap, center key)** | ✅ | ✅ |
| **Schema V2 dt-Based Gas & Brake Dynamics (Attack, Decay, Gamma, Turbo)** | ✅ | ✅ |
| **Hierarchical Profiles (Universal / Game / Class / Car)** | ✅ | ✅ |
| **Real-Time Undo & Redo History** | ✅ | ✅ |
| **Custom Hotkeys, Dual Keybindings & Conflict Alerts** | ✅ | ✅ |
| **Profile Sharing (.vwh files & Share Codes)** | ✅ | ✅ |
| **vJoy Diagnostic & 1-Click Auto-Config** | ✅ | ✅ |
| **LMU Input Compatibility Fix (DirectInput Fallback @ 1000 Hz)** | ✅ | ✅ |
| **Game Process Auto-Detection** | ✅ (Process-based) | ✅ (Advanced + Plugins) |
| **Telemetry ABS Helper (Wheel Lockup Prevention & Rate Control)** | ❌ *(Unlock on Boosty)* | ✅ |
| **Smart Traction Control (TC Helper with Reactive Rates)** | ❌ *(Unlock on Boosty)* | ✅ |
| **Adaptive Grip Learning Cache & UI Tools** | ❌ *(Unlock on Boosty)* | ✅ |
| **Speed-Sensitive Steering (Dynamic Telemetry Scaling)** | ❌ *(Unlock on Boosty)* | ✅ |
| **Synthetic Trail Braking Engine** | ❌ *(Unlock on Boosty)* | ✅ |
| **Slip Audio Feedback (Understeer / Oversteer Beeps)** | ❌ *(Unlock on Boosty)* | ✅ |
| **TC Audio Feedback (Wheelspin Beeps)** | ❌ *(Unlock on Boosty)* | ✅ |
| **In-Menu Control Suspension** | ❌ *(Unlock on Boosty)* | ✅ |
| **Auto-Unlock Mouse Cursor in Menu** | ❌ *(Unlock on Boosty)* | ✅ |
| **Automated Game Telemetry Plugin Installer** | ❌ *(Unlock on Boosty)* | ✅ |
| **Development Support & Priority Updates** | — | ❤️ Boosty Subscription |

---

### 🧩 Requirements

| Requirement | Details |
| :--- | :--- |
| **OS** | Windows 10 (version 1809 or newer) or Windows 11, **64-bit** |
| **Runtime** | [.NET 10 Desktop Runtime (x64)](https://dotnet.microsoft.com/download/dotnet/10.0) — **required**, the app is published framework-dependent and will not start without it |
| **Driver** | [vJoy Driver](https://github.com/jshafer817/vJoy/releases) with Device #1 configured (X, Y, Z axes and at least 2 buttons — the built-in **Auto-Configure vJoy** button does this for you) |
| **Privileges** | Standard user is enough to run the hub. Configuring vJoy requires a one-time Windows UAC confirmation |

> ⚠️ If Windows shows *"To run this application, you must install .NET"* on launch, install the **Desktop Runtime (x64)** (not the ASP.NET Core Runtime and not the SDK-only package) from the link above and start vWheel Hub again.

---

### 🚀 Quick Start Guide

1. **Install the .NET 10 Desktop Runtime:** Download it from [dotnet.microsoft.com](https://dotnet.microsoft.com/download/dotnet/10.0) (**Desktop Runtime, x64**). Skip this step only if it is already installed.
2. **Install vJoy:** Download and install the [vJoy Driver](https://github.com/jshafer817/vJoy/releases).
3. **Launch vWheel Hub.**
4. **Configure vJoy:** Click **Auto-Configure vJoy** in the diagnostic section to set up Device #1 automatically if needed.
5. **Set Up LMU Compatibility Fix (if playing Le Mans Ultimate):** Click **Apply LMU Input Compatibility Fix** on the dashboard to enable 1000 Hz DirectInput Fallback.
6. **Set Up Controls & Launch Game:** Launch your sim racing game — Auto-Detect will identify the game process automatically (or select your context manually). Once detected and plugin status is clear, fine-tune sensitivity, response curves, and keybindings to your liking.
7. **Activate Hub:** Toggle **Is Active** or press your assigned hotkey.

---

### 🔗 Links & Community

* 🧡 **Boosty (Pro Build & Support):** [https://boosty.to/barden_dev](https://boosty.to/barden_dev)
* 💬 **Discord Community:** [https://discord.gg/YQn5eh2vfc](https://discord.gg/YQn5eh2vfc)
* 📖 **Wiki & Guides:** [https://github.com/Barden-dev/vWheel-Hub/wiki](https://github.com/Barden-dev/vWheel-Hub/wiki)

---

## 📜 License

Copyright (c) 2026 Barden-dev. All Rights Reserved. See [LICENSE](LICENSE) for details.
