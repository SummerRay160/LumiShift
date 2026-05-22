<div align="center">

# LumiShift

**轻量级屏幕亮度与 Gamma 校正工具 — 让你的屏幕更护眼**

[![GitHub Release](https://img.shields.io/github/v/release/SummerRay160/LumiShift?style=flat-square&logo=github)](https://github.com/SummerRay160/LumiShift/releases/latest)
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
- 自动检测所有连接的显示器，无需手动配置
- 滑块实时调节，设置自动持久化

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
- 全控件树递归刷新，确保所有 UI 元素一致

#### 系统托盘

- 最小化到托盘运行，不占用任务栏
- 右键菜单：快速切换预设、开关 Gamma、检查更新、关闭显示器、退出

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
├── Controls/                  # 自定义控件
│   ├── FlatTabControl.cs      # 扁平化选项卡
│   ├── ModernSlider.cs        # 现代风格滑块
│   └── ToggleSwitch.cs        # 开关切换控件
├── Infrastructure/            # 核心基础设施
│   ├── BrightnessController.cs    # WMI / DDC/CI 亮度控制
│   ├── GammaController.cs         # Gamma 校正 (SetDeviceGammaRamp)
│   ├── EyeProtectionService.cs    # 护眼模式 (SetSysColors + 注册表)
│   ├── NightLightController.cs    # Windows 夜间模式控制
│   ├── MonitorManager.cs          # 显示器管理
│   ├── NativeMethods.cs           # Win32 API 声明
│   └── IBrightnessController.cs   # 亮度控制接口
├── Models/
│   └── UserSettings.cs        # 用户设置模型
├── Resources/
│   └── DesignConstants.cs     # 主题与设计常量
├── Services/
│   ├── AutoApplyService.cs    # 自动覆盖服务
│   ├── SettingsStore.cs       # 设置持久化
│   └── UpdateService.cs       # 自动更新服务
├── Form1.cs                   # 主窗体逻辑
├── Form1.Designer.cs          # 主窗体设计器
└── Program.cs                 # 入口点（单实例 + 命令行参数）
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

### 贡献指南

欢迎为 LumiShift 贡献代码！请遵循以下流程：

#### 报告问题

- 在 [Issues](https://github.com/SummerRay160/LumiShift/issues) 页面搜索是否已有相同问题
- 创建新 Issue，请包含以下信息：
  - Windows 版本和显示器型号
  - 问题复现步骤
  - 期望行为与实际行为
  - 如有错误信息，请附上完整内容

#### 提交代码

1. **Fork** 本仓库到你的 GitHub 账户
2. 从 `main` 分支创建功能分支：
   ```bash
   git checkout -b feature/your-feature-name
   ```
3. 编写代码并确保：
   - 代码风格与现有代码保持一致
   - 不引入不必要的依赖
   - 编译通过（`msbuild LumiShift.sln /p:Configuration=Release`）
4. 提交更改：
   ```bash
   git commit -m "feat: 简要描述你的更改"
   ```
   提交信息格式建议：
   - `feat:` 新功能
   - `fix:` 修复 Bug
   - `refactor:` 重构
   - `docs:` 文档更新
   - `chore:` 构建/工具变更
5. 推送到你的 Fork：
   ```bash
   git push origin feature/your-feature-name
   ```
6. 创建 **Pull Request** 到本仓库的 `main` 分支
7. 在 PR 描述中说明更改内容和关联的 Issue（如有）

#### 代码规范

- 遵循 C# 命名约定（PascalCase 用于公共成员，camelCase 用于私有字段）
- 私有字段使用 `_` 前缀（如 `_gammaController`）
- 保持与现有代码风格一致，包括缩进、花括号位置等
- Win32 API 声明统一放在 `NativeMethods.cs` 中
- 新增功能尽量保持与现有 UI 风格一致

#### 开发环境

- Visual Studio 2022 或更高版本
- .NET Framework 4.8 SDK
- Windows 10 / 11（部分功能依赖 Windows API，无法在其他平台运行）

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
- Automatic detection of all connected monitors
- Real-time slider adjustment with automatic settings persistence

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
- Recursive control tree refresh for consistent UI

#### System Tray

- Minimize to tray, no taskbar clutter
- Right-click menu: quick preset switch, Gamma toggle, check for updates, turn off monitor, exit

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
├── Controls/                  # Custom controls
│   ├── FlatTabControl.cs      # Flat tab control
│   ├── ModernSlider.cs        # Modern-style slider
│   └── ToggleSwitch.cs        # Toggle switch control
├── Infrastructure/            # Core infrastructure
│   ├── BrightnessController.cs    # WMI / DDC/CI brightness control
│   ├── GammaController.cs         # Gamma correction (SetDeviceGammaRamp)
│   ├── EyeProtectionService.cs    # Eye protection (SetSysColors + Registry)
│   ├── NightLightController.cs    # Windows Night Light control
│   ├── MonitorManager.cs          # Monitor management
│   ├── NativeMethods.cs           # Win32 API declarations
│   └── IBrightnessController.cs   # Brightness control interface
├── Models/
│   └── UserSettings.cs        # User settings model
├── Resources/
│   └── DesignConstants.cs     # Theme & design constants
├── Services/
│   ├── AutoApplyService.cs    # Auto-apply service
│   ├── SettingsStore.cs       # Settings persistence
│   └── UpdateService.cs       # Auto-update service
├── Form1.cs                   # Main form logic
├── Form1.Designer.cs          # Main form designer
└── Program.cs                 # Entry point (single instance + CLI args)
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

Contributions are welcome! Please follow these guidelines:

#### Reporting Issues

- Search [Issues](https://github.com/SummerRay160/LumiShift/issues) to check if the problem already exists
- Create a new Issue with:
  - Windows version and monitor model
  - Steps to reproduce
  - Expected vs. actual behavior
  - Full error message if applicable

#### Submitting Code

1. **Fork** this repository
2. Create a feature branch from `main`:
   ```bash
   git checkout -b feature/your-feature-name
   ```
3. Write code and ensure:
   - Code style is consistent with the existing codebase
   - No unnecessary dependencies are introduced
   - Build passes (`msbuild LumiShift.sln /p:Configuration=Release`)
4. Commit your changes:
   ```bash
   git commit -m "feat: brief description of your change"
   ```
   Suggested commit message format:
   - `feat:` New feature
   - `fix:` Bug fix
   - `refactor:` Refactoring
   - `docs:` Documentation update
   - `chore:` Build/tooling changes
5. Push to your fork:
   ```bash
   git push origin feature/your-feature-name
   ```
6. Create a **Pull Request** to the `main` branch of this repository
7. Describe your changes and reference related Issues in the PR description

#### Code Style

- Follow C# naming conventions (PascalCase for public members, camelCase for private fields)
- Use `_` prefix for private fields (e.g., `_gammaController`)
- Maintain consistency with existing code style (indentation, brace placement, etc.)
- Place Win32 API declarations in `NativeMethods.cs`
- Keep new features consistent with the existing UI style

#### Development Environment

- Visual Studio 2022 or later
- .NET Framework 4.8 SDK
- Windows 10 / 11 (some features depend on Windows APIs and cannot run on other platforms)

### License

This project is licensed under the [GPL-2.0 License](LICENSE).
