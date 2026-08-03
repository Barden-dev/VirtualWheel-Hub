# 🏎️ vWheel Hub (Open Source / Free Version)

---
[![Version](https://img.shields.io/badge/version-v1.0.19-blue.svg)](https://github.com/Barden-dev/vWheel-Hub)
[![License](https://img.shields.io/badge/license-All_Rights_Reserved-red.svg)](LICENSE)
[![Boosty](https://img.shields.io/badge/Support-Boosty-orange.svg?style=flat&logo=boosty)](https://boosty.to/barden_dev)
[![Discord](https://img.shields.io/badge/Community-Discord-5865F2.svg?style=flat&logo=discord)](https://discord.gg/YQn5eh2vfc)

**vWheel Hub** is a high-performance Windows utility designed for **Mouse Steering & Keyboard Control** in PC Sim Racing titles via the **vJoy** driver.

It translates real-time mouse movement into ultra-smooth virtual wheel angles and maps keyboard keypresses to dynamic gas and brake axes with physical curve customization (attack, decay, gamma correction).

---

### 🎥 Interface Preview

<img width="1276" alt="vWheel Hub Interface Walkthrough" src="https://github.com/user-attachments/assets/60a134f7-a1d7-4510-9f06-c3b84921a486"/>

---

### 🎮 Game Compatibility & Telemetry Support

> 💡 **Universal Compatibility:** You can play **ANY racing game** (Assetto Corsa, Automobilista 2, iRacing, BeamNG, Forza, etc.) using base mouse steering and keyboard pedal controls without needing telemetry.
>
> 🏎️ **Telemetry & Auto-Detection:** Telemetry features (ABS/TC Helpers, Speed-Sensitive Steering, Trail Braking, Audio Slip Cues) and automatic game detection are currently supported for:
> * **Le Mans Ultimate (LMU)** — Full Support (Telemetry & Auto-Detect)
> * **rFactor 2** — Experimental Support (Telemetry & Auto-Detect)
> * **iRacing** — Experimental Support (Auto-Detect Only)
> * **Assetto Corsa Competizione** — Experimental Support (Auto-Detect Only)
> * **Assetto Corsa** — Experimental Support (Auto-Detect Only)
> * **Dirt Rally 2.0** — Experimental Support (Auto-Detect Only)
> * **EA WRC** — Experimental Support (Auto-Detect Only)
> * **Richard Burns Rally** — Experimental Support (Auto-Detect Only)
>
> *Once detected and plugin status is clear, you can customize sensitivity, curves, and keybindings to your exact liking.*

---

### 📌 Key Features

* **High-Precision Mouse Steering:** Converts raw mouse coordinates into virtual joystick output with polling rates up to **1000 Hz**.
* **Advanced Gas & Brake Physics:** Fully customizable Fast/Slow Attack curves, Decay rates, instant cutoffs, thresholds, and gamma settings for keyboard inputs.
* **Hierarchical Profile System:** Multi-tiered context profile inheritance (`Game` ➔ `Car Class` ➔ `Car Model`) with real-time undo/redo support.
* **Automatic Game Detection:** Real-time monitoring of running sim racing processes.
* **1-Click vJoy Auto-Configuration:** Automatically sets up vJoy Device #1.

---

### 📊 Free (Open Source) vs Pro Feature Comparison

| Feature / Capability | Free (Open Source) | Pro Version (Boosty) |
| :--- | :---: | :---: |
| **Mouse Steering & Custom Curves** | ✅ | ✅ |
| **Gas & Brake Dynamics (Attack, Decay, Gamma)** | ✅ | ✅ |
| **Hierarchical Profiles (Universal / Game / Class / Car)** | ✅ | ✅ |
| **Custom Hotkeys & Keybindings** | ✅ | ✅ |
| **Polling Rates up to 1000 Hz** | ✅ | ✅ |
| **vJoy Diagnostic & 1-Click Auto-Config** | ✅ | ✅ |
| **Game Process Auto-Detection** | ✅ (Process-based) | ✅ (Advanced + Plugins) |
| **Telemetry ABS Helper (Wheel Lockup Intervention)** | ❌ *(Unlock on Boosty)* | ✅ |
| **Telemetry TC Helper (Traction Control)** | ❌ *(Unlock on Boosty)* | ✅ |
| **Speed-Sensitive Steering (via Telemetry Speed)** | ❌ *(Unlock on Boosty)* | ✅ |
| **Synthetic Trail Braking (Automated Corner Entry)** | ❌ *(Unlock on Boosty)* | ✅ |
| **Slip Audio Feedback (Understeer/Oversteer Cues)** | ❌ *(Unlock on Boosty)* | ✅ |
| **TC Audio Cues (Wheelspin Audio Feedback)** | ❌ *(Unlock on Boosty)* | ✅ |
| **Automated Game Telemetry Plugin Installer** | ❌ *(Unlock on Boosty)* | ✅ |
| **Development Support & Priority Updates** | — | ❤️ Boosty Subscription |

---

### 🚀 Quick Start Guide

1. **Install vJoy:** Download and install the [vJoy Driver](https://github.com/jshafer817/vJoy/releases).
2. **Launch vWheel Hub.**
3. **Configure vJoy:** Click **Auto-Configure vJoy** in the diagnostic section to set up Device #1 automatically if needed.
4. **Set Up Controls & Launch Game:** Launch your sim racing game — Auto-Detect will identify the game process automatically (or select your context manually). Once detected and plugin status is clear, fine-tune sensitivity, response curves, and keybindings to your liking.
5. **Activate Hub:** Toggle **Is Active** or press your assigned hotkey.

---

### 🔗 Links & Community

* 🧡 **Boosty (Pro Build & Support):** [https://boosty.to/barden_dev](https://boosty.to/barden_dev)
* 💬 **Discord Community:** [https://discord.gg/YQn5eh2vfc](https://discord.gg/YQn5eh2vfc)
* 📖 **Wiki & Guides:** [https://github.com/Barden-dev/vWheel-Hub/wiki](https://github.com/Barden-dev/vWheel-Hub/wiki)

---

## 📜 License

Copyright (c) 2026 Barden-dev. All Rights Reserved. See [LICENSE](LICENSE) for details.
