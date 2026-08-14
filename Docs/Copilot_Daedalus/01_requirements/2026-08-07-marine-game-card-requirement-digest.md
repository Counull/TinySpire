---
title: Marine Game 机枪兵卡牌需求摘要
page_type: requirement
lifecycle: superseded
created: 2026-08-07
updated: 2026-08-12
status: superseded-by-v2-2026-08-12
status_source: ../SESSION_LOG.md
source:
  - ../00_inbox/marine-game.zip (marine-game/cards.json, battle.html, README.md)
  - ../00_inbox/卡牌设计-机枪兵.json (仅保留已确认的 R1/R2 与历史对照)
confidence: mixed-source-stated-code-behavior-and-meeting-decision
related_plan: ../plans/2026-08-07-marine-game-card-only-integration.md
supersedes: 2026-08-06-machine-gunner-card-design-digest.md
---

# Marine Game 机枪兵卡牌需求摘要

> 已由 [2026-08-12 Marine Game 机枪兵 V2 卡牌需求摘要](2026-08-12-machine-gunner-v2-requirement-digest.md) 取代。本页保留旧压缩包的 64 模板来源、历史验收和当时的裁决，不应与 V2 README 混用。

> 本页是日常实施的卡牌摘要。压缩包原文保持 source-only；不得把 `cards.json` 当作运行时解释器，也不得由本摘要推导地图、敌人或 Run 内容。

## 1. 本轮确认的范围

- 用户明确只实施**机枪兵卡牌与单场战斗内必要的卡牌规则**。
- 不实施压缩包内的地图、敌人、意图、奖励流程、篝火、事件、跨战斗生命/牌组、Run、场景、Prefab、角色选择或 UI 流程。
- 默认 Hero `1001` 和 M10 黄金基线保持不变；MG1 已冻结的首回合 Energy `3`、资源上限降低立即裁剪、共享“补至 5 手牌”继续有效。

## 2. 更新来源与采用顺序

| 层次 | 当前采用方式 | 用途 |
|---|---|---|
| 用户已确认口径 | 首回合 Energy 为 3、降低上限立即裁剪、默认补至 5 | 高于压缩包未声明或相冲突的资源细节 |
| `marine-game/cards.json` | 卡牌身份、类型、稀有度、费用、升级原始值、基础目标/归宿字段 | 静态目录录入的主要数据源 |
| `marine-game/battle.html` | 已实现的伤害顺序、卡牌触发和时机 | 后续运行时规则的行为参考，不能直接复制为 Unity 运行时代码 |
| `marine-game/README.md` | 玩法说明、词汇和验收语义 | 在不与前两项冲突时补充说明 |

`cards.json` 的 `damage_pipeline` 没有列出易伤；`battle.html` 与 README 都明确为“力量处理后：虚弱 → 攻受双方烟雾 → 易伤 → 格挡 → 生命”。这不是“虚弱等于易伤”：Weakness 是攻击者造成攻击伤害 -25%，Vulnerable 是目标承受攻击伤害 +50%。本次只做目录录入，不在这里擅自把该冲突写入运行时公式；伤害管线切片开始前须以行为参考和测试冻结取整、延迟伤害和死亡中止。

`00_inbox/HANDOFF.md` 中的 STS2 Mod 后续数值记录不属于上表的 `marine-game` 卡牌来源链。它与 `ExplosiveElbow` 的 2 Energy / 基础 10 存在 1 Energy / 8 的冲突时，当前实施继续采用摘要来源链；除非用户另行提升该 handoff 的优先级，不得以它覆写作者表或已验收运行时数值。

## 3. 新版卡牌目录事实

| 类别 | 数量 | 目录处理 |
|---|---:|---|
| 初始牌模板 | 5（12 张实例） | `shoot`×4、`elbow`×1、`block`×5、`reload`×1、`stim`×1；已在 Hero 1002 的初始牌组中 `Implemented` |
| 奖励卡 | 58 | 由 `reward_pool_rules.pool_ids` 精确列出；只录身份，不实现奖励选择 |
| 临时卡 | 1 | `machinegun_burst`，不在奖励池；先 CatalogOnly |
| 合计 | 64 | 一次性固定外部身份、双语 key、中文来源说明/升级说明、归宿和占位图 |

新版 `cards` 有 59 张：12 Power、27 Skill、20 Attack；其中 58 张属于奖励池，`machinegun_burst` 是固定机枪生成的临时卡。初始牌仍是独立 `starter_deck` 定义，不在 `cards` 列表和奖励池中。

相对旧 JSON 的重要数值变动包括：初始 `block` 由 6 改为 5、`wild_rampage` 每弹伤害由 7 改为 5、`mag_expansion` 由 Uncommon 改为 Rare、`retreat` 由 Common 改为 Uncommon、`gas_pump` 升级 Oil 由 7 改为 8；`napalm` 改为“先 Burn（触发已有 Oil），后施加本次 Oil”，`knockback_shot` 改为施加临时 `lose_strength`，不再永久减少 Strength。

## 4. 卡牌机制归类（除基础 5 张初始牌外，不代表已实现）

| 机制族 | 代表卡牌/字段 | 当前缺口 |
|---|---|---|
| 基础资源与单目标 | `shoot`、`block`、`reload`、`elbow` | Ammo 支付/补满、最近目标、原子出牌程序 |
| 自动、多目标与随机 | `nearest`、`furthest`、`random_hits`、AOE | 只读稳定排序、独立卡牌随机流、死亡跳过 |
| 攻击状态 | Weakness、Smoke、Vulnerable、Burn、Oil、Armor | 强类型状态事实、统一攻击/延迟伤害管线与时机 |
| 回合与资源修饰 | `stim`、X 费、draw、energy/ammo 上限/增量、结束行动 | 手上限 10、抽牌数量、资源投影、冻结 X 值 |
| 延迟与 Power | Bomb、Banshee、Support、Invisible、Guerrilla、Buffer、Decoy、Power | 唯一归属、稳定触发顺序、结算记录与清理 |
| 特殊选择 | retain、discard-for-energy、免费攻击链 | 命令内选择协议（现有 DEP-010），不能用 UI 直接改事实 |

压缩包中 `guerrilla`、`buffer`、`decoy` 被卡牌引用但没有在 `buffs` 字典中结构化声明；射击只以 `shoot` / `sniper` 布尔字段表示，也未给出统一分类字段。这些在实现前需归一化为强类型程序和状态，不能以卡牌 ID 或说明文字分支。

## 5. 目录录入的降级规则

当前 Card 表只有 `Self`、`Enemy`、`AllEnemies`、`RandomEnemy` 四种通用输入目标。目录切片会把 `nearest`、`furthest`、第二近等来源字段暂记为 `Enemy`，将全体/随机映射为现有对应枚举；它只表达**目录分类**，不代表这些卡在运行时已具有该目标语义。每张新卡均为 `CatalogOnly`、空 `effect_bindings`、复用 `art_placeholder`；任何一张都不能进入 Deck 或翻为 `Implemented`。

中文基础说明逐字保留 `cards.json` 的 `desc`，63 张可升级卡的中文升级说明保留 `known_upgrades.change`；这两项是目录追溯事实，不能被“尚未实现”的通用文案替换。源文件未提供英文规则文案，en 列以同一压缩包的结构化字段与行为说明为依据给出项目内英文翻译；它不替代中文来源，也不构成玩法已实现或规则裁决。

## 6. 完成定义与下一步

目录完成仅表示 64 张卡的稳定身份、来源快照、基础卡面元数据、中文来源规则、升级声明和构建门禁已落地；不会自动表示一张卡能抽到、打出、升级、产生奖励或加载最终插画。

MG2A 已完成作者表、Luban 输出、目录门禁、快照测试代码与同步构建。MG2B 已单独把首批 5 个初始牌接入 Hero 1002 的会话私有程序：射击、肘击、防御、装填和兴奋剂可经 Queue 执行；其余 59 张仍为 `CatalogOnly`。下一步从 MG3 的稳定目标/随机流和 MG4 的状态/伤害链开始，不把尚未有权威待决协议的保留/弃牌选择伪造成 UI 私有状态。

下一条生产切片是单一 Card Program 的预构建/原子提交能力。它必须先能在首次写入前校验整张卡的资源、目标、卡区、全部操作和卡牌随机流，失败时保持零写入；随后才按机制族逐批把 starter 翻为 `Implemented` 并接入新 Deck。

## 当前运行时状态（2026-08-12）

本页中的目录登记描述仍适用于全部 64 张卡；当前可执行状态以此节为准：Hero 1002 已有 45 张 `Implemented`（3201--3209、3210--3212、3214--3216、3217--3222、3224--3230、3232--3233、3235--3236、3242、3245、3247--3249、3251--3256、3258--3259），其余 19 张保持 `CatalogOnly`。除 X 费、变动弹药和多段随机攻击外，3215/3228 的伤害后 Weakness、3221 的自身/全体 Smoke、3229 的伤害后 Vulnerable，以及 3247 的最远狙击（不接收 Stim/开火，但接收燃烧弹药）均已通过会话私有运行时和既有 Queue 结算；3206--3209、3211、3212、3245 的资源、护甲、烟雾、功夫机甲和额外抽牌 Power 规则也已具备完整程序与回合接线。

Burn/Oil 已在最后一名存活玩家结束行动后、敌人行动前结算一次：先按 Encounter 顺序对存活敌人自燃，再结算机枪兵玩家；Burn 为不衰减的 Debuff 伤害，可被 Block 吸收但不读取攻击修正或 Armor。敌人全灭时跳过玩家自燃并立即 Victory，玩家自燃致死时立即 Defeat。GasPump、Napalm、Molotov 与 FlameElbow 已据此开放：燃烧只消费施加前的 Oil 并将其向下减半，Napalm 固定先 Burn 后 Oil，FlameElbow 的致命伤害不会再施加 Burn。当前只支持卡牌基础值；所有升级数值仍等待通用 CardInstance 升级状态，不能将数据表中的升级字段硬编码为运行时升级。

KungfuMech 已按成功非射击攻击整卡结束触发 `4 × Power 层数` Block；ElectroBoost 的 `FirePower +2` 可叠层、只作用于常规射击段并在玩家行动结束清零；ComboElbow 在当前玩家回合紧邻上一张成功牌为非射击攻击时免费，且成本规则读取与队首提交共用同一个只读预览。压缩包可执行原型虽然把狙击列为射击，却没有让狙击读取 `firePower`，本切片依该更具体行为冻结为“狙击不吃开火”。

MG9 已把攻击型程序的逐段后置状态固定为“伤害 → 程序后置状态 → 全局命中钩子”：`SpikeShot` 每段依次造成 1 点伤害、施加 Weakness +1、Vulnerable +1，再由 `IncendiaryAmmo` 施加 Burn；Stim 的额外命中重复整段序列，第一段易伤会参与第二段伤害。`IncendiaryAmmo` 可叠层，并对全部 `IsShoot` 命中（含狙击）在目标仍存活时施加层数 × 1 Burn；`AgedOil` 则仅对非射击攻击的每一段存活命中固定施加 Oil +2，多个副本不放大数值。

`BurningOil` (3254) 已作为独立 Power 接入：在回合末任一 Burn Debuff 伤害前，若持有至少一张，就按 Encounter 顺序仅对存活且已有 Burn 的敌人执行 `Burn += 1 + Oil`。Oil 不减半、不消耗，玩家自身不增长，且多张副本只启用一次固定增长；所有增长状态记录先于任何 Burn 伤害，之后仍复用现有的格挡、死亡与胜负中断。`IncompleteCombustion` (3222) 已以专用操作接入：开始时冻结带 Burn 的存活敌人为来源，每个来源再对当时存活目标造成其冻结 Burn 值的 Debuff 伤害，之后才对存活敌人执行 `Smoke += Burn`、`Burn = 0`；死亡来源仍按快照出伤，Oil 不变，卡牌支付后 Exhaust。

`ExplosiveElbow` (3252) 自动攻击最近存活敌人 10 点；若普通攻击后目标仍存活，则在卡牌后置状态、燃烧弹药和陈年机油完成后，立即按该时刻的 Burn 值追加一次 Debuff。立即触发不消耗 Burn 或 Oil，Debuff 不读取 Weakness、Smoke、Vulnerable 或 Armor，只可由 Block/HP 结算；普通攻击致死会跳过该段。表内 `Enemy` 仅保留目录输入分类，运行时无显式目标输入；本卡 2 Energy / 10 采用当前 `marine-game` 来源链，不采用未列为本摘要来源的 STS2 Mod handoff 数值。

`OpticalCamo` (3249) 以 2 Energy、Self、Hand→DiscardPile 施加 `Invisible +2`；升级费用 1 仅保留作者表元数据。既有隐身受击减半继续由职业伤害管线处理；玩家行动结束减少 1 层，普通攻击仅在整张卡成功进入归宿后减少 1 层，失败攻击不消耗。3247 与 3248 显式保留隐身，且 3248 的“不破隐”独立于其现有开火加成，未将双词条伤害语义混入 `IsSniper`。

`HoloDecoy` (3259) 以 1 Energy、Self、Hand→ExhaustPile 施加 `Buffer +1`。Buffer 可叠层、无回合衰减，只使下一次正值 incoming Attack 完全不改变 Block/HP，随后减少一层；零值攻击不消费，且完全抵挡不会消耗 Armor。Effect 链在局部投影内预留 Buffer，提交顺序固定为 Damage 后再写 Buffer 状态，以保持多段同链的单次消费与全局连续 Order。来源对升级归宿存在冲突：`README web.md` 写升级后不消耗，但作者表基础/升级均为 ExhaustPile；当前没有升级 CardInstance，故保留作者表字段、仅实现基础态。

`GuerrillaTactics` (3251) 以 1 Energy、Self、Hand→Power 进入 PowerPile；每张基础态能力牌增加 2 层游击，升级 3 层仍只保留为元数据。运行时将实际扣除并生成 Ammo settlement 的 `AmmoSpent` 与游击触发用的冻结名义值 `AmmoSpentForGuerrilla` 分开保存；当前常规支付令两者相等，成功出牌后在原卡操作及既有功夫机甲钩子之后，按“游击层数 × 名义弹耗”追加 Block。能力牌自身不耗弹，因而不立即给 Block；Stim 射击实际/名义耗 2 弹时，基础 2 层游击给 4 Block。该字段只为后续免费攻击/虚拟弹耗留接口，本轮未由此实现 TacticalAdvance、固定机枪或临时 MachinegunBurst。

`Retreat` (3216) 以 2 Energy、Self、Hand→DiscardPile 获得 15 Block，预约下一玩家回合开始时把 Ammo 补至当前最大值，并结束本次玩家行动。结束行动由 Queue 的系统 continuation 签发；它在成功归宿后才冻结，普通重入 Play/End 会被同 actor 的强制结束锁零写入拒绝。`QuickRoll` (3235) 以 1 Energy、Self、Hand→DiscardPile 立即获得 5 Block 并叠加 5 层下挡；下一玩家回合开始先清除旧 Block，再把下挡总值转为 Block 并清空。两种延迟状态随后依次经过普通资源补充、预约补满弹和既有抽牌，故撤退的 Ammo 先按普通档案 +1、再填至上限。当前 CardInstance 没有升级态，3216 的 20 Block 和 3235 的 8/8 只保留为作者表元数据。

`TacticalAdvance` (3234) 仍保持 `CatalogOnly`。其“下一张攻击免费”虽然要求不消耗基础 Energy/Ammo，但需求未定义它与 Stim 额外射击的弹药支付优先级，且 Bound 前置规则没有相应运行时状态；不得据此猜测免费后 Stim 的行为或伪造束缚限制。

已实现集合不代表奖励/Run、Power UI、Invisible HUD/透明表现、升级实例或动态插画加载完成。3222、3252、3249、3259、3216、3235 与 3251 的代码、作者表、Luban 输出、静态编译、`Sync and Build All`、本地 Addressables 和原生 Unity EditMode 均已完成验收；最新完整任务 `d3968d32a61f4a8cb9bf9c3396b905b0` 为 574/574 passed。其余 19 张仍必须按独立机制切片处理；`TwelveHits`、选择、动态临时卡、自动连锁和超上限 Energy 不因本卡验收而提前开放。详细进度和证据见 `../06_testing/2026-08-12-machine-gunner-mg14b-guerrilla-tactics-runtime.md`。
