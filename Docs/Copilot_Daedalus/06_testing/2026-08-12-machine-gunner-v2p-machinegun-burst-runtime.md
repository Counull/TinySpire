---
title: Marine Game 机枪兵 V2P 机枪扫射基础态
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
related_decision: ../CODE_DECISIONS.md#cd-095v2p-机枪扫射分离实际零弹耗与游击名义弹耗并显式退出两类联动
---

# Marine Game 机枪兵 V2P 机枪扫射基础态

## 1. 验收对象与冻结行为

本切片只开放临时卡 `MachinegunBurst` (3263) 的直接运行时基础态：

- 0 Energy、Attack、RandomEnemy、Hand→ExhaustPile、无升级；成功时执行两段独立的基础 5 点普通 Attack。
- 每段开始都从当时投影的存活敌人重新取得候选并调用卡牌随机流。首段致死会改变第二段候选；显式 `TargetId` 不符合 RandomEnemy 规则，必须在首次写入前失败且不推进随机。
- 实际 Ammo 成本为 0：不改变当前 Ammo，也不生成 `AmmoSpent`。游击战术单独把本卡视为消耗 2 Ammo；两层 Guerrilla 因而在伤害之后、卡牌离手之前获得 4 Block。
- 来源没有声明 Shoot 标签，项目冻结 `Tags.None`，不从名称推断射击。因此 3263 不使用 Stim、IncendiaryAmmo、FirePower 或 PortableHelper。
- 3263 同时显式退出普通非射击 Attack 的 KungfuMech、AgedOil 与 `NonShootAttackRecent` 三个入口；它仍是普通 Attack，继续走既有伤害公式、Block/HP、致死与连续 settlement 生命周期。

来源声明 3263 只能由 `FixedMachinegun` (3261) 创建且不进入奖励池；但 3261 仍为 `CatalogOnly`，项目也没有奖励运行时。本切片只验证 3263 的直接程序，不把“生产流程可生成”或“奖励排除已实现”写成完成事实。

## 2. 已变更表面

| 层 | 变更 |
|---|---|
| Program 63 | 注册 RandomEnemy、两段基础 5 点 Attack、0 Energy / 0 Ammo、ExhaustPile 与无升级身份。 |
| 游击名义弹耗 | 程序可选声明 `AmmoSpentForGuerrillaOverride`；默认继续使用实际弹耗，3263 唯一覆盖为 2。 |
| 非射击联动资格 | 统一 `ParticipatesInNonShootAttackSynergies` 派生事实供 KungfuMech、AgedOil 与最近非射击 Attack 记录读取；3263 显式为 false。 |
| 随机与事务 | 每段基于最新投影重选活敌，整张卡成功后才提交卡牌随机状态；Queue 与卡区权威边界不变。 |
| 作者表与门禁 | 只将 3263 翻为 `Implemented`；82 模板为 70/12，V1 为 56/8，V2 扩展仍为 14/4。 |

## 3. 定向回归门禁

| 用例 | 锁定事实 |
|---|---|
| `MachinegunBurst_DealsTwoRandomFiveDamageWithoutAmmoSpendAndExhausts` | 两段各 5 点、实际 Ammo 不变、无 AmmoSpent、成功进入 ExhaustPile。 |
| `MachinegunBurst_GuerrillaUsesNominalTwoAmmoAfterDamageBeforeDeparture` | 游击只读取名义 2；两层获得 4 Block，顺序为伤害 → Block → 当前牌离手。 |
| `MachinegunBurst_WithoutShootTagDoesNotReceiveShootOrNonShootSynergies` | 不消费/触发 Stim、IncendiaryAmmo、PortableHelper，也不触发 KungfuMech、AgedOil 或记录最近非射击 Attack。 |
| `MachinegunBurst_ReSelectsFromLivingEnemiesAfterFirstHitKillsTarget` | 两名 5 HP 敌人在两段中分别死亡，证明第二段从首段致死后的存活候选重选。 |
| `MachinegunBurst_ExplicitTargetFailsWithoutWrites` | 显式目标返回目标规则失败；HP、Energy、Ammo、随机、Layout、Hand、Exhaust 与表现结果均零写入。 |
| V1/V2 快照与构建门禁 | 3263 的 Attack/Common/0E/RandomEnemy/Exhaust/无升级/Program 63/非 Innate/Implemented 元数据，以及 70/12、V1 56/8、V2 14/4 计数被精确冻结。 |

上述运行时用例通过专用 fixture 直接把 3263 放入 Hand；这证明程序可执行，不是 3261 已能在生产流程创建临时卡的证据。

## 4. 数据、同步与 Unity 证据

| 项目 | 结果 | 说明 |
|---|---|---|
| 正式作者表 | 已复核 | `battle.card.xlsx` SHA-256 `B65D97253A43B2FF8575BCEE6F230B651EFD36FE84A10B7ACBFC0BCC62A0AB29`；只将 3263 的状态翻为 `Implemented`。 |
| Luban 与生成配置 | 通过 | 82 模板为 70 `Implemented` / 12 `CatalogOnly`，V1 为 56/8、V2 为 14/4；3263 保持 Program 63、RandomEnemy、Exhaust、`is_innate=false` 与无升级。 |
| Editor 静态编译 | 通过 | 0 error；12 条既有程序集版本 warning。 |
| 本地化导入/校验 | 通过 | Excel 本地化导入与显式校验均成功。 |
| `Sync and Build All` / Addressables | 通过 | 同步及本地内容构建成功，Addressables 耗时 11.757 秒。 |
| Unity 最终聚合定向 EditMode | 通过 | MCP 任务 `0f60a2e799904069ab68ae6f13a91953`：154/154 passed，0 failed/skipped，2.698029 秒；覆盖 Starter 运行时、V1/V2 快照与构建门禁。 |
| CardArt 域重载探针 | 通过 | MCP 任务 `f87e7034664a4126bb0b32c2888751e9`：1/1 passed，10.3685051 秒。 |
| 完整 EditMode | 通过 | MCP 任务 `a078688b69bd4f198bb736c6285ab5e7`：678/678 passed，0 failed/skipped，47.3413725 秒。 |

3263 程序、作者表、生成、本地化、同步构建、最终聚合定向、Addressables 真实加载探针与完整 EditMode 均已通过；本切片按标准完整门禁收口。

## 5. Addressables 诊断边界

- 在同一 Editor 完成本地 Addressables 重建后、尚未发生域重载时，完整任务 `dd80e8747e394c7387f8c57497c88f7d` 与随后精确任务 `b4f20bc3f8364329a374df26c76925b6` 均完成测试枚举，但唯一非绿项都是 `CardArtLogicalAddresses_LoadSprites` 等待 180 秒。
- 没有提高测试 timeout，没有修改生产加载代码，没有清理 Addressables 缓存，也没有重建或替换 bundle。让既有 Editor 完成域重载后，同一真实加载探针在 10.3685051 秒通过，随后完整 678/678 在 47.3413725 秒通过。
- 因而该现象记录为“同 Editor 重建内容后遗留的 Addressables 静态状态”诊断，不记为 3263 生产缺陷，也不把两次 timeout 当作绿色验收证据。最终验收只采用域重载后的探针和完整任务。

## 6. 验收后边界

- 只实现 3263 的直接基础态程序；3261 仍为 `CatalogOnly`，临时卡实例的生产创建、卡区 settlement 与表现合同尚未实现。
- `Implemented` 表示该身份通过直接运行时入口可执行，不表示它已能从正常 Deck、抽牌、奖励或 Run 流程获得。
- 来源中的“不进入奖励池”尚无对应奖励系统可验收；本切片未新增奖励过滤器，也不伪称该排除已落地。
- 没有实现升级、默认 Deck、Run、UI、多人、Scene 或 Prefab。
- 本切片未创建提交，也不记录或杜撰提交 SHA。
