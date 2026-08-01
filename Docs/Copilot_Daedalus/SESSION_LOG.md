---
created: 2026-07-06
updated: 2026-08-01
---

## 2026-08-01 · M5D 全量验证、双轴复审与 M5 收口（已完成）

- M5A～M5D 已串行完成：Enemy 静态模板引用有序行为组；`BattleEnemyIntentsData` 以独立确定性随机流和不可变完整快照持有每名敌人的权威当前 `BehaviorId`；M4 合法敌人完成命令先原子选择下一意图，再保证推进 Encounter 顺序；M3D HUD 从同一意图、静态 Effect 与当前参与者事实派生正式图标和值。
- 最终 Luban 等价命令成功生成 C# 与 `Assets/GameData` JSON；生成器清理的手写 `game-config.json` 已从既有源逐字恢复，双方 SHA-256 为 `048CDC9E8DB80F80BE9E43D409ED1A91A011E0118CBAB18EC207509B3C904CF8`。最终 Addressables 报告 `buildlayout_2026.08.01.09.39.35.json` 的 `BuildError` 为空、`BuildResultHash=d030cfdcfd7d76e4ca432b66eae62cea`、耗时 `8.6252568s`，M5 JSON、game-config、BattleScene 与 HUD Prefab 的完整稳定地址均存在。
- Unity MCP 最终 M5 定向 EditMode **73/73**、全量 EditMode **98/98** 通过，均为 0 failed、0 skipped；串行 `dotnet build TinySpire/TinySpire.sln --no-restore -m:1` 为 0 error，保留 12 条既有依赖版本冲突 warning。
- 两次从 Bootstrap 使用同一 Inspector 种子 `1` 实跑得到完全相同序列：初始 ID `2 → 3`、`7001 Attack / 7003 Defend`；ID 2 完成后发布 `7001/7003`，ID 3 完成后发布 `7001/7002`；第二轮双方 HUD 均为正式攻击图标和值 6，玩家意图隐藏。两次 Console Error/Warning 为 `0/0`，没有 `InvalidKey` 或 VContainer 错误。
- M5C 已在实际 Game View 用不保存资产的运行期夹具目视确认 1～3 敌人图标语义、数值可读性和 HUD 不重叠。计划内 attack/defend/buff/debuff/special 五类正式图标全部存在且导入合约正确，没有缺失美术资源。
- Standards / Spec 首轮并行复审各自只指出同一项 P2：最终验证已写入验收页，但 `SESSION_LOG.md` 与计划状态尚未收口。现已通过本条状态源、计划归档和 M5D 验收页回填修复；两轴均未发现代码实现、规格偏差、scope creep 或明确代码气味。M5 未实现真实 Effect、伤害、格挡、状态、死亡动画、胜败、行为树或 DSL；`DEP-009` 保持 open，剩余工作为 M7/M8 的真实敌人 Effect 执行。
- 本次保护了启动前唯一既有未跟踪计划文件，并在其上同步状态；未修改 `BattleScene.unity`、ProjectSettings、asmdef、HybridCLR、Run 生命周期或启动流程，未 commit、未 push。详细证据见 `06_testing/2026-08-01-m5d-full-validation-review.md`，决策见 CD-032～CD-034。

## 2026-08-01 · M5C 敌人意图 HUD（已验证）

- `ParticipantHudView.prefab` 以静态 `IntentRoot / IntentIcon / IntentValueText` 子树接入五类正式意图 Sprite；所有 `_ref_` 参考图均未进入生产 Prefab。玩家 HUD 固定隐藏意图，存活敌人从同一 `EnemyIntentLayoutData` 的 `BehaviorId`、Luban 行为/Effect 模板和当前参与者事实即时派生图标与数值，死亡时隐藏。
- 原 `CardValueCalculator` 以保留 Meta GUID 的方式最小深化为 `BattleEffectValueCalculator`，卡牌文本与敌人 HUD 共用同一效果值计算入口；力量、生命、Locale、意图快照或 View 重建只触发展示重派生，不保存预测值，也不调用行为选择。`BattleParticipantPresenter` 只把现有 Session、Tables 与世界 View 交给 HUD，没有新增事实镜像或 DI 层。
- Unity MCP 定向 EditMode **39/39** 通过，覆盖共享效果值、HUD 纯投影、Prefab 正式资源/层级合约、权威意图核心、Session 和命令队列。`TinySpire/Addressables/Build Local Content` 成功；报告 `buildlayout_2026.08.01.09.25.30.json` 的 `BuildError` 为空、`BuildResultHash=d030cfdcfd7d76e4ca432b66eae62cea`、耗时 `15.5289761s`。
- Bootstrap 生产实跑首轮显示 Enemy 2001 `Behavior 7001 / Attack / 6`、Enemy 2002 `Behavior 7003 / Defend / 5`，玩家意图隐藏；第一轮完成后进入 Round 2，Enemy 2002 变为 `Behavior 7002 / Attack / 6`，HUD 与事实同步。重复读取前后敌人随机状态均为 `2144564843`。
- Game View 用现有正式 View/HUD 构造了不保存场景或 Prefab 的运行期 1/2/3 敌人视觉夹具，确认意图不与名称、生命和力量 HUD 重叠。MCP 截图实现自身曾写入 5 条 `PlayerLoop recursive` 错误；销毁夹具、退出并从 Bootstrap 干净复跑后 Console Error/Warning 为 `0/0`，未出现 `InvalidKey` 或 VContainer 错误。
- 本切片只修改 `ParticipantHudView.prefab`，未修改 `BattleScene.unity`、asmdef、ProjectSettings、启动流程或 DI 架构；未执行真实 Effect、伤害、格挡、状态、死亡动画或胜败。五类计划内正式意图图标均已存在，没有缺失美术资源。决策见 CD-034，验收见 `06_testing/2026-08-01-m5c-enemy-intent-hud.md`。

## 2026-08-01 · M5B Session、权威命令队列与生产接线（已验证）

- `BattleSession` 现在在按 Encounter 顺序创建敌人后建立并公开唯一 `BattleEnemyIntentsData`，构造失败会释放已经创建的意图、卡区与参与者，正常销毁时由 Session 先释放意图再释放其依赖事实。`BattleLifetimeScope` 把该同一实例交给命令队列，没有第二份聚合或额外 DI 层。
- `CompleteEnemyActionCommand` 到达队首后，`BattleTurnController` 先只读校验阶段、敌人身份与当前行动者；通过后由队列调用 `EnemyIntents.CompleteAndSelectNext`，成功才调用不可失败的 Encounter 顺序推进。无候选异常会让队列停在当前命令，意图、随机与回合均不变；错误阶段、错误敌人和重复完成继续返回 M4 原失败原因且零写入。
- `BattleCommandRuntimeDriver` 仍只在队列空闲时每帧提交一条当前敌人完成命令，不直接读取候选或调用随机。进入敌人轮前已死亡的敌人继续由 M4 Encounter 顺序跳过，不为其补选意图；第一版仍不执行伤害或其他 Effect。
- Unity MCP 相关 EditMode **47/47** 通过，覆盖 M5A 核心、Session、M5B 意图/队列集成与完整 `BattleCommandQueueTests` 回归。特别验证意图发布先于 Turn 推进、第一名完成不改变第二名、错误/重复完成零写入、死亡跳过、无候选停止命令链，以及生产驱动两轮每帧最多完成一名敌人。
- `TinySpire/Addressables/Build Local Content` 成功；报告 `buildlayout_2026.08.01.09.07.56.json` 的 `BuildError` 为空、`BuildResultHash=d8794bc54bf6fa0df3cc1595bc89c6ef`、耗时 `13.7309617s`，两个新增行为 JSON 与 BattleScene 均保留完整稳定地址。
- Bootstrap 实跑初始为 `PlayerAction / Round 1`，Enemy 2001 为 Behavior 7001 / Attack、Enemy 2002 为 Behavior 7003 / Defend；生产命令链完成两轮后进入 `PlayerAction / Round 3`，两名敌人生命均保持 20，Console Error/Warning 为 0。M5B 未修改场景、Prefab 或 HUD；M5C 尚未开始。决策见 CD-033，验收见 `06_testing/2026-08-01-m5b-session-command-queue-wiring.md`。

## 2026-08-01 · M5A 敌人行为配置与确定性选择核心（已验证）

- 新增 `EnemyBehaviorGroup` / `EnemyBehavior` Luban 表与 `EnemyIntentType` 枚举；Enemy 通过 `behavior_group_id` 引用行为组，默认 Encounter 5001 现在按顺序包含固定行为 Enemy 2001 与加权随机 Enemy 2002。行为只引用既有 `CardEffect` 数值，不执行 Effect。
- 新增 `BattleEnemyIntentsData`：按 Encounter 顺序为每名敌人选择初始意图，以不可变完整 R3 快照持有唯一的 `CombatantId -> BehaviorId` 事实；每名敌人只保存冷却与最大连续次数所需的已完成历史。敌人行为随机从战斗种子以稳定盐派生独立 `GameRandom`，单候选不消费随机，多候选只调用一次整数权重选择。
- 行动完成后的候选过滤、历史更新、随机选择与意图发布先在副本上完成；无候选或配置错误会恢复随机状态且不发布新快照。错误引用、非正权重、负冷却/连续上限、重复行为和权重溢出均显式失败，没有随机回退。
- 六份工作簿已通过 `@oai/artifact-tool` 编辑、渲染与公式错误扫描；Luban 等价命令成功生成新增配置 C# 与 `Assets/GameData` JSON，`ConfigService` 已预加载两张新表。Unity MCP 定向 `BattleEnemyIntentsDataTests + BattleSessionTests` **18/18** 通过，脚本编译与 Console Error 为 0。
- 本切片未修改 `BattleSession` 生产持有关系、M4 命令队列、回合推进、场景、Prefab 或 HUD，也未实现真实伤害、格挡、状态、胜败、行为树或条件 DSL。M5A 已满足独立停止点；M5B 尚未开始。决策见 CD-032，验收见 `06_testing/2026-08-01-m5a-enemy-behavior-selection.md`。

## 2026-08-01 · M4E 全量验证、轮次栅栏修复与文档收口（已完成）

- 首次 Spec 复审与生产探针发现：全体敌人已死亡时，上一轮排在结束命令后的玩家命令会在同步开始的新一轮重新合法。用户确认“玩家命令只能属于提交时的轮次”，并授权采用队列内部轮次栅栏；该问题属于当前 M4 已承诺行为，没有登记为未来依赖。
- `BattleCommandQueue` 的内部排队信封现在记录提交 `RoundNumber`。`PlayCardCommand` 与 `EndPlayerActionCommand` 到达队首时若已经跨轮，返回 `PlayerActionWindowExpired`，不调用 `BattleTurnController`；公共命令构造参数、`Submit` / `Queue` / `Turn` seam、`BattleTurnData` 与 DI 均未改变。两条 TDD 用例分别覆盖全敌死亡后的重复结束，以及同一 `CardInstanceId` 下一轮重抽后的旧出牌，均先复现旧行为再通过修复。
- 修复后的 Spec 复审又发现同 ID View 的展示关联风险：旧序号失败可能误清更新序号 pending。`HandCardVisual` 现以 nullable 权威序号作为 pending 唯一事实，失败只清除匹配序号；`HandCardContainer` 在 View 重建时从既有映射恢复最新待定序号。该行为同样完成红灯到绿灯闭环。
- 最终 Unity MCP 三项缺陷复合切片 **3/3**、M4 队列/回合/展示定向 EditMode **30/30**、全量 EditMode **70/70** 通过，均为 0 failed、0 skipped；串行 `dotnet build TinySpire/TinySpire.sln --no-restore -m:1` 为 0 error，保留 12 条既有依赖版本冲突 warning。
- Bootstrap 正常路径完成两个轮次并进入 `PlayerAction / Round 3`。生产跨轮探针将两条结束命令同时排在 Round 3：第一条进入 Round 4，第二条反馈 `Failed #8 · EndPlayerAction · PlayerActionWindowExpired`；最终仍为 `PlayerAction / Round 4 / Energy 3 / HasEndedAction false`、队列空闲，运行期 Console Error/Warning 为 0。
- 生产 View 重建探针在 Round 3 观察到与旧手牌重叠的 ID 4、9 两个新 View 均恢复 pending；旧跨轮出牌全部失败后当前 5 个 View pending 为 0、队列空闲，Console Error/Warning 为 0/0。
- 最终 `TinySpire/Addressables/Build Local Content` 成功；报告 `buildlayout_2026.08.01.06.41.41.json` 的 `BuildError` 为空、`BuildResultHash=259e02cf2d79b5cd0bd291f571b46782`、耗时 `16.9804458s`，场景与七个 GameData JSON 保持完整稳定地址。本阶段未修改 `DataTables/Datas/`、生成 JSON、场景、Prefab、ProjectSettings、asmdef、DI 或启动流程，因此未运行 Luban。
- Standards / Spec 双轴复审通过本次修复范围；既有三个 M4 提交摘要缺少冒号后空格的问题只记录、不改写历史，`BattleTurnController` 重复校验链也不借此扩展重构。M4E 与 M4 已完成，计划转入历史归档；详细证据见 `06_testing/2026-08-01-m4e-full-validation-review.md`，实现决策见 CD-031。

## 2026-08-01 · M4D 当前单玩家命令 UI 接线（已验证并接生产）

- `HandCardContainer` 不再直接弃牌或推进阶段：拖牌越过出牌线只提交 `PlayCardCommand`，并用权威序号关联该卡的短生命周期 pending 视觉。当前结果展示期间其他合法卡仍可继续提交；成功后由权威卡区布局移除 View，执行期失败则清除 pending、恢复交互，且能量和卡区保持不变。
- 新增 `BattleCommandPresentationAdapter`，生产中分别发布“已排队 / 执行失败 / 执行完成”反馈并在非缩放时间内保留最短展示窗口；新增 `BattleTurnHudView` 与静态 `BattleTurnHud.prefab`，从 `BattleCommandQueue.Turn` 和展示反馈派生第几轮、阶段、当前玩家能量、状态文字及按钮可用性。结束按钮只提交 `EndPlayerActionCommand`；成功进入敌人阶段后手牌输入立即锁定，系统敌人完成命令后下一轮恢复 3 能量、5 张新手牌和输入。
- Unity MCP 定向 EditMode **30/30** 通过（0 failed、0 skipped，`0.3453076s`）；两套相关程序集串行静态编译均为 0 error，保留 6/12 条既有依赖版本冲突 warning。Bootstrap 实跑首轮读取到 `PlayerAction / Round 1 / Energy 3 / Hand 5`，快速连续提交两张费用 1 的牌后按权威序号依次变为 1 能量、3 手牌；能量不足的费用 2 卡执行失败后仍为 1 能量、3 手牌并恢复交互。实际拖拽处理链由运行时回调执行，物理鼠标手感不冒充自动化结论。
- 从场景结束按钮实际 `onClick` 提交后，阶段进入 `EnemyAction`、剩余手牌统一弃置且旧 View 立即不可交互；系统完成无行为敌人后进入第 2 轮，能量恢复 3、手牌恢复 5、按钮和新手牌重新可用。运行期间 Console Error/Warning 均为 0，未出现 `InvalidKey` 或 VContainer 错误；随后正常退出 Play Mode。
- 场景和 Prefab 接线完成后，`TinySpire/Addressables/Build Local Content` 成功；报告 `buildlayout_2026.08.01.04.27.53.json` 的 `BuildError` 为空、哈希为 `259e02cf2d79b5cd0bd291f571b46782`、耗时 `19.984942s`。本切片未修改 Excel/Luban 表、生成 JSON、asmdef、DI 架构或启动流程，因此未运行 Luban；未实现 M4E 全量收口、真实敌人行为、Effect、伤害、状态、胜败或奖励。
- `DEP-002` 已解决；`DEP-001`、`DEP-004` 继续保持 open，M4E 尚未开始。决策见 CD-030，计划见 `plans/2026-07-31-m4-turn-scheduling-energy.md`，验收见 `06_testing/2026-08-01-m4d-single-player-command-ui.md`。

## 2026-08-01 · M4C 队列化结束行动与敌人顺序交接（已验证并接生产）

- `EndPlayerActionCommand` 现在在队首执行时校验阶段、玩家身份与存活、重复结束及玩家卡区；成功后只弃置该玩家剩余手牌并设置其结束标记。仍有存活玩家未结束时继续保持 `PlayerAction`，全体完成后才进入敌人阶段；重复结束和排在结束命令后的旧出牌均明确失败且不重复写入事实。
- 敌人阶段只读取 `BattleSession.EnemyCombatantIdsInEncounterOrder`：死亡或缺失敌人会跳过，每次只发布一个 `CurrentActingEnemyId`，错误或重复的 `CompleteEnemyActionCommand` 不会越过当前敌人。当前无行为敌人由生产逐帧入口在后续帧经同一 `BattleCommandQueue.Submit` 完成，每帧最多一名，没有场景直通阶段写入。
- `BattleSession` 现在只创建参与者、运行时卡牌实例和洗牌后的未发牌抽牌堆；`StartBattleCommand -> PlayerRoundStart` 成为首轮与后续轮次重置每玩家能量、结束标记并抽到目标手牌数的唯一入口。`BattleLifetimeScope` 已注册队列、即时表现 adapter 与 `BattleCommandRuntimeDriver`；当前生产 Session 仍只映射唯一玩家卡区，`DEP-008` 保持 open。
- Unity MCP 定向 EditMode **27/27** 通过；`Assembly-CSharp` 与 `Assembly-CSharp-Editor` 静态编译均为 0 error（保留 6/12 条既有依赖版本冲突 warning），脚本刷新后 Console Error 为 0。Bootstrap 实跑进入 BattleScene 后从生产容器读取到 `PlayerAction / Round 1 / Energy 3 / Hand 5 / queueIdle=true`，加载日志正常且 Error 为 0，随后正常退出 Play Mode。
- 本切片未修改 Excel/Luban 表、手写 JSON、Addressables 内容、场景、Prefab、asmdef、HybridCLR 或现有 UI，因此未运行 Luban 或重建 Addressables。拖牌提交、能量/回合显示和结束按钮仍属于 M4D；真实敌人行为与 Effect 仍分别属于 M5/M7。M4C 已满足独立停止点，M4D～M4E 尚未开始。决策见 CD-029，计划见 `plans/2026-07-31-m4-turn-scheduling-energy.md`，验收见 `06_testing/2026-08-01-m4c-end-action-enemy-handoff.md`。

## 2026-08-01 · M4B 队列化出牌、能量与执行期校验（已验证，未接生产）

- `GameConfig` 新增 `EnergyPerRound`，代码默认值、`DataTables/game-config.json` 与 `Assets/GameData/game-config.json` 均为 3；两份 JSON 内容一致。该配置是手写运行时规则，不是 Luban Excel 表，因此本切片未改工作簿或生成代码。
- `BattleCommandQueue` 现在把权威参与者、`CombatantId -> BattleCardZonesData`、Luban `Tables` 与每轮能量交给内部 `BattleTurnController`。`PlayCardCommand` 到达队首后依次校验阶段、玩家身份与存活、结束行动标记、玩家卡区、手牌实例、静态 `Card.Cost` 和当前能量；成功才把指定实例移入弃牌堆并扣该玩家能量，失败只返回明确执行原因且不发布新事实。
- 公共 `Submit` / `Queue` / `Turn` seam 的相关 EditMode **18/18** 通过：新增覆盖费用 1+2 顺序归零、首张牌展示期间另一玩家继续提交、旧能量重校验防透支、排队期间卡牌离手、敌人冒充玩家、死亡玩家、缺少玩家卡区和缺少静态模板；M4A 原有顺序与重复回调行为继续通过。
- `Assembly-CSharp` 与 `Assembly-CSharp-Editor` 静态编译均为 0 error（保留 6/12 条既有依赖版本冲突 warning），Unity 刷新后 Console Error 为 0。`TinySpire/Addressables/Build Local Content` 已完成；报告 `buildlayout_2026.08.01.01.41.10.json` 的 `BuildError` 为空、哈希为 `4877b4655f41f300d0ffc1bb4c37fb25`、耗时 `52.562s`，并确认 `Assets/GameData/game-config.json` 仍以完整稳定地址进入 `TinySpire GameData`。Bootstrap 短时运行打印“game-config.json 已加载。”，Error 与 `InvalidKey` 均为 0，随后正常退出 Play Mode；模式切换期间仅有 MCP 传输 warning。
- 未修改 `BattleSession` 初始抽牌、`BattleLifetimeScope`、场景、Prefab、asmdef 或 UI，也未实现真实 Effect、结束玩家行动或敌人交接；未运行 Luban、全量 EditMode 或完整 BattleScene 功能实跑。M4B 已满足独立停止点，M4C～M4E 尚未开始。决策见 CD-028，计划见 `plans/2026-07-31-m4-turn-scheduling-energy.md`，验收见 `06_testing/2026-08-01-m4b-queued-card-play-energy.md`。

## 2026-08-01 · M4A 权威命令队列与回合事实骨架（已验证，未接生产）

- 新增纯 C# `BattleCommandQueue` 调度根、四类首批命令、提交/执行结果与只读 `Queue`/`Turn` R3 事实；本地权威序号从 1 单调递增，当前命令执行和等待表现期间都可继续提交，只有绑定当前序号的表现完成回调可以推进下一条。
- `BattleTurnController` 保持队列内部，通过既有 `StateMachine<TEvent>` 组合 `NotStarted -> BattleStart -> PlayerRoundStart -> PlayerAction`；回合事实按 `CombatantId -> PlayerTurnData` 保存，M4A 能量骨架为 0，没有 `CurrentPlayer` 或全局 `CurrentEnergy`。
- TDD 通过公共 `Submit` / `Queue` / `Turn` seam 完成 9 个 EditMode 用例：覆盖未开始拒绝、执行期与等待期提交、稳定序号、FIFO 交接、重复表现回调、重复开始执行期失败、后续里程碑命令不改共享事实及双玩家独立映射。Unity MCP 定向测试 9/9；`Assembly-CSharp` 与 `Assembly-CSharp-Editor` 静态编译均为 0 error（保留 6/12 条既有依赖版本冲突 warning）。
- Unity MCP 已为新增目录、脚本和测试生成全部 Meta；Console Error 为 0。未修改 `BattleSession` 的现有初始抽牌，未接 `BattleLifetimeScope`、场景或 UI，也未实现能量扣除、出牌结算、结束玩家行动或敌人行为；未运行全量 EditMode、PlayMode、Luban 或 Addressables 构建。
- M4A 已满足独立停止点；M4 总计划仍为 active，M4B～M4E 尚未开始。实现计划见 `plans/2026-07-31-m4-turn-scheduling-energy.md`，验收记录见 `06_testing/2026-08-01-m4a-authoritative-command-queue.md`。

## 2026-07-31 · M4 多人命令队列口径修订（计划完成，尚未实施）

- 用户修订旧的“交错出牌/一张牌后切人”口径：所有未结束玩家可同时提交命令，提交不因其他玩家输入或当前效果展示而阻塞；权威调度层建立唯一顺序，再逐条执行共享状态修改和效果展示。全部玩家的结束命令均执行后才进入敌人阶段。
- 外部查证区分了原版《杀戮尖塔》的行动队列与《杀戮尖塔 2》的多人模型。当前结论锁定逻辑上的统一权威顺序，不声称未来必须使用单一物理 FIFO；研究记录见 `04_research/2026-07-31-slay-the-spire-action-queue.md`。
- M4 外部 seam 改为 `BattleCommandQueue.Submit` 与只读 `Queue`/`Turn` 事实；`BattleTurnController` 退为队列内部阶段模块。提交接受与执行成功分离，最终合法性在队首执行时重新校验。
- M4A 改为权威命令队列与调度骨架；M4B 为队列化出牌、能量与执行期校验；M4C 为队列化结束行动、敌人交接和生产接线；M4D 接当前单玩家 UI；M4E 全量验证。TDD seam 同步改为命令提交及队列公开事实。
- `DEP-008`/`DEP-009` 保持；新增 `DEP-010` 记录命令中途局部输入续接，`DEP-011` 记录未来网络权威确认与重放。本轮只修改文档，未修改代码、场景、配置或资源，未运行 Unity 测试。
- 完整计划见 `plans/2026-07-31-m4-turn-scheduling-energy.md`，代码决策见 CD-027，架构约定见 AC-009。

## 2026-07-31 · 牌面短键与 Addressables 逻辑地址迁移（已验证）

- `battle.card.xlsx` 已由 `illustration_address` 迁移为 `illustration_key`，策划只填写不带目录和扩展名的文件短名；ClosedXML 临时副本比较确认除 H1、H4、H5:H8 外，值、公式、样式与版式均未变化。Luban 已生成 `Card.IllustrationKey` 和对应 JSON。
- 四张动态牌面已集中到 `Assets/Arts/Runtime/Card/Illustrations/`，原 `.meta` GUID 全部保留。运行时统一把短键转换为 `card-art/{key}`；构建工具按文件短名建立不区分大小写的索引，阻止重名、缺失引用和非 `Sprite / Single / no mipmap` 资源。
- `TinySpire Card Art` 继续使用本地 `PackTogether` AssetBundle，但四个条目地址已改为 `card-art/*`。最终 `TinySpire/Addressables/Build Local Content` 成功（6.7 秒）；定向 EditMode 4/4、全量 EditMode 38/38、静态编译 0 error，四个逻辑地址均可通过 Addressables API 加载 Sprite。
- BootstrapScene 短时启动未出现 Error、`InvalidKey` 或资源地址错误。未修改图片像素、卡背、Prefab、场景、战斗逻辑及其他资源配置字段。实现见 `plans/2026-07-31-card-illustration-logical-keys.md`，验收见 `06_testing/2026-07-31-card-illustration-logical-keys.md`，决策见 CD-026。

## 2026-07-31 · DataTables 工作簿简易配色（已验证）

- `DataTables/Datas/` 下 10 个 `.xlsx` 已统一使用低饱和配色：首行深蓝底白色粗体，Luban 类型行浅蓝、分组行浅灰、说明行浅金；内容区按列循环使用蓝、绿、金、紫、橙、青六组淡色，并用同色深浅交替区分相邻数据行。没有新增、删除或改写任何单元格内容、公式、字段、表定义或共享字符串。
- OpenXML 写入在临时副本中完成，逐工作簿对比配色前后语义 SHA-256，10/10 一致；所有 XML、关系文件与样式索引均可解析。Luban 生成成功，生成目录中的 55 个 C# / JSON 文件前后内容哈希变化为 0。
- Unity MCP 回归先后暴露四张牌面的磁盘导入模式不一致；已通过当前 Editor 将 strength、strike、defend、bash 全部统一为 `Sprite / Single / no mipmap`，未改牌面地址或图片像素。最终定向 EditMode 1/1、全量 EditMode 35/35 通过，清理 Test Runner 结果写入提示后 Console Error 为 0。
- 最终 `TinySpire/Addressables/Build Local Content` 成功，报告 `buildlayout_2026.07.31.20.39.59.json` 的 `BuildError` 为空，构建哈希为 `f347180971402fb852359628813c07b2`，耗时 `8.911s`。本次没有新增代码决策；详细记录见 `06_testing/2026-07-31-datatables-simple-colors.md`。

## 2026-07-31 · 战斗 UI 首批美术与牌面配置链路接入（已验证）

- 按 `10_communication/2026-07-30-battle-ui-art-brief.md` 接入当前已有运行时事实能够承载的 P0/P4 素材：BattleScene 三个牌堆计数改用共用九宫格面板及抽牌/弃牌/消耗图标；`ParticipantHudView` 改用生命框、横向填充与力量图标。P1-P3 所对应的能量、回合、敌人意图、状态与结算覆盖层没有提前创建占位状态。
- `DataTables/Datas/battle.card.xlsx` 新增 `illustration_address`，四个模板使用完整 `Assets/Arts/Runtime/Card/card_art_*.png` 稳定地址；Luban 已重新生成 `Card.IllustrationAddress` 与 `Assets/GameData/battle_tbcard.json`。
- 四张牌面已统一导入为 `Sprite / Single / no mipmap`。`AddressablesBuildTools` 从生成卡牌表收集并校验地址，使专用 `TinySpire Card Art` 本地组与表中地址完全同步；`HandCardContainer` 按牌组唯一模板预加载并在销毁时释放句柄，`HandCardVisual` 让横图等比 cover 插图区后交给现有 Stencil Mask 裁切。
- Unity 6000.5.5f1 当前 Editor 内完成编译、定向测试 1/1、全量 EditMode 35/35、最终 Addressables 本地构建（19.026 秒）与 Bootstrap→BattleScene 实跑。初始 5 张手牌均加载到对应牌面，显示尺寸为 `862.5×575`、遮罩为 `682×575`，比例无拉伸；Console 错误、`InvalidKey` 与牌面加载失败均为 0。
- 实现边界和回滚见 `plans/2026-07-31-battle-ui-art-integration.md`，验收细节见 `06_testing/2026-07-31-battle-ui-art-integration.md`，资源事实与生命周期决策见 CD-025。

## 2026-07-30 · CardView 旋转插图灰边修复（待 Unity 人工验收）

- 用户报告手牌扇形布局中，只有旋转卡的插图区出现灰色边缘。代码与 Prefab 静态检查确认：`HandCardVisual` 旋转 `CardContent`，而 `IllustrationMask` 使用轴对齐的 `RectMask2D`，导致其子节点 `Illustration` 被错误裁剪并露出下层 `CardBase`。
- `CardView.prefab` 已将 `IllustrationMask` 的裁剪组件替换为 `Mask`，保留既有 `Image`、尺寸、卡图资源与层级，并关闭 `Show Mask Graphic`，使模板裁剪区域随卡片旋转。
- 新增 CD-024、实施方案与验收记录；未修改 C#、手牌布局参数、场景、数据表、资源地址或 Addressables 配置。
- 当前检测到用户正在使用 Unity，未启动第二个 Editor 或批处理实例；请在该 Editor 执行 `TinySpire/Addressables/Build Local Content`，并在 BattleScene 检查左右倾斜卡、悬停归零和拖拽时是否还会露出灰边。

## 2026-07-30 · M3B 抽牌堆/弃牌堆计数 HUD 实施（待 Unity 人工验收）

> - 新增 `BattleCardPileHudView`：它仅订阅 `BattleSession.CardZones.Layout` 与 `LocalizationService.LocaleChanged`，从已发布布局的 `DrawPile.Count`、`DiscardPile.Count`、`ExhaustPile.Count` 即时派生三个底部计数文本；没有新增计数、卡区列表或卡牌归属的镜像状态。场景 `BattleCardPileHud` 已置于主 Canvas 底部左右两侧，并由 `BattleLifetimeScope` 注入。
> - 新增 `battle.card_pile.draw.name` / `battle.card_pile.discard.name` / `battle.card_pile.exhaust.name` 三个 Excel i18n key（en：`Draw Pile` / `Discard Pile` / `Exhaust Pile`；zh-CN：`抽牌堆` / `弃牌堆` / `消耗牌堆`）。本地化校验器把它们纳入必需运行时 key，防止表格遗漏后静默运行。相应 String Table 已与 Excel 编辑源同步。
> - `DataTables/gen.bat` 已成功执行；两套程序集串行静态编译均为 0 error（保留 6/12 条既有版本冲突 warning）；工作表、场景 GUID 与 `git diff --check` 均已检查。新增 `BattleCardPileHudPresentationTests` 已编译，Unity EditMode 与实际场景验收尚未执行。
> - 未实施 M3C～M3E：它们分别依赖 M4 回合/能量、M5 意图及 M7～M9 的效果与结算事实，不能先以 UI 占位状态替代。需在当前 Unity Editor 执行 `TinySpire/Build/Sync and Build All` 后从 Bootstrap 人工验收 M3B，详见 `06_testing/2026-07-30-battlescene-card-pile-hud.md`。

## 2026-07-30 · M3A-1/2 参与者配置与 Prefab 工厂实施（待 Unity 验收）

> - `battle.Hero`、`battle.Enemy` 已新增 `name_i18n_key` 与 `view_prefab_address`；Test Warrior 与 Test Slime 分别指向现有玩家、敌人 Prefab，名称写入 `i18n.xlsx`。Luban 已生成对应 C# 与 `Assets/GameData` JSON。
> - 本地化导入/校验现在覆盖 Hero、Enemy 名称；Addressables 配置工具会把两个角色 Prefab 放入 `TinySpire Characters` 本地组，地址仍是表中的完整 `Assets/...` 路径。
> - 已实现 `BattleParticipantPresenter` 与 `EnemyCombatantLayout`：一名玩家、1–3 名敌人按 Encounter 顺序自右向左等距实例化；场景销毁时以 `ReleaseInstance` 释放。`BattleSession` 显式保留遇敌顺序，未依赖字典遍历顺序。
> - 本轮实跑曾暴露 VContainer 选择参数最多的非公开 `BattleSession` 构造函数，导致尝试解析本不应注册的 `BattleCombatantsData`。`BattleLifetimeScope` 已改为显式工厂，仅解析 `ConfigService` 与 `BattleSetupOptions` 后调用正确的公共构造函数。
> - 定向用例覆盖遇敌顺序、两/三敌布局、容量与间距错误；既有程序集 `dotnet build` 为 0 error（6 条既有程序集引用冲突警告）。运行中的 Unity 尚未将新文件刷新进生成的 `.csproj`，且当前存在用户的 `BattleScene.unity` 改动，因此未启动第二个 Editor、未修改场景、未运行 Unity EditMode 或 Addressables 构建。待在现有 Editor 执行 `TinySpire/Build/Sync and Build All` 后完成 M3A-1 内容验收；场景挂载与实跑属于尚未开始的 M3A-4。

## 2026-07-30 · M3A-3/4 HUD 与 BattleScene 接线实施（待 Unity 人工验收）

> - 新增 `ParticipantHudView` Prefab/组件：它只保存参与者事实、世界 Sprite、Canvas 与本地化服务的引用，不复制生命、力量或语言状态；Health/Strength/Locale 变化分别驱动展示重派生。名称投影在角色头顶，生命条与 `当前 / 上限` 投影在脚下，力量为零时隐藏。
> - `BattleParticipantPresenter` 现在同时创建并销毁角色 Addressables 实例和对应 HUD；HUD 构建失败会立即释放已生成角色，Presenter 销毁时也会显式释放两类 View。场景中的 `BattleLifetimeScope` 已挂载 Presenter，并接入既有 Player/Enemy Anchor、主 Canvas 与 HUD Prefab；Scope 注册该场景组件以完成 VContainer 注入。
> - 本轮未改动战斗表、翻译正文或角色 Addressables 配置。因现有 Unity Editor 正在被用户使用，尚未启动第二个 Editor 或重建 Addressables 本地内容；需在该 Editor 执行 `TinySpire/Build/Sync and Build All` 并从 Bootstrap 实跑 BattleScene，确认 HUD 和 Console 后再完成验收。

## 2026-07-30 · M3A HUD 前景渲染与素材名修正（待重建本地内容）

> - 人工实跑确认 HUD 的世界投影位置正确，但现有 Screen Space - Camera Canvas 的 `Plane Distance = 100` 位于世界背景之后。BattleScene 已将其改为 `1`，使该 Canvas 位于相机近端、背景之前；未改变角色或背景的 Sorting Layer。
> - Hero 1001 与 Enemy 2001 的名称语义取自实际 Sprite 的关键词：英文为 `Sisyphus`、`Warden`，中文为 `西西弗斯`、`典狱长`。稳定 i18n key 不变，Excel 编辑源、Unity String Table 与运行时读取链保持一致；之后仍需由现有 Unity Editor 重导入本地化并重建 Addressables。
> - `DataTables/gen.bat` 已成功生成；两套程序集静态构建均为 0 error（分别保留 6/12 条既有版本冲突警告），`git diff --check` 通过。尚未由 Unity 菜单重建 Addressables 本地内容，也尚未进行修正后的人工实跑。

## 2026-07-30 · M3 BattleScene 主 HUD 与参与者视图 grilling 完成

> - 已确认 M3 按运行时事实拆为 M3A-M3E；当前只规划 M3A 的参与者世界视图与生命 HUD。M3B 牌堆计数可复用已完成的 M2 卡区事实，M3C 能量/结束回合等待 M4，M3D 意图等待 M5，M3E 格挡/状态/死亡/覆盖层等待 M7-M9。
> - M3A 的静态模板将新增 `name_i18n_key` 与 `view_prefab_address`。名称进入现有 `i18n.xlsx` 和 Unity Localization；角色 Prefab 作为 Addressables 资源从表中指定的完整 `Assets/...` 地址加载。
> - 已确定 `BattleParticipantPresenter` 负责 BattleScene 内的实例与 HUD 生命周期：按 `CombatantId` 绑定，世界 Sprite 与 UGUI HUD 分层；单玩家、1-3 敌人按 Encounter 顺序自右向左布局。地址/加载/Prefab 合约错误直接抛出，不做占位或回退。
> - M3A 只显示名称、生命和非零力量；生命为零时仅刷新数值，尚不实现死亡、格挡、状态、意图、能量、回合、胜败或 Effect。完整设计见 `plans/2026-07-30-battlescene-participant-views.md`，决策见 CD-023。
> - 本轮仅完成设计与文档沉淀；未修改表格、Addressables、场景或运行时代码，未产生新的测试结果。

## 2026-07-30 · i18n Excel 编辑源接入与一键构建验收

> - 新增 `DataTables/Datas/i18n.xlsx`（`i18n` sheet，`key`、`en`、`zh-CN`、`smart`）作为翻译正文的编辑源；初始内容与既有 Strength、Strike、Defend、Bash 及共享关键词一致。
> - 新增 `I18nExcelReader` 和 `TinySpire/Localization/Import Battle Card Text from Excel`。导入后校验 Excel 覆盖运行时所需 key，并确认 String Table 的正文/Smart 标记与 Excel 一致；运行时仍只通过 Unity Localization 读取。
> - 新增 `TinySpire/Build/Sync and Build All`：依次执行 Luban 生成、Unity 资源刷新、Excel 导入与校验、Addressables 本地构建。已由用户在 Unity Editor 内执行并确认通过；决策见 CD-022。
> - `dotnet build` 为 0 error（12 条既有程序集引用冲突警告）。一键入口已完成 Luban、Excel 导入、本地化校验与 Addressables 本地内容构建，M2A 的 Excel 内容管线验收完成。决策见 CD-021。

## 2026-07-30 · 本地化文本唯一来源收敛

> - 删除 `LocalizationBuildTools` 中硬编码的 `LocalizedEntry[]`、配置/补全菜单及其写表辅助函数。`Battle Cards` Unity Localization 表资源现在是翻译正文的唯一来源。
> - 保留 `TinySpire/Localization/Validate Battle Card Text`，它只校验 locale、key、Smart String、参数和效果引用，不创建或覆盖翻译。
> - 新增/修改本地化内容的流程：直接编辑 String Table → 执行校验 → 重建 Addressables 本地内容。未修改任何翻译资源、Luban 表或运行时效果逻辑；决策见 CD-020。

## 2026-07-30 · 运行时数据命名与 R3 事实绑定修正

> - 运行时类型与文件统一改用 `Data` 尾缀：`CombatantData`、`PlayerCombatantData`、`EnemyCombatantData`、`BattleCombatantsData`、`CardInstanceData`、`CardZoneLayoutData`、`BattleCardZonesData`；`State` 留给未来状态机/状态模式。
> - 删除泛化 `Changed`/`Subject<Unit>`。生命、力量以只读 R3 属性公开；四卡区以不可变的完整 `CardZoneLayoutData` 原子发布。手牌 UI 订阅手牌布局、玩家力量与 Locale 的实际值，卡区移动不会向观察者暴露中间状态。
> - 验证：定向 EditMode 18/18、全量 EditMode 25/25 通过；BattleScene 实跑后 `BattleCardZonesData.Layout` 发布的手牌数为 4，`HandCardVisual` 也为 4，Console 无错误；`dotnet build` 为 0 error（12 条程序集引用冲突警告）；Addressables 本地内容已重建。决策见 CD-019；术语见 `CONTEXT.md`。

## 2026-07-30 · R3 通知绑定与 HandCardVisual 展示边界

> - 历史记录：当时曾将 `BattleState`、`CardZoneState` 与 `LocalizationService` 迁移为 `Subject<Unit>` / `Observable<Unit>`。该做法已由 CD-019 替代；`HandCardVisual` 的展示引用归属结论仍有效。
> - `CardView.prefab` 根节点现在序列化配置 `HandCardVisual` 的 Canvas、CardContent、标题、费用、类型和说明引用。容器不再按对象名扫描 `Text`，而是在 `HandCardVisual.Bind` 中写入展示值；语言和战斗事实变化仍只触发即时重派生，不保存文本镜像。
> - 验证：BattleState/CardZoneState EditMode 9/9、全量 EditMode 25/25 通过；`dotnet build TinySpire/TinySpire.sln --no-restore --verbosity:minimal` 为 0 error（12 条既有程序集引用冲突警告）。运行时触发卡区变动与战斗伤害后，手牌事实数与延迟销毁后的 View 数均为 4，Console 无错误；`TinySpire/Addressables/Build Local Content` 成功完成。
> - 决策见 `CODE_DECISIONS.md` CD-018；M2A 仍不包含 Effect 执行、费用、目标选择、敌人行为或回合流程。

## 2026-07-30 · Addressables 迁移与 M2A 完成

> - 已移除 YooAsset 运行时/包/收集设置，建立本地 Addressables 场景与 GameData 组；启动、配置和场景加载改走 Addressables，完整 `Assets/...` 地址保持稳定。
> - `battle.Card` 已迁移为 name/description i18n key 与有序 `CardEffectBinding`；Luban 生成成功。
> - 已实现 Unity Localization 薄服务、Smart String 资源配置/校验工具、`CardTextFormatter` 与 `CardValueCalculator`；手牌 UI 在语言或战斗事实通知后即时重派生文本，不保存格式化字符串或显示伤害状态。
> - Luban、Localization 配置/校验和 Addressables 本地内容构建均已完成；校验器要求 `en`、`zh-CN`、共享关键词 key 与 `zh-CN → en` fallback，String Database 启用 fallback；`dotnet build` 0 error，Unity EditMode 23/23 通过。
> - Bootstrap → LoadingScene → BattleScene 实跑成功；GameData 正常加载，中文/英文动态卡牌说明正确，切换语言前后 5 个手牌 View 身份不变，Console 0 error、0 warning。
> - 本阶段仍未实现 Effect 执行器、费用、目标选择、伤害/格挡/易伤结算、远程 catalog 或第二套资源包。

---

## 2026-07-30 · 卡牌区域与确定性洗牌实施

> - 新增 `GameRandom`，以实例方式封装项目已存在的 `Unity.Mathematics.Random`；规则随机可读取/恢复 `uint State`，并通过 Fisher–Yates 洗牌，不使用 `UnityEngine.Random` 全局状态。
> - `HandState` 升级为 `CardZoneState`：全部卡牌实例由 `Cards` 字典定义，抽牌堆、手牌、弃牌堆、消耗区分别保存互斥的有序 `CardInstanceId`；不保存 `Zone` 镜像或缓存计数。
> - `BattleSession` 现在创建完整 10 张初始卡组，按战斗种子洗牌并抽取 `GameConfig.InitialHandCount` 张；`DEP-006` 已解决。当前种子仍来自 BattleScene Inspector，未来改由 Run 生命周期提供，登记为 `DEP-007`。
> - 手牌拖过出牌线的既有占位行为现在把指定实例移动到弃牌堆；未实现效果器、目标合法性、费用、回合调度、地图/奖励/敌人随机。
> - TDD 定向 EditMode 10/10、完整 EditMode 13/13 通过；dotnet build 0 error；Bootstrap 实跑为 10 个实例、抽牌堆 5、手牌 5、弃牌/消耗 0，Console 0 error，保留既有 LoadingScene handle warning。
> - 双轴代码审查最终通过；审查修正了随机流外部别名、视图冗余模板 ID 与旧文档过期状态，复核无新增 P1/P2。
> - 本轮未修改表格、生成 JSON 或 YooAsset 包，因此不运行 Luban/AB 重建。

---

## 2026-07-30 · 卡牌 i18n key 与动态说明设计补充

> - 路线图新增 M2A：卡牌名称/说明使用 i18n key，说明模板使用 `{damage}`、`{block}`、`{vulnerable}` 等命名参数，并允许不同语言调整语序。
> - 规划 `CardTextFormatter` 深模块：UI 只提交卡牌实例和可选来源参与者，模块内部解析 key、效果参数、关键词和动态数值；格式化文本不进入 `CardInstanceState`。
> - 说明显示值、目标预览值和实际结算值必须复用同一纯数值计算模块，只是上下文不同；三者均由配置和运行时事实派生，不成为第二份状态。
> - 当前未安装 Unity Localization，文本目录后端与 fallback locale 保持为实施前 Open Question。本轮只修改设计文档，未改表格、代码、生成数据、AB 包或效果器。
> - 详细设计见 `plans/2026-07-30-card-localized-text-design.md`。

---

## 2026-07-30 · 战斗配置接入运行时 + BattleScene MVP 路线图

> - 新增 `BattleSession`，由 `BattleLifetimeScope` 从英雄 1001、遭遇 5001、初始卡组和 `GameConfig.InitialHandCount` 创建玩家、敌人与手牌；`CombatantState` 接入模板基础力量。
> - `HandState` 改用唯一 `CardInstanceId` + `TemplateId`，解决初始卡组内重复 Strike 无法独立表示的问题。手牌 UI 读取同一运行时状态，并从 `battle.Card` 显示卡名和费用。
> - 未实现效果器、目标、费用扣除、牌堆、回合流程或敌人行为。正式牌堆前暂取卡组前 5 张，登记为 `DEP-006`。
> - 重写 `ROADMAP.md`：按 M0～M10 规划牌堆、主 HUD、回合、敌人意图/随机行为、出牌命令、效果器、完整循环和反馈；并以 G1～G8 承接主菜单、Run、存档、地图、奖励、遗物/药水、商店/事件和完整产品收尾。每阶段明确唯一事实、派生数据与验收标准。
> - 验证：EditMode 6/6 通过；dotnet build 0 error；Bootstrap → BattleScene 实跑生成 5 张独立 Strike，标题/费用绑定正确。本次无 error，保留一条既有 LoadingScene handle warning。

---

## 2026-07-30 · STS 战士初始卡组配置

> - `battle.Deck` 1001 设为 5×Strike、4×Defend、1×Bash；初始手牌 `game-config.json` 已是 5，保持不变。
> - `battle.Card` 由单个 `effect_id` 改为 `effect_ids` 列表，使 Bash 可表达“8 伤害 + 2 易伤”；新增敌方目标、伤害、格挡、易伤和空属性枚举项，仅作为静态配置。
> - Luban 生成成功，YooAsset `Main` 内置包已重建；Bootstrap 场景实跑控制台 0 error。运行时效果结算不在本轮范围内。

---

## 2026-07-30 · 战斗表 YooAsset 生成路径修正

> - Luban JSON 输出从 `TinySpire/Assets/StreamingAssets/GameData` 改为 `TinySpire/Assets/GameData`，与 `ConfigService` 的资源路径加载约定对齐。
> - 生成后重建 YooAsset `Main` 内置包；仅刷新 Unity 不会把新 JSON 写入离线清单。Bootstrap 场景实跑确认 `battle_tbhero` 加载不再报错。

---

# Daedalus · 会话日志

> 记录每次编程会话的关键产出、决策和待办。

---
## 2026-07-30 · 战斗静态配置表实施

- 在 `DataTables/Datas/__tables__.xlsx` 登记 6 张手工 schema 战斗表，并在 `__enums__.xlsx` 定义 `TargetRule.Self`、`EffectType.ModifyAttribute`、`Attribute.Strength`。
- 新增 `battle.hero.xlsx`、`battle.enemy.xlsx`、`battle.deck.xlsx`、`battle.card.xlsx`、`battle.card_effect.xlsx`、`battle.encounter.xlsx`；填入一套闭合最小样例：Test Warrior（30 HP）→ deck 1001 → Strength 卡牌（Self，+3 Strength），Test Slime（20 HP）和单敌人 encounter 5001。
- 模板表只保存稳定 ID 与设计数值；`CombatantId`、当前生命、存活、手牌/抽牌/弃牌堆、卡牌实例、临时费用、升级、敌人意图和控制者不进入配置。表间关系暂以 ID 表达，未实现 `ref` 校验或运行时导航。
- Luban 生成 `cfg.battle` 的 6 个记录类型、6 个表管理器、3 个枚举及 6 个 JSON；`#demo.item.xlsx` 按既有删除意图保持缺失，旧 demo 生成产物已随重新生成移除。战斗数据文件故意不使用 `#` 前缀，避免自动导入与手工 schema 重复。
- 验证：UnityMCP 资源刷新无编译错误；`dotnet build TinySpire/TinySpire.sln --no-restore --verbosity:minimal` 为 0 错误（13 条既有程序集版本冲突警告）。详情见 `plans/2026-07-30-battle-static-config-tables.md`、`06_testing/2026-07-30-battle-static-config-tables.md` 与 CD-011。

---
## 2026-07-30 · BattleState 运行时参与者模型实施

- 新增纯 C# `TinySpire.Battle` 运行时模型：`CombatantId`、共同基类 `CombatantState`、`PlayerCombatantState`、`EnemyCombatantState` 与聚合根 `BattleState`。
- `BattleState` 是唯一持有 `CombatantId → CombatantState` 映射的事实源，并以只读字典 `Combatants` 暴露；按用户反馈删除了预置的玩家/敌人/存活派生视图和与 `TryGetCombatant` 重复的 `ResolveSelf`，未来只在真实目标规则出现时从字典值按需派生。未并存 `List` 作为索引或镜像；本次将原始 `List` 正式替换为该字典，`TryGetCombatant` 直接委托 `TryGetValue`。
- 初版共同可变事实仅为生命；`ApplyDamage` 修改目标参与者自身的当前生命，`IsAlive` 由该生命值派生，当前不预置存活视图。未接入 `HandState`、卡牌实例、Effect、敌人意图、能量、UI、场景锚点或 `BattleLifetimeScope`。
- 新增 EditMode `BattleStateTests`；字典调整后 UnityMCP 重新运行 3 项测试全部通过。`dotnet build TinySpire/TinySpire.sln --no-restore --verbosity:minimal` 为 0 错误（13 条既有程序集版本冲突警告）。测试结束后 Console 出现 YooAsset `AssetBundleCollectorWindow.RefreshWindow` 的既有编辑器包空引用（Undo 回调，未触及本次代码），已在验证记录注明。
- 决策见 `CODE_DECISIONS.md` CD-010；计划与验证记录分别见 `plans/2026-07-30-battle-runtime-state.md`、`06_testing/2026-07-30-battle-runtime-state.md`。

---
## 2026-07-30 · BattleScene 拖拽出牌（最小判定）验收完成

- 用户已在 Game View 完成并确认鼠标手势验收：拖拽保持抓取偏移且持续跟随；越过 `playLineY` 松手后卡牌销毁、其余手牌补位并显示透明度反馈；线内松手会回弹并恢复反馈。
- `拖拽打出最小判定 + 手牌数据归属权收回`（ROADMAP Phase 1）由“已实施，待人工手势验收”更新为“已完成并验证”。
- 已更新 `ROADMAP.md` 与 `06_testing/2026-07-30-battlescene-drag-to-play-minimal.md`；未修改代码、预制体、场景或 DEP-001～DEP-004 的未解决状态。

---
## 2026-07-30 · BattleScene LifetimeScope 实施

- 新增 `TinySpire/Assets/Scripts/Battle/BattleLifetimeScope.cs`：定义 sealed 的 VContainer 场景 Scope，`Configure` 保持为空，仅保留 `TODO(DEP-005)` 占位标记。
- 通过 Unity MCP/编辑器在 `BattleScene.unity` 根级创建并保存 `BattleLifetimeScope` GameObject，`parentReference` 设置为 `Bootstrap`。
- 未修改 `SceneFlowService.cs`、`Bootstrap.cs`，未实现回合调度器、抽牌堆、弃牌堆或相关抽象。
- 验证：`dotnet build TinySpire/TinySpire.sln --no-restore` 为 0 错误；Unity Play Mode 为 0 错误、0 警告，未出现 parent reference 警告。
- 验证记录：`06_testing/2026-07-30-battle-lifetime-scope.md`。

---
## 2026-07-30 · 战斗场景 LifetimeScope 结构 grilling（纯讨论，无代码改动）

- 用户提出未来战斗场景需要独立的回合调度循环、抽牌堆、弃牌堆，讨论是否需要专属 `LifetimeScope`、是否绑定场景生命周期。结论：需要，且应作为场景内挂载的 GameObject（不由代码动态创建/销毁），因为 YooAsset `LoadSceneMode.Single` 切场景时会自动销毁旧场景 GameObject，VContainer 的 `parentReference` 按类型全局查找父 Scope（`LifetimeScope.FindParent`），与 `SceneFlowService` 完全解耦，不需要改动 `SceneFlowService.cs`。
- 进一步讨论"未来加入地图"后的结构（Game → 存档 → 三张地图 → 具体事件），结论：不需要给每一层单开 DI Scope，只需要 3 层——Bootstrap（Root）→ RunScope（存档，跨场景持久，需要新的 `RunFlowService` 手动创建/销毁）→ 事件层场景 Scope（战斗/地图/商店，沿用场景挂载方案）；"地图"本身只是 `RunState` 里的字段，不需要单独 Scope。
- 确认了 `06_testing/2026-07-30-scene-child-scope.md` 描述的"`SceneFlowService.CreateChild` 动态创建子 Scope"方案已被用户撤回、代码已还原，该文件头部的 `source: CD-008` 是错误引用（当时 CD-008 从未真正存在）；已将该文件归档至 `99_archive/2026-07-30-scene-child-scope.md` 并更新其前言说明。
- 新增 `CD-008`（场景级服务用挂载在场景内的 `LifetimeScope`，不由代码动态创建/销毁）与 `CD-009`（存档层 `RunScope` 需要显式 `RunFlowService` 管理生命周期，前瞻记录、未实现）；`ARCHITECTURE_CONVENTIONS.md` 新增 Locked 的 `AC-006` 并在 Open 部分登记 `RunScope` 仍是前瞻性质。
- 按 CD-008/AC-006 产出 Codex 实施 Prompt（直接在对话中给出，未另存为文件）：仅创建 `BattleLifetimeScope`（挂在 `BattleScene.unity`，`parentReference` 指向 `Bootstrap`），`Configure` 暂空并标记 `DEP-005`；明确排除回合调度器/抽牌堆/弃牌堆的实际实现，不改动 `SceneFlowService.cs`。新增依赖项 `DEP-005` 登记到 `DEPENDENCIES.md`。
- 本轮未写任何 C# 代码，未创建 `RunFlowService`/`RunLifetimeScope`/`RunState`，仅做文档维护。

---

## 2026-07-30 · 最小状态机 Core 实施

- 新增纯 C# `TinySpire.Core.StateMachine`：状态包含 `Enter`、`Tick(TimeSpan)`、`Handle(event)`、`Exit`，状态通过返回值请求切换。
- 状态机不持有事件队列、不依赖 Unity/UniTask、不查找游戏运行时数据；Update/Tick 驱动和事件排队由外部负责。
- 支持状态跨多帧保持、同步事件分发、同一次 `Tick` 中后续状态使用零时间继续 Tick，以及不可重启的 `Stop()`。
- 本轮明确不实现 Context、嵌套状态、并行状态、异步调度和任何游戏领域接入，避免在缺少真实用例时扩展 Core。
- 验证记录：`06_testing/2026-07-30-state-machine-core.md`。

---

## 2026-07-30 · BattleScene 拖拽出牌（最小判定）实施

- 在 `TinySpire/Assets/Scripts/UI/Battle/Hand/HandState.cs` 新增纯 C# `HandState`：以占位 ID 初始化手牌列表，只暴露只读快照、`PlayCard(int)` 和 `event Action` 变化通知；不接入 R3、真实卡牌数据、费用或 BattleState。
- `HandCardContainer` 现在只以 Inspector `initialHandCount` 初始化 `HandState`；运行期张数从 `HandState.CardIds.Count` 得出。它订阅变化后销毁已打出卡的视觉对象并按状态快照重排其余卡牌。松手时以可调 `playLineY`（默认 240）判定；越线调用 `HandState.PlayCard`，未越线仍回弹。
- 拖拽坐标使用每帧 `PointerEventData.delta / Canvas.scaleFactor` 累加到当前锚点，不再把屏幕点换算到独立根 Canvas 的零尺寸 RectTransform；因此按下不跳中心，后续移动保持抓取偏移并持续跟随鼠标。
- `HandCardVisual` 使用 `CardContent` 上运行时添加的 `CanvasGroup` 做越线透明度反馈，并独立维护、终止其反馈 Tween；未修改 `CardView.prefab`。
- 按依赖台账添加 `TODO(DEP-001)` 至 `TODO(DEP-004)`：目标 ID 填充、费用、反馈样式、销毁前动作。没有实现目标、费用、效果、抽牌或弃牌逻辑。
- 验证：纯 `HandState` 检查通过；`dotnet build TinySpire/TinySpire.sln --no-restore` 通过（0 错误；9 条既有程序集版本冲突警告）；UnityMCP Play Mode Console 为 0 错误、0 警告。MCP 无指针事件注入，最终鼠标拖拽手势需人工确认。
- 验证记录：`06_testing/2026-07-30-battlescene-drag-to-play-minimal.md`。

### 后续动作

- 已完成：用户在当前 Game View 中人工确认移动不跳中心、越线销毁补位、线内回弹和透明度反馈。

---

## 2026-07-29 · 拖拽出牌（最小判定）grilling + 计划产出

- 确认杀戮尖塔式手牌 UI 已由 Codex 实施完成（见上一条会话日志与 `06_testing/2026-07-29-battlescene-hand-ui-sts-style.md`），但拖拽当前不能判定出牌。
- 用 `grilling` 技能逐项确认了最小可行的出牌判定（可调 Y 轴出牌线），并在过程中发现 `handCount` 应该从 UI 组件里收回归属权，因此新增了最小 `HandState` 纯 C# 聚合类的设计。
- 确认本轮不做目标选择、不做费用检查、打出后立即 `Destroy`（无过渡动画），拖过出牌线只加最简占位视觉反馈。
- 按用户建议引入了一套“依赖项 ID”机制（DEP-001~DEP-004），写进计划文档，并要求未来实现时在代码里用 `TODO(DEP-xxx)` 标记对应位置。
- 产出实现计划：`plans/2026-07-29-battlescene-drag-to-play-minimal.md`（proposal，未实施代码）。
- 新增代码决策 CD-005（HandState 收回数据归属权）与 CD-006（拖拽出牌判定机制）。
- 本轮未写任何 C# 代码，未 commit。配套 Codex Prompt 直接在对话中给出，未另存为文件。

### 下次会话

- 若 Codex 产出代码，需核对：HandState 是否真正持有数据且 UI 无自行自减、四个 TODO(DEP-xxx) 是否都写到了代码里。
- 验收通过后补充一条 `06_testing/` 验证记录。

---

## 2026-07-29 · BattleScene 手牌 UI 实施

- 在 `TinySpire/Assets/Scripts/UI/Battle/Hand/` 新增纯扇形布局计算、手牌容器、单卡视觉动画与 UGUI 事件转发脚本；手牌数量保持为 Inspector 的临时 `int` 占位字段，并在字段处标明未来仅替换为 Luban 数据来源。
- 通过 UnityMCP 创建 `TinySpire/Assets/Prefabs/UI/Battle/Hand/BattleHandUI.prefab`，并在 `BattleScene.unity` 引用它。预制体保存手牌容器和 Inspector 配置；运行时仅复用（未修改）`CardView.prefab` 创建数量可变的独立根 Canvas 卡牌及其交互组件。实现扇形/溢出间距压缩、悬停抬起、独立 Canvas 层级提升、拖拽跟手、拖起后的补位与松手回弹，不含任何出牌判定或数据接入。
- 每张卡的 `HandCardVisual` 独立保存 Tween，并在新动画前终止旧 Tween；悬停采用 `Ease.OutBack` / 0.15 秒，补位与回弹采用 `Ease.OutCubic` / 0.22 秒。
- 静态验证：`dotnet build TinySpire/TinySpire.sln --no-restore` 通过（0 错误；9 条既有第三方程序集版本冲突警告）。场景文本检查未发现 `Toggle` 或 `ToggleGroup`。
- UnityMCP 已连到 `TinySpire@8edf130c865b3957`；Game View 验证了 3 / 5 / 10 张扇形布局与 10 张间距压缩，最后一次 Play Mode Console 为 0 错误、0 警告。未重启、未结束任何用户 Unity 进程。
- 修正扇形旋转方向：布局旋转改为 `-t × maxFanAngle`，使左右卡牌的轴线朝手牌下方汇聚；纯布局测试先复现左 `-15°` / 右 `15°` 的错误方向，再验证为左 `15°` / 右 `-15°`。UnityMCP 干净重启 Play Mode 后，Game View 视觉确认扇轴朝下，Console 为 0 错误、0 警告。
- 验证记录：`06_testing/2026-07-29-battlescene-hand-ui-sts-style.md`。

### 后续动作

- 需在 Game View 手动确认 hover 与拖拽交互手感；本次 UnityMCP 无指针事件注入能力，未伪造该两项结果。

---

## 2026-07-29 · 手牌 UI 杀戮尖塔化 grilling + 计划产出

- 用 `grilling` 技能逐项确认了手牌 UI 从 CD-002 的静态 Toggle 单选，升级为杀戮尖塔式悬停抬起 + 扇形排布 + 拖拽跟手视觉（本轮不做出牌判定）。
- 确认用户已将 DOTween/DOTweenPro 导入 `TinySpire/Assets/Plugins/Demigiant/`；确定悬停/重排补位的时长与缓动曲线参数。
- 手牌数量来源经用户确认后改为：本轮不引入接口抽象，直接用 Inspector 可调 `int` 字段，注释标记为未来 Luban 数据驱动的临时占位。
- 产出实现计划：`plans/2026-07-29-battlescene-hand-ui-sts-style.md`（proposal，未实施代码）。
- 新增代码决策 CD-003（DOTween 引入）与 CD-004（交互模型替换 CD-002），未删除旧记录。
- 本轮未写任何 C# 代码，未改动 `BattleScene.unity`，未 commit。用户计划将计划 + 配套 Prompt 交给外部 Codex 实施。

### 下次会话

- 若 Codex 产出代码，需核对实现是否符合本计划的 10 条决策，尤其是“不做出牌判定”的边界是否被越界。
- 验收通过后补充一条 `06_testing/` 验证记录。

---

## 2026-07-12 · BattleScene 基础手牌 UI

- 在 `TinySpire/Assets/Scenes/BattleScene.unity` 的现有 Canvas 下新增 `BattleCardUI`：包含底部手牌托盘、5 个 `CardView` 实例和单选高亮。
- 卡牌选择使用 UGUI `Toggle` + `ToggleGroup`；本轮只构造表现与可点击状态，没有新增运行时代码，也未接入卡牌数据、ViewModel 或出牌逻辑。
- 将现有 Screen Space - Camera Canvas 的 `planeDistance` 从 100 调整为 1，避免 UI 平面落在背景 Sprite 后方而被完全遮挡。
- Unity Game View 目视验证通过；EventSystem 点击第二张卡后，第一张取消选中、第二张进入选中状态；Console 0 错误、0 警告。
- 实现计划：`Docs/Copilot_Daedalus/plans/2026-07-12-battlescene-card-ui.md`；验证记录：`Docs/Copilot_Daedalus/06_testing/2026-07-12-battlescene-card-ui.md`。

---

## 2026-07-12 · LoadingScene 最短展示时间

- `SceneFlowService` 在 LoadingScene 完成切入后开始计时，保证目标场景切换前至少展示 1 秒。
- 内容准备耗时计入这 1 秒；仅补足剩余时间，不给慢加载额外增加固定等待。
- 补足延迟不受 `Time.timeScale` 影响。
- `dotnet build TinySpire.sln --no-restore` 通过（0 错误、3 个既有程序集版本冲突警告）；Unity Editor 当前存在运行实例，未启动额外实例进行 Play Mode 验证。
- 验证记录：`Docs/Copilot_Daedalus/06_testing/2026-07-12-loading-scene-minimum-duration.md`。

---

## 2026-07-06 · 初始化

- 创建 `Copilot_Daedalus/` 工作区，确立与 Pegasus 的协作约定
- 项目处于 planning 阶段，尚未开始编码

### 当前状态

- Unity 项目路径：`../TinySpire/`（相对于 `Docs/`）
- 现有代码：仅 `Assets/Scripts/Launcher.cs`
- BattleScene MVP 待实现（见 `Hermes_Pegasus/STATUS.md` P0 列表）

### 下次会话

- 阅读最新 `AGENT_HANDOFF.md` + `STATUS.md`
- 根据 P0 优先级制定 BattleScene 实现计划

---

## 2026-07-08 · 协作体系与文档库初始化

### 设计讨论（proposal，未落事实源）

- 起点讨论：从纯 C# 内核倒着往外长（计算 → 状态 → 时序 → UI），先不铺框架
- character 数据：确认 `模板 / 运行时` 两层；运行时持模板引用 + 只存会变字段
- `maxHp / currentHp` 同类两字段，约束 `current ≤ max`；max 变化时 current 是否同步 = **Open Question**
- 数据管线选型：**Luban + JSON 输出**（承重基础设施，提前定合理）；Theseus 去接入
- Open Question：max 变化时 current 同步规则；游戏 asmdef 布局（暂定"一个游戏 asmdef + 一个 Test asmdef"）

### 协作体系（对齐 AI_COLLABORATION_RULES.md）

- 四角色确认：Theseus（拍板）/ Pegasus（设计·数值）/ Calliope（创意·文本，Gemini）/ Daedalus（实现）
- Gemini 正式名从讨论中的 Urania 定为 **Calliope / 卡利俄佩**

### 文档库产出

- 新建 `AGENT_PROMPT.md` — 调用 Daedalus 的 Prompt 模板（6 节）
- 拆分身份/导航：新建 `AGENT_PROFILE.md`（身份），`README.md` 重写为 llm-workflow `index` 路由页
- 新建 `AGENTS.md` — 文档库入口 + llm-workflow 角色本地化映射
- 按 llm-workflow bootstrap 初始化本库：index-first ✅、status source = 本文件 ✅
- **完整实例布局初始化**（每个 AI 各维护一份 llm-workflow）：新建 8 个角色目录
  `00_inbox` `01_requirements` `04_research` `06_testing` `07_retrospective` `08_tools` `10_communication` `99_archive`，各带 keeper README；
  已有文件就地充当角色：`README`=index、`SESSION_LOG`=dev-log、`plans/`=design、`CODE_DECISIONS`=decision（事实源不移动）；`09_meetings` 不适用未建

### 记录的文档冲突（待 Theseus 裁决，未覆盖）

1. `.github/instructions/TinySpire.instructions.md` 仍是两人叙事（Pegasus+Daedalus），与四人体系不一致
2. 主库 `dev` 分支与 `Pegasus_Docs` worktree 存在同名文件双份，本次改动落在**主库 dev**

### 下次会话

- 待 Theseus 确认上述 proposal / Open Question 后，制定 BattleScene 首个实现计划
- Luban 接入完成后，落地 `CharacterTemplate` 表 → 生成 C# 类的目录/程序集归属
