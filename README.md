<div align="center">

<img src=".github/Screenshot/app.png" width="96" alt="LumiShift">

# LumiShift

**轻量级屏幕亮度与 Gamma 校正工具 — 让你的屏幕更护眼**

[![License: GPL-2.0](https://img.shields.io/github/license/SummerRay160/LumiShift?style=for-the-badge&logo=gnu)](LICENSE)
[![Platform](https://img.shields.io/badge/platform-Windows-0078D6?style=for-the-badge&logo=windows&logoColor=white)]()
[![Release](https://img.shields.io/github/v/release/SummerRay160/LumiShift?style=for-the-badge&label=最新版本)](https://github.com/SummerRay160/LumiShift/releases/latest)
[![Downloads](https://img.shields.io/github/downloads/SummerRay160/LumiShift/total?style=for-the-badge&color=brightgreen)](https://github.com/SummerRay160/LumiShift/releases)
[![GitHub Issues](https://img.shields.io/github/issues/SummerRay160/LumiShift?style=for-the-badge&logo=github)](https://github.com/SummerRay160/LumiShift/issues)
[![GitHub Stars](https://img.shields.io/github/stars/SummerRay160/LumiShift?style=for-the-badge&logo=github)](https://github.com/SummerRay160/LumiShift/stargazers)
[![GitHub Forks](https://img.shields.io/github/forks/SummerRay160/LumiShift?style=for-the-badge&logo=github)](https://github.com/SummerRay160/LumiShift/network/members)

**[English](#english) · [中文](#中文) · [🐛 问题反馈](https://github.com/SummerRay160/LumiShift/issues)**

</div>

---

## 中文

### 介绍

   LumiShift 是一款 Windows 平台上的开源屏幕调节工具，集多显示器亮度控制、Gamma 校正、色温调节、护眼模式、定时调度于一体。无需安装，单文件运行，开机即用。

### 🎯 功能特性

#### 🖥️ 多显示器亮度控制

![亮度控制](.github/Screenshot/Screenshot_brightness.png)

- 基于 WMI 和 DDC/CI 双协议，独立调节每台显示器的硬件亮度
- 自动检测所有连接的显示器，支持 EDID 解析与厂商名称映射，智能显示设备名称
- 显示器热插拔检测，自动清理失效配置，位置推断逻辑优化多屏排序
- 滑块实时调节，设置自动持久化
- 支持自定义背景壁纸与透明度调节，图片半透明叠加于界面之上

#### 🎨 Gamma 校正与色温调节

![Gamma 校正](.github/Screenshot/Screenshot_Gamma.png)

- **多显示器独立调整**：下拉选择目标显示器，单独设置 Gamma 参数，支持一键重置
- **显示方案系统**：支持保存统一方案（所有显示器相同效果）和多屏方案（每台显示器独立参数），一键切换整组配置
- 精确调节 R / G / B 三通道增益、Gamma 值（Y）和主亮度，底部实时显示当前参数值
- **简化色温模式**：勾选「只调节色温」后，单一滑块即可映射冷暖色温（偏冷 ↔ 偏暖）
- **预设管理**：内置标准、防蓝光、护眼模式、游戏模式等预设，支持保存和删除自定义显示方案
- **定时联动**：可为每台显示器指定不同时段的定时调度方案，支持多屏方案自动切换，手动/定时来源自动标识
- 基于 GDI32 `SetDeviceGammaRamp`，支持按显示器独立应用 Gamma

#### 📖 使用指南 — 多显示器操作

> 以下内容针对 **两台及以上显示器** 的用户，帮助你理解多屏场景下的操作逻辑与常见误区。

##### Gamma 校正界面（多显示器）

![Gamma 校正](.github/Screenshot/Screenshot_Gamma.png)

**1. 显示器下拉选择框**

界面顶部有一个显示器下拉框，包含「**所有显示器**」选项和每个已连接显示器的名称：

| 选择项 | 行为 | 适用场景 |
|:---:|:---|:---|
| **所有显示器** | 调整的参数会**同步应用到所有屏幕**（全局模式） | 希望所有屏幕效果一致时 |
| **某台具体显示器** | 调整**仅影响该屏幕**，自动进入独立配置模式（独立模式） | 需要为不同屏幕设置不同参数时 |

**2. 全局模式 ⇄ 独立模式的切换**

- **从「所有显示器」切换到某台显示器**：滑块值会切换为该显示器的独立配置。如果该显示器之前没有独立配置，则继承当前全局参数作为初始值。
- **从某台显示器切回「所有显示器」**：系统会弹出**确认对话框**，提示你"切换将同步所有显示器参数，各显示器的独立配置将被清除"。这是因为：
  - 切回全局模式意味着"统一管理"，之前的独立差异化配置会被丢弃
  - 如果当时开启了**定时调度**，提示还会额外说明"定时调度的独立配置也将被清除"

> 💡 **易混淆点**：切回「所有显示器」不是"合并"各屏幕的独立参数，而是以**主显示器**的参数作为新的全局参数，其他屏幕的独立配置直接清除。

**3. Gamma 开关的行为差异**

| 当前模式 | Gamma 开关的作用范围 |
|:---:|:---|
| **所有显示器（全局）** | 控制**所有显示器**的 Gamma 开/关 |
| **某台显示器（独立）** | 仅控制**当前选中显示器**的 Gamma 开/关 |

在独立模式下开关 Gamma 时，该显示器会被标记为**手动来源（manual）**；如果此时定时调度正在运行，调度器会知道该显示器已被手动覆盖，不会在下次时段切换时覆盖你的手动调整。

**4. 一键重置的影响范围**

点击「重置」按钮**只影响当前下拉框选中的显示器**：
- 选中「所有显示器」→ 重置所有显示器到系统默认值
- 选中某台显示器 → 仅重置该台显示器

如果该显示器的配置是由**定时调度**生成的（来源标记为 `schedule`），重置时会额外提示："此配置由定时调度生成，重置后将在下次时段切换时恢复。"

**5. 手动调整与定时调度的交互**

当**定时调度功能开启**时，你在 Gamma 界面的任何手动调整（拖动滑块、切换预设、开关 Gamma）都会触发**手动覆盖标记**：
- 调度器暂停对该显示器的自动切换
- 界面底部会显示来源标识变化
- **等到下一个时间段变更时**，调度器会自动恢复定时控制，应用新时段的预设方案

这意味着：你可以放心地在白天临时调整某台屏幕的参数，到了晚上定时切换时间点，它会自动回到夜间预设。

**6. 预设应用的目标**

- 在「所有显示器」模式下应用预设 → **所有显示器**都会切换到该预设
- 在独立模式下（选中某台显示器）应用预设 → **仅该显示器**切换
- 通过**托盘菜单**的快速切换 → 可选择"全部显示器"或指定某一台显示器

##### 时间调度配置界面（多显示器）

![定时调度配置](.github/Screenshot/Screenshot_Setting_Time_Scheduling.png)

**1. 统一模式 vs 独立模式**

每个时段行的右侧有一个 **「多屏」开关控件**：

| 模式 | 开关状态 | 说明 |
|:---:|:---:|:---|
| **统一模式** | 关闭（默认） | 该时段内**所有显示器**使用同一个预设 |
| **独立模式** | 开启 | 可以为该时段内的**每台显示器分别指定不同的预设** |

**2. 如何使用独立模式**

1. 点击时段行右侧的 **Toggle 开关**，将其切换到开启状态
2. 该行会展开为**每台显示器一个独立的预设下拉框**
3. 为每台显示器单独选择所需的预设（如：主显示器用「护眼模式」，副显示器用「标准」）
4. 保存即可生效

> ⚠️ **注意**：从独立模式切回统一模式时，如果各显示器之前设置了不同的预设，系统会弹出确认提示："切换到统一模式将清除各显示器的独立预设配置"，因为统一模式下只能保留一个公共预设。

**3. 典型场景示例**

假设你有两台显示器（主屏 + 副屏），可以这样配置：

| 时段 | 主显示器预设 | 副显示器预设 | 配置方式 |
|:---|:---|:---|:---|
| 06:00 – 18:00 | 标准 | 标准 | 统一模式（关闭多屏开关） |
| 18:00 – 06:00 | 护眼模式 | 防蓝光 | 独立模式（开启多屏开关，分别选择） |

这样白天两屏一致，晚上根据使用场景给不同屏幕设置不同的护眼强度。

**4. 跨午夜时段**

支持配置跨越午夜的时间段（如 `22:00 – 06:00`），无需拆分为两个时段。

#### ⚙️ 设置

![设置](.github/Screenshot/Screenshot_Setting.png)

- **定时切换**：可启用定时切换功能（点击"配置定时..."进行详细的时间段与预设方案配置）
- **自定义背景**：可选择本地图片作为界面壁纸，支持透明度调节
- **开机自启**：可选开机自动启动
- **启动时最小化到托盘**：启动后自动最小化到系统托盘
- **启动时自动检查更新**：可关闭启动后的静默 GitHub Releases 更新检查，托盘菜单仍可手动检查
- **退出时还原 Gamma**：可选关闭程序时自动恢复所有显示器的 Gamma 值为系统原始状态
- **通知偏好**：可独立控制启动通知、定时切换通知、状态变更通知、显示器变更通知，总开关一键控制
- 单实例运行，重复启动时激活已有窗口

#### ⏰ 定时调度配置

![定时调度配置](.github/Screenshot/Screenshot_Setting_Time_Scheduling.png)

- **时间段管理**：按时段自动切换日/夜预设方案（如 18:00 切护眼模式，06:00 恢复标准），支持跨午夜时段配置
- **多显示器独立调度**：可为每个时间段指定不同显示器的预设方案，也可直接选择多屏方案自动切换
- **显示方案类型识别**：时段行显示 "使用统一方案"/"使用多屏方案"/"临时逐台配置" 摘要，配置意图一目了然
- **手动/定时智能切换**：手动调整时自动覆盖定时设置，下次时段切换时自动恢复定时方案
- 支持保存、删除和重置调度配置

#### 👁️ 护眼模式

![护眼模式](.github/Screenshot/Screenshot_Eye%20Protection.png)

- 系统级窗口颜色覆盖，修改 Window / Background / AppWorkspace 颜色
- 内置 3 种预设色（绿豆沙色、纸页黄、天空蓝）+ 自定义颜色选择器
- 一键恢复系统默认配色

#### 🔔 系统托盘

![系统托盘](.github/Screenshot/Screenshot_tray.png)

- 最小化到托盘运行，不占用任务栏
- **快速切换预设**：子菜单按显示器分组（全部显示器 / 单独显示器），一键切换标准、防蓝光、护眼模式、游戏模式
- Gamma 开关状态显示，检查更新、显示主界面、关闭显示器、退出

#### 🔄 自动更新

- 启动时静默检查 GitHub Releases 新版本，可在设置中关闭
- 托盘菜单支持手动检查更新

### 📥 下载安装

> 💡 **一键运行，无需安装！**

前往 [Releases](https://github.com/SummerRay160/LumiShift/releases/latest) 页面下载最新版 `LumiShift.exe`，直接运行即可。

[![Download](https://img.shields.io/badge/📥-立即下载-brightgreen?style=for-the-badge&logo=github)](https://github.com/SummerRay160/LumiShift/releases/latest)

### 📋 系统要求

| 要求 | 版本/说明 |
|:---:|:---|
| **操作系统** | Windows 10 / 11 |
| **.NET Framework** | 4.8（Windows 10 1903+ 已内置） |
| **显示器** | 支持 DDC/CI（大多数现代显示器均支持） |

### 🔨 编译

使用 Visual Studio 2022 打开 `LumiShift.sln`，选择 Release 配置编译。

```bash
msbuild LumiShift.sln /p:Configuration=Release
```

### 📁 项目结构

```
LumiShift/
├── BackgroundService.cs            # 后台服务（托盘/调度/定时器）
├── Controls/                       # 自定义控件
│   ├── FlatTabControl.cs           # 扁平化选项卡
│   ├── GdiCache.cs                 # GDI 对象缓存池
│   ├── ModernSlider.cs             # 现代风格滑块
│   └── ToggleSwitch.cs             # 开关切换控件
├── Infrastructure/                 # 核心基础设施
│   ├── BrightnessController.cs     # WMI / DDC/CI 亮度控制
│   ├── GammaController.cs          # Gamma 校正 (SetDeviceGammaRamp)
│   ├── GcHelper.cs                 # GC 回收与工作集修剪
│   ├── EyeProtectionService.cs     # 护眼模式 (SetSysColors + 注册表)
│   ├── NightLightController.cs     # Windows 夜间模式控制
│   ├── MonitorManager.cs           # 显示器管理 (EDID/热插拔/位置推断)
│   ├── NativeMethods.cs            # Win32 API 声明
│   ├── ScheduleEvaluator.cs        # 定时调度时段评估与哈希缓存
│   ├── WeakEvent.cs                # 弱事件模式实现
│   ├── LightweightJson.cs          # 轻量级 JSON 解析器
│   └── IBrightnessController.cs    # 亮度控制接口
├── Models/
│   ├── DisplayScheme.cs            # 显示方案模型（统一/多屏）
│   ├── GammaConfig.cs              # Gamma 配置与来源名称工具
│   ├── PresetDefinitions.cs        # 预设定义
│   └── UserSettings.cs             # 用户设置模型
├── Properties/
│   └── AssemblyInfo.cs             # 程序集信息
├── Resources/
│   └── DesignConstants.cs          # 主题与设计常量
├── Services/
│   ├── DisplayGammaStateService.cs # 全局/逐台显示器 Gamma 状态管理
│   ├── DisplaySchemeService.cs     # 显示方案聚合
│   ├── PresetService.cs            # 预设解析（内置 + 自定义）
│   ├── SaveDisplaySchemeDialog.cs  # 保存显示方案对话框
│   ├── SettingsStore.cs            # 设置持久化
│   ├── UpdateService.cs            # 自动更新服务
│   └── UpdateDialog.cs             # 更新提示对话框
├── Form1.cs                        # 主窗体逻辑
├── Form1.Designer.cs               # 主窗体设计器
├── ScheduleConfigForm.cs           # 定时调度配置窗体
├── Program.cs                      # 入口点（单实例 + 命令行参数）
├── App.config                      # 应用配置
└── LumiShift.csproj                # 项目文件
```

### 🛠️ 技术栈

[![.NET](https://img.shields.io/badge/.NET-4.8-purple?style=flat-square&logo=dotnet)]()
[![Windows Forms](https://img.shields.io/badge/Windows_Forms-UI-blue?style=flat-square&logo=windows)]()
[![WMI](https://img.shields.io/badge/WMI-DDC%2FCI-orange?style=flat-square)]()
[![GDI32](https://img.shields.io/badge/GDI32-Gamma-green?style=flat-square)]()
[![GitHub Actions](https://img.shields.io/badge/GitHub_Actions-CI/CD-success?style=flat-square&logo=github-actions)]()

| 功能 | 技术 |  |
|:---:|:---:|:---:|
| **框架** | .NET Framework 4.8 / Windows Forms | ![.NET](https://img.shields.io/badge/.NET-4.8-purple?style=flat-square) |
| **亮度控制** | WMI + DDC/CI (`dxva2.dll`) | ![WMI](https://img.shields.io/badge/WMI-DDC%2FCI-orange?style=flat-square) |
| **Gamma 校正** | GDI32 `SetDeviceGammaRamp` | ![GDI32](https://img.shields.io/badge/GDI32-Gamma-green?style=flat-square) |
| **护眼模式** | User32 `SetSysColors` + 注册表 | 🔒 |
| **夜间模式** | 注册表 `CloudStore` 读写 | 🌙 |
| **显示器管理** | EDID 解析 + Win32 API | 🖥️ |
| **自动更新** | GitHub Releases API | 🔄 |
| **CI/CD** | GitHub Actions (自动编译 + Release) | ✅ |

### 🤝 贡献

欢迎为 LumiShift 贡献代码！请阅读 [贡献指南](CONTRIBUTING.md) 了解详情。

[![Contributors](https://img.shields.io/github/contributors/SummerRay160/LumiShift?style=for-the-badge&logo=github)](https://github.com/SummerRay160/LumiShift/graphs/contributors)
[![PRs Welcome](https://img.shields.io/badge/PRs-welcome-brightgreen?style=for-the-badge&logo=github)](https://github.com/SummerRay160/LumiShift/pulls)

### 📜 许可证

本项目基于 [GPL-2.0 License](LICENSE) 开源。

[![License: GPL-2.0](https://img.shields.io/badge/license-GPL--2.0-red?style=for-the-badge&logo=gnu)](LICENSE)

---

<div align="center">

**⭐ 如果这个项目对你有帮助，请给一个 Star 支持一下！ ⭐**

Made with ❤️ by [SummerRay160](https://github.com/SummerRay160)

</div>

## English

### Introduction

<img src=".github/Screenshot/app.png" width="96" alt="LumiShift" align="right">

LumiShift is an open-source screen adjustment tool for Windows that combines multi-monitor brightness control, Gamma correction, color temperature adjustment, eye protection mode, and scheduled switching. No installation required — just run the single executable and go.

---

### 🎯 Features

#### 🖥️ Multi-Monitor Brightness Control

![Brightness Control](.github/Screenshot/Screenshot_brightness.png)

- Independent hardware brightness adjustment per monitor via WMI and DDC/CI dual protocols
- Automatic detection of all connected monitors with EDID parsing and vendor name mapping for intelligent device naming
- Hot-plug detection with automatic cleanup of stale configurations; position inference for smart multi-monitor sorting
- Real-time slider adjustment with automatic settings persistence
- Custom background wallpaper with adjustable opacity, semi-transparently overlaid on the interface

#### 🎨 Gamma Correction & Color Temperature

![Gamma Correction](.github/Screenshot/Screenshot_Gamma.png)

- **Per-monitor independent adjustment**: select target monitor from dropdown, set individual Gamma parameters, one-click reset
- **Display scheme system**: save unified schemes (same effect across all monitors) and multi-display schemes (independent per-monitor parameters); switch entire config groups with one click
- Fine-tune R / G / B channel gain, Gamma value (Y), and master brightness; real-time parameter display at bottom
- **Simplified color temperature mode**: enable "Color Temperature Only" for a single slider mapping warmth (Cool ↔ Warm)
- **Preset management**: built-in Standard, Anti-Blue, Eye Care, Gaming presets; save and delete custom display schemes
- **Schedule integration**: assign time-based schedules per monitor, multi-display scheme auto-switch support, manual/schedule source auto-labeled
- Based on GDI32 `SetDeviceGammaRamp`, supports per-display independent Gamma application

#### 📖 Usage Guide — Multi-Monitor Operation

> The following guide is for users with **two or more monitors**, explaining multi-monitor operation logic and common points of confusion.

##### Gamma Correction Interface (Multi-Monitor)

![Gamma Correction](.github/Screenshot/Screenshot_Gamma.png)

**1. Monitor Selector Dropdown**

At the top of the Gamma panel is a monitor selector dropdown containing an **"All Monitors"** option plus each connected monitor's name:

| Selection | Behavior | Use Case |
|:---:|:---|:---|
| **All Monitors** | Adjustments are **synced to all screens** (Global mode) | When you want all screens to look identical |
| **A specific monitor** | Adjustments affect **only that screen**; enters independent config mode (Per-Display mode) | When you need different settings per screen |

**2. Global Mode ⇄ Per-Display Mode Switching**

- **Switching from "All Monitors" to a specific monitor**: Slider values switch to that monitor's independent config. If no independent config exists yet, it inherits current global parameters as starting values.
- **Switching from a specific monitor back to "All Monitors"**: A **confirmation dialog** appears, stating "Switching will sync all monitor parameters; independent configs for each monitor will be cleared." This happens because:
  - Returning to Global mode means "unified management" — previous independent per-monitor configs are discarded
  - If **schedule** is enabled, the dialog additionally warns that "independent schedule configs will also be cleared"

> 💡 **Common confusion**: Switching back to "All Monitors" does **not merge** each screen's independent parameters. Instead, it uses the **primary monitor's** parameters as the new global values and directly clears all other monitors' independent configs.

**3. Gamma Toggle Behavior Differences**

| Current Mode | Gamma Toggle Affects |
|:---:|:---|
| **All Monitors (Global)** | **All monitors'** Gamma on/off |
| **A specific monitor (Per-Display)** | **Only the selected monitor's** Gamma on/off |

When toggling Gamma in Per-Display mode, that monitor is tagged with **manual source (`manual`)**. If a schedule is running, the scheduler knows this monitor has been manually overridden and won't overwrite your changes at the next time slot transition.

**4. Reset Button Scope**

Clicking **Reset** only affects the **currently selected monitor** in the dropdown:
- "All Monitors" selected → Resets **all monitors** to system defaults
- A specific monitor selected → Resets **only that monitor**

If that monitor's config was generated by **scheduled task** (source tagged as `schedule`), an additional prompt appears: "This config was generated by schedule; it will restore at next time slot change after reset."

**5. Manual Adjustment & Schedule Interaction**

When **schedule is enabled**, any manual adjustment you make in the Gamma interface (dragging sliders, switching presets, toggling Gamma) triggers a **manual override flag**:
- The scheduler pauses automatic switching for that monitor
- The source indicator at the bottom of the interface changes accordingly
- At the **next time slot boundary**, the scheduler automatically resumes scheduled control and applies the new time slot's preset

This means you can freely adjust a screen's parameters temporarily during the day, and when the evening scheduled time arrives, it will automatically revert to the night preset.

**6. Preset Application Target**

- Apply preset in "All Monitors" mode → **All monitors** switch to that preset
- Apply preset in Per-Display mode (specific monitor selected) → **Only that monitor** switches
- Quick-switch via **tray menu** → Choose "All Monitors" or a specific monitor

##### Schedule Configuration Interface (Multi-Monitor)

![Schedule Configuration](.github/Screenshot/Screenshot_Setting_Time_Scheduling.png)

**1. Unified Mode vs. Independent Mode**

Each time slot row has a **"Multi-Screen" toggle** on the right side:

| Mode | Toggle State | Description |
|:---:|:---:|:---|
| **Unified Mode** | Off (default) | **All monitors** use the same preset for this time slot |
| **Independent Mode** | On | You can assign **different presets per monitor** for this time slot |

**2. How to Use Independent Mode**

1. Click the **Toggle switch** on the right side of a time slot row to turn it **On**
2. The row expands to show **one independent preset dropdown per connected monitor**
3. Select the desired preset for each monitor individually (e.g., Primary → "Eye Care", Secondary → "Standard")
4. Save to apply

> ⚠️ **Note**: When switching from Independent mode back to Unified mode, if monitors had different presets configured, a confirmation dialog appears: "Switching to unified mode will clear independent preset configs for each monitor," because Unified mode only retains one shared preset.

**3. Typical Scenario Example**

With two monitors (Primary + Secondary), you could configure:

| Time Slot | Primary Preset | Secondary Preset | Config Method |
|:---|:---|:---|:---|
| 06:00 – 18:00 | Standard | Standard | Unified mode (toggle off) |
| 18:00 – 06:00 | Eye Care | Anti-Blue | Independent mode (toggle on, select per monitor) |

This way both screens are identical during daytime, while at night each screen gets eye-protection tuned to its usage context.

**4. Overnight Time Slots**

Time slots crossing midnight (e.g., `22:00 – 06:00`) are supported — no need to split into two separate slots.

#### ⚙️ Settings

![Settings](.github/Screenshot/Screenshot_Setting.png)

- **Scheduled switching**: enable scheduled switching (click "配置定时..." for detailed time slot and preset configuration)
- **Custom background**: choose local image as interface wallpaper with adjustable opacity
- **Auto-start**: optional startup with Windows
- **Minimize to tray on launch**: automatically minimize to system tray on startup
- **Auto-check updates on startup**: optionally disable the silent GitHub Releases update check; manual tray checks remain available
- **Restore Gamma on exit**: optionally restore all monitors' Gamma values to system defaults when closing
- **Notification preferences**: independently control startup, schedule switch, status change, and monitor change notifications; master toggle for one-click control
- Single instance: re-launching activates the existing window

#### ⏰ Schedule Configuration

![Schedule Configuration](.github/Screenshot/Screenshot_Setting_Time_Scheduling.png)

- **Time slot management**: auto-switch day/night presets by time (e.g., Eye Care at 18:00, Standard at 06:00); supports overnight ranges
- **Per-monitor independent scheduling**: assign different presets per monitor per time slot for fine-grained control of each screen's display
- **Manual/schedule smart switching**: manual adjustments override schedule; auto-resumes on next time slot change
- Support for saving, deleting, and resetting schedule configurations

#### 👁️ Eye Protection Mode

![Eye Protection](.github/Screenshot/Screenshot_Eye%20Protection.png)

- System-level window color overlay (Window / Background / AppWorkspace)
- 3 built-in preset colors (Mung Bean, Paper Yellow, Sky Blue) + custom color picker
- One-click restore to system defaults

#### 🔔 System Tray

![System Tray](.github/Screenshot/Screenshot_tray.png)

- Minimize to tray, no taskbar clutter
- **Quick preset switch**: submenu grouped by monitor (All / Individual), one-click Standard, Anti-Blue, Eye Care, Gaming
- Gamma toggle status display, check for updates, show main window, turn off monitor, exit

#### 🔄 Auto Update

- Silent check for new versions on GitHub Releases at startup, configurable in Settings
- Manual update check from tray menu

### 📥 Download

> 💡 **One-click run, no installation required!**

Go to the [Releases](https://github.com/SummerRay160/LumiShift/releases/latest) page and download `LumiShift.exe`. No installation required.

[![Download](https://img.shields.io/badge/📥-Download_Now-brightgreen?style=for-the-badge&logo=github)](https://github.com/SummerRay160/LumiShift/releases/latest)

### 📋 Requirements

| Requirement | Version / Notes |
|:---:|:---|
| **OS** | Windows 10 / 11 |
| **.NET Framework** | 4.8 (built into Windows 10 1903+) |
| **Monitor** | DDC/CI-capable (most modern monitors support this) |

### 🔨 Build

Open `LumiShift.sln` in Visual Studio 2022 and build in Release configuration.

```bash
msbuild LumiShift.sln /p:Configuration=Release
```

### 📁 Project Structure

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

### 🛠️ Tech Stack

[![.NET](https://img.shields.io/badge/.NET-4.8-purple?style=flat-square&logo=dotnet)]()
[![Windows Forms](https://img.shields.io/badge/Windows_Forms-UI-blue?style=flat-square&logo=windows)]()
[![WMI](https://img.shields.io/badge/WMI-DDC%2FCI-orange?style=flat-square)]()
[![GDI32](https://img.shields.io/badge/GDI32-Gamma-green?style=flat-square)]()
[![GitHub Actions](https://img.shields.io/badge/GitHub_Actions-CI/CD-success?style=flat-square&logo=github-actions)]()

| Feature | Technology |  |
|:---:|:---:|:---:|
| **Framework** | .NET Framework 4.8 / Windows Forms | ![.NET](https://img.shields.io/badge/.NET-4.8-purple?style=flat-square) |
| **Brightness Control** | WMI + DDC/CI (`dxva2.dll`) | ![WMI](https://img.shields.io/badge/WMI-DDC%2FCI-orange?style=flat-square) |
| **Gamma Correction** | GDI32 `SetDeviceGammaRamp` | ![GDI32](https://img.shields.io/badge/GDI32-Gamma-green?style=flat-square) |
| **Eye Protection** | User32 `SetSysColors` + Registry | 🔒 |
| **Night Light** | Registry `CloudStore` read/write | 🌙 |
| **Monitor Management** | EDID parsing + Win32 API | 🖥️ |
| **Auto Update** | GitHub Releases API | 🔄 |
| **CI/CD** | GitHub Actions (auto build + Release) | ✅ |

### 🤝 Contributing

Contributions are welcome! Please read the [Contributing Guide](CONTRIBUTING.md) for details.

[![Contributors](https://img.shields.io/github/contributors/SummerRay160/LumiShift?style=for-the-badge&logo=github)](https://github.com/SummerRay160/LumiShift/graphs/contributors)
[![PRs Welcome](https://img.shields.io/badge/PRs-welcome-brightgreen?style=for-the-badge&logo=github)](https://github.com/SummerRay160/LumiShift/pulls)

### 📜 License

This project is licensed under the [GPL-2.0 License](LICENSE).

[![License: GPL-2.0](https://img.shields.io/badge/license-GPL--2.0-red?style=for-the-badge&logo=gnu)](LICENSE)

---

<div align="center">

**⭐ If this project helps you, please give it a Star! ⭐**

Made with ❤️ by [SummerRay160](https://github.com/SummerRay160)

</div>
