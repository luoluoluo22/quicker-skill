param(
    [Parameter(Position=0)]
    [string]$Keyword = "",

    [string]$Style = "",

    [switch]$Exact,

    [int]$Top = 50
)

$csvPath = Join-Path $PSScriptRoot "..\references\quicker_fontawesome_icons.csv"
if (-not (Test-Path $csvPath)) {
    Write-Error "图标索引不存在: $csvPath"
    exit 1
}

$rows = Import-Csv $csvPath

if (-not [string]::IsNullOrWhiteSpace($Style)) {
    $rows = $rows | Where-Object { $_.style -ieq $Style }
}

if (-not [string]::IsNullOrWhiteSpace($Keyword)) {
    if ($Exact) {
        $rows = $rows | Where-Object {
            $_.enum_name -ieq $Keyword -or
            $_.icon_name -ieq $Keyword -or
            $_.token -ieq $Keyword
        }
    }
    else {
        $parts = $Keyword -split '[\s,_-]+' | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
        foreach ($part in $parts) {
            $p = [Regex]::Escape($part)
            $rows = $rows | Where-Object {
                $_.enum_name -match $p -or
                $_.icon_name -match $p -or
                $_.search_text -match $p
            }
        }
    }
}

$rows |
    Select-Object -First $Top enum_name, style, icon_name, token, action_icon |
    Format-Table -AutoSize
