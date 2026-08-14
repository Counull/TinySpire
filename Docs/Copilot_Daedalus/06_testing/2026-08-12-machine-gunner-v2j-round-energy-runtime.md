---
title: Marine Game 机枪兵 V2J 回合能量修正基础态
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
related_decision: ../CODE_DECISIONS.md#cd-089v2j-过载供能与防御姿态共享一次性下回合能量净修正
---

# Marine Game 机枪兵 V2J 回合能量修正基础态

## 1. 验收对象与冻结行为

本切片只开放 `Overload` (3213) 与 `DefensiveStance` (3271) 的基础态：

- 3213 为 0 Energy、Skill、Self、Hand→DiscardPile。成功时即时获得 2 Energy，但不得超过 EnergyMaximum；随后累计 `NextRoundEnergyGainPenalty +1`。
- 3271 为 1 Energy、Skill、Self、Hand→DiscardPile。成功时严格按支付 1 Energy → 获得 8 Block → `NextRoundEnergyGainBonus +1` → 弃牌提交。
- Bonus 与 Penalty 是两项独立、非负、可分别叠加的一次性职业私有状态。下一玩家回合开始的有效补给为 `max(0, baseGain + bonus - penalty)`，之后按 EnergyMaximum 裁剪；补给结算后两项状态分别清零。
- 当前回合的主动获得使用 `BattleEnergyGainedSettlement`，回合开始补给使用 `BattleEnergyRefilledSettlement`。两者保持不同记录类型，不用一个“能量变化”结果抹平来源和时机。
- 能量不足或 Self 卡收到显式敌人目标时，参与者、资源、Block、私有状态、卡区、随机流和表现结果均保持零写入。

本切片没有实现升级 `CardInstance`，也没有把卡加入默认 Deck、奖励或 Run；未修改 UI、多人、Scene 或 Prefab。

## 2. 已变更表面

| 层 | 变更 |
|---|---|
| 作者表/生成配置 | `battle.card.xlsx` 只将 3213 与 3271 的 `implementation_status` 翻为 `Implemented`；Luban 生成后的机枪兵目录为 82 项、64 `Implemented` / 18 `CatalogOnly`。 |
| 职业程序 | `Overload` 复用即时 `GainEnergy` 与下一回合 Penalty；`DefensiveStance` 复用 `GainBlock` 与下一回合 Bonus，两张均保持 Self / DiscardPile。 |
| 回合开始资源 | Bonus/Penalty 各自冻结、相减并只修正一次基础补给；有效值先下限裁剪到 0，再按 EnergyMaximum 上限裁剪，随后分别清零两项状态。 |
| settlement | 当前回合主动获得为 `BattleEnergyGainedSettlement`；下一回合补给为 `BattleEnergyRefilledSettlement`；私有状态叠加与清除继续使用强类型状态 settlement，`Order` 连续。 |
| 目录门禁 | V1 新增 3213 为 `Implemented`，V2 扩展新增 3271 为 `Implemented`；V1 为 54/10，V2 扩展为 10/8。 |

## 3. 已加入的定向回归

| 用例 | 锁定事实 |
|---|---|
| `Overload_GainsTwoEnergyUpToMaximumAndSchedulesPenalty` | 3213 在当前 Energy 4 / Maximum 5 时只实际增加 1 Energy，以 `BattleEnergyGainedSettlement` 记录 4→5，并累计 Penalty +1 后进入 DiscardPile。 |
| `DefensiveStance_SpendsOneEnergyGainsBlockAndSchedulesBonus` | 3271 支付 1 Energy，随后获得 8 Block、累计 Bonus +1；资源、Block、状态和弃牌 settlement 顺序连续。 |
| `RoundStart_StacksBonusAndPenaltyAppliesNetGainOnceAndClearsBoth` | 两层 Penalty 与一层 Bonus 独立叠加；下一回合只把净修正应用到一次基础补给，产生 `BattleEnergyRefilledSettlement` 而不是主动获得记录，之后两项状态均清零且再下一回合不重复生效。 |
| `DefensiveStance_InsufficientEnergyFailsWithoutWritingBlockBonusOrZones` | 能量不足时 Block、Bonus、资源、卡区、随机流和表现结果零写入。 |
| `RoundEnergyCards_ExplicitTargetFailsWithoutWritingResourcesStatusesOrZones` | 3213 与 3271 都拒绝显式敌人 `TargetId`，并保持资源、状态、卡区、随机流和表现结果零写入。 |
| `GeneratedCatalog_OverloadKeepsAuthoredMetadata` | 3213 保留 0 费、Self、DiscardPile、Program 13 与精确 `Implemented` 状态。 |
| `GeneratedCatalog_MarineGameV2ExtensionKeepsAuthoredMetadataAndImplementationGate` | 18 个 V2 扩展身份保持冻结，3271 精确开放；V2 为 10/8，全部机枪兵目录为 64/18。 |

## 4. 数据、同步与 Unity 证据

| 项目 | 结果 | 说明 |
|---|---|---|
| Luban 与生成配置 | 通过 | 只开放 3213 与 3271；82 模板为 64 `Implemented` / 18 `CatalogOnly`，V1 为 54/10，V2 为 10/8。 |
| 本地化 | 通过 | 从 Excel 导入战斗卡本地化成功。 |
| Sync 与本地 Addressables | 通过 | `Sync and Build All` 完成，Addressables 构建耗时 11.72 秒。 |
| Unity 定向 EditMode | 通过 | 补强获能上限、净修正、补给下限、一次性清除顺序与零反馈表现契约后，MCP 任务 `3e73f867e7404be8a3180660e4999d20`：136/136 passed，0 failed。 |
| Unity 完整 EditMode | 通过 | MCP 任务 `56274033527e4c78b50a78313bcc0f6c`：631/631 passed，0 failed，耗时 17.642 秒。 |

## 5. 3260 延期边界

`LimitOverload` (3260) 继续为 `CatalogOnly`，没有因本切片具备即时 GainEnergy 与下一回合 Penalty 就被一并开放。

它还要求“抽牌到手牌满”。当前权威出牌流程在程序操作提交完毕后才把正在打出的卡从 Hand 移至 DiscardPile；若直接在程序中使用 `DrawCards(10)`，抽牌容量计算会把 3260 自身仍计入 Hand，因此稳定少抽一张。正确实现需要独立的“抽至满手”卡区预演/提交 seam：准备阶段基于本卡成功归宿后的投影 Hand 冻结真实缺口，首次写入前联合校验卡区与抽牌事实，提交时保持洗牌、抽牌、卡牌归宿和 settlement 顺序原子一致。

V2J 没有使用固定抽牌数、提前移牌、额外补抽或第二条 Queue 外写入路径来伪装完成 3260。

## 6. 验收后边界

- 本切片只实现 3213 与 3271 基础态；升级列仍只是作者表元数据。
- 没有修改默认 Deck、奖励、Run、UI、多人、Scene 或 Prefab。
- 没有实现 3260、临时卡、手牌选择、自动免费攻击、AnyAlly 或其他跨卡协议。
- 其余 18 张 `CatalogOnly` 继续由精确目录门禁拒绝。
- 本切片未创建提交，也不记录或杜撰提交 SHA。
