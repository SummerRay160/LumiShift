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

**[English](README.en.md) · [简体中文](#介绍) · [繁體中文](README.zh-Hant.md) · [🐛 问题反馈](https://github.com/SummerRay160/LumiShift/issues)**

</div>

---

## 介绍

LumiShift 是一款 Windows 平台上的开源屏幕调节工具。一个 exe 文件，下载就能用，不用安装。它把多显示器亮度、Gamma 校正、色温调节、护眼模式和定时切换这些事都装在了一起，让你的屏幕用起来更舒服。

---

## 🎯 功能特性

### 🖥️ 多显示器亮度调节

![亮度控制](.github/Screenshot/Screenshot_brightness.png)

接了好几台显示器？LumiShift 会自动认出每一台，你直接拖滑块就能单独调每台的亮度，再也不用去摸显示器上那些难按的按钮了。

- 自动识别所有连接的显示器
- 拖一下滑块就调好，会自动保存
- 显示器热插拔不用重启程序，会自动同步

### 🎨 Gamma 与色温

![Gamma 校正](.github/Screenshot/Screenshot_Gamma.png)

不管你是想精细调 R/G/B 三通道、Gamma 值和主亮度，还是只想简单拖一个滑块把屏幕变暖一点，这里都能搞定。

**显示方案是什么？** 简单说就是「一套调好的参数」，存下来下次一键切回去，不用每次重新拖滑块。

- **统一方案**：一套参数同步到所有显示器。想让每个屏幕看起来都一样？选这个就对了
- **多屏方案**：把每台显示器各自的参数打包存成一个方案。比如主屏要鲜艳、副屏要护眼，存成多屏方案后一键整套切换，不用一台一台调
- 内置了标准、防蓝光、护眼模式、游戏模式四个预设，也可以把自己的调法存成自定义方案

**多显示器怎么管？**

- 下拉框选「所有显示器」时，调整会同步到每一台
- 选具体某台显示器时，只影响那一台，其他屏不动
- 没单独调过的显示器会显示「跟随全部」，意思是它跟着全局参数走；一旦你单独调过，就变成「单独设置」，跟其他屏脱钩
- 想让某台显示器重新跟着全局走？选中它点「跟随全部」按钮，独立配置会被清掉

> 💡 **小提示**：从单台切回「所有显示器」时会以主显示器参数为准覆盖全部，记得先确认。勾上「只调节色温」可以省掉 R/G/B 三通道，只剩一个冷暖滑块，简单粗暴。

### ⏰ 定时调度

![定时调度配置](.github/Screenshot/Screenshot_Setting_Time_Scheduling.png)

白天用标准模式，晚上自动切护眼模式？交给定时调度就行，到点自动切，不用你操心。

- 想几点切就几点切，跨午夜时段也支持（比如 22:00 – 06:00）
- 每个时段到了自动切换对应的预设方案
- 多显示器可以为每个时段指定不同方案，或者直接套用多屏方案
- 顶部时间轴一眼看到全天安排，时段重叠会自动标红提醒
- 白天临时手动调一下也没事，下个时段开始时会自动恢复定时

### 👁️ 护眼模式

![护眼模式](.github/Screenshot/Screenshot_Eye%20Protection.png)

把系统窗口颜色换成柔和的护眼色，长时间看文档也不那么累眼。

- 内置三套预设色：绿豆沙、纸页黄、天空蓝
- 不喜欢预设？自己挑一个喜欢的颜色

### 🔔 系统托盘

![系统托盘](.github/Screenshot/Screenshot_tray.png)

最小化到托盘后不占任务栏，右键菜单里能快速干这些事：

- 按显示器分组快速切换预设（标准 / 防蓝光 / 护眼 / 游戏）
- 开关 Gamma、检查更新、显示主界面
- 关闭显示器、退出程序

### ⚙️ 其他设置

![设置](.github/Screenshot/Screenshot_Setting.png)

- **开机自启**：开机就启动，不用每次手动开
- **启动时最小化到托盘**：不打扰你工作
- **退出时还原 Gamma**：关程序时把屏幕恢复成原始状态
- **通知偏好**：启动、定时切换、状态变更、显示器变更这几类通知想开哪条开哪条，也有一键总开关
- **自动检查更新**：默认开启，嫌烦可以关掉

### 🔄 自动更新

启动时静默检查 GitHub Releases 上的新版本，也可以从托盘菜单手动检查。

---

## 📥 下载安装

前往 [Releases](https://github.com/SummerRay160/LumiShift/releases/latest) 页面下载最新版 `LumiShift.exe`，双击就能运行，无需安装。

[![Download](https://img.shields.io/badge/📥-立即下载-brightgreen?style=for-the-badge&logo=github)](https://github.com/SummerRay160/LumiShift/releases/latest)

## 📋 系统要求

| 要求 | 版本 / 说明 |
|:---:|:---|
| **操作系统** | Windows 10 / 11 |
| **.NET Framework** | 4.8（Windows 10 1903+ 已内置） |
| **显示器** | 支持 DDC/CI（大多数现代显示器都支持） |

## 🔨 编译

使用 Visual Studio 2022 打开 `LumiShift.sln`，选择 Release 配置编译。

```bash
msbuild LumiShift.sln /p:Configuration=Release
```

## 📁 项目结构

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

## 🛠️ 技术栈

[![.NET](https://img.shields.io/badge/.NET-4.8-purple?style=flat-square&logo=dotnet)]()
[![Windows Forms](https://img.shields.io/badge/Windows_Forms-UI-blue?style=flat-square&logo=windows)]()
[![WMI](https://img.shields.io/badge/WMI-DDC%2FCI-orange?style=flat-square)]()
[![GDI32](https://img.shields.io/badge/GDI32-Gamma-green?style=flat-square)]()
[![GitHub Actions](https://img.shields.io/badge/GitHub_Actions-CI/CD-success?style=flat-square&logo=github-actions)]()

| 功能 | 技术 |
|:---:|:---:|
| **框架** | .NET Framework 4.8 / Windows Forms |
| **亮度控制** | WMI + DDC/CI (`dxva2.dll`) |
| **Gamma 校正** | GDI32 `SetDeviceGammaRamp` |
| **护眼模式** | User32 `SetSysColors` + 注册表 |
| **夜间模式** | 注册表 `CloudStore` 读写 |
| **显示器管理** | EDID 解析 + Win32 API |
| **自动更新** | GitHub Releases API |
| **CI/CD** | GitHub Actions（自动编译 + Release） |

## 📜 许可证

本项目基于 [GPL-2.0 License](LICENSE) 开源。

[![License: GPL-2.0](https://img.shields.io/badge/license-GPL--2.0-red?style=for-the-badge&logo=gnu)](LICENSE)

---

<div align="center">

**⭐ 如果这个项目对你有帮助，请给一个 Star 支持一下！ ⭐**

Made with ❤️ by [SummerRay160](https://github.com/SummerRay160)

</div>
