---
title: Marine Game 机枪兵 V2V 排气散热与共享手牌单选协议
page_type: testing
lifecycle: active
date: 2026-08-13
updated: 2026-08-13
status: verified-unity-native-2026-08-13
status_source: ../SESSION_LOG.md
source:
  - ../00_inbox/README web.md
  - ../01_requirements/2026-08-12-machine-gunner-v2-requirement-digest.md
related_plan: ../plans/2026-08-12-machine-gunner-v2-card-runtime-plan.md
related_decision: ../CODE_DECISIONS.md#cd-102v2v-排气散热以共享手牌单选协议和原子卡区事务执行
---

# Marine Game 机枪兵 V2V 排气散热与共享手牌单选协议

本页记录 `VentHeat` (3244) 基础态、共享手牌单选命令/规则/卡区/UI seam、双 transient 表现、正式作者表与本地化差异，以及 Luban、Addressables、静态编译和 Unity 原生验收证据。

## 1. 验收对象与冻结行为

- Program 44 基础态为 0 Energy、Skill、Self、Hand→DiscardPile。若来源之外存在另一张合法手牌，命令必须精确选择一个当前实例；空选择、多个选择、选中来源或陈旧实例均不得执行。
- 正常成功顺序固定为 `EnergySpent(0) → selected HandToExhaust → EnergyGained(1) → source HandToDiscard`。所选牌先进入 ExhaustPile，实际能量按 `EnergyMaximum` 裁剪，来源牌最后进入 DiscardPile。
- 来源是唯一手牌时不创建选择请求：仍提交 `EnergySpent(0)` 和来源弃置，但不增加能量。能量已满时仍消耗选择牌并弃置来源，但不会伪造 `EnergyGained`。
- 选择、Layout、Turn、Queue 或 owner 快照在首次写入前失效时，Turn、资源、卡区和 settlement 均保持不变；已提交的非法命令仍产生 typed failure lifecycle，UI 自身检测到漂移则在提交前取消。合法重试使用当前事实重新准备。

## 2. 共享选择 seam 与权威事务

| 层 | 已验收职责 |
|---|---|
| `PlayCardCommand.SelectedCardIds` | 以不可变实例 ID 集合把玩家选择带入权威命令；不保存 UI 对象，也不自行解释卡牌规则。 |
| `BattleHandCardSelectionRequest` | 规则层返回 `RequiredCount=1` 与合法其他手牌集合；来源是唯一手牌时无请求。 |
| `BattlePreparedHandCardSelectionResolution` | 在 `BattleCardZonesData` 内联合冻结 owner、起始/最终 Layout、selected→Exhaust 与 source→Discard；Prepare 零写入，Validate 拒绝漂移，Commit 只发布一次 Layout。 |
| `MachineGunnerBattleRuntime` | 把 Program 44、实际 +1 Energy 和共享卡区计划组合到同一职业出牌事务；不新增第二个共享写入入口。 |
| `BattleCommandQueue.Submit` | 继续独占 ordering、drain、continuation、barrier 与 fault；UI 只提交命令，不直接移动卡牌或改能量。 |

CardZones 的两条 `CardMoved` 保持连续顺序，且只在完整计划仍有效时一起提交。跨 owner、重复提交、来源/选择身份错误或布局漂移不会留下半次 Exhaust / Discard，也不会发布中间 Layout。

## 3. Hand UI 会话与双 transient 表现

- `HandCardSelectionSession` 是 UI 局部不可变状态，不进入 `BattleTurnData`。它冻结来源牌、合法候选、Layout、Turn 与 Queue 快照；候选牌左键确认并提交携带 `SelectedCardIds` 的命令，来源牌左键或任意卡右键取消，未知实例点击忽略。
- 选择期间候选和非候选使用独立 `HandCardSelectionPresentationRole`，二者都保持点击 raycast；全部牌禁拖，pending 选择视觉优先于普通不可出牌遮罩。选择确认、主动取消、Layout / Turn / Queue 漂移、容器禁用或销毁都会清除会话和视觉角色，且不会产生额外权威写入。
- 表现计划不制造伪 `CommandPrelude`。真实 settlement 先把 selected transient 路由到 Exhaust，再把 source transient 路由到 Discard；runner 在各步骤转换点按同一顺序清理两个 transient，完成回调只触发一次，表现期间不再写 CardZones。
- 本切片没有修改 Scene 或 Prefab；交互全部接在既有 `HandCardContainer`、`HandCardInteraction` 和 `HandCardVisual` 路径内。

## 4. 逐片 TDD 红绿证据

下表中的 `…` 明确表示历史任务只保留了前缀、完整 ID 未被留存；这些前缀不冒充完整任务号。

| 切片 | 红灯 / fixture-oracle 修正 | 绿灯 |
|---|---|---|
| Program 44 tracer | `6254826d…`：Program 44 尚未受支持。 | `93ae8669…`：唯一手牌直接弃置、无获能。 |
| 命令选择与职业事务 | `b1c01727…`：旧命令没有权威 selected IDs，暴露选择/事务缺口。 | `1a4c4546…`：selected Exhaust、实际获能、source Discard 与有序 settlement。 |
| 规则请求与失败边界 | 无额外 production red；沿用上一切片缺口。 | `f065f275…`：空选择返回精确请求；`7705d5e2…`：非法/陈旧选择零写入；`693da478663d40e5906c03fe85e8e181`：满能仍 Exhaust 且无伪获能。 |
| CardZones 原子双归宿 | 无额外 production red。 | `beeefa05…`：2/2，Prepare/Validate/Commit、单 Layout、跨 owner/漂移/重复提交拒绝。 |
| 纯 UI 会话与 release | 无额外 production red。 | `3a7bf78e…`：纯会话点击解析；`ab4a526d…`：release 进入选择而不提前注册命令。 |
| 候选点击确认 | `7e78d73…`：fixture 未执行 Layout→Rebuild，未指向生产缺陷。 | 修正 fixture 后 `f1e74a5829d746f08fa456b737b04caf`。 |
| 交互与视觉 | `c21b0c939f1945fc80765367b574d389`：容器尚未完成候选/非候选和右键取消契约。 | 交互转发 `d6a01d920498443ab32518797b770fe0`；视觉 `e46df1b0bda846b09b67dabca1433770`；容器角色/取消 2/2 `a92b6214657848d98776cae71fe8183f`。 |
| 漂移与生命周期 | compile red 精确暴露缺少 `RefreshHandCardSelectionFromCurrentFacts(bool reflow)`；首轮 Unity `18e7e29…` 为无定位的 null，诊断 `48eac529…` 证明 EditMode 的 `SetActive(false)` 没有可靠触发生命周期，属于 fixture 调用方式。 | fixture 改为反射调用真实私有 `OnDisable`，生产未因该 oracle 修改；最终 `2902d4a46c9b4a1fbf3b54f914d8ec42` 为 1/1。 |
| 双 transient | `4efba9f2…` 与 `02ab44ce…` 依次暴露浮点位置容差过严；`40011665…` 暴露 runner 在下一步骤转换点才清理 transient 的测试 oracle。 | 仅校准容差/清理时点后 `78bcd00d468c4b19952bb935ba708a77` 为 1/1，生产表现未因三项 oracle 修改。 |

最终行为聚合任务 `86186ac62acd476188b1c67c75443582` 为 **15/15 passed**。它覆盖上述基础态、规则、卡区、UI 选择、生命周期和表现契约；前缀历史用于保留开发轨迹，正式验收以完整任务 ID 的最终门禁为准。

## 5. 数据、本地化与 Addressables

| 项目 | 结果 | 说明 |
|---|---:|---|
| 正式作者表 | 已复核 | 只把 `Sheet1!Q134` 从 `CatalogOnly` 翻为 `Implemented`；SHA-256 `B3BA678FBC0C021F49C3F9FEDE4190099960EE109FFC302D96C77F29D54F4A6D`。 |
| 正式 i18n | 已复核 | 只修改 `Sheet1!B404:C405` 四个文本单元格；SHA-256 `8833E99F546B2C1195C4F0317A1B9208535ED083743F1ABF183874EFFFD23D77`。 |
| Luban / 生成 JSON | 通过 | 2026-08-13 14:55:40；JSON SHA-256 `5988DA20801C8BF724EF0E471466A0A746A5E732DE3450BD7680F00A735F2615`。全项目 168 张为 85/83，Marine 82 为 76/6、V1 60/4、V2 16/2。 |
| 3244 生成元数据 | 通过 | 0E Fixed / Skill / Self / base+upgraded DiscardPile / Program 44 / has upgrade / 空 bindings / 非 Innate。 |
| Localization | 通过 | Import 和显式 Validate 日志均通过。 |
| `Sync and Build All` | 通过 | 本地 Addressables 构建 15.85 秒。 |
| BuildLayout | 已写出 | `Library/com.unity.addressables/BuildReports/buildlayout_2026.08.13.14.59.24.json`，134615 bytes。 |

精确四个本地化文本为：

| 单元格 | 文本 |
|---|---|
| B404 | `Exhaust a card. Gain 1 Energy. If no card can be exhausted, do nothing.` |
| C404 | `消耗一张手牌，获得 1 点能量；若没有可消耗的手牌则无事发生。` |
| B405 | `Exhaust a card. Gain 2 Energy. If no card can be exhausted, do nothing.` |
| C405 | `升级：消耗一张手牌，获得的能量由 1 点提升至 2 点；若没有可消耗的手牌则无事发生。` |

## 6. 正式 Unity 与静态证据

| 层级 | 结果 | 任务 / 说明 |
|---|---:|---|
| V2V 行为 | 15/15 | `86186ac62acd476188b1c67c75443582`，0.5236988 秒。 |
| 正式目录快照 | 38/38 | `6f75f8955c944aae8290934cefe4dc45`，0.6193939 秒。 |
| 正式聚合 | 306/306 | `55d24ae6959f48fbbfc96238b9c1ce16`，15.0028596 秒；包含 CardIllustration 真实 Addressables AssetBundle 加载。 |
| 完整 EditMode | 744/744 | `0bf8b7bf3ffc40c986a55917993894f4`，21.6237739 秒。 |
| Runtime 静态编译 | 0 error / 6 warning | `Assembly-CSharp.csproj --no-restore`。 |
| Editor 静态编译 | 0 error / 12 warning | `Assembly-CSharp-Editor.csproj --no-restore`。 |

静态 warning 仍为既有程序集版本冲突类；本页没有把静态编译冒充 Unity 原生验证，正式结果以上述 Unity 任务为准。

## 7. 验收边界

- 本切片只实现 3244 基础态；升级时获得 2 Energy 仍只是作者表/本地化元数据，没有升级 `CardInstance` 运行时。
- 共享 seam 只证明普通手牌单选、实例传递、原子双归宿和既有 Hand UI 会话可复用；没有实现任意多选、跨玩家选择、自动选择、选择持久化或新的通用效果语言。
- Ironclad `Burning Pact` 可以作为后续独立消费者复用这些 seam，但本切片没有新增其程序、翻转目录状态、改动战士数据或执行战士验收。
- 不加入默认 Deck、奖励、Run、多人、Scene、Prefab、ProjectSettings、asmdef、DI 或构建管线改动，也不实现其他剩余目录卡。
- 本切片未创建提交，也不记录或杜撰提交 SHA。
