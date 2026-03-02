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

# 直接执行，不使用管道，让输出直接流向控制台
& $exePath -c120 "$commandUrl"
Write-Host "`n-> Build command sequence completed." -ForegroundColor Green
