---
title: 战斗配置接入运行时
page_type: plan
lifecycle: superseded
date: 2026-07-30
scope: TinySpire/Assets/Scripts/Battle/ 与 TinySpire/Assets/Scripts/UI/Battle/Hand/
source: 用户要求将现有战斗表接入运行时，同时暂缓效果器
status_source: ../SESSION_LOG.md
---

# 战斗配置接入运行时

> 本文是已替代的历史切片：当时的 `HandState` 与“取前 N 张”临时限制，已由 `2026-07-30-card-zones-deterministic-random.md` 的 `CardZoneState` 解决。当前状态不得从本文推断。

## 目标

把已经生成并加载的英雄、敌人、遭遇、卡组和卡牌模板接入当时的 `BattleState`、`HandState` 与手牌 UI；配置继续是静态模板，运行时可变事实继续由纯 C# 状态持有。

## 当时已实施

- `BattleSession` 从指定英雄和遭遇模板创建 `BattleState` 与当时的 `HandState`；后者现已由 `CardZoneState` 替代。
- `BattleLifetimeScope` 以场景生命周期注册 `BattleSession`，并给 `HandCardContainer` 注入战斗会话与配置服务。
- 英雄和敌人的模板 ID、最大生命、当前生命初值和基础力量进入 `CombatantState`。
- `CardInstanceId` 与卡牌模板 ID 分离；同模板的多张卡拥有不同实例身份。
- 手牌 UI 从 `battle.Card` 读取卡名与费用；卡牌类型和描述暂留空。后续 i18n key、说明模板和动态参数设计见 `2026-07-30-card-localized-text-design.md`。

## 唯一事实边界

- `BattleState.Combatants` 仍是全部参与者的唯一权威映射。
- 当时的 `HandState.Cards` 是当前手牌唯一权威有序集合；现行唯一事实边界见后续 `CardZoneState` 计划。
- `CardInstanceState.TemplateId` 只引用静态模板，不复制模板名称、费用或效果。
- UI 内的 `HandCardVisual` 是视图身份映射，不是手牌事实源。

## 本切片当时明确排除

- 不实现 Effect 执行器、伤害公式、格挡、易伤或力量修改。
- 不实现目标选择、费用扣除、敌人行为或回合调度。
- 当时不实现正式抽牌堆/弃牌堆/洗牌，并按卡组顺序取前 `initialHandCount` 张；该临时限制 `DEP-006` 后续已解决。
- 不修改任何表格、生成 JSON 或 YooAsset 包。

## 验收

- 配置英雄 1001 和遭遇 5001 能创建玩家与敌人。
- 初始手牌数量来自 `GameConfig.InitialHandCount`。
- 5 张相同 Strike 具有不同 `CardInstanceId`。
- BattleScene 中 5 张卡显示 `Strike` 与费用 `1`。
- 效果表可由 `ConfigService` 加载，但没有运行时效果执行入口。

验证结果见 `../06_testing/2026-07-30-battle-config-runtime-integration.md`。
