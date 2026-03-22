using System;
using System.Linq;
using System.Reflection;
using System.Windows;
using Quicker.Public;

// Roslyn v2 动态悬浮通知模板
// 优势：自动扫描 MainWindow 字段，兼容不同版本的 Quicker 混淆名

public static string Exec(IStepContext context)
{
    // 默认通过变量 message 获取内容，如果没有定义则使用 text 变量
    string message = (context.GetVarValue("message") as string) 
                  ?? (context.GetVarValue("text") as string) 
                  ?? "通知内容为空";

    Application.Current.Dispatcher.Invoke(() =>
    {
        try
        {
            var mw = Application.Current.MainWindow;
            if (mw == null) return;

            // 1. 动态定位 Notifier 实例
            object notifier = mw.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                .FirstOrDefault(f => f.FieldType.FullName?.Contains("ToastNotifications.Notifier") == true)
                ?.GetValue(mw);

            if (notifier == null) throw new Exception("无法在当前版本 Quicker 中定位 Notifier。");

            // 2. 定位扩展方法类 SuccessExtensions (此处也可以根据需要替换为 WarningExtensions/ErrorExtensions)
            var extType = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => { try { return a.GetTypes(); } catch { return Type.EmptyTypes; } })
                .FirstOrDefault(t => t.FullName == "ToastNotifications.Messages.SuccessExtensions");

            if (extType == null) throw new Exception("未找到 Toast 消息扩展库。");

            // 3. 执行弹出
            var method = extType.GetMethod("ShowSuccess", new[] { notifier.GetType(), typeof(string) });
            method?.Invoke(null, new object[] { notifier, message });
        }
        catch (Exception ex)
        {
            // 降级方案：MessageBox
            MessageBox.Show(message, "Quicker 通知 (Toast 调用失败)", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    });

    return "OK";
}
