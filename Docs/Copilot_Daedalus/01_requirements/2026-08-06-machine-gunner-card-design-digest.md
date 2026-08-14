---
title: 机枪兵卡牌设计需求摘要
page_type: requirement
lifecycle: superseded
created: 2026-08-06
updated: 2026-08-06
status: superseded-by-2026-08-07-marine-game-card-requirement-digest
status_source: ../SESSION_LOG.md
source: ../00_inbox/卡牌设计-机枪兵.json
superseded_by: 2026-08-07-marine-game-card-requirement-digest.md
confidence: mixed-source-stated-and-code-observed
related_plan: ../plans/2026-08-06-machine-gunner-card-pool-integration.md
---

# 机枪兵卡牌设计需求摘要

> 日常实施先读本文与关联计划；需要精确字段时再读取 source-only 的 [`../00_inbox/卡牌设计-机枪兵.json`](../00_inbox/卡牌设计-机枪兵.json)。本文不把对话设计稿伪装成已生效的表格或运行时事实。

## 1. MG0 结论与实施边界

- **MG0 已完成**：已把设计稿的 5 个初始牌模板、12 张初始实例、23 张奖励模板、7 类状态和升级/奖励缺口归一化为下文矩阵。
- **机枪兵定位（inferred，待确认）**：新增 Hero，不替换 Hero `1001`。现有默认战士继续保持 30 HP、每回合 3 能量、补至 5 手牌和其既有 Deck；本摘要不授权改变这些 M10 黄金基线。
- **已完成切片**：MG1「每 Hero 资源档案 + 每玩家只读资源事实」已在第 5 节的 R1--R2 确认后完成；下一步 MG2 必须另行授权，不能因 MG1 完成自动开始。
- **不在本阶段实施**：卡牌、状态、Power、奖励、Run、角色选择、Scene/Prefab、插画或本地化内容。策划 JSON 不会被运行时直接解析。

## 2. 当前项目基线（code-observed）

| 领域 | 当前事实 | 对机枪兵的含义 |
|---|---|---|
| Hero 静态表 | `battle.hero.xlsx` 只有 `max_health`、`base_strength`、`initial_deck_id`、`view_prefab_key` | 没有每 Hero 的能量/弹药档案；MG1 需扩展静态模板而非改全局默认值 |
| Deck 与目录门禁 | 正式 Deck 不能引用 `CatalogOnly` 卡 | 五张 starter 全部真实可玩前，不能接入机枪兵 Deck |
| 回合资源 | 当前每轮把玩家能量重置为全局 3 | 不支持能量上限、跨回合保留、弹药或角色差异 |
| 抽牌 | 当前规则是补至初始手牌数 5，不是每回合额外抽 5 | 用户确认默认抽牌数为 5；复用当前“补至 5”基线，不新增职业专属抽牌字段 |
| 出牌/目标 | 一张 `PlayCardCommand` 只有一个可选 `TargetId`；规则实际仅支持 Self / Enemy | 最近、第二近、全体、随机和重复必须成为程序步骤选择器，不能伪造为输入目标 |
| 效果与参与者状态 | 只有 Health、Strength、Block、Vulnerable；效果为 Strength/伤害/Block/Vulnerable | Weakness、Burn、Oil、Smoke、Armor、Stim、Ammo 均无现成映射 |
| 伤害公式 | Strength → Vulnerable ×1.5 → Block → HP | `weakness` 不是 `Vulnerable/易伤`，不能复用 `ApplyVulnerable` |
| 卡区与 Power | 只有 Draw/Hand/Discard/Exhaust；Power 归宿目前预写入失败 | Power 的唯一归属、叠层和清理需要 MG6 设计 |
| 随机 | 洗牌和敌人意图各有独立 `GameRandom` | 随机射击必须有新的卡牌执行随机域，不能复用已有流 |
| 权威写入口 | 所有共享战斗写入经 `BattleCommandQueue.Submit` 排序 | MG1 以后不能绕过 Queue、Turn、CardZones 或用 UI 保存资源副本 |

### 2.1 静态配置与构建硬门禁（code-observed）

| 位置 | 当前能力/门禁 | 对机枪兵的约束 |
|---|---|---|
| `battle.card.xlsx` | 已有类型、稀有度、Fixed/X、目标、归宿、升级展示元数据、实现状态、`effect_bindings`、插画短键 | 可承载目录身份；但不直接表达资源、步骤选择器、触发器、条件或状态时机 |
| `battle.CardEffectBinding` / `battle.card_effect.xlsx` | 仅为 `argument_key → effect_id`，Effect 仅有类型、属性、整数值 | 不能靠继续堆专用 EffectType/卡牌字段表达 28 张卡；MG2 需要通用卡牌程序/步骤数据并兼容旧绑定 |
| `CardInstanceData` | 只有实例 ID 与模板 ID | 升级不是现有实例事实；Ironclad I9/G4 后才能接入升级实例 |
| `BattleCardCatalogBuildValidator` | Deck 禁止引用 `CatalogOnly`；`Implemented` 需要有效程序/效果绑定与插画键；`CatalogOnly` 强制项目 `art_placeholder` | 新卡只能按能力分批翻转；不能用空效果/伪造素材绕过门禁 |
| 本地化门禁 | 每张卡需要 en/zh-CN name/description/upgraded description；动态 Smart String 参数与绑定一致 | 源内中文简述不是最终本地化交付；现有关键词仅覆盖 Strength/Vulnerable |
| Hero 资源/素材门禁 | `view_prefab_key` 必须是短键并解析到合约正确的角色 Prefab | 功能接入可复用 `pfb_char_player`，但不能把它宣称为机枪兵正式视觉 |

项目级 [`../Hermes_Pegasus/design/decision-locks.md`](../../Hermes_Pegasus/design/decision-locks.md) 的 P-004 是当前暂定结论：“基础抽牌数固定，卡牌/遗物以后才可改变”。因此在该结论被策划显式修订前，设计稿内的 `draw_per_turn: 5` 只可映射为项目的固定基础数值，不可成为机枪兵专属资源字段。若要改为职业差异，须先按 [`../Hermes_Pegasus/design/project-definition.md`](../../Hermes_Pegasus/design/project-definition.md) 的决策修订规则更新上游玩法文档。

## 3. 设计稿的确定内容（source-stated）

### 3.1 Hero 与全局战斗规则

| 设计项 | 原始值 | 状态 |
|---|---|---|
| 生命 | `hp: 70` | 可作为新增 Hero 的静态数值，仍需稳定表 ID 与 i18n key |
| 能量 | 上限 5；每回合 +3；跨回合保留 | 首回合值、上限变更后的当前值、时机未定义 |
| 弹药 | 上限 5；首回合满；每回合 +1；跨回合保留 | 需要新的通用资源事实 |
| 格挡 | 玩家回合开始清空 | 与 Armor 生成 Block 的顺序未定义 |
| 抽牌 | `draw_per_turn: 5` | 映射为项目固定的“补至 5”基线；不作为 Hero 资源字段 |
| AOE | `independent_per_enemy` | 目标快照、稳定顺序与死亡跳过未定义 |
| Power | `battle_only` | 战斗结束清理方向明确；Power 区与叠层规则未定义 |
| 攻击管线 | `weakness_percent → smoke_flat → block → hp` | 与 Strength/Vulnerable 的合并顺序、逐步取整和最小伤害未定义 |

### 3.2 状态语义

| 状态 | 设计稿语义 | 当前映射 | 仍需冻结 |
|---|---|---|---|
| Strength | 敌人攻击伤害增加；可被击退射击削减 | 已有 `Strength` | 与新增攻击管线的相对位置 |
| Weakness | 双方；攻击伤害 -25%，再结算 Block；每回合 -1；叠层延长持续回合 | **无**；不可映射 `Vulnerable` | 衰减时机、取整、与 Strength/Vulnerable/Smoke 的顺序 |
| Burn | 双方；玩家回合结束时对携带者造成层数伤害；不衰减 | **无** | 双方结算顺序、是否绕过 Block、死亡中止 |
| Oil | 敌人；施加 Burn 时额外加同层 Burn，随后 Oil 减半；额外 Burn 不二次触发 | **无** | 快照、同时施加、取整与死亡规则 |
| Smoke | 双方；造成/受到攻击伤害每层 -1；Debuff/主动失血不减免；玩家回合开始清空，Power 后改为每回合 -1 | **无** | 双方 Smoke 的组合、0 下限、敌方时机 |
| Armor | 玩家；回合开始得等值 Block；叠层相加；破防攻击每段 -1；战斗结束重置 | **无** | “破防”精确定义、与清 Block 的顺序 |
| Stim | 玩家；持续回合可叠加延长；射击牌额外一发且额外耗 1 Ammo，不足则不打 | **无** | 多段/随机/X 射击的触发次数和顺序 |

### 3.3 卡牌模板矩阵

“入口”只说明首次需要的通用能力；不是将卡翻为 `Implemented` 的授权。所有模板还缺稳定 int ID、大小写精确的 `external_key`、i18n key、插画短键和当前实现状态。

| 源 ID | 名称 | 类型 / 费用 | 源效果摘要 | 首次能力入口 |
|---|---|---|---|---|
| `shoot` ×4 | 射击 | Attack / 0 | 耗 1 Ammo；单体 6；标记为 shoot | MG1、MG2 |
| `elbow` ×1 | 肘击 | Attack / 1 | 最近敌人 6 | MG3 |
| `block` ×5 | 格挡 | Skill / 1 | 得 6 Block | MG2 |
| `reload` ×1 | 换弹 | Skill / 1 | Ammo 补满 | MG1、MG2 |
| `stim` ×1 | 兴奋剂 | Skill / 1 | 抽 2；获得 1 回合 Stim | MG5、MG6 |
| `core_expansion` | 核心扩容 | Power / 1 | Energy 上限 +1；升级 +2 | MG1、MG6 |
| `output_adjust` | 出力调整 | Power / 1 | 每回合 Energy +1、上限 -1；升级费用 0 | MG1、MG6 |
| `blast_shield` | 防爆护盾 | Power / 1 | 得 6 Armor | MG4、MG6 |
| `mag_expansion` | 扩容弹夹 | Power / 1 | Ammo 上限 +3；升级 +5 | MG1、MG6 |
| `incendiary_ammo` | 燃烧弹药 | Power / 1 | 每段 shoot 伤害施加 1 Burn 并触发 Oil | MG4、MG6 |
| `smoke_persist` | 烟雾弥漫 | Power / 1 | Smoke 不清零，改为每回合 -1 | MG4、MG6 |
| `kungfu_mech` | 功夫机甲 | Power / 1 | 每次非 shoot Attack 得 4 Block | MG6 |
| `overload` | 过载供能 | Skill / 0 | 立即 +2 Energy；下回合 -1 | MG1、MG5 |
| `tumble_reload` | 翻滚换弹 | Skill / 2 | 得 10 Block；Ammo 补满 | MG1、MG2 |
| `stun_grenade` | 震荡弹 | Skill / 1 | 全体 8 伤害；各 +1 Weakness | MG3、MG4 |
| `retreat` | 撤退 | Skill / 2 | 得 15 Block；下回合 Ammo 补满；结束行动 | MG1、MG5 |
| `gas_pump` | 汽油弹 | Skill / 1 | 全体 +5 Oil；升级 +7 | MG3、MG4 |
| `napalm` | 凝固汽油弹 | Skill / 2 | 全体同时 +3 Burn、+5 Oil；本次 Oil 不触发 | MG3、MG4 |
| `molotov` | 燃烧瓶 | Skill / 1 | 单体 +5 Burn | MG4 |
| `hold_line` | 坚守 | Skill / X | 每 X 次得 5 Block 和 1 Ammo；X=0 无事发生 | MG1、MG5 |
| `smoke_bomb` | 烟雾弹 | Skill / 2 | 得 10 Block；全体敌人和自己各 +3 Smoke | MG3、MG4 |
| `incomplete_combustion` | 不充分爆燃 | Skill / 3，Exhaust | 自然语言：每个 Burn 敌人对全体（含自身）造成其 Burn 值伤害，再 Burn 1:1 转 Smoke，不触发 Oil | MG0 先结构化，后 MG3、MG4 |
| `knockback_shot` | 击退射击 | Attack / 0 | 耗 1 Ammo；最近 7、第二近 3；两者各 -2 Strength | MG1、MG3 |
| `spray` | 扫射 | Attack / 0 | 耗 2 Ammo；随机命中 2 次、每次 7；升级 3 次 | MG1、MG3 |
| `bayonet_parry` | 刺刀招架 | Attack / 1 | 最近 7；得 7 Block | MG3 |
| `wild_rampage` | 猛烈发狂 | Attack / X | 射出全部 Ammo 的随机 7 伤；另作 X 次免费射击；零资源可打；Stim 时一发免费 Stim 射击 | MG1、MG3、MG5、MG6 |
| `quick_elbow` | 快速肘击 | Attack / 0 | 最近敌人 6 | MG3 |
| `hurricane_elbow` | 疾风肘击 | Attack / X | X 次随机 7 伤；X=0 无事发生 | MG3、MG5 |

### 3.4 升级、奖励与素材缺口

| 领域 | 已给出的设计信息 | 未给出的信息 |
|---|---|---|
| 升级 | 5 张奖励卡明示升级：`core_expansion`、`output_adjust`、`mag_expansion`、`spray`、`gas_pump` | 18 张奖励卡与全部 starter 是“无升级”还是遗漏；升级实例如何得到属于 G4 |
| 奖励池 | starter 排除；23 个 `pool_ids` | 生成时机、稀有度权重、候选数量、重复、跳过、保存、角色筛选 |
| 文本 | 中文卡名与简述 | 项目自有 en/zh-CN name/description/keyword key；动态值模板 |
| 素材 | 无 | 28 张项目自有插画短键、授权来源、导入契约和 Addressables 交付状态 |

## 4. 已验证的矩阵完整性

- 原始设计稿总计 **28** 个模板：5 个 starter + 23 个 reward；starter 实例数为 **12**。
- 上表覆盖全部 28 个源 ID；源中明示 **5** 个奖励升级、**18** 个奖励升级未声明。
- 原始文件保持在 `00_inbox/`，未被改写；本文只有摘要、分类和未决项。

## 5. MG1 已确认规则与验收结果

以下两项已由用户确认，并已在不接任何机枪兵卡、不改变 Hero `1001` 和不改 UI/Prefab 的范围内实施 MG1。基础抽牌数及“补至 5”语义继续固定为共享规则；其余状态/目标/Power 问题留在后续切片确认。

| 编号 | 需要确认的规则 | 可选口径 | 当前推荐（inferred，不是已定规则） |
|---|---|---|---|
| R1 | 机枪兵首回合 Energy | 0 / 3 / 满 5 / 其他 | **已确认：3**；首回合只应用 `initial_energy`，不额外叠加每回合 +3 |
| R2 | 上限变动后的当前资源 | 保留超上限 / 立即裁剪到新上限 / 仅下一次变化裁剪 | **已确认：立即裁剪**为 `min(current, max)`，保证权威事实始终合法 |

MG1 的实际验收已经证明：

1. 新 Hero 的 Energy/Ammo 初值、上限、跨回合保留与回合补充只由权威回合路径写入。
2. Hero `1001` 的现有 3 Energy / 补至 5 行为与 M10 黄金测试不变。
3. 任一资源档案无效或任一步预构建失败时，Energy、Ammo、CardZones、参与者与随机流均零写入。
4. 不新增第二份资源状态，不把资源写进 UI，不接入机枪兵卡、Power、状态或正式 Deck。

MG1 保持当前基础回合顺序：清 Block → 补 Energy/Ammo → 补至 5。Hero `1001` 的静态档案为 Energy `3/3/+3`、Ammo `0/0/+0`；未来机枪兵可使用 `3/5/+3`、`5/5/+1` 的同一档案形状，但本切片没有新增该 Hero、Deck 或卡牌。Power/状态的正式插入时机不在 MG1 猜测，留到 MG4/MG6。验证结果见 [`../06_testing/2026-08-06-machine-gunner-mg1-hero-resource-profile.md`](../06_testing/2026-08-06-machine-gunner-mg1-hero-resource-profile.md)。

## 6. 后续待确认队列（不阻塞 MG1）

- MG3：最近/第二近/随机/全体的 Encounter 顺序、是否可重复命中、目标死亡后的段数处理。
- MG4：Strength、Weakness、Smoke、Vulnerable、Block、HP 的完整顺序与取整；Burn/Oil/Armor 的触发、快照、死亡与清理时机。
- MG5：X 的冻结与支付、Overload/Retreat 的延迟效果、Stim 抽牌与免费段。
- MG6：Power 的唯一归属、叠层顺序、战斗结束清理和命令内触发记录。
- MG7：各卡 `ImplementationStatus` 翻转、稳定 ID、i18n/插画短键及真实 Game View 验收。
- G4：升级获得、奖励候选、选择/跳过、Run 持久化。
