---
title: BattleState 运行时参与者模型验证记录
page_type: testing
lifecycle: active
date: 2026-07-30
scope: TinySpire/Assets/Scripts/Battle/ 与 TinySpire/Assets/Editor/Tests/BattleStateTests.cs
source: 运行时模型实现、Unity EditMode 测试与 dotnet 编译
status_source: ../SESSION_LOG.md
---

# BattleState 运行时参与者模型验证记录

| 检查 | 方法 | 结果 |
|---|---|---|
| 唯一 ID 与事实字典 | `AddPlayerAndEnemy_AssignsDistinctCombatantIdsAndExposesTheSourceDictionary` | 玩家与敌人获得不同 ID，并可由唯一的 `Combatants` 字典按各自 ID 取回。 |
| ID 目标解析 | `TryGetCombatant_ReturnsTheCombatantWithTheRequestedId` | 请求的 ID 解析为同一 `PlayerCombatantState` 实例。 |
| 生命状态 | `ApplyDamage_ChangesOnlyTheTargetCombatantHealth` | 敌人受到致死伤害后生命归零；玩家生命与参与者集合均不变。 |
| Unity EditMode | UnityMCP `run_tests` | 字典作为唯一事实后的 3 项测试通过：3/3 通过，0 失败，0 跳过。 |
| 编译 | `dotnet build TinySpire/TinySpire.sln --no-restore --verbosity:minimal` | 0 错误；13 条既有程序集版本冲突警告。 |

## 环境记录

测试结束后 Unity Console 捕获到 YooAsset 编辑器包的 `AssetBundleCollectorWindow.RefreshWindow` 空引用（`Library/PackageCache/com.tuyoogame.yooasset.../AssetBundleCollectorWindow.cs:414`，由 `Undo` 回调触发）。该堆栈不涉及本次新增运行时模型或测试；3 项测试仍全部通过，本轮未修改该第三方包。

## 结论

`BattleState` 是参与者和目标解析的唯一事实源；当前只暴露只读的 `CombatantId → CombatantState` 字典，未预置阵营/存活等派生视图。测试不涉及 UI、场景对象、配置或卡牌效果。
