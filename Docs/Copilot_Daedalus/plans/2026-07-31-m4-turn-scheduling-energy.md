---
title: M4 回合调度、权威命令队列与每玩家能量
page_type: plan
lifecycle: active
date: 2026-07-31
updated: 2026-08-01
scope: TinySpire 战斗时序层、状态层与 BattleScene M3C 接线
source: 用户确认“多人根基、当前单玩家接线”及“并发提交、权威串行结算”
status_source: ../SESSION_LOG.md
---

# M4 回合调度、权威命令队列与每玩家能量

## 当前结论

M4 先建立整个战斗的调度根和统一命令队列，再让能量、出牌、抽弃牌、敌人行动和后续 Effect 通过同一个 interface 接入。

多人模型不采用“玩家交错执行”或“一张牌后切换当前玩家”。所有尚未结束行动的玩家都可以同时提交命令，提交操作不因其他玩家正在输入或当前命令正在展示而阻塞。权威调度层为已确认命令建立唯一顺序；命令执行、共享状态修改和效果展示仍按该顺序一次只处理一条。

本轮只把当前 BattleScene 的唯一玩家接到该模型，不实现真实网络、服务器或多玩家 UI。当前单玩家限制登记为 `DEP-008`，不能把全局唯一玩家、全局能量或固定玩家轮转写入队列 interface。

外部参照的事实边界见 `../04_research/2026-07-31-slay-the-spire-action-queue.md`：原版《杀戮尖塔》验证行动队列顺序结算；《杀戮尖塔 2》验证多人同时提交与权威排序方向。TinySpire 锁定的是逻辑上的统一权威顺序，不要求未来网络实现必须使用单一物理 FIFO。

## 目标

- 建立统一的 `BattleCommandQueue`，成为玩家、系统阶段和未来敌人/Effect 行动的唯一提交 seam。
- 建立 `BattleTurnController`，只负责阶段、轮次、每玩家能量与行动结束状态，不直接接收 UI 方法调用。
- 区分“提交已接受”和“执行成功”：排队不代表执行时一定合法。
- 允许新命令在当前命令执行或展示期间继续入队；共享事实仍只能由队首命令串行修改。
- 能量按 `CombatantId` 归属；当前每轮重置为 3，3 属于静态规则，不是 UI 常量或英雄模板运行时字段。
- 把出牌、结束玩家行动、敌人行动完成都表达为命令，不允许 UI 或场景脚本绕过队列推进战斗。
- 用纯 C# EditMode 测试验证队列顺序、执行期校验和阶段推进，再单独接入 DI、场景与 UI。

## 明确排除

- 不实现网络传输、Host 权威协议、重放、回滚、预测、断线重连或反作弊。
- 不锁定未来网络层内部使用一个 FIFO，还是每玩家入站队列加统一仲裁；只锁定所有节点最终使用同一权威执行顺序。
- 不实现第二名真实玩家的配置装配、独立牌组或多玩家 UI；见 `DEP-008`。
- 不实现目标选择、真实 Effect、伤害、格挡、状态或卡牌专属动画。
- 不解决命令执行中途需要玩家选择的局部输入/续接协议；当前 M4 命令执行后不得停在半完成的权威状态。
- 不实现敌人意图和行为选择；M4 只提供稳定顺序与命令交接，见 `DEP-009`。
- 不实现胜败和奖励结算，也不借 M4 重构 Presenter、Addressables 或程序集结构。

## 深模块与 seam

### 外部 seam：BattleCommandQueue

生产调用方和测试都只通过统一提交入口使用战斗写链：

```text
Submit(BattleCommand command) -> BattleCommandSubmissionResult
Queue -> ReadOnlyReactiveProperty<BattleCommandQueueData>
Turn  -> ReadOnlyReactiveProperty<BattleTurnData>
```

首批命令：

```text
StartBattleCommand
PlayCardCommand(actorId, cardId)
EndPlayerActionCommand(actorId)
CompleteEnemyActionCommand(enemyId)
```

interface 约束：

- UI 只负责构造并提交意图，不传费用、不扣能量、不移动卡牌、不设置阶段。
- `Submit` 在当前单机接线中分配单调递增的权威序号并入队；未来网络 adapter 可以在命令被 Host 确认后调用同一入队路径。
- 提交期间不等待当前队首命令完成，因此不同玩家以及同一玩家可以积累后续命令。
- `BattleCommandSubmissionResult.Accepted` 只表示命令已进入权威排序，不表示执行成功。
- 队列一次只执行一个命令；当前命令的权威写入与展示完成前，不执行下一条。
- 最终合法性在命令到达队首时校验。失败命令产生明确执行结果，但不得改变能量、卡区、阶段或轮次。
- 内部容器、仲裁算法、状态节点和 `StateMachine<TEvent>` 组合不属于外部 interface，也不是测试 seam。

### 内部模块：BattleTurnController

`BattleTurnController` 是命令执行器使用的阶段模块，不再向 UI 暴露 `TryPlayCard`、`TryEndPlayerAction` 等旁路方法。它通过构造参数接收 `CombatantId -> BattleCardZonesData` 映射；生产当前只提供唯一玩家的一项，测试可以提供多名玩家与多套卡区。

删除命令队列后，提交排序、执行期校验和展示等待会重新散落到 UI、网络和 Effect 调用方，因此该队列具备真实深度；`BattleTurnController` 则把阶段转换与多人结束门槛集中在队列内部。

### 权威快照

`BattleCommandQueueData` 至少包含：

- 当前执行命令的权威序号、类型与提交者；空闲时为空。
- 已确认但尚未执行的命令数量。
- 当前是否正在等待展示完成。

`BattleTurnData` 是一次完整发布的不可变只读快照，至少包含：

- `Phase`
- `RoundNumber`
- `Players`：`CombatantId -> PlayerTurnData`
- `CurrentActingEnemyId`：仅在 `EnemyAction` 阶段有值

`PlayerTurnData` 只保存当前能量与是否已经结束本轮行动。手牌、生命、费用、是否可出牌和队列长度不得复制进该对象。

### 写入权

| 事实 | 唯一所有者 | 合法写入路径 |
|---|---|---|
| 权威命令序号、队首与等待队列 | `BattleCommandQueue` | `Submit` 与队列执行循环 |
| 阶段、轮次、当前行动敌人 | `BattleTurnController` | 仅队首命令执行和内部阶段转换 |
| 每玩家能量、结束行动标记 | `BattleTurnController` | 仅队首命令执行 |
| 生命、力量 | `BattleCombatantsData` / `CombatantData` | 后续 Effect 命令经队列调用 |
| 卡牌实例与四区归属 | `BattleCardZonesData` | 队首命令经阶段模块协调调用 |
| 卡牌费用 | `battle.Card` 静态模板 | 执行期只读；不信任 UI 输入 |
| 每轮基础能量、目标手牌数 | `GameConfig` 静态规则 | 战斗创建时读取，UI 不持有常量 |

## 多人阶段与队列模型

```text
NotStarted
  -> [StartBattleCommand]
  -> BattleStart
  -> PlayerRoundStart
  -> PlayerAction
       |- 玩家 A/B/... 可同时 Submit(PlayCardCommand)
       |- 玩家 A/B/... 可同时 Submit(EndPlayerActionCommand)
       `- 队列按权威序号逐条执行和展示
  -> PlayerRoundEnd        （结束命令均执行且全体玩家已结束）
  -> EnemyRoundStart
  -> EnemyAction           （Encounter 顺序生成/完成敌人命令）
  -> EnemyRoundEnd
  -> RoundEnd
  -> PlayerRoundStart
```

术语约束：

- 一轮（Round）包含一次全体玩家阶段和一次全体敌人阶段。
- 玩家之间没有 `CurrentPlayer`，也没有“一张牌后切人”的调度规则。
- “并发”只描述命令提交；共享状态执行与效果展示不并发。
- 某玩家的 `EndPlayerActionCommand` 只有在执行后才锁定该玩家。排在它后面的该玩家命令到队首时会被拒绝。
- 当前 BattleScene 只有一名玩家，但仍走同一个提交、排队、执行和展示完成流程。

## 规则顺序

### 战斗开始

1. `BattleSession` 最终只创建参与者、卡牌实例与洗牌后的抽牌堆，不预先抽手牌。
2. 启动入口提交唯一的 `StartBattleCommand`。
3. 队列执行该命令后进入 `BattleStart`，再进入 `PlayerRoundStart`。
4. 首轮轮次设为 1，每名玩家能量重置为 3并抽到目标手牌数。
5. 进入 `PlayerAction` 后才允许玩家命令在执行期通过阶段校验。

### 出牌命令

`Submit` 只验证命令结构和队列是否接收新命令。`PlayCardCommand` 到达队首时按以下顺序做权威校验：

1. 当前阶段必须是 `PlayerAction`。
2. `actorId` 必须是本局存活玩家，且尚未执行结束行动。
3. 当前接线必须能解析该玩家的卡区；非当前生产接线返回明确失败，见 `DEP-008`。
4. `cardId` 必须仍在该玩家手牌中。
5. 从静态模板读取费用，执行时的当前能量必须足够。

全部验证成功后才扣能量、移动卡牌并产生本命令的顺序展示步骤。M4 暂以进入弃牌堆作为已提交卡牌的结束位置，不执行真实 Effect；展示 adapter 确认完成后，队列才处理下一条。两个基于同一份旧状态提交的命令可以都被接受，但后执行者若已不合法必须失败且不修改事实。

### 结束玩家行动

1. `EndPlayerActionCommand` 与出牌命令共用同一权威顺序。
2. 命令执行时将该玩家剩余手牌移入弃牌堆，并把结束标记设为真。
3. 仍有其他未结束玩家时保持 `PlayerAction`，并继续处理队列中合法命令。
4. 全部玩家结束后拒绝新的玩家命令，并进入 `PlayerRoundEnd`。
5. 重复结束，或排序在该玩家结束命令之后的出牌命令，执行失败且不能再次弃牌或推进阶段。

### 敌人阶段

1. 敌人顺序只读取 `EnemyCombatantIdsInEncounterOrder`，不依赖字典枚举。
2. 死亡敌人跳过，但不修改 Encounter 顺序事实。
3. 每次只公布一个 `CurrentActingEnemyId`；敌人行为以后也作为命令进入同一队列。
4. M4 当前以系统生成的无行为完成命令结束该敌人行动；M5 替换命令内容，不替换队列和阶段根。
5. 全部敌人完成后进入 `RoundEnd`，再开始下一轮。

## 分步实施

### M4A · 权威命令队列与调度事实骨架

状态：**已完成并通过独立验收（2026-08-01）**。实现保持纯 C# 且未接生产场景；Unity MCP 定向 EditMode 9/9、两套相关程序集静态编译 0 error。详细证据见 `../06_testing/2026-08-01-m4a-authoritative-command-queue.md`。M4B、M4C 已于同日完成，M4D～M4E 尚未开始。

范围：新增但尚不接入生产场景的纯 C# 根，不改变当前 BattleScene。

- 新增 `BattleCommand`、`BattleCommandSubmissionResult`、`BattleCommandExecutionResult` 与 `BattleCommandQueueData`。
- 新增 `BattleTurnPhase`、`BattleTurnData`、`PlayerTurnData` 与内部 `BattleTurnController`。
- 统一 `Submit` seam；先用最小测试命令验证单调序号、FIFO 执行、当前命令等待展示完成和后续命令可继续提交。
- 使用既有 `StateMachine<TEvent>` 组合阶段，不扩展 Core 状态机 interface。
- 当前 `BattleSession` 仍保留既有初始抽牌，直到 M4C 同一切片完成生产注册和启动迁移。

停止点验收：

- 未开始时玩家命令可以被结构性拒绝，不进入队列。
- 当前命令等待展示完成时，两个不同玩家仍可提交命令；它们取得稳定序号但不提前修改状态。
- 完成当前命令后才按序执行下一条，执行结果和展示顺序与权威序号一致。
- `StartBattleCommand` 只能执行一次；重复命令不得重复初始化。
- 两名玩家拥有独立 `PlayerTurnData`，没有全局 `CurrentEnergy`。

预计代码范围：

- `TinySpire/Assets/Scripts/Battle/Commands/`
- `TinySpire/Assets/Scripts/Battle/Turn/`
- `TinySpire/Assets/Editor/Tests/BattleCommandQueueTests.cs`
- `TinySpire/Assets/Editor/Tests/BattleTurnControllerTests.cs`
- 对应 `.meta`

### M4B · 队列化出牌、能量与执行期校验

状态：**已完成并通过独立验收（2026-08-01）**。`EnergyPerRound` 默认与手写 JSON 均为 3；出牌在队首读取权威参与者、玩家卡区与 Luban `Card.Cost`，成功后扣除对应玩家能量并把指定实例移入弃牌堆。Unity MCP 相关 EditMode 18/18、两套相关程序集静态编译 0 error；本地 Addressables 构建成功。详细证据见 `../06_testing/2026-08-01-m4b-queued-card-play-energy.md`。M4C 已于同日完成，M4D～M4E 尚未开始。

范围：费用与卡区规则进入队首命令，不执行真实 Effect。

- `GameConfig` 新增每轮基础能量，默认与 JSON 均为 3。
- 实现 `PlayCardCommand(actorId, cardId)`。
- 将阶段、玩家身份、结束状态、卡区归属和费用校验放在执行期。
- 成功时扣对应玩家能量并移动指定实例；失败产生结果但不改变任何权威事实。
- 当前使用可控制完成时机的展示 adapter 验证：展示未完成时下一命令不执行，但 `Submit` 继续接收。
- 本步不改变 `HandCardContainer` 生产接线；UI 收口与 `DEP-002` 最终解决放在 M4D。

停止点验收：

- 3 能量可按权威顺序执行费用 1、2 的牌并正确归零。
- 两名玩家可在第一张牌展示期间继续提交；第二张只在第一张展示完成后执行。
- 同一玩家基于旧能量连续提交的命令在执行时重新校验，不能透支。
- 错误阶段、错误玩家、已结束玩家、卡已不在手中或能量不足均失败且不改变事实。
- 修改 `game-config.json` 后执行 `TinySpire/Addressables/Build Local Content`。

### M4C · 队列化结束行动与敌人顺序交接

状态：**已完成并通过独立验收（2026-08-01）**。结束行动、全体玩家门槛、显式 Encounter 敌人顺序、死亡跳过、错误/重复完成保护与下一轮重置均已进入队列；初始抽牌已迁移到 `PlayerRoundStart`，`BattleLifetimeScope` 已注册生产队列和每帧最多完成一名无行为敌人的驱动。Unity MCP 相关 EditMode 27/27、两套程序集静态编译 0 error；Bootstrap 实跑读取到 `PlayerAction / Round 1 / Energy 3 / Hand 5 / queueIdle=true` 且 Console Error 为 0。详细证据见 `../06_testing/2026-08-01-m4c-end-action-enemy-handoff.md`。M4D～M4E 尚未开始。

范围：完整轮次闭环，不实现敌人行为内容。

- 实现 `EndPlayerActionCommand` 与全体玩家完成门槛。
- 实现当前敌人的系统完成命令；错误或重复完成不能推进。
- 把初始抽牌从 `BattleSession` 构造迁移到 `StartBattleCommand -> PlayerRoundStart`。
- 在 `BattleLifetimeScope` 注册队列、阶段模块与启动/逐帧执行入口。
- 当前无行为敌人仍经同一队列在后续帧完成，不从场景脚本直接跳阶段。
- 下一轮重置所有玩家能量与结束标记，并重新抽牌。

停止点验收：

- 两玩家测试中，一人的结束命令执行后，另一人仍能提交和执行。
- 全体玩家结束后才进入敌人阶段；排在已执行结束命令后的该玩家出牌失败。
- 敌人按 Encounter 顺序逐个完成，死亡敌人跳过。
- 下一轮轮次加一、能量重置、手牌重新抽取。
- 重复完成和一帧连续回调不会重复进入阶段。

### M4D · 当前单玩家 M3C 接线

范围：把现有输入与显示接到队列，不扩展玩法。

- `HandCardContainer` 不再调用 `DiscardFromHand` 或阶段模块方法，只提交 `PlayCardCommand`。
- 新增能量与结束行动 View；结束按钮只提交 `EndPlayerActionCommand`。
- View 订阅当前玩家事实和命令执行结果，区分“已排队”“执行失败”“执行完成”。
- 当前命令展示期间仍允许拖拽提交后续合法意图；卡牌排队视觉只表现 pending，不提前扣能量或移动权威卡区。
- 使用已有能量球、结束回合按钮和玩家回合横幅；不制造意图、状态或结算占位数据。
- 完成本步后解决 `DEP-002`；`DEP-001` 目标检测与 `DEP-004` 真实 Effect/动画保持 open。

停止点验收：

- Bootstrap 进入 BattleScene 后，`StartBattleCommand` 进入队列并显示第 1 轮、3 能量。
- 单玩家连续提交两张牌时，命令按序执行，能量、手牌和展示顺序一致。
- 执行期失败的卡牌恢复可交互状态，不扣能量、不移动卡牌。
- 结束命令执行后锁定玩家输入，完成敌人阶段后进入下一轮。
- 场景修改后重建本地 Addressables，再从 Bootstrap 实跑，无 `InvalidKey` 或 VContainer 错误。

### M4E · 全量验证、复审与文档收口

- 定向运行命令队列和回合控制 EditMode 测试。
- 运行全量 EditMode 与 `dotnet build TinySpire/TinySpire.sln --no-restore --verbosity:minimal`。
- 从 Bootstrap 实跑至少两个完整轮次，包含快速连续提交。
- 最终执行 `TinySpire/Addressables/Build Local Content`。
- 做 Standards / Spec 双轴审查，重点检查是否存在绕过队列的共享写入。
- 更新 `SESSION_LOG.md`、`CODE_DECISIONS.md`、`DEPENDENCIES.md` 与 `06_testing/`。
- 展示提交审查包，等待用户批准后才提交。

## TDD 测试 seam

用户确认的 seam 改为 `BattleCommandQueue.Submit` 与队列公开的 `Queue`/`Turn` 事实。测试不直接调用 `BattleTurnController` 的内部命令处理方法，不断言私有队列容器，也不把“方法被调用一次”当作行为成功。

首要行为测试：

1. 当前命令未完成展示时，其他玩家仍能提交并取得后续权威序号。
2. 后续命令不得提前修改状态或开始展示。
3. 当前命令完成后，下一命令按序开始。
4. 排队期间状态变化后，命令以执行时事实重新校验。
5. 单机与测试 adapter 通过同一 `Submit` interface，不建立第二条直通路径。

## 风险与回滚

| 风险 | 控制方式 | 回滚单位 |
|---|---|---|
| 把并发提交误写成并发改状态 | 只有队首命令可写；测试阻塞当前展示并检查后续命令未执行 | M4A 命令队列目录 |
| 把提交接受误当执行成功 | 分离 Submission 与 Execution 结果 | M4A 结果类型 |
| 单玩家结构渗入根 | 能量、命令提交者和卡区映射始终按 `CombatantId` | M4A/M4C 接线 |
| UI 或系统绕过队列 | M4D 后生产写链只允许 `Submit` | M4D UI 接线 |
| 网络细节提前污染 M4 | 当前只做本地权威排序；网络 adapter 列为排除 | M4A queue interface |
| M4 顺手实现 M5/M7/M9 | 只提供命令和展示完成 seam，内容保持排除 | 对应分步提交 |

## 完成定义

M4 完成不等于 Effect 闭环。完成标准是：当前单玩家 BattleScene 也通过统一命令队列运行；多人可以在模型测试中并发提交，权威命令按唯一顺序逐条执行和展示；能量、卡区、阶段和结束行动无法绕过队列写入；轮次可经过稳定敌人顺序闭环，并通过自动测试、Unity 实跑与 Addressables 构建。
