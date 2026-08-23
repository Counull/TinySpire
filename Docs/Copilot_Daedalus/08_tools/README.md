---
title: 08_tools · 工具链
page_type: entry
lifecycle: active
updated: 2026-08-24
---

# 08_tools · 工具链

- 角色：工具与流程笔记——Luban gen 脚本、构建、CI、编辑器工具、代码生成配置。
- 记录"怎么用、坑在哪"，便于复现。
- 机器绝对路径不进本目录（进 `~/.llm-wiki/instances.json`）。
- `Test-LLMKnowledgeWorkflow.ps1`：离线检查 Daedalus 的 Status 路由、默认读取集、状态页预算与相对链接；执行 `pwsh -File Docs/Copilot_Daedalus/08_tools/Test-LLMKnowledgeWorkflow.ps1`。
- [BYTEROVER.md](BYTEROVER.md)：项目级可选检索适配；固定 `query → 精确来源路径 → 核对原文`，只按需 curate 耐久事实。
