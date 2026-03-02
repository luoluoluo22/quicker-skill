# Quicker 内部模块与参数解析 (进阶规范)

## 1. 内部交互模块 (反射调用)
当 `IStepContext` 的标准 API 不足时，可通过深度反射调用 Quicker 内部模块。

### 1.1 悬浮 Toast (Success/Info/Warn/Error)
*   **核心库**: `ToastNotifications.dll`
*   **实例定位**: `Application.Current.MainWindow` 的混淆字段（当前版本为 `l6sHHaS3IZJ`）。
*   **调用方式**: 通过 `ToastNotifications.Messages` 下的扩展静态类（如 `SuccessExtensions`）的 `ShowXxx` 方法。
*   **示例代码**:
    ```csharp
    Application.Current.Dispatcher.Invoke(() => {
        var mw = Application.Current.MainWindow;
        var notifier = mw.GetType().GetField("l6sHHaS3IZJ", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(mw);
        var extType = AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => a.GetTypes())
            .First(t => t.FullName == "ToastNotifications.Messages.SuccessExtensions");
        // 调用 ShowSuccess(notifier, message)
        extType.GetMethod("ShowSuccess", new[] { notifier.GetType(), typeof(string) })
            .Invoke(null, new object[] { notifier, "操作成功！" });
    });
    ```

### 1.2 Windows 10+ 系统通知 (WindowsToast)
*   **实现位置**: `Quicker.Utilities.AppHelper.ShowWindowsToastMessage` (静态方法)。
*   **调用签名**: `ShowWindowsToastMessage(string message, string title, ...)`
*   **示例代码**:
    ```csharp
    var appHelper = AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => a.GetTypes())
        .First(t => t.FullName == "Quicker.Utilities.AppHelper");
    appHelper.GetMethod("ShowWindowsToastMessage", new[] { typeof(string), typeof(string) })
        .Invoke(null, new object[] { "消息内容", "标题" });
    ```

### 1.3 用户选择弹窗 (SelectOperationWindow)
*   **核心类**: `Quicker.View.SelectOperationWindow`
*   **数据模型**: `Quicker.View.SimpleOperationItem`
*   **构造签名**: `ctor(IList<SimpleOperationItem> operations, ShowWindowLocation location, bool useKeyboard, bool isMultiSelect, bool showFilter, double fontSize)`
*   **示例代码**:
    ```csharp
    Application.Current.Dispatcher.Invoke(() => {
        var assemblies = AppDomain.CurrentDomain.GetAssemblies();
        Type itemType = assemblies.SelectMany(a => a.GetTypes()).First(t => t.FullName == "Quicker.View.SimpleOperationItem");
        Type selectWinType = assemblies.SelectMany(a => a.GetTypes()).First(t => t.FullName == "Quicker.View.SelectOperationWindow");
        Type locType = assemblies.SelectMany(a => a.GetTypes()).First(t => t.FullName == "Quicker.Domain.ShowWindowLocation");

        // 创建选项列表
        var itemsList = Activator.CreateInstance(typeof(List<>).MakeGenericType(itemType));
        var addMethod = itemsList.GetType().GetMethod("Add");
        var item = Activator.CreateInstance(itemType);
        itemType.GetProperty("Name").SetValue(item, "[fa:Solid_Check:#28A745] 选项一");
        addMethod.Invoke(itemsList, new[] { item });

        // 弹出窗口
        var win = Activator.CreateInstance(selectWinType, new[] { itemsList, Enum.Parse(locType, "WithMouse1"), true, false, true, 14.0 });
        if ((bool)selectWinType.GetMethod("ShowDialog").Invoke(win, null)) {
            var selected = selectWinType.GetProperty("SelectedItem").GetValue(win);
            string name = itemType.GetProperty("Name").GetValue(selected) as string;
        }
    });
    ```

### 1.4 获取选中文本 (GetSelectedText)
*   **实现位置**: `Quicker.Utilities.AppHelper.GetSelectedText` (静态方法)。
*   **方法签名**: `string GetSelectedText(long repeatCount, TextDataFormat format, int waitMs, bool tryUiAuto)`
*   **示例代码**:
    ```csharp
    var appHelper = AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => a.GetTypes())
        .First(t => t.FullName == "Quicker.Utilities.AppHelper");
    var method = appHelper.GetMethod("GetSelectedText", new[] { typeof(long), typeof(TextDataFormat), typeof(int), typeof(bool) });
    string text = (string)method.Invoke(null, new object[] { 0L, TextDataFormat.UnicodeText, 250, false });
    ```

---

## 2. 运行时参数解析 (quicker_in_param)
URL 启动模式 (`runaction:ActionID?key=val`) 的参数合并在 `quicker_in_param` 变量中。

### 2.1 推荐解析算法
```csharp
string inParam = context.GetVarValue("quicker_in_param") as string;
if (!string.IsNullOrEmpty(inParam)) {
    foreach (var part in inParam.Split('&')) {
        var pair = part.Split('=');
        if (pair.Length == 2) {
            string key = pair[0].ToLower().Trim();
            string val = System.Net.WebUtility.UrlDecode(pair[1]);
            // 赋值逻辑...
        }
    }
}
```
