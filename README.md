<div align="center">

<img src=".github/Screenshot/app.png" width="96" alt="LumiShift">

# LumiShift

**轻量级屏幕亮度与 Gamma 校正工具 — 让你的屏幕更护眼**

[![License](https://img.shields.io/github/license/SummerRay160/LumiShift?style=flat-square)](LICENSE)
[![Platform](https://img.shields.io/badge/platform-Windows-blue?style=flat-square&logo=windows)]()

[English](#english) · [中文](#中文)

</div>

---

## 中文

### 介绍

   LumiShift 是一款 Windows 平台上的开源屏幕调节工具，集多显示器亮度控制、Gamma 校正、色温调节、护眼模式、定时调度于一体。无需安装，单文件运行，开机即用。

### 功能特性

#### 多显示器亮度控制

![亮度控制](.github/Screenshot/Screenshot_brightness.png)

- 基于 WMI 和 DDC/CI 双协议，独立调节每台显示器的硬件亮度
- 自动检测所有连接的显示器，支持 EDID 解析与厂商名称映射，智能显示设备名称
- 显示器热插拔检测，自动清理失效配置，位置推断逻辑优化多屏排序
- 滑块实时调节，设置自动持久化
- 支持自定义背景壁纸与透明度调节，图片半透明叠加于界面之上

#### Gamma 校正与色温调节

![Gamma 校正](.github/Screenshot/Screenshot_Gamma.png)

- **多显示器独立调整**：下拉选择目标显示器，单独设置 Gamma 参数，支持一键重置
- 精确调节 R / G / B 三通道增益、Gamma 值（Y）和主亮度，底部实时显示当前参数值
- **简化色温模式**：勾选「只调节色温」后，单一滑块即可映射冷暖色温（偏冷 ↔ 偏暖）
- **预设管理**：内置标准、防蓝光、护眼模式、游戏模式等预设，支持保存和删除自定义预设
- **定时联动**：可为每台显示器指定不同时段的定时调度方案，手动/定时来源自动标识
- 基于 GDI32 `SetDeviceGammaRamp`，支持按显示器独立应用 Gamma

#### 定时调度与设置

![设置](.github/Screenshot/Screenshot_Setting_Time_Scheduling.png)

- **定时切换**：按时段自动切换日/夜预设（如 18:00 切护眼模式，06:00 恢复标准），支持跨午夜时段
- 可为每个时间段指定不同显示器的预设方案，精细化控制
- 手动调整时自动覆盖定时设置，下次时段切换时恢复
- **主题模式**：深色 / 浅色 / 跟随系统三种模式，实时监听 Windows 主题变更即时同步
- **自定义背景**：选择本地图片作为界面壁纸，支持清除与透明度调节
- **开机自启**：可选开机自动启动，支持启动时最小化到托盘运行
- 单实例运行，重复启动时激活已有窗口

#### 护眼模式

![护眼模式](.github/Screenshot/Screenshot_Eye%20Protection.png)

- 系统级窗口颜色覆盖，修改 Window / Background / AppWorkspace 颜色
- 内置 3 种预设色（绿豆沙色、纸页黄、天空蓝）+ 自定义颜色选择器
- 一键恢复系统默认配色

#### 系统托盘

![系统托盘](.github/Screenshot/Screenshot_tray.png)

- 最小化到托盘运行，不占用任务栏
- **快速切换预设**：子菜单按显示器分组（全部显示器 / 单独显示器），一键切换标准、防蓝光、护眼模式、游戏模式
- Gamma 开关状态显示，检查更新、显示主界面、关闭显示器、退出

#### 自动更新

- 启动时静默检查 GitHub Releases 新版本
- 托盘菜单支持手动检查更新

### 下载安装

前往 [Releases](https://github.com/SummerRay160/LumiShift/releases/latest) 页面下载最新版 `LumiShift.exe`，直接运行即可，无需安装。

### 系统要求

- Windows 10 / 11
- .NET Framework 4.8（Windows 10 1903+ 已内置）
- 支持 DDC/CI 的显示器（亮度控制功能，大多数现代显示器均支持）

### 编译

使用 Visual Studio 2022 打开 `LumiShift.sln`，选择 Release 配置编译。

```bash
msbuild LumiShift.sln /p:Configuration=Release
```

### 项目结构

```
LumiShift/
├── Controls/                      # 自定义控件
│   ├── FlatTabControl.cs          # 扁平化选项卡
│   ├── ModernSlider.cs            # 现代风格滑块
│   └── ToggleSwitch.cs            # 开关切换控件
├── Infrastructure/                # 核心基础设施
│   ├── BrightnessController.cs    # WMI / DDC/CI 亮度控制
│   ├── GammaController.cs         # Gamma 校正 (SetDeviceGammaRamp)
│   ├── EyeProtectionService.cs    # 护眼模式 (SetSysColors + 注册表)
│   ├── NightLightController.cs    # Windows 夜间模式控制
│   ├── MonitorManager.cs          # 显示器管理 (EDID/热插拔/位置推断)
│   ├── NativeMethods.cs           # Win32 API 声明
│   └── IBrightnessController.cs   # 亮度控制接口
├── Models/
│   ├── PresetDefinitions.cs       # 预设定义
│   └── UserSettings.cs            # 用户设置模型
├── Properties/
│   └── AssemblyInfo.cs            # 程序集信息
├── Resources/
│   └── DesignConstants.cs         # 主题与设计常量
├── Services/
│   ├── AutoApplyService.cs        # 自动覆盖服务
│   ├── SettingsStore.cs           # 设置持久化
│   └── UpdateService.cs           # 自动更新服务
├── Form1.cs                       # 主窗体逻辑
├── Form1.Designer.cs              # 主窗体设计器
├── ScheduleConfigForm.cs          # 定时调度配置窗体
├── Program.cs                     # 入口点（单实例 + 命令行参数）
├── App.config                     # 应用配置
└── LumiShift.csproj               # 项目文件
```

### 技术栈

| 功能 | 技术 |
|------|------|
| 框架 | .NET Framework 4.8 / Windows Forms |
| 亮度控制 | WMI (`WmiMonitorBrightness`) + DDC/CI (`dxva2.dll`) |
| Gamma 校正 | GDI32 `SetDeviceGammaRamp` |
| 护眼模式 | User32 `SetSysColors` + 注册表 |
| 夜间模式 | 注册表 `CloudStore` 读写 + `WM_SETTINGCHANGE` 通知 |
| 显示器管理 | EDID 解析 + Win32 `EnumDisplayMonitors` |
| 自动更新 | GitHub Releases API |
| CI/CD | GitHub Actions (自动编译 + Release) |

### 贡献

欢迎为 LumiShift 贡献代码！请阅读 [贡献指南](CONTRIBUTING.md) 了解详情。

### 许可证

本项目基于 [GPL-2.0 License](LICENSE) 开源。

---

## English

### Introduction

<img src=".github/Screenshot/app.png" width="96" alt="LumiShift" align="right">

LumiShift is an open-source screen adjustment tool for Windows that combines multi-monitor brightness control, Gamma correction, color temperature adjustment, eye protection mode, and scheduled switching. No installation required — just run the single executable and go.

### Features

#### Multi-Monitor Brightness Control

![Brightness Control](.github/Screenshot/Screenshot_brightness.png)

- Independent hardware brightness adjustment per monitor via WMI and DDC/CI dual protocols
- Automatic detection of all connected monitors with EDID parsing and vendor name mapping for intelligent device naming
- Hot-plug detection with automatic cleanup of stale configurations; position inference for smart multi-monitor sorting
- Real-time slider adjustment with automatic settings persistence
- Custom background wallpaper with adjustable opacity, semi-transparently overlaid on the interface

#### Gamma Correction & Color Temperature

![Gamma Correction](.github/Screenshot/Screenshot_Gamma.png)

- **Per-monitor independent adjustment**: select target monitor from dropdown, set individual Gamma parameters, one-click reset
- Fine-tune R / G / B channel gain, Gamma value (Y), and master brightness; real-time parameter display at bottom
- **Simplified color temperature mode**: enable "Color Temperature Only" for a single slider mapping warmth (Cool ↔ Warm)
- **Preset management**: built-in Standard, Anti-Blue, Eye Care, Gaming presets; save and delete custom presets
- **Schedule integration**: assign time-based schedules per monitor, manual/schedule source auto-labeled
- Based on GDI32 `SetDeviceGammaRamp`, supports per-display independent Gamma application

#### Scheduled Switching & Settings

![Settings](.github/Screenshot/Screenshot_Setting_Time_Scheduling.png)

- **Scheduled switching**: auto-switch day/night presets by time (e.g., Eye Care at 18:00, Standard at 06:00); supports overnight ranges
- Assign different per-monitor presets per time slot for fine-grained control
- Manual adjustments override schedule; auto-resumes on next time slot change
- **Theme mode**: Dark / Light / Auto (follows system); real-time Windows theme monitoring with instant UI sync
- **Custom background**: choose local image as interface wallpaper, with clear and opacity controls
- **Auto-start**: optional startup with Windows; minimize-to-tray on launch support
- Single instance: re-launching activates the existing window

#### Eye Protection Mode

![Eye Protection](.github/Screenshot/Screenshot_Eye%20Protection.png)

- System-level window color overlay (Window / Background / AppWorkspace)
- 3 built-in preset colors (Mung Bean, Paper Yellow, Sky Blue) + custom color picker
- One-click restore to system defaults

#### System Tray

![System Tray](.github/Screenshot/Screenshot_tray.png)

- Minimize to tray, no taskbar clutter
- **Quick preset switch**: submenu grouped by monitor (All / Individual), one-click Standard, Anti-Blue, Eye Care, Gaming
- Gamma toggle status display, check for updates, show main window, turn off monitor, exit

#### Auto Update

- Silent check for new versions on GitHub Releases at startup
- Manual update check from tray menu

### Download

Go to the [Releases](https://github.com/SummerRay160/LumiShift/releases/latest) page and download `LumiShift.exe`. No installation required.

### Requirements

- Windows 10 / 11
- .NET Framework 4.8 (built into Windows 10 1903+)
- DDC/CI-capable monitor (for brightness control; most modern monitors support this)

### Build

Open `LumiShift.sln` in Visual Studio 2022 and build in Release configuration.

```bash
msbuild LumiShift.sln /p:Configuration=Release
```

### Project Structure

```
LumiShift/
├── Controls/                      # Custom controls
│   ├── FlatTabControl.cs          # Flat tab control
│   ├── ModernSlider.cs            # Modern-style slider
│   └── ToggleSwitch.cs            # Toggle switch control
├── Infrastructure/                # Core infrastructure
│   ├── BrightnessController.cs    # WMI / DDC/CI brightness control
│   ├── GammaController.cs         # Gamma correction (SetDeviceGammaRamp)
│   ├── EyeProtectionService.cs    # Eye protection (SetSysColors + Registry)
│   ├── NightLightController.cs    # Windows Night Light control
│   ├── MonitorManager.cs          # Monitor management (EDID/hot-plug/position)
│   ├── NativeMethods.cs           # Win32 API declarations
│   └── IBrightnessController.cs   # Brightness control interface
├── Models/
│   ├── PresetDefinitions.cs       # Preset definitions
│   └── UserSettings.cs            # User settings model
├── Properties/
│   └── AssemblyInfo.cs            # Assembly information
├── Resources/
│   └── DesignConstants.cs         # Theme & design constants
├── Services/
│   ├── AutoApplyService.cs        # Auto-apply service
│   ├── SettingsStore.cs           # Settings persistence
│   └── UpdateService.cs           # Auto-update service
├── Form1.cs                       # Main form logic
├── Form1.Designer.cs              # Main form designer
├── ScheduleConfigForm.cs          # Schedule configuration form
├── Program.cs                     # Entry point (single instance + CLI args)
├── App.config                     # Application configuration
└── LumiShift.csproj               # Project file
```

### Tech Stack

| Feature | Technology |
|---------|-----------|
| Framework | .NET Framework 4.8 / Windows Forms |
| Brightness Control | WMI (`WmiMonitorBrightness`) + DDC/CI (`dxva2.dll`) |
| Gamma Correction | GDI32 `SetDeviceGammaRamp` |
| Eye Protection | User32 `SetSysColors` + Registry |
| Night Light | Registry `CloudStore` read/write + `WM_SETTINGCHANGE` notification |
| Monitor Management | EDID parsing + Win32 `EnumDisplayMonitors` |
| Auto Update | GitHub Releases API |
| CI/CD | GitHub Actions (auto build + Release) |

### Contributing

Contributions are welcome! Please read the [Contributing Guide](CONTRIBUTING.md) for details.

### License

This project is licensed under the [GPL-2.0 License](LICENSE).
