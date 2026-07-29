---
title: 战斗静态配置表
page_type: plan
lifecycle: active
date: 2026-07-30
scope: DataTables/Datas/ 与 TinySpire/Assets/Scripts/Core/Generated/Config/battle/
source: 用户确认的最小战斗模板表需求
status_source: ../SESSION_LOG.md
---

# 战斗静态配置表

## 目标

为玩家、敌人和卡牌建立最小 Luban 静态模板来源；生成 C# 类型与 JSON，但不让配置承担战斗局内可变状态。

## 已实施表

| 概念表 | Luban 记录类型 | 关键字段 | 样例 |
|---|---|---|---|
| 英雄模板 | `battle.Hero` | `max_health`、`base_strength`、`initial_deck_id` | 1001 / Test Warrior / 30 HP / deck 1001 |
| 敌人模板 | `battle.Enemy` | `max_health`、`base_strength` | 2001 / Test Slime / 20 HP |
| 卡组模板 | `battle.Deck` | `card_template_ids` | 1001 → [3001] |
| 卡牌模板 | `battle.Card` | `cost`、`target_rule`、`effect_id` | 3001 / Strength / Self / 4001 |
| 卡牌效果 | `battle.CardEffect` | `effect_type`、`attribute`、`value` | 4001 / ModifyAttribute / Strength / +3 |
| 遭遇模板 | `battle.Encounter` | `enemy_template_ids` | 5001 → [2001] |

## 枚举

- `battle.TargetRule.Self`
- `battle.EffectType.ModifyAttribute`
- `battle.Attribute.Strength`

## 数据关系与边界

`Hero.initial_deck_id → Deck.card_template_ids → Card.effect_id → CardEffect`；`Encounter.enemy_template_ids → Enemy`。

这些关系当前以模板 ID 表达，尚未接入 Luban `ref` 校验或运行时导航。`CombatantId`、当前生命、存活与否、手牌/抽牌/弃牌堆、卡牌实例、临时费用、升级、敌人意图和控制者都明确不进入表格。

## Luban 约定

- 手工登记的战斗表使用 `battle.TbXxx` 表名和 `battle.Xxx` 记录类型；避免把 table 与 value type 设为同名。
- 战斗数据文件不以 `#` 开头，以免与 Luban 自动导入规则重复；`#demo.item.xlsx` 已按既有删除意图移除，不再参与生成。
- Luban JSON 输出到 `TinySpire/Assets/GameData`；生成后必须用 YooAsset 的 `Main` / `BuiltinBuildPipeline` 重建内置包，单独刷新 Unity 不会更新离线清单。
- ID 列表使用 `(array#sep=,),int`，在一个单元格内以逗号分隔多个 ID。

## 明确排除

- 不创建运行时玩家/敌人的模板实例工厂，不修改 `BattleState` 或 `HandState`。
- 不实现配置 ID 的运行时查找、效果执行、目标选择、费用扣除、敌人意图或 UI 绑定。

验证结果见 `../06_testing/2026-07-30-battle-static-config-tables.md`。
