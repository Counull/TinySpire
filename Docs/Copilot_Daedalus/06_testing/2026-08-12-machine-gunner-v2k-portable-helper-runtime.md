---
title: Marine Game 机枪兵 V2K 便携帮手基础态
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
related_decision: ../CODE_DECISIONS.md#cd-090v2k-便携帮手作为即时射击段后的受限同目标伤害而非通用命中事件
---

# Marine Game 机枪兵 V2K 便携帮手基础态

## 1. 验收对象与冻结行为

本切片只开放 `PortableHelper` (3267) 基础态：

- 1 Energy、Power、Self、Hand→PowerPile；每次成功施放增加一层整场不衰减的帮手，多层共存。
- 每一段卡牌即时 `IsShootCategory` 实际伤害完成来源 Damage 与所有既有命中后/全局钩子后，若原目标仍存活，每层帮手依序向同一目标各追加一次基础 1 点伤害。
- 来源伤害致死时不触发帮手；某个帮手致死后停止剩余帮手。帮手不重定向、不递归。
- 帮手伤害只读取来源 FirePower、目标 Vulnerable 与 ArmorBreak，并经 Block/HP；忽略 Strength、Weakness、双方 Smoke、目标 Invisible 与狙击倍率。
- 帮手段没有卡牌标签，因此不触发 Stim、IncendiaryAmmo、AgedOil、KungfuMech、Ammo、Invisible 生命周期或再次帮手。

本切片没有实现升级 `CardInstance`，也没有把卡加入默认 Deck、奖励或 Run；未修改 UI、多人、Scene 或 Prefab。

## 2. 已变更表面

| 层 | 变更 |
|---|---|
| 作者表/生成配置 | `battle.card.xlsx` 只将 3267 的 `implementation_status` 翻为 `Implemented`；目录为 65/17，V1 为 54/10、V2 为 11/7。 |
| Power 程序 | 3267 注册为 Self / PowerPile，并复用既有 Power 层数与整场生命周期。 |
| 即时命中钩子 | 来源即时射击段的 post-hit/global hooks 完成后，按帮手层数追加独立同目标伤害；每段沿用同一局部投影与原出牌事务。 |
| 伤害档案 | 新增帮手专用 DamageKind，集中冻结只读 FirePower、Vulnerable、ArmorBreak 与 Block/HP 的公式。 |
| 目录门禁 | 仅 V2 扩展身份 3267 新增为 `Implemented`；其余 17 张保持 `CatalogOnly`。 |

## 3. 已加入的定向回归

| 用例 | 锁定事实 |
|---|---|
| `PortableHelper_StacksInPowerPileAndEachStackFollowsShootDamage` | 两次施放进入 PowerPile 并叠为两层；一次 Shoot 依序产生来源伤害与两个同目标帮手段。 |
| `PortableHelper_PersistsAcrossFullRoundAndTriggersOnNextRoundShoot` | 帮手跨完整玩家/敌人轮次保留，并在下一轮射击继续触发。 |
| `PortableHelper_TwoStacksFollowEachStimShootSegmentWithoutRecursion` | Stim 的两个来源射击段分别触发全部帮手，序列按每个来源段后紧跟帮手，帮手自身不递归。 |
| `PortableHelper_UsesShootCategoryAndExcludesMarkAndElbow` | 纯 Sniper 与 Shoot\|Sniper 触发，`Tags.None` 的 Mark 和肘击不触发。 |
| `PortableHelper_DamageReadsFirePowerVulnerableArmorBreakButIgnoresAttackModifiers` | 帮手读取 FirePower/Vulnerable/ArmorBreak，忽略 Strength/Weakness/双方 Smoke/Invisible，并正常经过 Block。 |
| `PortableHelper_StopsWhenSourceOrEarlierHelperKillsTarget` | 来源致死零帮手；较早帮手致死后不再生成剩余层数。 |
| `PortableHelper_IncendiaryAppliesOnceFromSourceBeforeHelperDamage` | 燃烧弹药只由来源射击施加一次，结算顺序为来源 Damage→Burn→帮手 Damage。 |
| `PortableHelper_FailedCostOrExplicitTargetWritesNothing` | 能量不足或 Self 卡携带显式目标时，资源、Power、卡区、随机流与表现结果零写入。 |

## 4. 数据、同步与 Unity 证据

| 项目 | 结果 | 说明 |
|---|---|---|
| Luban 与生成配置 | 通过 | 只开放 3267；82 模板为 65 `Implemented` / 17 `CatalogOnly`，V1 为 54/10，V2 为 11/7。 |
| 本地化 | 通过 | 从 Excel 导入战斗卡本地化成功。 |
| Sync 与本地 Addressables | 通过 | `Sync and Build All` 完成，Addressables 构建耗时 12.163 秒。 |
| 开发中 Starter 补强 | 通过 | MCP 任务 `f4c3fc07550d4237b029a112b1ce2563`：98/98 passed，0 failed。 |
| Unity 定向 EditMode | 通过 | MCP 任务 `95707f1918fa4633b671c6a10f9b0da3`：120/120 passed，0 failed，耗时 0.918363 秒。 |
| Unity 完整 EditMode | 通过 | MCP 任务 `8c0ce8f925e94a35b893f5b5892ef447`：639/639 passed，0 failed，耗时 131.4561842 秒。 |

## 5. 结构证据与跨模块边界

- `IsShootCategory` 包含 Shoot、Sniper 与 Shotgun；当前没有 Shotgun 卡实例，所以 Shotgun 触发只属于共用分类的结构证据，没有直接运行用例。
- 延迟 Support、Bomb、Needle 与 TripleStrike 延迟段不经过即时卡牌 `AppendPreparedHitAndPostHitOperations` 入口，因此结构上不触发帮手。本切片没有修改延迟调度器，也不把该排除伪报为跨模块运行验收。
- 本切片没有建立全局伤害事件；便携帮手钩子只服务即时卡牌射击段。狂轰滥炸与天空之怒仍需后续独立的支援触发协议。

## 6. 验收后边界

- 本切片只实现 3267 基础态；升级 0 费仍只是作者表元数据。
- 没有修改默认 Deck、奖励、Run、UI、多人、Scene 或 Prefab。
- 没有实现 3260、临时卡、支援 Power、手牌选择、自动免费攻击、AnyAlly 或其他跨卡协议。
- 其余 17 张 `CatalogOnly` 继续由精确目录门禁拒绝。
- 本切片未创建提交，也不记录或杜撰提交 SHA。
