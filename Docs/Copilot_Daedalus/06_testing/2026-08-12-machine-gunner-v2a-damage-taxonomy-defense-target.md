---
title: Marine Game 机枪兵 V2A 伤害语义与防御靶机运行时
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
related_decision: ../CODE_DECISIONS.md#cd-080伤害段以-kind--cardtag-声明并将防御后效局部预约
---

# Marine Game 机枪兵 V2A 伤害语义与防御靶机运行时

## 1. 验收对象与冻结行为

本记录覆盖新版 README 驱动的第一条纵切：伤害语义/词条迁移、`ElectroBoost` (3236) 更新与 `DefenseTarget` (3262) 基础态。

- 每段伤害使用 `MachineGunnerDamageKind` 和 `MachineGunnerCardTag`；Attack、Support、Bomb、Burn、Debuff 的修正只由伤害管线内部规则档案决定。
- `ElectroBoost` 为 1 Energy / Uncommon / Power / Hand→PowerPile，基础态获得可叠加、整场持续的 `FirePower +3`。
- `DefenseTarget` 为 2 Energy / Self / Hand→ExhaustPile，最少 2、最多 9 弹；每实际 3 弹获得 1 层 Intangible。2 弹成功但不写虚假的 Intangible `0→0` settlement。
- Intangible 只处理正值 incoming Attack，在 Block 前把攻击值封顶为 1 并消费一层；不随回合衰减。Buffer 优先于 Intangible，完全抵挡后不消费 Intangible；该组合优先级是本项目的显式实现决定。

## 2. 已变更表面

| 层 | 变更 |
|---|---|
| 伤害语义 | `MachineGunnerDamagePipeline` 增加 DamageKind/Tag 输入与私有规则档案；纯 Sniper 不读取 FirePower，`Shoot | Sniper` 同时读取射击与狙击规则。 |
| 受击后效 | Effect 链局部投影预约 Buffer 与 Intangible；提交严格为 Damage settlement 后的私有状态 settlement，保证多段链、失败路径和连续 Order。 |
| 职业状态/程序 | `MachineGunnerCombatState` 增加 Intangible 与 ArmorBreak；Program 62 以实际 Ammo/3 预演 Intangible，Program 36 改为持久 FirePower Power。 |
| 作者表与生成数据 | 3236 的类型/稀有/归宿更新为 Power/Uncommon/Power；3262 更新为 ExhaustPile 并翻为 Implemented。Luban JSON、构建门禁和目录快照同步为 46 / 18。 |
| 本地化与内容 | 已从 `i18n.xlsx` 导入 Battle Card String Table；随后重建本地 Addressables 内容。 |

## 3. 已加入或更新的回归

| 用例 | 锁定事实 |
|---|---|
| `FirePower_AppliesToShootAndShootSniperTags` | 纯 Sniper 不吃 FirePower，普通 Shoot 与 Shoot+Sniper 会读取 FirePower。 |
| `Support_UsesTargetSmokeVulnerableAndArmorBreakOnly` | Support 不读取来源 Strength/Weakness/Smoke，只读取目标允许的修正。 |
| `BombAndBurn_KeepTheirSeparateDamageSemantics` | Bomb 与 Burn 不被错误并入普通攻击公式。 |
| `DefenseTarget_SpendsAmmoInThreesAppliesIntangibleAndExhausts` | Energy/Ammo/Intangible/Hand→Exhaust 的事务顺序与无回合衰减。 |
| `DefenseTarget_UsesActualAmmoWithMinimumAndMaximumBoundaries` | 2、3、10 弹的下限、分段层数和 9 弹上限。 |
| `DefenseTarget_RequiresAtLeastTwoAmmoWithoutWritingAnyState` | 1 弹失败时资源、手牌、卡区与状态零写入。 |
| `PrepareAndCommit_MachineGunnerIntangible_CapsEachReservedAttackBeforeBlock` | 同一 Effect 链逐段预留/消费 Intangible，封顶发生在 Block 前。 |
| `PrepareAndCommit_MachineGunnerBuffer_PrioritizesOverIntangible` | Buffer 先完全抵挡，下一段才消费 Intangible。 |
| `ElectroBoost_EntersPowerPileAndKeepsFirePowerAfterActionEnd` | 3236 的 Power 归宿、+3 与跨行动持续。 |
| `SpikeShot_StimInterleavesEveryHitAndFeedsNextHit` | 双标签 Spike 的 FirePower、Stim 与狙击易伤倍率使用新版期望值。 |

## 4. 本轮证据与结论

| 项目 | 结果 | 证据/说明 |
|---|---|---|
| Luban 与生成配置 | 通过 | 已生成 `battle_tbcard.json`；3236 为 Power/Uncommon/Power，3262 为 Skill/Rare/Exhaust/Implemented。 |
| 本地化 | 通过 | Unity 菜单 `TinySpire/Localization/Import Battle Card Text from Excel` 输出 `TinySpire battle card localization imported from Excel and validated.` |
| Sync 与本地 Addressables | 通过 | 单一既有 Unity 6000.5.5f1 Editor 的 `TinySpire/Build/Sync and Build All` 输出 Addressable content built（19.945 秒）和整体同步成功。 |
| Unity 定向 EditMode | 通过 | MCP 任务 `6426b623af174b908464886033acfda5`：110/110 passed，0 failed，0 skipped，0.631671 秒。 |
| Unity 完整 EditMode | 通过 | MCP 任务 `3e20086749f1423f99784361f0477cf5`：584/584 passed，0 failed，0 skipped，14.9492098 秒。 |

## 5. 验收后边界

本切片没有新增 18 个 V2 奖励模板、奖励/Run、默认 Deck、升级实例、AnyAlly、手牌选择、Power HUD、Scene、Prefab 或第二条写入链。Support/Bomb/Burn 类型已具备伤害语义，但延迟效果实例、触发时机、随机目标和多实例生命周期仍需后续专门切片实现。
