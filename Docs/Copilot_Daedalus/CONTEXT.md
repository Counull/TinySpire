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
- **遭遇敌人顺序（`EnemyCombatantIdsInEncounterOrder`）**：由 `Encounter.enemy_template_ids` 实例化时保留的敌方 `CombatantId` 顺序；它是布局和未来敌方行动需要的顺序事实，不能由参与者字典枚举推导，也不复制参与者数值。
- **卡牌实例数据（`CardInstanceData`）**：本局的一张唯一卡牌实例，仅以 `TemplateId` 指向静态卡牌模板。
- **卡区布局数据（`CardZoneLayoutData`）**：抽牌、手牌、弃牌、消耗四区的完整有序归属；一次移动发布一个新的完整布局，不存在半完成布局。
- **战斗卡区数据（`BattleCardZonesData`）**：本局卡牌实例及其可观察卡区布局的所有者。
- **参与者视觉（Combatant View）**：只保存一个 `CombatantId` 的场景角色或 HUD 表现对象；静态名称与 Prefab 由模板派生，生命和力量从 `CombatantData` 读取，不成为领域事实或可变镜像。
- **参与者视图编排者（`BattleParticipantPresenter`）**：属于 BattleScene 生命周期的场景组件；负责按模板 Addressables 地址创建/释放角色和 HUD，不拥有第二份参与者集合或战斗数值。

`State` 只保留给将来的状态机、状态节点或明确的状态模式对象，不能作为上述运行时数据的通用尾缀。
