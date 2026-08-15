---
title: DeepSeek Harness · BattleScene → Run 只读审计 Prompt
page_type: communication
lifecycle: active
created: 2026-08-14
updated: 2026-08-14
scope: milestone-battlescene-mvp-2026-08-14
status_source: ../SESSION_LOG.md
---

# DeepSeek Harness · BattleScene → Run 只读审计 Prompt

## 运行建议

DeepSeek Harness 当前仍是 developer preview。官方 CLI 支持 `headless` 一次性任务；必须显式指定 `read-only`，不能只依赖 Prompt。建议从仓库根目录运行：

```powershell
$env:DSH_PERMISSION_MODE = 'read-only'
$env:DSH_TOOLS_MODE = 'native'
$env:DSH_TELEMETRY_DISABLED = '1'

$auditPrompt = @'
<粘贴下方“Prompt 正文”代码块的完整内容>
'@

npx @deepseek-ai/dsh --profile headless $auditPrompt
```

Windows ACL 沙箱的官方状态是 partial，因此 `read-only` 是必要条件，但不是形式化的绝对隔离。最高安全级别应使用一次性只读副本或 OS / 容器只读挂载。若直接使用当前仓库，拒绝全部权限升级，并在运行前后比较 HEAD、工作区状态与 diff。

官方参考：[DeepSeek Harness](https://github.com/deepseek-ai/deepseek-harness)、[CLI](https://github.com/deepseek-ai/deepseek-harness/blob/master/apps/cli/README.md)、[Headless bundle](https://github.com/deepseek-ai/deepseek-harness/blob/master/packages/bundle/headless/README.md)、[Sandbox](https://github.com/deepseek-ai/deepseek-harness/blob/master/docs/subsystems/sandbox.md)。

## Prompt 正文

```text
你是 TinySpire BattleScene → Run 阶段交接的独立代码审计员。

任务目标：判断已提交的 BattleScene 基线中，是否存在必须在 G1 首片 Grill 前修复的真实缺陷、生命周期阻塞或事实源冲突。你只审计，不实施修改。

首先完整读取并严格执行：
Docs/Copilot_Daedalus/10_communication/2026-08-14-battlescene-to-run-audit-brief.md

再读取仓库中适用的 AGENTS.md。若 AGENTS.md 与任务书冲突，采用更严格、写权限更小的约束。

不可妥协的约束：
1. 仅允许只读 Git 命令、源码搜索、文件读取和静态推理。
2. 禁止创建、修改、删除、移动或格式化任何文件；禁止 apply patch、代码生成、安装依赖、暂存、提交、推送、切分支、stash、reset 或 clean。
3. 禁止运行 Unity、Luban、Addressables、build、测试或任何可能写缓存/快照/生成物的命令。
4. 禁止请求或接受权限升级；禁止把报告写回仓库，只输出到最终 stdout / 对话回复。
5. 产品代码固定审计 tag `milestone-battlescene-mvp-2026-08-14`，必须解引用为 `e07e39a29efe6395f79c2d9e63b1ae3b740263b5`。Run 交接文档基线为 `18d9023494a9da1975d158cf0b176f0fc45d28c9`。
6. 不使用当前未提交工作区内容作为证据。先确认上述两个提交之间 `TinySpire/**` 没有已提交变化；若审计范围内代码相对提交对象存在未提交变化，返回 `BASELINE_UNRELIABLE` 并停止。
7. 缺少尚未设计的 Run 功能本身不是 BattleScene 缺陷。把这类问题归为 `G1DesignInput` 或 `Questions for later Grill`。
8. 不把 UI、动画、美术品质债、CatalogOnly 内容、升级实例缺失或 G2+ 功能列为 blocker。
9. 每个 finding 必须有项目相对路径、精确行号、直接证据和可观察失败路径。证据不足时明确写“未确认”，不要猜。
10. 最多报告 5 条 actionable findings，不为凑数制造问题，不提供代码补丁。

如果 Harness 支持隔离的并行子 Agent，请用两个互不共享推理上下文的 reviewer：
- Standards：仓库规则、事实所有权、生命周期、统一写入 seam、确定性与模块边界。
- Spec / Transition：BattleScene 完成规格、终局/退出/销毁证据，以及 G1 入口契约。

最后只做轻量聚合，保持两轴分开，不跨轴重新排名。

严格采用任务书第 8 节的输出格式。报告首行必须三选一：
SAFE_TO_START_G1_GRILL
PRE_G1_CORRECTION_REQUIRED
BASELINE_UNRELIABLE

最后附上 Not findings、Questions for later Grill，以及两轴各自 finding 数量和最严重项。

完成报告后立即停止。不要提出“我可以顺便修复”，不要执行任何修复。
```
