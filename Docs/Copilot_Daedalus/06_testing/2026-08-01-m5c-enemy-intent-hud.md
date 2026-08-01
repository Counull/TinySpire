---
title: M5C 敌人意图 HUD
page_type: testing
lifecycle: active
date: 2026-08-01
plan: ../plans/2026-08-01-m5-enemy-intents-deterministic-behavior.md
---

# M5C 敌人意图 HUD

## 验收范围

- 固定敌人与加权随机敌人的图标、数值均来自 Session 持有的同一当前 `BehaviorId`。
- 玩家和死亡敌人隐藏意图；力量、生命、意图快照或 View 重建只重派生展示，不推进随机。
- 卡牌文本与敌人预测共用 `BattleEffectValueCalculator`，但本切片不执行 Effect。
- Prefab 静态持有五类正式 Sprite，不引用 `_ref_` 图；1～3 敌人布局不与既有名称、生命和力量 HUD 重叠。
- Addressables 与 Bootstrap 生产链不出现缺失引用、`InvalidKey` 或 VContainer 错误。

## 自动验证

Unity MCP 运行以下七组 EditMode：

- `BattleEffectValueCalculatorTests`
- `EnemyIntentHudPresentationTests`
- `ParticipantHudPrefabContractTests`
- `ParticipantHudPresentationTests`
- `BattleEnemyIntentsDataTests`
- `BattleEnemyIntentQueueTests`
- `BattleSessionTests`

结果为 **39/39 passed，0 failed，0 skipped**，耗时 `0.2307917s`。覆盖攻击数值随当前力量派生、防御读取静态 Effect 值、当前/下一 `BehaviorId` 投影、重复投影不推进随机、玩家/死亡可见性，以及 Prefab 的层级、正式 Sprite、导入模式和名称避让合约。

## Addressables

- 执行菜单：`TinySpire/Addressables/Build Local Content`。
- 结果：成功，Unity 日志报告内容构建耗时 `14.748s`。
- 报告：`Library/com.unity.addressables/BuildReports/buildlayout_2026.08.01.09.25.30.json`。
- `BuildError`：空。
- `BuildResultHash`：`d030cfdcfd7d76e4ca432b66eae62cea`。
- 报告耗时：`15.5289761s`。

## Bootstrap 与 Game View

从 `BootstrapScene` 进入生产 `BattleScene`：

- 初始为 `PlayerAction / Round 1`，敌人随机状态 `2144564843`。
- Enemy 2001：`Behavior 7001 / Attack`，HUD 为正式攻击图标、数值 `6`。
- Enemy 2002：`Behavior 7003 / Defend`，HUD 为正式防御图标、数值 `5`。
- Player 1001 的 `IntentRoot.activeSelf=false`。
- 第一轮完成后进入 `PlayerAction / Round 2`；Enemy 2001 仍为 `7001 / Attack / 6`，Enemy 2002 变为 `7002 / Attack / 6`，HUD 与运行时事实一致。
- 初始、重复 HUD 读取和第二轮检查时随机状态均为 `2144564843`；该轮受冷却/连续上限约束为单候选，没有因展示而消费随机。

Game View 使用既有正式敌人 View/HUD 构造只存在于 Play Mode 的 1/2/3 敌人布局夹具，按 M3A 的 `0`、`+1/-1`、`+2/0/-2` 间距目视确认：意图图标和值清晰可读，且不与名称、生命或力量 HUD 重叠；没有保存场景、Prefab 或临时对象。

MCP 截图实现自身在每次合成截图时向 Console 写入一条 `PlayerLoop internal function has been called recursively`。该错误堆栈全部位于 `MCPForUnity.Runtime.Helpers.ScreenshotUtility`，不来自 TinySpire。完成视觉记录后退出 Play Mode、清空 Console，并从 Bootstrap 干净复跑完整首轮；最终 Console Error/Warning 为 `0/0`，未出现 `InvalidKey` 或 VContainer 错误。

## 资源与边界

- 已使用并验证五份计划内正式意图图标；均为单个 Sprite 子资源且关闭 mipmap，没有缺失美术资源。
- 未引用任何 `_ref_` 参考图，未修改图标像素。
- 只修改 `ParticipantHudView.prefab`，没有修改 `BattleScene.unity`。
- 未实现真实 Effect、伤害、格挡、状态、死亡动画、胜败、行为树或条件 DSL。
