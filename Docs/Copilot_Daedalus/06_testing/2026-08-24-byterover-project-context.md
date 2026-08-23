---
title: ByteRover · TinySpire 项目知识关联验收
page_type: testing
lifecycle: active
date: 2026-08-24
status_source: ../STATUS.md
source: ../08_tools/BYTEROVER.md
implementation_status: verified
---

# ByteRover · TinySpire 项目知识关联验收

## 验收边界

本记录只验证 ByteRover 本地 MCP/CLI 链路、项目路由 seed 和来源回查。它不构成 Unity、Luban、Addressables、运行时或 BRV 云同步证据。

## 初始 RED

- MCP `query` 已成功到达当前项目 context tree，但返回 `No matching knowledge` 与 `Sources: None`。
- CLI 为 `byterover-cli/3.16.1`；项目可解析，本地 context tree 可用，但 Account 与 Space 均未连接。
- 未连接账号/Space 只阻塞 BRV 云 push/pull；本地 query/curate 与 TinySpire 两个普通 Git 仓库不依赖它。

## Seed 与回查（GREEN）

- 首次尝试因私有文档外部处理尚未获精确授权而被安全门拒绝；没有文件提交，也没有绕过。用户随后明确批准指定 5 份路由文档进入 ByteRover。
- MCP curate 任务 `f8289d91-6021-4b95-b17f-fc082c7d3666`（log `cur-1787518302539`）完成：从项目入口、Status、Run Roadmap、架构约定与验收索引生成 4 个 `tinyspire/` 知识主题，`added=4 / failed=0`，verification 为 `checked=4 / confirmed=4 / missing=0`。
- 回查问题要求给出唯一当前状态源、已验证 Run 阶段、下一已授权切片与精确仓库相对来源。BRV 正确返回：`STATUS.md` 是唯一当前可变状态源；G3 `completed / verified`；当前没有已授权切片，G4-A 只是未 Grill/计划/授权的 candidate。
- 回查给出的原始来源为 `Docs/Copilot_Daedalus/STATUS.md`、`Docs/Copilot_Daedalus/RUN_ROADMAP.md`、`Docs/Copilot_Daedalus/README.md`。逐项打开原文核对后，状态、993/993 证据与授权边界完全一致。
- 4 个生成知识文件先由 ByteRover 自有 VC 以本地提交 `70c49b4` 保存。项目验收索引改为 verified 后，又以任务 `3e9bb903-db8a-4a71-b952-8c4bbe661dfa`（log `cur-1787518731646`）刷新对应知识：`updated=1 / failed=0 / checked=1 / confirmed=1`。
- 刷新操作虽被 BRV 标为 low-confidence / needs-review，但人工审查 context-tree diff 后确认只把旧“等待授权”改为 verified local，并保留非权威与原始路径规则；第二次定向 query 正确返回本验收页路径、已验证/未验证边界。刷新由本地提交 `2eb1b37` 保存，最终 context-tree 工作树干净。
- BRV 未配置 remote，本轮没有进行云 push/pull，也不把本地提交冒充云同步证据。

## 事实源保护

- BRV 是 locator/cache，不是事实源；原始项目文档始终优先。
- `.brv/` 已由 TinySpire 根 `.gitignore` 排除，避免嵌套 context-tree Git 被主仓库误暂存。
- 公共 `Docs/_external/llm-workflow/` 只保存供应商无关的 Optional Retrieval Adapter，不含 TinySpire 或 ByteRover 私有语义。

结论：TinySpire 的小型路由骨架已进入本地 BRV context tree，实际 query 能返回精确原始路径与正确授权边界；无账号/Space 的云同步仍是未配置的可选能力，不影响本地检索闭环。
