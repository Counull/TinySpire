---
title: Copilot_Daedalus · 目录索引
project: TinySpire
page_type: index
lifecycle: active
updated: 2026-08-14
---

# Copilot_Daedalus · 目录索引

Page Type: `index`

> 本页是 Daedalus 工作区的路由入口。进入本目录先读本页，再按 Default Read Set 取所需页面，不必扫描全部文件。

## 概况

Owner: Daedalus / 代达罗斯（实现 Agent）

Status Source: [SESSION_LOG.md](SESSION_LOG.md)

身份说明: [AGENT_PROFILE.md](AGENT_PROFILE.md)

调用方式: [AGENT_PROMPT.md](AGENT_PROMPT.md)

## Default Read Set

进入本目录后，通常只需读以下页面（≤ 3-4 篇）。

| 顺序 | 页面 | 为什么 |
|---:|---|---|
| 1 | [SESSION_LOG.md](SESSION_LOG.md) | 当前进度、状态与下一步动作（唯一状态源）。 |
| 2 | [CODE_DECISIONS.md](CODE_DECISIONS.md) | 已生效的代码级决策，避免重复讨论。 |
| 3 | [ARCHITECTURE_CONVENTIONS.md](ARCHITECTURE_CONVENTIONS.md) | 代码架构级的 Locked/Provisional 约定，任何新实现（包括交给 Codex 等外部 Agent）默认必须遵守。 |
| 4 | [AGENT_PROFILE.md](AGENT_PROFILE.md) | Daedalus 身份、职责边界与工作方式。 |

战斗术语变更或涉及运行时数据命名时，额外阅读 [CONTEXT.md](CONTEXT.md)。

## Optional Deep-Dive Docs

| 页面 | 何时读 |
|---|---|
| [plans/](plans/) | 需要某个切片的完整实现计划时。 |
| [RUN_ROADMAP.md](RUN_ROADMAP.md) | 需要看当前 Run MVP 的阶段骨架、依赖与逐切片 Grill 门禁时；它不构成实施授权。 |
| [06_testing/](06_testing/) | 需要查切片自动验证、Bootstrap、真实 Game View 或复审证据时。 |
| [DEPENDENCIES.md](DEPENDENCIES.md) | 需要查某个 `DEP-NNN` 依赖项的阻塞条件、状态或解决记录时。 |
| [AGENT_PROMPT.md](AGENT_PROMPT.md) | 需要按标准格式向 Daedalus 派任务时。 |

## Archive Docs

| 页面 | 归档职责 |
|---|---|
| [ROADMAP.md](ROADMAP.md) | 已完成的 BattleScene MVP（M0～M10）固定路线与验收历史；Run 阶段不再在此扩写。 |

## Communication Docs

| 目录 | 何时读 |
|---|---|
| [10_communication/](10_communication/) | 需要向其他 Agent 外发审计任务、确认问题或素材说明时；不属于默认工程上下文，外部输出也不自动成为项目事实。 |

## 外部依赖（只读）

不属于默认上下文，按任务相关性取。

| 来源 | 用途 |
|---|---|
| `Docs/COLLABORATION_SOURCE_OF_TRUTH.md` | 根契约。 |
| `Docs/AI_COLLABORATION_RULES.md` | 全局协作规则（事实源、提交、决策、安全）。 |
| `Docs/Hermes_Pegasus/design/` | Pegasus 的设计、数值与锁定决策。 |
| `Docs/Gemini_Calliope/` | Calliope 的创意 / 文本 / 概念（若已建立）。 |

## 状态标签

- `analysis` · `design` · `development` · `integration` · `testing` · `published` · `archived`
