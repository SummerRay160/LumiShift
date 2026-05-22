# Contributing to LumiShift

[中文](#中文) · [English](#english)

---

## 中文

感谢你对 LumiShift 的关注！欢迎通过 Issue 和 Pull Request 为项目做出贡献。

### 报告问题

- 在 [Issues](https://github.com/SummerRay160/LumiShift/issues) 页面搜索是否已有相同问题
- 创建新 Issue，请包含以下信息：
  - Windows 版本和显示器型号
  - 问题复现步骤
  - 期望行为与实际行为
  - 如有错误信息，请附上完整内容

### 提交代码

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

### 代码规范

- 遵循 C# 命名约定（PascalCase 用于公共成员，camelCase 用于私有字段）
- 私有字段使用 `_` 前缀（如 `_gammaController`）
- 保持与现有代码风格一致，包括缩进、花括号位置等
- Win32 API 声明统一放在 `NativeMethods.cs` 中
- 新增功能尽量保持与现有 UI 风格一致

### 开发环境

- Visual Studio 2022 或更高版本
- .NET Framework 4.8 SDK
- Windows 10 / 11（部分功能依赖 Windows API，无法在其他平台运行）

---

## English

Thank you for your interest in LumiShift! Contributions via Issues and Pull Requests are welcome.

### Reporting Issues

- Search [Issues](https://github.com/SummerRay160/LumiShift/issues) to check if the problem already exists
- Create a new Issue with:
  - Windows version and monitor model
  - Steps to reproduce
  - Expected vs. actual behavior
  - Full error message if applicable

### Submitting Code

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

### Code Style

- Follow C# naming conventions (PascalCase for public members, camelCase for private fields)
- Use `_` prefix for private fields (e.g., `_gammaController`)
- Maintain consistency with existing code style (indentation, brace placement, etc.)
- Place Win32 API declarations in `NativeMethods.cs`
- Keep new features consistent with the existing UI style

### Development Environment

- Visual Studio 2022 or later
- .NET Framework 4.8 SDK
- Windows 10 / 11 (some features depend on Windows APIs and cannot run on other platforms)
