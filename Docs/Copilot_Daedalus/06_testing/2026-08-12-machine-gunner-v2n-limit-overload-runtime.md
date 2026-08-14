---
title: Marine Game 机枪兵 V2N 极限过载基础态
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
related_decision: ../CODE_DECISIONS.md#cd-093v2n-极限过载以当前牌离手后投影的卡区深事务抽至满手
---

# Marine Game 机枪兵 V2N 极限过载基础态

## 1. 验收对象与冻结行为

本切片只开放 `LimitOverload` (3260) 基础态：

- 0 Energy、Rare、Skill、Self、Hand→DiscardPile。成功时仍先产生金额为 0 的 `BattleEnergySpentSettlement`，再即时获得 1 Energy；获能受 EnergyMaximum 上限裁剪，已在上限时不生成伪 `BattleEnergyGainedSettlement`。
- 以当前卡成功离手后的投影 Hand 为输入，抽到手牌上限 10；如果牌源不足，只抽取可用的原 DrawPile 与原 DiscardPile，不伪造额外卡。
- 联合 settlement 顺序为 `EnergySpent(0) → 可选 EnergyGained(1) → 当前牌 Hand→DiscardPile → 旧 DiscardPile→DrawPile/重洗/抽牌 → NextRoundEnergyGainPenalty +3`，Order 全程连续。
- 同次重洗只包含解算前已在 DiscardPile 的牌。当前 3260 在抽牌冻结后才放入最终 DiscardPile，不参与同次重洗，因而不会自抽。
- 每张成功施放累计 `NextRoundEnergyGainPenalty +3`。下一玩家回合开始沿用 V2J 的 `max(0, baseGain + bonus - penalty)`，再按 EnergyMaximum 裁剪，之后清除 Bonus/Penalty。
- 3260 不是 Attack/Shoot，不消耗 Ammo 或 Stim，不触发 IncendiaryAmmo、PortableHelper 或任何伤害。

升级“+2 能量”仍只是作者表元数据，没有对应的升级 `CardInstance`。本切片没有把 3260 加入默认 Deck、奖励或 Run，未修改 UI、多人、Scene 或 Prefab。

## 2. 已变更表面

| 层 | 变更 |
|---|---|
| 作者表/生成配置 | `battle.card.xlsx` 只将 Q150（3260）的 `implementation_status` 翻为 `Implemented`；目录为 68/14，V1 为 55/9、V2 为 13/5。 |
| Program 60 | 注册 `GainEnergy(1) → DrawToHandLimitAfterPlayedCardDeparture(10) → NextRoundEnergyGainPenalty +3`；保持 Self / DiscardPile，不带 Attack/Shoot 标签。 |
| 卡区深事务 | `BattleCardZonesData` 提供 Prepare / Validate / Commit，把离手后容量、旧弃牌重洗、洗牌随机与最终布局封装为一个冻结计划。 |
| 出牌提交 | 在首次对外写入前准备并校验 CardZones 计划；获能先写本地 `playerTurnAfter`，复合卡区提交已包含当前牌归宿，普通结尾不重复移牌。 |
| 现有边界 | 普通 `DrawCards` 和 `BattleCommandQueue.Submit` 不变；卡区归属者仍是唯一布局/洗牌随机写入者。 |
| 目录门禁 | 仅 V1 身份 3260 新增为 `Implemented`；其余 14 张保持 `CatalogOnly`。 |

## 3. 已加入的定向回归

| 用例 | 锁定事实 |
|---|---|
| `PlayedCardDepartureAndDrawToHandLimit_UsesPostDepartureCapacityWithoutTransientOverflow` | Prepare 零写；单卡 Hand 以离手后缺口抽至 10，满 Hand 出牌后只回填一个位置；当前卡在 DiscardPile，仅发布一次最终 Layout，不观测到超限 Hand。 |
| `PlayedCardDepartureAndDrawToHandLimit_ReshufflesOldDiscardButExcludesResolvingCard` | 牌源不足时只重洗旧 DiscardPile；重洗快照排除正在解算的卡，当前卡最终独留 DiscardPile，洗牌随机按真实重洗推进。 |
| `PlayedCardDepartureAndDrawToHandLimit_RejectsPreparedStateDriftWithoutCommitWrites` | Prepare 后布局或洗牌随机快照漂移时 Validate 返回 false、Commit 拒绝，拒绝本身不发布布局也不推进随机。 |
| `LimitOverload_GainsEnergyDrawsToTenAfterDepartureAndSchedulesPenalty` | 0 费 settlement、实际 Gain 1、当前牌离手、Hand=10、Penalty +3 与连续 Order 被同一场景锁定。 |
| `LimitOverload_AtEnergyAndHandMaximumAvoidsFakeGainAndRefillsDepartureSlot` | 满能量不生成获能记录；起始 Hand=10 时当前卡离手后精确回填一张，最终仍为 10。 |
| `LimitOverload_StacksPenaltyThenNextRoundFloorsEnergyGainAtZeroAndClears` | 两张 3260 叠至 Penalty 6；下回合有效补给下限为 0，不伪造回填记录，且同次回合开始清除状态。 |
| `LimitOverload_InvalidTargetOrMissingHandCardWritesNothing` | Self 卡携带显式敌方目标或卡不在 Hand 时，资源、Penalty、卡区、卡牌/洗牌随机与表现结果零写入。 |
| `LimitOverload_DoesNotTriggerShootStimIncendiaryOrPortableHelper` | 3260 不造成伤害、不消耗 Ammo，不消耗或触发 Stim、IncendiaryAmmo 与 PortableHelper，卡牌随机不变。 |
| V1 快照与构建门禁 | 3260 的 Rare/0E/Self/Discard/Program 60/Implemented 元数据与 68/14、V1 55/9、V2 13/5 计数被精确冻结，降级或越界翻表会被验证器拒绝。 |

## 4. 数据、同步与 Unity 证据

| 项目 | 结果 | 说明 |
|---|---|---|
| Luban 与生成配置 | 通过 | 只开放 Q150（3260）；82 模板为 68 `Implemented` / 14 `CatalogOnly`，V1 为 55/9，V2 为 13/5。 |
| 本地化 | 通过 | 从 Excel 导入战斗卡本地化并执行校验成功。 |
| Sync 与本地 Addressables | 通过 | `Sync and Build All` 完成，Addressables 构建耗时 15.828 秒。 |
| Unity 正式定向 EditMode | 通过 | MCP 任务 `feda36c5daef4fffab34065ba5988686`：169/169 passed，0 failed/skipped，耗时 2.2836982 秒；覆盖 CardZones 深事务、Starter 运行时、V1/V2 目录快照与构建验证。 |
| 完整 EditMode | 通过 | MCP 任务 `a84b5bb4f7dd4ca1b9791c81bb930973`：659/659 passed，0 failed/skipped，耗时 282.0044831 秒；CardArt 与 Character Prefab 的 Addressables 冷加载较慢，但均通过。 |

3260 的程序、卡区事务、作者表、生成、本地化、同步、正式定向与完整 EditMode 均已通过；本切片按标准完整门禁收口。

## 5. 深事务结构证据

- CardZones 计划持有创建者、原 `Layout` 引用、洗牌随机前/后状态、最终 `CardZoneLayoutData`、全部 settlement 与一次性提交状态。Prepare 仅操作局部列表和候选 `GameRandom`，不写入聚合。
- 准备时先从局部 Hand 移除当前卡，然后只消费局部 DrawPile 和初始 DiscardPile。离手 settlement 位于列表首项，但当前卡要在抽牌计算后才加入最终目标堆，同时满足顺序可观察与自抽排除。
- Validate 只读检查 Owner、`IsCommitted`、原 Layout 引用与洗牌随机前状态。Commit 在校验后标记一次性，写入冻结的随机后状态和最终 Layout；提交阶段不会再洗牌。
- 职业运行时只声明 `DrawToHandLimitAfterPlayedCardDeparture`，不读写卡堆细节。它在首次对外写入前预演/校验计划，并在复合提交后跳过普通的出牌归宿移动，因而不会重复弃牌。
- 该 seam 没有修改普通 `DrawCards`，也没有增加 Queue 外的卡区或随机写入路径。

## 6. 验收后边界

- 本切片只实现 3260 基础态；升级“+2 能量”仍只是作者表元数据。
- 没有修改默认 Deck、奖励、Run、UI、多人、Scene 或 Prefab。
- 没有实现生命恢复、临时卡、手牌选择、自动免费攻击、AnyAlly 或其他跨卡协议。
- 其余 14 张 `CatalogOnly` 继续由精确目录门禁拒绝。
- 本切片未创建提交，也不记录或杜撰提交 SHA。
