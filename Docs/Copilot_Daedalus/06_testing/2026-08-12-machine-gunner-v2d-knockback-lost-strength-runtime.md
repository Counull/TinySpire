---
title: Marine Game 机枪兵 V2D 击退射击与失去力量
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
related_decision: ../CODE_DECISIONS.md#cd-083击退射击以双目标快照和独立-losestrength-行动结束计划接入
---

# Marine Game 机枪兵 V2D 击退射击与失去力量

## 1. 验收对象与冻结行为

本切片只开放 `KnockbackShot` (3223) 的基础态，并为其提供独立的 `LoseStrength` 状态与行动结束生命周期：

- 卡支付 1 Ammo；施放时按 Encounter 顺序一次快照前两名存活敌人，不接受显式 `TargetId`，也不声明 `Shoot` / `Sniper` 标签。
- 第一快照目标依次承受 7 点 Attack，并在仍存活时获得 `LoseStrength +2`；第二快照目标随后承受 3 点 Attack，并在仍存活时获得 `LoseStrength +2`。
- 只有一名存活敌人时第二段跳过；第一段击杀不会重新选择第三名敌人递补第二段。
- `LoseStrength` 独立于 Weakness、Vulnerable 与永久 Strength。Attack 使用 `max(0, baseDamage + Strength - LoseStrength)`，Burn 不受影响。
- 状态由携带者自己的行动结束清零并写 settlement；敌方顺序为 action Effect / completion → LoseStrength 清除 → intent advance，玩家清除位于回合末 Burn 之前。

作者表中的升级 9/5 Attack 与 `LoseStrength +3` 仍只是元数据；当前 `CardInstanceData` 没有升级态。本切片未把 3223 加入默认 Deck、奖励或 Run，也未修改多人、UI、Scene 或 Prefab。

## 2. 已变更表面

| 层 | 变更 |
|---|---|
| 作者表/生成配置 | 只将 3223 的 `implementation_status` 翻为 `Implemented`；Luban 后保持原有费用、目标规则、卡区归宿和升级元数据。 |
| 目标与职业程序 | 增加自动前两名存活敌人的冻结目标模式，并让两段 Attack 固定读取对应快照位置；缺失目标跳过且不递补。 |
| 状态与伤害 | 增加非负 `LoseStrength` 私有状态；只有 Attack 读取它，Burn 等其他伤害种类保持原公式。 |
| 生命周期 | 玩家和敌人都通过可预演、可校验、可提交的 actor action-end 计划清零状态；敌方清除 settlement 位于 completion 后、intent 推进前，玩家清除位于回合末 Burn 前，Order 连续。 |
| 目录门禁 | V1 快照将 3223 纳入精确 `Implemented` 集合；V2 扩展的 2/16 状态不变。 |

## 3. Unity MCP 定向回归

| 用例 | 锁定事实 |
|---|---|
| `KnockbackShot_FreezesNearestTwoTargetsAndClearsLoseStrengthAfterTheirActions` | 双目标结算、1 Ammo 支付、各自失去力量与携带者行动结束清零；每个敌方行动均锁定 `Damage.Order < LoseStrength clear.Order < IntentAdvanced.Order`。 |
| `KnockbackShot_WithOneLivingEnemySkipsMissingSecondSegment` | 只有一名存活敌人时只结算第一段，不因第二目标缺失而失败或递补。 |
| `KnockbackShot_FirstKillDoesNotRetargetMissingSnapshotSlot` | 三敌场景中首敌被 7 点首段击杀后，第二段仍命中初始第二敌；第三敌不受伤且不获得 LoseStrength。 |
| `KnockbackShot_InsufficientAmmoFailsWithoutWritingCombatOrZones` | Ammo 不足时不写战斗状态、资源或卡区。 |
| `KnockbackShot_ExplicitTargetFailsWithoutWritingCombatOrZones` | 显式目标输入被拒绝，失败路径零写入。 |
| `LoseStrength_SubtractsFromAttackButDoesNotModifyBurn` | 失去力量只改变 Attack 的来源力量项、不影响 Burn，并以实际低于零的输入锁定 Attack 结果下限为 0。 |
| `EndPlayerAction_ClearsPlayerLoseStrengthBeforeRoundEndBurn` | 玩家自己的行动结束先清除 LoseStrength，再结算回合末 Burn。 |
| `Resolve_AutomaticModesFollowEncounterOrder` | 自动前两名存活敌人遵循 Encounter 顺序。 |
| `GeneratedCatalog_MarineGameV1SnapshotPassesStarterRuntimeValidation` | V1 精确实现集合接受 3223 的状态翻转。 |
| `GeneratedCatalog_KnockbackShotKeepsAuthoredMetadata` | 3223 的 ID、费用/升级费用、目录目标、基础/升级归宿、可升级标记和 Program 绑定保持作者表契约。 |

上述定向集合经 Unity MCP 实际执行为 **10/10 passed，0 failed**，耗时 **0.3051449 秒**。随后完整 EditMode 为 **597/597 passed，0 failed**，耗时 **17.8830785 秒**。

## 4. 构建与数据证据

| 项目 | 结果 | 证据/说明 |
|---|---|---|
| 静态编译 | 通过 | `dotnet build TinySpire.sln --no-restore -v:minimal`：0 errors / 12 warnings。 |
| Luban 与生成配置 | 通过 | 3223 的生成状态为 `Implemented`；机枪兵共 82 项、49 `Implemented` / 33 `CatalogOnly`，V1 为 47/17，V2 扩展为 2/16。 |
| 本地化 | 通过 | Unity 菜单 `TinySpire/Localization/Import Battle Card Text from Excel` 执行成功。 |
| Sync 与本地 Addressables | 通过 | 唯一既有 Unity Editor 的 `TinySpire/Build/Sync and Build All` 成功，本地 Addressables 内容构建耗时 13.783 秒。 |
| Unity 定向 EditMode | 通过 | 10/10 passed，0 failed，0.3051449 秒。 |
| Unity 完整 EditMode | 通过 | 597/597 passed，0 failed，17.8830785 秒。 |

## 5. 验收后边界

本轮完成的是基础态 3223 与可复用的 LoseStrength 攻击修正/行动结束生命周期。升级 9/5/+3、恢复、束缚、格挡修正、延迟支援、动态临时卡、自动免费攻击、AnyAlly、默认 Deck、奖励和 Run 仍需后续独立切片；不得因目录状态或升级元数据存在而宣称这些能力已经可玩。
