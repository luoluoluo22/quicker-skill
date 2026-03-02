param(
    [Parameter(Mandatory=$true)]
    [string]$ActionId,
    
    [Parameter(Mandatory=$true)]
    [string]$JsonPath,

    [Parameter(Mandatory=$false)]
    [int]$WaitSeconds = 6,

    [Parameter(Mandatory=$false)]
    [string]$LogPath = ""
)

if ([string]::IsNullOrWhiteSpace($LogPath)) {
    $LogPath = [System.IO.Path]::ChangeExtension($JsonPath, ".log")
}

# 强制使用 UTF8
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

$encodedPath = [System.Net.WebUtility]::UrlEncode($JsonPath)
$commandUrl = "runaction:${ActionId}?action=build&filePath=$encodedPath"
$exePath = "C:\Program Files\Quicker\QuickerStarter.exe"

Write-Host "========== ACTION BUILD INITIATED ==========" -ForegroundColor Cyan
Write-Host "-> Triggering Quicker Compilation..."
Write-Host "Command: $exePath -c120 `"$commandUrl`"" -ForegroundColor DarkGray

$initialLogSize = 0
if (Test-Path $LogPath) {
    $initialLogSize = (Get-Item $LogPath).Length
}

# 使用 call 运算符直接启动，避免 Start-Process 对复杂字符串的处理偏差
& $exePath -c120 "$commandUrl" | Out-Null
Write-Host "-> Command sent to Quicker. Waiting $WaitSeconds seconds for compilation..." -ForegroundColor DarkGray

Start-Sleep -Seconds $WaitSeconds

Write-Host "========== EXECUTION LOG VERIFICATION ==========" -ForegroundColor Cyan
if (Test-Path $LogPath) {
    $fileInfo = Get-Item $LogPath
    if ($fileInfo.Length -gt $initialLogSize) {
        $stream = [System.IO.File]::OpenRead($LogPath)
        $stream.Seek($initialLogSize, [System.IO.SeekOrigin]::Begin) | Out-Null
        $reader = New-Object System.IO.StreamReader($stream, [System.Text.Encoding]::UTF8)
        $newContent = $reader.ReadToEnd()
        $reader.Close()

        if ($newContent -match "(?i)Exception|Error|FATAL|\b失败\b") {
           Write-Host "`n[❌ BUILD COMPILED BUT RUNTIME ERROR DETECTED]" -ForegroundColor Red
           Write-Host "------------------------------------------------"
           Write-Host $newContent
           Write-Host "------------------------------------------------"
           exit 1
        } else {
           Write-Host "`n[✅ BUILD & RUN EXECUTION SUCCESS]" -ForegroundColor Green
           Write-Host "------------------------------------------------"
           Write-Host $newContent
           Write-Host "------------------------------------------------"
           exit 0
        }
    } else {
        Write-Host "`n[⚠️ COMPILE FAILED OR EXECUTION SILENT]" -ForegroundColor Yellow
        Write-Host "Explanation: No new logs appended. This usually means a C# syntax compile error caught by the Quicker IDE, preventing execution." -ForegroundColor Yellow
        exit 1
    }
} else {
    Write-Host "`n[⚠️ LOG FILE NOT FOUND]" -ForegroundColor Yellow
    exit 1
}
