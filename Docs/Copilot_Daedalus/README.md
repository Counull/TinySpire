---
title: Copilot_Daedalus · 目录索引
project: TinySpire
page_type: index
lifecycle: active
updated: 2026-08-24
---

# Copilot_Daedalus · 目录索引

Owner: Daedalus / 代达罗斯（实现 Agent）

Status Source: [STATUS.md](STATUS.md)

## Default Read Set

1. [STATUS.md](STATUS.md) — 当前进度、授权、阻塞与下一步；唯一当前可变状态源。
2. [ARCHITECTURE_CONVENTIONS.md](ARCHITECTURE_CONVENTIONS.md) — 仅实现、运行时或架构任务作为规则预检读取；它不占下方的一份任务知识页额度。
3. [AGENT_PROFILE.md](AGENT_PROFILE.md) — 仅需要 Daedalus 身份、职责或交接格式时读取，不属于普通任务默认集。

完成全局/项目规则预检后，只再读取至多一份直接相关的计划、决策或验收页；只有缺失证据或发现冲突时才继续下钻。

## On-Demand Routes

| 需要 | 入口 |
|---|---|
| Run 阶段骨架与候选切片 | [RUN_ROADMAP.md](RUN_ROADMAP.md)；路线图不构成授权 |
| 实施计划 | [plans/](plans/) |
| 测试与验收 | [06_testing/](06_testing/) |
| 精确代码决定或冲突 | [CODE_DECISIONS.md](CODE_DECISIONS.md)；只定位相关 CD |
| 历史与时间线 | [SESSION_LOG.md](SESSION_LOG.md)；按需 changelog |
| 依赖项 | [DEPENDENCIES.md](DEPENDENCIES.md) |
| 工具与 ByteRover 检索 | [08_tools/](08_tools/)、[ByteRover adapter](08_tools/BYTEROVER.md) |
| 过期或被取代内容 | [99_archive/](99_archive/) |

## Boundary

- 可复用协议只引用 [llm-workflow](../_external/llm-workflow/LLM_WORKFLOW.md)；TinySpire 私有语义只留在本实例。
- 设计事实读取 [Hermes_Pegasus/design/](../Hermes_Pegasus/design/)；创意/文本读取 [Gemini_Calliope/](../Gemini_Calliope/)。
- ByteRover 是可选 locator/cache，不是事实源；查询结果必须返回精确项目相对路径并核对原文。
