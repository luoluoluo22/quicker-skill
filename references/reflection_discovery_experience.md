# Quicker 反射探索与逆向经验总结

本文件记录了在 Quicker 混淆环境下的深度反射探索过程及成功经验，为后续探索其他内部模块提供参考。

## 1. 核心挑战：混淆 (Obfuscation)
Quicker 的内部私有成员字段名（如 `l6sHHaS3IZJ`）随版本变化且无语义。

- **应对策略**: 
    - **避开名称，依赖类型**: 使用 `val.GetType().FullName` 进行匹配。
    - **程序集扫描**: 自研脚本遍历 `AppDomain.CurrentDomain.GetAssemblies()`，寻找特定关键词（如 "Toast" / "Notifier"）的程序集和类型。
    - **IsInstanceOfType**: 在 `MainWindow` 或 `App` 实例的所有字段中，利用 `notifierType.IsInstanceOfType(val)` 寻找活体实例。

## 2. 签名陷阱：委托工厂 (Delegate Factory)
许多现代 C# 库使用 `Func<T>` 委托或表达式树来延迟执行。

- **经验**: 不要仅搜索 `Show(string, string)` 这种简单参数的方法。如果反射调用失败，检查参数是否为 `Func<INotification>` 这种工厂模式。
- **解决方案**: 在反射环境下构建泛型委托极其复杂（易报 ContainsGenericParameters 错误）。**最优解**是直接利用相关库的静态**扩展方法** (Extension Methods)，它们签名固定且不包含复杂的运行时泛型解析。

## 3. 环境与线程约束
- **UI 线程同步**: 绝大多数反射操作（尤其是访问 `MainWindow` 或触发 UI 组件）必须运行在 `Application.Current.Dispatcher` 线程中。
- **构建结果验证**: Quicker 构建是异步的。必须捕获 `QuickerStarter.exe` 的输出流，硬核扫描 `error` 关键字，而非依赖进程退出码。

## 4. 探索工作流模板 (The Hunt Workflow)
1. **定位类型**: 寻找目标功能相关的程序集和基类/接口。
2. **定位实例**: 扫描核心全局对象（`App.Current`, `MainWindow`）的私有成员。
3. **获取成员信息**: 通过反射遍历可疑对象的方法名和参数类型，利用 `MessageBox` 或 `Exec` 返回值输出信息。
4. **验证调用**: 优先寻找静态扩展方法进行功能验证。

## 5. 关键发现记录
- **ToastNotifications 系统**: 实例隐藏在 `MainWindow` 的随机字段中，需配合 `ToastNotifications.Messages` 下的扩展类调用。
- **系统通知系统**: 位于 `Quicker.Utilities.AppHelper` 静态类的 `ShowWindowsToastMessage`。
- **启动参数获取**: 通过 URL 启动时，参数需从 `quicker_in_param` 中通过字符串拆分和 `UrlDecode` 手动提取。
