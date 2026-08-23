---
title: LLM 项目知识工作流 V2 试点计划
page_type: plan
lifecycle: completed
date: 2026-08-24
scope: Docs/Copilot_Daedalus local instance only
status_source: ../STATUS.md
source: 用户完成局部 Grill 后的明确优化授权；Docs/_external/llm-workflow/LLM_WORKFLOW.md
---

# LLM 项目知识工作流 V2 试点计划

## 目标

把“开始一个 TinySpire 任务所需的正确上下文”收敛到小型、可验证的入口：当前事实只由 `STATUS.md` 承担，历史仍可追溯但不进入默认上下文。

## 范围与排除

- 本计划只验收本地实例 `Docs/Copilot_Daedalus/`；公共 `llm-workflow` 的并行优化由独立仓库差异与提交承载，不把 TinySpire 语义写入公共协议。
- 保留 `SESSION_LOG.md`、`CODE_DECISIONS.md`、既有 plans 与 `06_testing/` 的所有历史内容；本试点不迁移、不删除、不压缩历史。
- 不修改 Unity、配置表、资源、场景、Prefab、程序集或构建链路。

## 实施

1. 新建 `STATUS.md`，写入唯一当前状态、授权、阻塞、下一步、下钻路由及四类更新节点。
2. 将主索引、Run 路线图和活动 G3 的计划/验收记录改为指向该状态源；把 changelog 与决策集移出默认读取集。
3. 新增无网络、无 Unity 依赖的 PowerShell 校验器，检查状态页大小、主要入口、默认读取集、相对链接与可选检索的非权威边界。
4. 以 `git diff --check` 和校验脚本记录文档结构证据。

ByteRover 的项目 seed 与实际回查是独立的可选适配验收，见 `../06_testing/2026-08-24-byterover-project-context.md`；它不改变本 V2 状态路由试点的完成判定。

## 验收

- `STATUS.md` 为唯一当前状态源，且小于等于 5 KiB。
- 默认读取集不包含 `SESSION_LOG.md` 或 `CODE_DECISIONS.md` 全文。
- 当前 G3 的计划、验收和路线图均回链到 `STATUS.md`。
- 校验器与 `git diff --check` 均通过。

## 回滚

本试点只添加一个状态页、一个脚本和少量 Markdown 链接。删除新增文件并还原列出的 Markdown 路由即可回到 V1；不会影响历史内容或运行时。
