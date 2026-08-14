---
title: STS2 v0.107.1 Ironclad 单人卡池接入
page_type: plan
lifecycle: active
created: 2026-08-06
updated: 2026-08-14
status: juggernaut-settlement-trigger-verified-unity-native-2026-08-14
status_source: ../SESSION_LOG.md
---

# STS2 v0.107.1 Ironclad 单人卡池接入

## 目标与版本边界

以用户本机 Steam public/main 安装为版本事实，尽可能把 Ironclad 单人卡池接入 TinySpire：

- 游戏版本：`v0.107.1`
- Steam build：`23811903`
- 游戏构建 commit：`59260271`
- 语言源：英文；TinySpire 提供项目自有 en / zh-CN 表述
- 提取日期：`2026-08-06`
- 范围：85 张单人卡（3 Basic、20 Common、35 Uncommon、25 Rare、2 Ancient）
- 排除：`Demonic Shield`、`Tank` 两张多人专用卡，等待 `DEP-008`；不把 v0.110.0 beta 内容混入本快照

本计划不会提交 STS2 官方卡图、完整原文数据库或二进制提取物。卡名、结构化数值与机制事实用于兼容实现；说明文本采用项目自有双语表述，未有原创牌面时使用明确的项目占位素材。

## 锁定接口与原子性

- 共享战斗写入仍只由 `BattleCommandQueue.Submit` 排序。
- Queue、Turn、BattleSession、CardZones 对外只作为只读取证面；不得增加第二份 Hand、CardZones、Combatant、Intent 或 Power 事实。
- 卡牌程序在首次权威写入前完整解析、投影和校验；失败必须是零能量、零卡区、零参与者、零随机流写入。
- 配置只表达机制原语、目标选择器、值表达式、条件、重复和触发器；不得按模板 ID、卡名或本地化文本在运行时代码分支。
- 新增 settlement 只能表达通用机制事实，不能出现卡牌专用 settlement。
- 每个切片先取得精确红灯，再做最小实现、相关回归、Luban / `TinySpire/Build/Sync and Build All`、文档同步和独立停止点。

## 串行切片

| 切片 | 目标 | 精确红灯 / 验收 seam | 受限边界 |
|---|---|---|---|
| I0 | 冻结版本、85 张目录和机制缺口矩阵 | 本机 manifest/release_info 与同 build 社区结构化数据交叉核对 | 只读；不复制官方图像/完整原文 |
| I1（已完成） | `CatalogOnly` 运行时隔离 | `Submit_CatalogOnlyCard_FailsBeforeEnergyOrCardZoneWrites` | Card schema、Rules、失败原因；不改 Queue/Turn/settlement |
| I2（已完成） | `CatalogOnly` 构建隔离 | `Validate_CatalogOnlyCardReferencedByDeck_Throws`；Implemented 卡必须具备有效程序/牌面键 | Editor validator；不改运行时权威写链 |
| I3（已完成） | 85 张单人卡全部进入 Card 表并标明实现状态 | 表计数、唯一外部 key、类型/稀有度/费用/目标/归宿/升级元数据、双语 key 覆盖；当前 Deck 只引用 Implemented | 不提交官方牌面；CatalogOnly 不可玩 |
| I4（已完成） | 成功归宿（Discard / Exhaust） | 配置为 Exhaust 的真实卡按 Effect 后移入 Exhaust | 只改 Turn 内部成功归宿；未增加 Exhaust 飞行动画 |
| 首批运行卡（已完成） | 通用有序 Effect 组合、`DrawCards` 与 PreparedDraw | Bludgeon、Twin Strike、Pommel Strike、Shrug It Off 经 Queue 验证基础数值、顺序、重洗、手牌上限、致死与零写入 | 不宣称 I5 的每步独立目标整体完成；不实现升级实例 |
| Burning Pact（已完成） | 出牌前单选、选择牌 Exhaust 后普通抽牌与来源牌归宿的联合事务 | 3125 经 Rules→UI Session→Queue 验证合法/非法选择、零候选、Hand 10、重洗、RNG/Layout 漂移和表现顺序 | 只实现基础 Draw 2；升级 Draw 3 仍为元数据，不开放执行中暂停选择 |
| Not Yet（已完成） | 通用封顶治疗、零实际治疗记录与正实际值表现 | 3171 经 Queue 验证缺血封顶、满血、后续非法 Effect 原子失败、Exhaust 与 `+N` 表现 | 只完成 I12 的治疗子能力；升级 13、失血、Fatal、永久 Max HP 仍排除 |
| I5 | 每步独立目标与深卡牌执行 module | Enemy 伤害后 Source 格挡，记录顺序正确且原子 | 需要改 Turn/settlement；Queue seam 不变 |
| I6 | 多目标、随机目标与重复命中 | Encounter 顺序、确定性随机、死亡跳过 | 不增加全局随机或第二队列 |
| I7 | 抽牌、手牌上限与卡区选择 | 抽牌上限、队首选择重验、漂移零写入 | 需要 CardZones 通用移动；执行中暂停选择仍排除，出牌前请求不在此限 |
| I8 | X 费、能量增减与临时费用 | 同一冻结 X 驱动所有步骤并支付一致 | 不公开新的能量写入口 |
| I9 | 升级与实例修饰 | 升级实例使用升级费用/程序且模板身份不变 | 不把实例事实回写表格 |
| I10 | Retain、Ethereal、Innate 与回合卡区时机 | 回合结束稳定顺序处理保留/消耗/弃牌 | 需要改 Turn，保持单一 Layout 发布 |
| I11 | Power、Modifier 与命令内触发器 | 后续命令内按序展开触发器并生成通用记录 | 不增加第二命令/动画队列 |
| I12 | 失血、治疗与战斗内生命变化 | 失血绕过 Block，治疗不超过战斗上限 | 永久 Max HP 等 Run authority |
| I13 | 生成、复制与随机牌 | 同种子结果一致、实例 ID 唯一、随机域隔离 | 不消费洗牌或意图随机流 |
| I14 | 全卡逐张 Queue 回归与真实 BattleScene 验收 | 每张 Implemented 卡有 Queue/事实/settlement/文本/AB/Console 证据 | 主观观感不冒充性能或规则通过 |

## 当前停止范围：I0 → I4 + 首批四张基础运行卡 + Burning Pact + Not Yet + Sword Boomerang + Body Slam + Barricade + Havoc（已完成 Unity 原生验证）

I4 的成功归宿边界继续有效：`BattleTurnController` 在首次权威写入前冻结基础 `PlayDestination`，只接受 Discard / Exhaust，并在全部 Effect 之后调用既有 CardZones 移动原语。I4 历史证据见 `../06_testing/2026-08-06-sts2-ironclad-i4-success-destination.md`。

2026-08-13 的后续独立停止点已经完成 Bludgeon（3123）、Twin Strike（3120）、Pommel Strike（3113）与 Shrug It Off（3115）基础态。公共 `EffectType.DrawCards = 4`、有序 `BattleCardEffectSequenceExecutor` 与 CardZones `PreparedDraw` 共同保证：全部子计划在首次写入前预构建和联合校验，普通抽牌遵守 Hand 10 上限、旧弃牌重洗、随机快照、连续 settlement 与至多一次布局发布；Draw 前致死仍执行已冻结抽牌，非法费用、目标或绑定保持零写入。

正式数据为 Pommel `Damage 9 + Draw 1`、Shrug `Block 8 + Draw 1`、Twin `Damage 5` 两次与 Bludgeon `Damage 32`；Twin 的绑定键固定为 `damage` / `damageRepeat`。该独立停止点完成时 Ironclad 85 张为 **8 张 `Implemented` / 77 张 `CatalogOnly`**，历史证据见 `../06_testing/2026-08-13-sts2-ironclad-first-four-effect-runtime.md`。

后续独立停止点已完成 `Burning Pact`（3125）基础态：1 Energy、Self、DiscardPile，`Program.None` 按 `exhaustCards:4012,cards:4013` 解释为选择并消耗另一张手牌后抽 2 张。公共 `ExhaustSelectedHandCard = 5` 只接受 `Attribute.None / Value 1` 且必须位于唯一 Draw 之前；没有卡牌 ID、名称或职业分支。选择协议复用 V2V 的 `BattleSingleOtherHandCardSelectionRules`、`SelectedCardIds` 和 Hand UI 会话；CardZones 联合计划按 selected Exhaust→重洗/抽牌→source Discard 冻结 owner、Layout 与 RNG，并只发布一次 Layout。无候选仍支付并抽 2；Hand 10 时来源仍占容量，只抽 1、最终 Hand 9。当前 Ironclad 为 **9 张 `Implemented` / 76 张 `CatalogOnly`**；详细证据见 `../06_testing/2026-08-13-sts2-ironclad-burning-pact-runtime.md`。这仍不代表 I5 或 I7 的全部能力完成，也不开放升级实例、其余目录卡、Deck、奖励、Run、执行中暂停选择或多人流程。

最新独立停止点完成 `Not Yet`（3171）基础态：2 Energy、Self、ExhaustPile，`Program.None` 通过 `heal:4014` 使用公共 `EffectType.Heal = 6` 恢复至多 10。治疗请求、前后生命与实际量在首写前冻结；满生命仍记录实际 0 并完成 Exhaust，但不会派生 `+0`。Heal 后存在缺失 Effect 时整个命令零写入。当前 Ironclad 为 **10 张 `Implemented` / 75 张 `CatalogOnly`**；该切片只满足 I12 的封顶治疗子能力，不开放升级恢复 13、失血、Fatal、永久 Max HP 或失血历史，详见 `../06_testing/2026-08-13-shared-heal-not-yet-field-surgery-runtime.md`。

当前最新独立停止点完成 `Sword Boomerang`（3116）基础态：1 Energy、Common Attack、RandomEnemy、DiscardPile，三条有序绑定 `damage:4015, damageRepeat1:4015, damageRepeat2:4015` 各自表达一次基础 3 点伤害。通用 `BattleRepeatedDamageExecutor` 在首写前冻结来源、Encounter 全体敌人、每段目标/outcome、终态投影和 Turn-owned CardTarget RNG 前后状态；每击从上一击投影后的存活敌人中重选，被击杀目标不再进入候选，没有存活敌人时停止尾段且不推进额外 RNG。显式目标、费用、配置或快照失败保持 Energy、HP/Block、卡区、Turn、settlement 与随机流零写入。当前 Ironclad 为 **11 张 `Implemented` / 74 张 `CatalogOnly`**；升级第 4 击仍只是作者表与本地化元数据，详见 `../06_testing/2026-08-13-shared-repeated-damage-sword-boomerang-prismatic-shot-runtime.md`。

### I1 红灯（已完成）

1. 通过公开 `BattleCommandQueue.Submit` 提交一张 `CatalogOnly` 手牌。
2. 期望稳定失败 `CardNotImplemented`。
3. 断言能量、Hand/Discard/Exhaust、Combatants、Turn 与 Queue fault 均未改变。

### I2 红灯（已完成）

1. 让任一 Deck 引用 `CatalogOnly` 卡。
2. 构建期校验必须在 Luban 后、Localization/Addressables 前抛出带 Deck/Card ID 的错误。
3. `Implemented` 卡缺效果程序或合法 `illustration_key` 时同样 fail-fast；`CatalogOnly` 的占位素材规则必须显式，不允许伪造路径。

### I4 红灯（已完成）

1. 经公开 `BattleCommandQueue.Submit` 提交配置为 Exhaust 的 Tremble，期望结算严格为 Energy 3→2、Vulnerable 0→3、Hand→Exhaust；旧实现实际移动到 Discard。
2. 生成数据要求 Tremble 为 `Implemented`、绑定 `vulnerable:4006` 且 Effect 4006 为 ApplyVulnerable 3；旧 JSON 实际仍为 `CatalogOnly`。
3. 构建门禁在 4/81 数量不变时交换 Tremble 与 Anger 的可玩身份，必须报告 missing/unexpected；旧门禁只检查 3/82 数量。

### 首批四张基础运行卡红绿（已完成）

1. TDD 任务 `2f8fa9d405e94893b9a0cc600faff777` 中 Bludgeon / Twin Strike 先复用既有效果通过，Pommel / Shrug 的两项唯一红因均为 `UnsupportedEffectType`，精确暴露公共 Draw Effect 缺口。
2. 最小实现新增 `DrawCards = 4`、Effect 4007～4011、有序 Effect 组合与 PreparedDraw 联合事务；四张正式绑定和文本参数均由构建门禁锁定，Twin 的第二键使用 `damageRepeat`，没有放宽本地化 validator。
3. 正式 smoke `49d34997a550459f98b80d6ee88deec0` 为 20/20，正式聚合 `c3281b04224845eaa4138ea5024904a0` 为 67/67，完整 EditMode `0856b63a9ad44ea08a8a37d0df803571` 为 713/713；Luban、Localization、Sync/Addressables 与 BuildLayout 均通过。

### Burning Pact 红绿（已完成）

1. 精确 TDD 红灯 `91544da77057452bba4004fda382a130` 为 1/1 failed，唯一原因是 `ExhaustSelectedHandCard` 尚未受支持；后续七项运行时/卡区/规则/UI/表现绿灯的完整 ID 见验收页。
2. 最小实现新增 Effect 4012/4013、`ExhaustSelectedHandCard = 5`、通用选择后抽牌语法和 `BattlePreparedSelectedHandCardDrawAndPlayedCardDeparture`。合法/非法选择、零候选、Hand 10、旧弃牌重洗、跨 owner、Layout/RNG 漂移、重复提交与真实表现顺序均在单一 Queue 写链内验证。
3. 正式行为 `c1c48a5d4738462aa8a150d6e614f577` 为 9/9，目录 `5310c9f189044261922c4cdc2823ef31` 为 22/22，含真实 AB 的聚合 `54a914c2c66647879fe274dcc384b86d` 为 172/172，完整 EditMode `c708030e61834d7dbe3196c6d378f30f` 为 754/754；Luban、Localization、Sync/Addressables、BuildLayout 与 Console 0 error 均已收口。

### Not Yet 与共享 Heal 红绿（已完成）

1. 精确红灯 `4b1ef61209f749fe87138f6e9a767175` 为 1/1 failed，唯一暴露 Heal 尚未受普通 Effect 支持；最小实现后的封顶绿灯 `94d085dec3c74d2287831846b0baddba` 为 1/1 passed。
2. 满血实际 0、后续缺失 Effect 零写入、表现计划和飘字 factory 分别由 `1615e33b3d8c4a159a258f2baebaff43`、`00240b3902af4ee395c7da2fad1cf6b4`、`0726442131f944748c7d92768a23726a`、`649b881e43ac4601bd2ae04a51f3a959` 锁定。
3. 与 Field Surgery 共用 Heal 基础设施并修正来源顺序后，精确行为 `b511f5ddcd2041a9b264c0f982c4b600` 为 9/9，正式目录 `c3e5c7dbcb534cd18a85b635761fb8d7` 为 50/50，治疗视图 `4d5e4253e93840bd849571512f5f0a43` 为 1/1，含治疗视图与真实 AB 聚合 `818f8283386b4d86aa625c6d95284245` 为 243/243，完整 EditMode `c6a86ba528804a13b1c84fe38c28b48b` 为 766/766。

### Sword Boomerang 与共享重复伤害红绿（已完成）

1. 精确红灯任务前缀 `7f83...` 为 1/1 failed，唯一暴露普通出牌不支持 `RandomEnemy`；最小通用适配后 `587c3264fb684e49bf46501a81c96b33` 为 1/1 passed。
2. concrete repeated-damage plan 只开放固定敌人与逐段随机存活敌人两种真实策略；Prepare / Validate / Commit 冻结段目标、普通伤害公式、死亡候选投影和 CardTarget RNG，显式目标拒绝、击杀排除、无候选停止、跨 owner / 标量 / RNG 漂移与重复提交均已覆盖。
3. 与幻彩射击共享规划器后的双卡定向 `6932f72f288a477ca5869c21e3ac3996` 为 11/11，正式门禁 `908e5fb8b93e437d89533bb1b727231a` 为 53/53，回归修复代表集 `6ee679521f4c45d9a69b9984110c51bb` 为 5/5，最终行为聚合 `4ea4eff81b3c4ce786e318d0902c1ed4` 为 243/243，完整 EditMode `3e0a091d891e4f918668b99cb4a20157` 为 776/776；Luban、Localization、Sync/Addressables 与真实 BuildLayout 均已收口。

### Body Slam 与共享来源 Block 伤害（已完成 Unity 原生验证）

1. `Body Slam`（3105）基础态读取命令 Prepare 时的来源当前 Block 作为普通攻击 base magnitude；它仍经过 Strength、目标 Vulnerable、目标 Block / HP 与致死公式，且不会消耗来源 Block。基础态与升级 metadata 的正式文本均为 EN `Deal {damage} damage, equal to your Block.`、ZH `造成 {damage} 点伤害，数值等同于你当前的格挡。`；`{damage}` 是 Localization validator 所需占位符，运行时动态显示来源 Block。升级实例仍未实现。
2. 通用 magnitude source 只区分配置固定值与 `SourceBlock`，没有 3105、名称或职业分支。公式与状态计数联合任务前缀 `419c…` 为 2/2，原子 / 回归修复任务前缀 `b5f…` 为 8/8，行为聚合任务前缀 `79a…` 为 289/289。
3. 3105 已正式翻为 `Implemented` 并绑定 Effect 4016 / 强枚举 `DealDamageFromSourceBlock`；Luban、Localization Import / Validate、`Sync and Build All`、真实 BuildLayout / `AssetBundleProvider` 均成功。正式计数为 Ironclad **12/73**、全项目 **92/76**、Effect **16**；最终定向任务 `88e36d2a5cbb47b7b4a67207dad00856` 为 9/9，完整 EditMode `9ca3d43a79d24b25a917fad7b6166584` 为 793/793。作者表、生成物与物理 bundle 的精确 hash 见 `../06_testing/2026-08-14-shared-source-magnitude-poison-body-slam-secondhand-smoke-runtime.md`。

### Barricade 与共享 Block 保留（已完成 Unity 原生验证）

1. `Barricade`（3157）基础态为 3 Energy、Rare Power、Self、PowerPile，通过普通 Effect 4017 建立永久 Block 保留；以后每个 PlayerRoundStart 均跳过 Block 清除，不改变 Block 数值。
2. 通用 `BattleBlockRetention` 的永久 / 计时 / 回合开始操作都走 Prepare / Validate / Commit，并由同一个 Turn-owned 实例服务普通 Effect 与状态时机；它不读取 3157、3246、职业或卡区。与 Garrison 共享后的精确计时为 `2→1→0` 两次仍保留，下一次才清 Block。
3. 3157 已正式翻为 `Implemented`，Ironclad 为 **13/72**、全项目 **94/74**、Effect **17**。最终定向任务前缀 `17e031…` 为 300/300、完整 EditMode 前缀 `b4d970…` 为 798/798；升级 2 Energy 仍只是 metadata，详见 `../06_testing/2026-08-14-shared-block-retention-barricade-garrison-runtime.md`。

### Havoc 与共享触发出牌（已完成 Unity 原生验证）

1. `Havoc`（3108）基础态支付自身 1 Energy 后，冻结 DrawPile 顶牌，通过 Queue-owned system-token continuation 免费执行完整出牌，并无视该牌原归宿而强制进入 ExhaustPile。
2. continuation 只在当前命令成功后由 Queue 串行消费，不在 Turn 内递归提交，也不开放第二公共写入口；空 DrawPile、快照漂移或触发牌 typed failure 均保留现有命令 / fault 语义。
3. 3108 已翻为 `Implemented`，Ironclad **14/71**、全项目 **96/72**、Effect **18**；定向 8/8、完整 EditMode 802/802 与真实 AB 通过。升级费用 0 仍只是 metadata，详见 `../06_testing/2026-08-14-shared-triggered-play-havoc-opportunistic-strike-runtime.md`。

### Juggernaut 与共享 settlement-derived trigger（已完成 Unity 原生验证）

1. `Juggernaut`（3169）基础态冻结为 2 Energy / Rare Power / Self / PowerPile / `Program.None`，通过 `triggerDamage:4019` 注册 raw Effect type 10 / `Attribute.None` / value 6。只有目标为持有者且实际增加量为正的 `BattleBlockGainedSettlement` 产生一次触发；每条 settlement 独立冻结一个随机存活敌人。
2. 共享 `BattleSettlementTriggerEngine` 在 Power 父出牌事务中以 Prepare / Validate / Commit 注册，并在后续父命令提交后按 settlement 顺序、注册顺序冻结 intent。Queue 在父表现屏障之后以内部 `ResolveSettlementTriggers` continuation 提交子伤害；外部公开入口仍只有 `BattleCommandQueue.Submit`。
3. 基础 6 点触发伤害不读取来源 Strength 或目标 Vulnerable，但仍经目标 Block / HP / 致死权威写链。无存活敌人时不伪造伤害 settlement；候选、随机或参与者快照漂移时子事务在首写前失败。
4. Juggernaut 升级伤害仍只是作者表 / 本地化 metadata，升级实例未实现；本切片没有新 HUD、Prefab 或 Scene。正式生成后 Ironclad **15/70**、全项目 **98/70**、Effect **19**，强枚举已替代 raw 10。Luban 通过；首次 Sync 因缺少 `{triggerDamage}` 被正确拒绝，单点 i18n 修复后 Localization / Sync / Addressables 成功。定向 `054b6bcd5d734f729a2f1f95c4e7a80d` 7/7，完整 EditMode `d156b8e2537546ef9e83da0ef5dadd2a` 807/807；哈希、BuildLayout 与耗时见 `../06_testing/2026-08-14-shared-settlement-trigger-juggernaut-unstoppable-runtime.md`。

## 完成定义

“录入”与“可玩”必须分开报告：

- 录入完成：85/85 单人卡都有稳定目录身份、版本来源、类型/稀有度/费用/目标/归宿/升级元数据和 en/zh-CN key，并能由 Luban/构建校验重复生成。
- 可玩完成：卡牌从 `CatalogOnly` 翻为 `Implemented`，且该卡涉及的全部机制已通过 Queue、只读事实、settlement、Addressables Packed 路径、真实 Game View 与 Console 验收。
- 多人专用卡、永久 Run 修改和需要执行中暂停选择的卡，不得用局部假状态冒充完成。
