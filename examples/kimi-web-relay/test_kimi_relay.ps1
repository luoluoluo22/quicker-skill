param(
    [string]$BaseUrl = "http://127.0.0.1:56000",
    [string]$Model = "kimi-k2.5-fast",
    [string]$Prompt = "请只回复：测试成功",
    [int]$TimeoutSec = 90
)

$ErrorActionPreference = "Stop"

$uri = "$BaseUrl/v1/chat/completions"
$body = @{
    model = $Model
    stream = $false
    messages = @(
        @{
            role = "user"
            content = $Prompt
        }
    )
} | ConvertTo-Json -Depth 10

Write-Host "POST $uri" -ForegroundColor Cyan
Write-Host "Model: $Model"
Write-Host "Prompt: $Prompt"
Write-Host ""

try {
    $response = Invoke-RestMethod `
        -Uri $uri `
        -Method Post `
        -ContentType "application/json; charset=utf-8" `
        -Headers @{ Authorization = "Bearer sk-any" } `
        -Body $body `
        -TimeoutSec $TimeoutSec

    Write-Host "Response received:" -ForegroundColor Green
    $response | ConvertTo-Json -Depth 20
}
catch {
    Write-Host "Request failed:" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red

    if ($_.ErrorDetails.Message) {
        Write-Host ""
        Write-Host "Server details:" -ForegroundColor Yellow
        Write-Host $_.ErrorDetails.Message
    }

    exit 1
}
