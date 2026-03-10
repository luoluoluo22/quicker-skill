using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Threading;
using Quicker.Public;
using System.Threading.Tasks;

// 这是一个使用 WPF DispatcherFrame 同步等待子程序（不卡死 UI）的标准化模板
public static string Exec(IStepContext context)
{
    // 输入变量示例（可选项：将本地变量或者从动作外传入的变量传给子程序）
    // var imagePath = context.GetVarValue("imagePath") as string;
    
    try
    {
        // 1. 组装输入给子程序的变量映射 
        var inputs = new Dictionary<string, object> 
        {
            // 例如给子程序的 "imagePath" 变量赋值
            { "imagePath", @"C:\test.png" }, 
        };

        // 2. 发起对子程序的异步调用 (但不马上 Wait，避免死锁)
        var task = context.RunSpAsync("MyOcrSubProgram", inputs);
        
        // 【核心操作】利用 DispatcherFrame 启动局部消息处理泵，安全地在这个线程同步等待
        // 这个手段能确保虽然下方代码暂停了执行，但 WPF 的内部组件事件能继续流动，从而让子程序能够正常完毕
        var frame = new DispatcherFrame();
        task.ContinueWith(t => 
        {
            // 当子程序异步执行完毕后，结束局部消息泵，释放当前线程
            frame.Continue = false;
        });
        Dispatcher.PushFrame(frame);
        
        // 3. 此时获取结果，非常安全且 100% 已经是执行完毕的状态
        var result = task.GetAwaiter().GetResult();
        
        // 4. 解析输出
        if (result != null && result.ContainsKey("text"))
        {
            var ocrText = result["text"]?.ToString();
            
            // 可以把结果写回当前动作的同名输出变量 (要求该动作配置里包含 IsOutput: true 的 text 变量)
            context.SetVarValue("text", ocrText);
            
            return ocrText;
        }

        return "OK (未获取到预期结果文本)";
    }
    catch (Exception ex)
    {
        return "ERROR: " + ex.Message;
    }
}
