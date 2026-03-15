# Kimi Web Relay Case

这个案例展示了如何用 `quicker-skill` 构建一个基于 `WebView2 + Quicker + 本地 HTTP 中继` 的网页转 API 动作。

## 包含文件
- `KimiWebRelay.json`：动作配置。
- `KimiWebRelay.cs`：核心逻辑，包含 Kimi 页面驱动、模型切换、联网搜索、附件上传、本地 HTTP 兼容层。
- `KimiWebRelay_简介.md`：动作简介。
- `test_kimi_relay.ps1`：基础单请求验证。
- `verify_kimi_relay.ps1`：构建后自动停服、重建、双请求验证。
- `test_kimi_content_matrix.ps1`：代码/HTML/SVG/长文本完整性验证。
- `test_kimi_attachments.ps1`：文本文件与图片上传验证。

## 当前能力
- 支持 `kimi-k2.5-fast`、`kimi-k2.5-fast-search`、`kimi-k2.5-thinking`、`kimi-k2.5-thinking-search`
- 端口默认 `56000`
- 首次请求走网页发送建立模板，后续请求优先走 C# 直发
- 已验证联网搜索菜单的开启和关闭
- 已验证文本、HTML、SVG、长文本完整输出
- 已验证文本文件和图片上传

## 使用提示
1. 构建前优先请求 `/shutdown` 停掉旧实例。
2. 用 `scripts/build.ps1` 构建 `KimiWebRelay.json`。
3. 构建后动作会自动启动，再用测试脚本做回归。
