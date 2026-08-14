---
title: Marine Game 机枪兵 V2M 天空之怒基础态
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
related_decision: ../CODE_DECISIONS.md#cd-092v2m-天空之怒在四类原始-support-逻辑段后逐层随机结算
---

# Marine Game 机枪兵 V2M 天空之怒基础态

## 1. 验收对象与冻结行为

本切片只开放 `SkyWrath` (3266) 基础态：

- 1 Energy、Rare、Power、Self、Hand→PowerPile；每次成功施放增加 1 层整场不衰减的天空之怒 Power，多张卡线性叠加，卡本身不造成即时伤害也不推进随机流。
- 只在四类原始 Support 逻辑段完整结算后触发：`BansheeStrike` 每个 hit 一次、`FireSupport` 每个 hit 一次、`FireBombardment` 每个 wave 一次、`TripleStrike` 延迟 Support 一次。燃烧轰炸一波内全部目标的 `Damage → 存活后 Burn → Oil` 完成后，才进入该波的天空之怒。
- 每一层独立读取当时投影中的存活敌人，并调用一次随机流选择主目标；候选只有 1 名时也调用 `NextInt(1)`。主目标先承受基础 8 点 Support，随后该层开始时其余存活敌人按 Encounter 顺序各承受基础 4 点 Support。前一层结算完成后，下一层重新取得存活候选；若已没有存活敌人则停止且不再推进随机流。
- 天空之怒伤害沿用 Support 管线，读取目标 Smoke、Vulnerable 与 ArmorBreak。当前 Bombard 层数会先按既有 half-up 规则缩放 8/4，再进入 Support 管线；例如 Bombard 4 层时基础值为 11/6。
- 天空之怒自身不递归触发。`NeedleStorm` 的 Delayed、GuidedNuke / FiveHundredPounder 的 Bomb、回合末 Burn、即时 Attack/Shoot 与 PortableHelper 均不触发。

`README web.md` 把支援明确限定为女妖打击、火力支援、燃烧轰炸与三连击延迟段；因此旧 `HANDOFF.md` 中把钢针纳入触发的描述没有覆盖当前来源。本切片没有实现升级 `CardInstance`，也没有把卡加入默认 Deck、奖励或 Run；未修改 UI、多人、Scene 或 Prefab。

## 2. 已变更表面

| 层 | 变更 |
|---|---|
| 作者表/生成配置 | `battle.card.xlsx` 只将 Q156（3266）的 `implementation_status` 翻为 `Implemented`；目录为 67/15，V1 为 54/10、V2 为 13/5。 |
| Power 程序 | 3266 注册为 Self / PowerPile，每次成功施放增加 1 层；卡本身没有即时伤害、Block、私有状态或随机写入。 |
| 延迟支援钩子 | 在四类既有 scheduled Support 的原始逻辑段结束点追加受限钩子；写入仍属于同一延迟触发计划，没有新增全局伤害事件。 |
| 逐层随机伤害 | 每层独立重取存活候选并消费一次随机数，先主目标 8，再按 Encounter 顺序处理其余目标 4；伤害使用既有 Support 投影与操作。 |
| Bombard 组合 | 复用不绑定延迟种类的既有 Bombard 正值 half-up 换算，再走 Support 管线；没有改变原始支援载荷或 Bombard 白名单。 |
| 目录门禁 | 仅 V2 扩展身份 3266 新增为 `Implemented`；其余 15 张保持 `CatalogOnly`。 |

## 3. 已加入的定向回归

| 用例 | 锁定事实 |
|---|---|
| `SkyWrath_StacksOnePerPowerCardWithoutImmediateDamageOrRandomAdvance` | 每张成功施放增加 1 层并进入 PowerPile；两张叠至 2 层，卡本身没有伤害或随机推进。 |
| `SkyWrath_FailedCostOrExplicitTargetWritesNothing` | 能量不足或 Self 卡携带显式目标时，能量、Power、卡区、伤害、随机流与表现结果零写入。 |
| `SkyWrath_TriggersOnceAfterEveryDeclaredSupportSegment` | 女妖每 hit、火力支援每 hit、燃烧轰炸每 wave、三连击延迟段各按规定次数触发，并保持原始段在前、天空之怒在后的连续 Order。 |
| `SkyWrath_EachLayerRerollsLivingCandidatesAndSingleCandidateStillAdvancesRandom` | 每层重新读取存活候选；前层主目标死亡后下一层重抽，单候选仍调用 `NextInt(1)`，随机状态与镜像 oracle 一致。 |
| `SkyWrath_UsesBombardScalingAndExcludesNonSupportDamageWithoutRecursion` | Bombard 4 层把天空之怒 8/4 缩放为 11/6；钢针、炸弹、回合末燃烧、即时射击与便携帮手均不触发，天空之怒自身也不递归。 |
| V2 快照与构建门禁 | 3266 的 Rare/1E/Self/PowerPile/Program/Implemented 元数据被精确冻结，降级为 `CatalogOnly` 会被验证器拒绝。 |

开发中出现过两轮红测，均由测试场景或 oracle 不符合真实前提造成，生产实现没有为测试迁就：

1. 首版分层随机场景使用 `2 × SkyWrath + TripleStrike`，总需 6 Energy，但 fixture 的 `initialEnergy` 上限为 5，第三张卡实际未成功施放，`Play` helper 因结果数量不符失败。场景改用总计 4 Energy 的 BansheeStrike 后再验证相同逐层语义。
2. 改场景后的 oracle 曾把当前 raw random state 直接传给 `new GameRandom(rawState)`，这与生产端“以 1 初始化后赋 `State = rawState`”不是同一序列。测试改为 `new GameRandom(1u) { State = randomBefore }`，随后主目标与最终随机状态一致。

## 4. 数据、同步与 Unity 证据

| 项目 | 结果 | 说明 |
|---|---|---|
| Luban 与生成配置 | 通过 | 只开放 3266；82 模板为 67 `Implemented` / 15 `CatalogOnly`，V1 为 54/10，V2 为 13/5。 |
| 本地化 | 通过 | 从 Excel 导入战斗卡本地化成功。 |
| Sync 与本地 Addressables | 通过 | `Sync and Build All` 完成，Addressables 构建耗时 12.956 秒。 |
| 翻表前运行时 EditMode | 通过 | MCP 任务 `eefded85c7aa4a099d3b16ee4577e704`：117/117 passed；用于在目录状态仍关闭时验证程序与调度行为。 |
| Unity 定向 EditMode | 通过 | MCP 任务 `3a279411d63749abaf8eca64ec4236cc`：139/139 passed，0 failed；覆盖 Starter、Scheduled Effects、V2 目录快照、目录验证器与伤害管线相关集合。 |
| 完整 EditMode | 通过 | MCP 任务 `a46a25a9da924131965130d6e2b07b8b`：650/650 passed，0 failed/skipped，耗时 174.2163423 秒；CardArt 与 Character Prefab 冷加载较慢但均通过。 |

3266 的精确作者表、生成、同步、定向与完整 EditMode 均已通过；本切片按标准完整门禁收口。

## 5. 结构证据与排除边界

- 钩子位于四类 scheduled effect 的原始逻辑段结束点，而不是按任意 `Support` damage settlement 监听：Banshee/FireSupport 在每 hit 后，FireBombardment 在每 wave 的全部 Damage/Burn/Oil 后，TripleStrike 在唯一延迟伤害后。
- 每层先保存该层开始时的存活候选快照，随机主目标先结算，再按该快照的 Encounter 顺序处理其余目标；下一层重新查询投影，因此前层致死会改变后层候选。候选为空时不调用随机流，候选为一时仍显式调用。
- 天空之怒伤害通过 scheduled Support 操作进入现有管线，但追加完成后不再次调用天空之怒入口。NeedleStorm、Bomb、Burn、即时攻击和 PortableHelper 的代码路径没有调用该入口。
- Bombard 仅复用数值换算函数缩放天空之怒的基础 8/4；这不把天空之怒伪装成新的 scheduled effect，也不改变四种原始支援的触发频率、目标选择、载荷或生命周期 settlement。

## 6. 验收后边界

- 本切片只实现 3266 基础态；升级费用与数值仍只是作者表元数据。
- 没有修改默认 Deck、奖励、Run、UI、多人、Scene 或 Prefab。
- 没有实现 3260、临时卡、选择、自动免费攻击、AnyAlly 或其他跨卡协议。
- 其余 15 张 `CatalogOnly` 继续由精确目录门禁拒绝。
- 本切片未创建提交，也不记录或杜撰提交 SHA。
