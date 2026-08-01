---
title: M5A 敌人行为配置与确定性选择核心
page_type: testing
lifecycle: active
date: 2026-08-01
plan: ../plans/2026-08-01-m5-enemy-intents-deterministic-behavior.md
---

# M5A 敌人行为配置与确定性选择核心

## 验收范围

- Luban 静态表增加 Enemy 行为组引用、有序行为组、行为模板与 `EnemyIntentType`。
- 默认 Encounter 同时包含固定行为敌人与加权随机敌人。
- `BattleEnemyIntentsData` 按 Encounter 顺序持有权威当前 `BehaviorId`、最小历史和敌人专属随机流。
- 固定选择、加权确定性、随机流隔离、冷却、最大连续次数、权重边界、配置引用和无候选原子失败。
- 保持 M5A 边界：不接命令队列、场景、Prefab 或 HUD，不执行真实 Effect。

## 工作簿与 Luban

- 使用 `@oai/artifact-tool` 修改并复核 `__tables__.xlsx`、`__enums__.xlsx`、`battle.enemy.xlsx`、`battle.encounter.xlsx`，新增 `battle.enemy_behavior_group.xlsx` 与 `battle.enemy_behavior.xlsx`。
- 六份最终工作簿均完成渲染目视检查；公式错误扫描未发现 `#REF!`、`#DIV/0!`、`#VALUE!`、`#NAME?` 或 `#N/A`。
- 固定样例：Enemy 2001 → Group 6001 → Behavior 7001（Attack / Enemy / Effect 4002 / weight 1）。
- 加权样例：Enemy 2002 → Group 6002 → Behavior 7002（Attack，weight 3）与 7003（Defend，weight 1，冷却 1，最大连续 1）。
- 已从 `DataTables/` 执行与 `gen.bat` 相同的 Luban 命令并成功完成；新增生成类型与 `battle_tbenemybehavior*.json` 位于约定目录。生成过程移除的手写 `game-config.json` 已立即从 `DataTables/game-config.json` 原样恢复，两份 SHA-256 一致。
- `ConfigService.TableNames` 已加入 `battle_tbenemybehaviorgroup` 和 `battle_tbenemybehavior`；EditMode 用例同时验证这两个预加载名称。

## 运行时契约

- 初始选择严格遍历 `EnemyCombatantIdsInEncounterOrder`；不依赖参与者或生成表字典枚举。
- 一个候选直接选中且不推进随机流；多个候选只对稳定有序候选执行一次 `NextInt(totalWeight)`。
- 冷却表示完成该行为后跳过的后续成功选择次数；`max_consecutive = 0` 表示不限。
- 当前意图仅保存 `CombatantId -> BehaviorId`。意图类型、目标、Effect 和数值不复制进运行时快照。
- 下一意图先在历史副本上选择。无合法候选或选择异常会恢复随机状态，且保持原快照与历史不变。

## 自动验证结果

| 检查 | 结果 |
|---|---|
| Luban 生成 | 通过；新增 C# 与 JSON 已生成 |
| Unity 脚本刷新/编译 | 通过；Console Error 0 |
| `BattleEnemyIntentsDataTests` | 15/15 通过 |
| `BattleSessionTests` 回归 | 3/3 通过 |
| 合计定向 EditMode | 18/18 通过，0 failed，0 skipped |

覆盖点包括：固定行为不消费随机；同种子加权序列一致；洗牌与敌人随机互不推进；重复读取不推进随机；冷却和连续上限序列；Int32 权重边界与溢出；非正权重、负限制、缺失 Group/Behavior/Effect；无候选重复失败时快照、历史和随机状态均不变。

## 未在本切片执行

- 未将意图聚合接入 `BattleSession`、`BattleCommandQueue`、`BattleTurnController` 或生产驱动；属于 M5B。
- 未修改场景、`ParticipantHudView.prefab` 或 HUD 代码；属于 M5C。
- Addressables 最终本地内容构建、全量 EditMode、串行 solution build、Bootstrap 两轮实跑与双轴复审统一在 M5D 收口。
- 未实现 Effect、伤害、格挡、状态、死亡表现、胜败、行为树或通用条件 DSL。
