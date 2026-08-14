---
title: STS2 v0.107.1 Ironclad 首批运行卡需求摘要
page_type: requirement
lifecycle: active
created: 2026-08-13
updated: 2026-08-14
status: juggernaut-base-verified-unity-native-2026-08-14
status_source: ../SESSION_LOG.md
source:
  - ../04_research/2026-08-06-sts2-v01071-ironclad-card-snapshot.md
related_plan: ../plans/2026-08-06-sts2-v01071-ironclad-card-pool.md
confidence: source-stated-and-code-observed
---

# STS2 v0.107.1 Ironclad 首批运行卡需求摘要

> 本页是冻结 Ironclad 目录进入逐卡运行时阶段后的日常摘要。85 张目录身份与版本来源仍以研究快照为 source；本页记录首批四张、Burning Pact、Not Yet 与 Sword Boomerang 基础态，以及为它们开放的通用 Effect、普通抽牌、出牌前单选、原子卡区事务、封顶治疗和逐段随机重复伤害契约。

## 1. 当前结论与范围

- 冻结快照 `sts2-v0.107.1-23811903-59260271` 仍为 85 张单人卡，并继续排除多人专用 `DEMONIC_SHIELD` 与 `TANK`。
- 首批独立停止点把 `POMMEL_STRIKE`、`SHRUG_IT_OFF`、`TWIN_STRIKE`、`BLUDGEON` 四张卡从 `CatalogOnly` 翻为 `Implemented`；该停止点连同 Bash、Defend、Strike、Tremble 达到历史 **8/77**。后续停止点再把 `BURNING_PACT`（3125）、`NOT_YET`（3171）与 `SWORD_BOOMERANG`（3116）基础态翻为 `Implemented`，Ironclad 当前为 **11 张 `Implemented` / 74 张 `CatalogOnly`**。
- 七张后续卡都只完成基础实例的运行程序与自动门禁；作者表中的升级费用、升级归宿和升级说明仍是目录元数据。Burning Pact 升级“抽 3 张”、Not Yet 升级“恢复 13”与 Sword Boomerang 升级第 4 击都不代表升级实例或升级数值已经进入运行时。
- 当前规则仍只从 `BattleCommandQueue.Submit` 进入共享写链；没有为 Ironclad 增加卡牌 ID、显示名或本地化文本分支。

## 2. 四张基础态与有序绑定

| 卡牌 | ID | 费用 / 目标 / 归宿 | 有序 `effect_bindings` | 基础态语义 |
|---|---:|---|---|---|
| Pommel Strike | 3113 | 1 Energy / Enemy / DiscardPile | `damage:4009, cards:4010` | 造成 9 点伤害，然后抽 1 张。 |
| Shrug It Off | 3115 | 1 Energy / Self / DiscardPile | `block:4011, cards:4010` | 获得 8 Block，然后抽 1 张。 |
| Twin Strike | 3120 | 1 Energy / Enemy / DiscardPile | `damage:4008, damageRepeat:4008` | 同一 5 点伤害 Effect 按绑定原序独立结算两次。 |
| Bludgeon | 3123 | 3 Energy / Enemy / DiscardPile | `damage:4007` | 造成 32 点伤害。 |

`damageRepeat` 只负责保留 Twin Strike 的第二段执行身份与顺序；说明文本继续只展示 `{damage}`，不要求第二个显示参数。

### Sword Boomerang 基础态

| 卡牌 | ID | 费用 / 目标 / 归宿 | 有序 `effect_bindings` | 基础态语义 |
|---|---:|---|---|---|
| Sword Boomerang | 3116 | 1 Energy / RandomEnemy / DiscardPile | `damage:4015, damageRepeat1:4015, damageRepeat2:4015` | 独立造成三次基础 3 点普通伤害，每击从当前投影仍存活敌人中重新随机选择。 |

- 被前一击杀死的敌人不会进入后续候选；没有存活敌人时停止剩余段并不再取随机数。命令携带显式 TargetId、费用不足、非法绑定或计划快照漂移时，Energy、参与者、卡区、Turn、settlement 与 CardTarget RNG 保持零写入。
- 升级说明中的第 4 次伤害仍只是目录与本地化元数据；当前基础 `CardInstance` 固定执行三条绑定。

## 3. Burning Pact 基础态与选择语法

| 卡牌 | ID | 费用 / 目标 / 归宿 | 有序 `effect_bindings` | 基础态语义 |
|---|---:|---|---|---|
| Burning Pact | 3125 | 1 Energy / Self / DiscardPile | `exhaustCards:4012, cards:4013` | 若存在另一张手牌，精确选择并消耗一张；随后抽 2 张，来源牌最后弃置。 |

- `Program.None` 的通用选择抽牌语法固定为首项 `ExhaustSelectedHandCard / Attribute.None / Value 1`，随后恰好一个符合普通 Draw 约束的 `DrawCards`；3125 的 Effect 4013 冻结为 `Attribute.None / Value 2`。缺失、重复、乱序、非法 Attribute/Value 或夹入其他战斗 Effect 均在首次写入前失败；运行时没有 3125、名称或 Ironclad 分支。
- `BattleSingleOtherHandCardSelectionRules` 同时供 Burning Pact 与 Vent Heat 使用。存在另一张手牌时，`SelectedCardIds` 必须精确携带一个合法实例；规则层返回 RequiredCount 1 和合法候选。空选、多个、选择来源或陈旧实例均拒绝。
- 来源是唯一手牌时不创建选择请求，也不执行 Exhaust；Burning Pact 仍支付 1 Energy、抽 2 张并把来源牌弃置。这个零候选结果属于 Burning Pact 自身的 Effect 语义，不改变 Vent Heat 的职业能量规则。

## 4. 通用 Effect 数据契约

| Effect ID | 类型 | Attribute | Value | 用途 |
|---:|---|---|---:|---|
| 4007 | `DealDamage` | `None` | 32 | Bludgeon |
| 4008 | `DealDamage` | `None` | 5 | Twin Strike 两次复用 |
| 4009 | `DealDamage` | `None` | 9 | Pommel Strike |
| 4010 | `DrawCards`（枚举值 4） | `None` | 1 | Pommel Strike / Shrug It Off |
| 4011 | `GainBlock` | `None` | 8 | Shrug It Off |
| 4012 | `ExhaustSelectedHandCard`（枚举值 5） | `None` | 1 | Burning Pact 精确选择一张其他手牌 |
| 4013 | `DrawCards` | `None` | 2 | Burning Pact |
| 4014 | `Heal`（枚举值 6） | `None` | 10 | Not Yet |
| 4015 | `DealDamage` | `None` | 3 | Sword Boomerang 三次复用 |

- `DrawCards = 4` 是公共 Effect 类型，不是 Ironclad 专用 program。当前一张普通卡至多声明一个 Draw binding；它必须使用 `Attribute.None` 且 Value 不得为负，第二个 Draw、非法 Attribute 或负值都在首次权威写入前失败。
- 普通 Effect 组合按作者绑定原序分为 Draw 前战斗 Effect、至多一次 Draw、Draw 后战斗 Effect。全部子计划先联合预构建和校验；Draw 后战斗 Effect 读取 Draw 前 Effect 的完整投影，不因中间卡区操作丢失 Strength、Block 或 HP 快照。
- 多次引用同一伤害 Effect 仍是多个独立逻辑步骤。Twin Strike 第一击致死时，第二击保留为跳过记录，不产生第二次伤害，也不阻止当前牌完成归宿。
- `Heal` 通过共享纯 outcome 在首次写入前冻结请求量、当前 / 上限生命、治疗后生命与实际恢复量，再经唯一内部生命写入口提交。实际恢复可以为 0；领域 settlement 仍保留，但只有正实际量显示 `+N`。

### Not Yet 基础态

| 卡牌 | ID | 费用 / 目标 / 归宿 | 有序 `effect_bindings` | 基础态语义 |
|---|---:|---|---|---|
| Not Yet | 3171 | 2 Energy / Self / ExhaustPile | `heal:4014` | 请求恢复 10 点生命，受战斗生命上限约束；来源牌随后消耗。 |

- 缺 7 HP 时实际恢复 7；满生命时仍支付 2 Energy、记录 requested 10 / actual 0，并把来源移入 ExhaustPile。
- Heal 后存在非法或缺失 Effect 时，完整 Effect 计划在 Energy、Health、卡区、随机流、Turn 和 settlement 首写前失败。
- Not Yet 只开放 I12 的“治疗不超过战斗生命上限”子能力；失血、Fatal、永久 Max HP 和失血历史仍未完成。

## 5. `PreparedDraw` 与选择后抽牌原子契约

- `BattleCardZonesData` 通过 `PrepareDraw` / `ValidatePreparedDraw` / `CommitPreparedDraw` 独占普通抽牌的 Hand 10 上限、DrawPile / DiscardPile、旧弃牌重洗、洗牌随机前后状态、连续 settlement 与最终布局。
- Prepare 只在本地副本预演；Validate 拒绝跨聚合、重复提交、布局或随机快照漂移；Commit 不重新随机，并且只有实际抽牌时才发布一次完整 `Layout`。
- Draw Effect 执行时当前打出的卡仍在 Hand。因此命令开始时已有 10 张手牌时本次抽牌为 0、不推进洗牌随机；随后当前卡按正常成功归宿进入 DiscardPile，最终 Hand 为 9。
- Draw 前伤害致死不取消已经冻结的普通抽牌；能量不足、缺少显式目标、非法绑定或计划漂移则必须在 Energy、战斗事实、卡区、随机流和表现结果发生任何写入前失败。
- Burning Pact 使用单一 `BattlePreparedSelectedHandCardDrawAndPlayedCardDeparture`，在 Prepare 阶段冻结 owner、起始 Layout、洗牌 RNG 前后状态、最终 Layout、全部移动 settlements 与一次性提交标记。Validate 拒绝跨 owner、Layout/RNG 漂移或重复提交；Commit 不重新随机，并只发布一次最终 Layout。
- Burning Pact 的逻辑顺序为 `EnergySpent → optional selected HandToExhaust → optional reshuffle → DrawPileToHand → source HandToDiscard`。抽牌容量投影中来源牌仍占 Hand：初始 Hand 10 时先移走选择牌，只能抽 1 张，来源随后弃置，最终 Hand 为 9。
- Sword Boomerang 使用 `BattlePreparedRepeatedDamagePlan`：Prepare 冻结来源、Encounter 全体敌人、每段随机目标、主伤 outcome、终态投影和 CardTarget RNG before/after；Validate 拒绝 owner、标量、Encounter、RNG、顺序或一次性生命周期漂移；Commit 不再随机或重算。唯一可变随机流由 Turn 持有，Session 只保存种子，Queue 只读观察状态。

## 6. 本地化参数规范

- 四张卡的 en / zh-CN 基础与升级说明已改为真实语义；升级说明仍只是文本和目录事实，不代表升级实例已实现。
- 本轮 Localization 先诊断出生成配置仍是 stale config；刷新到当前配置后，又暴露参数规范不允许下划线。最终 Twin Strike 的第二绑定统一为 `damageRepeat`，没有把 validator 放宽为接受 `damage_repeat`。
- Twin Strike 文本只消费 `{damage}`；`damageRepeat` 不进入显示模板。Pommel Strike 与 Shrug It Off 分别使用现有 `damage` / `block` 与 `cards` 参数。
- Burning Pact 基础说明使用 `{exhaustCards}` / `{cards}`，分别取 Effect 4012/4013；升级说明固定为“消耗 1 张，抽 3 张”，但升级 Draw 3 仍仅为文本与目录元数据。
- Not Yet 基础说明使用 `{heal}` 读取 Effect 4014 的 10；升级说明固定为恢复 13，但升级数值仍仅为目录和本地化元数据。
- Sword Boomerang 基础说明与三条 4015 绑定保持基础 3 伤 / 3 次一致，升级说明同步为 4 次；升级文本与 metadata 已入表，但升级实例执行仍未实现。

## 7. 已验证发布事实

### Burning Pact 发布时历史证据

| 项目 | 已验证结果 |
|---|---|
| 正式作者表 SHA-256 | Enums `D0984D35BE585D04C9C1E56B62B5C8AEFBB0F9760A38DBACF9477B3A685D0EC3`；Card `C3025BA774D84E24CAD679DEE057AA79F25A41F81AC83798E6263DDE8FAA22DB`；Effect `0B002B0C97820E7BF3F5DEFB54084F53CF94F1F224E77E15A8E8BCB62CC30173`；i18n `A05411C781FE20D3CFA99F0FD4AAD08F68E34F0A80571E425A5C2772E50B4C37`。 |
| artifact 校验 | 候选与正式工作簿 7217 个单元格的逻辑值、公式及规范化样式一致，渲染一致；Effect C 列仅按内容由 15.625 自动调整为 21.13。 |
| Luban / 生成数据 | 2026-08-13 17:32:12 成功；Card JSON `23BCA0295418E949AC3CA752C26F2C23A56FBD569EEA88C784C65B8EC914BAF6`，Effect JSON `D06C67D9AF1B22733340706607AE2D95DD3E7E78FD12AC7D4AEFA79AB077D008`；168 张为 86/82、Ironclad 9/76、Marine 76/6、Effect 13 项。 |
| Localization | 首轮失败只证明 Unity `AssetDatabase` 仍读取 stale 资源；强制刷新后 Import 7.401 秒、Validate 6.161 秒均通过。 |
| 同步与资源 | `Sync and Build All` 22.054 秒、Addressables 13.551 秒；BuildLayout `buildlayout_2026.08.13.17.40.23.json`（134622 bytes）证明 12085 bytes 的 GameData bundle 由 `AssetBundleProvider` 包含 Card/Effect JSON。 |
| 静态编译 | Runtime 0 error / 6 warning；Editor 0 error / 12 warning。 |
| Unity | 正式行为 9/9、目录 22/22、含真实 AB 的聚合 172/172、完整 EditMode 754/754，均通过；Console 最终 0 error。 |

首批四张发布时的 8/77、旧工作簿 SHA、713/713 等历史事实继续保留在 `../06_testing/2026-08-13-sts2-ironclad-first-four-effect-runtime.md`；Burning Pact 的完整红绿、正式任务 ID、耗时与范围边界见 `../06_testing/2026-08-13-sts2-ironclad-burning-pact-runtime.md`。

Not Yet 与共享 Heal 发布后的历史事实为：工作簿 SHA-256 Enums `dc35fc55df7a4223347f81054c09df88ddea3b6eb88da36de41499562dd7618e`、Effect `34eef4012c2b858e43fb0f7cb7c2417e1a3caa34d5afa3dcb46dfbd61c465af0`、Card `7c57c0a024d445d990ee275e7474a5460f7055504b1169f0b74dfd525d3665f3`、i18n `bd37b5660cbd5b1ceff8c07a58410c4f49e124acbdc3b97d893d4754b8551f5e`；当时 168 张为 88/80、Ironclad 10/75、Marine 77/5、Effect 14 项。Luban、Localization、Sync/Addressables、静态编译、正式 50/50、治疗视图 1/1、含真实 AB 聚合 243/243 与完整 EditMode 766/766 均通过；详见 `../06_testing/2026-08-13-shared-heal-not-yet-field-surgery-runtime.md`。

### 当前 Sword Boomerang 发布事实

Sword Boomerang 与共享重复伤害发布后的当前正式事实为：工作簿 SHA-256 Enums `DC35FC55DF7A4223347F81054C09DF88DDEA3B6EB88DA36DE41499562DD7618E`、Card `EA90C1A34FBDD9C54EBE2832C6CCC796DC4752A6B90C15F6A42BDB8C03A2CDF1`、Effect `35BF163D09E6F8AA6478C134D90A5FBAC304CC3135357D8237909DBC87ECAE64`、i18n `B80CD6EDCD0EAE2F52812B1CFF5DDAD96C1AB0507CD05E012C919DB05122215F`；168 张为 90/78、Ironclad 11/74、Marine 78/4、Effect 15 项。Luban、Localization、Sync/Addressables、BuildLayout/物理 bundle、静态编译、双卡定向 11/11、正式门禁 53/53、行为聚合 243/243 与完整 EditMode 776/776 均通过；详见 `../06_testing/2026-08-13-shared-repeated-damage-sword-boomerang-prismatic-shot-runtime.md`。

## 8. 未纳入范围

### 2026-08-14 Body Slam 后续摘要（已完成 Unity 原生验证）

- `Body Slam`（3105）基础态要求造成等同来源当前 Block 的伤害。当前实现把 Prepare 时的 Block 冻结为普通攻击 base magnitude，仍读取来源 Strength、目标 Vulnerable 和目标 Block / HP，且不消费来源 Block；它不是 true damage，也不是“失去 Block”。
- 基础态只消费 `SourceBlock` magnitude；基础态与升级 metadata 的正式文本均为 EN `Deal {damage} damage, equal to your Block.`、ZH `造成 {damage} 点伤害，数值等同于你当前的格挡。`。`{damage}` 是 Localization validator 所需占位符并在运行时动态显示来源 Block；当前仍没有升级 `CardInstance`。source 页面不因本实现改写，运行时裁决与发布状态以 CD-106 和当前测试页为准。
- 运行时公式与回归任务前缀 `419c…`、`b5f…`、`79a…` 保留为前置证据；3105 / Effect 4016 已正式生成，Luban、Localization、`Sync and Build All`、真实 AB 均成功。最终定向任务 `88e36d2a5cbb47b7b4a67207dad00856` 为 9/9，完整 EditMode `9ca3d43a79d24b25a917fad7b6166584` 为 793/793；当前正式计数为 Ironclad **12/73**、全项目 **92/76**、Effect **16**。

- 其余 74 张 `CatalogOnly` 不因共享 Effect、选择协议、PreparedDraw、Heal 或 repeated-damage plan 已存在而自动变为可玩。
- 不实现升级 `CardInstance`、升级数值切换、默认 Deck 变更、奖励选择、Run 持久牌组、战士专用 UI 或多人卡牌；I14 的逐卡真实 BattleScene / Game View 验收仍未完成。
- 本轮没有完成原计划 I5/I7 或 I6 的全部能力；随机只开放 Sword Boomerang 使用的逐段 `RandomEnemy` 存活候选策略，不代表全体、权重/链式目标、随机牌、X 费、执行中暂停选择、Power、Retain、Ethereal 或生成/复制等后续机制已开放。

## 9. Juggernaut 基础态需求摘要（已完成 Unity 原生验证）

- 基础态冻结为 `Juggernaut`（3169）/ 2 Energy / Rare Power / Self / PowerPile / `Program.None`；有序绑定为 `triggerDamage:4019`，Effect 4019 的数据语法为 raw type 10 / `Attribute.None` / value 6。
- Power 激活后，每条目标为持有者且实际增加 Block 的 `BattleBlockGainedSettlement` 各产生一次触发。触发伤害随机选择当时存活敌人，基础值 6，不读 Strength / Vulnerable，仍受目标 Block / HP / 致死写链处理。
- 触发不在 Effect 或 Turn 内递归执行；父命令提交 settlement 后冻结 intent，Queue 于父表现屏障完成后再执行内部子命令。无候选不伪造结果，快照漂移在子事务首写前失败。
- 升级伤害仍只是 metadata，未实现升级卡实例；本切片无新 HUD / Prefab / Scene。正式生成后 Ironclad **15/70**、全项目 **98/70**、Effect **19**，强枚举已替代 raw 10。Luban 通过；首次 Sync 因 i18n 缺少 `{triggerDamage}` 被正确拒绝，单点修复后同步与 Addressables 成功。定向 `054b6bcd5d734f729a2f1f95c4e7a80d` 7/7，完整 EditMode `d156b8e2537546ef9e83da0ef5dadd2a` 807/807；完整证据见 `../06_testing/2026-08-14-shared-settlement-trigger-juggernaut-unstoppable-runtime.md`。
