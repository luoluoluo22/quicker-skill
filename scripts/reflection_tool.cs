using System;
using System.Windows;
using System.Reflection;
using System.Linq;
using Quicker.Public;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Net;

public static string Exec(IStepContext context)
{
    // 获取输入参数 (由 URL 模式 runaction:ID?key=val 注入)
    string inParam = context.GetVarValue("quicker_in_param") as string ?? "";
    var args = ParseParams(inParam);

    string action = args.ContainsKey("action") ? args["action"] : "search";
    string keyword = args.ContainsKey("keyword") ? args["keyword"] : "";
    string typeName = args.ContainsKey("type") ? args["type"] : "";
    string logPath = args.ContainsKey("log") ? args["log"] : "reflection_result.log";

    StringBuilder sb = new StringBuilder();
    sb.AppendLine($"=== Quicker Reflection Tooling ({DateTime.Now:yyyy-MM-dd HH:mm:ss}) ===");
    sb.AppendLine($"Action: {action} | Keyword: {keyword} | Type: {typeName}");
    sb.AppendLine(new string('-', 40));

    try
    {
        var assemblies = AppDomain.CurrentDomain.GetAssemblies().Where(a => a.GetName().Name.Contains("Quicker")).ToList();

        if (action == "search")
        {
            SearchTypes(assemblies, keyword, sb);
        }
        else if (action == "dump")
        {
            DumpType(assemblies, typeName, sb);
        }
        else if (action == "search_members")
        {
            SearchMembers(assemblies, keyword, sb);
        }
        else if (action == "list_assemblies")
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                 sb.AppendLine($"[ASM] {asm.GetName().Name} ({asm.Location})");
        }

        File.WriteAllText(logPath, sb.ToString(), Encoding.UTF8);
        return "SUCCESS";
    }
    catch (Exception ex)
    {
        File.WriteAllText(logPath, "Error: " + ex.ToString(), Encoding.UTF8);
        return "ERROR";
    }
}

static void SearchTypes(IEnumerable<Assembly> asms, string kw, StringBuilder sb)
{
    foreach (var asm in asms)
    {
        try {
            var types = asm.GetTypes().Where(t => t.IsPublic && t.FullName.IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0);
            if (types.Any()) {
                sb.AppendLine($"\n[Assembly] {asm.GetName().Name}");
                foreach (var t in types) sb.AppendLine($"  - {t.FullName}");
            }
        } catch {}
    }
}

static void DumpType(IEnumerable<Assembly> asms, string tn, StringBuilder sb)
{
    var type = asms.SelectMany(a => { try { return a.GetTypes(); } catch { return new Type[0]; }} )
                   .FirstOrDefault(t => t.FullName.Equals(tn, StringComparison.OrdinalIgnoreCase) || t.Name.Equals(tn, StringComparison.OrdinalIgnoreCase));
    
    if (type == null) { sb.AppendLine($"Type '{tn}' not found."); return; }

    sb.AppendLine($"\n[Type] {type.FullName}");
    sb.AppendLine("\nProperties:");
    foreach (var p in type.GetProperties()) sb.AppendLine($"  - {p.PropertyType.Name} {p.Name}");
    
    sb.AppendLine("\nMethods:");
    foreach (var m in type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance).Where(m => m.DeclaringType == type))
        sb.AppendLine($"  - {m.Name}({string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name))}) -> {m.ReturnType.Name}");
}

static void SearchMembers(IEnumerable<Assembly> asms, string kw, StringBuilder sb)
{
    foreach (var asm in asms)
    {
        try {
            foreach (var t in asm.GetTypes().Where(t => t.IsPublic)) {
                var matches = t.GetMembers().Where(m => m.Name.IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0);
                if (matches.Any()) {
                    sb.AppendLine($"\nType: {t.FullName}");
                    foreach (var m in matches) sb.AppendLine($"  - [{m.MemberType}] {m.Name}");
                }
            }
        } catch {}
    }
}

static Dictionary<string, string> ParseParams(string input)
{
    var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    if (string.IsNullOrEmpty(input)) return dict;
    foreach (var part in input.Split('&')) {
        var pair = part.Split('=');
        if (pair.Length == 2) dict[WebUtility.UrlDecode(pair[0])] = WebUtility.UrlDecode(pair[1]);
    }
    return dict;
}
