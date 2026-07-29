---
title: Daedalus · 依赖项账本（Dependency Ledger）
page_type: registry
lifecycle: active
created: 2026-07-29
updated: 2026-07-29
status_source: ../SESSION_LOG.md
note: 全项目范围唯一的依赖项 ID 分配与状态账本；实现计划文档只引用 ID，不重复维护完整描述。
---

# Daedalus · 依赖项账本（Dependency Ledger）

> 每条"本轮先占位、留给未来解决"的实现细节，都在这里登记一个全局唯一 ID（`DEP-NNN`）。代码中用 `// TODO(DEP-NNN): <一句话>` 标记对应位置；plan 文档只引用 ID + 一句话摘要，不重复维护阻塞条件全文——这份文件是唯一事实源。

## 使用规则

1. **ID 分配**：新依赖项永远追加到表格末尾，用下一个未使用的编号，不重用、不跳号。**不要在单个 plan 文档里独立编号**，避免多个 plan 各自从 `DEP-001` 开始导致撞号。
2. **状态**：`open`（尚未解决）/ `resolved`（已解决，保留记录不删除）。
3. **解决时**：把状态改成 `resolved`，补充"解决记录"列（哪个 plan/commit 解决的、怎么解决的），不要删除整行。
4. **代码标记**：代码里对应位置写 `// TODO(DEP-NNN): <一句话说明>`，一句话应能让人不查文档也大致知道要做什么；详细阻塞条件查本文件。

## 依赖项列表

| ID | 内容 | 阻塞条件 | 涉及代码位置（预期/实际） | 来源 Plan | 状态 | 解决记录 |
|---|---|---|---|---|---|---|
| DEP-001 | 目标检测方式（UGUI `GraphicRaycaster` vs 2D `Collider`/`OverlapPoint`） | 取决于怪物/玩家锚点最终是 UGUI 元素还是 World Space Sprite（P0 待办，尚未开始） | `TinySpire/Assets/Scripts/UI/Battle/Hand/HandState.cs:27` | `plans/2026-07-29-battlescene-drag-to-play-minimal.md` | open | — |
| DEP-002 | 费用/能量系统与检查逻辑 | 需先定义能量池数据结构；应并入 `HandState` 或其后续演化的聚合 | `TinySpire/Assets/Scripts/UI/Battle/Hand/HandState.cs:25` | `plans/2026-07-29-battlescene-drag-to-play-minimal.md` | open | — |
| DEP-003 | 拖过出牌线的最终视觉样式 | 需要策划/美术确认最终表现 | `TinySpire/Assets/Scripts/UI/Battle/Hand/HandCardVisual.cs:83` | `plans/2026-07-29-battlescene-drag-to-play-minimal.md` | open | — |
| DEP-004 | 打出后卡牌的销毁前过渡动作（按卡牌效果类型区分） | 需要 Effect 系统 / 卡牌数据结构先落地 | `TinySpire/Assets/Scripts/UI/Battle/Hand/HandCardContainer.cs:130` | `plans/2026-07-29-battlescene-drag-to-play-minimal.md` | open | — |
