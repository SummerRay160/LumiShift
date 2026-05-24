<div align="center">

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
- 支持单显示器独立 Gamma 预设，通过托盘菜单快速切换单屏配置

#### Gamma 校正与色温调节

![Gamma 校正](.github/Screenshot/Screenshot_Gamma.png)

- 精确调节 RGB 三通道增益、Gamma 值和主亮度
- **简化色温模式**：一键拖动色温滑块，自动映射冷暖色温（偏冷 → 偏暖）
- 基于 GDI32 `SetDeviceGammaRamp`，对所有连接的显示器同时生效

#### 预设管理

- 内置 4 种预设：标准、防蓝光、护眼模式、游戏模式
- 支持保存、覆盖、删除自定义预设
- 预设可在主界面、系统托盘右键菜单、定时调度中快速切换

#### 定时调度

![定时调度](.github/Screenshot/Screenshot_Setting.png)

- 按时段自动切换日间/夜间预设（如 18:00 切换护眼模式，06:00 恢复标准模式）
- 支持跨午夜时段（如 22:00 – 06:00）
- 手动调整时自动覆盖定时设置，下次时段切换时恢复

#### 护眼模式

![护眼模式](.github/Screenshot/Screenshot_Eye%20Protection.png)

- 系统级窗口颜色覆盖，修改 Window / Background / AppWorkspace 颜色
- 内置 3 种预设色 + 自定义颜色选择器
- 一键恢复系统默认配色

#### 背景壁纸

- 支持自定义背景图片与透明度调节
- 图片自动缩放覆盖，半透明叠加于界面之上

#### 主题切换

- 深色 / 浅色 / 跟随系统自动切换三种模式
- 实时监听 Windows 系统主题变更，界面配色即时同步
- 全控件树递归刷新，确保所有 UI 元素一致

#### 系统托盘

- 最小化到托盘运行，不占用任务栏
- 右键菜单：快速切换预设、单显示器独立 Gamma 预设切换、开关 Gamma、检查更新、关闭显示器、退出

#### 开机自启

- 可选开机自动启动，通过注册表 `Run` 键实现
- 支持 `--minimized` 静默启动参数，启动即最小化到托盘
- 单实例运行，重复启动时激活已有窗口

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
│   ├── MonitorManager.cs          # 显示器管理
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
| 自动更新 | GitHub Releases API |
| CI/CD | GitHub Actions (自动编译 + Draft Release) |

### 贡献

欢迎为 LumiShift 贡献代码！请阅读 [贡献指南](CONTRIBUTING.md) 了解详情。

### 许可证

本项目基于 [GPL-2.0 License](LICENSE) 开源。

---

## English

### Introduction

LumiShift is an open-source screen adjustment tool for Windows that combines multi-monitor brightness control, Gamma correction, color temperature adjustment, eye protection mode, and scheduled switching. No installation required — just run the single executable and go.

### Features

#### Multi-Monitor Brightness Control

![Brightness Control](.github/Screenshot/Screenshot_brightness.png)

- Independent hardware brightness adjustment per monitor via WMI and DDC/CI dual protocols
- Automatic detection of all connected monitors with EDID parsing and vendor name mapping for intelligent device naming
- Hot-plug detection with automatic cleanup of stale configurations; position inference for smart multi-monitor sorting
- Real-time slider adjustment with automatic settings persistence
- Per-monitor independent Gamma presets with quick single-screen switching from tray menu

#### Gamma Correction & Color Temperature

![Gamma Correction](.github/Screenshot/Screenshot_Gamma.png)

- Fine-tune RGB channel gain, Gamma value, and master brightness
- **Simplified color temperature mode**: drag a single slider to adjust warmth (Cool → Warm)
- Based on GDI32 `SetDeviceGammaRamp`, applies to all connected displays simultaneously

#### Preset Management

- 4 built-in presets: Standard, Anti-Blue, Eye Care, Gaming
- Save, overwrite, and delete custom presets
- Quick switching from main UI, system tray context menu, and scheduled tasks

#### Scheduled Switching

![Scheduled Switching](.github/Screenshot/Screenshot_Setting.png)

- Auto-switch between day/night presets by time (e.g., switch to Eye Care at 18:00, back to Standard at 06:00)
- Supports overnight time ranges (e.g., 22:00 – 06:00)
- Manual adjustments override the schedule; auto-resumes on next time slot change

#### Eye Protection Mode

![Eye Protection](.github/Screenshot/Screenshot_Eye%20Protection.png)

- System-level window color overlay (Window / Background / AppWorkspace)
- 3 built-in preset colors + custom color picker
- One-click restore to system defaults

#### Background Wallpaper

- Custom background image with adjustable opacity
- Auto-scaled cover fit with semi-transparent overlay

#### Theme Switching

- Dark / Light / Auto (follows system) modes
- Real-time Windows system theme monitoring with instant UI color sync
- Recursive control tree refresh for consistent UI

#### System Tray

- Minimize to tray, no taskbar clutter
- Right-click menu: quick preset switch, per-monitor Gamma preset switching, Gamma toggle, check for updates, turn off monitor, exit

#### Auto Start

- Optional startup with Windows via registry `Run` key
- `--minimized` flag for silent startup (minimize to tray)
- Single instance: re-launching activates the existing window

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
│   ├── MonitorManager.cs          # Monitor management
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
| Auto Update | GitHub Releases API |
| CI/CD | GitHub Actions (auto build + Draft Release) |

### Contributing

Contributions are welcome! Please read the [Contributing Guide](CONTRIBUTING.md) for details.

### License

This project is licensed under the [GPL-2.0 License](LICENSE).
