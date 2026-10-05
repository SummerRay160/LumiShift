<div align="center">

<img src=".github/Screenshot/app.png" width="96" alt="LumiShift">

# LumiShift

**輕量級螢幕亮度與色溫校正工具 — 讓你的螢幕更護眼**

[![Platform](https://img.shields.io/badge/platform-Windows-0078D6?style=for-the-badge&logo=windows&logoColor=white)]()  [![Release](https://img.shields.io/github/v/release/SummerRay160/LumiShift?style=for-the-badge&label=最新版本)](https://github.com/SummerRay160/LumiShift/releases/latest)  [![Downloads](https://img.shields.io/github/downloads/SummerRay160/LumiShift/total?style=for-the-badge&color=brightgreen)](https://github.com/SummerRay160/LumiShift/releases)  [![GitHub Stars](https://img.shields.io/github/stars/SummerRay160/LumiShift?style=for-the-badge&logo=github)](https://github.com/SummerRay160/LumiShift/stargazers)

**[English](README.en.md) · [简体中文](README.md) · [繁體中文](#介紹) · [問題回報](https://github.com/SummerRay160/LumiShift/issues)**

</div>

---

## 介紹

LumiShift 是一款在 Windows 上執行的開源螢幕調節工具。無需安裝，下載即可執行。它把多顯示器亮度調節、Gamma 校正、色溫調節、護眼模式和定時切換整合在一起，讓長時間使用螢幕的體驗更舒適。

---

## 🎯 功能特性

### 🖥️ 多顯示器亮度調節

![亮度控制](.github/Screenshot/Screenshot_brightness.png)

接了好幾台顯示器？LumiShift 會自動認出每一台，你直接拖滑桿就能單獨調每台的亮度，再也不用去摸顯示器上那些難按的按鈕了。

### 🎨 Gamma 與色溫

![Gamma 校正](.github/Screenshot/Screenshot_Gamma.png)

既可以精細調節 R/G/B 三通道、Gamma 值和主亮度，也可以只拖動一個冷暖滑桿，快速把螢幕調暖或調冷。

**顯示方案**：把目前調好的參數儲存下來，之後一鍵即可恢復，不必每次重新調整。

- **統一方案**：目前的色溫調節會同步套用到所有顯示器
- **多屏方案**：為每台顯示器分別儲存參數並組合成一個方案。例如主屏色彩鮮豔、副屏偏護眼，切換時整套套用，不需要逐台調整
- 內建標準、防藍光、護眼模式、遊戲模式四個預設，也支援把自訂參數儲存為方案

**多顯示器管理規則**

- 選擇 **所有顯示器** 時，調整會同步套用到每一台
- 選擇具體某台顯示器時，只影響這一台，其他顯示器保持不變
- 未單獨調整過的顯示器顯示為 **跟隨全部** 狀態，跟隨全域參數；一旦單獨調整，就轉為 **單獨設定** 狀態，與全域參數脫鉤
- 想讓某台顯示器重新跟隨全域參數，選中它點擊 **跟隨全部** 按鈕，獨立配置會被清除

> 💡 **小提示**：從單台顯示器切回所有顯示器時，會以主顯示器的參數為準覆蓋其餘顯示器，操作前請先確認。R/G/B 三通道與 γ 等精細參數預設收在 **進階調參** 折疊區，需要時展開即可。

### ⏰ 定時排程

![定時排程配置](.github/Screenshot/Screenshot_Setting_Time_Scheduling.png)

白天使用標準模式，晚上自動切換護眼模式，設定好時段後由程式按時間自動執行。

- 時段起止時間自由設定，支援跨午夜的時段（例如 22:00 – 06:00）
- 每個時段開始時自動切換到對應的預設方案
- 多顯示器可以為每個時段指定不同方案，或直接套用多屏方案
- 頂部時間軸展示全天安排，時段重疊會標紅提醒
- 白天臨時手動調整不影響排程，下個時段開始時會自動恢復

### 👁️ 護眼模式

![護眼模式](.github/Screenshot/Screenshot_Eye%20Protection.png)

將系統視窗顏色替換為柔和的護眼配色，長時間閱讀文件時眼睛更不容易疲勞。

---

## 📥 下載安裝

前往 [Releases](https://github.com/SummerRay160/LumiShift/releases/latest) 頁面下載最新版 `LumiShift.exe`。

[![Download](https://img.shields.io/badge/📥-立即下載-brightgreen?style=for-the-badge&logo=github)](https://github.com/SummerRay160/LumiShift/releases/latest)

## 📋 系統需求

| 需求 | 版本 / 說明 |
|:---:|:---|
| **作業系統** | Windows 10 / 11 |
| **.NET Framework** | 4.8（Windows 10 1903+ 已內建） |
| **顯示器** | 支援 DDC/CI |

## 🔨 編譯

使用 Visual Studio 2022 開啟 `LumiShift.sln`，選擇 Release 設定編譯。

```bash
msbuild LumiShift.sln /p:Configuration=Release
```

<div align="center">

**⭐ 如果這個專案對你有幫助，請給一個 Star 支持一下！ ⭐**

Made with ❤️ by [SummerRay160](https://github.com/SummerRay160)

</div>
