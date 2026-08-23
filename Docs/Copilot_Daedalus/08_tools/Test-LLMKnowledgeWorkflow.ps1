[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$wikiRoot = Split-Path -Parent $PSScriptRoot
$failures = [System.Collections.Generic.List[string]]::new()

# 收集所有结构失败，避免首次失败掩盖后续问题。
function Add-WorkflowFailure {
    param([string]$Message)

    $failures.Add($Message)
}

# 断言文件包含精确的工作流标记。
function Require-Text {
    param(
        [string]$Path,
        [string]$Expected,
        [string]$Label
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        Add-WorkflowFailure "$Label 缺失：$Path"
        return
    }

    $content = Get-Content -Raw -LiteralPath $Path
    if (-not $content.Contains($Expected)) {
        Add-WorkflowFailure "$Label 缺少：$Expected"
    }
}

$statusPath = Join-Path $wikiRoot 'STATUS.md'
$readmePath = Join-Path $wikiRoot 'README.md'
$sessionPath = Join-Path $wikiRoot 'SESSION_LOG.md'
$roadmapPath = Join-Path $wikiRoot 'RUN_ROADMAP.md'
$g3PlanPath = Join-Path $wikiRoot 'plans/2026-08-24-g3-deterministic-act-map.md'
$g3TestingPath = Join-Path $wikiRoot '06_testing/2026-08-24-g3-deterministic-act-map.md'
$byteRoverPath = Join-Path $wikiRoot '08_tools/BYTEROVER.md'
$plansIndexPath = Join-Path $wikiRoot 'plans/README.md'
$testingIndexPath = Join-Path $wikiRoot '06_testing/README.md'

Require-Text -Path $statusPath -Expected 'page_type: status' -Label 'STATUS.md'
Require-Text -Path $statusPath -Expected 'lifecycle: active' -Label 'STATUS.md'
Require-Text -Path $readmePath -Expected 'Status Source: [STATUS.md](STATUS.md)' -Label 'README.md'
Require-Text -Path $roadmapPath -Expected 'status_source: STATUS.md' -Label 'RUN_ROADMAP.md'
Require-Text -Path $g3PlanPath -Expected 'status_source: ../STATUS.md' -Label 'G3 plan'
Require-Text -Path $g3PlanPath -Expected 'lifecycle: archived' -Label 'G3 plan'
Require-Text -Path $g3TestingPath -Expected 'status_source: ../STATUS.md' -Label 'G3 testing'
Require-Text -Path $sessionPath -Expected 'page_type: changelog' -Label 'SESSION_LOG.md'
Require-Text -Path $sessionPath -Expected 'status_source: STATUS.md' -Label 'SESSION_LOG.md'
Require-Text -Path $plansIndexPath -Expected 'status_source: ../STATUS.md' -Label 'plans index'
Require-Text -Path $testingIndexPath -Expected 'status_source: ../STATUS.md' -Label 'testing index'
Require-Text -Path $byteRoverPath -Expected 'non-authoritative locator/cache' -Label 'ByteRover adapter'
Require-Text -Path $byteRoverPath -Expected 'exact repository-relative source paths' -Label 'ByteRover adapter'

if (Test-Path -LiteralPath $statusPath) {
    $statusSize = (Get-Item -LiteralPath $statusPath).Length
    if ($statusSize -gt 5KB) {
        Add-WorkflowFailure "STATUS.md 超出 5 KiB 预算：$statusSize bytes"
    }

    $statusContent = Get-Content -Raw -LiteralPath $statusPath
    foreach ($match in [regex]::Matches($statusContent, '\]\((?<target>[^)]+)\)')) {
        $target = $match.Groups['target'].Value
        $fileTarget = ($target -split '#', 2)[0]
        if ([string]::IsNullOrWhiteSpace($fileTarget) -or $fileTarget -match '^[a-zA-Z][a-zA-Z0-9+.-]*:') {
            continue
        }

        $resolvedPath = Join-Path (Split-Path -Parent $statusPath) $fileTarget
        if (-not (Test-Path -LiteralPath $resolvedPath)) {
            Add-WorkflowFailure "STATUS.md 链接目标不存在：$target"
        }
    }
}

if (Test-Path -LiteralPath $readmePath) {
    $readmeContent = Get-Content -Raw -LiteralPath $readmePath
    $defaultSection = [regex]::Match($readmeContent, '(?ms)^## Default Read Set\s*$.*?(?=^## |\z)').Value
    if ([string]::IsNullOrWhiteSpace($defaultSection)) {
        Add-WorkflowFailure 'README.md 缺少 Default Read Set 区段'
    }
    elseif ($defaultSection -match 'SESSION_LOG\.md|CODE_DECISIONS\.md') {
        Add-WorkflowFailure 'Default Read Set 不得默认加载 SESSION_LOG.md 或 CODE_DECISIONS.md'
    }
}

if ($failures.Count -gt 0) {
    $failures | ForEach-Object { Write-Error $_ }
    exit 1
}

Write-Host 'LLM knowledge workflow V2 checks passed.'
