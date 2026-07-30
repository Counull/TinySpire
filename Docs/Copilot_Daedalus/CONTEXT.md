---
title: TinySpire 战斗领域术语
page_type: glossary
lifecycle: active
updated: 2026-07-30
status_source: SESSION_LOG.md
---

# TinySpire 战斗领域术语

- **战斗局（Battle Session）**：从静态配置实例化出的单场战斗上下文；它协调运行时数据，但不是状态机。
- **运行时战斗数据（`*Data`）**：只在一场战斗中成立的事实对象，统一使用 `Data` 尾缀，以区别于静态配置、View 和未来的状态机。
- **参与者数据（`CombatantData`）**：玩家或敌人在本局中的身份与属性事实。生命、力量等持续变化的属性以只读 R3 属性对外公开。
- **战斗参与者数据（`BattleCombatantsData`）**：本局 `CombatantId → CombatantData` 的唯一映射与目标解析入口。
- **卡牌实例数据（`CardInstanceData`）**：本局的一张唯一卡牌实例，仅以 `TemplateId` 指向静态卡牌模板。
- **卡区布局数据（`CardZoneLayoutData`）**：抽牌、手牌、弃牌、消耗四区的完整有序归属；一次移动发布一个新的完整布局，不存在半完成布局。
- **战斗卡区数据（`BattleCardZonesData`）**：本局卡牌实例及其可观察卡区布局的所有者。

`State` 只保留给将来的状态机、状态节点或明确的状态模式对象，不能作为上述运行时数据的通用尾缀。
