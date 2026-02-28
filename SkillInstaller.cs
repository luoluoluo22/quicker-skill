using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Windows;
using System.Windows.Forms;
using Quicker.Public;
using System.Threading.Tasks;

public static async Task Exec(IStepContext context)
{
    string defaultPath = context.GetVarValue("targetPath") as string ?? @"D:\QuickerSkillsProject";
    string zipUrl = context.GetVarValue("zipUrl") as string;
    // 获取传入的扳手 ID，用于更新 config.json
    string wrenchId = context.GetVarValue("wrench_id") as string;

    // 1. 让用户选择编辑器类型 (UI 必须在主线程/STA 运行)
    var editors = new Dictionary<string, (string Path, string Cmd)>
    {
        { "🤖 Antigravity / Gemini", (".agent/skills", "Antigravity .") },
        { "🚀 Trae IDE", (".trae/skills", "trae .") },
        { "🧠 Claude Code", (".claude/skills", "cmd.exe /k claude") },
        { "💻 Cursor", ("skills", "cursor .") },
        { "💻 VSCode / 通用", ("skills", "code .") }
    };

    string selectedEditor = "🤖 Antigravity / Gemini";
    using (var form = new Form())
    {
        form.Text = "选择您的 AI 编辑器 - QK技能安装助手";
        form.Width = 400;
        form.Height = 300;
        form.StartPosition = FormStartPosition.CenterScreen;
        form.FormBorderStyle = FormBorderStyle.FixedDialog;
        form.MaximizeBox = false;
        form.TopMost = true;

        Label label = new Label() { Left = 20, Top = 20, Text = "请选择你要安装到的编辑器环境：", Width = 350 };
        ListBox listBox = new ListBox() { Left = 20, Top = 50, Width = 340, Height = 120 };
        foreach (var key in editors.Keys) listBox.Items.Add(key);
        listBox.SelectedIndex = 0;

        Button buttonOk = new Button() { Text = "确定", Left = 260, Width = 100, Top = 200, DialogResult = DialogResult.OK };
        
        form.Controls.Add(label);
        form.Controls.Add(listBox);
        form.Controls.Add(buttonOk);
        form.AcceptButton = buttonOk;

        if (form.ShowDialog() == DialogResult.OK)
        {
            selectedEditor = listBox.SelectedItem.ToString();
        }
        else return;
    }

    string editorRelativePath = editors[selectedEditor].Path;
    string editorCmd = editors[selectedEditor].Cmd;
    Log($"用户选择了编辑器: {selectedEditor}, 相对路径: {editorRelativePath}");

    // 2. 询问用户安装位置
    var result = System.Windows.MessageBox.Show(
        $"准备安装 Quicker-Skill 技能包。\n\n编辑器：{selectedEditor}\n默认根目录：{defaultPath}\n\n是否使用默认目录？点击“否”选择其他目录，点击“取消”退出。",
        "QK技能安装助手",
        MessageBoxButton.YesNoCancel,
        MessageBoxImage.Question,
        MessageBoxResult.Yes,
        System.Windows.MessageBoxOptions.DefaultDesktopOnly);

    string finalPath = defaultPath;
    if (result == MessageBoxResult.Cancel) return;
    if (result == MessageBoxResult.No)
    {
        using (var topForm = new Form() { TopMost = true })
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = $"请选择您的项目根目录 (安装路径将为: 项目目录\\{editorRelativePath.Replace("/", "\\")}\\quicker-skill)";
                if (dialog.ShowDialog(topForm) == DialogResult.OK)
                {
                    finalPath = dialog.SelectedPath;
                }
                else return;
            }
        }
    }

    // 3. 准备目录结构
    string skillRelativePath = Path.Combine(finalPath, editorRelativePath.Replace("/", "\\"), "quicker-skill");
    if (!Directory.Exists(finalPath)) Directory.CreateDirectory(finalPath);

    string tempZip = Path.Combine(Path.GetTempPath(), "quicker-skill-master.zip");
    string tempExtract = Path.Combine(Path.GetTempPath(), "quicker-skill-temp-" + Guid.NewGuid().ToString("N"));

    try
    {
        // =========================================================
        // 在后台执行的任务
        // =========================================================
        await Task.Run(() => {
            Log("开始下载 Zip...");
            using (var client = new WebClient())
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                client.DownloadFile(zipUrl, tempZip);
            }
            Log("下载完成，准备解压...");

            if (Directory.Exists(tempExtract)) Directory.Delete(tempExtract, true);
            ZipFile.ExtractToDirectory(tempZip, tempExtract);
            Log($"解压到临时目录: {tempExtract}");

            // 4. 寻找源根
            string sourceRoot = null;
            var topDirs = Directory.GetDirectories(tempExtract);
            if (topDirs.Length > 0)
            {
                sourceRoot = topDirs[0]; 
                Log($"识别源根目录: {sourceRoot}");
            }

            if (sourceRoot != null && File.Exists(Path.Combine(sourceRoot, "SKILL.md")))
            {
                Log($"验证成功，找到 SKILL.md。");
                
                // 清理并创建目标目录
                if (Directory.Exists(skillRelativePath))
                {
                    Log("清理旧版本目录 (更新技能)...");
                    Directory.Delete(skillRelativePath, true);
                }
                
                Directory.CreateDirectory(Path.GetDirectoryName(skillRelativePath));
                
                // 复制文件
                Log($"将 {sourceRoot} 复制并安装至 {skillRelativePath}");
                CopyDirectory(sourceRoot, skillRelativePath);

                // --- 关键增强：根据传入的 wrench_id 更新 config.json ---
                if (!string.IsNullOrEmpty(wrenchId))
                {
                    string configPath = Path.Combine(skillRelativePath, "config.json");
                    Log($"准备更新配置文件: {configPath}, ID: {wrenchId}");
                    string configContent = "{\n  \"wrench_action_id\": \"" + wrenchId + "\"\n}";
                    File.WriteAllText(configPath, configContent);
                }
                
                Log("核心文件复制完成。");
            }
            else
            {
                throw new Exception("在下载的压缩包中未找到有效的技能包结构。");
            }
        });

        // =========================================================
        // 回到 UI 线程完成后续通知和启动
        // =========================================================
        System.Windows.MessageBox.Show(
            $"安装/更新成功！\n\n编辑器：{selectedEditor}\n项目目录：{finalPath}\n\n配置已更新，点击确定后将尝试为您打开该项目。",
            "完成", MessageBoxButton.OK, MessageBoxImage.Information, MessageBoxResult.OK, System.Windows.MessageBoxOptions.DefaultDesktopOnly);
            
        // 5. 自动启动相关编辑器
        try
        {
            Log($"尝试启动编辑器: {editorCmd} 在目录: {finalPath}");
            if (editorCmd.StartsWith("cmd.exe"))
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = editorCmd.Substring(8),
                    WorkingDirectory = finalPath,
                    UseShellExecute = true
                });
            }
            else
            {
                var parts = editorCmd.Split(new[] { ' ' }, 2);
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = parts[0],
                    Arguments = parts.Length > 1 ? parts[1] : "",
                    WorkingDirectory = finalPath,
                    UseShellExecute = true,
                    CreateNoWindow = true
                });
            }
        }
        catch (Exception ex)
        {
            Log($"启动编辑器失败 ({ex.Message})，降级使用资源管理器打开...");
            System.Diagnostics.Process.Start("explorer.exe", finalPath);
        }
    }
    catch (Exception ex)
    {
        Log($"异常: {ex.ToString()}");
        System.Windows.MessageBox.Show("安装失败：" + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK, System.Windows.MessageBoxOptions.DefaultDesktopOnly);
    }
    finally
    {
        try { if (File.Exists(tempZip)) File.Delete(tempZip); } catch {}
        try { if (Directory.Exists(tempExtract)) Directory.Delete(tempExtract, true); } catch {}
    }
}

// 跨盘符文件夹复制辅助函数
private static void CopyDirectory(string sourceDir, string destDir)
{
    Directory.CreateDirectory(destDir);

    foreach (string file in Directory.GetFiles(sourceDir))
    {
        string destFile = Path.Combine(destDir, Path.GetFileName(file));
        File.Copy(file, destFile, true);
    }

    foreach (string subDir in Directory.GetDirectories(sourceDir))
    {
        string destSubDir = Path.Combine(destDir, Path.GetFileName(subDir));
        CopyDirectory(subDir, destSubDir);
    }
}

// 日志记录函数
private static void Log(string message)
{
    string logDir = @"F:\Desktop\kaifa\quicker-skill";
    if (Directory.Exists(logDir))
    {
        string logPath = Path.Combine(logDir, "install_log.txt");
        string content = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}\r\n";
        File.AppendAllText(logPath, content);
    }
}
