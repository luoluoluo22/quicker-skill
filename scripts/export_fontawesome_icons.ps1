param(
    [string]$QuickerDir = "C:\Program Files\Quicker"
)

$dllPath = Join-Path $QuickerDir "FontAwesomeIconsWpf.dll"
if (-not (Test-Path $dllPath)) {
    Write-Error "未找到图标程序集: $dllPath"
    exit 1
}

$outPath = Join-Path $PSScriptRoot "..\references\quicker_fontawesome_icons.csv"

$asm = [Reflection.Assembly]::LoadFrom($dllPath)
$iconEnum = $asm.GetType("FontAwesome5.EFontAwesomeIcon")
if ($null -eq $iconEnum) {
    Write-Error "未找到枚举类型 FontAwesome5.EFontAwesomeIcon"
    exit 1
}

[Enum]::GetNames($iconEnum) |
    Where-Object { $_ -ne "None" } |
    ForEach-Object {
        $parts = $_ -split "_", 2
        $style = if ($parts.Length -ge 1) { $parts[0] } else { "" }
        $name = if ($parts.Length -ge 2) { $parts[1] } else { $_ }
        [pscustomobject]@{
            enum_name   = $_
            style       = $style
            prefix      = "fa:$style"
            icon_name   = $name
            token       = "[fa:$_]"
            action_icon = "fa:$($_):#2D8CFF"
            search_text = (($_ + " " + $style + " " + $name) -replace "_"," ")
        }
    } |
    Export-Csv -Path $outPath -NoTypeInformation -Encoding UTF8

Write-Host "已导出: $outPath"
