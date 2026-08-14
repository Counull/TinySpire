---
title: Marine Game 机枪兵 V2I 充能爆射基础态
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
related_decision: ../CODE_DECISIONS.md#cd-088v2i-充能爆射按施放快照序号线性生成纯狙击段
---

# Marine Game 机枪兵 V2I 充能爆射基础态

## 1. 验收对象与冻结行为

本切片只开放 `ChargedBurst` (3282) 的基础态：

- 2 Energy、Attack、AllEnemies、Hand→DiscardPile，不消耗 Ammo；显式 `TargetId` 被拒绝。
- 施放时按 Encounter 顺序快照全部存活敌人。第 `n` 名目标的基础伤害为 `12 + 6 × (n - 1)`，即前三名分别承受 12、18、24；前段致死不会删除快照槽位，也不会让后续目标改用更小序号。
- 每一段都是纯 `Sniper`：不带 `Shoot`，因此不读取 Stim 的额外段或 FirePower；读取 IncendiaryAmmo，并在每个目标的伤害后分别施加燃烧弹药；成功攻击后保留来源 Invisible。
- 全部伤害、逐目标燃烧弹药、2 Energy 支付和弃牌都属于同一权威出牌事务，settlement 的 `Order` 连续。

本切片没有把卡加入默认 Deck、奖励或 Run，也没有实现升级 `CardInstance`、UI、多人或其他跨卡协议。

## 2. 已变更表面

| 层 | 变更 |
|---|---|
| 作者表/生成配置 | `battle.card.xlsx` 只将 3282 的 `implementation_status` 翻为 `Implemented`；Luban 生成后的目录为 82 项、62 `Implemented` / 20 `CatalogOnly`。 |
| 职业程序 | `MachineGunnerBattleRuntime` 注册 `ChargedBurst`，使用 `AllLivingEnemies` 输入模式、纯 `Sniper` 标签和 `LinearDamageByTargetOrdinal` 执行类型；基础值为 12，并声明成功攻击后保留 Invisible。 |
| 目标与序号 | 准备阶段冻结当时存活敌人的 Encounter 顺序和各自序号；提交阶段按该快照逐段结算，前段致死不重排后续段。 |
| 目录门禁 | V2 扩展快照只新增 3282 为 `Implemented`；V1 保持 53/11，V2 扩展更新为 9/9。 |

## 3. 已加入的定向回归

| 用例 | 锁定事实 |
|---|---|
| `ChargedBurst_ThreeEnemiesDealTwelveEighteenTwentyFourAndDiscard` | 三名存活敌人按 Encounter 顺序承受 12/18/24，支付 2 Energy、进入 DiscardPile，全部 settlement 顺序连续。 |
| `ChargedBurst_EarlierFatalHitsDoNotRenumberLaterEnemyDamage` | 前两段分别致死时，第三名敌人仍保留施放快照的第三段 24 点基础伤害，不重排为 12。 |
| `ChargedBurst_PureSniperIgnoresStimAndFirePowerPreservesInvisibleAndAppliesIncendiaryPerTarget` | 程序只有 `Sniper` 标签；Invisible 使三段成为 24/36/48 且结算后保留，Stim/FirePower 不参与，每名目标在自身伤害后分别获得 IncendiaryAmmo 的 Burn。 |
| `ChargedBurst_ExplicitTargetFailsWithoutWritingCombatResourcesOrZones` | AllEnemies 卡收到显式敌方 `TargetId` 时返回 `TargetRuleMismatch`，参与者、资源、卡区、随机流与表现结果零写入。 |
| `ChargedBurst_InsufficientEnergyFailsWithoutWritingCombatStatusesOrZones` | 能量不足时伤害、Invisible、资源、卡区、随机流与表现结果均零写入。 |
| `GeneratedCatalog_MarineGameV2ExtensionKeepsAuthoredMetadataAndImplementationGate` | 18 个 V2 扩展身份的元数据和 9/9 精确实现状态保持冻结。 |

## 4. 数据、同步与 Unity 证据

| 项目 | 结果 | 说明 |
|---|---|---|
| Luban 与生成配置 | 通过 | 3282 的基础态状态翻转成功；82 模板为 62 `Implemented` / 20 `CatalogOnly`，V1 为 53/11，V2 为 9/9。 |
| 本地化 | 通过 | 从 Excel 导入战斗卡本地化成功。 |
| Sync 与本地 Addressables | 通过 | `Sync and Build All` 完成，Addressables 构建耗时 11.456 秒。 |
| Unity 定向 EditMode | 通过 | MCP 任务 `1d5c9e1d96fe4ebcadd990fcc73fccdc`：94/94 passed，0 failed。 |
| Unity 完整 EditMode | 通过 | MCP 任务 `822d066bc54c43d78ac206072789f840`：622/622 passed，0 failed，耗时 18.193 秒。 |

首次以 `bdaf0` 开头的 MCP 任务在测试初始化阶段超时，实际执行 0 项；它不构成失败回归。确认没有可用测试结果后重试，上表记录的是随后真实执行完成的定向与完整任务。

## 5. 验收后边界

- 本切片只实现 3282 基础态；没有实现升级实例。
- 没有修改默认 Deck、奖励、Run、UI、多人、Scene 或 Prefab。
- 没有接入临时卡、手牌选择、自动免费攻击、AnyAlly、帮手/支援命中后钩子或其他跨卡协议。
- 其余 20 张 `CatalogOnly` 继续由精确目录门禁拒绝。
- 本切片未创建提交，也不记录或杜撰提交 SHA。
