<div align="center">

<img src=".github/Screenshot/app.png" width="96" alt="LumiShift">

# LumiShift

**Lightweight screen brightness & Gamma correction tool — easier on your eyes**

[![License: GPL-2.0](https://img.shields.io/github/license/SummerRay160/LumiShift?style=for-the-badge&logo=gnu)](LICENSE)
[![Platform](https://img.shields.io/badge/platform-Windows-0078D6?style=for-the-badge&logo=windows&logoColor=white)]()
[![Release](https://img.shields.io/github/v/release/SummerRay160/LumiShift?style=for-the-badge&label=Latest)](https://github.com/SummerRay160/LumiShift/releases/latest)
[![Downloads](https://img.shields.io/github/downloads/SummerRay160/LumiShift/total?style=for-the-badge&color=brightgreen)](https://github.com/SummerRay160/LumiShift/releases)
[![GitHub Issues](https://img.shields.io/github/issues/SummerRay160/LumiShift?style=for-the-badge&logo=github)](https://github.com/SummerRay160/LumiShift/issues)
[![GitHub Stars](https://img.shields.io/github/stars/SummerRay160/LumiShift?style=for-the-badge&logo=github)](https://github.com/SummerRay160/LumiShift/stargazers)
[![GitHub Forks](https://img.shields.io/github/forks/SummerRay160/LumiShift?style=for-the-badge&logo=github)](https://github.com/SummerRay160/LumiShift/network/members)

**[简体中文](README.md) · [English](#introduction) · [繁體中文](README.zh-Hant.md) · [🐛 Report Issues](https://github.com/SummerRay160/LumiShift/issues)**

</div>

---

## Introduction

LumiShift is an open-source screen adjustment tool for Windows. It's a single exe — just download and run, no installation needed. It packs multi-monitor brightness, Gamma correction, color temperature, eye protection mode, and scheduled switching all in one place, so your screen feels a lot more comfortable to use.

---

## 🎯 Features

### 🖥️ Multi-Monitor Brightness

![Brightness Control](.github/Screenshot/Screenshot_brightness.png)

Got several monitors hooked up? LumiShift spots each one automatically — just drag the slider to tweak each screen's brightness independently. No more fumbling with the fiddly buttons on the monitor itself.

- Auto-detects every connected monitor
- Drag the slider, release, and it's saved — that simple
- Hot-plug a monitor and the app syncs up without a restart

### 🎨 Gamma & Color Temperature

![Gamma Correction](.github/Screenshot/Screenshot_Gamma.png)

Whether you want to fine-tune the R/G/B channels, Gamma value, and master brightness — or just drag a single slider to warm the screen up a touch — it's all here.

**What's a display scheme?** Simply put, "a set of tuned parameters" you can save and switch back to with one click — no need to drag sliders all over again.

- **Unified Scheme**: one set of parameters synced to all monitors. Want every screen to look identical? This is the one
- **Multi-Display Scheme**: each monitor's parameters packed into one scheme. Primary vivid, secondary eye-friendly? Save it as a multi-display scheme and switch the whole set in one click — no need to tune monitor by monitor
- Four built-in presets (Standard, Anti-Blue, Eye Care, Gaming), or save your own tweak as a custom scheme

**How does multi-monitor management work?**

- Select "All Monitors" in the dropdown — adjustments sync to every screen
- Select a specific monitor — only that one changes, others stay put
- A monitor you haven't tuned individually shows "Follow All", meaning it tracks the global parameters. Once you tweak it individually, it becomes "Independent" and decouples from the rest
- Want a monitor to follow the global config again? Select it and click the "Follow All" button — its independent config gets cleared

> 💡 **Heads-up**: Switching back from a single monitor to "All Monitors" overwrites everything with the primary monitor's settings — double-check before you do it. Tick "Color Temperature Only" to drop the R/G/B channels and keep just a single warmth slider — quick and simple.

### ⏰ Schedule

![Schedule Configuration](.github/Screenshot/Screenshot_Setting_Time_Scheduling.png)

Standard mode during the day, eye care mode at night? Let the schedule handle it — it switches automatically at the times you set, so you don't have to think about it.

- Switch at whatever time you want — overnight ranges are supported (e.g. 22:00 – 06:00)
- Each time slot auto-applies its preset when the moment comes
- Multi-monitor setups can use different schemes per slot, or just apply a multi-display scheme directly
- The timeline at the top shows the whole day at a glance; overlapping slots are flagged in red
- Need to tweak things manually during the day? No problem — it auto-resumes the schedule at the next slot

### 👁️ Eye Protection Mode

![Eye Protection](.github/Screenshot/Screenshot_Eye%20Protection.png)

Swaps the system window colors for a softer eye-care tint, so long hours staring at documents feel a little less harsh.

- Three built-in preset colors: Mung Bean, Paper Yellow, Sky Blue
- Not a fan of the presets? Pick your own color

### 🔔 System Tray

![System Tray](.github/Screenshot/Screenshot_tray.png)

Minimizes to the tray and stays out of your taskbar. Right-click the icon to quickly:

- Switch presets grouped by monitor (Standard / Anti-Blue / Eye Care / Gaming)
- Toggle Gamma, check for updates, show the main window
- Turn off the monitor, exit the app

### ⚙️ Other Settings

![Settings](.github/Screenshot/Screenshot_Setting.png)

- **Auto-start with Windows**: launches at boot, no need to open it manually
- **Minimize to tray on launch**: stays out of your way while you work
- **Restore Gamma on exit**: resets the screen to its original state when you close the app
- **Notification preferences**: startup, schedule switch, status change, monitor change — toggle each one independently, or turn them all off with the master switch
- **Auto-check updates**: on by default, turn it off if it bothers you

### 🔄 Auto Update

Silently checks GitHub Releases for new versions at startup, or trigger a manual check from the tray menu.

---

## 📥 Download

Head to the [Releases](https://github.com/SummerRay160/LumiShift/releases/latest) page and grab the latest `LumiShift.exe`. Double-click to run — no installation required.

[![Download](https://img.shields.io/badge/📥-Download_Now-brightgreen?style=for-the-badge&logo=github)](https://github.com/SummerRay160/LumiShift/releases/latest)

## 📋 Requirements

| Requirement | Version / Notes |
|:---:|:---|
| **OS** | Windows 10 / 11 |
| **.NET Framework** | 4.8 (built into Windows 10 1903+) |
| **Monitor** | DDC/CI-capable (most modern monitors are) |

## 🔨 Build

Open `LumiShift.sln` in Visual Studio 2022 and build in Release configuration.

```bash
msbuild LumiShift.sln /p:Configuration=Release
```

## 📁 Project Structure

```
LumiShift/
├── BackgroundService.cs            # Background service (tray/schedule/timer)
├── Controls/                       # Custom controls
│   ├── FlatTabControl.cs           # Flat tab control
│   ├── GdiCache.cs                 # GDI object cache pool
│   ├── ModernSlider.cs             # Modern-style slider
│   └── ToggleSwitch.cs             # Toggle switch control
├── Infrastructure/                 # Core infrastructure
│   ├── BrightnessController.cs     # WMI / DDC/CI brightness control
│   ├── GammaController.cs          # Gamma correction (SetDeviceGammaRamp)
│   ├── GcHelper.cs                 # GC collection & working set trim
│   ├── EyeProtectionService.cs     # Eye protection (SetSysColors + Registry)
│   ├── NightLightController.cs     # Windows Night Light control
│   ├── MonitorManager.cs           # Monitor management (EDID/hot-plug/position)
│   ├── NativeMethods.cs            # Win32 API declarations
│   ├── ScheduleEvaluator.cs        # Schedule time slot evaluation & hash caching
│   ├── WeakEvent.cs                # Weak event pattern implementation
│   ├── LightweightJson.cs          # Lightweight JSON parser
│   └── IBrightnessController.cs    # Brightness control interface
├── Models/
│   ├── DisplayScheme.cs            # Display scheme model (unified/multi-display)
│   ├── GammaConfig.cs              # Gamma config & source name helpers
│   ├── PresetDefinitions.cs        # Preset definitions
│   └── UserSettings.cs             # User settings model
├── Properties/
│   └── AssemblyInfo.cs             # Assembly information
├── Resources/
│   └── DesignConstants.cs          # Theme & design constants
├── Services/
│   ├── DisplayGammaStateService.cs # Global & per-display Gamma state management
│   ├── DisplaySchemeService.cs     # Display scheme aggregation
│   ├── PresetService.cs            # Preset resolution (built-in + custom)
│   ├── SaveDisplaySchemeDialog.cs  # Save display scheme dialog
│   ├── SettingsStore.cs            # Settings persistence
│   ├── UpdateService.cs            # Auto-update service
│   └── UpdateDialog.cs             # Update notification dialog
├── Form1.cs                        # Main form logic
├── Form1.Designer.cs               # Main form designer
├── ScheduleConfigForm.cs           # Schedule configuration form
├── Program.cs                      # Entry point (single instance + CLI args)
├── App.config                      # Application configuration
└── LumiShift.csproj                # Project file
```

## 🛠️ Tech Stack

[![.NET](https://img.shields.io/badge/.NET-4.8-purple?style=flat-square&logo=dotnet)]()
[![Windows Forms](https://img.shields.io/badge/Windows_Forms-UI-blue?style=flat-square&logo=windows)]()
[![WMI](https://img.shields.io/badge/WMI-DDC%2FCI-orange?style=flat-square)]()
[![GDI32](https://img.shields.io/badge/GDI32-Gamma-green?style=flat-square)]()
[![GitHub Actions](https://img.shields.io/badge/GitHub_Actions-CI/CD-success?style=flat-square&logo=github-actions)]()

| Feature | Technology |
|:---:|:---:|
| **Framework** | .NET Framework 4.8 / Windows Forms |
| **Brightness Control** | WMI + DDC/CI (`dxva2.dll`) |
| **Gamma Correction** | GDI32 `SetDeviceGammaRamp` |
| **Eye Protection** | User32 `SetSysColors` + Registry |
| **Night Light** | Registry `CloudStore` read/write |
| **Monitor Management** | EDID parsing + Win32 API |
| **Auto Update** | GitHub Releases API |
| **CI/CD** | GitHub Actions (auto build + Release) |

## 📜 License

This project is licensed under the [GPL-2.0 License](LICENSE).

[![License: GPL-2.0](https://img.shields.io/badge/license-GPL--2.0-red?style=for-the-badge&logo=gnu)](LICENSE)

---

<div align="center">

**⭐ If this project helps you, please give it a Star! ⭐**

Made with ❤️ by [SummerRay160](https://github.com/SummerRay160)

</div>
