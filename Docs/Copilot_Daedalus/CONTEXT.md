---
title: TinySpire 战斗领域术语
page_type: glossary
lifecycle: active
updated: 2026-08-05
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
- **配置素材短键（Asset Key）**：配置表中的 Unity 素材身份，只保存大小写精确匹配文件名的无目录、无扩展名短键，例如 `pfb_char_player`；它不是 Unity 工程路径，也不是可直接加载的地址。
- **Addressables 逻辑地址（Logical Address）**：由素材域转换函数从短键生成的运行时地址，例如 `character-view/pfb_char_player`、`card-art/card_art_strike`。运行时只消费逻辑地址并经 Addressables 加载。
- **基础设施稳定地址（Infrastructure Address）**：Addressables 用来定位场景或生成配置文件的完整 `Assets/...` catalog 地址，例如 `Assets/Scenes/BattleScene.unity`、`Assets/GameData/battle_tbhero.json`；它不等同于配置表中的业务素材引用。
- **参与者视图编排者（`BattleParticipantPresenter`）**：属于 BattleScene 生命周期的场景组件；负责把模板 `view_prefab_key` 转换为 `character-view/{key}`，经 Addressables 创建/释放角色和 HUD，不拥有第二份参与者集合或战斗数值。
- **战斗命令队列（`BattleCommandQueue`）**：M4 的外部 seam。玩家、系统阶段和未来敌人/Effect 向同一个 interface 提交命令；提交可以并发发生，权威调度层为已确认命令建立唯一顺序，再一次执行和展示一条。
- **战斗调度根（`BattleTurnController`）**：命令队列内部使用的阶段模块；只在队首命令执行期间推进阶段、校验玩家行动并写入每玩家能量与结束状态。UI 不直接调用其写入口。
- **命令提交（Submission）**：玩家或系统表达行动意图并获得权威排序的过程。提交接受不表示执行成功，也不提前扣能量、移动卡牌或修改阶段。
- **命令执行（Execution）**：已确认命令到达队首后，依据当时权威事实重新校验、修改状态并按序展示效果的过程。当前命令完成前下一条不得执行，但新命令仍可提交。
- **一轮（Round）**：一次全体玩家行动阶段加一次全体敌人行动阶段。玩家共享 `PlayerAction` 窗口并可同时提交命令；不存在固定轮转、当前玩家或“一张牌后切人”。
- **玩家行动数据（`PlayerTurnData`）**：某个 `CombatantId` 在当前一轮内的能量与是否结束行动；不保存生命、手牌、控制器身份或静态英雄字段。
- **结束玩家行动（End Player Action）**：单名玩家声明本轮不再行动。只有全部玩家都结束后才进入敌人阶段；当前单玩家 UI 可以继续显示“结束回合”，但领域命令不采用单人回合语义。

`State` 只保留给将来的状态机、状态节点或明确的状态模式对象，不能作为上述运行时数据的通用尾缀。
