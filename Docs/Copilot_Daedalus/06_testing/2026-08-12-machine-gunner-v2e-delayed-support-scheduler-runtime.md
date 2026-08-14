---
title: Marine Game 机枪兵 V2E 延迟效果与支援链
page_type: testing
lifecycle: active
date: 2026-08-12
updated: 2026-08-12
status: verified-unity-native-2026-08-12
status_source: ../SESSION_LOG.md
source:
  - ../00_inbox/README web.md
  - ../01_requirements/2026-08-12-machine-gunner-v2-requirement-digest.md
related_plan: ../plans/2026-08-12-machine-gunner-v2-card-runtime-plan.md
related_decision: ../CODE_DECISIONS.md#cd-084v2e-以职业私有实例调度器和阶段联合计划承载延迟支援
---

# Marine Game 机枪兵 V2E 延迟效果与支援链

## 1. 验收对象与冻结行为

本切片只开放 7 张卡的基础态，并以职业私有的独立实例调度器承载其跨回合行为：

| 卡牌 | 验收行为 |
|---|---|
| `GuidedNuke` (3237) | 支付 5 Energy、Hand→DiscardPile，立即 Shackle +1；Shackle 只拒绝 Attack、允许 Skill，并在当前行动结束清除。施放回合末推进倒计时，在第三个未来回合末对全体造成 Bomb 99。 |
| `BansheeStrike` (3238) | 支付 2 Energy；后续两个玩家回合开始各锁定当时最近敌人并执行 Support 8×2，同次触发首段击杀不重定向第二段。 |
| `FireSupport` (3239) | 支付 1 Energy；下回合开始进行 5 次独立随机 Support 2，每次读取当前投影中的存活候选。 |
| `FireBombardment` (3240) | 支付 2 Energy；下回合开始进行两波全体结算，每个目标按 `Support 2 → 存活后 Burn +4 → Oil +3` 处理，Burn 沿用已有 Oil 交互。 |
| `FiveHundredPounder` (3241) | 支付 3 Energy；施放回合末推进倒计时，在第二个未来回合末对全体造成 Bomb 60。 |
| `TripleStrike` (3264) | 支付 4 Energy + 3 Ammo；先 Invisible +2，再对显式目标执行 Sniper 12×2并保留 Invisible，Hand→ExhaustPile；下回合开始对当前最远敌人执行 Support 20。 |
| `NeedleStorm` (3274) | 支付 1 Energy；下回合开始进行 4 次独立随机 Delayed 1，每段后仅在目标存活时加 ArmorBreak +1。 |

Support 固定读取目标 Smoke、Vulnerable、ArmorBreak，不读取来源修正；Bomb 只读取目标 Smoke；钢针 Delayed 只读取目标 Smoke。多实例按创建顺序处理，随机状态只随完整阶段计划成功提交。回合开始触发位于敌方 Smoke 清理、玩家资源补充和抽牌之前；回合结束炸弹位于状态清理后、Burn 前。战斗终局跳过后续工作并清空遗留实例。

这些来源未完全给出的修正、顺序与目标细节，是本次按用户授权“脑补”后冻结的项目实现口径。当前没有升级 CardInstance，因此升级数值仍只是作者表元数据。

## 2. V2E 调度器定向用例

| 用例 | 锁定事实 |
|---|---|
| `FiveHundredPounder_CountsCastRoundAndExplodesBeforeBurnOnSecondFutureRoundEnd` | 施放回合计入倒计时、第二个未来回合末全体 Bomb 60、炸弹先于 Burn、Triggered/Countdown/Removed 与连续 Order。 |
| `FireSupport_TriggersFiveRandomSupportHitsBeforeRoundStartSmokeClearAndRemoves` | 下回合开始 5 次随机 Support、触发时 Smoke 尚未清除、一次触发后移除。 |
| `BansheeStrike_TriggersForTwoRoundStartsAndLocksNearestPerActivation` | 两个回合开始触发；每次先锁最近目标，首段致死不在同次触发中递补。 |
| `FireBombardment_ResolvesDamageBurnOilPerTargetAcrossTwoWaves` | 两波按 Encounter 目标执行 Damage→Burn→Oil，并锁定旧 Oil 对 Burn 与 Oil 减半/新增的逐段投影。 |
| `TripleStrike_GainsInvisibleBeforeTwoSniperHitsThenSupportsFurthestNextRound` | Invisible 位于两段立即 Sniper 前，卡进入 ExhaustPile且不破隐；下回合命中当前最远敌人。 |
| `NeedleStorm_DealsFourDelayedHitsThenAppliesArmorBreakPerLivingHit` | 4 次 Delayed 1；每次命中后紧邻写 ArmorBreak +1，状态不反哺当前针伤。 |
| `GuidedNuke_ShacklesCurrentTurnAndExplodesOnThirdFutureRoundEnd` | Shackle 只阻止 Attack、允许 Skill、行动结束清除；第三个未来回合末全体 Bomb 99。 |
| `TripleStrike_InsufficientAmmoFailsWithoutImmediateOrScheduledWrites` | Ammo 不足时 Energy、Ammo、Invisible、生命、卡区、调度器与 settlements 全部零写入。 |

V2E 调度器 8 项与既有职业运行时、V1/V2 目录快照及构建门禁组成定向集合。Unity MCP 任务 `586264ec18e549d89d1a063aac4d7b93` 实际执行为 **101/101 passed，0 failed**。随后完整 EditMode 任务 `89cfdfe8441b45d39d0cd57d939734c7` 为 **606/606 passed，0 failed**，耗时 **46.847 秒**。

## 3. 数据、同步与构建证据

| 项目 | 结果 | 说明 |
|---|---|---|
| 作者表与 Luban | 通过 | 只将 3237、3238、3239、3240、3241、3264、3274 翻为 `Implemented`；生成目录为 82 项、56 `Implemented` / 26 `CatalogOnly`，V1 为 53/11，V2 为 3/15。 |
| 本地化 | 通过 | 从 Excel 导入战斗卡本地化成功。 |
| Sync 与本地 Addressables | 通过 | 唯一既有 Unity Editor 的 `TinySpire/Build/Sync and Build All` 成功；Addressables 构建耗时 14.252 秒。 |
| Unity 定向 EditMode | 通过 | `586264ec18e549d89d1a063aac4d7b93`：101/101 passed。 |
| Unity 完整 EditMode | 通过 | `89cfdfe8441b45d39d0cd57d939734c7`：606/606 passed，46.847 秒。 |

## 4. 已知边界与未实施范围

- round-start 延迟阶段内部使用同一份投影计划准备、校验和提交；但它是在最后一名敌人的行动事务已经成功提交之后才执行。目前没有覆盖“敌人行动 + round-start 延迟阶段”的跨事务回滚。上述测试证明正常 Queue 串行路径，不证明异常提交故障下的完整跨域原子性。
- 来源规定恢复先于炸弹、炸弹先于 Burn；当前恢复卡尚未实现，因此测试只锁定已经存在的“状态清理 → 炸弹 → Burn”，没有伪造恢复结算。
- 本切片未实现升级 CardInstance，未将卡加入默认 Deck、奖励或 Run，未修改 UI、多人、Scene、Prefab，也未开放剩余 26 张 `CatalogOnly`。帮手、狂轰滥炸、天空之怒、临时机枪扫射、选择和自动免费出牌仍需独立切片。
- 未创建提交，也未记录或杜撰本切片 SHA。
