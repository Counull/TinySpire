---
title: M5B Session、权威命令队列与生产接线
page_type: testing
lifecycle: active
date: 2026-08-01
plan: ../plans/2026-08-01-m5-enemy-intents-deterministic-behavior.md
---

# M5B Session、权威命令队列与生产接线

## 验收范围

- `BattleSession` 为全部 Encounter 敌人建立、公开并统一释放一个意图聚合。
- `BattleCommandQueue` 复用 M4 阶段、身份、当前行动敌人与 Encounter 顺序，不改变公共 seam。
- 合法完成严格执行“校验 → 下一意图 → Encounter 推进”；错误、重复、死亡跳过与无候选均不产生半完成写入。
- 生产逐帧驱动仍每帧最多提交一名当前敌人，不直接随机或执行 Effect。
- Bootstrap 至少完成两个轮次，确认当前不会造成敌人生命变化。

## 自动验证

Unity MCP 运行以下四组 EditMode：

- `BattleEnemyIntentsDataTests`
- `BattleSessionTests`
- `BattleEnemyIntentQueueTests`
- `BattleCommandQueueTests`

结果为 **47/47 passed，0 failed，0 skipped**，耗时 `0.86983s`。脚本刷新后 Console Error 为 0。

新增 M5B 集成覆盖：

- Session 中每个 Encounter 敌人均有初始 `BehaviorId`；相同种子产生相同加权意图序列，意图推进不改变洗牌布局。
- 合法完成第一名敌人时，意图快照先发布、Turn 后推进；只改变第一名敌人的行为，第二名保持不变。
- 错误阶段、非当前敌人和重复完成保持同一意图快照与随机状态，并沿用 M4 的 `InvalidTurnPhase` / `EnemyNotCurrentActor`。
- 进入敌人阶段前死亡的敌人由 M4 顺序跳过，其 `BehaviorId` 不变。
- 无合法候选时 `Submit(CompleteEnemyActionCommand)` 显式抛出配置契约异常；队列停在该命令，不进入表现，意图、随机和 Turn 均保持原对象/原值。
- 生产驱动模型连续两轮每个 Tick 最多交接一名敌人；每名敌人每轮恰好推进一次意图，生命值不变。

## Addressables

- 执行菜单：`TinySpire/Addressables/Build Local Content`。
- 结果：成功，Unity 日志报告内容构建耗时 `12.801s`。
- 报告：`Library/com.unity.addressables/BuildReports/buildlayout_2026.08.01.09.07.56.json`。
- `BuildError`：空。
- `BuildResultHash`：`d8794bc54bf6fa0df3cc1595bc89c6ef`。
- 报告耗时：`13.7309617s`。
- 已确认报告包含：
  - `Assets/GameData/battle_tbenemybehavior.json`
  - `Assets/GameData/battle_tbenemybehaviorgroup.json`
  - `Assets/GameData/battle_tbenemy.json`
  - `Assets/Scenes/BattleScene.unity`

## Bootstrap 实跑

从 `BootstrapScene` 进入生产 `BattleScene`，由场景 `BattleLifetimeScope.Container` 读取同一 Session 与 Queue：

- 初始：`PlayerAction / Round 1 / queueIdle=true`。
- Encounter 顺序：运行时敌人 ID 2（Enemy 2001）后 ID 3（Enemy 2002）。
- 初始意图：Enemy 2001 为 `Behavior 7001 / Attack`；Enemy 2002 为 `Behavior 7003 / Defend`。
- 提交第一轮结束行动后立即进入 `EnemyAction / Round 1 / actor 2`；相关 EditMode 生产驱动用例验证后续按 ID 3 交接且每帧最多一名。
- 生产驱动完成两轮后进入 `PlayerAction / Round 3`；Enemy 2001 仍为固定 `Behavior 7001 / Attack`，Enemy 2002 当前为 `Behavior 7002 / Attack`。
- 两名敌人生命始终为 20，证明 M5B 没有执行真实 Effect。
- 退出 Play Mode 后 Console Error/Warning 为 `0/0`，未出现 `InvalidKey` 或 VContainer 错误。

## 边界

- 未修改 `BattleScene.unity`、任何 Prefab、ProjectSettings、asmdef、HybridCLR、启动流程或 DI 架构。
- 未增加敌人完成失败枚举、行为执行器、伤害、格挡、状态、死亡事件驱动交接、胜败或通用 AI 抽象。
- `ParticipantHudView` 与意图图标仍属于 M5C。
