---
title: STS2 Ironclad Burning Pact 与通用选择消耗抽牌事务
page_type: testing
lifecycle: active
date: 2026-08-13
updated: 2026-08-13
status: verified-unity-native-2026-08-13
status_source: ../SESSION_LOG.md
source:
  - ../04_research/2026-08-06-sts2-v01071-ironclad-card-snapshot.md
  - ../01_requirements/2026-08-13-sts2-v01071-ironclad-first-four-runtime-digest.md
related_plan: ../plans/2026-08-06-sts2-v01071-ironclad-card-pool.md
related_decision: ../CODE_DECISIONS.md#cd-103burning-pact-以通用选择-effect-和原子选牌抽牌归宿事务接入
---

# STS2 Ironclad Burning Pact 与通用选择消耗抽牌事务

本页记录 `Burning Pact`（3125）基础态、通用选择 Effect、选择牌 Exhaust→普通抽牌→来源牌 Discard 的原子 CardZones 事务，以及正式作者表、生成数据、本地化、Addressables 与 Unity 原生验收证据。

## 1. 验收结论

- 3125 基础态为 1 Energy、Skill、Self、Hand→DiscardPile，`Program.None` 与有序绑定 `exhaustCards:4012,cards:4013` 表达“选择并消耗另一张手牌，然后抽 2 张”。它已从 `CatalogOnly` 翻为 `Implemented`；Ironclad 85 张当前为 **9 `Implemented` / 76 `CatalogOnly`**。
- 若存在另一张手牌，命令必须精确选择一个当前合法实例；所选牌先进入 ExhaustPile，再按容量抽至多 2 张，来源牌最后进入 DiscardPile。若来源是唯一手牌，则没有选择请求或 Exhaust，但仍支付 1 Energy、抽 2 张并弃置来源。
- 抽牌投影时来源牌仍占 Hand。初始 Hand 10 时先移走选择牌，只形成一个空位，实际抽 1 张；来源最后弃置后 Hand 为 9。旧 DiscardPile 的重洗、RNG 推进与移动记录全部由同一准备计划冻结。
- 升级说明“消耗 1 张，抽 3 张”只是作者表与本地化元数据；本页不证明升级 `CardInstance` 或升级数值切换。

## 2. 通用 Effect、选择协议与语法门禁

| Effect ID | 类型 | Attribute | Value | 用途 |
|---:|---|---|---:|---|
| 4012 | `ExhaustSelectedHandCard`（枚举值 5） | `None` | 1 | 精确选择并消耗一张来源之外的当前手牌 |
| 4013 | `DrawCards` | `None` | 2 | 在选择牌离手、来源牌仍占 Hand 的投影上抽牌 |

- 通用 `Program.None` 语法只接受选择 Effect 位于首项，其后恰好一个 Draw。缺少 Draw、Draw 在前、重复选择、重复 Draw、前后夹入战斗 Effect、选择 Attribute 非 `None` 或 Value 非 1，规则层与 Queue 均返回一致失败，并保持权威事实零写入。
- 运行时没有 3125、Burning Pact 显示名或 Ironclad 分支。`CardTextFormatter` 也从 Effect Value 解析 `{exhaustCards}` / `{cards}`，没有硬编码牌面数字。
- `BattleSingleOtherHandCardSelectionRules` 同时服务 Burning Pact 与 Vent Heat，只定义“来源之外当前同 owner 手牌”的候选集合。职业能量收益和通用选择后抽牌仍由各自适配器负责，没有为了代码复用混淆两张卡的结果语义。
- `PlayCardCommand.SelectedCardIds`、`BattleHandCardSelectionRequest` 和 `HandCardSelectionSession` 继续承担权威实例输入、规则请求和 UI 局部选择会话。空选、多个、选择来源或陈旧实例均在首写前失败；零候选时不创建请求。

## 3. CardZones 原子事务与表现顺序

`BattlePreparedSelectedHandCardDrawAndPlayedCardDeparture` 只属于 `BattleCardZonesData` 的深事务：Prepare 在本地副本冻结 owner、起始 Layout、洗牌 RNG 前后状态、最终 Layout、连续 settlements 与一次性提交标记；Validate 拒绝跨 owner、Layout/RNG 漂移与重复提交；Commit 不重新随机，并且只发布一次最终 Layout。

| 场景 | 冻结顺序与结果 | 验收 |
|---|---|---|
| 有合法候选 | `EnergySpent(1) → selected HandToExhaust → optional reshuffle → DrawPileToHand ×0..2 → source HandToDiscard` | 通过 |
| 来源是唯一手牌 | `EnergySpent(1) → DrawPileToHand ×0..2 → source HandToDiscard`，无选择请求、无 Exhaust | 通过 |
| 初始 Hand 10 | selected 离手后来源仍占位，只抽 1；source 弃置后最终 Hand 9 | 通过 |
| 旧弃牌重洗 | DiscardPile→DrawPile、reshuffle、DrawPile→Hand 顺序连续；RNG 只在成功 Commit 时推进 | 通过 |
| 非法选择或语法 | Energy、Layout、RNG、Turn、Queue 事实与 settlements 全部零写入 | 通过 |
| 跨 owner / Layout 漂移 / RNG 漂移 / 重复提交 | Validate 拒绝；不发布中间或第二次 Layout | 通过 |

表现层不制造伪 `CommandPrelude`：`EnergySpent` 没有可见步骤，真实的 selected→Exhaust、每次 Draw→Hand、source→Discard 按 settlement 顺序进入既有 adapter/runner；相应 transient 在步骤转换时清理，完成回调仍只触发一次。本切片没有新增战士专用 UI、Scene 或 Prefab。

## 4. 逐片 TDD 证据

| 切片 | 任务 | 结果与锁定事实 |
|---|---|---|
| 精确红灯 | `91544da77057452bba4004fda382a130` | 1/1 failed，唯一原因 `UnsupportedEffectType`；精确暴露类型 5 尚未实现。 |
| 基础 Queue + CardZones 原子计划 | `1e10bdc85d3a4970a540378e8e9aa773` | 2/2 passed；selected Exhaust、Draw 2、source Discard，以及 Prepare 零写入、单 Layout、RNG 冻结和一次性提交。 |
| 零候选与 Hand 10 | `f161c8b0ffd549bd906dfb7da7715de2` | 1/1 passed；唯一来源仍支付/抽牌/弃置，满手投影只抽 1、最终 Hand 9。 |
| 规则请求与漂移拒绝 | `a8246f19fdb24f8283430abf81022307` | 2/2 passed；RequiredCount 1/合法候选，以及跨 owner、Layout/RNG 漂移、重复提交零写入。 |
| 非法选择 | `814665044dac4aea917a624ea969757f` | 1/1 passed；空选、来源、多个和陈旧选择分别返回稳定失败，全部零写入。 |
| 非法绑定语法 | `08954580668f4d7588792cbb2898059e` | 1/1 passed；8 类非法语法在 Rules 与 Queue 返回一致失败。 |
| 真实选择协议 | `1325defc95f644d69fd4f43cf50f289b` | 1/1 passed；Rules→选择请求→`HandCardSelectionSession`→携带 `SelectedCardIds` 的 Queue 命令完整通过。 |
| 表现顺序 | `34146393c7f9466ba7f0faf285127094` | 1/1 passed；Energy、selected Exhaust、两次 Draw、source Discard 的可见步骤与 transient 清理顺序通过。 |

正式行为门禁 A 还覆盖 `CardTextFormatter` 对 Effect 4012/4013 的参数格式化，因此没有另造只验证硬编码文本的旁路测试。

## 5. 正式数据与生成证据

| 工作簿 | 正式 SHA-256 |
|---|---|
| `__enums__.xlsx` | `D0984D35BE585D04C9C1E56B62B5C8AEFBB0F9760A38DBACF9477B3A685D0EC3` |
| `battle.card.xlsx` | `C3025BA774D84E24CAD679DEE057AA79F25A41F81AC83798E6263DDE8FAA22DB` |
| `battle.card_effect.xlsx` | `0B002B0C97820E7BF3F5DEFB54084F53CF94F1F224E77E15A8E8BCB62CC30173` |
| `i18n.xlsx` | `A05411C781FE20D3CFA99F0FD4AAD08F68E34F0A80571E425A5C2772E50B4C37` |

- 工作簿候选与正式文件在复制前后均通过 artifact 复核：7217 个单元格的逻辑值、公式和规范化样式一致，四份渲染一致；`battle.card_effect.xlsx` 的 C 列只因新增长枚举名从 15.625 自动调整为 21.13。
- Luban 于 2026-08-13 17:32:12 成功。Card JSON SHA-256 为 `23BCA0295418E949AC3CA752C26F2C23A56FBD569EEA88C784C65B8EC914BAF6`，Effect JSON 为 `D06C67D9AF1B22733340706607AE2D95DD3E7E78FD12AC7D4AEFA79AB077D008`。
- 生成结果为 Card 168 个、86 `Implemented` / 82 `CatalogOnly`；Ironclad 85 为 9/76，Marine 82 保持 76/6；Effect 共 13 项。3125、4012、4013 与枚举值 5 均由正式生成数据锁定。

精确本地化文本为：

| 单元格 | 文本 |
|---|---|
| B134 | `Exhaust {exhaustCards} card(s). Draw {cards} card(s).` |
| C134 | `消耗 {exhaustCards} 张牌。抽 {cards} 张牌。` |
| B135 | `Exhaust 1 card. Draw 3 cards.` |
| C135 | `消耗 1 张牌。抽 3 张牌。` |

## 6. Localization、Addressables、静态与 Unity 门禁

| 层级 | 结果 | 任务 / 说明 |
|---|---:|---|
| Localization 首轮 | 诊断性失败 | Unity `AssetDatabase` 尚未看见新 Luban 资源，读取 stale config；没有据此修改产品语义。 |
| Localization Import | 通过 | 强制刷新资源后 7.401 秒。 |
| Localization Validate | 通过 | 6.161 秒。 |
| `Sync and Build All` | 通过 | 22.054 秒；其中 Addressables 13.551 秒。 |
| 静态编译 | 通过 | Runtime 0 error / 6 warning；Editor 0 error / 12 warning。首次并行构建仅因两个进程争用输出文件出现 `CS2012`，改为串行后均绿。 |
| 正式行为 A | 9/9 | `c1c48a5d4738462aa8a150d6e614f577`，job 7.182 秒；含 Queue/CardZones/Rules/UI 协议、表现和 formatter。 |
| 正式目录 B | 22/22 | `5310c9f189044261922c4cdc2823ef31`，test 0.724446 秒 / job 2.605 秒。 |
| 正式聚合 C | 172/172 | `54a914c2c66647879fe274dcc384b86d`，test 14.1150631 秒 / job 14.755 秒；包含真实 Addressables AssetBundle 加载。 |
| 完整 EditMode D | 754/754 | `c708030e61834d7dbe3196c6d378f30f`，test 148.9621502 秒 / job 154.155 秒；以 Unity runner 实际发现的 754 项为正式口径。 |
| Console | 0 error | 最终 error 过滤为空。 |

BuildLayout 为 `TinySpire/Library/com.unity.addressables/BuildReports/buildlayout_2026.08.13.17.40.23.json`（134622 bytes）。它证明 `tinyspiregamedata_assets_all_aa609eaff8569429297e832a2721d5a6.bundle`（12085 bytes）使用 `AssetBundleProvider`，并包含生成的 Card / Effect JSON；因此 Fast Mode 或静态检查没有被冒充为真实 AB 证据。

## 7. 验收边界与复审状态

- 本页证明 3125 基础态、通用选择 Effect、出牌前单选协议、选择后抽牌原子事务和现有表现链；不证明任意多选、跨玩家选择、自动选择、执行中暂停选择或新的通用脚本语言。
- 升级 Draw 3、升级 `CardInstance`、其余 76 张 `CatalogOnly`、默认 Deck、奖励、Run、多人、Scene、Prefab、ProjectSettings、asmdef、DI 或构建管线改造均未纳入。
- V2V 的历史验证仍只证明当时的 Vent Heat 切片；Burning Pact 是其共享选择 seam 的后续独立消费者，不回写或改造 Vent Heat 的职业能量语义。
- 最终双轴 production / spec 复审已在 Wiki 闭环后完成：blocker 0、Spec finding 0；唯一非阻塞债务是 Rules 与 Executor 各自解析选择到抽牌语法，现由八场一致性失败测试锁定。
- 复审后把通用 UI 协议测试改为直接读取正式 generated JSON，移除旧的内存夹具覆写；精确复验 `fa417c7d278d463595252e9327b913f2` 为 1/1 passed，生产与正式数据未再变化。
- 本切片未创建提交，也不记录或杜撰提交 SHA。
