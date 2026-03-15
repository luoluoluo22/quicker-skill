param(
    [string]$JsonPath = "f:\Desktop\kaifa\deepseek-网页转api\KimiWebRelay.json",
    [string]$BaseUrl = "http://127.0.0.1:56000",
    [string]$Prompt1 = "请只回复：自动验证第一次",
    [string]$Prompt2 = "请只回复：自动验证第二次",
    [int]$StartupWaitSec = 45
)

$ErrorActionPreference = "Stop"
$serverLog = "f:\Desktop\kaifa\deepseek-网页转api\服务端.log"
$buildScript = "f:\Desktop\kaifa\deepseek-网页转api\.agent\skills\quicker-skill\scripts\build.ps1"

function Invoke-RelayTest {
    param([string]$Prompt)
    & "f:\Desktop\kaifa\deepseek-网页转api\test_kimi_relay.ps1" -BaseUrl $BaseUrl -Prompt $Prompt
}

function Wait-RelayUp {
    param([int]$TimeoutSec = 25)
    $deadline = (Get-Date).AddSeconds($TimeoutSec)
    while ((Get-Date) -lt $deadline) {
        try {
            $resp = Invoke-WebRequest -Uri "$BaseUrl/models" -UseBasicParsing -TimeoutSec 3
            if ($resp.StatusCode -eq 200) { return $true }
        } catch {}
        try {
            $listener = Get-NetTCPConnection -LocalPort 56000 -State Listen -ErrorAction Stop
            if ($listener) { return $true }
        } catch {}
        Start-Sleep -Seconds 1
    }
    return $false
}

Write-Host "Preflight: shutdown existing relay over HTTP..." -ForegroundColor Cyan
try {
    Invoke-WebRequest -Uri "$BaseUrl/shutdown" -UseBasicParsing -TimeoutSec 3 | Out-Null
} catch {}
Start-Sleep -Seconds 3

if (Test-Path $serverLog) {
    Clear-Content $serverLog
}

Write-Host "Building action (build auto-starts relay)..." -ForegroundColor Cyan
& $buildScript -JsonPath $JsonPath | Out-Host

if (-not (Wait-RelayUp -TimeoutSec $StartupWaitSec)) {
    throw "Relay did not start within $StartupWaitSec seconds."
}

Write-Host "First request: build template" -ForegroundColor Cyan
$first = Invoke-RelayTest -Prompt $Prompt1
Start-Sleep -Seconds 2

Write-Host "Second request: verify direct request path" -ForegroundColor Cyan
$second = Invoke-RelayTest -Prompt $Prompt2
Start-Sleep -Seconds 2

if (-not (Test-Path $serverLog)) {
    throw "Server log not found: $serverLog"
}

$logText = Get-Content $serverLog -Raw
$usedDirect = $logText -match 'Direct request \(C#\): https://www\.kimi\.com/apiv2/kimi\.gateway\.chat\.v1\.ChatService/Chat'
$fallback = $logText -match 'Direct request failed, fallback to UI'
$nativeReq = $logText -match '\[REQ-NATIVE\] 已捕获原始请求体'
$secondResponseOk = $second -match [regex]::Escape($Prompt2.Replace("请只回复：", ""))

Write-Host ""
Write-Host "Verification summary" -ForegroundColor Green
Write-Host "Native request captured: $nativeReq"
Write-Host "Direct request used: $usedDirect"
Write-Host "Fallback happened: $fallback"
Write-Host "Second response matches prompt: $secondResponseOk"
Write-Host ""
Write-Host "Last 80 log lines:" -ForegroundColor Yellow
Get-Content $serverLog -Tail 80

[pscustomobject]@{
    native_request_captured = $nativeReq
    direct_request_used = $usedDirect
    fallback_happened = $fallback
    second_response_matches_prompt = $secondResponseOk
    first_response = $first
    second_response = $second
    server_log = $serverLog
} | ConvertTo-Json -Depth 6
