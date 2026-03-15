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
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms; 
using System.Drawing;
using Microsoft.Web.WebView2.Core;

public static string Exec(Quicker.Public.IStepContext context)
{
    if (System.Threading.Thread.CurrentThread.GetApartmentState() != System.Threading.ApartmentState.STA)
    {
        MessageBox.Show("配置错误：\n请将模块【执行线程】设置为 【后台线程 (STA独立线程) / staLongRun】", "Kimi Relay");
        return "ERROR";
    }

    string inputParam = "";
    try { inputParam = context.GetVarValue("input") as string; } catch { }
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
                    if (pair.Length == 2 && string.Equals(pair[0], "input", StringComparison.OrdinalIgnoreCase))
                    {
                        inputParam = System.Net.WebUtility.UrlDecode(pair[1]);
                        break;
                    }
                }
            }
        }
        catch { }
    }
    inputParam = inputParam?.Trim().ToLower() ?? "";

    if (inputParam == "shutdown" || inputParam == "stop")
    {
        ShutdownRemoteService();
        return "OK"; 
    }

    bool isSilent = inputParam == "silent";

    Application.EnableVisualStyles();
    Application.SetCompatibleTextRenderingDefault(false);
    KimiForm form = new KimiForm(isSilent);
    Application.Run(form); 
    return "OK";
}

private static void ShutdownRemoteService()
{
    try 
    {
        var request = WebRequest.Create("http://127.0.0.1:56000/shutdown");
        request.Timeout = 2000; 
        using(var response = request.GetResponse()) { }
        MessageBox.Show("✅ 已发送停止指令。", "Kimi Relay");
    }
    catch { MessageBox.Show("⚠️ 连接服务失败。", "错误"); }
}

public class KimiForm : Form
{
    private readonly bool _isDebugMode; 
    private const string TargetUrl = "https://www.kimi.com/";
    private const int ServerPort = 56000;  
    private readonly string _userDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Kimi_Quicker_Data");
    private readonly string _settingsFile; // 配置文件路径
    private readonly string _runtimeLogFile;
    private readonly string _workspaceLogFile = @"f:\Desktop\kaifa\deepseek-网页转api\服务端.log";
    private const string IconUrl = "https://files.getquicker.net/_icons/F61D79E8F06DE37A4D3C8D76A6DE4835E15F0720.png";

    private CoreWebView2Environment _env;
    private CoreWebView2Controller _controller;
    private CoreWebView2 _coreWebView;
    private bool _isInitializing = false; 

    private TextBox _txtLog;
    private Button _btnToggle;
    private Button _btnRestart; 
    private Label _lblStatus;
    private Panel _rightPanel;
    private Panel _webPanel; 
    private SplitContainer _split; 
    
    private NotifyIcon _notifyIcon;
    private ContextMenuStrip _trayMenu;
    private ToolStripMenuItem _itemAutoNewTopic; // [V44] 设置项

    private HttpListener _httpListener;
    private HttpListenerContext _currentContext;
    private bool _isCurrentRequestStreaming;
    private StringBuilder _networkBuffer = new StringBuilder();
    private StringBuilder _nonStreamResponseBuffer = new StringBuilder();
    private List<string> _citations = new List<string>();
    private bool _chatResponseHandled = false;
    private string _latestChatRequestPrompt = "";
    private string _latestChatRequestUrl = "";
    private string _latestChatRequestMethod = "POST";
    private string _latestChatRequestBody = "";
    private byte[] _latestChatRequestBodyBytes = new byte[0];
    private Dictionary<string, string> _latestChatRequestHeaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    private string _latestChatRequestModelKey = "";
    private string _pageAuthToken = "";
    private string _pageDeviceId = "";
    private string _pageLanguage = "";
    private string _currentRequestedModelName = "kimi-k2.5-fast";
    private string _currentModelKey = "quick";
    private string _currentModelUiLabel = "K2.5 快速";

    private TaskCompletionSource<bool> _pageLoadTcs = new TaskCompletionSource<bool>();
    
    // 状态管理
    private int _thinkIndex = -1;     
    private int _responseIndex = -1;  
    private bool _isThinkingOpen = false;
    private bool _hasResponseStarted = false;
    private string _lastEventType = ""; 

    private bool _isHiddenMode = false;
    
    // [V44] 设置：是否开启自动新话题
    private bool _enableAutoNewTopic = true; 

    private class KimiModelConfig
    {
        public string RequestedModelName;
        public string ModelKey;
        public string UiLabel;
        public bool UseThink;
        public bool UseSearch;
    }

    private class RelayAttachment
    {
        public string Name;
        public string MimeType;
        public string DataBase64;
    }

    public KimiForm(bool isSilent)
    {
        _isDebugMode = !isSilent; 
        _settingsFile = Path.Combine(_userDataFolder, "settings.json");
        _runtimeLogFile = Path.Combine(_userDataFolder, "relay-runtime.log");
        LoadSettings(); // 加载配置

        InitializeComponent();
        InitializeSystemTray(); 
        
        this.Load += async (s, e) => {
            _split.SplitterDistance = _split.Width - 400; 
            await LoadCustomIconAsync(); 
            await InitializeWebViewSequenceAsync(); 
        };
        
        this.Resize += (s, e) => {
            if (!_isHiddenMode) ResizeWebView();
        };
        
        StartHttpServer(); 
    }

    private void LoadSettings()
    {
        try {
            if (File.Exists(_settingsFile)) {
                string json = File.ReadAllText(_settingsFile);
                // 简单的手动 JSON 解析，避免引入依赖
                if (json.Contains("\"enableAutoNewTopic\": false")) _enableAutoNewTopic = false;
                else _enableAutoNewTopic = true;
            } else {
                _enableAutoNewTopic = true; // 默认值
            }
        } catch { _enableAutoNewTopic = true; }
    }

    private void SaveSettings()
    {
        try {
            if (!Directory.Exists(_userDataFolder)) Directory.CreateDirectory(_userDataFolder);
            string json = $"{{\"enableAutoNewTopic\": {(_enableAutoNewTopic ? "true" : "false")}}}";
            File.WriteAllText(_settingsFile, json);
        } catch { }
    }

    private void InitializeSystemTray()
    {
        _trayMenu = new ContextMenuStrip();
        
        // [V44] 开关菜单项
        _itemAutoNewTopic = new ToolStripMenuItem("自动开启新话题 (清理上下文)");
        _itemAutoNewTopic.CheckOnClick = true;
        _itemAutoNewTopic.Checked = _enableAutoNewTopic;
        _itemAutoNewTopic.Click += (s, e) => {
            _enableAutoNewTopic = _itemAutoNewTopic.Checked;
            SaveSettings();
            Log($"设置已更改: 自动开启新话题 = {_enableAutoNewTopic}");
        };
        _trayMenu.Items.Add(_itemAutoNewTopic);
        
        _trayMenu.Items.Add(new ToolStripSeparator());
        _trayMenu.Items.Add("显示/隐藏控制台", null, (s, e) => ToggleWindowVisibility());
        _trayMenu.Items.Add("重启浏览器内核", null, async (s, e) => await RestartWebViewAsync());
        _trayMenu.Items.Add(new ToolStripSeparator());
        _trayMenu.Items.Add("退出服务", null, (s, e) => this.Close());

        _notifyIcon = new NotifyIcon();
        _notifyIcon.Text = "Kimi Relay Service";
        _notifyIcon.Icon = SystemIcons.Application; 
        _notifyIcon.ContextMenuStrip = _trayMenu;
        _notifyIcon.Visible = true;
        _notifyIcon.DoubleClick += (s, e) => ToggleWindowVisibility();
    }

    private async Task LoadCustomIconAsync()
    {
        try {
            using (var client = new WebClient())
            {
                byte[] data = await client.DownloadDataTaskAsync(IconUrl);
                using (var ms = new MemoryStream(data))
                using (var bmp = new Bitmap(ms))
                {
                    var icon = Icon.FromHandle(bmp.GetHicon());
                    this.Icon = icon;
                    _notifyIcon.Icon = icon;
                }
            }
        } catch { }
    }

    private async Task RestartWebViewAsync()
    {
        Log("♻️ 正在重启浏览器内核...");
        if (_coreWebView != null)
        {
            try { 
                _coreWebView.ProcessFailed -= OnProcessFailed; 
                _coreWebView.Stop(); 
            } catch { }
        }
        
        if (_controller != null)
        {
            try { _controller.Close(); } catch { }
            _controller = null;
        }
        
        _coreWebView = null;
        _env = null;
        _isInitializing = false;

        await Task.Delay(1000);
        await InitializeWebViewSequenceAsync();
    }

    private async Task InitializeWebViewSequenceAsync()
    {
        if (_isInitializing) return;
        _isInitializing = true;
        try {
            Log("正在初始化 WebView2...");
            _env = await CoreWebView2Environment.CreateAsync(null, _userDataFolder);
            _controller = await _env.CreateCoreWebView2ControllerAsync(_webPanel.Handle);
            _coreWebView = _controller.CoreWebView2;
            
            _coreWebView.ProcessFailed += OnProcessFailed;

            ResizeWebView();

            await _coreWebView.AddScriptToExecuteOnDocumentCreatedAsync(GetNetworkInterceptorScript());
            _coreWebView.AddWebResourceRequestedFilter("https://www.kimi.com/apiv2/kimi.gateway.chat.v1.ChatService/Chat*", CoreWebView2WebResourceContext.All);
            _coreWebView.WebMessageReceived += OnWebMessageReceived;
            _coreWebView.WebResourceRequested += OnWebResourceRequested;
            _coreWebView.WebResourceResponseReceived += OnWebResourceResponseReceived;
            _coreWebView.NavigationCompleted += async (s, e) => {
                if (e.IsSuccess) {
                    await _coreWebView.ExecuteScriptAsync(GetDomControlScript());
                    await CapturePageAuthContextAsync();
                    Log("页面就绪 (DOM脚本已注入)"); 
                    _pageLoadTcs.TrySetResult(true);
                }
            };

            _pageLoadTcs = new TaskCompletionSource<bool>();
            _pageLoadTcs.TrySetResult(true); 
            _coreWebView.Navigate(TargetUrl);
            Log("✅ WebView2 启动成功");
        } 
        catch (Exception ex) 
        { 
            Log($"❌ 初始化失败: {ex.Message}"); 
        }
        finally
        {
            _isInitializing = false;
        }
    }

    private void OnProcessFailed(object sender, CoreWebView2ProcessFailedEventArgs e)
    {
        string reason = e.Reason.ToString();
        Log($"⚠️ 警告：浏览器进程崩溃！原因: {reason}");
        
        this.Invoke(new Action(async () => {
            Log("⏳ 3秒后尝试自动恢复...");
            await Task.Delay(3000);
            await RestartWebViewAsync();
        }));
    }

    private void ToggleWindowVisibility()
    {
        if (_isHiddenMode)
        {
            _isHiddenMode = false;
            if (this.Left < -10000) this.CenterToScreen();
            this.ShowInTaskbar = true;
            this.WindowState = FormWindowState.Normal;
            
            try {
                if (_controller != null) {
                    _controller.IsVisible = true;
                }
            } catch {
                Log("⚠️ 句柄失效，正在重建...");
                Task.Run(() => this.Invoke(new Action(async () => await RestartWebViewAsync())));
            }

            ResizeWebView(); 
            this.BringToFront();
            this.Activate();
        }
        else
        {
            _isHiddenMode = true;
            this.Location = new Point(-32000, -32000);
        }
    }

    private void InitializeComponent()
    {
        this.Text = $"Kimi Relay (Port: {ServerPort})";
        this.Size = new Size(1250, 850);
        this.StartPosition = FormStartPosition.CenterScreen;
        this.BackColor = Color.FromArgb(30, 30, 30);
        this.FormBorderStyle = FormBorderStyle.Sizable; 

        if (_isDebugMode) 
        {
            this.ShowInTaskbar = true;
            _isHiddenMode = false;
        }
        else
        {
            this.ShowInTaskbar = false;
            this.StartPosition = FormStartPosition.Manual;
            this.Location = new Point(-32000, -32000);
            _isHiddenMode = true;
        }

        _split = new SplitContainer();
        _split.Dock = DockStyle.Fill;
        _split.Orientation = Orientation.Vertical;
        _split.FixedPanel = FixedPanel.Panel2; 
        _split.BackColor = Color.FromArgb(45, 45, 48);
        this.Controls.Add(_split);

        _webPanel = new Panel();
        _webPanel.Dock = DockStyle.Fill;
        _webPanel.BackColor = Color.Black; 
        _split.Panel1.Controls.Add(_webPanel);
        _webPanel.Resize += (s, e) => ResizeWebView();

        _rightPanel = _split.Panel2;
        _rightPanel.Padding = new Padding(10);
        _rightPanel.BackColor = Color.FromArgb(30, 30, 30);

        var btnPanel = new Panel();
        btnPanel.Dock = DockStyle.Bottom;
        btnPanel.Height = 80; 
        btnPanel.BackColor = Color.Transparent;

        _btnRestart = new Button();
        _btnRestart.Text = "🔄 重启浏览器内核";
        _btnRestart.Height = 35;
        _btnRestart.Dock = DockStyle.Top;
        _btnRestart.BackColor = Color.SteelBlue;
        _btnRestart.ForeColor = Color.White;
        _btnRestart.FlatStyle = FlatStyle.Flat;
        _btnRestart.Click += async (s, e) => await RestartWebViewAsync();

        _btnToggle = new Button();
        _btnToggle.Text = "停止 HTTP 服务";
        _btnToggle.Height = 35;
        _btnToggle.Dock = DockStyle.Bottom;
        _btnToggle.BackColor = Color.IndianRed;
        _btnToggle.ForeColor = Color.White;
        _btnToggle.FlatStyle = FlatStyle.Flat;
        _btnToggle.Click += (s, e) => ToggleService();
        
        btnPanel.Controls.Add(_btnRestart);
        btnPanel.Controls.Add(_btnToggle);
        _rightPanel.Controls.Add(btnPanel);

        var infoPanel = new Panel();
        infoPanel.Height = 500;
        infoPanel.Dock = DockStyle.Top;
        
        var lblTitle = new Label { Text = "Kimi Relay", Font = new Font("Microsoft YaHei", 14, FontStyle.Bold), ForeColor = Color.CornflowerBlue, AutoSize = true, Location = new Point(0, 0) };
        infoPanel.Controls.Add(lblTitle);

        _lblStatus = new Label { Text = "✅ 正在运行 (Kimi)", ForeColor = Color.LightGreen, AutoSize = true, Location = new Point(0, 35) };
        infoPanel.Controls.Add(_lblStatus);

        int startY = 65;
        AddConfigItem(infoPanel, "Base URL (API地址):", $"http://127.0.0.1:{ServerPort}", startY);
        AddConfigItem(infoPanel, "API Key (任意填写):", "sk-any", startY + 50);

        var lblModels = new Label { Text = "可用模型组合 (点击框体复制):", ForeColor = Color.Orange, Location = new Point(0, startY + 110), AutoSize = true, Font = new Font("Microsoft YaHei", 9, FontStyle.Bold) };
        infoPanel.Controls.Add(lblModels);

        AddConfigItem(infoPanel, "K2.5 快速:", "kimi-k2.5-fast", startY + 135);
        AddConfigItem(infoPanel, "K2.5 快速+搜索:", "kimi-k2.5-fast-search", startY + 185);
        AddConfigItem(infoPanel, "K2.5 思考:", "kimi-k2.5-thinking", startY + 235);
        AddConfigItem(infoPanel, "K2.5 思考+搜索:", "kimi-k2.5-thinking-search", startY + 285);

        _rightPanel.Controls.Add(infoPanel);

        _txtLog = new TextBox();
        _txtLog.Multiline = true;
        _txtLog.ScrollBars = ScrollBars.Vertical;
        _txtLog.Dock = DockStyle.Fill;
        _txtLog.BackColor = Color.FromArgb(45, 45, 48);
        _txtLog.ForeColor = Color.Gainsboro;
        _txtLog.ReadOnly = true;
        _txtLog.BorderStyle = BorderStyle.FixedSingle;
        
        var logContainer = new Panel();
        logContainer.Dock = DockStyle.Fill;
        logContainer.Padding = new Padding(0, 10, 0, 10);
        logContainer.Controls.Add(_txtLog);
        _rightPanel.Controls.Add(logContainer);
        
        logContainer.BringToFront();
    }

    private void AddConfigItem(Panel panel, string title, string value, int y)
    {
        var lbl = new Label { Text = title, ForeColor = Color.Gray, Location = new Point(0, y), AutoSize = true, Font = new Font("Microsoft YaHei", 9) };
        var txt = new TextBox { 
            Text = value, ReadOnly = true, BackColor = Color.FromArgb(40,40,40), 
            ForeColor = Color.White, Location = new Point(0, y + 20), Width = 310, BorderStyle = BorderStyle.FixedSingle 
        };
        var btnCopy = new Button {
            Text = "复制", Location = new Point(320, y + 19), Width = 50, Height = 23,
            FlatStyle = FlatStyle.Flat, BackColor = Color.DimGray, ForeColor = Color.White, Font = new Font("Microsoft YaHei", 8)
        };
        btnCopy.FlatAppearance.BorderSize = 0;
        EventHandler copyAction = (s, e) => {
            try {
                Clipboard.SetText(value);
                string originalText = btnCopy.Text;
                btnCopy.Text = "✅"; btnCopy.ForeColor = Color.LightGreen;
                Task.Delay(1000).ContinueWith(_ => {
                    if (btnCopy.IsDisposed) return;
                    btnCopy.Invoke(new Action(() => { btnCopy.Text = originalText; btnCopy.ForeColor = Color.White; }));
                });
            } catch { }
        };
        btnCopy.Click += copyAction;
        txt.Click += copyAction;
        panel.Controls.Add(lbl); panel.Controls.Add(txt); panel.Controls.Add(btnCopy);
    }

    private async Task ResetToHomeAndWaitAsync()
    {
        Log("新对话：正在跳转回主页清理上下文...");
        _pageLoadTcs = new TaskCompletionSource<bool>(); 
        this.Invoke(new Action(async () => {
            try {
                if (_coreWebView != null && _coreWebView.BrowserProcessId != 0) {
                     _coreWebView.Navigate(TargetUrl);
                } else {
                    Log("⚠️ 浏览器进程无效，正在重启...");
                    await RestartWebViewAsync();
                }
            } catch (Exception ex) { 
                Log("⚠️ 导航失败: " + ex.Message); 
                await RestartWebViewAsync();
            }
        }));

        var completedTask = await Task.WhenAny(_pageLoadTcs.Task, Task.Delay(15000));
        if (completedTask != _pageLoadTcs.Task) Log("⚠️ 超时，强行继续...");
        await Task.Delay(2000); 
        Log("主页环境已就绪。");
    }

    private void ResizeWebView()
    {
        if (_controller != null && !_webPanel.IsDisposed && _webPanel.IsHandleCreated)
        {
            try {
                if (_webPanel.Width > 0 && _webPanel.Height > 0)
                {
                    _controller.Bounds = new Rectangle(0, 0, _webPanel.Width, _webPanel.Height);
                }
            } catch { /* 忽略错误 */ }
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        StopHttpServer();
        if (_notifyIcon != null) {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
        }
        base.OnFormClosing(e);
    }

    private void ToggleService()
    {
        if (_httpListener != null && _httpListener.IsListening) {
            StopHttpServer(); Log("服务已停止"); 
            _btnToggle.Text = "启动 HTTP 服务"; _btnToggle.BackColor = Color.SeaGreen;
            _lblStatus.Text = "⏹ 已停止"; _lblStatus.ForeColor = Color.Gray;
        } else {
            StartHttpServer(); Log("服务已启动"); 
            _btnToggle.Text = "停止 HTTP 服务"; _btnToggle.BackColor = Color.IndianRed;
            _lblStatus.Text = "✅ 正在运行"; _lblStatus.ForeColor = Color.LightGreen;
        }
    }

    private void StartHttpServer()
    {
        try {
            if (_httpListener != null) StopHttpServer(); 
            _httpListener = new HttpListener();
            _httpListener.Prefixes.Add($"http://127.0.0.1:{ServerPort}/");
            _httpListener.Start();
            Task.Run(ListenLoop);
            Log($"HTTP 服务监听中: {ServerPort}");
        } catch (Exception ex) { Log($"HTTP 启动失败: {ex.Message}"); }
    }

    private void StopHttpServer()
    {
        try { if (_httpListener != null) { _httpListener.Stop(); _httpListener.Close(); _httpListener = null; } } catch { }
    }

    private async Task ListenLoop()
    {
        var listener = _httpListener;
        if (listener == null) return;
        while (listener != null && listener.IsListening) {
            try { var context = await listener.GetContextAsync(); HandleRequest(context); } catch { break; }
        }
    }

    private async void HandleRequest(HttpListenerContext ctx)
    {
        string path = ctx.Request.Url.AbsolutePath.ToLower();
        string method = ctx.Request.HttpMethod.ToUpper();
        ctx.Response.AppendHeader("Access-Control-Allow-Origin", "*");
        ctx.Response.AppendHeader("Access-Control-Allow-Headers", "Content-Type, Authorization");

        if (method == "OPTIONS") { ctx.Response.StatusCode = 200; ctx.Response.Close(); return; }

        if (path == "/shutdown") {
            ResponseText(ctx, "Closing...");
            this.Invoke(new Action(() => { 
                Task.Delay(1000).ContinueWith(_ => this.Invoke(new Action(() => this.Close()))); 
            }));
            return;
        }

        // [V47] 增加获取模型列表接口
        if ((path == "/v1/models" || path == "/models") && method == "GET")
        {
            long now = DateTimeOffset.Now.ToUnixTimeSeconds();
            string modelsJson = $@"{{
                ""object"": ""list"",
                ""data"": [
                    {{""id"": ""kimi-k2.5-fast"", ""object"": ""model"", ""created"": {now}, ""owned_by"": ""kimi-relay""}},
                    {{""id"": ""kimi-k2.5-fast-search"", ""object"": ""model"", ""created"": {now}, ""owned_by"": ""kimi-relay""}},
                    {{""id"": ""kimi-k2.5-thinking"", ""object"": ""model"", ""created"": {now}, ""owned_by"": ""kimi-relay""}},
                    {{""id"": ""kimi-k2.5-thinking-search"", ""object"": ""model"", ""created"": {now}, ""owned_by"": ""kimi-relay""}}
                ]
            }}";
            ResponseText(ctx, modelsJson, 200, "application/json");
            return;
        }

        if ((path == "/v1/chat/completions" || path == "/chat") && method == "POST")
        {
            if (_currentContext != null) { ResponseText(ctx, "{\"error\": \"Busy\"}", 429, "application/json"); return; }
            try {
                using (var reader = new StreamReader(ctx.Request.InputStream, Encoding.UTF8)) {
                    string body = reader.ReadToEnd();
                    Log("收到请求");

                    // [V44] 检查自动新话题设置
                    if (_enableAutoNewTopic)
                    {
                        int roleCount = Regex.Matches(body, "\"role\"\\s*:\\s*\"").Count;
                        if (roleCount <= 2)
                        {
                            await ResetToHomeAndWaitAsync();
                        }
                        else
                        {
                            Log($"延续历史对话 ({roleCount}条消息)");
                        }
                    }
                    else
                    {
                        Log("延续对话 (自动新话题已关闭)");
                    }

                    bool isStream = body.Contains("\"stream\": true") || body.Contains("\"stream\":true");
                    
                    string requestedModelName = (ExtractValueFromJson(body, "model") ?? "kimi-k2.5-fast").ToLower();
                    KimiModelConfig modelConfig = ParseKimiModelConfig(requestedModelName);
                    string modelName = modelConfig.RequestedModelName;
                    bool useThink = modelConfig.UseThink;
                    bool useSearch = modelConfig.UseSearch;
                    _currentRequestedModelName = modelConfig.RequestedModelName;
                    _currentModelKey = modelConfig.ModelKey;
                    _currentModelUiLabel = modelConfig.UiLabel;
                    
                    List<RelayAttachment> attachments = ExtractAttachmentsFromJson(body);
                    string prompt = ExtractPromptFromJson(body);
                    if (string.IsNullOrWhiteSpace(prompt)) {
                        if (!body.Trim().StartsWith("{")) prompt = body;
                        else { ResponseText(ctx, "{\"error\": \"Parse Error\"}", 400, "application/json"); return; }
                    }

                    _currentContext = ctx;
                    _isCurrentRequestStreaming = isStream;
                    _networkBuffer.Clear(); 
                    _nonStreamResponseBuffer.Clear();
                    _citations.Clear(); 
                    
                    // 重置状态
                    _isThinkingOpen = false;
                    _hasResponseStarted = false; 
                    _lastEventType = "";
                    _thinkIndex = -1;
                    _responseIndex = -1;
                    _chatResponseHandled = false;
                    Log($"收到请求 (Model: {modelName}, Think: {(useThink ? "ON" : "OFF")}, Search: {(useSearch ? "ON" : "OFF")})");

                    if (isStream) {
                        ctx.Response.ContentType = "text/event-stream";
                        ctx.Response.AppendHeader("Cache-Control", "no-cache");
                        ctx.Response.AppendHeader("Connection", "keep-alive");
                    } else { ctx.Response.ContentType = "application/json"; }
                    
                    bool directSuccess = false;
                    bool hasTemplate = 
                        !string.IsNullOrWhiteSpace(_latestChatRequestUrl) &&
                        !string.IsNullOrWhiteSpace(_latestChatRequestBody) &&
                        string.Equals(_latestChatRequestModelKey, _currentModelKey, StringComparison.OrdinalIgnoreCase);
                    if (attachments.Count > 0)
                    {
                        Log($"检测到附件: {attachments.Count}，本次强制走网页上传链路");
                    }
                    else if (hasTemplate)
                    {
                        directSuccess = await SendDirectRequestAsync(prompt, useThink, useSearch);
                    }
                    if (!directSuccess)
                    {
                        this.Invoke(new Action(() => InjectPromptToWebView(prompt, useThink, useSearch, attachments)));
                    }
                }
            } catch (Exception ex) { Log($"Error: {ex.Message}"); if (_currentContext == ctx) CleanupRequest(); }
        } else { ResponseText(ctx, "Not Found", 404); }
    }

    private string ExtractPromptFromJson(string json)
    {
        try {
            string systemPrompt = "", userPrompt = "";
            var roleRegex = new Regex("\"role\"\\s*:\\s*\"(system|user|assistant)\"");
            var contentRegex = new Regex("\"content\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"");
            var roles = roleRegex.Matches(json).Cast<Match>().ToList();
            var contents = contentRegex.Matches(json).Cast<Match>().ToList();

            if (roles.Count > 0 && roles.Count == contents.Count) {
                for (int i = 0; i < roles.Count; i++) {
                    string r = roles[i].Groups[1].Value.ToLower();
                    string c = UnescapeJson(contents[i].Groups[1].Value);
                    if (r == "system") systemPrompt += c + "\n\n";
                    else if (r == "user") userPrompt = c; 
                }
            } else {
                var lastMatch = contents.LastOrDefault();
                if (lastMatch != null) userPrompt = UnescapeJson(lastMatch.Groups[1].Value);
            }
            string finalPrompt = "";
            if (!string.IsNullOrWhiteSpace(systemPrompt)) finalPrompt += "[System Instructions]:\n" + systemPrompt.Trim() + "\n\n[User Message]:\n";
            finalPrompt += userPrompt;
            return finalPrompt.Trim();
        } catch { return null; }
    }

    private List<RelayAttachment> ExtractAttachmentsFromJson(string json)
    {
        var attachments = new List<RelayAttachment>();
        try
        {
            var objectRegex = new Regex("\"attachments\"\\s*:\\s*\\[(.*?)\\]", RegexOptions.Singleline);
            var objectMatch = objectRegex.Match(json);
            if (!objectMatch.Success) return attachments;

            var itemRegex = new Regex("\\{(.*?)\\}", RegexOptions.Singleline);
            var items = itemRegex.Matches(objectMatch.Groups[1].Value);
            int fileIndex = 1;
            foreach (Match item in items)
            {
                string itemJson = "{" + item.Groups[1].Value + "}";
                string name = ExtractValueFromJson(itemJson, "name") ?? ExtractValueFromJson(itemJson, "filename") ?? ("file_" + fileIndex + ".bin");
                string mimeType = ExtractValueFromJson(itemJson, "mime_type") ?? ExtractValueFromJson(itemJson, "mimeType") ?? "application/octet-stream";
                string dataBase64 = ExtractValueFromJson(itemJson, "data_base64") ?? ExtractValueFromJson(itemJson, "base64");
                if (string.IsNullOrWhiteSpace(dataBase64)) continue;
                attachments.Add(new RelayAttachment {
                    Name = name,
                    MimeType = mimeType,
                    DataBase64 = dataBase64
                });
                fileIndex++;
            }
        }
        catch { }
        return attachments;
    }

    private KimiModelConfig ParseKimiModelConfig(string modelName)
    {
        string normalized = (modelName ?? "kimi-k2.5-fast").Trim().ToLower();
        bool useSearch = normalized.Contains("search");
        bool useThink = normalized.Contains("thinking") || normalized.Contains("think") || normalized.Contains("k1");
        string modelKey = "quick";
        string uiLabel = "K2.5 快速";

        if (normalized.Contains("agent-swarm") || normalized.Contains("agent_cluster") || normalized.Contains("agent-cluster") || normalized.Contains("agentswarm") || normalized.Contains("集群"))
        {
            modelKey = "agent-swarm";
            uiLabel = "K2.5 Agent 集群";
            useThink = false;
            useSearch = false;
        }
        else if (normalized.Contains("agent"))
        {
            modelKey = "agent";
            uiLabel = "K2.5 Agent";
            useThink = false;
            useSearch = false;
        }
        else if (useThink)
        {
            modelKey = useSearch ? "thinking-search" : "thinking";
            uiLabel = "K2.5 思考";
        }
        else
        {
            modelKey = useSearch ? "quick-search" : "quick";
            uiLabel = "K2.5 快速";
        }

        string canonicalName;
        switch (modelKey)
        {
            case "agent-swarm":
                canonicalName = "kimi-k2.5-agent-swarm";
                break;
            case "agent":
                canonicalName = "kimi-k2.5-agent";
                break;
            case "thinking-search":
                canonicalName = "kimi-k2.5-thinking-search";
                break;
            case "thinking":
                canonicalName = "kimi-k2.5-thinking";
                break;
            case "quick-search":
                canonicalName = "kimi-k2.5-fast-search";
                break;
            default:
                canonicalName = "kimi-k2.5-fast";
                break;
        }

        return new KimiModelConfig {
            RequestedModelName = canonicalName,
            ModelKey = modelKey,
            UiLabel = uiLabel,
            UseThink = useThink,
            UseSearch = useSearch
        };
    }

    private void InjectPromptToWebView(string prompt, bool useThink, bool useSearch, List<RelayAttachment> attachments)
    {
        string promptBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(prompt ?? ""));
        string modelLabelBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(_currentModelUiLabel ?? ""));
        StringBuilder attachmentsJsonBuilder = new StringBuilder();
        attachmentsJsonBuilder.Append("[");
        bool first = true;
        foreach (var a in attachments ?? new List<RelayAttachment>())
        {
            if (!first) attachmentsJsonBuilder.Append(",");
            first = false;
            attachmentsJsonBuilder.Append("{");
            attachmentsJsonBuilder.Append("\"name\":\"").Append(EscapeJson(a.Name ?? "attachment.bin")).Append("\",");
            attachmentsJsonBuilder.Append("\"mimeType\":\"").Append(EscapeJson(a.MimeType ?? "application/octet-stream")).Append("\",");
            attachmentsJsonBuilder.Append("\"dataBase64\":\"").Append(EscapeJson(a.DataBase64 ?? "")).Append("\"");
            attachmentsJsonBuilder.Append("}");
        }
        attachmentsJsonBuilder.Append("]");
        string attachmentsJson = attachmentsJsonBuilder.ToString();
        string attachmentsBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(attachmentsJson));
        if (_isDebugMode)
        {
            Log($"[REQ] body长度: {(_latestChatRequestBody ?? "").Length}");
        }
        string script = $"window.KimiBridge.sendPromptFromBase64(\"{promptBase64}\", {(useThink ? "true" : "false")}, {(useSearch ? "true" : "false")}, \"{modelLabelBase64}\", \"{attachmentsBase64}\")";
        if (_coreWebView != null) {
            try { _coreWebView.ExecuteScriptAsync(script); } catch { }
        }
    }

    private void OnWebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try {
            string msg = e.TryGetWebMessageAsString();
            if (msg.StartsWith("[LOG]")) { Log(msg); return; }
            if (_currentContext == null) return;
            if (msg.StartsWith("[NETWORK_DONE]")) { FinishRequest(); return; }
            if (msg.StartsWith("[NETWORK_DATA]")) { string rawData = msg.Substring(14); ProcessNetworkData(rawData); }
        } catch (Exception ex) { Log($"MsgError: {ex.Message}"); }
    }

    private async void OnWebResourceResponseReceived(object sender, CoreWebView2WebResourceResponseReceivedEventArgs e)
    {
        if (_currentContext == null) return;

        try
        {
            string url = e.Request.Uri ?? "";
            string method = e.Request.Method ?? "";
            string lowerUrl = url.ToLower();
            string lowerMethod = method.ToUpper();

            if (lowerMethod != "POST") return;
            if (!IsKimiChatEndpoint(url)) return;
            if (_chatResponseHandled) return;

            string contentType = "";
            try { contentType = e.Response.Headers.GetHeader("Content-Type") ?? ""; } catch { }
            Log($"[RES] {method} {url}");
            if (!string.IsNullOrWhiteSpace(contentType)) Log($"[RES] Content-Type: {contentType}");

            string text = "";
            try
            {
                using (var stream = await e.Response.GetContentAsync())
                {
                    if (stream == null) return;
                    using (var reader = new StreamReader(stream, Encoding.UTF8))
                    {
                        text = await reader.ReadToEndAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"ResReadError: {ex.Message}");
                return;
            }
            if (string.IsNullOrWhiteSpace(text)) return;

            if (_isDebugMode)
            {
                string snippet = text.Length > 200 ? text.Substring(0, 200) + "..." : text;
                Log($"[RES-BODY] {snippet.Replace("\r", "\\r").Replace("\n", "\\n")}");
            }

            // Kimi 的聊天接口已优先通过页面侧 fetch 拦截解析 connect+json 包体。
            // 原生响应钩子这里仅用于记录实际命中的 URL / Content-Type，避免重复消费并提前 FinishRequest。
        }
        catch (Exception ex)
        {
            Log($"ResHookError: {ex.Message}");
        }
    }

    private void OnWebResourceRequested(object sender, CoreWebView2WebResourceRequestedEventArgs e)
    {
        try
        {
            string url = e.Request.Uri ?? "";
            string method = e.Request.Method ?? "";
            if (!IsKimiChatEndpoint(url)) return;
            if (!string.Equals(method, "POST", StringComparison.OrdinalIgnoreCase)) return;

            string bodyText = "";
            try
            {
                var content = e.Request.Content;
                if (content != null)
                {
                    byte[] rawBytes;
                    if (content.CanSeek) content.Position = 0;
                    using (var ms = new MemoryStream())
                    {
                        content.CopyTo(ms);
                        rawBytes = ms.ToArray();
                    }
                    if (content.CanSeek) content.Position = 0;

                    bodyText = ExtractJsonPayloadFromConnectBody(rawBytes);
                    _latestChatRequestBodyBytes = rawBytes;
                }
            }
            catch (Exception ex)
            {
                Log("ReqBodyReadError: " + ex.Message);
            }

            Dictionary<string, string> headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                string[] headerNames = new[] {
                    "authorization",
                    "content-type",
                    "accept",
                    "accept-language",
                    "x-traffic-id",
                    "x-client-trace-id",
                    "x-msh-device-id",
                    "x-language",
                    "user-agent"
                };
                foreach (var headerName in headerNames)
                {
                    string value = "";
                    try { value = e.Request.Headers.GetHeader(headerName) ?? ""; } catch { value = ""; }
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        headers[headerName] = value;
                    }
                }
            }
            catch (Exception ex)
            {
                Log("ReqHeaderReadError: " + ex.Message);
            }

            if (!string.IsNullOrWhiteSpace(bodyText))
            {
                string prompt = ExtractPromptFromTemplateBody(bodyText);
                _latestChatRequestUrl = url;
                _latestChatRequestMethod = method;
                _latestChatRequestHeaders = new Dictionary<string, string>(headers, StringComparer.OrdinalIgnoreCase);
                _latestChatRequestBody = bodyText;
                _latestChatRequestModelKey = _currentModelKey;
                _latestChatRequestPrompt = prompt ?? _latestChatRequestPrompt;

                if (_isDebugMode)
                {
                    Log("[REQ-NATIVE] 已捕获原始请求体");
                    Log("[REQ-NATIVE] body长度: " + bodyText.Length);
                    string bodySnippet = bodyText.Length > 600 ? bodyText.Substring(0, 600) + "..." : bodyText;
                    Log("[REQ-NATIVE-PAYLOAD] " + bodySnippet.Replace("\r", "\\r").Replace("\n", "\\n"));
                }
            }
        }
        catch (Exception ex)
        {
            Log("ReqHookError: " + ex.Message);
        }
    }

    private void ProcessNetworkData(string rawChunk)
    {
        _networkBuffer.Append(rawChunk);
        string bufferStr = _networkBuffer.ToString();

        if (_isDebugMode && rawChunk.Length > 0) { 
             string debugSnippet = rawChunk.Length > 100 ? rawChunk.Substring(0, 100) + "..." : rawChunk;
             Log($"[RAW] {debugSnippet.Replace("\n", "\\n")}");
        }

        int lastNewLine = bufferStr.LastIndexOf('\n');
        if (lastNewLine == -1) return; 
        
        string processablePart = bufferStr.Substring(0, lastNewLine + 1);
        _networkBuffer.Remove(0, lastNewLine + 1);

        using (StringReader reader = new StringReader(processablePart))
        {
            string line;
            while ((line = reader.ReadLine()) != null) {
                if (string.IsNullOrWhiteSpace(line)) { _lastEventType = ""; continue; }
                line = line.Trim();

                if (line.StartsWith("event:")) {
                    _lastEventType = line.Substring(6).Trim();
                    continue;
                }

                if (line.StartsWith("data:")) {
                    if (_lastEventType == "title" || _lastEventType == "update_session" || _lastEventType == "close") continue;
                    line = line.Substring(5).Trim();
                }
                else if (line.StartsWith("message")) line = line.Substring(7).Trim(); 
                
                if (!line.StartsWith("{") && !line.StartsWith("[")) continue;

                if (TryProcessKimiConnectFrame(line)) {
                    continue;
                }

                string messageType = ExtractValueFromJson(line, "type");
                if (!string.IsNullOrEmpty(messageType))
                {
                    if (string.Equals(messageType, "THINK", StringComparison.OrdinalIgnoreCase))
                    {
                        _thinkIndex = 0; 
                        if (!_isThinkingOpen) { SendDelta("<think>\n"); _isThinkingOpen = true; }
                        var initContent = ExtractValueFromJson(line, "content");
                        if (!string.IsNullOrEmpty(initContent)) SendDelta(initContent);
                        continue;
                    }
                    else if (string.Equals(messageType, "RESPONSE", StringComparison.OrdinalIgnoreCase))
                    {
                        if (_isThinkingOpen) { SendDelta("\n</think>\n"); _isThinkingOpen = false; }
                        
                        _responseIndex = (_thinkIndex == 0) ? 1 : 0;
                        _hasResponseStarted = true; 
                        
                        var initContent = ExtractValueFromJson(line, "content");
                        if (!string.IsNullOrEmpty(initContent)) SendDelta(initContent);
                        continue;
                    }
                }

                bool isProcessed = false;
                
                // [V48] 核心解析：优先匹配具有路径标识的片段 (Web版特征)
                var fragmentMatch = Regex.Match(line, "\"p\"\\s*:\\s*\"response/fragments/(\\d+)/content\"");
                var valueMatch = Regex.Match(line, "\"v\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"");
                
                if (fragmentMatch.Success && valueMatch.Success)
                {
                    isProcessed = true;
                    int currentIndex = int.Parse(fragmentMatch.Groups[1].Value);
                    string content = UnescapeJson(valueMatch.Groups[1].Value);
                    
                    bool isThink = (currentIndex == _thinkIndex);
                    bool isResponse = (currentIndex == _responseIndex);

                    // 自动探测索引 (如果尚未确定索引)
                    if (_thinkIndex == -1 && _responseIndex == -1 && currentIndex == 0) isThink = true;

                    if (isThink) {
                        if (_hasResponseStarted) continue; // 丢弃迟到的思考
                        if (!_isThinkingOpen) { SendDelta("<think>\n"); _isThinkingOpen = true; }
                        SendDelta(content);
                    }
                    else if (isResponse) {
                        if (_isThinkingOpen) { SendDelta("\n</think>\n"); _isThinkingOpen = false; }
                        _hasResponseStarted = true;
                        SendDelta(content);
                    }
                    else {
                        // 兜底：如果是未知索引但内容有效，直接输出 (防止丢字)
                        SendDelta(content);
                    }
                }
                
                // [V48] 引用处理
                if (line.Contains("results\"") && line.Contains("\"v\":")) {
                    ProcessCitations(line);
                    isProcessed = true;
                }

                // [V48] 强力兜底：处理所有包含 v: "" 或 content: "" 但没有路径标识的散包
                if (!isProcessed)
                {
                    string content = ExtractValueFromJson(line, "content") 
                        ?? ExtractValueFromJson(line, "text")
                        ?? ExtractValueFromJson(line, "v");

                    if (!string.IsNullOrEmpty(content) && content != "FINISHED" && !line.Contains("/elapsed_secs"))
                    {
                         // 排除掉纯路径定义包 (如 "p": "xxxx")
                         if (!line.Contains("\"p\":") || line.Contains("\"v\":")) {
                             SendDelta(content);
                         }
                    }
                }
            }
        }
    }

    private bool TryProcessKimiConnectFrame(string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return false;
        if (!line.Contains("\"eventOffset\"")) return false;

        if (line.Contains("\"heartbeat\"") || line.Contains("\"done\"")) {
            return true;
        }

        string mask = ExtractValueFromJson(line, "mask") ?? "";
        string op = ExtractValueFromJson(line, "op") ?? "";

        if (mask == "block.text" || mask == "block.text.content")
        {
            string content = ExtractNestedTextContent(line);
            if (!string.IsNullOrEmpty(content)) {
                SendDelta(content);
            }
            return true;
        }

        if (mask.StartsWith("message"))
        {
            string role = ExtractValueFromJson(line, "role") ?? "";
            if (role == "user" || role == "system") return true;
            if (role == "assistant") return true;
        }

        if (line.Contains("\"lastRequest\"") || line.Contains("\"tools\"") || line.Contains("\"chat\":")) {
            return true;
        }

        if (!string.IsNullOrEmpty(op)) {
            return true;
        }

        return false;
    }

    private string ExtractNestedTextContent(string json)
    {
        var match = Regex.Match(json, "\"text\"\\s*:\\s*\\{[^{}]*\"content\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"");
        if (match.Success) return UnescapeJson(match.Groups[1].Value);
        return null;
    }

    private string ExtractPromptFromTemplateBody(string bodyText)
    {
        if (string.IsNullOrWhiteSpace(bodyText)) return "";
        var roleContentRegex = new Regex("\"role\"\\s*:\\s*\"user\"[\\s\\S]*?\"content\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"");
        var match = roleContentRegex.Match(bodyText);
        if (match.Success) return UnescapeJson(match.Groups[1].Value);

        var promptRegex = new Regex("\"prompt\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"");
        match = promptRegex.Match(bodyText);
        if (match.Success) return UnescapeJson(match.Groups[1].Value);

        return "";
    }

    private string ExtractJsonPayloadFromConnectBody(byte[] rawBytes)
    {
        if (rawBytes == null || rawBytes.Length == 0) return "";

        if (rawBytes.Length >= 5)
        {
            int length = (rawBytes[1] << 24) | (rawBytes[2] << 16) | (rawBytes[3] << 8) | rawBytes[4];
            if (length >= 0 && 5 + length <= rawBytes.Length)
            {
                return Encoding.UTF8.GetString(rawBytes, 5, length);
            }
        }

        return Encoding.UTF8.GetString(rawBytes);
    }

    private async Task CapturePageAuthContextAsync()
    {
        if (_coreWebView == null) return;

        try
        {
            string script = @"
            (() => {
                function collect(storage) {
                    const items = [];
                    try {
                        for (let i = 0; i < storage.length; i++) {
                            const key = storage.key(i);
                            items.push({ k: key, v: storage.getItem(key) || '' });
                        }
                    } catch (e) {}
                    return items;
                }

                const items = collect(window.localStorage).concat(collect(window.sessionStorage));
                let token = '';
                let deviceId = '';
                let language = '';
                for (const item of items) {
                    const key = (item.k || '').toLowerCase();
                    const value = item.v || '';
                    if (!token && /^eyJ[A-Za-z0-9_-]+\./.test(value)) token = value;
                    if (!token && (key.includes('token') || key.includes('auth')) && /^eyJ/.test(value)) token = value;
                    if (!deviceId && (key.includes('device') || key.includes('msh'))) deviceId = value;
                    if (!language && key.includes('lang')) language = value;
                }
                return JSON.stringify({
                    token: token,
                    deviceId: deviceId,
                    language: language
                });
            })();";

            string result = await _coreWebView.ExecuteScriptAsync(script);
            string json = DecodeScriptStringResult(result);
            if (string.IsNullOrWhiteSpace(json)) return;

            string token = ExtractValueFromJson(json, "token");
            string deviceId = ExtractValueFromJson(json, "deviceId");
            string language = ExtractValueFromJson(json, "language");

            if (!string.IsNullOrWhiteSpace(token)) _pageAuthToken = token;
            if (!string.IsNullOrWhiteSpace(deviceId)) _pageDeviceId = deviceId;
            if (!string.IsNullOrWhiteSpace(language)) _pageLanguage = language;

            if (_isDebugMode)
            {
                Log($"页面认证上下文: token={(!string.IsNullOrWhiteSpace(_pageAuthToken))}, deviceId={(!string.IsNullOrWhiteSpace(_pageDeviceId))}, language={(!string.IsNullOrWhiteSpace(_pageLanguage))}");
            }
        }
        catch (Exception ex)
        {
            Log("AuthContextError: " + ex.Message);
        }
    }

    private string DecodeScriptStringResult(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw) || raw == "null") return "";
        string text = raw;
        if (text.Length >= 2 && text[0] == '"' && text[text.Length - 1] == '"')
        {
            text = text.Substring(1, text.Length - 2);
        }
        return UnescapeJson(text);
    }

    private async Task<bool> SendDirectRequestAsync(string prompt, bool useThink, bool useSearch)
    {
        try
        {
            byte[] requestBytes = BuildDirectRequestBodyBytes(prompt, useThink, useSearch);
            if (requestBytes == null || requestBytes.Length == 0)
            {
                Log("Direct request skipped: body build failed");
                return false;
            }

            string requestUrl = !string.IsNullOrWhiteSpace(_latestChatRequestUrl)
                ? _latestChatRequestUrl
                : "https://www.kimi.com/apiv2/kimi.gateway.chat.v1.ChatService/Chat";

            Log("Direct request (C#): " + requestUrl);

            var request = (HttpWebRequest)WebRequest.Create(requestUrl);
            request.Method = string.IsNullOrWhiteSpace(_latestChatRequestMethod) ? "POST" : _latestChatRequestMethod;
            request.ContentType = GetHeaderValue("content-type", "application/connect+json");
            request.Accept = GetHeaderValue("accept", "*/*");
            request.UserAgent = GetHeaderValue("user-agent", "Mozilla/5.0");
            request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
            request.Referer = "https://www.kimi.com/";
            await ApplyCookiesAsync(request, requestUrl);

            ApplyHeaderIfPresent(request, "authorization");
            ApplyHeaderIfPresent(request, "accept-language");
            ApplyHeaderIfPresent(request, "x-traffic-id");
            ApplyHeaderIfPresent(request, "x-client-trace-id");
            ApplyHeaderIfPresent(request, "x-msh-device-id");
            ApplyHeaderIfPresent(request, "x-language");

            using (var reqStream = await request.GetRequestStreamAsync())
            {
                await reqStream.WriteAsync(requestBytes, 0, requestBytes.Length);
            }

            using (var response = (HttpWebResponse)await request.GetResponseAsync())
            using (var respStream = response.GetResponseStream())
            {
                if (respStream == null)
                {
                    Log("Direct request failed: empty response stream");
                    return false;
                }

                if ((response.ContentType ?? "").ToLower().Contains("application/connect+json"))
                {
                    await ProcessConnectJsonResponseAsync(respStream);
                }
                else
                {
                    using (var reader = new StreamReader(respStream, Encoding.UTF8))
                    {
                        string text = await reader.ReadToEndAsync();
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            ProcessNetworkData(text.EndsWith("\n") ? text : text + "\n");
                        }
                    }
                }
            }

            FinishRequest();
            return true;
        }
        catch (Exception ex)
        {
            Log("Direct request failed, fallback to UI: " + ex.Message);
            _networkBuffer.Clear();
            _nonStreamResponseBuffer.Clear();
            _chatResponseHandled = false;
            return false;
        }
    }

    private async Task<bool> SendPageDirectRequestAsync(string prompt, bool useThink, bool useSearch)
    {
        try
        {
            byte[] requestBytes = BuildColdStartDirectRequestBodyBytes(prompt, useThink, useSearch);
            if (requestBytes == null || requestBytes.Length == 0)
            {
                Log("Page direct request skipped: body build failed");
                return false;
            }

            string bodyBase64 = Convert.ToBase64String(requestBytes);
            string script = $"window.KimiBridge.sendDirectRequestFromBase64(\"{bodyBase64}\")";
            string result = await ExecuteScriptOnUiThreadAsync(script);
            string trimmed = (result ?? "").Trim();
            bool success = !string.Equals(trimmed, "false", StringComparison.OrdinalIgnoreCase) &&
                           !string.Equals(trimmed, "null", StringComparison.OrdinalIgnoreCase) &&
                           trimmed != "";
            if (success)
            {
                Log("Direct request (Page): /apiv2/kimi.gateway.chat.v1.ChatService/Chat");
            }
            else
            {
                Log("Page direct request skipped: script returned " + trimmed);
            }
            return success;
        }
        catch (Exception ex)
        {
            Log("Page direct request failed: " + ex.Message);
            return false;
        }
    }

    private Task<string> ExecuteScriptOnUiThreadAsync(string script)
    {
        var tcs = new TaskCompletionSource<string>();

        Action action = async () =>
        {
            try
            {
                string result = await _coreWebView.ExecuteScriptAsync(script);
                tcs.TrySetResult(result);
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        };

        if (this.InvokeRequired) this.BeginInvoke(action);
        else action();

        return tcs.Task;
    }

    private byte[] BuildDirectRequestBodyBytes(string prompt, bool useThink, bool useSearch)
    {
        if (_latestChatRequestBodyBytes == null || _latestChatRequestBodyBytes.Length == 0)
        {
            return BuildColdStartDirectRequestBodyBytes(prompt, useThink, useSearch);
        }

        string oldPromptEscaped = EscapeJson(_latestChatRequestPrompt ?? "");
        string newPromptEscaped = EscapeJson(prompt ?? "");
        string body = _latestChatRequestBody;

        if (!string.IsNullOrWhiteSpace(oldPromptEscaped))
        {
            body = body.Replace(oldPromptEscaped, newPromptEscaped);
        }

        body = Regex.Replace(body, "\"thinking\"\\s*:\\s*(true|false)", "\"thinking\":" + (useThink ? "true" : "false"));

        if (!useSearch)
        {
            body = Regex.Replace(body, ",?\\s*\\{\\s*\"type\"\\s*:\\s*\"TOOL_TYPE_SEARCH\"\\s*,\\s*\"search\"\\s*:\\s*\\{\\s*\\}\\s*\\}", "");
        }
        else if (!body.Contains("\"TOOL_TYPE_SEARCH\""))
        {
            body = body.Replace("\"tools\":[]", "\"tools\":[{\"type\":\"TOOL_TYPE_SEARCH\",\"search\":{}}]");
        }

        byte[] payloadBytes = Encoding.UTF8.GetBytes(body);

        if (_latestChatRequestBodyBytes.Length >= 5)
        {
            byte[] framedBytes = new byte[payloadBytes.Length + 5];
            framedBytes[0] = _latestChatRequestBodyBytes[0];
            framedBytes[1] = (byte)((payloadBytes.Length >> 24) & 0xFF);
            framedBytes[2] = (byte)((payloadBytes.Length >> 16) & 0xFF);
            framedBytes[3] = (byte)((payloadBytes.Length >> 8) & 0xFF);
            framedBytes[4] = (byte)(payloadBytes.Length & 0xFF);
            Buffer.BlockCopy(payloadBytes, 0, framedBytes, 5, payloadBytes.Length);
            return framedBytes;
        }

        return payloadBytes;
    }

    private byte[] BuildColdStartDirectRequestBodyBytes(string prompt, bool useThink, bool useSearch)
    {
        string escapedPrompt = EscapeJson(prompt ?? "");
        string toolsJson = useSearch ? "[{\"type\":\"TOOL_TYPE_SEARCH\",\"search\":{}}]" : "[]";
        string payload = "{" +
            "\"scenario\":\"SCENARIO_K2D5\"," +
            "\"tools\":" + toolsJson + "," +
            "\"message\":{" +
                "\"role\":\"user\"," +
                "\"blocks\":[{\"message_id\":\"\",\"text\":{\"content\":\"" + escapedPrompt + "\"}}]," +
                "\"scenario\":\"SCENARIO_K2D5\"" +
            "}," +
            "\"options\":{\"thinking\":" + (useThink ? "true" : "false") + "}" +
        "}";

        byte[] payloadBytes = Encoding.UTF8.GetBytes(payload);
        byte[] framedBytes = new byte[payloadBytes.Length + 5];
        framedBytes[0] = 0x00;
        framedBytes[1] = (byte)((payloadBytes.Length >> 24) & 0xFF);
        framedBytes[2] = (byte)((payloadBytes.Length >> 16) & 0xFF);
        framedBytes[3] = (byte)((payloadBytes.Length >> 8) & 0xFF);
        framedBytes[4] = (byte)(payloadBytes.Length & 0xFF);
        Buffer.BlockCopy(payloadBytes, 0, framedBytes, 5, payloadBytes.Length);
        return framedBytes;
    }

    private string GetHeaderValue(string name, string fallback)
    {
        string value;
        if (_latestChatRequestHeaders.TryGetValue(name, out value) && !string.IsNullOrWhiteSpace(value))
            return value;
        if (string.Equals(name, "authorization", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(_pageAuthToken))
            return "Bearer " + _pageAuthToken;
        if (string.Equals(name, "x-msh-device-id", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(_pageDeviceId))
            return _pageDeviceId;
        if (string.Equals(name, "x-language", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(_pageLanguage))
            return _pageLanguage;
        return fallback;
    }

    private void ApplyHeaderIfPresent(HttpWebRequest request, string name)
    {
        string value;
        if (!_latestChatRequestHeaders.TryGetValue(name, out value) || string.IsNullOrWhiteSpace(value))
        {
            value = GetHeaderValue(name, "");
            if (string.IsNullOrWhiteSpace(value)) return;
        }

        request.Headers[name] = value;
    }

    private async Task ApplyCookiesAsync(HttpWebRequest request, string requestUrl)
    {
        if (_coreWebView == null) return;

        try
        {
            var cookies = await GetCookiesOnUiThreadAsync(requestUrl);
            if (cookies == null || cookies.Count == 0) return;

            request.CookieContainer = new CookieContainer();
            foreach (var cookie in cookies)
            {
                try
                {
                    var netCookie = new Cookie(cookie.Name, cookie.Value, cookie.Path, cookie.Domain);
                    if (cookie.Expires > DateTime.MinValue) netCookie.Expires = cookie.Expires;
                    netCookie.HttpOnly = cookie.IsHttpOnly;
                    netCookie.Secure = cookie.IsSecure;
                    request.CookieContainer.Add(netCookie);
                }
                catch { }
            }
        }
        catch (Exception ex)
        {
            Log("CookieApplyError: " + ex.Message);
        }
    }

    private Task<IList<CoreWebView2Cookie>> GetCookiesOnUiThreadAsync(string requestUrl)
    {
        var tcs = new TaskCompletionSource<IList<CoreWebView2Cookie>>();

        Action action = async () =>
        {
            try
            {
                var cookies = await _coreWebView.CookieManager.GetCookiesAsync(requestUrl);
                tcs.TrySetResult(cookies);
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        };

        if (this.InvokeRequired) this.BeginInvoke(action);
        else action();

        return tcs.Task;
    }

    private async Task ProcessConnectJsonResponseAsync(Stream respStream)
    {
        using (var ms = new MemoryStream())
        {
            await respStream.CopyToAsync(ms);
            byte[] data = ms.ToArray();
            int offset = 0;
            int frameCount = 0;
            while (offset + 5 <= data.Length)
            {
                byte flags = data[offset];
                int length = (data[offset + 1] << 24) | (data[offset + 2] << 16) | (data[offset + 3] << 8) | data[offset + 4];
                if (length < 0 || offset + 5 + length > data.Length) break;
                string frameText = Encoding.UTF8.GetString(data, offset + 5, length);
                if ((flags & 0x02) == 0x02)
                {
                    if (!string.IsNullOrWhiteSpace(frameText) && frameText != "{}")
                    {
                        Log("[DIRECT-ERROR-FRAME] " + (frameText.Length > 400 ? frameText.Substring(0, 400) : frameText));
                    }
                    offset += 5 + length;
                    continue;
                }
                if (!string.IsNullOrWhiteSpace(frameText))
                {
                    frameCount++;
                    ProcessNetworkData(frameText.EndsWith("\n") ? frameText : frameText + "\n");
                }
                offset += 5 + length;
            }
            if (frameCount == 0)
            {
                Log("Direct request failed: no usable connect frames");
            }
        }
    }

    private void ProcessCitations(string json)
    {
         try {
            var urlMatches = Regex.Matches(json, "\"url\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"");
            var titleMatches = Regex.Matches(json, "\"title\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"");
            int count = Math.Min(urlMatches.Count, titleMatches.Count);
            for (int i = 0; i < count; i++) {
                string url = UnescapeJson(urlMatches[i].Groups[1].Value);
                string title = UnescapeJson(titleMatches[i].Groups[1].Value);
                string citationEntry = $"[{_citations.Count + 1}] [{title}]({url})";
                if (!_citations.Contains(citationEntry)) _citations.Add(citationEntry);
            }
        } catch { }
    }

    private string ExtractValueFromJson(string json, string key) {
        var regex = new Regex($"\"{key}\":\\s*\"((?:[^\"\\\\]|\\\\.)*)\"");
        var match = regex.Match(json);
        if (match.Success) return UnescapeJson(match.Groups[1].Value);
        return null;
    }

    private string UnescapeJson(string str) {
        return str.Replace("\\\"", "\"").Replace("\\\\", "\\").Replace("\\n", "\n").Replace("\\r", "\r").Replace("\\t", "\t");
    }

    private void SendDelta(string text) {
        if (!_isCurrentRequestStreaming) {
            _nonStreamResponseBuffer.Append(text);
        }

        if (_isCurrentRequestStreaming) {
            string jsonChunk = $"{{\"id\":\"chatcmpl-kimi\",\"object\":\"chat.completion.chunk\",\"created\":{DateTimeOffset.Now.ToUnixTimeSeconds()},\"model\":\"{EscapeJson(_currentRequestedModelName)}\",\"choices\":[{{\"index\":0,\"delta\":{{\"content\":\"{EscapeJson(text)}\"}},\"finish_reason\":null}}]}}";
            WriteSseData($"data: {jsonChunk}\n\n");
        }
        Log($"[NET] {text.Replace("\n", "\\n")}");
    }

    private void FinishRequest() {
        Log("Stream Finished");
        if (_networkBuffer.Length > 0) { ProcessNetworkData("\n"); _networkBuffer.Clear(); }
        
        if (_isThinkingOpen) {
            SendDelta("\n</think>\n");
            _isThinkingOpen = false;
        }

        if (_citations.Count > 0) {
            StringBuilder sb = new StringBuilder();
            sb.Append("\n\n**引用来源：**\n");
            foreach (var cite in _citations) sb.Append(cite + "\n");
            SendDelta(sb.ToString());
        }
        if (_isCurrentRequestStreaming) {
            string stopChunk = $"{{\"id\":\"chatcmpl-kimi\",\"object\":\"chat.completion.chunk\",\"created\":{DateTimeOffset.Now.ToUnixTimeSeconds()},\"model\":\"{EscapeJson(_currentRequestedModelName)}\",\"choices\":[{{\"index\":0,\"delta\":{{}},\"finish_reason\":\"stop\"}}]}}";
            WriteSseData($"data: {stopChunk}\n\n");
            WriteSseData("data: [DONE]\n\n");
        } else {
            string content = _nonStreamResponseBuffer.ToString();
            string json = $"{{\"id\":\"chatcmpl-kimi\",\"object\":\"chat.completion\",\"created\":{DateTimeOffset.Now.ToUnixTimeSeconds()},\"model\":\"{EscapeJson(_currentRequestedModelName)}\",\"choices\":[{{\"index\":0,\"message\":{{\"role\":\"assistant\",\"content\":\"{EscapeJson(content)}\"}},\"finish_reason\":\"stop\"}}]}}";
            ResponseText(_currentContext, json, 200, "application/json");
        }
        CleanupRequest();
    }

    private void WriteSseData(string data) {
        try {
            byte[] buffer = Encoding.UTF8.GetBytes(data);
            _currentContext.Response.OutputStream.Write(buffer, 0, buffer.Length);
            _currentContext.Response.OutputStream.Flush();
        } catch { CleanupRequest(); }
    }

    private void CleanupRequest() {
        try { _currentContext?.Response.Close(); } catch { }
        _currentContext = null;
        _networkBuffer.Clear();
        _nonStreamResponseBuffer.Clear();
        _chatResponseHandled = false;
    }

    private void ResponseText(HttpListenerContext ctx, string text, int code = 200, string contentType = "text/plain") {
        try {
            ctx.Response.StatusCode = code; ctx.Response.ContentType = contentType;
            byte[] buffer = Encoding.UTF8.GetBytes(text);
            ctx.Response.ContentLength64 = buffer.Length;
            ctx.Response.OutputStream.Write(buffer, 0, buffer.Length);
            ctx.Response.Close();
        } catch { }
    }

    private string EscapeJson(string s) {
        if (s == null) return "";
        var sb = new StringBuilder(s.Length + 16);
        foreach (char ch in s)
        {
            switch (ch)
            {
                case '\\': sb.Append("\\\\"); break;
                case '"': sb.Append("\\\""); break;
                case '\r': sb.Append("\\r"); break;
                case '\n': sb.Append("\\n"); break;
                case '\t': sb.Append("\\t"); break;
                case '\b': sb.Append("\\b"); break;
                case '\f': sb.Append("\\f"); break;
                default:
                    if (char.IsControl(ch))
                    {
                        sb.Append("\\u");
                        sb.Append(((int)ch).ToString("x4"));
                    }
                    else
                    {
                        sb.Append(ch);
                    }
                    break;
            }
        }
        return sb.ToString();
    }

    private void Log(string msg) {
        WriteRuntimeLog(msg);
        if (!_isDebugMode) return;
        if (_txtLog != null && !_txtLog.IsDisposed) {
            if (_txtLog.InvokeRequired) _txtLog.Invoke(new Action(() => AppendLogToUi(msg)));
            else AppendLogToUi(msg);
        }
    }

    private void AppendLogToUi(string msg)
    {
        if (_txtLog == null || _txtLog.IsDisposed) return;
        _txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {msg}\r\n");
    }

    private void WriteRuntimeLog(string msg)
    {
        try
        {
            if (!Directory.Exists(_userDataFolder)) Directory.CreateDirectory(_userDataFolder);
            string safeMsg = msg ?? "";
            safeMsg = Regex.Replace(safeMsg, "(?i)(\"authorization\"\\s*:\\s*\")([^\"]+)(\")", "$1***REDACTED***$3");
            safeMsg = Regex.Replace(safeMsg, "(?i)(authorization\\s*[:=]\\s*bearer\\s+)([^\\s]+)", "$1***REDACTED***");
            string line = $"[{DateTime.Now:HH:mm:ss}] {safeMsg}{Environment.NewLine}";
            File.AppendAllText(_runtimeLogFile, line, Encoding.UTF8);
            File.AppendAllText(_workspaceLogFile, line, Encoding.UTF8);
        }
        catch { }
    }

    private string GetNetworkInterceptorScript() {
        return @"
        window.chrome.webview.postMessage('[LOG] Interceptor Mounting (V45)...');
        const kimiGetUrlInfo = (rawUrl) => {
            try {
                const parsed = new URL(rawUrl, window.location.origin);
                return {
                    href: parsed.href,
                    hostname: (parsed.hostname || '').toLowerCase(),
                    pathname: (parsed.pathname || '').toLowerCase()
                };
            } catch (e) {
                return {
                    href: rawUrl || '',
                    hostname: '',
                    pathname: (rawUrl || '').toLowerCase()
                };
            }
        };
        const kimiShouldCapture = (url, method) => {
            const info = kimiGetUrlInfo(url);
            const lowerMethod = (method || 'GET').toUpperCase();
            if (lowerMethod !== 'POST') return false;
            if (info.hostname !== 'www.kimi.com' && info.hostname !== 'kimi.com') return false;
            return info.pathname.indexOf('/apiv2/kimi.gateway.chat.v1.chatservice/chat') !== -1;
        };
        const kimiConcatBytes = (left, right) => {
            if (!left || left.length === 0) return right;
            const merged = new Uint8Array(left.length + right.length);
            merged.set(left, 0);
            merged.set(right, left.length);
            return merged;
        };
        const kimiParseConnectFrames = (buffer) => {
            const frames = [];
            let offset = 0;
            while (buffer && offset + 5 <= buffer.length) {
                const flags = buffer[offset];
                const length =
                    (buffer[offset + 1] << 24) |
                    (buffer[offset + 2] << 16) |
                    (buffer[offset + 3] << 8) |
                    buffer[offset + 4];
                if (length < 0 || offset + 5 + length > buffer.length) break;
                frames.push({
                    flags,
                    payload: buffer.slice(offset + 5, offset + 5 + length)
                });
                offset += 5 + length;
            }
            return {
                frames,
                remaining: buffer.slice(offset)
            };
        };
        const kimiReadResponse = async (response, sourceTag, url) => {
            try {
                const contentType = (response.headers.get('content-type') || '').toLowerCase();
                window.chrome.webview.postMessage('[LOG] [' + sourceTag + '] Content-Type: ' + contentType + ' @ ' + url);
                const clone = response.clone();
                const reader = clone.body && clone.body.getReader ? clone.body.getReader() : null;
                if (!reader) {
                    const text = await clone.text();
                    if (text) window.chrome.webview.postMessage('[NETWORK_DATA]' + text + '\n');
                    window.chrome.webview.postMessage('[NETWORK_DONE]');
                    return;
                }
                if (contentType.indexOf('application/connect+json') !== -1) {
                    let buffer = new Uint8Array(0);
                    const decoder = new TextDecoder();
                    while (true) {
                        const { done, value } = await reader.read();
                        if (done) break;
                        if (!value || value.length === 0) continue;
                        buffer = kimiConcatBytes(buffer, value);
                        const parsed = kimiParseConnectFrames(buffer);
                        buffer = parsed.remaining;
                        parsed.frames.forEach(frame => {
                            if ((frame.flags & 0x02) === 0x02) return;
                            const text = decoder.decode(frame.payload);
                            if (text) {
                                window.chrome.webview.postMessage('[LOG] [' + sourceTag + '] Connect frame: ' + text.substring(0, 200));
                                window.chrome.webview.postMessage('[NETWORK_DATA]' + text + '\n');
                            }
                        });
                    }
                    window.chrome.webview.postMessage('[NETWORK_DONE]');
                    return;
                }
                const decoder = new TextDecoder();
                while (true) {
                    const { done, value } = await reader.read();
                    if (done) {
                        window.chrome.webview.postMessage('[NETWORK_DONE]');
                        break;
                    }
                    const text = decoder.decode(value, {stream: true});
                    if (text) window.chrome.webview.postMessage('[NETWORK_DATA]' + text);
                }
            } catch (e) {
                window.chrome.webview.postMessage('[LOG] ' + sourceTag + ' Stream Error: ' + e);
            }
        };
        const originalFetch = window.fetch;
        window.fetch = async (...args) => {
            let url = '';
            let method = 'GET';
            try {
                if (args[0] instanceof Request) {
                    url = args[0].url;
                    method = args[0].method || 'GET';
                } else {
                    url = args[0].toString();
                    if (args[1] && args[1].method) method = args[1].method;
                }
            } catch(e) { url = 'unknown'; }
            if (kimiShouldCapture(url, method)) {
                window.chrome.webview.postMessage('[LOG] [Fetch] Intercepted: ' + method + ' ' + url);
                try {
                    const response = await originalFetch(...args);
                    kimiReadResponse(response, 'Fetch', url);
                    return response;
                } catch (e) { window.chrome.webview.postMessage('[LOG] Fetch Exec Error: ' + e); return originalFetch(...args); }
            }
            return originalFetch(...args);
        };
        const OriginalXHR = window.XMLHttpRequest;
        window.XMLHttpRequest = function() {
            const xhr = new OriginalXHR();
            let url = '';
            let method = 'GET';
            const originalOpen = xhr.open;
            xhr.open = function(requestMethod, requestUrl) {
                method = requestMethod || 'GET';
                url = requestUrl || '';
                if (kimiShouldCapture(url, method)) { window.chrome.webview.postMessage('[LOG] [XHR] Intercepted: ' + method + ' ' + url); }
                return originalOpen.apply(this, arguments);
            };
            xhr.addEventListener('progress', function() {
                if (kimiShouldCapture(url, method)) {
                    try {
                        let fullText = '';
                        try { fullText = xhr.responseText; } catch(e) { return; }
                        if (!fullText) return;
                        const lastLen = xhr._lastLength || 0;
                        const newChunk = fullText.substring(lastLen);
                        if (newChunk.length > 0) {
                            window.chrome.webview.postMessage('[NETWORK_DATA]' + newChunk);
                            xhr._lastLength = fullText.length;
                        }
                    } catch(e) { window.chrome.webview.postMessage('[LOG] XHR Progress Error: ' + e); }
                }
            });
            xhr.addEventListener('load', function() {
                if (kimiShouldCapture(url, method)) {
                    try {
                        const fullText = xhr.responseText || '';
                        const lastLen = xhr._lastLength || 0;
                        const newChunk = fullText.substring(lastLen);
                        if (newChunk.length > 0) {
                            window.chrome.webview.postMessage('[NETWORK_DATA]' + newChunk);
                            xhr._lastLength = fullText.length;
                        }
                    } catch(e) {}
                    window.chrome.webview.postMessage('[NETWORK_DONE]');
                }
            });
            return xhr;
        };
        window.chrome.webview.postMessage('[LOG] Interceptor Ready');
        ";
    }

    private bool LooksLikeAiResponse(string text, string url, string contentType)
    {
        string lowerText = (text ?? "").ToLower();
        string lowerType = (contentType ?? "").ToLower();

        if (!IsKimiChatEndpoint(url)) return false;
        if (lowerType.Contains("application/connect+json")) return true;
        if (lowerType.Contains("event-stream")) return true;
        if (lowerText.Contains("\"response\"")) return true;
        if (lowerText.Contains("\"fragments\"")) return true;
        if (lowerText.Contains("\"content\"")) return true;
        if (lowerText.Contains("\"text\"")) return true;
        if (lowerText.Contains("data: {")) return true;

        return false;
    }

    private bool IsKimiChatEndpoint(string url)
    {
        string lowerUrl = (url ?? "").ToLower();
        return lowerUrl.Contains("https://www.kimi.com/apiv2/kimi.gateway.chat.v1.chatservice/chat")
            || lowerUrl.Contains("https://kimi.com/apiv2/kimi.gateway.chat.v1.chatservice/chat");
    }

    private string GetDomControlScript() {
        return @"
        function kimiSetContentEditableText(editor, text) {
            const escapeHtml = (value) => value
                .replace(/&/g, '&amp;')
                .replace(/</g, '&lt;')
                .replace(/>/g, '&gt;');

            editor.focus();
            editor.innerHTML = '<p><br></p>';

            try {
                const selection = window.getSelection();
                const range = document.createRange();
                range.selectNodeContents(editor);
                range.collapse(true);
                selection.removeAllRanges();
                selection.addRange(range);
            } catch (e) {}

            let inserted = false;
            try {
                inserted = document.execCommand('insertText', false, text);
            } catch (e) {}

            if (!inserted || !(editor.innerText || '').trim()) {
                const lines = text.split(/\r?\n/);
                editor.innerHTML = lines.map(line => '<p>' + (line ? escapeHtml(line) : '<br>') + '</p>').join('');
            }

            editor.dispatchEvent(new Event('input', { bubbles: true }));
        }

        window.KimiBridge = {
            sendPromptFromBase64: function(textBase64, useThink, useSearch, modelLabelBase64, attachmentsBase64) {
                const decodeBase64 = (value) => {
                    if (!value) return '';
                    try {
                        const binary = atob(value);
                        const bytes = Uint8Array.from(binary, ch => ch.charCodeAt(0));
                        return new TextDecoder().decode(bytes);
                    } catch (e) {
                        window.chrome.webview.postMessage('[LOG] Base64 decode failed: ' + e);
                        return '';
                    }
                };
                return this.sendPrompt(
                    decodeBase64(textBase64),
                    useThink,
                    useSearch,
                    decodeBase64(modelLabelBase64),
                    JSON.parse(decodeBase64(attachmentsBase64) || '[]')
                );
            },
            sendDirectRequestFromBase64: function(bodyBase64) {
                try {
                    const binary = atob(bodyBase64 || '');
                    const bytes = Uint8Array.from(binary, ch => ch.charCodeAt(0));
                    window.chrome.webview.postMessage('[LOG] Attempt: Direct Fetch');
                    fetch('/apiv2/kimi.gateway.chat.v1.ChatService/Chat', {
                        method: 'POST',
                        headers: {
                            'Content-Type': 'application/connect+json',
                            'Accept': 'application/connect+json, application/json, text/plain, */*'
                        },
                        credentials: 'include',
                        body: bytes
                    }).then(response => {
                        window.chrome.webview.postMessage('[LOG] Direct Fetch Status: ' + response.status);
                    }).catch(e => {
                        window.chrome.webview.postMessage('[LOG] Direct Fetch Error: ' + e);
                    });
                    return true;
                } catch (e) {
                    window.chrome.webview.postMessage('[LOG] Direct Fetch Error: ' + e);
                    return false;
                }
            },
            sendPrompt: function(text, useThink, useSearch, modelLabel, attachments) {
                const clickModelOption = (label, callback) => {
                    if (!label) {
                        callback();
                        return;
                    }
                    const currentModel = document.querySelector('.current-model');
                    if (!currentModel) {
                        window.chrome.webview.postMessage('[LOG] Model selector not found');
                        callback();
                        return;
                    }

                    const openTargets = [
                        currentModel,
                        currentModel.querySelector('.model-name'),
                        currentModel.querySelector('.arrow')
                    ].filter(Boolean);
                    openTargets.forEach(target => {
                        try {
                            target.dispatchEvent(new MouseEvent('mousedown', { view: window, bubbles: true, cancelable: true }));
                            target.dispatchEvent(new MouseEvent('click', { view: window, bubbles: true, cancelable: true }));
                            target.dispatchEvent(new MouseEvent('mouseup', { view: window, bubbles: true, cancelable: true }));
                        } catch (e) {}
                    });

                    setTimeout(() => {
                        const exactName = Array.from(document.querySelectorAll('.models-container .model-item .model-name .name'))
                            .find(el => ((el.textContent || '').trim() === label));
                        let target = exactName ? exactName.closest('.model-item-content') || exactName.closest('.model-item') || exactName : null;

                        if (!target) {
                            const candidates = Array.from(document.querySelectorAll('.models-container .model-item')).filter(el => {
                                const txt = (el.innerText || '').trim().replace(/\s+/g, ' ');
                                if (!txt) return false;
                                return txt.startsWith(label) || txt.includes(label);
                            });
                            target = candidates[0] || null;
                        }

                        if (!target) {
                            const sampleTexts = Array.from(document.querySelectorAll('.models-container .model-item'))
                                .map(el => (el.innerText || '').trim().replace(/\s+/g, ' '))
                                .filter(Boolean)
                                .slice(0, 10);
                            window.chrome.webview.postMessage('[LOG] Model options visible: ' + sampleTexts.join(' | '));
                            window.chrome.webview.postMessage('[LOG] Model option not found: ' + label);
                            callback();
                            return;
                        }

                        [target, exactName].filter(Boolean).forEach(node => {
                            try {
                                node.dispatchEvent(new MouseEvent('mousedown', { view: window, bubbles: true, cancelable: true }));
                                node.dispatchEvent(new MouseEvent('click', { view: window, bubbles: true, cancelable: true }));
                                node.dispatchEvent(new MouseEvent('mouseup', { view: window, bubbles: true, cancelable: true }));
                            } catch (e) {}
                        });
                        window.chrome.webview.postMessage('[LOG] Action: Select Model -> ' + label);
                        setTimeout(callback, 180);
                    }, 220);
                };

                const uploadAttachments = (items, done) => {
                    const finish = (delay) => setTimeout(() => { if (done) done(); }, delay || 0);
                    const clickLikeUser = (node) => {
                        if (!node) return;
                        node.dispatchEvent(new MouseEvent('mousedown', { view: window, bubbles: true, cancelable: true }));
                        node.dispatchEvent(new MouseEvent('click', { view: window, bubbles: true, cancelable: true }));
                        node.dispatchEvent(new MouseEvent('mouseup', { view: window, bubbles: true, cancelable: true }));
                    };
                    const decodeFileBytes = (base64) => {
                        const binary = atob(base64 || '');
                        return Uint8Array.from(binary, ch => ch.charCodeAt(0));
                    };

                    try {
                        if (!items || !items.length) {
                            finish(0);
                            return;
                        }

                        const trigger = document.querySelector('.toolkit-trigger-btn');
                        if (!trigger) {
                            window.chrome.webview.postMessage('[LOG] Attachment toolkit trigger not found');
                            finish(0);
                            return;
                        }

                        window.chrome.webview.postMessage('[LOG] Action: Open Attachment Toolkit');
                        clickLikeUser(trigger);

                        setTimeout(() => {
                            const fileInput = document.querySelector('.toolkit-container label.toolkit-item input.hidden-input[type=""file""]')
                                || document.querySelector('.toolkit-container input.hidden-input[type=""file""]')
                                || document.querySelector('input.hidden-input[type=""file""]');
                            if (!fileInput) {
                                window.chrome.webview.postMessage('[LOG] Attachment input not found');
                                finish(0);
                                return;
                            }

                            const transfer = new DataTransfer();
                            items.forEach((item, index) => {
                                try {
                                    const bytes = decodeFileBytes(item.dataBase64 || '');
                                    const blob = new Blob([bytes], { type: item.mimeType || 'application/octet-stream' });
                                    const file = new File([blob], item.name || ('attachment_' + (index + 1)), { type: item.mimeType || 'application/octet-stream' });
                                    transfer.items.add(file);
                                } catch (e) {
                                    window.chrome.webview.postMessage('[LOG] Attachment build failed: ' + e);
                                }
                            });

                            if (transfer.files.length === 0) {
                                window.chrome.webview.postMessage('[LOG] Attachment list empty after build');
                                finish(0);
                                return;
                            }

                            fileInput.files = transfer.files;
                            fileInput.dispatchEvent(new Event('input', { bubbles: true }));
                            fileInput.dispatchEvent(new Event('change', { bubbles: true }));
                            window.chrome.webview.postMessage('[LOG] Action: Attach Files -> ' + transfer.files.length);
                            finish(2500);
                        }, 500);
                    } catch (e) {
                        window.chrome.webview.postMessage('[LOG] Attachment upload error: ' + e);
                        finish(0);
                    }
                };

                const setSearchMode = (enabled, done) => {
                    const finish = (delay) => setTimeout(() => { if (done) done(); }, delay || 0);
                    const clickLikeUser = (node) => {
                        if (!node) return;
                        node.dispatchEvent(new MouseEvent('mousedown', { view: window, bubbles: true, cancelable: true }));
                        node.dispatchEvent(new MouseEvent('click', { view: window, bubbles: true, cancelable: true }));
                        node.dispatchEvent(new MouseEvent('mouseup', { view: window, bubbles: true, cancelable: true }));
                    };
                    const hoverLikeUser = (node) => {
                        if (!node) return;
                        node.dispatchEvent(new MouseEvent('mouseenter', { view: window, bubbles: true, cancelable: true }));
                        node.dispatchEvent(new MouseEvent('mouseover', { view: window, bubbles: true, cancelable: true }));
                        node.dispatchEvent(new MouseEvent('mousemove', { view: window, bubbles: true, cancelable: true }));
                    };

                    try {
                        const trigger = document.querySelector('.toolkit-trigger-btn');
                        if (!trigger) {
                            window.chrome.webview.postMessage('[LOG] Search toolkit trigger not found');
                            finish(0);
                            return;
                        }

                        window.chrome.webview.postMessage('[LOG] Action: Open Search Toolkit');
                        clickLikeUser(trigger);

                        setTimeout(() => {
                            const searchEntry = Array.from(document.querySelectorAll('.toolkit-container .toolkit-item')).find(el => {
                                const txt = (el.innerText || '').trim().replace(/\s+/g, ' ');
                                return txt.includes('联网搜索');
                            });
                            if (!searchEntry) {
                                window.chrome.webview.postMessage('[LOG] Search menu entry not found');
                                finish(0);
                                return;
                            }

                            window.chrome.webview.postMessage('[LOG] Action: Open Search Submenu');
                            hoverLikeUser(searchEntry);
                            clickLikeUser(searchEntry);

                            setTimeout(() => {
                                const targetLabel = enabled ? '自动' : '关闭';
                                const option = Array.from(document.querySelectorAll('.connect-popover .connect-item, .n-popover__content.connect-popover .connect-item')).find(el => {
                                    const txt = (el.innerText || '').trim().replace(/\s+/g, ' ');
                                    return txt.startsWith(targetLabel);
                                });

                                if (!option) {
                                    window.chrome.webview.postMessage('[LOG] Search mode option not found: ' + targetLabel);
                                    finish(0);
                                    return;
                                }

                                clickLikeUser(option);
                                window.chrome.webview.postMessage('[LOG] Action: Search Mode -> ' + targetLabel);
                                finish(420);
                            }, 650);
                        }, 500);
                    } catch (e) {
                        window.chrome.webview.postMessage('[LOG] Search mode error: ' + e);
                        finish(0);
                    }
                };

                const applyTogglesAndSend = () => {
                    try {
                        const buttons = document.querySelectorAll('div[role=""button""]');
                        buttons.forEach(btn => {
                            const btnText = btn.innerText || """";
                            if (btnText.includes(""深度思考"")) {
                                const isSelected = btn.classList.contains(""ds-toggle-button--selected"");
                                if (useThink && !isSelected) { 
                                    window.chrome.webview.postMessage('[LOG] Action: Toggle Deep Thinking -> ON');
                                    btn.click(); 
                                }
                                else if (!useThink && isSelected) { 
                                    window.chrome.webview.postMessage('[LOG] Action: Toggle Deep Thinking -> OFF');
                                    btn.click(); 
                                }
                            }
                        });
                    } catch(e) { window.chrome.webview.postMessage('[LOG] DOM Error: ' + e); }

                    uploadAttachments(attachments, () => {
                        setSearchMode(useSearch, () => {
                            let input = document.querySelector('.chat-input-editor[contenteditable=""true""]')
                                || document.querySelector('[contenteditable=""true""][data-lexical-editor=""true""]')
                                || document.getElementById('chat-input')
                                || document.querySelector('textarea');
                            if (!input) { window.chrome.webview.postMessage('[LOG] Error: Input not found'); return; }

                            const isContentEditable = input.getAttribute('contenteditable') === 'true';
                            if (isContentEditable) {
                                kimiSetContentEditableText(input, text);
                            } else {
                                const nativeSetter = Object.getOwnPropertyDescriptor(window.HTMLTextAreaElement.prototype, 'value').set;
                                nativeSetter.call(input, text);
                                input.dispatchEvent(new Event('input', { bubbles: true }));
                                input.focus();
                            }

                            setTimeout(() => {
                                window.chrome.webview.postMessage('[LOG] Attempt: Send');
                                let sendBtn = document.querySelector('.send-button-container:not(.disabled)')
                                    || document.querySelector('.send-button-container')
                                    || document.querySelector('div[role=\'button\'].ds-send-button')
                                    || document.querySelector('div[role=\'button\'][aria-disabled=\'false\']');
                                if (!sendBtn) { const btns = document.querySelectorAll('div[role=\'button\']'); if(btns.length > 0) sendBtn = btns[btns.length - 1]; }
                                if (sendBtn) {
                                    sendBtn.dispatchEvent(new MouseEvent('mousedown', { view: window, bubbles: true, cancelable: true }));
                                    sendBtn.dispatchEvent(new MouseEvent('click', { view: window, bubbles: true, cancelable: true }));
                                    sendBtn.dispatchEvent(new MouseEvent('mouseup', { view: window, bubbles: true, cancelable: true }));
                                    window.chrome.webview.postMessage('[LOG] Status: Sent');
                                } else {
                                    window.chrome.webview.postMessage('[LOG] Error: Send button not found');
                                }
                            }, 420);
                        });
                    });
                };

                setTimeout(() => {
                    clickModelOption(modelLabel, applyTogglesAndSend);
                }, 300); 
            }
        };
        ";
    }
}
