---
title: ByteRover · TinySpire 项目知识关联验收
page_type: testing
lifecycle: active
date: 2026-08-24
status_source: ../STATUS.md
source: ../08_tools/BYTEROVER.md
---

# ByteRover · TinySpire 项目知识关联验收

## 验收边界

本记录只验证 ByteRover 本地 MCP/CLI 链路、项目路由 seed 和来源回查。它不构成 Unity、Luban、Addressables、运行时或 BRV 云同步证据。

## 初始 RED

- MCP `query` 已成功到达当前项目 context tree，但返回 `No matching knowledge` 与 `Sources: None`。
- CLI 为 `byterover-cli/3.16.1`；项目可解析，本地 context tree 可用，但 Account 与 Space 均未连接。
- 未连接账号/Space 只阻塞 BRV 云 push/pull；本地 query/curate 与 TinySpire 两个普通 Git 仓库不依赖它。

## Seed 与回查

- 首次尝试以 5 份路由骨架执行 curate 时，安全审查因“将私有项目文档交给外部 ByteRover 处理”拒绝了请求；没有文件被提交，也没有使用 CLI、间接命令或其他路径绕过。
- 继续 curate 需要用户明确批准这 5 份私有文档的外部处理。等待批准期间，本地 adapter、`.gitignore`、离线结构检查与已成功的空树 query 保持可用。
- 未取得成功 curate 与带来源回查前，不声称 context tree 已完成关联。

## 事实源保护

- BRV 是 locator/cache，不是事实源；原始项目文档始终优先。
- `.brv/` 已由 TinySpire 根 `.gitignore` 排除，避免嵌套 context-tree Git 被主仓库误暂存。
- 公共 `Docs/_external/llm-workflow/` 只保存供应商无关的 Optional Retrieval Adapter，不含 TinySpire 或 ByteRover 私有语义。
