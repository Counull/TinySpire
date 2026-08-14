---
title: 共享重复伤害、Ironclad Sword Boomerang 与机枪兵幻彩射击运行时
page_type: testing
lifecycle: active
date: 2026-08-13
updated: 2026-08-14
status: verified-unity-native-2026-08-13
status_source: ../SESSION_LOG.md
source:
  - ../04_research/2026-08-06-sts2-v01071-ironclad-card-snapshot.md
  - ../00_inbox/README web.md
  - ../01_requirements/2026-08-13-sts2-v01071-ironclad-first-four-runtime-digest.md
  - ../01_requirements/2026-08-12-machine-gunner-v2-requirement-digest.md
related_plan:
  - ../plans/2026-08-06-sts2-v01071-ironclad-card-pool.md
  - ../plans/2026-08-12-machine-gunner-v2-card-runtime-plan.md
related_decision: ../CODE_DECISIONS.md#cd-105sword-boomerang-与幻彩射击通过共享具体重复伤害计划接入
---

# 共享重复伤害、Ironclad Sword Boomerang 与机枪兵幻彩射击运行时

本页记录 concrete prepared repeated-damage plan、通用逐段随机目标适配、机枪兵固定目标后效适配、`Sword Boomerang`（3116）与 `Prismatic Shot / 幻彩射击`（3279）基础态，以及作者表、Luban、本地化、真实 AssetBundle 和 Unity 原生门禁证据。

## 1. 验收结论

- `BattlePreparedRepeatedDamagePlan` 已在首次写入前冻结来源、Encounter 全体敌人标量、每段目标与伤害 outcome、各目标终态投影、卡牌目标随机流前后状态和 settlement 数量；Validate 拒绝 owner、快照、随机流或顺序漂移，Commit 不再选目标或重算公式。
- `Sword Boomerang`（3116）基础态已翻为 `Implemented`：1 Energy、Common Attack、`RandomEnemy`、Hand→DiscardPile，按 `damage:4015, damageRepeat1:4015, damageRepeat2:4015` 独立造成 3 次基础 3 点伤害。每段从当时投影仍存活敌人中重选；目标死亡后不再进入后续候选，没有存活敌人时停止且不再推进随机。升级第 4 次仍只是元数据。
- `Prismatic Shot`（3279）基础态已翻为 `Implemented`：0 Energy、Rare Attack、显式 Enemy、Program 79、基础 Ammo 1。命令开始时冻结目标状态种类数 `S`，逻辑段为 `[6, 9 × S]`；Stim 激活时每个逻辑段后立即复制同基础值，并一次性要求 `1 + logicalCount` Ammo，任一资源不足都在首写前失败。
- 幻彩射击的每个来源段或 Stim 复制段都复用既有 `main Damage → IncendiaryAmmo Burn → PortableHelper` 顺序；固定目标投影死亡后停止全部剩余段，不重定向，帮手段也不递归。升级把首段 6 改为 9、状态重复段保持 9，当前仍只属于升级元数据。
- 全项目 168 张现为 **90 `Implemented` / 78 `CatalogOnly`**；Ironclad 85 张为 **11/74**，Marine 82 张为 **78/4**（V1 **61/3**、V2 **17/1**），Effect 共 **15** 项。

## 2. 共享 concrete prepared plan 与适配边界

| 层 | 当前契约 |
|---|---|
| 目标策略 | `FixedEnemy` 固定同一显式敌人，死亡后停止；`RandomLivingEnemyPerHit` 每段只从投影存活 Encounter 敌人中选择。 |
| 不可变输入 | `BattleRepeatedDamageRequest` 保存来源、目标策略和有序 `BattleRepeatedDamageHitRequest`；配置 Effect ID 可选，基础值逐段冻结。 |
| Prepare | `BattleRepeatedDamageExecutor` 只读预演全部段，保存来源与所有敌人快照、段目标、主伤 outcome、后效后的目标投影和 RNG before/after。 |
| Validate | 核对计划 owner、来源/敌人标量、Encounter 顺序、卡牌目标 RNG、职业序列快照、连续 starting order 和一次性生命周期。 |
| Commit | 按冻结段调用命中序列提交连续 settlement，最后一次性推进权威卡牌目标 RNG；不再随机或重算伤害。 |
| 通用适配 | `BattleRepeatedDamageEffectAdapter` 只接受普通 `DealDamage / Attribute.None` grammar；默认序列复用既有伤害 outcome 与写入口，不知道 3116、卡名或职业。 |
| 机枪兵适配 | `MachineGunnerRepeatedDamageHitSequence` 冻结 Stim、IncendiaryAmmo、PortableHelper 和 17 种私有状态快照，并复用现有逐 hit 后效；共享规划器不知道 Program 79、Ammo 或职业状态。 |

通用卡牌目标随机流由 `BattleTurnController` 持有唯一可变 `GameRandom`；`BattleSession` 只保存由战斗种子复制的不可变 `CardTargetRandomSeed`，`BattleCommandQueue` 只读暴露当前状态供事务与测试核对。Sword Boomerang 的成功提交推进该流，显式目标错误、费用/配置/快照失败或没有候选的未执行尾段不推进；固定目标的幻彩射击不消费该随机域。

## 3. 两张卡的权威语义

| 场景 | 冻结顺序与结果 | 验收 |
|---|---|---|
| Sword，三名存活敌人 | 支付 1 Energy 后，每段根据上一段投影重新选取存活敌人并造成基础 3 点普通伤害，最后来源 Hand→DiscardPile | 通过 |
| Sword，前段击杀 | 被击杀目标从后续候选移除；仍有敌人则继续随机，没有敌人则停止剩余段且不再取随机数 | 通过 |
| Sword 携带显式目标 | 在 Energy、HP/Block、卡区、Turn、settlement 与卡牌目标 RNG 首写前返回 `TargetRuleMismatch` | 通过 |
| Prismatic，目标有两种起始状态 | `S=2`，逻辑段固定为 `6,9,9`；Stim 激活时展开为 `6,6,9,9,9,9`，每段紧邻执行 Burn 与 Helper | 通过 |
| Prismatic，Stim + 两种状态但只有 3 Ammo | 需要 `1 + 3 = 4` Ammo；规则/费用门禁在任何资源、伤害、状态、卡区或 settlement 写入前失败 | 通过 |
| Prismatic，固定目标中途死亡 | 完成致死段及其合法紧邻后效后停止剩余段，不选择其他敌人 | 通过 |

幻彩射击的 `S` 精确包含：Strength 非零、Vulnerable 正层，以及 17 种 `MachineGunnerCombatantStatus` 中每一种正层各计一次；同种多层仍只计一种。HP、Block、资源、PowerPile、Stim 本身和 scheduled effect 不计入 `S`。

## 4. TDD、回归诊断与修复证据

| 切片 | 红灯 / 初始证据 | 绿灯 |
|---|---|---|
| Sword RandomEnemy grammar | 任务前缀 `7f83`，1/1 failed，唯一暴露 `UnsupportedTargetRule` | `587c3264fb684e49bf46501a81c96b33`，1/1 passed |
| Prismatic Program 79 | `5f6ae2d4063e4fb6b40e5acb40558fca`，failed，旧实现返回 `ExecutionFailed` | `4136fa81888a40a6b147193447eea60d`，Sword + Prismatic 核心 2/2 passed |
| concrete plan 与双卡边界 | — | `6932f72f288a477ca5869c21e3ac3996`，11/11 passed |
| 正式目录 / 数据门禁 | — | `908e5fb8b93e437d89533bb1b727231a`，53/53 passed |
| 复合卡区回归代表集 | — | `6ee679521f4c45d9a69b9984110c51bb`，5/5 passed |

初次广义行为聚合 `14131e7fa23c4f14a3a08e2cad0da556` 完成 250 项，但有 16 项失败。最小化到 Bully 后确认 Queue 进入 `UnexpectedException`，精确异常为“机枪兵卡区 settlement 顺序不连续”。根因不是新卡规则，而是现有机枪兵复合卡区计划在计算 starting order 时，局部 `settlements` 尚未包含稍后会排在最前的 `EnergySpent` / 可选 `AmmoSpent`；Commit 因而看到序号少 1 或 2。

修复只调整本地计划序列：在任何复合计划 Prepare 前先把不可变付款 settlement 放入局部列表，所有后续 starting order 统一读取 `settlements.Count`；这一步不写权威资源，失败时列表直接丢弃。修复后代表集 5/5、最终行为聚合 `4ea4eff81b3c4ce786e318d0902c1ed4` **243/243 passed**，既有 Bully、Limit Overload、Machinegun Burst 与 Vent Heat 路径恢复连续顺序。

## 5. 正式数据与生成证据

| 项目 | 正式结果 |
|---|---|
| `DataTables/Datas/__enums__.xlsx` | SHA-256 `DC35FC55DF7A4223347F81054C09DF88DDEA3B6EB88DA36DE41499562DD7618E`；本切片未改枚举。 |
| `DataTables/Datas/battle.card.xlsx` | SHA-256 `EA90C1A34FBDD9C54EBE2832C6CCC796DC4752A6B90C15F6A42BDB8C03A2CDF1`；3116 与 3279 精确翻为 `Implemented`。 |
| `DataTables/Datas/battle.card_effect.xlsx` | SHA-256 `35BF163D09E6F8AA6478C134D90A5FBAC304CC3135357D8237909DBC87ECAE64`；4015=`DealDamage / None / 3`。 |
| `DataTables/Datas/i18n.xlsx` | SHA-256 `B80CD6EDCD0EAE2F52812B1CFF5DDAD96C1AB0507CD05E012C919DB05122215F`；两张卡基础与升级文本和参数已同步。 |
| Luban | 等价 `dotnet` 命令退出码 0，并复制当前 `game-config.json`。 |
| `TinySpire/Assets/GameData/battle_tbcard.json` | SHA-256 `6E98294D29018782E7EB7E878B2A51808D297EF80FE5F345CD854651767FDE4A`；168 张为 90/78。 |
| `TinySpire/Assets/GameData/battle_tbcardeffect.json` | SHA-256 `79FB3FC1ED473E4D35873D4D5EC25EC1111016AB60DBF5B9B5FE596C8C0FF03D`；15 项。 |

生成数据精确冻结 3116 为 1E / RandomEnemy / Discard / Program 0，三条绑定均指向 Effect 4015；3279 为 0E / Enemy / Discard / Program 79 / 空 bindings。Localization Import 与 Validate 均成功。

`TinySpire/Build/Sync and Build All` 成功，Addressables 子构建耗时 13.962 秒。最新报告 `TinySpire/Library/com.unity.addressables/BuildReports/buildlayout_2026.08.13.21.31.49.json`（134612 bytes）证明 Card / Effect JSON 由 `AssetBundleProvider` 写入物理包 `TinySpire/Library/com.unity.addressables/aa/Windows/StandaloneWindows64/tinyspiregamedata_assets_all_7a71d50ab0fc1e0160ad9082a64b653d.bundle`（12182 bytes）。

## 6. 静态、行为与完整 Unity 门禁

| 门禁 | 任务 / 结果 |
|---|---|
| Runtime 静态编译 | 0 error / 6 条既有 warning |
| Editor 静态编译 | 0 error / 12 条既有 warning |
| concrete plan 与双卡定向行为 | `6932f72f288a477ca5869c21e3ac3996`，**11/11 passed** |
| 正式目录 / 数据 | `908e5fb8b93e437d89533bb1b727231a`，**53/53 passed** |
| 回归修复代表集 | `6ee679521f4c45d9a69b9984110c51bb`，**5/5 passed** |
| 最终行为聚合 | `4ea4eff81b3c4ce786e318d0902c1ed4`，**243/243 passed** |
| 完整 EditMode | `3e0a091d891e4f918668b99cb4a20157`，**776/776 passed**（77.7525946 秒） |

最终正式任务均为 0 failed / 0 skipped，Console 已清空；完整 runner 的权威实际结果为 776/776。真实 AB 证据来自本轮最新 BuildLayout 与物理 bundle，不以 Fast Mode、静态 JSON 或代码编译替代。

## 7. 验收边界

### 2026-08-14 通用 Poison 后续口径

- 本页发布时幻彩射击的历史 `S` 为 Strength + Vulnerable + 17 种职业私有状态，相关 90/78、78/4 与 776/776 证据保持原样。CD-106 后共享 helper 追加目标正层通用 Poison，当前最大 `S` 身份数为 20；同一 Poison 只按存在计一次，仍在命令起点冻结。
- Poison 计数任务前缀 `419c…` 与行为聚合 `79a…` 保留为开发中证据；Secondhand Smoke 已完成正式生成与 post-generation 门禁，最终定向任务 `88e36d2a5cbb47b7b4a67207dad00856` 为 9/9、完整 EditMode `9ca3d43a79d24b25a917fad7b6166584` 为 793/793。当前正式计数为全项目 92/76、Marine 79/3，但不得把该后续证据倒写成 2026-08-13 发布时已含 Poison。

- Sword Boomerang 升级第 4 次伤害、Prismatic Shot 升级首段 9 点仍只是作者表与本地化元数据；没有升级 `CardInstance` 或升级数值切换。
- 本切片只开放具体 repeated-damage plan 的 `FixedEnemy` 与 `RandomLivingEnemyPerHit` 两种已使用策略，不把它扩大成全体、链式、权重、独立目标规则 DSL 或全局伤害事件。
- 共享 planner 只管理目标选择、投影、随机快照和计划生命周期；机枪兵 Ammo、Stim、IncendiaryAmmo、PortableHelper 与状态种类仍留在职业适配器，不泄漏到普通 Effect 层。
- 默认 Deck、奖励、Run、多人 Session、Scene / Prefab、升级实例、其余 74 张 Ironclad 与 4 张 Marine `CatalogOnly` 均未因本切片自动完成。
