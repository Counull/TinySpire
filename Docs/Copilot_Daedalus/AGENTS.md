---
title: Copilot_Daedalus · Agent 入口
project: TinySpire
page_type: entry
lifecycle: active
updated: 2026-07-08
---

# Copilot_Daedalus · Agent 入口（AGENTS.md）

> 本文件是进入 Daedalus 文档库的最短入口。它不是完整工作流手册；规则以 canonical workflow 为准。

## 1. Canonical Workflow（只引用，不复制）

- 可复用工作流协议：`../_external/llm-workflow/LLM_WORKFLOW.md`
- 公共仓库：<https://github.com/Counull/LLM-Workflow.git>
- 本文档库遵循其分层与读取策略，**不得把 TinySpire 私有语义写回 `_external/llm-workflow/`**。

## 2. 本地入口

- 目录索引（先读）：[README.md](README.md) — `index` 路由页
- 状态源（当前进度）：[SESSION_LOG.md](SESSION_LOG.md)
- 身份说明：[AGENT_PROFILE.md](AGENT_PROFILE.md)
- 调用模板：[AGENT_PROMPT.md](AGENT_PROMPT.md)
- 代码决策：[CODE_DECISIONS.md](CODE_DECISIONS.md)

## 3. 项目约束与来源

- 全局规则：`../COLLABORATION_SOURCE_OF_TRUTH.md`、`../AI_COLLABORATION_RULES.md`
- 设计 / 数值来源（只读）：`../Hermes_Pegasus/design/`
- 创意 / 文本来源（只读）：`../Gemini_Calliope/`（若已建立）
- 实现目标：`../../TinySpire/`（Unity 6.5 · C#）
- 路径一律相对；机器绝对路径只进 `~/.llm-wiki/instances.json`，不进本库。

## 4. 公私边界

- 通用工作流改动 → 提到 `LLM-Workflow` 公共仓库。
- TinySpire 事实、实现决策、原始来源 → 留在本私有实例。
- 二者不互相复制。

## 5. llm-workflow 实例布局（本地化）

> 本库是每个 AI 各自维护的一份 llm-workflow 实例。角色采用标准布局，但**已有文件就地充当对应角色**，只为缺失角色新建目录，避免重复。

| llm-workflow 角色 | 本库落点 | 说明 |
|---|---|---|
| `02_index` | `README.md`（根） | 路由入口 |
| `05_development-log` | `SESSION_LOG.md`（根） | **状态源** |
| `03_design` | `plans/` | 实现计划 |
| `decision` | `CODE_DECISIONS.md`（根） | **正式事实源**，路径不可变（见 `../AI_COLLABORATION_RULES.md` §4） |
| `00_inbox` | `00_inbox/` | 待消化任务 / 来源 |
| `01_requirements` | `01_requirements/` | 上游需求 digest（Pegasus / Calliope） |
| `04_research` | `04_research/` | 技术调研（Luban、R3 等） |
| `06_testing` | `06_testing/` | 测试记录 / 验收 |
| `07_retrospective` | `07_retrospective/` | 复盘 |
| `08_tools` | `08_tools/` | 工具链笔记 |
| `10_communication` | `10_communication/` | 对其他 Agent 的 hand-off |
| `99_archive` | `99_archive/` | 归档 |
| `09_meetings` | — | 不适用（实现 Agent 无会议），未建目录 |

> 每个数字目录内有 `README.md` 说明其角色。默认上下文仍是 `README.md` → `SESSION_LOG.md` → `CODE_DECISIONS.md`；数字目录按需读取。

## 6. 边界提醒

- 我的输出默认是 proposal，非事实源；经 Theseus 确认或写入正式事实源才成立。
- 提交前按 `../AI_COLLABORATION_RULES.md` §3 展示审查包，不静默 commit / push。
