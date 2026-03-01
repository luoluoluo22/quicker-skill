# Quicker 内部提示消息与参数解析 (进阶规范)

## 1. 内部提示框 (反射调用)
当 `IStepContext.ShowToast` 不可用或样式受限时，可以通过深度反射调用 Quicker 内部的提示模块。

### 1.1 悬浮 Toast (Success/Info/Warn/Error)
*   **核心库**: `ToastNotifications.dll`
*   **实例定位**: `Application.Current.MainWindow` 下被混淆的私有字段。由于混淆，字段名会随版本变化（当前版本为 `l6sHHaS3IZJ`）。
*   **调用逻辑**:
    1.  从 `MainWindow` 获取 `Notifier` 实例。
    2.  定位 `ToastNotifications.Messages` 命名空间下的扩展静态类（如 `SuccessExtensions`, `InformationExtensions`）。
    3.  通过反射调用其静态方法（如 `ShowSuccess`, `ShowInformation`），传入 `Notifier` 实例和消息字符串。
*   **示例代码结构**:
    ```csharp
    var notifier = mw.GetType().GetField("l6sHHaS3IZJ", flags).GetValue(mw);
    var extType = assemblies.FirstOrDefault(t => t.FullName == "ToastNotifications.Messages.SuccessExtensions");
    extType.GetMethod("ShowSuccess").Invoke(null, new object[] { notifier, "Message content" });
    ```

### 1.2 Windows 10+ 系统通知 (WindowsToast)
*   **实现位置**: `Quicker.Utilities.AppHelper.ShowWindowsToastMessage` (静态方法)。
*   **调用签名**:
    - `ShowWindowsToastMessage(string message, string title, ...)`
*   **优点**: 消息会出现在操作中心的通知历史中，适合重要提醒。

---

## 2. 运行时参数解析 (quicker_in_param)
通过 URL 模式启动动作（如 `runaction:ActionID?key=val`）时，所有参数会被合并到一个隐式变量 `quicker_in_param` 中。

### 2.1 推荐解析算法
```csharp
string inParam = context.GetVarValue("quicker_in_param") as string;
if (!string.IsNullOrEmpty(inParam)) {
    foreach (var part in inParam.Split('&')) {
        var pair = part.Split('=');
        if (pair.Length == 2) {
            string key = pair[0].ToLower().Trim();
            // 注意需要进行 URL 解码
            string val = System.Net.WebUtility.UrlDecode(pair[1]);
            // 根据 key 进行变量赋值...
        }
    }
}
```
*   **注意**: 即使 JSON 中定义了 `IsInput` 变量，某些启动方式（如外部 URL 唤起）仍需通过此模式手动解析以确保兼容性。
