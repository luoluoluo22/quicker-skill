using System;
using System.Windows;
using System.Reflection;
using System.Linq;
using Quicker.Public;
using System.Collections.Generic;
using System.Text;
using System.Threading;

// C# 脚本入口方法，必须返回 string，接受 IStepContext 参数
public static string Exec(IStepContext context)
{
    try
    {
        // 核心 1：所有操作建议包裹在 Dispatcher.Invoke 中处理 UI 线程冲突
        Application.Current.Dispatcher.Invoke(() => 
        {
            // 在此处编写你的业务逻辑
            // ...
        });

        return "OK";
    }
    catch (Exception ex)
    {
        // 返回错误信息供 Quicker 获取
        return "ERROR: " + ex.Message;
    }
}
