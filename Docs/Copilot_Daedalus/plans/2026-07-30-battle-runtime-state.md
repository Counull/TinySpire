---
title: BattleState 运行时参与者模型
page_type: plan
lifecycle: active
date: 2026-07-30
scope: TinySpire/Assets/Scripts/Battle/
source: 用户确认的运行时模型与测试边界
status_source: ../SESSION_LOG.md
---

# BattleState 运行时参与者模型

## 目标

在不接入配置、卡牌效果或场景目标的前提下，为 BattleScene 建立玩家与敌人的最小纯 C# 运行时模型，并让稳定 `CombatantId` 成为未来目标解析的唯一引用。

## 已实施方案

- `CombatantState` 保存共同事实：`Id`、`TemplateId`、最大生命、当前生命；`IsAlive` 由当前生命派生。
- `PlayerCombatantState` 与 `EnemyCombatantState` 只表达参与者角色，继承共同状态；牌组/手牌/能量、敌人意图/AI 暂不进入本切片。
- `BattleState` 是参与者集合、ID 分配、目标解析与伤害写入的唯一入口。
- `Combatants` 以 `IReadOnlyDictionary<CombatantId, CombatantState>` 暴露唯一的 ID 到参与者映射；玩家、敌人、存活目标的筛选留到真实业务规则出现时再从字典值派生，不维护镜像列表、存活计数或第二份集合。

## 明确排除

- 不修改 `HandState`，不创建卡牌实例或 Effect 执行链。
- 不实现敌人意图、抽牌/弃牌、费用、状态效果、场景锚点或 UI 绑定。
- 不注册到 `BattleLifetimeScope`；战斗局内服务的注册时机仍由 DEP-005 约束。

## 验收

- 玩家和敌人注册后取得不同 `CombatantId`，并可由唯一参与者字典按 ID 取回。
- `TryGetCombatant` 按 ID 返回同一运行时参与者。
- 致死伤害只修改目标参与者自身生命，不改动参与者集合。

验证结果见 `../06_testing/2026-07-30-battle-runtime-state.md`。
