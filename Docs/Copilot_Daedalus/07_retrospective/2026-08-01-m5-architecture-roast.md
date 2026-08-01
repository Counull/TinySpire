---
title: M5 收口后 · BattleScene 整体架构锐评
owner: Daedalus
page_type: retrospective
lifecycle: active
created: 2026-08-01
updated: 2026-08-01
note: 依据实际代码（Assets/Scripts/Battle、UI/Battle）对照 ROADMAP/SESSION_LOG 主张的批判性复审；不是验收文档，不改变任何既有决策的效力。
---

# M5 收口后 · BattleScene 整体架构锐评

> 范围（第一遍·过程层）：`BattleSession` / `BattleCommandQueue` / `BattleTurnController` / `BattleEnemyIntentsData` / `BattleCardZonesData` / `BattleLifetimeScope` / `BattleCommandPresentationAdapter` / `HandCardContainer`。
> 范围（第二遍·模块级，§6 起）：`CombatantData` / `BattleCombatantsData` / `BattleEffectValueCalculator` / `CardTextFormatter` / `BattleCommand` / `BattleTurnData` / `GameRandom` / `StateMachine` / `BattleParticipantPresenter` / `ParticipantHudView` / `HandCardVisual` / `HandCardContainer` 余部。
> 范围（补完扫描，§6.11 起）：`BattleCommandResults` / `BattleCommandQueueData` / `IBattleCommandPresentation` / `EnemyCombatantLayout` / `CardIllustrationAddress` / `ParticipantHudPresentation` / `BattleCardPileHudView`+`Presentation` / `BattleTurnHudView`+`Presentation` / `HandCardLayout` / `HandCardInteraction` / `ConfigService` / `GameConfig` / `LocalizationService`。至此 M1～M5 产出的运行时代码全部过目；`BattleCardPlayRules.cs` 与 `BattleCommandExecutionFailureReason` 的 Target 枚举扩展属同工作区 Agent 的 M6 在途代码，不作为存量锐评对象，仅在 §6.16 做协调标注。
> 阶段状态口径：本文审查对象只有 M0～M5 已交付代码（M6 在途、他人负责）。**M7 及之后全部未开工，不存在可审查的 M7+ 代码**；文中所有 M7/M8/M9/M10/M3E 字样均为到期日标签——指"现有代码里哪处形状会在该里程碑开工时成为成本、届时应顺势清偿"，不是对未实施功能的审查结论，也不是要求现在提前实施。
> 立场：M0～M5 的单一事实纪律是真的守住了，但有几处结构正在靠“现在体量小”掩盖，M6～M8 一到就会连本带利收账。第二遍用 deep-module 语汇逐模块过接口：深度是接口的性质，不是实现的性质。

## 0. 一句话总评

**这是一套“数据层拿了满分、调度层开始欠债、UI 层已经在替架构打工”的代码。** 快照不可变、派生不落地、随机流隔离、确定性可复现——这些承诺全部兑现，值得承认。但命令分发、出牌事务、表现管线和 DI 装配四个地方，都在用“第一版够用”的写法占住 M6/M7/M8 必须经过的路口。

## 1. 先说守住了什么（免得像纯泼脏水）

- **单一事实纪律是真的**：`CardZoneLayoutData`、`EnemyIntentLayoutData`、`BattleTurnData` 全部是发布即冻结的完整快照；没有找到任何 `Zone` 镜像字段、`IsAlive` 缓存或“最终伤害”存量。ROADMAP §2.2 的清单逐条兑现了。
- **确定性是真的**：`BattleEnemyIntentsData` 的“先在副本上选、失败恢复随机状态、单候选不消费随机”写得干净，`GameRandom` 按域隔离并加盐派生，SESSION_LOG 里两次同种子实跑序列一致的证据是可信的。
- **原子性是真的**：`CompleteAndSelectNext` 的 clone-then-commit、`BattleSession.FromConfig` 的失败回滚释放，事务意识在线。

以上是地基。下面是地基上正在歪的柱子。

## 2. 重锤问题

### 2.1 `BattleCommandQueue.Execute` 正在长成上帝分发器，且已经开始夹带私活

**定性：调度根里藏了一段两聚合事务脚本，这不是调度，这是业务。**

证据（`Assets/Scripts/Battle/Commands/BattleCommandQueue.cs`，`Execute` 方法）：

- 命令分发是一条 `is StartBattleCommand / is PlayCardCommand / is EndPlayerActionCommand / is CompleteEnemyActionCommand` 的 if-else 链。M6 的带目标出牌、M7 的效果操作、M8 的敌人行动命令，每个都要在这里加一个分支。
- `CompleteEnemyActionCommand` 分支已经不是转发了：`ValidateCompleteEnemyAction` → `_enemyIntents.CompleteAndSelectNext` → `_turnController.AdvanceAfterValidatedEnemyAction`，三步跨两个聚合的事务顺序**硬编码在队列里**。队列既是排序者又是事务协调者。今天两个聚合还能背下来，M8 加上“执行意图 Effect → 写结算记录 → 状态衰减 → 选下一意图 → 推进顺序”之后，这个方法就是下一个大 Update。
- 更阴的一点：`CompleteAndSelectNext` 抛异常时（无候选），异常会沿 `BeginNextCommand` 一路穿出。而 `BeginNextCommand` 是在 **`Submit` 的调用栈上同步执行的**——也就是说，配置错误的异常最终炸在某个无辜提交者（生产里是 `BattleCommandRuntimeDriver.Tick`）脸上，队列停在 `_currentCommand != null` 且永远等不到表现完成的状态。SESSION_LOG 把这描述成“无候选异常会让队列停在当前命令”，听起来像设计；实际上是“靠崩溃停机”，没有任何显式的队列错误态或恢复出口。

### 2.2 “打出一张牌”在权威状态里等于“弃一张牌 + 扣能量”——M7 的路口被现在的事务形状堵着

**定性：`TryPlayCard` 的最后一行就是 M7 最大的隐性重构成本。**

证据（`Assets/Scripts/Battle/Turn/BattleTurnController.cs`，`TryPlayCard`）：

- 校验链结束后直接 `cardZones.DiscardFromHand(command.CardId)` 然后扣能量、发快照。没有效果、没有结算记录、没有“结算完成后再进弃牌堆”的位置。
- ROADMAP M7 明确要求“操作依次写入权威状态，并产生只读结算记录”“卡牌完成后进入弃牌堆**或消耗区**”。现在的事务是 `校验 → 弃牌 → 扣能量` 一口气做完；M7 要的是 `校验 → 扣能量 → 效果管线（可多步、可产生记录）→ 按卡牌属性进弃牌堆/消耗区`。**事务的形状要变，不是往后追加一段就行。** `ExhaustFromHand` 现在是一个从未被调用的死入口，也印证了这条路径还没人走过。
- 顺带：伤害数字等一次性反馈按 ROADMAP 要“消费结算记录”，而当前 `BattleCommandExecutionResult` 只有序号、类型、提交者、失败原因四个字段。结算记录这个 M7/M9 的关键契约，今天连个空壳类型都没有——它不是可以最后再想的细节，它是 `Execute` 返回值的形状。

### 2.3 校验链已经复制了两份，M6 会让你抄第三份

**定性：这不是“重复代码”级别的洁癖问题，是失败原因语义正在靠人肉对齐。**

证据（`BattleTurnController.TryPlayCard` 与 `TryEndPlayerAction`）：

- 两个方法开头 ~25 行完全同构：阶段校验 → `_players.TryGetValue` + `_combatants.TryGet` + 玩家类型 → 存活 → `HasEndedAction` → 卡区存在。失败原因枚举一一对应。
- SESSION_LOG（M4E）自己都承认了：“`BattleTurnController` 重复校验链也不借此扩展重构”。当时不扩是对的；但 M6 要加目标规则、目标存活、战斗结束共 3+ 项新校验，如果继续 copy-paste，第三份链和前两份的顺序、语义漂移只是时间问题。**M6 动这个文件的那一刀，就是提取“玩家命令校验上下文”的唯一正确时机**，不要单开重构，也不要再抄。

### 2.4 “所有命令通过同一 Submit 建立权威顺序”——抽牌表示不服

**定性：文档主张的统一命令面，被阶段进入副作用凿了一个洞。**

证据：

- ROADMAP M4 规则第一条：“玩家、系统阶段与未来敌人/Effect 都通过同一个 `BattleCommandQueue.Submit` 提交命令”。
- 现实：`EnterPhase(PlayerRoundStart)` → `ResetPlayersForRound` → `DrawPlayerToTargetHand` → `cardZones.Draw(...)`。抽牌、弃手牌、重洗这些卡区写入**不是命令**，是某条别的命令（`CompleteEnemyActionCommand` / `EndPlayerActionCommand`）执行期间、状态机 `Tick(TimeSpan.Zero)` 连锁推进时的阶段进入副作用。它们没有权威序号、没有执行结果、没有表现反馈。
- 今天这是无害的，因为抽牌没有表现需求。但 M7 有“抽牌效果卡”，M9 有“抽牌/弃牌/重洗动画”——届时同一个 `Draw` 一部分走命令/结算记录、一部分走阶段副作用，两套时序。要么现在就承认“阶段副作用产生的卡区变更也要出结算记录”，要么 M7 把回合开始抽牌改成系统命令。二选一，但别拖到 M9 用动画脚本找补。

### 2.5 表现管线是单槽位流水线，每条命令强制 0.35 秒过闸费

**定性：M9 的“STS 式反馈”不可能在这条管线上长出来，它现在就该被承认是临时桩。**

证据（`Assets/Scripts/UI/Battle/BattleCommandPresentationAdapter.cs` + `BattleLifetimeScope.cs` 里的 `BattleCommandRuntimeDriver`）：

- Adapter 只有一个 `_currentResult` 槽位，所有命令串行排队，每条至少等 `DefaultPresentationDurationSeconds = 0.35f`——包括 `StartBattleCommand` 和无任何可见表现的敌人完成命令。三个敌人的空过场就是白等一秒多。
- `BattleCommandRuntimeDriver.Tick` 是**逐帧轮询**：每帧看一眼队列是否空闲 + 阶段是否 `EnemyAction`，是就替当前敌人提交完成命令。系统行动者靠轮询驱动，而不是被队列/阶段变化驱动。这在 M5“敌人不做事”的世界里工作正常；M8 敌人要真的执行 Effect、等待动画屏障时，“谁在什么时候替敌人提交什么”会变成一个真正的调度问题，轮询 + 单槽位 + 固定时长三件套全都要换。
- 另一个协议裂缝：`Queued` 反馈是 **UI 替 adapter 发布的**（`HandCardContainer.SubmitPlayCard` 调 `_commandPresentation.PublishQueued`）。也就是说“每条被接受的命令都有 Queued 反馈”这个协议，靠每个提交者自觉遵守。`BattleCommandRuntimeDriver` 就没发。要么把 Queued 收进 `Submit` 内部统一发布，要么承认这个事件只属于手牌 UI，别留一个一半人遵守的协议。

### 2.6 “多人根基”只在中间层是真的，两头都是单人

**定性：多人兼容的说法目前只覆盖了字典的形状，没覆盖聚合的形状。**

证据：

- `BattleSession` 只持有**一个** `BattleCardZonesData`；“每玩家卡区”这个多人形状是 `BattleLifetimeScope.CreateCurrentPlayerCardZones` 在 DI 装配时现场捏出来的字典，还带着一个 DEP-008 的“超过一个玩家就炸”守卫。
- 于是出现了一个别扭的分工：ROADMAP §3 说 `BattleSession`“仅负责从配置创建和装配”，但真正的装配逻辑（玩家→卡区映射、8 个参数的 `BattleCommandQueue` 构造）住在一个 `MonoBehaviour` 子类（LifetimeScope）里。**领域装配逻辑住在 Unity 场景组件里**，这意味着纯 C# 测试要么绕开生产装配路径、要么复制它。
- 真到多人接线那天，`BattleSession`、`BattleLifetimeScope`、`BattleCommandQueue` 构造函数三处一起改。“多人根基”承诺的应该是“只改装配一处”，现在做不到。把 `PlayerCardZones` 映射收进 `BattleSession` 本体，LifetimeScope 退回纯注册，是一步小而正确的迁移。

### 2.7 状态机是仪式，不是杠杆；中间阶段是同步爆发的装饰性事件

**定性：`StateMachine<T>` 的 Enter/Handle/Tick/Exit 四件套，在这里 90% 的实现是空方法和 `return Stay`。**

证据（`BattleTurnController` 内嵌状态类）：

- 所有推进都是 `Dispatch(event); Tick(TimeSpan.Zero)` 成对同步调用，`deltaTime` 从未被使用；`Exit` 全部为空；`NotStartedState.Tick`、`EnemyActionState.Tick` 恒 Stay。这是用状态机框架写了一个同步阶段推进函数。
- 更值得警惕的是副作用：一次 `CompleteEnemyActionCommand` 会让 `AutomaticPhaseState.Tick` 同步连跳 `EnemyRoundEnd → RoundEnd → PlayerRoundStart → PlayerAction`，**每一跳都发布一次 `_turn.Value` 快照**，R3 订阅者（`HandCardContainer.HandleTurnChanged`、HUD）在**命令执行的调用栈上**被同步喂了 3～4 次中间态。目前订阅者只做派生刷新所以没炸，但“订阅者在写入方调用栈上观察到中间阶段、且理论上可以在此刻 `Submit`”是一个已经上膛的重入枪。M8 要在 `RoundEnd`/`EnemyRoundStart` 挂状态衰减和动画屏障时，这些阶段会从“同步跳过的装饰”变成“必须停留的真状态”，届时整个自动连跳链要重写——现在就该在计划里承认这一点，而不是让 M8 的实施者自己发现。

### 2.8 `HandCardContainer`：一个 MonoBehaviour 干了五份工

**定性：这是当前代码库里最接近“输入 + 预测 + 提交 + 回调 + 资源 + 布局全挂一身”的类，Skill 定义里的顶格批判对象。**

证据（`Assets/Scripts/UI/Battle/Hand/HandCardContainer.cs`）：

- 它同时是：Addressables 插画加载器（`LoadCardIllustrationsAsync` + 句柄生命周期）、扇形布局引擎、拖拽状态机（`_draggingCard`）、命令提交者（`SubmitPlayCard`）、pending 展示簿记员（`_pendingPlayCards`）、Queued 反馈的代发者、本地化刷新器。六个注入依赖，四路订阅。
- `BindCardPresentation` 在插画未加载时直接 `throw InvalidOperationException`——从一个 UI 绑定方法里抛异常当控制流，而调用方之一是 R3 订阅回调（`RefreshCardTexts`），异常会进订阅链。
- M6 要在这上面加目标箭头、合法目标高亮、命中判定；M9 要加飞卡、灰化、费用提示。如果不先把“命令提交 + pending 追踪”从“布局 + 资源 + 拖拽”里拆出去，M6 的每一行新代码都在给这个类的第六份工作添砖。

## 3. 文档主张 vs 代码现实 对照表

| ROADMAP/SESSION_LOG 主张 | 现实 | 判定 |
|---|---|---|
| “所有命令通过同一 `Submit` 建立统一权威顺序” | 抽牌/弃手牌/重洗是阶段进入副作用，无序号无反馈（§2.4） | **夸大** |
| “多人根基、当前单玩家接线” | 仅命令队列的参数形状是多人；Session 与装配是单人硬编码（§2.6） | **半真** |
| “`BattleSession` 仅负责从配置创建和装配” | 玩家→卡区装配住在 LifetimeScope（§2.6） | **失守** |
| “无候选异常会让队列停在当前命令” | 靠未捕获异常炸穿提交者调用栈来“停”（§2.1） | **美化** |
| 快照不可变、派生不落地、随机域隔离 | 逐条兑现 | **属实** |
| “重复校验链不借此扩展重构”（M4E） | 属实且正确，但必须在 M6 动刀时清偿（§2.3） | **属实，带到期日** |

## 4. 针对性意见（按到期顺序，不是按严重度）

1. **M6 动 `TryPlayCard` 的同一刀里**：提取玩家命令校验为一个返回 `(校验上下文 | 失败原因)` 的单一方法，`TryPlayCard`/`TryEndPlayerAction`/M6 目标校验共用；顺手把目标规则校验放进去。不单开重构 PR，不抄第三份链。
2. **M7 开工前先定结算记录的形状**：哪怕先是一个空的 `IReadOnlyList<BattleSettlementRecord>` 挂在 `BattleCommandExecutionResult` 上。同时把 `TryPlayCard` 的事务改成“校验 + 扣能量”与“效果管线 + 归堆（弃/耗）”两段，`DiscardFromHand` 不再是出牌的终点动作。这是 §2.2 的清偿。
3. **M7 同时裁决 §2.4**：回合开始抽牌要么改为系统命令，要么规定“阶段副作用的卡区变更也产出结算记录”。写进 M7 计划，二选一，不允许两套时序活到 M9。
4. **M8 前替换表现管线的三件套**：轮询驱动 → 队列/阶段事件驱动；单槽位固定 0.35s → 按命令类型给出表现描述（无表现命令零时长直通）；`PublishQueued` 收进 `Submit` 内部。同时给队列一个显式错误态，取代“异常炸穿提交者”（§2.1、§2.5）。
5. **小而立刻可做**：把 `CreateCurrentPlayerCardZones` 迁进 `BattleSession`（暴露 `PlayerCardZones` 映射），LifetimeScope 退回纯注册。改动面小，直接兑现“Session 是装配唯一入口”的既有主张（§2.6）。
6. **M8 计划里预先承认**：自动连跳的中间阶段将升级为可停留状态，同步爆发式快照发布需要改为“每条命令至多一次对外可见的阶段推进”或引入显式屏障。别让实施切片临场发现（§2.7）。
7. **M6 接线目标交互时顺势拆 `HandCardContainer`**：先拆出“命令提交 + pending 追踪”的非 MonoBehaviour 协作者，布局/资源/拖拽留在原地。只拆这一刀，不做全面 MVVM 化（§2.8）。
8. **快照全量复制模式**（每次移动复制四个列表、每次意图完成复制整个字典）在当前体量下是正确的取舍——但把它记为**有意决策**：M7 一张 Bash 会连发 3+ 个全量快照，若届时 profiler 说话，失效策略按 ROADMAP M2A 缓存条款的同等纪律来，不许静默加缓存。

## 5. 第一遍结论（过程层）

M0～M5 最值钱的资产不是任何一个类，而是“事实只有一份、派生现场重算、随机可复现”这条被真正执行的纪律。当前的债务全部集中在**过程层**：命令分发、事务形状、表现管线、装配归属。它们的共同特征是“今天不痛，但都蹲在 M6/M7/M8 的必经之路上”。上面 8 条意见没有一条要求现在停下来大重构——每一条都绑定在一个本来就要动那块代码的里程碑上。按到期日清偿，别提前，也别再展期。

## 6. 第二遍 · 模块级审查（deep-module 视角）

> 语汇约定：**模块** = 接口 + 实现；**接口** = 调用方必须知道的一切（签名、不变量、顺序约束、错误模式）；**深度** = 每学一份接口能换到多少行为；**seam** = 接口所在的位置；**删除测试** = 删掉这个模块，复杂度是消失（直通货）还是在 N 个调用方重现（真模块）。

### 6.0 先点名真正的深模块（这些别动）

- **`GameRandom`**：三个成员（`State`/`NextInt`/`Shuffle`）背后藏了引擎随机类型、非零状态不变量和 Fisher–Yates。接口小、行为全、删除测试即刻不及格——删了它，确定性纪律在四个随机域各自重现一遍。教科书级深模块。
- **`BattleCardZonesData.Draw`**：调用方学一个方法，换到“抽牌 + 空堆重洗 + 双空少抽 + 原子快照发布”四件事。这就是杠杆。
- **`BattleEnemyIntentsData.CompleteAndSelectNext`**：一个入口藏了候选过滤、加权抽样、克隆提交、随机状态回滚。同上。
- **View + 静态 Presentation 纯函数**的配对模式（`ParticipantHudPresentation.DeriveEnemyIntent` 等）：派生逻辑从 MonoBehaviour 里拿出来变成可测纯函数，接口就是测试面。保持。
- **`BattleTurnController` 是 `internal`、只通过 `BattleCommandQueue` 的 seam 被测试**：这是“接口即测试面”的正确执行，不要为了“好测”把它 public 化。

### 6.1 `BattleEffectValueCalculator`：8 行实现背着“展示=结算共享公式”的接口承诺

**定性：这是全库杠杆最虚的模块——接口的承诺（ROADMAP：展示值和结算值调用同一计算模块）由一个只认识力量、硬编码 `if != DealDamage return Value` 的静态方法背书。**

- 接口形状是 `Calculate(effect, source)`——**没有 target 参数**。M7 的易伤/虚弱是目标侧倍率，这个签名在类型层面就表达不了“目标命中后的预览”。届时签名必改，`CardTextFormatter` 和 `ParticipantHudPresentation` 两个调用方全动。
- seam 本身是真的（两个调用方共用一处公式 = 真局部性），但接口的形状活不过 M7。**深度是接口的性质**：现在往 `Calculate` 里加参数是把浅模块拍宽，不是加深。
- 建议（绑 M7）：先设计结算公式模块的接口（source、target、结算上下文、取整规则），让 `Calculate` 退化为它的“无目标投影”，而不是反过来往 8 行方法里塞第三个参数。

### 6.2 `ApplyDamage`：双层直通 + 死接口 + 注定被替换的形状

- `BattleCombatantsData.ApplyDamage` 是纯转发（查字典 → 调 `target.ApplyDamage`），删除测试不及格：删掉它，调用方写 `TryGet + ApplyDamage` 两行，复杂度没有消失也没有重现——因为本来就没有复杂度。
- 更重要的是：**全库没有任何生产调用方**。这是 M7 之前的死接口。而它“正数伤害直接扣血”的实现形状与 M7 的“格挡先吸收、易伤加倍、再取整”公式**冲突**——它不是可以往上叠格挡的起点，是要被重塑的占位。
- 错误模式混装：`damage <= 0` 抛异常、目标死亡返回 `false`——同一个方法两条失败通道，调用方必须同时准备 try/catch 和 bool 检查。接口包含错误模式；这个接口现在教调用方两套矛盾的习惯。
- 建议（绑 M7）：结算管线落地时直接重塑或删除，禁止在现有形状上“先扣格挡再调它”。

### 6.3 `PlayCardCommand.TargetId`：接口文档在承诺一条不存在的错误模式

代码注释原文：“缺失目标由执行期规则明确拒绝”。现实：`TryPlayCard` 的整条校验链**从不读取 `TargetId`**。传了目标不校验、不传也不拒绝。接口 = 调用方必须知道的一切；这行注释让调用方“知道”了一个假行为，比没有注释更糟。M6 之前要么删注释，要么兑现它——这是文档替未来代码撒谎的最小样本。

> 2026-08-01 补注：同工作区 Agent 的 M6 在途代码（`BattleCardPlayRules`）正在兑现该承诺；截至本次审查它尚未被 `TryPlayCard` 消费。本条从“撒谎”降级为“承诺在途”，但 §6.16 的协调风险生效。

### 6.4 强类型 ID 纪律半途而废

`CombatantId`、`CardInstanceId` 各花 50 行样板换类型安全——然后 `BehaviorId`、`EffectId`、`TemplateId` 全部裸 `int` 穿层（`EnemyIntentLayoutData` 的字典是 `<CombatantId, int>`）。被强类型保护的是最不容易混淆的两个（参与者 vs 卡牌实例），被放走的是最容易混淆的三个（全是 Luban 表里量级相近的小整数，一个 `GetOrDefault(behaviorId)` 传成 `effectId` 编译器全程微笑）。不要求补全所有强类型；但 M7 效果管线里 `EffectId` 至少别再以裸 int 穿三层以上。

### 6.5 `ParticipantHudView.Bind`：七参数宽接口 + 单类双角色

- `Bind(combatant, nameI18nKey, worldView, canvas, localization, tables, enemyIntents)`——调用方必须凑齐 HUD 的**整张依赖图**才能用它。接口几乎和实现一样宽，这是标准浅模块：Presenter 对 HUD 内部结构的了解，和 HUD 自己差不多。
- 内部再按 `_enemyCombatant != null` 分支玩家/敌人两种角色。M3E 要加格挡、状态、死亡表现，参数和分支都只会涨。
- 附带两个运行时脾气：`LateUpdate` 每帧做世界→屏幕投影，`Canvas.worldCamera` 为空时逐帧兜底 `Camera.main`；相机缺失或投影失败**从 LateUpdate 抛异常**——每帧一条异常刷屏的失败模式。
- 建议（绑 M3E）：动它时把参数收进一个绑定上下文（或由 Presenter 预组装），玩家/敌人拆成同一 seam 下的两个 adapter；投影失败降级为隐藏 + 一次性报错。

### 6.6 pending 事实的双所有权（M4E 已经为它付过一次利息）

- “某张卡有未决出牌命令”这一个语义存在**两套机制**：`HandCardContainer._pendingPlayCards`（序号→卡 ID 字典）和每个 `HandCardVisual._pendingCommandSequence`（nullable 序号），靠 `TryGetLatestPendingPlaySequence` 在 View 重建时手动同步。M4E 修的“旧序号误清新 pending”缺陷，就是这个双所有权结构收的第一笔利息——修复方式是把同步协议写得更精细，而不是消灭第二份状态。
- 顺带，**权威序号这个领域调度概念漏进了 View 的接口**（`SetCommandPending(long?)` / `PlayCommandFailureFeedback(long)`）：视觉对象在替调度层记账。
- 建议（绑 M6 拆 `HandCardContainer` 那一刀）：pending 关系给一个唯一 owner（命令提交协作者），View 接口收窄到 `SetPending(bool)` 级别，序号比对留在 owner 里。

### 6.7 卡牌 View 的 prefab 契约一半住在代码里

`CreateCard` 在运行时给 prefab 依次 `AddComponent<CanvasGroup>`、`AddComponent<Image>`（透明命中区）、`AddComponent<HandCardInteraction>`。卡牌 View 的完整形状 = prefab 资产 + 容器代码两处拼装；seam 本该在 prefab 上，现在删除测试会告诉你“prefab 单独不可用，容器不可删”。M9 的飞卡/灰化动画要在这个半资产半代码的结构上叠层。建议：M9 前把三个组件收进 prefab，`CreateCard` 退化为 Instantiate + Initialize。

### 6.8 `async void Start` + throw：显式失败退化成显式打印

`HandCardContainer.Start` 与 `BattleParticipantPresenter.Start` 都是 `async void`，配置/资源错误以异常抛出后只会进 Unity 日志，**场景以半构建状态继续活着**——没有错误态、没有输入封锁、没有覆盖层。项目引以为傲的“配置错误显式失败”纪律，在 UI 装配层的实际语义是“显式打印然后带病运行”。另：Presenter 对玩家和每个敌人**串行 await** Addressables 加载，加载时长随敌人数线性增长。都不急，绑 M9 覆盖层：失败进显式错误态，加载改并发。

### 6.9 i18n key 字面量跨模块复制 + 校验时机与主张不符

- `battle.keyword.strength.name` 同时硬编码在 `CardTextFormatter`（`StrengthKeywordKey`）和 `ParticipantHudView`（`StrengthNameKey`）两处。两个常量、一个语义，改 key 要人肉找齐。
- ROADMAP M2A 承诺“构建前检查 key 唯一、参数与绑定一致、拒绝重复/未知参数”；现实是这些检查以 `throw` 的形式活在**每次 Format 调用的运行时**。校验存在，时机与主张不符——半真。构建前校验入口至今缺位，G7 内容校验会再次撞见它。

### 6.10 `StateMachine<TEvent>`：框架税实锤 + §2.7 重入枪的扳机确认

- 通用接口四方法 × 每状态，唯一消费者 `BattleTurnController` 的实现里 `Exit` 全空、`deltaTime` 无人使用、多数 `Handle`/`Tick` 恒 `Stay`。**一个 adapter = 假想 seam**：这个框架的通用性没有第二个消费者兑现，接口比它在唯一语境下的行为更宽。
- 补 §2.7 的关键证据：`BeginProcessing` 有重入守卫，重入**直接抛 `InvalidOperationException`**。也就是说，任何 R3 订阅者在阶段快照回调里同步 `Submit` 一条会触发状态机 `Dispatch` 的命令，不是状态损坏，是当场爆炸。比静默损坏好，但确认了 §2.7 不是理论风险——枪是上膛且一触即发的，只是现在没人扣。M8 动画屏障接入前必须给出“订阅回调内提交”的明确规则（延迟入队或显式禁止）。

### 6.11 `ConfigService`：规则事实带静默回退——全库唯一一处“失败继续跑”

**定性：整个代码库对配置错误的纪律是显式失败，唯独每轮能量、初始手牌数这两个规则事实例外。**

- `LoadGameConfigAsync` catch 全部异常后 `Debug.LogWarning` + 返回 `new GameConfig()` 默认值（能量 3、手牌 5）。game-config.json 丢失、损坏或地址失效时，战斗照常开、数值来自代码常量——与 M10“数值来自表格，不残留代码常量”正面冲突，也和同文件里 Luban 表缺失即 throw 的纪律双标。
- 今天默认值恰好等于表值所以无人察觉；哪天表改成 4 能量而加载失败，你会得到一场“看起来正常”的 3 能量战斗和一条没人看的 Warning。规则事实的静默回退比崩溃更贵。
- 另：`TableNames` 是生成表清单的手工影子列表，M5A 就补过两行；漏补的症状是运行期 “not preloaded” 异常。低优先级，但它是一个人肉同步点。

### 6.12 “提交者惯用法”已复制到第三份——命令提交协作者的缺位在自我证明

- `BattleTurnHudView` 完整复刻了 `HandCardContainer` 的 pending 簿记：nullable `_pendingEndActionSequence` + 反馈序号匹配 + 跳过 Queued + 自行调用 `PublishQueued`。加上 `HandCardVisual` 的 nullable 序号，同一“提交→记 pending→发 Queued→对账反馈”惯用法已有三份手写实现，而 `BattleCommandRuntimeDriver` 又完全不发 Queued。
- §2.5/§6.6 说的“一半人遵守的协议”不是修辞：每新增一个提交入口就要再抄一遍这套生命周期，并且可以选择性漏抄。M6 的目标选择 UI 是下一个提交者——拆出命令提交协作者的时机就是现在，不然就是第四份。

### 6.13 “当前唯一玩家”解析已有四份副本，且门禁强度不一致

- `BattleLifetimeScope.CreateCurrentPlayerCardZones`、`BattleTurnHudView.ResolveCurrentPlayer`、`BattleParticipantPresenter.CreatePlayerViewAsync` 都在遍历 `All.Values` 找玩家并对“多于一个”抛 DEP-008 异常；`HandCardContainer.ResolvePlayer` 却**取第一个就返回**，多玩家时静默绑定错人。同一约束、四处实现、一处漏检——缺一个 seam 的教科书症状。
- 建议（并入意见 5 的 Session 迁移）：`BattleSession` 暴露当前玩家/玩家集合的唯一解析出口，四处副本全部改为消费它；DEP-008 门禁只活在一处。

### 6.14 i18n key 字面量扩散与 “Battle Cards” 表的语义漂移

- key 字面量现散布在至少四个文件：`CardTextFormatter`（keyword 两枚）、`ParticipantHudView`（strength）、`BattleCardPileHudView`（三枚牌堆名）——§6.9 说的“两处”已经过时，实际在扩散。
- `LocalizationService.GetString` 把所有请求钉死在 `Battle Cards` 一张表上；牌堆名、回合 HUD、关键词全住在一张名叫“卡牌”的表里。接口名撒了个小谎，M9 的横幅/胜负文案进来时会更明显。收敛 key 常量 + 裁决表的真实边界，一次小改。

### 6.15 补完批次的清白名单

`HandCardLayout`、`EnemyCombatantLayout`、`BattleCardPileHudPresentation`、`BattleTurnHudPresentation`、`ParticipantHudPresentation`：纯函数、无状态、参数即测试面，全部合格。`HandCardInteraction` 是 Unity 事件系统 seam 上的合法薄 adapter，不按直通货论处。`CardIllustrationAddress` 的键格式防御恰到好处。`BattleCommandQueueData`/`BattleCommandSubmissionResult`/`BattleCommandExecutionResult` 快照与结果类型干净。

### 6.16 M6 在途协调标注（不是锐评，是风险登记）

同工作区 Agent 的 `BattleCardPlayRules.Evaluate` 是校验链的**第三份副本**——§2.3 的预测正在字面意义上发生：阶段/玩家/存活/结束/卡区/手牌/模板费用整条链与 `TryPlayCard` 同构，且已出现首个语义分叉（`Evaluate` 查 `BattleAlreadyEnded`，`TryPlayCard` 不查；能量不足时两者返回结构不同）。协调口径应当是：**`Evaluate` 成为唯一校验链，`TryPlayCard` 到达队首时消费它**，而不是 Evaluate 做 UI 预览、TryPlayCard 再长一条平行链。若 M6 计划已如此安排，此条自动关闭；若不是，这是 M6 验收前必须对齐的一件事。

## 7. 第二遍新增意见（并入 §4 到期表）

9. **M6 拆 `HandCardContainer` 时**：pending 唯一 owner，View 接口收窄到 bool（§6.6）；同时删掉或兑现 `TargetId` 的注释承诺（§6.3）。
10. **M7 开工第一步**：先定结算公式模块的接口（source/target/上下文），`BattleEffectValueCalculator.Calculate` 退为无目标投影（§6.1）；`ApplyDamage` 重塑而非叠加（§6.2）；`EffectId` 在新管线内不以裸 int 穿层（§6.4）。
11. **M8 前**：明确“订阅回调内提交命令”的规则——延迟入队或显式禁止，别让重入守卫的异常当规则（§6.10）。
12. **M3E 动 `ParticipantHudView` 时**：Bind 收进绑定上下文，玩家/敌人拆 adapter，投影失败降级（§6.5）。
13. **M9 前**：卡牌 prefab 契约归位（§6.7）；UI 装配失败进显式错误态、参与者加载并发化（§6.8）。
14. **随手可做**：keyword key 常量收敛到一处（§6.9 前半）；构建前 i18n 校验入口登记为 M10/G7 依赖，别让运行时 throw 继续冒充它（§6.9 后半）。
15. **立即可修**：`ConfigService.LoadGameConfigAsync` 去掉静默默认回退，加载失败显式失败（§6.11）。这是全库唯一违反自家显式失败纪律的点，改动一行级别。
16. **M6 接线前对齐**：`BattleCardPlayRules.Evaluate` 与 `TryPlayCard` 必须收敛为一条校验链（§6.16）；同时命令提交协作者落地，终结第三份提交者惯用法（§6.12）。
17. **并入意见 5**：Session 暴露唯一玩家解析出口，消灭四份 ResolvePlayer 副本与不一致门禁（§6.13）；key 常量收敛与表边界裁决随 M9 文案进场一并处理（§6.14）。

## 8. 两遍合并结论

第一遍的债在**过程层**（分发、事务、表现、装配），第二遍的债在**接口形状**（承诺过宽的浅模块、撒谎的注释、双所有权、半途而废的类型纪律）。两类债的共性不变：今天全都不痛，且全都蹲在 M6/M7/M8/M3E/M9 的必经之路上。真正的深模块（`GameRandom`、卡区、意图聚合、纯函数 Presentation）一个都不要动——资产和债务分得很清，按里程碑到期清偿即可。

## 9. 审查覆盖清单

已审（M1～M5 存量运行时代码，全部过目）：

- Battle：`BattleSession` / `BattleCombatantsData` / `CombatantData` / `BattleCardZonesData` / `BattleEnemyIntentsData` / `BattleEffectValueCalculator` / `CardTextFormatter` / `CardIllustrationAddress` / `EnemyCombatantLayout` / `BattleLifetimeScope`（含 `BattleCommandRuntimeDriver`）
- Turn/Commands：`BattleTurnController` / `BattleTurnData` / `BattleCommandQueue` / `BattleCommand` / `BattleCommandResults` / `BattleCommandQueueData` / `IBattleCommandPresentation`（含 `ImmediateBattleCommandPresentation`）
- Core：`GameRandom` / `StateMachine` / `ConfigService` / `GameConfig` / `LocalizationService`
- UI：`HandCardContainer` / `HandCardVisual` / `HandCardLayout` / `HandCardInteraction` / `BattleParticipantPresenter` / `ParticipantHudView`+`Presentation` / `BattleCardPileHudView`+`Presentation` / `BattleTurnHudView`+`Presentation` / `BattleCommandPresentationAdapter`

明确排除：`Core/Generated/**`（Luban 生成物）；`Bootstrap`/`GameLauncher`/`SceneFlowService`/`AddressableAssetService`/`RandomLoadingCover`（M0 前启动链路基础设施，非 M1～M5 产出，如需纳入另行安排）；EditMode 测试资产（按“接口即测试面”只审了被测 seam）；`BattleCardPlayRules.cs` 与 Target 枚举扩展（同工作区 Agent M6 在途，仅 §6.16 协调标注）。
