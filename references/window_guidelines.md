# Quicker 窗口开发规范

为了确保 Quicker 动作在启动时具有良好的用户体验，必须遵循以下窗口管理规范：

## 1. 窗口唤起与焦点管理 (Focus & Activation)

**核心目标**：窗口在运行后必须立即显示在所有普通窗口的最前面，并获得键盘焦点，严禁出现“运行后窗口躲在后台”的情况。

### 推荐实现代码 (C#)

不要仅使用 `win.Show()` 或 `win.ShowDialog()`，应当使用以下组合拳确保窗口成功激活：

```csharp
public static void Exec(IStepContext context)
{
    Application.Current.Dispatcher.Invoke(() =>
    {
        var win = new YourWindow();
        
        // 1. 设置为最顶层以破开其他窗口的遮挡
        win.Topmost = true; 
        win.Show();
        
        // 2. 激活窗口以获得系统级输入焦点
        win.Activate(); 
        
        // 3. 立即取消置顶（避免遮挡用户后续操作，实现“正常打开”）
        win.Topmost = false; 
        
        // 4. 显式获取焦点
        win.Focus();
    });
}
```

## 2. 交互完整性 (Interactive Integrity)

- **必备关闭按钮**：所有自定义 UI 窗口（尤其是 `WindowStyle="None"` 的无边框窗口）**必须**在右上角显式提供关闭按钮。
- **拖动支持**：无边框窗口必须实现标题栏（或顶部区域）的 `DragMove()` 逻辑。
- **防止置底**：严禁将窗口初始化为后台状态或最小化状态（除非动作特殊要求）。

## 3. 兼容性约束 (Compatibility)

- **避免扩展方法定义冲突**：在 Quicker Roslyn 环境中，尽量避免定义 `public static class Extensions` 以防止与内置库冲突。推荐使用显式类型转换 `(Button)border.FindName("name")`。
- **UI 线程安全**：所有 UI 操作必须包装在 `Application.Current.Dispatcher.Invoke` 中。
