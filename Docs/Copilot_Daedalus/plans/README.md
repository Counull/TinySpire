---
title: plans · 实现计划（03_design 角色）
page_type: plan
lifecycle: active
updated: 2026-07-31
---

# plans · 实现计划

- 角色：llm-workflow `03_design` 的本地化落点——各切片的实现计划与技术方案。
- 每份计划含：目标、影响的层（计算/状态/时序/UI）、Open Question、验收点。
- 计划是 proposal，经 Theseus 确认后方可执行；当前状态见 `../SESSION_LOG.md`。

## 现有计划

- [2026-07-31 M4 回合调度与每玩家能量](2026-07-31-m4-turn-scheduling-energy.md)
- [2026-07-30 BattleScene M3A 参与者视图与生命 HUD](2026-07-30-battlescene-participant-views.md)
- [2026-07-30 卡牌区域与确定性洗牌](2026-07-30-card-zones-deterministic-random.md)
- [2026-07-30 YooAsset 到 Addressables 迁移](2026-07-30-addressables-migration.md)
- [2026-07-30 卡牌本地化文本与动态参数设计](2026-07-30-card-localized-text-design.md)
- [2026-07-30 战斗静态配置表](2026-07-30-battle-static-config-tables.md)
- [2026-07-30 BattleState 运行时参与者模型](2026-07-30-battle-runtime-state.md)
- [2026-07-12 BattleScene 基础手牌 UI](2026-07-12-battlescene-card-ui.md)

## 已替代 / 历史计划

- [2026-07-31 牌面短键与 Addressables 逻辑地址迁移](2026-07-31-card-illustration-logical-keys.md) — 已完成并通过逻辑地址加载验收。
- [2026-07-31 战斗 UI 首批美术与牌面配置链路接入](2026-07-31-battle-ui-art-integration.md) — 已完成并通过运行时验收。
- [2026-07-30 战斗配置接入运行时](2026-07-30-battle-config-runtime-integration.md) — 已由卡牌区域与确定性洗牌计划承接运行时牌区。
