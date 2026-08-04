---
title: M9 验收后 BUG 分诊与结构审查关联
page_type: testing
lifecycle: active
created: 2026-08-05
updated: 2026-08-05
status: passed
scope: M9 验收后用户实机反馈、卡牌运动修复证据、生命 HUD 临时头顶投影边界
plan: ../plans/2026-08-02-m9-sts-feedback-outcome-restart.md
status_source: ../SESSION_LOG.md
---

# M9 验收后 BUG 分诊与结构审查关联

## 当前结论

本页记录 M9 完成后用户实机发现的三个可见问题。`BUG-MOTION-001` 与 `BUG-MOTION-002` 已在真实 `CardZones.Layout` 订阅、`HandCardContainer`、`HandCardVisual` 与命令 runner 组合的 EditMode 红灯中复现并转绿；这证明可见运动时序，不冒充真实 Game View 连续帧。`BUG-UI-001` 已在既有 Editor 的五种 M9 Game View 预设中用运行时屏幕矩形复现：生命条与手牌相交，而二者处在会确定遮挡方向的不同 Canvas 渲染模式。用户明确后续 UI 会大规模重做，因此本轮不调整 Canvas，而以临时头顶投影降低当前遮挡风险。

- `BUG-UI-001`：战斗生命 HUD 仍被遮挡。它与用户提出的整体 Battle UI 重设计相关，但“遮挡”本身是独立缺陷，“整体重设计”是另一个产品/设计任务。
- `BUG-MOTION-001`：已修复为 opening Hand View 在 StartBattle 覆盖层期间保持隐藏，只在冻结的 `DrawPile → Hand` cue 开始时显示。
- `BUG-MOTION-002`：已修复为下一轮 Hand View 在伤害数字与 HealthLossShake 结束前保持隐藏，只在其 `DrawPile → Hand` cue 开始时显示。

`BUG-MOTION-001` 与 `BUG-MOTION-002` 的行为入口不同，应保留为两个验收项；当前代码证据却高度指向同一个结构原因：**权威 Hand 只有一份，但“普通布局回流”和“命令表现 cue”同时拥有可见手牌运动。** 现有证据不支持把 `BUG-MOTION-002` 归因于 Queue 缺少表现栅栏。

实现修改 `HandCardContainer.cs`、`HandCardVisual.cs`、`ParticipantHudView.cs`、`ParticipantHudView.prefab` 及其 Editor 测试；没有修改 Queue、Turn、settlement、Scene、DI、配置、资源或 Candidates。可见 Hand motion 的局部 gate 只保存“该 View 是否已经由 cue 展示”的表现生命周期，不保存或镜像 Hand 事实；权威卡区仍仅来自 `BattleCardZonesData.Layout`。生命 HUD 临时布局也只读取当前 `SpriteRenderer` bounds，不保存或镜像 Combatant 事实。

## 编号口径

聊天中“生命条遮挡”和“第一条卡牌运动问题”都曾被口头称作“第一个 BUG”。为避免后续测试、提交和验收互相覆盖，本页按问题域固定编号：

| 固定编号 | 聊天中的称呼 | 状态 |
|---|---|---|
| `BUG-UI-001` | `BUG-001：战斗生命条仍被遮挡` | 五种 M9 宽高比已运行时复现；临时头顶投影已在五种真实尺寸复测为 0 相交对，完整 UI 重做另行处理 |
| `BUG-MOTION-001` | “第一个 BUG”“两套抽牌动画” | 精确 EditMode 红灯已复现并转绿；真实 Game View 连续帧待补 |
| `BUG-MOTION-002` | “第二个 BUG”“敌人受击未完就抽牌” | 精确 EditMode 红灯已复现并转绿；真实 Game View 连续帧待补 |

## 证据等级

| 标签 | 本页含义 |
|---|---|
| 用户实机报告 | 用户已经在真实游戏画面观察到行为；不是 Agent 的独立复现证据 |
| 代码已观察 | 当前工作区源码或既有测试中可直接读到的事实 |
| 高置信推断 | 多条代码路径能够解释全部症状，但仍必须用精确红灯和连续帧闭环 |
| 已验证 | 有明确测试任务、运行结果或真实交互证据；本页只把证据限定到其实际覆盖范围 |

## BUG-UI-001 · 战斗生命 HUD 仍被遮挡

### 实际表现

战斗中玩家或敌人的生命条/生命数值仍会被其他画面元素遮挡，导致关键生存事实不易读取。

2026-08-05 使用既有 Editor 在 `Round 1 / PlayerAction` 的五种 M9 固定尺寸读取运行时矩形。三名 `ParticipantHudView` 的 `VitalsAnchor/HealthBar` 均由 `BattleScene` 的 `ScreenSpaceCamera` Canvas（sorting order `0`）承载；五张 `HandCardVisual.CardContent` 则各自位于 `ScreenSpaceOverlay` Canvas（sorting order `0`～`4`）。当生命条与手牌矩形相交时，Overlay 手牌在 Camera HUD 之后渲染，故会遮挡生命信息。

| 宽高比 | 固定尺寸 | 生命条与手牌的相交对数 |
|---|---:|---:|
| `16:7` | `1600×700` | 8 |
| `16:9` | `1600×900` | 6 |
| `16:10` | `1600×1000` | 6 |
| `16:11` | `1600×1100` | 5 |
| `16:14` | `1600×1400` | 5 |

这不是单张截图推断：每组都读取了所有参与者生命条与所有当前手牌的屏幕矩形，并以 `Rect.Overlaps` 计算交集。它已经确定遮挡者为手牌及其 Canvas 渲染层级；尚未把它扩张为整体 UI 重设计。

曾以未保存的 Play Mode 实验验证过 `ScreenSpaceOverlay`/`200` 能依靠排序覆盖手牌；该实验没有写入资产。用户随后明确后续 UI 将大规模重做，故本轮不固化该 Canvas 方案，而保留它为遮挡方向的诊断证据。当前临时修复只把生命/状态锚点移到角色头顶，并把名称置于其上方；它不隐藏或复制任何生命事实，也不改变 Canvas、Scene 或排序。

### 预期表现

- 存活参与者的生命条与当前/最大生命数值在战斗关键阶段持续清晰可读，不被角色、手牌、横幅或其他 HUD 覆盖。
- `0 HP` 世界 View 与生命 HUD 在 fatal 死亡过渡完成前仍保留；不能用提前隐藏来掩盖遮挡。
- 至少覆盖 M9 已锁定的 `16:7`、`16:9`、`16:10`、`16:11`、`16:14` 五种宽高比。

### 后续红灯与验收

1. 已在真实 BattleScene 固定参与者、阶段、遮挡者、五种宽高比与矩形交集；现有证据已确定手牌覆盖方向。
2. 临时修复仅修改 `ParticipantHudView`：生命/状态锚点投影到精灵 bounds 顶部外侧，名称以序列化偏移稳定置于其上方。
3. `LateUpdate_ProjectsVitalsAboveHeadAndNameAboveVitals` 已以实际 Prefab 通过，锁定头顶投影、名称间距和 Camera Canvas 投影；它不把 Canvas 排序或完整 UI 重设计伪装成已验收。
4. 已在真实 BattleScene 的五种 `M9D final` 尺寸复测 3 条实际 `HealthBar` 与 5 张 `CardContent`：每种均为 0 个矩形相交对；Game View 已恢复 `1600×1100`，本次启动的 Play Mode 已退出。此复测覆盖存活参与者，死亡过渡仍由既有 `ParticipantHudViewTests` 的生命 HUD 保留契约覆盖。

### 与整体 UI 重设计的边界

用户已明确后续 UI 会大规模修改。`REQ-UI-001` 因此是后续设计任务：本轮头顶投影只为保持当前生命信息可读，不设定最终 HUD 的层级、布局、交互或视觉规范。后续大改应整体替换该临时锚点规则；若要在此之前再扩大到 Canvas、Scene、层级注册或整体布局，必须先提交影响文件、风险与回滚方式并等待确认。

## BUG-MOTION-001 · 初始手牌提前出现并重复发牌

### 最小复现

1. 从 Bootstrap 进入新的 BattleScene。
2. 在“战斗开始”覆盖层仍显示时观察手牌区。
3. 当前画面已经显示初始手牌，表现为从屏幕中部快速落到基础手牌位置。
4. 覆盖层结束后，同一批权威初始手牌又按正式 `DrawPile → Hand` 路径逐张播放发牌。

### 实际表现

同一批初始手牌出现两次可见入场：先由普通手牌布局批量落位，再由 M9 的正式抽牌 cue 逐张入场。用户补充的“刚进场已经有牌”和“遮罩结束后又发一次牌”可由同一根因解释，不另拆第四个 BUG。

### 预期表现

- StartBattle 覆盖层期间，可见且可交互的 Hand View 数量为 `0`。
- 覆盖层完成后，每条权威 opening `CardMoved(DrawPile → Hand)` 只产生一次可见入场 cue。
- 不得在第一条正式 cue 前播放批量 base-pose 回流；最终 View 卡 ID 与当前权威 Hand 完全一致。
- 正常、立即完成、取消和 Scene/owner 销毁均不留下 ghost、Tween 或迟到 completion。

### 精确红灯与回归（已通过）

`StartBattle_OverlayBeforeOpeningDraw_DoesNotExposeHandBeforeCardMovedCue` 绑定真实 `CardZones.Layout` 订阅、`HandCardContainer`、`HandCardVisual` 与同一 runner。修复前任务 `a63b7dfd32a74427ac0bc28f5b925bcb` 稳定失败：覆盖层期间根 Canvas 已可见；修复后任务 `c7e59c0df1424678a38ba5ecebad0b25` 通过，覆盖层中 Canvas 保持关闭，随后唯一 `CardMoved` cue 才显示并启动 incoming motion。

## BUG-MOTION-002 · 玩家受击反馈未结束，下一轮手牌已经运动

### 最小复现

1. 进入敌方回合，并让最后一名敌人的攻击结束本轮 EnemyAction。
2. 观察玩家伤害数字、实际生命损失抖动与随后出现的下一轮手牌。
3. 当前可见结果是：伤害结算数值正确，但玩家受击动画刚开始或尚未结束，下一轮抽牌/落位已经开始。

### 预期表现

- 同一冻结结果中，敌人攻击的全部前序反馈必须先完成，再出现下一轮任何新手牌运动和正式 Player Turn 横幅。
- 权威 Turn 可以已经进入 `PlayerAction`，既有合法玩家意图仍可提交并由 Queue 排序；不得用全局 `IsWaitingForPresentation` 锁输入来掩盖时序问题。
- 若用户看到的是静态 phase 文本提前更新，而不是正式大横幅，需要作为投影时机的附加现象单独取证，不能在无证据时混写成横幅 bug。

### 精确红灯与回归（已通过）

`EnemyAttackBeforeRoundDraw_DoesNotStartHandMotionUntilDamageFeedbackCompletes` 把真实 `CardZones.Layout` 订阅与冻结 `Damage → HealthLossNumber → HealthLossShake → CardMoved` 链交给同一 runner。修复前同一任务 `a63b7dfd32a74427ac0bc28f5b925bcb` 稳定失败：伤害 cue 尚未完成时根 Canvas 已可见；修复后任务 `c7e59c0df1424678a38ba5ecebad0b25` 通过，所有前序伤害 feedback 结束前 Canvas 和 incoming motion 均保持关闭。

## 两个卡牌运动 BUG 的共同诊断

### 当前已观察的行为链

| 阶段 | 当前职责与事实 | 风险 |
|---|---|---|
| 权威结算 | Queue/Turn/settlement 在命令执行中同步得到 CardZones 与 Phase 的最终事实 | 本身没有发现第二份 Hand 或缺失 Queue completion |
| 普通 View 投影 | `HandCardContainer` 订阅 `CardZones.Layout`，重建/复用 Hand View，并只记录其当前 base pose | 未展示的 Draw→Hand View 保持隐藏，普通布局不拥有可见入场 |
| M9 一次性表现 | `BattleCommandPresentationAdapter` 从冻结结果派生 `CardMoved` cue，由同一 runner 串行播放 | cue 开始时才显示并播放唯一入场运动 |
| 用户观察 | 初始手牌重复入场；受击反馈期间下一轮手牌抢跑 | 两条精确红灯均转绿，真实 Game View 连续帧仍待补 |

相关 production 文件：

- [`HandCardContainer.cs`](../../../TinySpire/Assets/Scripts/UI/Battle/Hand/HandCardContainer.cs)
- [`HandCardVisual.cs`](../../../TinySpire/Assets/Scripts/UI/Battle/Hand/HandCardVisual.cs)
- [`BattleCommandPresentationAdapter.cs`](../../../TinySpire/Assets/Scripts/UI/Battle/BattleCommandPresentationAdapter.cs)
- [`BattleCommandPresentationRunner.cs`](../../../TinySpire/Assets/Scripts/UI/Battle/Feedback/BattleCommandPresentationRunner.cs)

### 为什么当前不改 Queue

- 现有 Queue 仍以 `IBattleCommandPresentation.Present(result, completion)` 的一次 completion 作为表现屏障；定向测试证明 runner 会等待手动 tick，并按 cue 顺序完成。
- 最后一名敌人的伤害、下一轮抽牌和 `EnemyAction → PlayerAction` 可以合法地存在于同一个 `CompleteEnemyAction` 冻结结果中。Queue 屏障只阻止下一条命令越过当前结果，不能阻止 runner 外的普通 UI Tween 抢跑。
- 因此当前最小修复目标是让 concrete UI 层只有一个可见手牌运动 owner，而不是增加第二个 completion、拆命令、重排 settlement 或扩展 Queue 契约。

## 与两份代码结构审查的关系

### 历史审查：M6 阶段产出的 M5 收口锐评

用户确认的历史文档是 [`2026-08-01-m5-architecture-roast.md`](../07_retrospective/2026-08-01-m5-architecture-roast.md)。它审查的是 M0～M5 已交付代码，产出时 M6 在途；因此本页称它为“历史架构审查（M6 阶段产出）”，不把它误称为 M6 验收或当前实现事实源。

| 历史发现 | 对当前 BUG 的意义 | 不能推出的结论 |
|---|---|---|
| §2.4 预警回合开始抽牌若同时存在阶段副作用与命令/记录路径，会留下两套时序到 M9 | 准确指出“抽牌事实与表现边界”是高风险接缝 | M7～M9 已补齐 settlement 与统一 runner；原文描述的 exact 缺口不等于当前 BUG |
| §2.7 指出自动阶段会在一个调用栈同步连跳并发布当前事实 | 解释了为什么权威 Hand/Turn 可以先于可见反馈抵达最终值 | 不代表 Queue 表现屏障失效 |
| §2.8 与 §6.6 指出 `HandCardContainer` 职责集中、交互/投影存在多 owner 风险 | 与当前“普通布局 Tween + 正式 cue”双 owner 直接相邻 | 不自动授权把整个 Container 在 BUG 修复中大拆 |

[`M6D 验收页`](2026-08-02-m6d-full-validation-review.md#m5-回顾意见的谨慎采纳与后期归属) 当时已经明确：回顾页只是建议来源；M6 只做计划内窄改，M7 承接结算/抽牌时序，M9 承接最终反馈，不为消除气味提前实施大改。当前处理仍沿用这个边界。

### 当前审查：M9 代码结构与实现质量审查

当前用户已有审查为 [`2026-08-05-m9-code-structure-review.md`](../plans/2026-08-05-m9-code-structure-review.md)。本页不修改该原文，只把其中建议与已报告 BUG 对照：

| 当前审查项 | 关联判断 |
|---|---|
| A1 `HandCardContainer.Start()` 初始化顺序脆弱 | 是真实风险，但当前没有证据证明它导致上述两个运动症状，不应借 BUG 顺手修改 |
| A2 `HandCardVisual` Tween 生命周期管理分散 | 与取消/抢占/清理相邻；可随精确红灯评估，但仅增加 `CardTweenScope` 并不能自动保证“只有一个可见运动 owner” |
| C1 拆分 `HandCardContainer` | 当前 BUG 为职责过度集中的结构问题提供了具体案例；完整拆分仍属于独立计划/架构决策，不是最小修复前置条件 |
| “架构亮点（不予修改）” | `BattleCommandPresentationRunner`、一次 completion、Plan 顺序校验、冻结结果与原子 CardZones Layout 均继续保留 |

两份审查都提供了有价值的方向，但当前 BUG 新增的具体结构结论是：**需要收口可见 Hand motion 的所有权，而不是仅仅统一 Tween 清理语法，也不是重写权威调度。**

## 已执行的支持性测试与覆盖缺口

分诊时已运行以下三个既有 EditMode 测试，任务 `4abbc7e83d2b4a58882570f0e94554b9`，结果 **3/3 passed，0 failed，0 skipped**：

1. `BattleCommandPresentationRunnerTests.Play_VisibleTimeline_WaitsForManualTickAndPreservesCueOrder`
2. `BattleCommandQueueM8DTests.EnemyAction_SingleEnemy_CommitsBlockEffectVulnerableIntentInOrder`
3. `BattleCommandQueueM8DTests.EnemyAction_SingleEnemy_ReshufflesDiscardedOpeningHandBeforeOrderedRoundDraw`

它们分别支持 runner 串行、敌人结算顺序和下一轮重洗/抽牌事实，但都没有把真实 `HandCardContainer` 的 Layout subscription 与伤害/抽牌 cue 放进同一个可见时间线。因此它们不是本 BUG 的复现或修复验收。

本次实现的两条精确红灯先在任务 `a63b7dfd32a74427ac0bc28f5b925bcb` 得到 **0/2 passed，2/2 failed**，再在任务 `c7e59c0df1424678a38ba5ecebad0b25` 得到 **2/2 passed**。相关 Hand/card-motion/adapter/runner 回归任务 `d925456056364adf9c6f10fa87cd3c2f` 为 **46/46 passed**；全量 EditMode 任务 `d40a8c5543194fa79db5ac18d5e561cb` 为 **425/425 passed，0 failed，0 skipped**。`dotnet build TinySpire/TinySpire.sln --no-restore -m:1` 为 **0 error**，保留 12 条既有程序集版本冲突 warning。

## 建议修复切片与停止点

### 切片 A · 两个运动红灯（已完成）

- 在现有 `TinySpire/Assets/Editor/Tests/` 下复用 production container、adapter 与手动时间推进。
- 首先证明覆盖层期间 Hand 不可见，其次证明受击反馈结束前 Hand 不运动。
- 红灯只读当前权威 CardZones/Turn 与冻结结果，不建立第二份 Hand、outcome 或 phase 镜像。

### 切片 B · 最小收口可见 Hand motion owner（已完成）

- 普通 Layout 投影仍负责把 View 集合收敛到当前权威 Hand，并以局部 View lifecycle gate 隐藏未展示的 Draw→Hand View；它只更新 base pose，不再在 cue 之前制造可见入场运动。
- 抽牌、弃牌、出牌、重洗的一次性轨迹继续只由既有 adapter/runner 消费冻结结果；completion、取消与场景销毁仍只有原边界。
- 预计优先范围是 `HandCardContainer.cs`、`HandCardVisual.cs`、现有 hand/card-motion/adapter Editor tests；只有红灯证明必要时才触及 concrete motion factory 或 adapter。
- 不修改 Queue、Turn、settlement、公式、目标、终局、domain/public seam、Scene、DI、DataTables、Localization 或随机策略；不使用 Candidates。

### 切片 C · 独立处理生命 HUD

- `BUG-UI-001` 已在五种宽高比确认手牌会在相交时盖住 Camera Canvas 的生命 HUD；不需要修改参与者事实、Queue 或 Canvas 排序。
- 用户明确后续 UI 会大规模重做后，本轮只修改 `ParticipantHudView.cs`、`ParticipantHudView.prefab` 与投影测试：生命/状态锚点移到角色头顶，名称向上错开；不修改 `BattleScene.unity`、Canvas 或排序。
- 先前未保存的 Overlay/`200` 实验只是渲染方向的诊断证据，未写入资产，也不属于当前实现。
- 后续整体 Battle UI 重设计应另立需求与计划，并整体替换这项临时布局规则。

### 独立架构工作

- A2 的 Tween owner/清理收敛可在 BUG 红灯保护下做窄重构。
- C1 的完整 `HandCardContainer` 拆分、factory DI 化或其他 B/C 项必须单独规划、单独验证，不与行为修复混交。
- 若精确红灯证明现有 settlement/只读事实不足，或修复需要 Queue/Turn/settlement 契约、第二动画队列、事件总线、全局输入锁、公开 terminal API、Scene/计划外 Prefab/DI，立即停止并请求确认。

## 验收范围与保留证据边界

- 两个精确红灯均已创建，且已经在旧行为下失败、在修复后转绿；相关 46/46 与全量 EditMode 425/425 通过，solution build 为 0 error。
- 已在既有 Editor 的五种 M9 固定尺寸取得 `BUG-UI-001` 的运行时矩形和 Canvas 排序证据；它们是修复前红灯，不代表头顶投影已经完成真实 Game View 验收。
- Overlay/`200` 只在未保存的 Play Mode 中作为候选排序诊断，不是本次资产修改，也没有真实连续帧或正式 Scene 回归测试。
- Unity 已导入本次改动，Console Error 为 0。新增 `LateUpdate_ProjectsVitalsAboveHeadAndNameAboveVitals` 以实际 `ParticipantHudView.prefab` 验证头顶世界点、名称间距和 Camera Canvas 投影，任务 `765ccd9eae494101a1a7ae673057b23c` 为 **1/1 passed**；全量 EditMode 任务 `d50762b82f0147df82921b0e6c388c00` 为 **426/426 passed**；solution build 为 0 error、12 条既有版本冲突 warning。
- Agent 尚未独立取得 BUG 三项的真实 Game View 连续帧或短录屏。
- 已在真实 BattleScene 的 `M9D final` 五种尺寸完成修复后的生命 HUD 复测：每种均读取 3 条实际 `HealthBar` 与 5 张 `CardContent` 的屏幕矩形，并得到 0 个相交对。测量期间依次切换 `1600×700`、`1600×900`、`1600×1000`、`1600×1100`、`1600×1400`，结束后恢复 `1600×1100` 并退出本次启动的 Play Mode。尚未取得修复后战斗开始/敌人攻击的真实跨帧录屏；自动化已覆盖 runner 的立即完成/取消契约，但不是该录屏证据。
- 本次修改了常规 HUD Prefab 的序列化偏移与脚本，没有修改 Addressables 配置、Scene、配置或 Localization；`ParticipantHudView.prefab` 的 GUID 不在现有 Addressables group asset 中，故 Local Content 重建不适用。

## 后续待确认信息

1. `BUG-MOTION-002` 中提前出现的是正式 “Player Turn” 大横幅、静态 phase HUD，还是只有手牌运动。
2. 后续整体 UI 重做时，生命 HUD 最终应采用哪一类布局与层级方案；本轮头顶投影不预设其答案。
3. 普通回合抽牌与重洗抽牌是否也能观察到与 opening draw 相同的“双入场”；这会影响复现矩阵，但不改变单一 motion owner 的修复边界。

## 相关文档

- [M9 唯一实施计划](../plans/2026-08-02-m9-sts-feedback-outcome-restart.md)
- [M9G 历史全量验收](2026-08-02-m9g-full-validation-review.md)
- [M9E 卡区运动验收](2026-08-02-m9e-card-zone-motion.md)
- [M9 代码结构与实现质量审查](../plans/2026-08-05-m9-code-structure-review.md)
- [M6 阶段产出的 M5 收口架构锐评](../07_retrospective/2026-08-01-m5-architecture-roast.md)
- [M6D 全量验证与回顾意见分流](2026-08-02-m6d-full-validation-review.md)
