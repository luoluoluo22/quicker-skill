param(
    [string]$BaseUrl = "http://127.0.0.1:56000",
    [string]$Model = "kimi-k2.5-fast",
    [int]$TimeoutSec = 180
)

$ErrorActionPreference = "Stop"

$workDir = Join-Path $PSScriptRoot "temp_attachments"
New-Item -ItemType Directory -Path $workDir -Force | Out-Null

$fileToken = "FILE_TOKEN_52481"
$imageToken = "IMG_TOKEN_85294"
$textPath = Join-Path $workDir "relay_test_note.txt"
$imagePath = Join-Path $workDir "relay_test_image.png"

Set-Content -Path $textPath -Value "文本附件口令：$fileToken" -Encoding UTF8

Add-Type -AssemblyName System.Drawing
$bmp = New-Object System.Drawing.Bitmap 900,260
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.Clear([System.Drawing.Color]::White)
$font = New-Object System.Drawing.Font("Microsoft YaHei", 36, [System.Drawing.FontStyle]::Bold)
$brush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::Black)
$g.DrawString("图片口令：$imageToken", $font, $brush, 30, 90)
$bmp.Save($imagePath, [System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose()
$font.Dispose()
$brush.Dispose()
$bmp.Dispose()

$attachments = @(
    @{
        name = [System.IO.Path]::GetFileName($textPath)
        mime_type = "text/plain"
        data_base64 = [Convert]::ToBase64String([System.IO.File]::ReadAllBytes($textPath))
    },
    @{
        name = [System.IO.Path]::GetFileName($imagePath)
        mime_type = "image/png"
        data_base64 = [Convert]::ToBase64String([System.IO.File]::ReadAllBytes($imagePath))
    }
)

$prompt = "请读取我上传的文本文件和图片中的口令，只按如下格式回复：FILE=<文本口令>;IMG=<图片口令>。不要添加其他内容。"
$uri = "$BaseUrl/v1/chat/completions"
$body = @{
    model = $Model
    stream = $false
    messages = @(
        @{
            role = "user"
            content = $prompt
        }
    )
    attachments = $attachments
} | ConvertTo-Json -Depth 12

Write-Host "POST $uri" -ForegroundColor Cyan
Write-Host "Model: $Model"
Write-Host "Text file token: $fileToken"
Write-Host "Image token: $imageToken"
Write-Host ""

try {
    $response = Invoke-RestMethod `
        -Uri $uri `
        -Method Post `
        -ContentType "application/json; charset=utf-8" `
        -Headers @{ Authorization = "Bearer sk-any" } `
        -Body $body `
        -TimeoutSec $TimeoutSec

    $content = $response.choices[0].message.content
    Write-Host "Response received:" -ForegroundColor Green
    $response | ConvertTo-Json -Depth 20
    Write-Host ""
    Write-Host "Attachment validation:" -ForegroundColor Yellow
    Write-Host ("file_token_present={0}" -f ($content -like "*$fileToken*"))
    Write-Host ("image_token_present={0}" -f ($content -like "*$imageToken*"))
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
