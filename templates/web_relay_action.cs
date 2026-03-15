//css_ref System.Windows.Forms.dll
//css_ref System.Drawing.dll
//css_ref Microsoft.Web.WebView2.Core.dll
//css_ref System.Runtime.Serialization.dll
//css_ref System.Xml.dll

using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Drawing;
using Microsoft.Web.WebView2.Core;

public static string Exec(Quicker.Public.IStepContext context)
{
    if (System.Threading.Thread.CurrentThread.GetApartmentState() != System.Threading.ApartmentState.STA)
    {
        MessageBox.Show("配置错误：请将模块执行线程设置为 STA 独立线程。", "Web Relay");
        return "ERROR";
    }

    string inputParam = "";
    string menuKeyParam = "";
    try { inputParam = context.GetVarValue("input") as string; } catch { }
    try { menuKeyParam = context.GetVarValue("menuKey") as string; } catch { }

    if (string.IsNullOrWhiteSpace(inputParam))
    {
        try
        {
            string quickerInParam = context.GetVarValue("quicker_in_param") as string;
            if (!string.IsNullOrWhiteSpace(quickerInParam))
            {
                foreach (var part in quickerInParam.Split('&'))
                {
                    var pair = part.Split(new[] { '=' }, 2);
                    if (pair.Length != 2) continue;
                    if (string.Equals(pair[0], "input", StringComparison.OrdinalIgnoreCase))
                        inputParam = WebUtility.UrlDecode(pair[1]);
                    else if (string.Equals(pair[0], "menuKey", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(menuKeyParam))
                        menuKeyParam = WebUtility.UrlDecode(pair[1]);
                }
            }
        }
        catch { }
    }

    inputParam = inputParam?.Trim().ToLower() ?? "";
    menuKeyParam = menuKeyParam?.Trim().ToLower() ?? "";
    string startupMode = !string.IsNullOrWhiteSpace(inputParam) ? inputParam : menuKeyParam;

    if (startupMode == "shutdown" || startupMode == "stop")
    {
        WebRelayForm.ShutdownRemoteService();
        return "OK";
    }

    bool isSilent = startupMode == "silent";

    Application.EnableVisualStyles();
    Application.SetCompatibleTextRenderingDefault(false);
    Application.Run(new WebRelayForm(isSilent));
    return "OK";
}

public class WebRelayForm : Form
{
    private const string TargetUrl = "https://example.com/chat";
    private const int ServerPort = 57000;
    private const string RelayTitle = "Web Relay";

    private readonly bool _isDebugMode;
    private readonly string _userDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "WebRelay_Data");

    private CoreWebView2Environment _env;
    private CoreWebView2Controller _controller;
    private CoreWebView2 _coreWebView;
    private HttpListener _httpListener;
    private HttpListenerContext _currentContext;
    private NotifyIcon _notifyIcon;
    private TextBox _txtLog;
    private Panel _webPanel;
    private bool _isInitializing;
    private bool _isCurrentRequestStreaming;
    private StringBuilder _nonStreamResponseBuffer = new StringBuilder();

    public WebRelayForm(bool isSilent)
    {
        _isDebugMode = !isSilent;

        InitializeComponent();
        InitializeSystemTray();

        this.Load += async (s, e) => {
            await InitializeWebViewSequenceAsync();
        };

        StartHttpServer();
    }

    public static void ShutdownRemoteService()
    {
        try
        {
            var request = WebRequest.Create("http://127.0.0.1:" + ServerPort + "/shutdown");
            request.Timeout = 2000;
            using (var response = request.GetResponse()) { }
        }
        catch { }
    }

    private void InitializeComponent()
    {
        this.Text = RelayTitle + " (Port: " + ServerPort + ")";
        this.Size = new Size(1200, 800);
        this.StartPosition = FormStartPosition.CenterScreen;

        _webPanel = new Panel();
        _webPanel.Dock = DockStyle.Fill;
        this.Controls.Add(_webPanel);

        _txtLog = new TextBox();
        _txtLog.Multiline = true;
        _txtLog.Dock = DockStyle.Right;
        _txtLog.Width = 360;
        _txtLog.ScrollBars = ScrollBars.Vertical;
        _txtLog.ReadOnly = true;
        this.Controls.Add(_txtLog);
    }

    private void InitializeSystemTray()
    {
        _notifyIcon = new NotifyIcon();
        _notifyIcon.Text = RelayTitle;
        _notifyIcon.Icon = SystemIcons.Application;
        _notifyIcon.Visible = true;
    }

    private async Task InitializeWebViewSequenceAsync()
    {
        if (_isInitializing) return;
        _isInitializing = true;
        try
        {
            _env = await CoreWebView2Environment.CreateAsync(null, _userDataFolder);
            _controller = await _env.CreateCoreWebView2ControllerAsync(_webPanel.Handle);
            _coreWebView = _controller.CoreWebView2;
            _controller.Bounds = _webPanel.ClientRectangle;
            await _coreWebView.AddScriptToExecuteOnDocumentCreatedAsync(GetNetworkInterceptorScript());
            _coreWebView.WebMessageReceived += OnWebMessageReceived;
            _coreWebView.Navigate(TargetUrl);
        }
        finally
        {
            _isInitializing = false;
        }
    }

    private void StartHttpServer()
    {
        try
        {
            _httpListener = new HttpListener();
            _httpListener.Prefixes.Add("http://127.0.0.1:" + ServerPort + "/");
            _httpListener.Start();
            Task.Run(ListenLoop);
            Log("HTTP 服务监听中: " + ServerPort);
        }
        catch (Exception ex)
        {
            Log("HTTP 启动失败: " + ex.Message);
        }
    }

    private async Task ListenLoop()
    {
        var listener = _httpListener;
        if (listener == null) return;
        while (listener.IsListening)
        {
            try
            {
                var ctx = await listener.GetContextAsync();
                HandleRequest(ctx);
            }
            catch { break; }
        }
    }

    private async void HandleRequest(HttpListenerContext ctx)
    {
        string path = ctx.Request.Url.AbsolutePath.ToLower();
        string method = ctx.Request.HttpMethod.ToUpper();
        ctx.Response.AppendHeader("Access-Control-Allow-Origin", "*");
        ctx.Response.AppendHeader("Access-Control-Allow-Headers", "Content-Type, Authorization");
        if (method == "OPTIONS") { ctx.Response.StatusCode = 200; ctx.Response.Close(); return; }

        if (path == "/shutdown")
        {
            ResponseText(ctx, "Closing...", 200, "text/plain");
            this.Invoke(new Action(() => this.Close()));
            return;
        }

        if (path == "/v1/models" && method == "GET")
        {
            ResponseText(ctx, "{\"object\":\"list\",\"data\":[]}", 200, "application/json");
            return;
        }

        if (path == "/v1/chat/completions" && method == "POST")
        {
            using (var reader = new StreamReader(ctx.Request.InputStream, Encoding.UTF8))
            {
                string body = reader.ReadToEnd();
                string prompt = ExtractPromptFromJson(body);
                if (string.IsNullOrWhiteSpace(prompt))
                {
                    ResponseText(ctx, "{\"error\":\"Parse Error\"}", 400, "application/json");
                    return;
                }

                _currentContext = ctx;
                _isCurrentRequestStreaming = body.Contains("\"stream\":true") || body.Contains("\"stream\": true");
                _nonStreamResponseBuffer.Clear();
                this.Invoke(new Action(() => InjectPromptToWebView(prompt)));
            }
            return;
        }

        ResponseText(ctx, "Not Found", 404, "text/plain");
    }

    private string ExtractPromptFromJson(string json)
    {
        try
        {
            var contentRegex = new Regex("\"content\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"");
            var lastMatch = contentRegex.Matches(json).Cast<Match>().LastOrDefault();
            return lastMatch != null ? UnescapeJson(lastMatch.Groups[1].Value) : null;
        }
        catch { return null; }
    }

    private void InjectPromptToWebView(string prompt)
    {
        string promptBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(prompt ?? ""));
        string script = "window.WebRelayBridge.sendPromptFromBase64(\"" + promptBase64 + "\")";
        try { _coreWebView.ExecuteScriptAsync(script); } catch { }
    }

    private void OnWebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            string msg = e.TryGetWebMessageAsString();
            if (msg.StartsWith("[LOG]")) { Log(msg); return; }
            if (_currentContext == null) return;
            if (msg.StartsWith("[NETWORK_DONE]")) { FinishRequest(); return; }
            if (msg.StartsWith("[NETWORK_DATA]")) { SendDelta(msg.Substring(14)); }
        }
        catch (Exception ex) { Log("MsgError: " + ex.Message); }
    }

    private void SendDelta(string text)
    {
        if (!_isCurrentRequestStreaming)
            _nonStreamResponseBuffer.Append(text);

        if (_isCurrentRequestStreaming)
        {
            string jsonChunk = "{\"id\":\"chatcmpl-webrelay\",\"object\":\"chat.completion.chunk\",\"created\":" + DateTimeOffset.Now.ToUnixTimeSeconds() + ",\"model\":\"web-relay\",\"choices\":[{\"index\":0,\"delta\":{\"content\":\"" + EscapeJson(text) + "\"},\"finish_reason\":null}]}";
            WriteSseData("data: " + jsonChunk + "\n\n");
        }
    }

    private void FinishRequest()
    {
        if (_currentContext == null) return;
        if (_isCurrentRequestStreaming)
        {
            string stopChunk = "{\"id\":\"chatcmpl-webrelay\",\"object\":\"chat.completion.chunk\",\"created\":" + DateTimeOffset.Now.ToUnixTimeSeconds() + ",\"model\":\"web-relay\",\"choices\":[{\"index\":0,\"delta\":{},\"finish_reason\":\"stop\"}]}";
            WriteSseData("data: " + stopChunk + "\n\n");
            WriteSseData("data: [DONE]\n\n");
        }
        else
        {
            string content = _nonStreamResponseBuffer.ToString();
            string json = "{\"id\":\"chatcmpl-webrelay\",\"object\":\"chat.completion\",\"created\":" + DateTimeOffset.Now.ToUnixTimeSeconds() + ",\"model\":\"web-relay\",\"choices\":[{\"index\":0,\"message\":{\"role\":\"assistant\",\"content\":\"" + EscapeJson(content) + "\"},\"finish_reason\":\"stop\"}]}";
            ResponseText(_currentContext, json, 200, "application/json");
        }
        CleanupRequest();
    }

    private void WriteSseData(string data)
    {
        try
        {
            byte[] buffer = Encoding.UTF8.GetBytes(data);
            _currentContext.Response.OutputStream.Write(buffer, 0, buffer.Length);
            _currentContext.Response.OutputStream.Flush();
        }
        catch { CleanupRequest(); }
    }

    private void CleanupRequest()
    {
        try { _currentContext?.Response.Close(); } catch { }
        _currentContext = null;
        _nonStreamResponseBuffer.Clear();
    }

    private void ResponseText(HttpListenerContext ctx, string text, int code, string contentType)
    {
        try
        {
            ctx.Response.StatusCode = code;
            ctx.Response.ContentType = contentType;
            byte[] buffer = Encoding.UTF8.GetBytes(text);
            ctx.Response.ContentLength64 = buffer.Length;
            ctx.Response.OutputStream.Write(buffer, 0, buffer.Length);
            ctx.Response.Close();
        }
        catch { }
    }

    private string EscapeJson(string s)
    {
        if (s == null) return "";
        return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t");
    }

    private string UnescapeJson(string str)
    {
        return (str ?? "").Replace("\\\"", "\"").Replace("\\\\", "\\").Replace("\\n", "\n").Replace("\\r", "\r").Replace("\\t", "\t");
    }

    private void Log(string msg)
    {
        if (!_isDebugMode) return;
        try
        {
            if (_txtLog != null && !_txtLog.IsDisposed)
            {
                if (_txtLog.InvokeRequired) _txtLog.Invoke(new Action(() => Log(msg)));
                else _txtLog.AppendText("[" + DateTime.Now.ToString("HH:mm:ss") + "] " + msg + "\r\n");
            }
        }
        catch { }
    }

    private string GetNetworkInterceptorScript()
    {
        return @"
        window.chrome.webview.postMessage('[LOG] Interceptor Ready');
        const originalFetch = window.fetch;

        window.WebRelayBridge = {
            sendPromptFromBase64: function(textBase64) {
                const binary = atob(textBase64 || '');
                const text = new TextDecoder().decode(Uint8Array.from(binary, ch => ch.charCodeAt(0)));
                return this.sendPrompt(text);
            },
            sendPrompt: function(text) {
                // TODO: 替换为目标站点的输入框定位、模型切换、联网开关和发送按钮逻辑。
                window.chrome.webview.postMessage('[LOG] TODO: implement DOM send flow');
            }
        };

        window.fetch = async (...args) => {
            const response = await originalFetch(...args);
            // TODO: 只拦截目标聊天接口，并把真实响应增量透传回宿主。
            return response;
        };
        ";
    }
}
