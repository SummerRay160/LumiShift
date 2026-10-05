<div align="center">

<img src=".github/Screenshot/app.png" width="96" alt="LumiShift">

# LumiShift

**轻量级屏幕亮度与色温校正工具 — 让你的屏幕更护眼**

[![Platform](https://img.shields.io/badge/platform-Windows-0078D6?style=for-the-badge&logo=windows&logoColor=white)]()  [![Release](https://img.shields.io/github/v/release/SummerRay160/LumiShift?style=for-the-badge&label=最新版本)](https://github.com/SummerRay160/LumiShift/releases/latest)  [![Downloads](https://img.shields.io/github/downloads/SummerRay160/LumiShift/total?style=for-the-badge&color=brightgreen)](https://github.com/SummerRay160/LumiShift/releases)  [![GitHub Stars](https://img.shields.io/github/stars/SummerRay160/LumiShift?style=for-the-badge&logo=github)](https://github.com/SummerRay160/LumiShift/stargazers)

**[English](README.en.md) · [简体中文](#介绍) · [繁體中文](README.zh-Hant.md) · [问题反馈](https://github.com/SummerRay160/LumiShift/issues)**

</div>

---

## 介绍

LumiShift 是一款运行在 Windows 上的开源屏幕调节工具。无需安装，下载即可运行。它把多显示器亮度调节、Gamma 校正、色温调节、护眼模式和定时切换整合在一起，让长时间使用屏幕的体验更舒适。

---

## 🎯 功能特性

### 🖥️ 多显示器亮度调节

![亮度控制](.github/Screenshot/Screenshot_brightness.png)

连接多台显示器时，LumiShift 会自动识别每一台，拖动滑块即可单独调节各显示器的亮度，不必再去按显示器机身上的物理按键。



### 🎨 Gamma 与色温

![Gamma 校正](.github/Screenshot/Screenshot_Gamma.png)

既可以精细调节 R/G/B 三通道、Gamma 值和主亮度，也可以只拖动一个冷暖滑块，快速把屏幕调暖或调冷。

**显示方案**：把当前调好的参数保存下来，之后一键即可恢复，不必每次重新调整。

- **统一方案**：当前的色温调节会同步应用到所有显示器
- **多屏方案**：为每台显示器分别保存参数并组合成一个方案。例如主屏色彩鲜艳、副屏偏护眼，切换时整套应用，不需要逐台调整
- 内置标准、防蓝光、护眼模式、游戏模式四个预设，也支持把自定义参数保存为方案

**多显示器管理规则**

- 选择 **所有显示器** 时，调整会同步应用到每一台
- 选择具体某台显示器时，只影响这一台，其他显示器保持不变
- 未单独调整过的显示器显示为 **跟随全部** 状态，跟随全局参数；一旦单独调整，就转为 **单独设置** 状态，与全局参数脱钩
- 想让某台显示器重新跟随全局参数，选中它点击 **跟随全部** 按钮，独立配置会被清除

> 💡 **小提示**：从单台显示器切回所有显示器时，会以主显示器的参数为准覆盖其余显示器，操作前请先确认。R/G/B 三通道与 γ 等精细参数默认收在 **进阶调参** 折叠区，需要时展开即可。

### ⏰ 定时调度

![定时调度配置](.github/Screenshot/Screenshot_Setting_Time_Scheduling.png)

白天使用标准模式，晚上自动切换护眼模式，设置好时段后由程序按时间自动执行。

- 时段起止时间自由设定，支持跨午夜的时段（例如 22:00 – 06:00）
- 每个时段开始时自动切换到对应的预设方案
- 多显示器可以为每个时段指定不同方案，或直接应用多屏方案
- 顶部时间轴展示全天安排，时段重叠会标红提醒
- 白天临时手动调整不影响调度，下个时段开始时会自动恢复

### 👁️ 护眼模式

![护眼模式](.github/Screenshot/Screenshot_Eye%20Protection.png)

将系统窗口颜色替换为柔和的护眼配色，长时间阅读文档时眼睛更不容易疲劳。

---

## 📥 下载安装

前往 [Releases](https://github.com/SummerRay160/LumiShift/releases/latest) 页面下载最新版 `LumiShift.exe`。

[![Download](https://img.shields.io/badge/📥-立即下载-brightgreen?style=for-the-badge&logo=github)](https://github.com/SummerRay160/LumiShift/releases/latest)

## 📋 系统要求

| 要求 | 版本 / 说明 |
|:---:|:---|
| **操作系统** | Windows 10 / 11 |
| **.NET Framework** | 4.8（Windows 10 1903+ 已内置） |
| **显示器** | 支持 DDC/CI |

## 🔨 编译

使用 Visual Studio 2022 打开 `LumiShift.sln`，选择 Release 配置编译。

```bash
msbuild LumiShift.sln /p:Configuration=Release
```

<div align="center">

**⭐ 如果这个项目对你有帮助，请给一个 Star 支持一下！ ⭐**

Made with ❤️ by [SummerRay160](https://github.com/SummerRay160)

</div>
