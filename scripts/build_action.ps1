param(
    [Parameter(Mandatory=$true)]
    [string]$ActionId,
    
    [Parameter(Mandatory=$true)]
    [string]$JsonPath
)

# 强制使用 UTF8
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

# 获取完整的绝对路径，防止相对路径导致 URL 拼接错误
$fullPath = [System.IO.Path]::GetFullPath($JsonPath)

# 进行 URL 编码
$encodedPath = [System.Net.WebUtility]::UrlEncode($fullPath)

# 构造完整的命令 URL
$commandUrl = "runaction:${ActionId}?action=build&filePath=$encodedPath"
$exePath = "C:\Program Files\Quicker\QuickerStarter.exe"

Write-Host "========== ACTION BUILD INITIATED ==========" -ForegroundColor Cyan
Write-Host "-> Target Path: $fullPath"
Write-Host "-> Triggering Quicker Compilation..."
Write-Host "Command: $exePath -c120 `"$commandUrl`"" -ForegroundColor DarkGray

# 执行并捕获输出
# 使用 | Out-String 强制同步执行并获取结果，而不是后台静默运行
$result = & $exePath -c120 "$commandUrl" | Out-String

if (![string]::IsNullOrWhiteSpace($result)) {
    Write-Host "========== BUILD RESULT ==========" -ForegroundColor Cyan
    Write-Host $result.Trim()
} else {
    Write-Host "-> Build command sent, but no output was returned." -ForegroundColor Yellow
}
