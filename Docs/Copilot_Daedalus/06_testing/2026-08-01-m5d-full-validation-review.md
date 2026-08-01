---
title: M5D 全量验证与双轴复审
page_type: testing
lifecycle: active
date: 2026-08-01
plan: ../plans/2026-08-01-m5-enemy-intents-deterministic-behavior.md
---

# M5D 全量验证与双轴复审

## 最终数据与资源管线

- 在 `DataTables/` 串行执行 `gen.bat` 的等价 Luban 命令，schema、数据加载、校验、C# 与 `json2` 生成全部成功。
- Luban 会按输出目录所有权清理手写 `Assets/GameData/game-config.json`；生成后已立即从既有 `DataTables/game-config.json` 恢复，双方 SHA-256 均为 `048CDC9E8DB80F80BE9E43D409ED1A91A011E0118CBAB18EC207509B3C904CF8`。
- 新增 JSON 位于 `TinySpire/Assets/GameData/battle_tbenemybehavior.json` 与 `battle_tbenemybehaviorgroup.json`，Enemy/Encounter 生成 JSON 同步更新。
- 最终执行 `TinySpire/Addressables/Build Local Content` 成功；Unity 日志耗时 `7.74s`，报告 `Library/com.unity.addressables/BuildReports/buildlayout_2026.08.01.09.39.35.json`。
- 报告 `BuildError` 为空，`BuildResultHash=d030cfdcfd7d76e4ca432b66eae62cea`，报告耗时 `8.6252568s`。
- 报告确认以下完整稳定地址存在：两份新增行为 JSON、Enemy/Encounter JSON、`Assets/GameData/game-config.json`、`Assets/Scenes/BattleScene.unity` 与 `Assets/Arts/Runtime/Prefabs/ParticipantHudView.prefab`。

## EditMode 与静态构建

M5 定向 EditMode 包含选择核心、随机、Session、命令队列、回合、共享效果值、HUD 投影、Prefab 合约与 1～3 敌人布局：

- 结果：**73/73 passed，0 failed，0 skipped**。
- 耗时：`0.3158987s`。

全量 EditMode：

- 结果：**98/98 passed，0 failed，0 skipped**。
- 耗时：`6.9537175s`。

串行执行 `dotnet build TinySpire/TinySpire.sln --no-restore -m:1`：

- 结果：**0 error，12 warnings**。
- 耗时：`4.40s`。
- warning 均为既有 Unity/R3/UniTask 依赖程序集版本冲突，本次没有新增编译错误。

## Bootstrap 两次同种子复现

两次均从 `BootstrapScene` 进入生产 `BattleScene`，使用 Inspector 既有种子 `1`；探针只订阅只读事实并提交玩家结束行动命令，不写生产资产。

两次运行得到相同事实：

1. 初始 `PlayerAction / Round 1`；Encounter 运行时敌人顺序为 ID `2 → 3`。
2. 初始意图为 ID 2 `Behavior 7001 / Attack`，ID 3 `Behavior 7003 / Defend`；随机状态 `2144564843`。
3. 阶段进入 `EnemyAction actor=2`；其完成后发布 `7001/7003`，固定敌人的下一意图仍为 7001。
4. Encounter 顺序随后进入 `EnemyAction actor=3`；其完成后发布 `7001/7002`，加权敌人的下一意图为 `7002 / Attack`。
5. 阶段依次进入 `EnemyRoundEnd → RoundEnd → PlayerRoundStart → PlayerAction / Round 2`。
6. 第二轮 HUD：ID 2 与 ID 3 均显示正式攻击图标和值 `6`；玩家 `IntentRoot` 隐藏。HUD 与当前 `BehaviorId` 完全一致。
7. 两次最终随机状态均为 `2144564843`；本轮后续选择因冷却/连续上限成为单候选，没有额外随机消费。
8. 两次 Console Error/Warning 均为 `0/0`，没有 `InvalidKey` 或 VContainer 错误。

M5C 另已在实际 Game View 目视确认正式图标语义、数值可读性及 1～3 敌人布局；证据见 `2026-08-01-m5c-enemy-intent-hud.md`。

## Standards / Spec 双轴复审

固定基线为 `HEAD c51eeead39eec8ef21e6759da4a2f8c9ec06cee5`；复审同时读取 tracked diff 与全部未跟踪 M5 新文件。两个只读代理并行运行，保持 Standards 与 Spec 结论分离：

- Standards 首轮：P1 0 / P2 1 / P3 0。唯一 P2 为 `SESSION_LOG.md` 尚未回填已完成的 M5D 事实，违反其状态源职责；未发现明确代码气味。
- Spec 首轮：P1 0 / P2 1 / P3 0。唯一 P2 为计划仍显示“复审进行中”且缺少 M5D 状态源记录；随机隔离与失败回滚、Encounter 顺序、一次换意图、HUD 单一事实、正式资源、排除项与最终证据均无其他 finding。
- 修复：新增 `SESSION_LOG.md` M5D 最终记录，将 M5 计划状态改为已完成并归档，并把计划索引移入历史区；没有因复审扩大代码范围。
- 修复后由原 Standards / Spec 代理复核，确认该共同 P2 已关闭且没有新增 finding。

## 范围边界

- 没有修改 `BattleScene.unity`、ProjectSettings、asmdef、HybridCLR、Run 生命周期或启动流程。
- 没有实现真实 Effect、伤害、格挡、状态、死亡动画、胜败、行为树或通用条件 DSL。
- `DEP-009` 保持 open，剩余工作明确为 M7/M8 的真实敌人 Effect 执行。
- 五类计划内正式意图图标均存在并已验证；没有缺失美术资源。
