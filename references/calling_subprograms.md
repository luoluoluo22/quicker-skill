# C# 调用 Quicker 子程序指南

在开发 Quicker 代码动作时，当某些高级功能（例如内部 OCR、文件对话框、提示框等内部模块）难以通过反射直接调用（常因为上下文不匹配或缺少内部变量支持）时，**最佳实践**是将这部分功能封装为一个**子程序（SubProgram）**，然后在 C# 代码中直接调用该子程序。

这样可以在享受 C# 代码高度控制力的同时，复用 Quicker 现成且稳定的模块化组件。

## 调用语法

Quicker 提供了 `IStepContext.RunSpAsync` API 用于在代码中调用子程序。

### 核心代码模板

此方法支持 `async Task` 或 `async Task<string>` 签名（如果要返回给动作结果）：

```csharp
using System; 
using System.Collections.Generic; 
using System.Threading.Tasks; 
using System.Windows; 
using Quicker.Public; 

// 引擎支持异步 Task 签名
public static async Task<string> Exec(IStepContext context) 
{
    // 1. 组装传给子程序的数据 (字典的 Key 需要与子程序的“输入变量名”完全一致)
    var inputs = new Dictionary<string, object> 
    {
        { "input1", "你好" },
        { "imgKey", @"C:\path\to\image.png" }
    };
    
    try 
    {
        // 2. 调用子程序 (填入实际的子程序名称或网络子程序ID)
        // 注意：因为是异步操作，必须用 await
        var result = await context.RunSpAsync("你的子程序名称", inputs);
        
        // 3. 处理子程序的输出结果
        // "resultKey" 改成子程序里配置的“输出变量名”
        if (result != null && result.ContainsKey("resultKey")) 
        {
            var value = result["resultKey"]?.ToString();
            return "成功获取到结果: " + value;
        }
        
        return "调用完成，但未找到预期的输出变量";
    } 
    catch (Exception ex) 
    {
        return "调用失败: " + ex.Message;
    }
}
```

## 注意事项

1. **变量名称对齐**
   字典的 `Key` 必须对应子程序里配置的**局部输入变量的名称**。返回结果字典的 `Key` 必须对应子程序的**输出变量名称**。
   
2. **异步方法死锁预防 (核心难点)**
   `context.RunSpAsync` 是异步的。如果你的 `Exec` 函数由于框架原因**必须**是同步返回 `string`（没有 `async Task` 修饰），并且你需要等待子程序的结果，**绝对不能直接使用 `.GetAwaiter().GetResult()`**！这会直接阻塞 WPF 主线程，导致子程序抛出结果时无法被处理，引发死锁（界面卡死，程序不退出）。

   **纯同步代码中的完美解决方案：使用 DispatcherFrame 嵌套消息循环**
   ```csharp
   using System.Windows.Threading;
   
   // ... 在 Exec() 中 ...
   var task = context.RunSpAsync("子程序名称", inputs);
   
   // 开启局部消息泵，让出 UI 线程控制权，但代码依旧会“停留”在这里等待
   var frame = new DispatcherFrame();
   task.ContinueWith(t => frame.Continue = false);
   Dispatcher.PushFrame(frame);
   
   // 此时再获取结果，安全无死锁
   var result = task.GetAwaiter().GetResult();
   ```
   你可以参考 `templates/sync_subprogram_action.cs` 的完整模板。

3. **子程序名称或 ID**
   `RunSpAsync` 的第一个参数可以是本地的**子程序名称**（直接手写的中文名/英文名），也可以是网络共享**子程序的 ID**。推荐将其作为子程序的独立步骤建立好，然后在 C# 中根据需要灵活调度。
