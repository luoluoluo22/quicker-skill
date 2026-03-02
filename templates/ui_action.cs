using System;
using System.Windows;
using System.Reflection;
using System.Linq;
using Quicker.Public;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;

// C# 脚本入口方法，必须接受 IStepContext 参数
public static string Exec(IStepContext context)
{
    // 关键原则：日志路径应默认设置在当前脚本/JSON 旁边，以便 build_action.ps1 能够捕捉到执行结果
    string logPath = @"F:\Desktop\kaifa\quicker-skill\YourActionName.log"; 
    StringBuilder sb = new StringBuilder();
    sb.AppendLine($"--- Run at {DateTime.Now:yyyy-MM-dd HH:mm:ss} ---");
    
    try
    {
        // 核心 1：所有基于 WPF UI 的控制必须在 Dispatcher 线程中执行
        Application.Current.Dispatcher.Invoke(() => 
        {
            var mainWindow = Application.Current.MainWindow;
            
            // 将代码逻辑放到此处
            // ...
            
            sb.AppendLine("[OK] Executed UI Logic successfully.");
        });

        // 核心 2：将动作的返回值设定给输出变量 rtn
        context.SetVarValue("rtn", "处理完成");
        
        // 将运行日志写出，供自动化构建脚本检查
        File.AppendAllText(logPath, sb.ToString(), Encoding.UTF8);
        return "OK";
    }
    catch (Exception ex)
    {
        string err = $"[ERROR] Exception encountered:\n{ex}";
        File.AppendAllText(logPath, err, Encoding.UTF8);
        
        // 通知 Quicker 弹窗显示错误信息（如果不静默的话）
        context.SetVarValue("errMessage", err);
        return "ERROR";
    }
}
