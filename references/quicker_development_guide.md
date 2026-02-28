# Quicker 动作开发指南 (Roslyn v2 专修版)

## 角色定位
你是 Quicker（Windows 自动化工具）的专业开发者。你遵循"零样板"架构，专注于 **普通模式 v2 (Roslyn)** 引擎。
**目标**：生成精简的配置清单（.json）和纯逻辑的 C# 代码文件（.cs），然后编译并立即测试动作。

---

## 一、文件规范

你必须在工作区生成 **三个文件**。它们必须使用完全相同的基准文件名（BaseName）。

### 文件 A：动作配置清单 (.json)

此文件定义元数据、变量和菜单。

| 字段             | 说明                                                                                                                              |
| ---------------- | --------------------------------------------------------------------------------------------------------------------------------- |
| `ActionId`       | 本地动作 ID（InternalId）。保持为空 `""` 会自动生成。                                                                             |
| `SharedActionId` | 云端动作 ID。首次发布后自动填入，用于后续更新。                                                                                   |
| `Title`          | 动作标题                                                                                                                          |
| `Description`    | 动作（本地）描述。仅在本地显示。                                                                                                  |
| `Keywords`       | **(新)** 搜索关键词/标签。仅用于发布。                                                                                            |
| `ChangeLog`      | **(新)** 版本更新说明。仅用于发布更新。                                                                                           |
| `ShareUrl`       | 分享链接（自动回填）                                                                                                              |
| `Icon`           | 图标，格式：`fa:图标名:#颜色`（例：`fa:Solid_Robot:#0080FF`）                                                                     |
| `Variables`      | 定义动作变量。关键字段：`Type`(类型代码), `Key`(变量名), `DefaultValue`(默认值), `IsInput`(作为参数输入), `SaveState`(记忆上次值) |
| `Menus`          | (可选) 定义右键菜单。格式: `{ "Key": "标题" }`。构建器会自动生成 `menuKey` 变量供 C# 读取。                                       |

### 文件 B：C# 逻辑文件 (.cs)
（见下文代码规则）

### 文件 C：动作简介文档 (.md)
这是一个 Markdown 文件，用于作为动作的 **线上简介**。
*   文件名约定：`基准名_简介.md`（推荐） 或 `基准名.md`。
*   此文件内容不会影响本地动作运行，仅在执行 `update` (更新简介) 命令时被读取并上传。

#### 变量类型对照表 (Type)
|  代码  | 类型 (Type)         | 说明                       |
| :----: | ------------------- | -------------------------- |
| **0**  | **文本 (String)**   | 最常用。默认值填字符串。   |
| **1**  | **数字 (Double)**   | 浮点数/双精度小数。        |
| **2**  | **布尔 (Bool)**     | `true` 或 `false`。        |
| **3**  | 图片 (Image)        |                            |
| **4**  | 列表 (List)         | 每行一个元素。             |
| **6**  | 日期时间 (DateTime) |                            |
| **10** | 词典 (Dict)         | 键值对，格式 `key:value`。 |
| **12** | **整数 (Integer)**  | 纯整数。                   |
| **13** | 表格 (Table)        |                            |
| **99** | 动态对象 (Object)   |                            |

#### 自动注入变量 (无需手动定义)
构建器会自动注入以下标准变量，请直接在 C# 中使用：
- `text` (String): 结果文本
- `rtn` (String): 返回值
- `errMessage` (String): 错误信息
- `menuKey` (String): 菜单点击的 Key (对应 Menus 中的 Key)
- `silent` (Bool): 静默启动 (默认 true)

#### JSON 示例
```json
{
  "ActionId": "",
  "SharedActionId": "00000000-0000-0000-0000-000000000000",
  "Title": "简单弹窗演示",
  "Description": "这是一个由 AI 生成的简单弹窗示例动作。",
  "Keywords": "演示,弹窗,示例",
  "ChangeLog": "v1.0.0 初始化版本",
  "ShareUrl": "https://getquicker.net/SharedAction?code=...",
  "Icon": "fa:Solid_CommentDots:#FF8000",
  "Variables": [{
      "Type": 0,
      "Desc": "结果文本(自动)",
      "IsOutput": false,
      "DefaultValue": null,
      "Key": "text"
    }],
  "Menus": {
    "test_menu": "[fa:Light_Flag]菜单标题(tooltip内容)"
  },
  "References": [ "Microsoft.Web.WebView2.Wpf.dll"]
}
```

---

### 文件 B：C# 逻辑文件 (.cs)

核心规则：**零样板（Zero-Boilerplate）**
- 【绝对禁令】：文件内**严禁**包含 `namespace` 或 `class` 定义。
- 【Roslyn 优势】：支持 C# 7.0+ 语法（如 Lambda、元组、模式匹配等）。

#### 1. 入口函数签名
必须为 `public static` 且方法名为 `Exec`：
- 无返回值：`public static void Exec(Quicker.Public.IStepContext context)`
- 有返回值：`public static [类型] Exec(Quicker.Public.IStepContext context)`

#### 2. 多线程建议
| 场景                      | 推荐线程                  |
| ------------------------- | ------------------------- |
| 纯数据/文件处理           | 后台线程 (MTA) - 默认推荐 |
| 剪贴板/COM/简单 Winform   | 后台线程 (STA)            |
| WPF 复杂界面/ShowDialog() | UI 线程/前台线程          |

#### 3. 依赖引用
- 第三方 DLL：`//css_reference DLL路径;`
- 系统库：直接 `using`

#### 4. context 核心方法
- `context.GetVarValue("变量名")` → 返回 `object`
- `context.SetVarValue("变量名", 值)` → 写回动作变量

#### C# 示例 1：基础弹窗
```csharp
using System;
using System.Windows;
using Quicker.Public;

public static void Exec(IStepContext context)
{
    MessageBox.Show("你好！这是来自 Quicker 的弹窗！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
    context.SetVarValue("rtn", "Success");
}
```

#### C# 示例 2：Linq 与现代语法
```csharp
using System;
using System.Linq;
using Quicker.Public;

public static void Exec(IStepContext context)
{
    var list = new[] { 1, 2, 3, 4, 5 };
    var result = list.Where(x => x > 2).Sum();
    context.SetVarValue("rtn", $"计算结果: {result}");
}
```

#### C# 示例 3：带返回值（STA 线程场景）
```csharp
using System;
using System.Windows;
using Quicker.Public;

// 涉及剪贴板时，请在动作设置中选择 STA 线程
public static string Exec(IStepContext context)
{
    string text = Clipboard.GetText();
    return "剪贴板长度: " + text.Length;
}

#### C# 示例 4：处理菜单事件
```csharp
using System;
using System.Windows.Forms;
using Quicker.Public;

public static void Exec(IStepContext context)
{
    // 从自动变量 menuKey 获取点击的菜单键
    string key = context.GetVarValue("menuKey") as string;
    
    if (key == "config")
    {
        MessageBox.Show("点击了设置菜单");
    }
    else
    {
        MessageBox.Show("正常运行");
    }
}
```
```

---

## 二、执行命令

**元动作构建器 ID**：见 `config.json` 中的 `wrench_action_id` 字段（不可更改）。

### 参数格式
所有命令使用统一的参数格式：`action=xxx&filePath=yyy&shareUrl=zzz`

| action    | 功能                 | 必需参数                                                                               |
| --------- | -------------------- | -------------------------------------------------------------------------------------- |
| `build`   | 构建动作（本地部署） | `filePath` (JSON配置路径)                                                              |
| `publish` | 首次发布到云端       | `filePath` (JSON配置路径)                                                              |
| `update`  | 更新线上简介         | 可选: `shareUrl` + `filePath` (MD路径) <br> **或** `filePath` (JSON配置路径, 智能推断) |
| `read`    | 读取线上简介         | `shareUrl`                                                                             |


### 构建命令（本地部署）
```powershell
Start-Process "C:\Program Files\Quicker\QuickerStarter.exe" -ArgumentList "-c `"runaction:{{wrench_id}}?action=build&filePath=$([System.Net.WebUtility]::UrlEncode('{{你的JSON文件路径}}'))`""
```

### 发布/更新动作命令 (Publish)
**一键发布**：首次运行为新建分享，后续运行为更新分享（基于 SharedActionId）。
```powershell
Start-Process "C:\Program Files\Quicker\QuickerStarter.exe" -ArgumentList "-c `"runaction:{{wrench_id}}?action=publish&filePath=$([System.Net.WebUtility]::UrlEncode('{{你的JSON文件路径}}'))`""
```

### 更新动作简介命令 (Update Docs)
**智能推断**：只需提供 JSON 路径，构建器会自动：
1. 从 JSON 中读取 `ShareUrl`。
2. 自动寻找同名 MD 文件（优先级：`文件名_简介.md` > `文件名.md`）。

```powershell
Start-Process "C:\Program Files\Quicker\QuickerStarter.exe" -ArgumentList "-c `"runaction:{{wrench_id}}?action=update&filePath=$([System.Net.WebUtility]::UrlEncode('{{你的JSON文件路径}}'))`""
```
*(也可以显式提供 `shareUrl` 参数，如旧版命令)*

### 读取动作简介命令 (Read)
```powershell
Start-Process "C:\Program Files\Quicker\QuickerStarter.exe" -ArgumentList "-c `"runaction:3eebe8d9-7521-46fa-b2e1-502754bce14f?action=read&shareUrl=$([System.Net.WebUtility]::UrlEncode('{{动作分享后的url}}'))`""
```

### 运行已构建动作
```powershell
Start-Process "C:\Program Files\Quicker\QuickerStarter.exe" -ArgumentList '-c "runaction:{{生成的动作ID}}"'
```

---

## 四、高级技巧与避坑指南

### 1. 环境变量与注册表操作
*   **秒级保存**：直接操作注册表 (`Microsoft.Win32.Registry`) 比调用 `Environment.SetEnvironmentVariable` 性能高得多，因为后者会强制阻塞式向全系统发送刷新广播。
*   **系统通知同步**：操作完注册表后，**必须**异步发送 `WM_SETTINGCHANGE` 广播（使用 `user32.dll` 的 `SendMessageTimeout`），否则资源管理器或其他第三方程序无法识别环境变化。
*   **变量名规范**：尽量使用 **全大写** 变量名。部分系统和脚本对全小写环境变量支持较差，可能导致识别失败。
*   **环境继承陷阱**：子进程会继承父进程（Quicker）启动时的环境快照。如果在动作运行期间修改了系统变量，直接启动子进程可能拿不到新值。测试时应开启全新的命令行窗口。

### 2. 异步操作与 UI 响应
*   **防 UI 冻结**：长耗时操作（如注册表密集写、网络备份/恢复）必须包裹在 `Task.Run` 中执行。
*   **加载反馈**：异步任务期间建议显示 Loading 遮罩。
*   **Dispatcher 调用**：在异步任务中修改 ObservableCollection 或更新 UI 控件属性，必须使用 `Dispatcher.Invoke` 回到主线程。

### 3. UI 交互提示 (Toast vs MessageBox)
*   **稳定性优先**：在复杂的 WPF 窗口逻辑中，`context.ShowToast` 或全局静态 `App.ShowToast` 可能会因为作用域、SDK 版本或依赖冲突导致编译失败（如 `CS0103` 或 `CS1061`）。
*   **稳健回退**：如果 Toast 报错，最稳健的方案是使用 WPF 原生的 `MessageBox.Show`。
*   **交互优化**：简单的“复制成功”提示建议使用 Toast 以保持流程连贯；涉及“警告/确认”或“操作指引”时建议使用 MessageBox。

### 4. 依赖项管理
*   **Json 解析**：Quicker 默认携带 `Newtonsoft.Json`，无需额外手工引用 DLL。
*   **动态反射**：如果需要调用 Quicker 内部未暴露的私有 API（如 `CloudState`），可使用动态反射机制。

### 5. 云同步开发技巧
*   **数据隔离**：建议在云端存储时使用特定的 Key 前缀（如 `ActionName_Backup`），避免数据碰撞。
*   **版本兼容性**：存储数据时建议携带时间戳和机器名字段，方便在多设备同步时进行冲突判断。
*   **安全保护**：云同步操作通常涉及用户数据隐私。在执行“恢复”操作前，**必须**弹出二次确认窗口（如下面演示的 `SyncRestoreWindow`），让用户手动勾选需要覆盖的项，防止意外丢失本地配置。
*   **原子性**：建议先将所有待恢复变量在内存中处理完毕后，再一次性写入注册表，减少系统广播次数。

> **延伸阅读**：关于反射调用云端接口的底层细节与完整代码示例，请参阅专用文档：[云同步专研指南](./cloud_sync_guide.md)
