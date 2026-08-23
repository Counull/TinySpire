---
title: Copilot_Daedalus · Agent 局部规则
project: TinySpire
page_type: instruction
lifecycle: active
updated: 2026-08-24
---

# Copilot_Daedalus · Agent 局部规则

> 本文件只保存 Daedalus 的项目私有硬约束。知识导航唯一入口是 [README.md](README.md)；可复用规则只引用 [LLM Workflow](../_external/llm-workflow/LLM_WORKFLOW.md)，不在这里复制。

## 开始任务

1. 先遵守根 [AGENTS.md](../../AGENTS.md)、[协作事实源](../COLLABORATION_SOURCE_OF_TRUTH.md) 与 [AI 协作规则](../AI_COLLABORATION_RULES.md)。
2. 读取 [README.md](README.md) → [STATUS.md](STATUS.md)。
3. 实现、运行时或架构任务再预检 [ARCHITECTURE_CONVENTIONS.md](ARCHITECTURE_CONVENTIONS.md)。
4. 随后只读取一份直接相关的计划、决策或验收；仅在缺失证据、精确溯源或冲突裁决时继续下钻。

[AGENT_PROFILE.md](AGENT_PROFILE.md) 只说明角色，[AGENT_PROMPT.md](AGENT_PROMPT.md) 只提供调用模板，均不属于普通任务默认上下文。

## 写回落点

| 变化 | 唯一落点 |
|---|---|
| 当前执行状态、授权、阻塞、下一步 | [STATUS.md](STATUS.md) |
| 代码决定 | [CODE_DECISIONS.md](CODE_DECISIONS.md) 的精确 CD |
| 实施方案 | [plans/](plans/) |
| 真实测试与验收 | [06_testing/](06_testing/) |
| 历史过程 | [SESSION_LOG.md](SESSION_LOG.md) |

只写回耐久变化；不为普通对话、上下文压缩或工具过程建立第二份状态记录。

## 硬边界

- 目录职责不等于写入授权。讨论与评审保持只读；修改、commit、push 分别服从当前用户请求和根规则。
- Roadmap、计划状态或旧任务授权都不产生新的实施权限；当前权限只查 [STATUS.md](STATUS.md) 与当前用户请求。
- TinySpire 私有语义不得写入公共 `_external/llm-workflow/`；公共规则也不得复制回本实例。
- Pegasus / Calliope 文档默认只读；发现设计冲突时停止该语义分支并交给用户裁决。
- 文档使用项目相对路径；保护脏工作区，只精确暂存本轮审核文件。
