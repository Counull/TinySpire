---
title: Marine Game 机枪兵 MG13 全息诱饵运行时
page_type: testing
lifecycle: active
date: 2026-08-12
updated: 2026-08-12
status: verified-unity-native-2026-08-12
status_source: ../SESSION_LOG.md
source:
  - ../00_inbox/README web.md
  - ../01_requirements/2026-08-07-marine-game-card-requirement-digest.md
related_plan: ../plans/2026-08-07-marine-game-card-only-integration.md
related_decision: ../CODE_DECISIONS.md#cd-077buffer-通过-effect-链局部伤害序列冻结一次性受击防御
---

# Marine Game 机枪兵 MG13 全息诱饵运行时

## 1. 验收对象与冻结行为

本记录覆盖 Hero 1002 的 `HoloDecoy` (3259) 基础值程序。作者表冻结为：支付 1 Energy、Self 输入、施加 `Buffer +1`、从 Hand 移至 ExhaustPile。Buffer 是可叠层、无回合衰减的职业私有状态；它仅拦截正值的 incoming Attack，使该次伤害完全不改变 Block/HP，随后消费一层。零值攻击不消费 Buffer，且被 Buffer 完全抵挡时不消耗 Armor。

来源对升级归宿存在未裁决冲突：`README web.md` 写升级后“不消耗”，作者表的 `upgraded_play_destination` 仍为 ExhaustPile；项目没有通用 CardInstance 升级状态。因此本切片以作者表的基础值路径运行，保留升级字段，不伪造升级运行时。

本切片不实现无实体、攻击重定向、诱饵生命、HUD、场景表现、升级实例、奖励/Run、默认 Deck/Hero 或第二条写入链。

## 2. 已变更表面

| 层 | 变更 |
|---|---|
| 职业状态与程序 | `TinySpire/Assets/Scripts/Battle/MachineGunner/MachineGunnerCombatState.cs` 新增 `Buffer`；`TinySpire/Assets/Scripts/Battle/MachineGunner/MachineGunnerBattleRuntime.cs` 注册 Program 59 的 Self `Buffer +1` 程序。 |
| 伤害预演/提交 | `TinySpire/Assets/Scripts/Battle/Effects/BattleEffectExecution.cs` 与 `BattleEffectExecutor.cs` 按 Effect 链创建局部伤害序列；`MachineGunnerDamagePipeline.cs` 在局部投影预留 Buffer，提交时写出 Damage 后的 Buffer 状态 settlement。 |
| 连续顺序 | `BattlePreparedEffectPlan.PlannedSettlementCount` 将后续 settlement 纳入容量与 next Order 计算；`BattleEnemyActionExecutor.cs` 和 `BattleTurnController.cs` 使用它避免 intent/卡牌链的 Order 重叠。 |
| EditMode 回归 | `MachineGunnerStarterRuntimeTests.cs` 增加四项 3259 行为锁定，`BattleEffectExecutorTests.cs` 增加同一 Effect 链双段伤害用例。 |
| 作者表与门禁 | `DataTables/Datas/battle.card.xlsx` 仅 Q149 从 `CatalogOnly` 翻为 `Implemented`；生成 JSON、目录构建校验与机枪兵快照更新为 42 / 22。 |

## 3. 已加入的回归用例

| 用例 | 锁定事实 |
|---|---|
| `HoloDecoy_AppliesBufferAndExhausts` | 1 Energy 支付后 Buffer 0→1、Hand→Exhaust；完整零伤害回合后 Buffer 仍为 1。 |
| `HoloDecoy_BufferPreventsOnlyFirstIncomingAttackAndConsumesAfterDamage` | 两个敌人的 8 点攻击依次使玩家 70→70、70→62；Buffer 1→0 紧随首条伤害，且所有 continuation command 的 Order 连续。 |
| `HoloDecoy_StackedBuffersProtectOneIncomingAttackEach` | 两张基础卡叠出 Buffer 2；两次正值攻击各消费一层，玩家均不失血。 |
| `HoloDecoy_InsufficientEnergyLeavesBufferAndZonesUnchanged` | 支付失败不写 Energy、Buffer 或卡区。 |
| `PrepareAndCommit_MachineGunnerBuffer_ReservesOnlyFirstDamageInSameEffectChain` | 同一 Effect 链的两段 6 伤只让第一段消费 Buffer：提交 settlement 为 Damage(0) → Buffer(1) → Damage(2)，玩家 30→24。 |
| `GeneratedCatalog_HoloDecoyKeepsAuthoredMetadata` | 直接读取 Luban JSON，锁定 3259 的基础/升级费用、Self、两处 ExhaustPile、升级标记、Implemented 状态与 Program 59。 |

## 4. 本轮证据与验收结论

| 项目 | 结果 | 证据/说明 |
|---|---|---|
| 作者工作簿 | 通过 | 精确值差异、重新导入、渲染和公式错误扫描只确认 Q149；最终 SHA-256 为 `926CE36A1E6190B4B1BFD1EF93AC6C396AFC1B37E37F840622437C89864BB57A`。 |
| Luban | 通过 | 已执行生成命令，生成器移除的 `TinySpire/Assets/GameData/game-config.json` 已立即从 `DataTables/game-config.json` 恢复并以 SHA-256 核对一致。 |
| 生成 JSON | 通过 | 64 张机枪兵目录为 42 张 `Implemented` / 22 张 `CatalogOnly`；3259 为 Program 59、Cost 1、Self、基础/升级均 ExhaustPile。 |
| 静态编译 | 通过 | `dotnet build TinySpire/Assembly-CSharp-Editor.csproj --no-restore`：0 errors；保留 Unity 依赖图既有的 12 条 `MSB3277` 警告。 |
| `Sync and Build All` 与本地 Addressables | 通过 | 2026-08-12 在唯一既有 Unity 6000.5.5f1 Editor 执行菜单。首次因资源导入域重载断连；重试后 Console 记录 BuildLayout 写入、`Addressable content successfully built (duration : 0:00:21.093)` 与 `TinySpire sync and local content build completed successfully.`。BuildLayout 同时列出 `Assets/GameData/battle_tbcard.json`，并由 `AssetBundleProvider` 打包。 |
| Unity EditMode | 通过 | 最终 Unity MCP 任务 `dff6b79a8f4e486297d1cdd410c029a6`：119/119 passed，0 failed，0 skipped，0.5207738 秒；覆盖机枪兵运行时、伤害管线、Effect 执行、敌人行动、回合控制器、目录快照与构建门禁。首次同步后测试请求因延迟域重载未初始化，稳定后以 120 秒初始化窗口重试的本任务才是验收事实。 |

## 5. 验收完成后的后续顺序

剩余 22 张 CatalogOnly 卡继续采用“独立机制切片 → 精确表项翻转 → Luban → `Sync and Build All` → 定向 EditMode”的顺序。Buffer 的局部序列只解决已确认的“一次正值攻击完全抵挡”基础语义，不能据此提前实现 3262 的无实体、延迟/下回合时机、攻击重定向、选择、临时卡、升级实例、HUD/场景表现、奖励/Run 或第二写入链。
