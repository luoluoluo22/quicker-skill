# Quicker-Skill: AI 驱动的动作极速开发套件

本工具包通过 **Antigravity AI 技能 (Skill)** 与 **QK 扳手 (Quicker 动作)** 的深度联动，实现“对话即代码，部署即运行”的自动化开发体验。

---

## 🛠️ 安装与配置指南

### 第一步：安装 QK 扳手 (Quicker 端)
这是本技能在 Quicker 面板中的“执行端”，负责接收 AI 指令并进行本地构建。
- **动作名称**：QK 扳手
- **安装地址**：[点击安装集成助手](https://getquicker.net/Sharedaction?code=f10c350a-f5cf-4726-543d-08de40f9c963)
- **注意**：请确保 Quicker 客户端处于运行状态。

### 第二步：安装 quicker-skill (AI 技能端)
1. **下载技能包**：从本仓库下载 `.agent/skills/quicker-skill` 目录。
2. **放置位置**：将该目录复制到你当前 Antigravity 工作区的 `.agent/skills/` 路径下。
   - 路径示例：`F:\YourProject\.agent\skills\quicker-skill\`
3. **验证安装**：在对话中输入 `@quicker-skill` 确认 AI 已成功识别技能。

---

## 📖 核心使用场景与对话示例

### 1. 新建动作
你可以直接描述你想要的功能，AI 会自动为你生成图标、配置变量并调用 QK 扳手。
- **用户**：“帮我写一个 QK 动作，功能是读取剪贴板内容，把所有的空格替换成换行。”
- **AI**：(生成 `Replacement.json` 和 `Replacement.cs`) -> (调用 QK 扳手自动本地构建)

### 2. 本地测试与迭代
在本地构建后，你可以要求 AI 进行微调。
- **用户**：“把刚才的动作图标换成一个蓝色的搜索图标，并增加一个可以自定义替换字符的输入变量。”
- **AI**：(更新 JSON 配置) -> (重新推送构建)

### 3. 文档生成与云端发布
当动作开发完成后，AI 可以帮你整理简介并一键推送到 Quicker 动作库。
- **用户**：“这个动作很好用，帮我写个痛点切入式的简介，并发布上线。”
- **AI**：(生成 `_简介.md`) -> (调用 QK 扳手发布/更新分享)

### 4. 获取帮助
- **用户**：“我要怎么在 C# 代码里获取 Quicker 的变量？”
- **AI**：(基于 Skill 中的 `context.GetVarValue` 指南回答你的问题)

---

## � 技术规格
- **引擎版本**：Quicker 普通模式 v2 (Roslyn)。
- **代码架构**：零样板模式（Zero-Boilerplate），禁止 `namespace` 和 `class`。
- **自动化操作**：通过 `QuickerStarter.exe` 命令行参数实现静默构建。

---
*欲了解更详细的 C# 开发规范，请阅读同目录下的 `SKILL.md`。*
