param(
    [Parameter(Mandatory=$true)]
    [string]$JsonPath,

    [string]$WrenchId = ""
)

# 统一控制台编码为 UTF-8，避免中文输出乱码
$Utf8NoBom = New-Object System.Text.UTF8Encoding($false)
[Console]::InputEncoding = $Utf8NoBom
[Console]::OutputEncoding = $Utf8NoBom
$OutputEncoding = $Utf8NoBom

if ([string]::IsNullOrWhiteSpace($WrenchId)) {
    # 尝试读取同级或上级目录的 config.json
    $ConfigPath = Join-Path -Path $PSScriptRoot -ChildPath "..\config.json"
    if (Test-Path $ConfigPath) {
        try {
            $WrenchId = (Get-Content $ConfigPath -ErrorAction Stop | ConvertFrom-Json).wrench_action_id
        } catch { }
    }
}

# 兜底默认扳手 ID
if ([string]::IsNullOrWhiteSpace($WrenchId)) {
    $WrenchId = "3eebe8d9-7521-46fa-b2e1-502754bce14f"
}

# 将目标 json 转为绝对路径，并进行 URL 编码
$ResolvedPath = Convert-Path $JsonPath
$EncodedPath = [System.Net.WebUtility]::UrlEncode($ResolvedPath)

# 构建动作参数（不回显完整命令，避免噪音）
$CmdArgs = "runaction:${WrenchId}?action=build&filePath=${EncodedPath}"
Write-Host "正在调用 QuickerWrench ..." -ForegroundColor Cyan

# 执行构建调用并捕获输出
$rawOutput = & "C:\Program Files\Quicker\QuickerStarter.exe" -c120 $CmdArgs 2>&1 | Out-String

# 统一输出风格：错误红色、成功绿色
if ($LASTEXITCODE -ne 0 -or $rawOutput -match "❌|错误|编译失败|error\s+[A-Z]+\d+") {
    Write-Host ""
    Write-Host "构建失败" -ForegroundColor Red
    Write-Host "--------------------------------" -ForegroundColor DarkRed

    $errorLines = $rawOutput -split "`r?`n" | Where-Object {
        $_ -match "❌|错误|编译失败|error\s+[A-Z]+\d+"
    }

    if ($errorLines.Count -gt 0) {
        $errorLines | ForEach-Object { Write-Host $_ -ForegroundColor Red }
    } else {
        Write-Host ($rawOutput.Trim()) -ForegroundColor Red
    }

    exit 1
} else {
    Write-Host "构建完成" -ForegroundColor Green
    $resultText = $rawOutput.Trim()
    if (-not [string]::IsNullOrWhiteSpace($resultText)) {
        Write-Host "动作执行结果:" -ForegroundColor Cyan
        Write-Host $resultText
    }
}
