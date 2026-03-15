Kimi 网页转 API (Kimi Relay)

这是一个基于 Edge WebView2 的本地中继动作。它会在后台打开 Kimi 网页，复用网页登录态，把本地 OpenAI 兼容请求转发到网页端，再将网页返回的流式内容回传给客户端。

核心特性：

- 支持本地 `http://127.0.0.1:56000/v1/chat/completions`
- 兼容 SSE 流式输出
- 保留网页登录态，不需要单独维护 cookie
- 提供可视化控制台和托盘菜单
- 支持通过模型名映射“普通对话 / 联网搜索 / 深度思考”

使用方法：

1. 运行本动作。
2. 首次启动时在弹出的 Kimi 网页中手动登录。
3. 看到控制台显示“正在运行”后，在任意 OpenAI Compatible 客户端中配置：
   Base URL: `http://127.0.0.1:56000`
   API Key: `sk-any`
   Model: `kimi-k2.5-fast`
4. 如需后台启动，可给动作输入参数 `silent`。
5. 如需停止后台服务，可给动作输入参数 `stop` 或 `shutdown`。

可用模型名：

- `kimi-k2.5-fast`
- `kimi-k2.5-fast-search`
- `kimi-k2.5-thinking`
- `kimi-k2.5-thinking-search`

注意事项：

- 这是基于现有 DeepSeek 中继动作派生出的 Kimi 试验版本，发送按钮与模式按钮选择器尚未针对 Kimi 页面做专项校准。
- 如果 Kimi 网页 DOM 结构与当前脚本不一致，可能出现“输入成功但未发出”或模式切换失效，需要进一步抓取并适配实际页面结构。
- 需要系统已安装 WebView2 Runtime。
