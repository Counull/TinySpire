---
title: M6 出牌命令、合法性与目标选择
page_type: plan
lifecycle: archived
date: 2026-08-01
updated: 2026-08-02
scope: TinySpire 出牌目标契约、派生合法性、队首重校验与 BattleScene Self/Enemy 目标交互
source: 用户要求输出一份可由新会话总 Goal 串行实施的 M6 计划
status_source: ../SESSION_LOG.md
depends_on: 2026-07-31-m4-turn-scheduling-energy.md（M4 已完成）、2026-08-01-m5-enemy-intents-deterministic-behavior.md（M5 已完成）
---

# M6 出牌命令、合法性与目标选择

## 当前结论

M6 使用**一份总计划和一个总 Goal**执行，Goal 内按 M6A → M6B → M6C → M6D 串行推进。每个切片必须先满足自己的停止点验收，才能进入下一切片；Goal 可以长期运行，但不能跳过失败、扩大范围或绕过 Unity、Prefab、Addressables 与 Git 安全规则。

M6 不从零创建出牌命令，也不重做阶段、手牌、费用和能量规则。M4 已经建立 `PlayCardCommand`、`BattleCommandQueue.Submit`、只读 `Queue`/`Turn`、提交轮次栅栏、队首执行期重校验，以及成功后扣能量并把指定卡牌实例移入弃牌堆的临时闭环。M6 只在同一根上补充：

- 显式目标意图与 `Self`/`Enemy` 规则。
- 一个不写状态的合法性 module，供 UI 预览与队首权威校验复用。
- 目标在排队期间失效时的零写入失败。
- 当前单玩家 BattleScene 的 Self 自动目标、Enemy 箭头、合法高亮、屏幕命中与费用可用性预览。

M6 成功出牌后仍只扣能量并进入弃牌堆，不读取或执行 `effect_bindings`，不改变目标生命、格挡、力量或状态。真实 Effect 属于 M7；敌人真实行动属于 M8；完整胜负、死亡过渡和最终反馈属于 M9。

最终执行状态：M6A → M6B → M6C → M6D 已按顺序通过各自独立停止点，M6 完成并归档。最终 M6 定向 EditMode **53/53**、全量 EditMode **122/122**、串行 solution build 0 error；Addressables 报告 `BuildError` 为空，Bootstrap 生产路径手牌 5/HUD 3 且无 Error/InvalidKey/VContainer；真实 Game View 的 Self、左右 Enemy、无效释放、费用不足、多分辨率和下一轮恢复全部有物理证据。Standards / Spec 双轴复审完成，唯一硬 finding 为过期文档句子并已修正，Spec 为 0 finding。最终聚焦/弃牌动画与真实 Effect 仍按本计划排除，分别由 M9、M7 承接；证据见 `../06_testing/2026-08-02-m6d-full-validation-review.md`。

## 推荐 Goal 文案

> 完成 TinySpire BattleScene M6。以 `Docs/Copilot_Daedalus/plans/2026-08-01-m6-card-play-legality-target-selection.md` 为唯一实施计划，显式遵守根 `AGENTS.md`、`Docs/Copilot_Daedalus/ARCHITECTURE_CONVENTIONS.md` 以及计划中的范围、停止点和验证要求，按 M6A → M6B → M6C → M6D 串行执行，每个切片满足独立验收后再继续。复用现有 M4 `BattleCommandQueue.Submit`、只读 `Queue`/`Turn`、执行期重校验、提交轮次栅栏和 pending 权威序号关联；加入 Self/Enemy 目标契约、派生合法性预览、队首目标重校验，以及功能性目标箭头、高亮和命中。UI 只组成并提交命令，不扣能量、不移动卡牌、不写参与者或阶段事实。M6 不实现 M7 Effect、伤害、格挡、状态、死亡结算、M8 敌人真实行动、M9 胜负/奖励/最终动画，也不修改 Luban 表、Localization、asmdef、ProjectSettings、HybridCLR、网络、Run 生命周期或 DI 架构。开始前重新读取仓库规则并执行 `git status --short`，保护所有已有改动；优先使用 Unity MCP 生成 Meta 和验证，禁止启动第二个 Editor、结束用户 Unity 进程或删除锁文件。最终完成定向与全量 EditMode、串行构建、Bootstrap 实跑、Addressables 本地内容构建、真实 Game View 拖拽验收、文档同步和 Standards / Spec 双轴复审。未经明确批准不 commit、不 push；若实际需要扩大文件范围、工具被阻塞、测试无法在本切片内安全修复，或物理拖拽手感无法验证，停止并准确报告，保持 Goal 未完成。

## 无人值守执行规则

1. Goal 启动后先重新读取根 `AGENTS.md`、本计划、`../ARCHITECTURE_CONVENTIONS.md`、`../CODE_DECISIONS.md` 中 CD-027～CD-031，以及 M4/M5 最终验收；随后执行 `git status --short` 并记录全部已有改动。
2. 严格按 M6A → M6B → M6C → M6D 执行。每步完成对应自动检查、停止点验收和文档同步后，才能继续下一步。
3. 不改变 `BattleCommandQueue.Submit` 与只读 `Queue`/`Turn` 这一外部写入 seam；不得增加 UI 直通写入口、第二条出牌执行链或网络专用写链。
4. 测试失败时只修复当前切片引入的问题，不借机重构 M4/M5、VContainer、程序集、启动流程、场景结构或 Addressables 架构。
5. 新增函数必须有中文注释；测试名称与断言优先描述经公开 interface 可观察的行为。
6. 不安装替代包、不启动第二个 Unity Editor、不结束用户进程、不删除项目锁文件，也不清理 `Library`、`Temp` 或用户已有改动。
7. Prefab/Meta 修改优先经 Unity MCP 完成。Unity 正在编译或重载时等待并重试，不强制结束进程。
8. M6 不预计修改 `DataTables/Datas/` 或 Localization。若实现发现必须改表、生成配置或本地化资源，视为超出本计划并停止请求确认。
9. 目标箭头、屏幕命中和回弹必须在真实 Game View 用物理指针确认；自动事件调用和静态 Prefab 检查不能冒充手感验收。
10. 不因用户暂时离线而 commit、push、清理、还原或覆盖文件。最终先展示 review package，等待用户决定是否提交。

## 目标

- 在现有 `PlayCardCommand` 中加入可空的 `TargetId`，但不携带费用、目标规则、目标生命或 UI 预判结果。
- 当前 M6 支持的 `Self` 与 `Enemy` 都要求命令在执行时拥有显式目标；Self 由 UI 自动填入 `ActorId`，Enemy 由玩家命中一个合法敌人产生。
- 集中派生阶段、玩家、卡区、手牌、模板、费用、能量、战斗可继续性、目标规则、目标存在、阵营与存活合法性，不保存 `CanPlayCard` 或合法目标列表的可变镜像。
- UI 预览和队首执行读取同一规则 module；预览只影响交互、费用颜色、箭头和高亮，不能替代执行期重校验。
- 目标在提交后、到达队首前死亡或变得非法时，命令执行失败，能量、卡区、阶段、生命和目标事实全部保持原值。
- 保留 M4 的权威排序、展示屏障、轮次栅栏和按权威序号恢复 pending 卡牌的行为。
- Self 卡越线松手即可提交自身；Enemy 卡越线后进入瞄准，只有释放在合法存活敌人上才提交。
- 用现有世界角色 View 的 `SpriteRenderer.bounds` 投影屏幕矩形完成第一版命中，不增加 Collider、Physics2D Raycaster、场景名查找或第二套参与者 View 注册表。
- 在 M6C 实现并通过 Game View 验证后解决 `DEP-001`；`DEP-002` 保持 M4 已解决状态，`DEP-003`、`DEP-004`、`DEP-010` 继续 open。

## 明确排除

- 不执行 `Card.EffectBindings`，不造成伤害、不增加格挡、不修改力量、不施加易伤或其他状态，不生成完整结算记录。
- 不实现 `AllEnemy`、`RandomEnemy`、多目标集合、目标重选、链式目标、召唤目标或通用目标 DSL。
- 不实现命令执行中途暂停等待玩家输入、选择 token、取消、超时或续接协议；`DEP-010` 不在 M6 解决。
- 不让敌人意图执行真实 Effect，不改变 M5 当前意图选择和 Encounter 行动顺序。
- 不新增可变 `BattleOutcome`/`BattleEnded` 镜像，不实现胜利、失败、奖励、重开、死亡动画或场景退出。
- 不完成 M9 的最终不可用灰化、飞牌轨迹、伤害数字、受击反馈或美术级目标动画；M6 只交付功能性箭头、高亮、费用颜色与回弹反馈。
- 不接入第二名生产玩家，不修改 `DEP-008`，不实现网络、Host 确认、广播、重放或断线恢复。
- 不修改 Luban 工作簿、生成枚举、正式卡牌表、Localization、asmdef、ProjectSettings、HybridCLR、Run 生命周期或启动流程。
- 不修改角色 Prefab、增加 Collider 或 Physics Layer；不为了命中改写摄像机或 `BattleScene.unity`。

## 现有基础与接入位置

| 现有事实或 module | 当前能力 | M6 处理方式 |
|---|---|---|
| `BattleCommandQueue` | 唯一提交 seam、权威序号、串行队首、展示屏障、轮次栅栏 | 保持外部 interface，不新增写入口 |
| `BattleTurnController.TryPlayCard` | 校验阶段、玩家、卡区、手牌、模板、费用与能量；成功后弃牌并扣能量 | 复用现有顺序，在首次写入前加入目标与派生终止校验 |
| `PlayCardCommand` | `ActorId + CardId` | 增加 `CombatantId? TargetId`，调用方不传规则结果 |
| `BattleCombatantsData` | 唯一 `CombatantId → CombatantData` 映射 | 派生玩家/敌人、存活候选和双方是否仍有存活者 |
| `battle.Card.TargetRule` | 正式数据已有 `Self` 与 `Enemy` | 不改表；执行期读取模板，不信任 UI |
| `HandCardContainer` | 拖拽、越线、提交、pending 序号和失败回弹 | 加入预览、Self 自动目标、Enemy 瞄准与目标提交 |
| `BattleParticipantPresenter` | 持有 `CombatantId → world view/HUD`，按 Encounter 创建敌人 | 深化为目标 View seam，负责屏幕投影、命中与高亮 |
| `ParticipantHudView` | 已把角色 bounds 投影为名称、生命、力量和意图 HUD | 增加纯表现目标高亮，不保存玩法合法性 |
| `BattleCommandPresentationAdapter` | 按权威序号发布 queued/failed/completed，失败含原因 | 继续复用，不建立第二条目标结果流 |

当前 `ROADMAP.md` 中“越线即删除”的描述已被 M4D 取代；M6 不是替换占位删除，而是把现有无目标命令升级为带目标的完整意图。`DEP-002` 也已由 M4D 解决，M6 不重复实现能量系统。

## PlayCardCommand 目标契约

M6 命令形状：

```text
PlayCardCommand
  ActorId
  CardId
  TargetId?
```

interface 约束：

- `ActorId` 与 `CardId` 继续使用现有运行时标识，不改名为模板 ID 或 UI 索引。
- `TargetId` 表达调用方选择的单个运行时参与者，不携带 GameObject、Transform、目标类型、阵营、存活或合法性布尔值。
- `TargetId` 可空是为了让缺失目标作为可观察的执行期失败，并为未来非单目标规则保留形状；当前 M6 的 Self/Enemy 成功命令都必须有值。
- Self 由生产 UI 自动传 `ActorId`，不要求玩家点击自己；Enemy 由屏幕命中返回一个 `CombatantId`。
- 结构无效的非空 `TargetId` 在构造时拒绝；“是否需要目标、目标是否属于规则”必须等命令到队首按当时事实判断。
- M6A 可以用默认空目标暂时保持既有调用方可编译，但这只是分切片迁移手段，不是最终兼容旁路。M6B 更新权威测试调用，M6C 更新生产 UI 调用；M6C 停止点前必须移除默认值，使所有生产与测试调用显式表达目标。

## 合法性 module、失败顺序与写入权

M6 建立一个具体、纯 C#、不持有可变玩法镜像的 `BattleCardPlayRules` module（最终文件名可按现有目录规范微调）。它集中读取当前 `Turn`、参与者、玩家卡区、静态 `Tables` 与 Encounter 顺序，并通过一个小 interface 返回本次评估结果；不为单一实现额外创建 `ICardPlayValidator`、`ITargetResolver` 或 adapter。

评估结果至少能表达：

- 当前具体命令的 `FailureReason` 与是否可成功。
- 卡牌静态 `TargetRule`。
- 当前是否有能力开始交互，以及费用当前是否可支付。
- 按稳定顺序派生的合法目标 ID；结果是一次性只读快照，不是第二份响应式状态。

UI 可以重复读取该结果来刷新预览；重复读取不得修改能量、卡区、生命、回合、随机流或目标历史。队首执行使用同一规则 module，并在结果成功后才进入现有扣能量/弃牌写入。

队首失败优先级保持可预测：

1. `BattleCommandQueue` 先执行现有提交轮次栅栏；跨轮仍返回 `PlayerActionWindowExpired`。
2. 当前阶段必须为 `PlayerAction`。
3. `ActorId` 必须是本局存活玩家，且尚未结束行动。
4. 双方必须仍各有至少一名存活参与者；这里只派生“当前不能继续出牌”，不创建胜负事实或流程。
5. 必须能解析该玩家卡区，且 `CardId` 仍在该玩家手牌。
6. 静态卡牌模板、非负费用和当前能量必须合法。
7. `TargetRule` 必须是 M6 支持的 `Self` 或 `Enemy`。
8. `TargetId` 必须存在；对应参与者必须属于本场且仍存活。
9. Self 必须满足 `TargetId == ActorId`；Enemy 必须是 `EnemyCombatantData`。

建议新增稳定失败原因：

```text
BattleAlreadyEnded
TargetRequired
TargetNotFound
TargetNotAlive
TargetRuleMismatch
UnsupportedTargetRule
```

任一失败都只能产生 `BattleCommandExecutionResult`，不得发布新的 Turn/卡区/生命事实。成功仍只扣一次当前玩家能量，并只把指定运行时卡牌实例移入弃牌堆；目标生命在整个 M6 保持不变。

## 派生预览与 UI 写入限制

- `CanPlayCard`、费用是否可支付、合法目标集合和高亮状态全部按当前事实即时派生，不写入 `BattleTurnData`、`CombatantData`、卡牌实例或新的响应式玩法状态。
- 费用不足仍使规则 `CanStartInteraction=false` 并显示不可支付颜色；UI 可以允许不会提交的视觉拖动，但出牌反馈、目标选择、resolver 与队列提交继续要求规则许可。不能把目标无效等其他失败伪装成费用不足或一并放开输入。
- 玩家阶段、能量、卡区或参与者生命变化时重新派生卡牌可用性。参与者集合在当前战斗中稳定，可订阅各自 Health，但不得维护第二份“存活敌人列表”。
- UI 在提交前可以拒绝明显无效的松手，不为其分配权威序号；一旦提交被接受，最终结果只认队首校验。
- UI 不调用 `DiscardFromHand`、不扣能量、不写生命、不推进阶段，也不缓存 `TargetRule` 或目标生命作为权威事实。
- 执行失败继续通过现有 `BattleCommandPresentationAdapter` 与 `BattleTurnHud` 展示枚举原因，并由匹配权威序号的 pending 关联恢复卡牌；M6 不新增 Localization 文本链。

## Self / Enemy 目标交互与命中方案

### Self

1. 保留当前按下不跳、指针增量跟手、扇形填空和越线反馈。
2. 越线松手时由 UI 自动构造 `TargetId = ActorId` 并提交。
3. 不显示敌人箭头或敌人高亮。
4. 未越线、当前不可出牌或命令未被接受时回到原手牌排布。

### Enemy

1. 越线前继续沿用 `PointerEventData.delta / Canvas.scaleFactor` 跟手，禁止改回会跳到 `(0, 0)` 的绝对坐标路径。
2. 首次越线后进入临时瞄准态：卡牌保持在越线位置，箭头端点继续跟随 `PointerEventData.position`。
3. 全部合法存活敌人显示普通高亮；当前命中目标显示强化高亮。
4. 松手命中合法目标时提交该敌人的 `CombatantId`；松手在空白、玩家、死亡或非法敌人上时不提交，清理箭头/高亮并回弹。
5. 阶段变化、卡牌 View 重建、对象销毁、命令提交失败或执行失败时，都必须清理当前瞄准表现；pending 仍只锁对应卡牌。

### 屏幕命中

`BattleParticipantPresenter` 已经拥有唯一的 `CombatantId → world view/HUD` 映射，因此 M6 在该 module 内深化目标 View 行为，不再创建第二套注册表：

```text
BeginTargetSelection(legalTargetIds)
UpdateTargetSelection(pointerScreenPosition) -> CombatantId?
EndTargetSelection()
```

具体约束：

- 从现有角色 `SpriteRenderer.bounds` 通过当前 Camera 投影屏幕矩形，只交换屏幕坐标，不直接比较不同 Canvas 的 `anchoredPosition`。
- 命中只考虑传入的合法目标 ID；死亡或未创建完成的 View 不得命中。
- 多个屏幕矩形重叠时，先选指针到矩形中心距离最近者；距离相同按 `EnemyCombatantIdsInEncounterOrder` 决定，禁止依赖字典枚举。
- 第一版矩形命中允许基于 Sprite bounds 做适量序列化 padding；不做像素 alpha 精确命中。
- 不增加 Collider、Physics2D Raycaster、透明射线目标、场景名查找或角色 Prefab 身份脚本。
- `ParticipantHudView` 的目标高亮默认隐藏且所有 Image `raycastTarget=false`，不得遮挡名称、生命、力量和意图。
- 功能性箭头 Overlay 必须不接收 Raycast，不得中断 `OnDrag`/`OnEndDrag`。

## 战斗终止边界

M6 只从 `BattleCombatantsData` 即时派生“双方是否仍各有存活参与者”，用于拒绝已经没有对手或没有存活玩家时的出牌；不新增可变 `BattleOutcome`、终止阶段、结果发布、胜败面板、重开或 Run 写回。

M9 引入正式胜负事实后，应把该事实接入同一个合法性 module，而不是在 UI 另加禁用开关。M6D 不能把当前派生栅栏宣称为完整战斗结束闭环。

## Open Question

当前没有阻塞 Goal 启动的架构问题。箭头颜色、线宽、目标矩形 padding 与普通/悬停高亮色属于 M6C 可序列化表现参数，以 1～3 敌人和多分辨率 Game View 验收收口，不上升为玩法事实。

若实施发现功能性反馈必须新增正式美术、Localization、修改 `BattleScene.unity`、角色 Prefab、摄像机或 Physics 设置，则超出本计划的最小方案，必须停止并请求用户确认，不能在无人值守 Goal 中自行扩大。

## 分步实施

### M6A · 目标契约与纯合法性 module

状态：**已完成并通过独立验收（2026-08-01）**。`PlayCardCommand` 已加入可空 `TargetId` 与构造期非空标识校验；新增具体纯 C# `BattleCardPlayRules`，从当前 Turn、参与者、玩家卡区、静态 Tables 与 Encounter 顺序派生 Self/Enemy、费用、战斗可继续性及稳定合法目标快照。规则 EditMode **8/8**、相关 M4 队列/回合基线 **26/26** 通过，串行 solution build 0 error、保留 12 条既有依赖 warning；验收见 `../06_testing/2026-08-01-m6a-card-play-rules.md`。本切片未修改队列执行、`BattleTurnController`、场景、Prefab、配置、卡区写入或 Effect；M6B 尚未实施。

实施：

1. 扩展 `PlayCardCommand` 为 `ActorId + CardId + TargetId?`；本切片允许默认空目标维持既有调用方编译，完整显式迁移分别由 M6B/M6C 收口。
2. 增加目标、战斗终止与未知规则的明确执行失败原因。
3. 建立具体纯 C# `BattleCardPlayRules` module 与不可变评估结果，不增加抽象 adapter 或写入口。
4. 复用 `BattleCombatantsData`、玩家卡区、`Tables`、`Turn` 和 Encounter 顺序派生 Self/Enemy 规则、费用可支付性与合法目标。
5. 增加纯规则测试，覆盖重复读取零写入和稳定候选顺序。

停止点：

- Self 仅接受玩家自身目标；Enemy 仅接受本场存活敌人。
- 缺失目标、未知目标、死亡目标、错误阵营、未知规则、能量不足和派生战斗结束都有稳定结果。
- 合法目标不依赖参与者字典枚举；重复预览不推进任何状态或随机流。
- `BattleCommandQueue.Submit`、`Queue`、`Turn` interface 未改变。
- 未修改队列执行、场景、Prefab、配置、卡区写入或 Effect。
- 对应定向 EditMode 与相关程序集静态编译通过后停止，记录 M6A 验收，再进入 M6B。

### M6B · 队首目标重校验与权威写链

状态：**已完成并通过独立验收（2026-08-01）**。`BattleTurnController.TryPlayCard` 已在首次权威写入前复用 M6A `BattleCardPlayRules`；目标排队后死亡会按当前事实失败且 Turn、卡区、生命对象和值不变，合法 Self/Enemy 只扣一次能量并只移动一次指定卡牌，Enemy 不提前执行 Effect。相关 EditMode **60/60**、串行 solution build 0 error；验收见 `../06_testing/2026-08-01-m6b-queue-head-target-revalidation.md`。本切片未修改队列公共 seam、场景、Prefab 或生产 UI；在 M6B 独立停止点尚未进入 M6C，后续切片现已完成。

实施：

1. 让现有 `BattleTurnController.TryPlayCard` 在首次写入前调用 M6A 同一规则 module。
2. 保持队列提交轮次栅栏优先于重新进入阶段模块。
3. 全部规则成功后才执行现有扣能量和弃牌；失败只产生执行结果。
4. 扩展队列测试工厂，使测试卡可显式配置 `TargetRule`，不再把全部卡硬编码为 Self。
5. 更新现有命令与 presentation 测试中的构造调用，使测试全部显式传目标；生产 UI 的显式迁移留给 M6C。

停止点：

- 目标在排队或展示等待期间死亡/失效时，队首失败且 Turn、卡区、生命对象和值均不变化。
- 合法 Self/Enemy 命令只扣一次能量、只移动一次指定卡牌；Enemy 生命保持不变。
- 跨轮旧命令仍优先 `PlayerActionWindowExpired`，不会因新一轮目标重新合法而执行。
- 费用透支、卡牌离手、死亡玩家、错误玩家、另一玩家排队、旧展示回调和 M5 敌人意图回归继续通过。
- 未修改场景或 Prefab；M6B 定向 EditMode 与相关程序集静态编译通过后停止，记录验收，再进入 M6C。

### M6C · Self / Enemy 目标选择 UI

状态：**已完成并通过独立停止点（2026-08-02）**。生产 UI 复用 M6A 规则并显式提交 Self/Enemy 目标；功能性箭头、稳定屏幕命中、合法/悬停高亮、无效释放回弹及生命周期清理已通过真实 Game View 审阅，16:7、16:9、16:10、16:11、16:14 均正常。审阅提出的最终聚焦/弃牌动效已记录到 M9 文档；LXX-6 后续完成四张目标 PNG 的独立美术交付，但其回复也明确资源接线属于后续 M9，不纳入 M6，真实 Effect 仍属于 M7。本期只把费用不足改为“保持红色且可视觉拖动，但不进入反馈、瞄准、resolver 或 Submit”。独立审计补齐 `Playable → VisualOnly` 与真实 `CardZones → Turn` 发布顺序后，纯 transition seam 统一收敛保留/取消、排除重排、降级与清理决策。定向 EditMode **53/53**、串行 solution build 0 error、`git diff --check` 通过；用户已在真实 Game View 复测费用不足卡的跟手、无瞄准/提交和释放回弹，Console 无错误。`DEP-001` resolved，M6C 停止点完成，现串行进入 M6D。证据见 `../06_testing/2026-08-01-m6c-self-enemy-target-selection.md`。

实施：

1. `HandCardInteraction.OnEndDrag` 把 `PointerEventData` 交给容器，保留现有 delta 跟手路径。
2. `HandCardContainer` 消费 M6A 派生结果，刷新可交互性、费用颜色、合法目标与提交目标。
3. Self 越线自动提交自身；Enemy 越线进入瞄准并在合法屏幕目标上释放后提交。
4. 深化 `BattleParticipantPresenter` 的屏幕矩形命中与高亮行为；`ParticipantHudView` 只负责表现。
5. 在 `BattleHandUI.prefab` 接入不接收 Raycast 的功能性箭头 Overlay；在 `ParticipantHudView.prefab` 接入默认隐藏的目标高亮。
6. 移除 `PlayCardCommand` 目标参数默认值，使生产与测试调用全部显式传目标；沿用现有权威序号 pending 与失败恢复，不增加目标专用结果流。

停止点：

- Self 不要求点击玩家；Enemy 在 1～3 名敌人布局中只高亮合法存活目标并提交精确 ID。
- 空白、玩家、死亡目标或非法目标释放不产生权威序号，卡牌回弹且权威事实不变。
- 能量不足显示不可支付费用颜色；阶段、能量、卡区与生命变化后预览即时重派生。
- 队首目标失效的执行失败只恢复匹配序号卡牌，不解锁更新的 pending 意图。
- Scene、角色 Prefab、`CardView.prefab`、ProjectSettings 和 Physics 设置均未修改；若实际必须修改，停止请求确认。
- Unity MCP 定向 UI/规则/队列测试通过；Addressables 本地内容构建成功；Bootstrap Console 无 Error/InvalidKey/VContainer 错误。
- 真实 Game View 完成 Self、左右敌人、空白回弹、费用不足、分辨率与残留高亮验收后，才把 `DEP-001` 标记 resolved。

### M6D · 全量验证、复审与文档收口

状态：**已完成并通过最终验收（2026-08-02）**。M6 定向 EditMode **53/53**、全量 EditMode **122/122**，均为 0 failed、0 skipped；串行 solution build 0 error、保留 12 条既有依赖 warning。Addressables 本地内容重建 `BuildError` 为空、`BuildResultHash=2f21014862b879079e277deb7b7d1cbb`；Bootstrap 从生产链进入 BattleScene，手牌 5、HUD 3，无 Error/InvalidKey/VContainer。真实 Game View 使用 M6C 完整物理序列与最终费用不足修订复测的累计证据覆盖全部要求。Spec 轴 0 finding；Standards 的过期文档 finding 已修正，两个判断性气味按后期路线与深模块边界处置。验收见 `../06_testing/2026-08-02-m6d-full-validation-review.md`。

实施与验证：

1. 运行 M6 规则、命令队列、presentation、手牌目标、屏幕命中和 Prefab 合约定向 EditMode。
2. 运行全量 EditMode，0 failed、0 skipped。
3. 串行执行 `dotnet build TinySpire/TinySpire.sln --no-restore -m:1`，记录 error 与既有 warning。
4. 执行 `TinySpire/Addressables/Build Local Content`，确认报告 `BuildError` 为空，BattleScene 与相关 Prefab 的完整稳定地址仍有效。
5. 从 Bootstrap 实跑 Self、Enemy、无效释放、结束行动与下一轮恢复；通过测试夹具验证队首前目标失效，不把夹具写回资产。
6. 在真实 Game View 物理拖拽确认箭头端点、目标矩形、高亮、回弹、手牌重排和至少两个分辨率；自动事件不能替代。
7. 分别执行 Standards 与 Spec 双轴复审，只修复 M6 内 finding，不扩大到 M7～M9。
8. 更新本计划切片状态、`../SESSION_LOG.md`、`../CODE_DECISIONS.md`、`../DEPENDENCIES.md`、`../ROADMAP.md`、`../06_testing/` 与其 README；M6 完成后把计划移入历史区。

停止点：

- 自动验证、Addressables、Bootstrap、Game View 和双轴复审全部有真实证据。
- `DEP-001` 已由实现与 Game View 验证解决；`DEP-003`、`DEP-004`、`DEP-009`、`DEP-010`、`DEP-011` 保持原状态。
- 计划、状态源、决策源、依赖表和验收页没有互相矛盾的 M6 状态。
- 没有实现 Effect、目标伤害、胜负、最终动画或其他后续里程碑内容。
- 最终先展示 review package；未经用户明确确认不 commit、不 push。

## TDD 测试 seam

权威行为测试继续通过生产相同的外部 seam：

```text
Submit(command)
  → Queue 等待/推进
  → 队首执行
  → presentation result
  → 只读 Turn + CardZones + Combatants 断言
```

不直接调用 `BattleTurnController.TryPlayCard`，不断言私有队列容器，也不以“helper 被调用一次”替代行为验证。纯合法性 module 与纯屏幕命中计算可分别通过自身小 interface 测试，但队首原子性必须回到 `BattleCommandQueue.Submit` 验证。

首要规则/队列用例：

1. Self 目标为 Actor 时成功；目标为其他参与者时失败零写入。
2. Enemy 目标为存活敌人时成功；空目标、玩家目标、未知目标与死亡目标失败零写入。
3. 未知 `TargetRule` 显式失败，不随机回退或按 Enemy 猜测。
4. 双方不再各有存活参与者时，派生为不可出牌，但不创建胜负状态。
5. 目标提交时存活、队首时死亡，执行失败且能量、卡区、生命与阶段对象保持原值。
6. 目标失败后队列仍按唯一权威序号推进，并向 presentation 发布同一序号失败。
7. 两条基于旧能量提交的命令仍在队首重新校验，后者不能透支。
8. 跨轮命令即使卡牌和目标重新合法仍失败。
9. Enemy 卡 M6 成功后目标生命保持不变，证明未提前执行 Effect。
10. UI 预览重复读取、语言变化、View 重建和目标高亮不改变权威事实。

首要 UI/Prefab 用例：

1. Self 越线提交自身；未越线不提交。
2. Enemy 合法目标释放提交精确 ID；空白、玩家与非候选目标不提交。
3. 屏幕矩形重叠时先按中心距离、再按 Encounter 顺序稳定选择。
4. View 尚未异步创建完成时目标选择安全失败并回弹，不出现空引用。
5. 阶段变化、销毁和提交失败都清除箭头与高亮。
6. 费用足够/不足颜色随当前能量重派生；目标无效不能错误显示为费用不足。
7. Overlay 与目标高亮默认隐藏、`raycastTarget=false`，且不遮挡 HUD。
8. 旧失败反馈不得清除同一卡牌更新的 pending 权威序号。

## 预期文件范围

M6A/M6B 领域与队列：

- `TinySpire/Assets/Scripts/Battle/Commands/BattleCommand.cs`
- `TinySpire/Assets/Scripts/Battle/Commands/BattleCommandResults.cs`
- `TinySpire/Assets/Scripts/Battle/Commands/BattleCommandQueue.cs`（只做必要接线，外部 interface 不变）
- `TinySpire/Assets/Scripts/Battle/Turn/BattleTurnController.cs`
- `TinySpire/Assets/Scripts/Battle/` 下一个具体纯合法性 module 与必要不可变结果类型
- `TinySpire/Assets/Editor/Tests/BattleCommandQueueTests.cs`
- `TinySpire/Assets/Editor/Tests/BattleCommandPresentationAdapterTests.cs`
- `TinySpire/Assets/Editor/Tests/` 下对应纯规则测试

M6C UI 与 Prefab：

- `TinySpire/Assets/Scripts/UI/Battle/Hand/HandCardInteraction.cs`
- `TinySpire/Assets/Scripts/UI/Battle/Hand/HandCardContainer.cs`
- `TinySpire/Assets/Scripts/UI/Battle/Hand/HandCardVisual.cs`
- `TinySpire/Assets/Scripts/UI/Battle/BattleParticipantPresenter.cs`
- `TinySpire/Assets/Scripts/UI/Battle/ParticipantHudView.cs`
- `TinySpire/Assets/Scripts/UI/Battle/` 下功能性目标箭头/屏幕命中纯展示文件
- `TinySpire/Assets/Prefabs/UI/Battle/Hand/BattleHandUI.prefab`
- `TinySpire/Assets/Prefabs/UI/Battle/Targeting/` 下一个目标箭头 Overlay Prefab
- `TinySpire/Assets/Arts/Runtime/Prefabs/ParticipantHudView.prefab`
- `TinySpire/Assets/Editor/Tests/` 下目标交互、屏幕命中和 Prefab 合约测试

文档：

- `Docs/Copilot_Daedalus/ROADMAP.md`
- `Docs/Copilot_Daedalus/SESSION_LOG.md`
- `Docs/Copilot_Daedalus/CODE_DECISIONS.md`（实施后追加，不在计划轮预写）
- `Docs/Copilot_Daedalus/DEPENDENCIES.md`
- `Docs/Copilot_Daedalus/plans/2026-08-01-m6-card-play-legality-target-selection.md`
- `Docs/Copilot_Daedalus/plans/README.md`
- `Docs/Copilot_Daedalus/06_testing/`（实施后新增）

以下文件不在预期范围：

- `DataTables/Datas/`、`TinySpire/Assets/GameData/` 与生成配置代码。
- `TinySpire/Assets/Scenes/BattleScene.unity`。
- `TinySpire/Assets/Arts/Runtime/Card/Prefab/CardView.prefab`。
- 角色 Prefab、ProjectSettings、asmdef、HybridCLR、Run/网络/启动流程。

若实际实现必须超出以上排除范围，先停止并说明预计文件、风险和回滚单位，等待用户确认。

## 依赖项与文档收口

- `DEP-001`：计划轮保持 open；M6C 使用 Sprite bounds 屏幕投影完成目标命中并通过 Game View 后才能 resolved。
- `DEP-002`：已由 M4D resolved，M6 只做回归，不重新打开。
- `DEP-003`：最终越线视觉样式仍 open；M6 功能性反馈不等于美术定稿。
- `DEP-004`：打出后按效果类型区分的过渡仍等待 Effect，保持 open。
- `DEP-009`：敌人真实 Effect 仍等待 M7/M8，保持 open。
- `DEP-010`：命令中途局部选择仍 open；M6 只在 Submit 前选择目标。
- `DEP-011`：网络权威确认与重放仍 open。

M6 每个切片完成时更新本页状态与 `SESSION_LOG.md`，并在 `06_testing/` 写对应验收页。代码决策只有在实现实际锁定后才追加到 `CODE_DECISIONS.md`；计划 proposal 不能冒充已实施决策。

## 风险与回滚

| 风险 | 控制方式 | 回滚单位 |
|---|---|---|
| 把 M4 已完成合法性重写成第二套系统 | 提取/复用一个规则 module，保留 `Submit`/`Queue`/`Turn` interface | M6A 规则文件与最小调用接线 |
| UI 预览被误当权威结果 | 队首总是按当前事实重校验；目标失效测试证明零写入 | M6B 队列接线 |
| 目标规则或合法目标成为第二份状态 | 每次从 Turn、卡区、Tables、Combatants 与 Encounter 顺序派生 | M6A 评估结果 |
| 目标命中要求改角色/Physics/Scene | 使用现有 SpriteRenderer bounds 屏幕投影；超出范围即停 | M6C Presenter/HUD/Overlay |
| 两种 Canvas 坐标混用导致偏移 | module 之间只交换屏幕坐标；Game View 多分辨率验收 | M6C 屏幕命中 |
| Overlay 截断拖拽事件 | 所有箭头/高亮 Graphic 均 `raycastTarget=false`，加 Prefab 合约测试 | M6C Prefab |
| View 异步加载未完成产生空引用 | 无可用目标 View 时安全拒绝选择并回弹 | M6C Presenter |
| 目标排队期间死亡仍扣费弃牌 | 首次权威写入前队首重校验并断言快照对象不变 | M6B 队列测试 |
| Self/Enemy 命令提前执行 Effect | M6 成功后目标生命/格挡/状态保持不变 | M6B/M6D |
| 功能性高亮被误称最终反馈 | M9 动画与美术明确排除，`DEP-003/004` 保持 open | M6C/M6D 文档复审 |
| 自动测试冒充真实鼠标手感 | M6D 必须物理 Game View 拖拽；缺失则 Goal 不完成 | M6D 验收 |

每个切片都是独立回滚单位。禁止广泛还原、`git reset --hard`、`git clean` 或覆盖用户未提交改动。

## 完成定义

M6 完成不等于卡牌已经产生真实 Effect。完成标准是：现有 `PlayCardCommand` 携带并在队首重校验显式 Self/Enemy 目标；所有合法性由当前权威事实和静态模板派生；提交后目标失效会零写入失败；Self 与 Enemy 在当前单玩家 BattleScene 通过功能性费用预览、箭头、高亮和屏幕命中提交正确 `CombatantId`；UI 仍只经 `BattleCommandQueue.Submit` 写入；M4/M5 回归、定向与全量 EditMode、串行构建、Addressables、Bootstrap、真实 Game View、文档和双轴复审全部完成。

目标生命、格挡、状态、敌人真实行动、完整胜负和最终动画仍保持不变，分别等待 M7～M9。最终交付先展示 review package；未经用户明确确认，不创建 commit，不 push。
