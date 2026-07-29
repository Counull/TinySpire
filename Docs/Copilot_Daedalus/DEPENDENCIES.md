---
title: Daedalus · 依赖项账本（Dependency Ledger）
page_type: registry
lifecycle: active
created: 2026-07-29
updated: 2026-07-30
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
| DEP-001 | 目标检测方式（UGUI `GraphicRaycaster` vs 2D `Collider`/`OverlapPoint`） | 取决于怪物/玩家锚点最终是 UGUI 元素还是 World Space Sprite（P0 待办，尚未开始） | `TinySpire/Assets/Scripts/UI/Battle/Hand/HandCardContainer.cs` | `plans/2026-07-29-battlescene-drag-to-play-minimal.md` | open | — |
| DEP-002 | 费用/能量系统与检查逻辑 | 需先定义能量池数据结构；最终应由出牌命令在提交区域移动前统一校验 | `TinySpire/Assets/Scripts/UI/Battle/Hand/HandCardContainer.cs` | `plans/2026-07-29-battlescene-drag-to-play-minimal.md` | open | — |
| DEP-003 | 拖过出牌线的最终视觉样式 | 需要策划/美术确认最终表现 | `TinySpire/Assets/Scripts/UI/Battle/Hand/HandCardVisual.cs` | `plans/2026-07-29-battlescene-drag-to-play-minimal.md` | open | — |
| DEP-004 | 打出后卡牌的销毁前过渡动作（按卡牌效果类型区分） | 需要 Effect 系统 / 卡牌数据结构先落地 | `TinySpire/Assets/Scripts/UI/Battle/Hand/HandCardContainer.cs` | `plans/2026-07-29-battlescene-drag-to-play-minimal.md` | open | — |
| DEP-005 | `BattleLifetimeScope` 已注册战斗会话，但回合调度器与其余战斗局内模块仍待确定后注册 | 需要先完成路线图 M3～M4 的视图与回合流程边界 | `TinySpire/Assets/Scripts/Battle/BattleLifetimeScope.cs` | `plans/2026-07-30-battle-config-runtime-integration.md` | open | — |
| DEP-006 | 初始手牌临时取初始卡组的前 N 张，尚未经过抽牌堆洗牌与抽取 | 需要先实现 `CardZoneState`、战斗专属确定性随机源与抽牌/重洗流程 | `TinySpire/Assets/Scripts/Battle/BattleSession.cs` | `plans/2026-07-30-battle-config-runtime-integration.md` | resolved | `plans/2026-07-30-card-zones-deterministic-random.md`：创建完整卡组、确定性洗牌后抽取初始手牌，并实现弃牌重洗。 |
| DEP-007 | BattleScene 的战斗种子当前由 Inspector 常量提供，尚未来自 Run 的根种子/存档 | 需要先实现 `RunState`、战斗创建标识与随机流派生/恢复规则 | `TinySpire/Assets/Scripts/Battle/BattleLifetimeScope.cs` | `plans/2026-07-30-card-zones-deterministic-random.md` | open | — |
