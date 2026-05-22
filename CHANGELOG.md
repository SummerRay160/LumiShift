# 更新日志

## [1.1.0] - 2026-05-23

### 新增

- 多显示器独立 Gamma 调度配置功能，支持为每个显示器单独设置不同时段的 Gamma 预设
- 定时调度配置窗体（ScheduleConfigForm），支持多时段、多显示器独立预设配置
- PerDisplayGamma 和 ScheduleSegment 数据模型，重构用户设置调度结构
- 按显示器独立应用 Gamma 方法（ApplyGammaPerScreen）
- 预设定义类（PresetDefinitions），管理内置和自定义 Gamma 预设
- 设置迁移逻辑，兼容旧版调度配置自动升级
- 贡献指南文档（CONTRIBUTING.md）

### 变更

- 调整主窗体大小和布局，添加显示器选择和调度配置入口
- 调整设计常量 ContentWidth 适配新 UI 布局

### 移除

- 移除 README 中的 GitHub Release 徽章

---

## [1.0.0] - 2026-05-17

### 新增

- 多显示器亮度控制
- Gamma 校正与色温调节
- 护眼模式
- 定时调度
- 单文件运行，无需安装
