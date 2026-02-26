# Quicker 云同步开发指南

本指南总结了在 Quicker 动作（Roslyn v2 模式）中实现自定义数据云端备份与恢复的最佳实践。

## 一、 核心架构

Quicker 并没有对 Roslyn 脚本直接公开 `CloudState` 对象，因此我们采用 **动态反射** 的方式调用 Quicker 内部的异步保存/读取方法。

### 1. 通用辅助类 (CloudStateHelper)
将以下代码放入你的 `.cs` 文件末尾。它封装了反射逻辑，并提供了简单的静态接口。

```csharp
public static class CloudStateHelper {
    private static Type _t; private static object _i; private static MethodInfo _s, _r;
    static CloudStateHelper() { Init(); }
    static void Init() {
        if (_i != null) return;
        foreach(var a in AppDomain.CurrentDomain.GetAssemblies()){
            _t = a.GetType("Quicker.Public.CloudState"); if(_t != null) break;
        }
        if(_t == null) return;
        var f = _t.GetField("OWJCWqTrDY4Wyj5Oc7t3", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        if(f != null) _i = f.GetValue(null);
        if(_i == null) { _i = Activator.CreateInstance(_t, true); if(f != null) try{ f.SetValue(null, _i); } catch{} }
        _s = _t.GetMethod("SaveTextAsync", new[]{ typeof(string), typeof(string), typeof(double) });
        _r = _t.GetMethod("ReadTextAsync", new[]{ typeof(string), typeof(double) });
    }
    // 保存数据：k=Key, v=Value, e=过期分钟
    public static bool SaveText(string k, string v, double e) {
        if(_i == null) Init(); if(_s == null) return false;
        return Task.Run(async() => { try{ await(Task)_s.Invoke(_i, new object[]{k, v ?? "", e}); return true; } catch{ return false; } }).Result;
    }
    // 读取数据
    public static string ReadText(string k, double e) {
        if(_i == null) Init(); if(_r == null) return null;
        return Task.Run(async() => { try{ var t = (Task)_r.Invoke(_i, new object[]{k, e}); await t; return t.GetType().GetProperty("Result")?.GetValue(t) as string; } catch{ return null; } }).Result;
    }
}
```

## 二、 备份流程 (Backup)

备份建议采用 **JSON 序列化**，并包含元数据以方便后期识别。

### 示例代码
```csharp
private void BackupToCloud() {
    // 1. 序列化数据（包含元数据）
    var data = new {
        Content = MyDataList,
        Time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
        Machine = Environment.MachineName
    };
    string json = JsonConvert.SerializeObject(data);

    // 2. 异步保存
    Task.Run(() => {
        bool success = CloudStateHelper.SaveText("MyAction_Backup", json, 43200); // 30天有效期
        Dispatcher.Invoke(() => {
            if (success) MessageBox.Show("备份成功！");
            else MessageBox.Show("失败：云端未响应");
        });
    });
}
```

## 三、 恢复流程 (Restore)

**严禁静默恢复**。恢复操作必须从云端拉取数据后，展示给用户进行二次确认。

### 1. 安全规范
*   **预览数据**：展示备份的时间和设备名。
*   **手动勾选**：允许用户选择部分恢复或全部恢复。
*   **二次确认**：在覆盖本地数据前弹出警告。

### 2. 示例逻辑
```csharp
private void RestoreFromCloud() {
    Task.Run(() => {
        string json = CloudStateHelper.ReadText("MyAction_Backup", 43200);
        
        Dispatcher.Invoke(() => {
            if (string.IsNullOrEmpty(json)) {
                MessageBox.Show("云端没有发现备份。");
                return;
            }
            
            // 解析并弹出确认窗口
            var data = JsonConvert.DeserializeObject<dynamic>(json);
            var restoreWin = new MySyncWindow(data); // 自定义勾选窗口
            if (restoreWin.ShowDialog() == true) {
                // 执行真正的写入逻辑
                ApplyData(restoreWin.SelectedItems);
            }
        });
    });
}
```

## 四、 避坑指南

1.  **数据碰撞**：Key 名称必须具备极高辨识度，建议格式：`{动作名}_{数据类型}`。
2.  **过期时间**：`SaveText` 的第三个参数是分钟，如果不希望频繁清理，建议设置较大的值（如 43200 为 30 天）。
3.  **线程安全**：云端操作必须放在 `Task.Run` 中，否则会由于网络延迟导致 Quicker 界面彻底卡死。
4.  **Dispatcher**：所有涉及 UI 更新（包括弹出恢复窗口）的操作都必须回到主线程。
