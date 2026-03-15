param(
    [string]$BaseUrl = "http://127.0.0.1:56000",
    [string]$Model = "kimi-k2.5-fast"
)

$ErrorActionPreference = "Stop"

$tests = @(
    @{
        name = "code"
        prompt = "请只输出一个 JavaScript 代码块，内容是 add(a,b) 函数并返回 a+b。不要解释。"
    },
    @{
        name = "html"
        prompt = "请只输出完整 HTML，包含 doctype、html、head、body，并在 body 里放一个标题 Hello Relay。不要解释。"
    },
    @{
        name = "svg"
        prompt = "请只输出一个完整 SVG，画一个蓝色圆和白色文字 OK。不要解释。"
    },
    @{
        name = "longtext"
        prompt = '请输出 1 到 20 的编号列表，每行格式为"序号. 测试内容"。不要解释。'
    }
)

function Invoke-Test {
    param([string]$Prompt)
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

    Invoke-RestMethod `
        -Uri $uri `
        -Method Post `
        -ContentType "application/json; charset=utf-8" `
        -Headers @{ Authorization = "Bearer sk-any" } `
        -Body $body `
        -TimeoutSec 120
}

$results = @()
foreach ($test in $tests) {
    Write-Host "Running $($test.name)..." -ForegroundColor Cyan
    $resp = Invoke-Test -Prompt $test.prompt
    $content = $resp.choices[0].message.content
    $snippet = if ($content.Length -gt 160) { $content.Substring(0,160) + "..." } else { $content }
    $results += [pscustomobject]@{
        name = $test.name
        length = $content.Length
        snippet = $snippet
        content = $content
    }
}

$results | ConvertTo-Json -Depth 6
