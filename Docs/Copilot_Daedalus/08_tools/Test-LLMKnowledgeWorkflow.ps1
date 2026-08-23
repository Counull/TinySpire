[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$wikiRoot = Split-Path -Parent $PSScriptRoot
$docsRoot = Split-Path -Parent $wikiRoot
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

# 检查知识库内所有 Markdown 本地链接，外部 URL 与纯锚点不在此脚本联网验证。
function Test-RelativeMarkdownLinks {
    param([string]$Root)

    Get-ChildItem -LiteralPath $Root -File -Recurse -Filter '*.md' | ForEach-Object {
        $markdownPath = $_.FullName
        $content = Get-Content -Raw -LiteralPath $markdownPath
        foreach ($match in [regex]::Matches($content, '!?\[[^\]]*\]\((?<target>[^)]+)\)')) {
            $target = $match.Groups['target'].Value.Trim()
            if ($target.StartsWith('<') -and $target.EndsWith('>')) {
                $target = $target.Substring(1, $target.Length - 2)
            }

            $fileTarget = ($target -split '#', 2)[0]
            if ([string]::IsNullOrWhiteSpace($fileTarget) -or
                $fileTarget -match '^[a-zA-Z][a-zA-Z0-9+.-]*:' -or
                $fileTarget.StartsWith('//')) {
                continue
            }

            $decodedTarget = [uri]::UnescapeDataString($fileTarget)
            $resolvedPath = Join-Path (Split-Path -Parent $markdownPath) $decodedTarget
            if (-not (Test-Path -LiteralPath $resolvedPath)) {
                $relativeMarkdownPath = [IO.Path]::GetRelativePath($Root, $markdownPath)
                Add-WorkflowFailure "Markdown 链接目标不存在：$relativeMarkdownPath -> $target"
            }
        }
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
$codeDecisionsPath = Join-Path $wikiRoot 'CODE_DECISIONS.md'
$inboxIndexPath = Join-Path $wikiRoot '00_inbox/README.md'
$plansRoot = Join-Path $wikiRoot 'plans'
$legacySceneScopePath = Join-Path $wikiRoot '06_testing/2026-07-30-scene-child-scope.md'
$archivedSceneScopePath = Join-Path $wikiRoot '99_archive/2026-07-30-scene-child-scope.md'
$collaborationPath = Join-Path $docsRoot 'COLLABORATION_SOURCE_OF_TRUTH.md'
$collaborationRulesPath = Join-Path $docsRoot 'AI_COLLABORATION_RULES.md'

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
Require-Text -Path $codeDecisionsPath -Expected 'page_type: decision' -Label 'CODE_DECISIONS.md'
Require-Text -Path $codeDecisionsPath -Expected '按需读取的代码决策账本' -Label 'CODE_DECISIONS.md'
Require-Text -Path $inboxIndexPath -Expected 'source-only' -Label '00_inbox index'
Require-Text -Path $collaborationPath -Expected 'Ownership 只表示责任归属' -Label 'Collaboration source'
Require-Text -Path $collaborationRulesPath -Expected '目录 ownership 是职责与落点，不是常驻编辑权限' -Label 'AI collaboration rules'

if (Test-Path -LiteralPath $legacySceneScopePath) {
    Add-WorkflowFailure '已归档 scene-child-scope 不得继续保留在 06_testing/'
}

if (-not (Test-Path -LiteralPath $archivedSceneScopePath)) {
    Add-WorkflowFailure '99_archive 缺少 scene-child-scope 归档原件'
}
elseif ((Get-Content -LiteralPath $archivedSceneScopePath -TotalCount 1) -ne '---') {
    Add-WorkflowFailure 'scene-child-scope 归档页缺少合法 YAML front matter 起始符'
}

# 只检查现存数字目录；未启用的可选目录不要求为了布局完整而创建。
Get-ChildItem -LiteralPath $wikiRoot -Directory | Where-Object Name -Match '^\d{2}_' | ForEach-Object {
    $directoryIndex = Join-Path $_.FullName 'README.md'
    if (-not (Test-Path -LiteralPath $directoryIndex)) {
        Add-WorkflowFailure "数字目录缺少 README.md：$($_.Name)"
    }
}

# 已验证计划必须退出 active lifecycle，避免历史计划与当前执行入口竞争。
Get-ChildItem -LiteralPath $plansRoot -File -Filter '*.md' | ForEach-Object {
    $planContent = Get-Content -Raw -LiteralPath $_.FullName
    if ($planContent -match '(?m)^implementation_status:\s*verified\s*$' -and
        $planContent -match '(?m)^lifecycle:\s*active\s*$') {
        Add-WorkflowFailure "已验证计划仍为 active lifecycle：$($_.Name)"
    }
}

Test-RelativeMarkdownLinks -Root $wikiRoot

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
