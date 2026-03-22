# Quicker 内置图标说明

本地已解析 Quicker 安装目录中的图标程序集：

- `C:\Program Files\Quicker\FontAwesomeIconsWpf.dll`

Quicker 当前内置图标基于：

- `FontAwesome5.EFontAwesomeIcon`
- `FontAwesome5.EFontAwesomeStyle`

可用风格：

- `Solid`
- `Regular`
- `Light`
- `Brands`

图标索引文件：

- `.codex/skills/quicker-skill/references/quicker_fontawesome_icons.csv`

辅助脚本：

- 导出/刷新索引：
  - `.codex/skills/quicker-skill/scripts/export_fontawesome_icons.ps1`
- 搜索图标：
  - `.codex/skills/quicker-skill/scripts/search_fontawesome_icons.ps1`

搜索示例：

- `powershell -ExecutionPolicy Bypass -File .\.codex\skills\quicker-skill\scripts\search_fontawesome_icons.ps1 barcode`
- `powershell -ExecutionPolicy Bypass -File .\.codex\skills\quicker-skill\scripts\search_fontawesome_icons.ps1 info -Style Solid`
- `powershell -ExecutionPolicy Bypass -File .\.codex\skills\quicker-skill\scripts\search_fontawesome_icons.ps1 Solid_BarcodeAlt -Exact`

## 命名规则

Quicker 动作 JSON 中的图标写法：

- `[fa:Solid_Play]`
- `[fa:Solid_InfoCircle]`
- `[fa:Regular_Sun]`

动作主图标可写为：

- `"fa:Solid_Server:#2D8CFF"`

## 已确认存在的示例

- `Solid_BarcodeAlt`
- `Solid_Play`
- `Solid_Stop`
- `Solid_InfoCircle`
- `Solid_FileAlt`
- `Solid_Server`
- `Solid_PaperPlane`
- `Regular_Sun`

## 已确认不存在的示例

- `Solid_CircleInfo`
- `Solid_FileLines`
- `Regular_FileLines`

## 本次修正

`FeishuListenerHost.json` 已改为使用这些已确认存在的图标：

- `start` → `Solid_Play`
- `stop` → `Solid_Stop`
- `status` → `Solid_InfoCircle`
- `show_log` → `Solid_FileAlt`
