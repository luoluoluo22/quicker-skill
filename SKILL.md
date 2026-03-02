---
name: quicker-skill
description: 用于开发、部署和发布 Quicker 动作（Roslyn v2 引擎）。支持生成 JSON 配置、C# 逻辑代码和 Markdown 简介，并通过 PowerShell 调用 QuickerStarter.exe 进行本地构建或云端发布。
---

### 📁 技能内置资产 (Assets)
在 `quicker-skill/` 下提供了丰富的脚手架工具：
- `scripts/build_action.ps1`：自动调用编译器并解析日志的**一键构建脚本**。
- `scripts/reflection_tool.cs`：用于探测内部对象的泛型反射实验室。
- `templates/basic_action.json`：带输入/输出的标准快捷动作包裹容器。
- `templates/ui_action.cs`：集成了 WPF UI 线程调度、异常捕获并输出到 `reflection_log.txt` 的 C# 代码骨架。

# Quicker 动作开发技能 (quicker-skill)

## 目标
协助用户在 Windows 环境下按照 **普通模式 v2 (Roslyn)** 引擎规范，高效地开发、部署、发布和维护 Quicker 自动化动作。

## 指令集

### 1. 开发阶段（零样板架构）
- **文件生成**：在当前工作目录下生成三个关键文件，使用相同的基准文件名（BaseName）。
    - **JSON 配置 (.json)**：定义 `ActionId` (留空自动生成), `Title`, `Variables`, `Icon`, `Menus`, `References` 等。
    - **C# 逻辑 (.cs)**：纯逻辑代码，**严禁** 包含 `namespace` 或 `class`。
    - **简介文档 (.md)**：作为线上简介，建议命名为 `基准名_简介.md`。
- **变量操作**：
    - `context.GetVarValue("变量名")`：获取变量。
    - `context.SetVarValue("变量名", 值)`：设置变量。
- **入口函数**：必须是 `public static void Exec(Quicker.Public.IStepContext context)`。
- **窗口管理规范**：遵循 `references/window_guidelines.md` 中的规范，确保窗口能够成功激活、前置且具备交互完整性。

### 2. 执行命令（PowerShell）
所有命令均通过“QK 扳手”执行 (ID 见 `config.json` 中的 `wrench_action_id` 字段)。
**警告**：以下命令是与 Quicker 交互的唯一合法协议，严禁重构。
### Quicker 构建指令集合
- **本地一键构建 (Build & Verify)**：
  取代繁琐的手动调用，优先使用自带的一键构建脚本。它会自动启动异步编译并拦截执行结果，只需查看它的终端输出即可判定成功或报错。（注意：编译成功后仍会触发 Quicker 自动运行）。
  ```powershell
  & "f:\Desktop\kaifa\quicker-skill\.agent\skills\quicker-skill\scripts\build_action.ps1" -ActionId "{{wrench_id}}" -JsonPath "{{JSON绝对路径}}"
  ```
- **云端发布/更新 (Publish)**：
  ```powershell
  & "C:\Program Files\Quicker\QuickerStarter.exe" -c120 "runaction:{{wrench_id}}?action=publish&filePath=$([System.Net.WebUtility]::UrlEncode('{{JSON绝对路径}}'))" | Out-String
  ```

### 🏗️ 动作构建核心原则 (Minimalism)
为确保 `build` 命令成功并自动覆盖逻辑，必须严格遵守以下原则：
1. **JSON 极简设计**：`templates/basic_action.json` 中严禁包含 `Steps` 数组。它仅应定义元数据（ID、变量、图标等）。
2. **同名配对原则**：构建时，JSON 的文件名必须与 C# 核心逻辑脚本文件名完全一致且位于同一目录下。例如：`MyTask.json` 对应 `MyTask.cs`。
   - Quicker 构建器检测到此模式时，会自动将 `.cs` 内容填充到动作主步骤中，无需在 JSON 中声明 Steps。
3. **独立 ID**：新动作的 `ActionId`必须与 `config.json` 中的“扳手 ID”区分开（以免修改到扳手本身）。
4. **日志同名原则 (Local logging)**：为了让 `build_action.ps1` 能够准确捕获运行时的状态，C# 脚本中的 `logPath` 必须默认设置为与 JSON 文件**同目录且同名（.log后缀）**。

---

## 🛠️ 反射工具实验室 (Reflection Tooling)
可通过运行 `scripts/reflection_tool.cs` 并传入参数快速获取内部信息：
- **搜索**: `qreflect?action=search&keyword=Toast`
- **详细 Dump**: `qreflect?action=dump&type=Quicker.Utilities.AppHelper`- **更新简介 (Update Docs)**：
  ```powershell
  & "C:\Program Files\Quicker\QuickerStarter.exe" -c120 "runaction:{{wrench_id}}?action=update&filePath=$([System.Net.WebUtility]::UrlEncode('{{JSON绝对路径}}'))" | Out-String
  ```
- **运行动作**：
  ```powershell
  & "C:\Program Files\Quicker\QuickerStarter.exe" -c120 "runaction:{{生成的动作ID}}" | Out-String
  ```

## 变量类型代码 (Type)
- **0**: 文本 (String)
- **1**: 数字 (Double)
- **2**: 布尔 (Bool)
- **4**: 列表 (List)
- **12**: 整数 (Integer)
- **13**: 表格 (Table)

## 示例文件

### 1. JSON 配置示例 (`Demo.json`)
```json
{
  "ActionId": "",
  "SharedActionId": "00000000-0000-0000-0000-000000000000",
  "Title": "动作标题",
  "Description": "本地动作描述",
  "Keywords": "标签1,标签2;标签3",
  "ChangeLog": "v1.0.0 初始化版本",
  "ShareUrl": "",
  "Icon": "fa:Solid_Robot:#0080FF",
  "Variables": [
    {
      "Type": 0,
      "Key": "input_var",
      "DefaultValue": "默认值",
      "IsInput": true,
      "Desc": "输入变量说明"
    }
  ],
  "Menus": {
    "config": "[fa:Regular_Sun]设置"
  }
}
```

### 2. C# 逻辑示例 (`Demo.cs`)
```csharp
using System;
using System.Windows;
using Quicker.Public;

// Roslyn v2 零样板模式：直接编写代码，禁止 namespace/class
public static void Exec(IStepContext context)
{
    // 获取输入变量
    string input = context.GetVarValue("input_var") as string;
    
    // 逻辑处理
    string rtn = context.GetVarValue("rtn") as string ?? "";
    MessageBox.Show("你好！这是来自 Quicker 的弹窗！", "提示", MessageBoxButton.OK, MessageBoxImage.Information, MessageBoxResult.OK, MessageBoxOptions.DefaultDesktopOnly);
    rtn = "Success";
    context.SetVarValue("rtn", rtn);
}
```

## 约束条件
- **环境限制**：仅限 Windows 操作系统。
- **代码禁令**：C# 文件内绝对禁止出现 `namespace` 或 `class` 定义。
- **路径要求**：命令中的路径必须是绝对路径，并进行 URL 编码。
- **自动变量**：`text`, `rtn`, `errMessage`, `menuKey`, `silent` 会由构建器自动注入。
- **内部核心模块 (反射调用) & 获取选中文本**：包含 Toast, WindowsToast, SelectOperationWindow 及 GetSelectedText 的深度调用规则，详见 `references/internal_modules.md`。
- **内部 API 全量清单 (CSV)**：汇总所有探测到的内部窗体、服务及方法列表，见 `references/quicker_internal_apis.csv`。
- **URL 参数自动解析 (quicker_in_param)**：外部 URL 启动时的参数处理模式参见 `references/internal_modules.md`。
- **反射与内部模块探索**：混淆绕过、签名匹配等深度探索经验总结见 `references/reflection_discovery_experience.md`。
- **云端同步指南**：详细同步流程和配置参考 `references/cloud_sync_guide.md`。
- **窗口唤起规范**：详细规则参考 `references/window_guidelines.md`。
- **发布指南**：参考 `references/publishing_workflow.md`。
