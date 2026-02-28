---
name: quicker-skill
description: 用于开发、部署和发布 Quicker 动作（Roslyn v2 引擎）。支持生成 JSON 配置、C# 逻辑代码和 Markdown 简介，并通过 PowerShell 调用 QuickerStarter.exe 进行本地构建或云端发布。
---

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
- **本地构建 (Build)**：
  ```powershell
  Start-Process "C:\Program Files\Quicker\QuickerStarter.exe" -ArgumentList "-c `"runaction:{{wrench_id}}?action=build&filePath=$([System.Net.WebUtility]::UrlEncode('{{JSON绝对路径}}'))`""
  ```
- **云端发布/更新 (Publish)**：
  ```powershell
  Start-Process "C:\Program Files\Quicker\QuickerStarter.exe" -ArgumentList "-c `"runaction:{{wrench_id}}?action=publish&filePath=$([System.Net.WebUtility]::UrlEncode('{{JSON绝对路径}}'))`""
  ```
- **更新简介 (Update Docs)**：
  ```powershell
  Start-Process "C:\Program Files\Quicker\QuickerStarter.exe" -ArgumentList "-c `"runaction:{{wrench_id}}?action=update&filePath=$([System.Net.WebUtility]::UrlEncode('{{JSON绝对路径}}'))`""
  ```
- **运行动作**：
  ```powershell
  Start-Process "C:\Program Files\Quicker\QuickerStarter.exe" -ArgumentList '-c "runaction:{{生成的动作ID}}"'
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
    MessageBox.Show("收到的输入是: " + input);
    
    // 设置回传变量 (rtn 是常用回传变量名)
    context.SetVarValue("rtn", "处理完成");
}
```

## 约束条件
- **环境限制**：仅限 Windows 操作系统。
- **代码禁令**：C# 文件内绝对禁止出现 `namespace` 或 `class` 定义。
- **路径要求**：命令中的路径必须是绝对路径，并进行 URL 编码。
- **自动变量**：`text`, `rtn`, `errMessage`, `menuKey`, `silent` 会由构建器自动注入，无需在 JSON 中重复定义繁琐参数，但需在 JSON `Variables` 中定义用于 UI 或存储的变量。
- **窗口唤起规范**：窗口运行后必须前置激活，严禁置底。详细规则参考 `references/window_guidelines.md`。

## 发布流程建议
1. 准备 6 字以内标题。
2. 痛点切入式简介：以用户第一视角描述解决了什么问题。
3. 关键词用分号 `;` 分割。
4. 提供详细的使用说明。
