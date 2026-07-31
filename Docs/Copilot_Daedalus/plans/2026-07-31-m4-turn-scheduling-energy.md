---
title: M4 回合调度与每玩家能量
page_type: plan
lifecycle: active
date: 2026-07-31
scope: TinySpire 战斗时序层、状态层与 BattleScene M3C 接线
source: 用户确认“多人根基、当前单玩家接线”
status_source: ../SESSION_LOG.md
---

# M4 回合调度与每玩家能量

## 当前结论

M4 先建立整个战斗的调度根，再让能量、出牌、抽弃牌、敌人行动和后续效果系统通过同一个命令入口接入。根模型遵守已锁定的多人玩法：所有玩家共享 `PlayerAction` 阶段，每名玩家拥有自己的行动状态与能量；玩家可以交错出牌，只有全部玩家结束行动后才进入敌人阶段。

本轮只把当前 BattleScene 的唯一玩家接到这套多人模型上，不实现联网、房间、输入仲裁或多玩家 UI。当前单玩家限制必须显式登记为 `DEP-008`，不能把全局唯一玩家或全局能量写进调度根的接口。

## 目标

- 建立战斗时序层的唯一根模块 `BattleTurnController`。
- 以明确阶段驱动战斗，不让 UI、卡区或未来效果系统自行推进回合。
- 能量按 `CombatantId` 归属；当前固定每轮重置为 3，但 3 是静态规则，不是运行时模板字段或 UI 常量。
- 把“出牌”和“结束玩家行动”收口为命令；命令失败不改变任何权威事实。
- 保留明确的敌人行动交接点，让 M5 接入意图与行为时不重写玩家阶段。
- 用纯 C# EditMode 测试覆盖调度顺序，再单独接入 DI、场景和 UI。

## 明确排除

- 不实现联网同步、服务器权威、回滚、超时或输入仲裁。
- 不实现第二名真实玩家的配置装配、独立牌组或多玩家 UI；见 `DEP-008`。
- 不实现目标选择、Effect 执行、伤害、格挡、状态或卡牌播放动画。
- 不实现敌人意图和行为选择；M4 只提供按 Encounter 稳定顺序逐个交接的阶段，见 `DEP-009`。
- 不实现胜利、失败和奖励结算；M4 只保证调度器以后可以在阶段切换前插入终止判断。
- 不借 M4 重构参与者 Presenter、卡牌展示、Addressables 或程序集结构。

## 深模块与 seam

### 外部 seam

`BattleTurnController` 是调用方和测试共同使用的 seam。调用方只需知道以下命令和只读事实：

```text
StartBattle()
TryPlayCard(actorId, cardId) -> BattleCommandResult
TryEndPlayerAction(actorId) -> BattleCommandResult
TryCompleteEnemyAction(enemyId) -> BattleCommandResult
Turn -> ReadOnlyReactiveProperty<BattleTurnData>
```

接口约束：

- `StartBattle` 只能成功一次。
- 所有命令同步完成校验；失败时阶段、轮次、能量和卡区均不得变化。
- UI 不传入费用，不直接扣能量；控制器从卡牌模板读取费用。
- UI 不设置阶段、轮次、能量或结束标记。
- `TryCompleteEnemyAction` 只允许完成当前明确的敌人，不能跳过 Encounter 顺序。
- 内部状态节点、转换事件和 `StateMachine<TEvent>` 组合方式不属于外部接口，也不是测试 seam。

控制器通过构造参数接收 `CombatantId -> BattleCardZonesData` 映射。生产接线当前只提供唯一玩家的一项映射；纯 C# 测试可提供两名玩家与两套独立卡区，验证多人调度根本身不依赖当前 `BattleSession` 的单玩家装配限制。

### 权威快照

`BattleTurnData` 是一次完整发布的不可变只读快照，至少包含：

- `Phase`
- `RoundNumber`
- `Players`：`CombatantId -> PlayerTurnData`
- `CurrentActingEnemyId`：仅在 `EnemyAction` 阶段有值

`PlayerTurnData` 至少包含当前能量与是否已经结束本轮行动。它不是静态英雄模板，也不保存手牌、生命或控制器身份。`CanPlayCard`、`CanEndAction`、是否显示输入和能量颜色均由上述事实、卡牌费用与卡区归属派生，不另存镜像布尔值。

### 写入权

| 事实 | 唯一所有者 | 允许发起写入者 |
|---|---|---|
| 阶段、轮次、当前行动敌人 | `BattleTurnController` | 仅控制器内部状态转换 |
| 每玩家能量、结束行动标记 | `BattleTurnController` | 仅控制器命令 |
| 生命、力量 | `BattleCombatantsData` / `CombatantData` | 后续 Effect 模块经战斗命令链调用 |
| 卡牌实例与四区归属 | `BattleCardZonesData` | M4 后生产代码只由控制器协调调用 |
| 卡牌费用 | `battle.Card` 静态模板 | 只读；运行时不复制基础费用 |
| 每轮基础能量、目标手牌数 | `GameConfig` 静态规则 | 战斗创建时读取，UI 不持有常量 |

## 多人阶段模型

```text
NotStarted
  -> BattleStart
  -> PlayerRoundStart
  -> PlayerAction
       |- 玩家可交错提交出牌命令
       `- 每名玩家独立提交结束行动
  -> PlayerRoundEnd        （全部玩家均结束）
  -> EnemyRoundStart
  -> EnemyAction           （按 Encounter 顺序逐个敌人）
  -> EnemyRoundEnd
  -> RoundEnd
  -> PlayerRoundStart
```

术语约束：

- 一轮（Round）包含一次全体玩家阶段和一次全体敌人阶段。
- 玩家之间没有 `CurrentPlayer`，也没有固定 A→B 的个人回合。
- `PlayerAction` 是共享窗口；某玩家结束后只锁定该玩家，其他未结束玩家仍可行动。
- 当前 BattleScene 只有一名玩家，因此该玩家结束后会立即满足“全体玩家已结束”。

## 规则顺序

### 战斗开始

1. `BattleSession` 只创建参与者、卡牌实例和洗牌后的抽牌堆，不再预先抽手牌。
2. `StartBattle` 进入 `BattleStart`，只执行一次战斗级初始化。
3. 首次进入 `PlayerRoundStart`：轮次设为 1，每名玩家能量重置为 3，并抽到目标手牌数。
4. 进入 `PlayerAction` 后才开放玩家命令。

### 出牌命令

按以下顺序统一校验：

1. 当前阶段必须是 `PlayerAction`。
2. `actorId` 必须是本局存活玩家，且尚未结束行动。
3. 当前接线必须能解析该玩家的卡区；非当前单玩家接线返回明确失败，见 `DEP-008`。
4. `cardId` 必须在该玩家手牌中。
5. 从静态模板读取费用，当前能量必须足够。

全部验证成功后才扣能量并把卡移出手牌。M4 暂时沿用“进入弃牌堆”作为已提交卡牌的结束位置，不执行 Effect；失败不能扣能量、移动卡牌或发布新快照。

### 结束玩家行动

1. 只接受 `PlayerAction` 阶段内尚未结束的存活玩家。
2. 将该玩家剩余手牌移入弃牌堆，并把其结束标记设为真。
3. 仍有其他未结束玩家时停留在 `PlayerAction`。
4. 全部玩家结束后锁定玩家输入并进入 `PlayerRoundEnd`。
5. 重复结束命令返回失败，不能重复弃牌或推进阶段。

### 敌人阶段

1. 敌人顺序只读取 `EnemyCombatantIdsInEncounterOrder`，不依赖字典枚举。
2. 死亡敌人跳过，但不修改原始 Encounter 顺序事实。
3. 每次只公布一个 `CurrentActingEnemyId`，等待 `TryCompleteEnemyAction` 后再进入下一个。
4. M4 当前场景接线在下一帧以“无行为完成”结束该敌人行动；M5 将接管这一交接点并执行已选意图。
5. 全部敌人完成后进入 `RoundEnd`，再开始下一轮。

## 分步实施

### M4A · 调度事实与状态机骨架

范围：新增但尚不接入生产场景的纯 C# 根，不改变当前 BattleScene 行为。

- 新增 `BattleTurnPhase`、`BattleTurnData`、`PlayerTurnData`、`BattleCommandResult` 与 `BattleTurnController`。
- 使用既有 `TinySpire.Core.StateMachine<TEvent>` 组合阶段节点；不扩展 Core 状态机接口。
- `BattleTurnController` 接受依赖，不自行创建 `BattleSession`、配置或随机流。
- 当前 `BattleSession` 仍保留既有初始抽牌，直到 M4C 同一切片完成控制器注册与启动后再迁移，避免中间步骤破坏 BattleScene。
- TDD seam：只通过控制器命令和 `Turn`/卡区公开事实验收。

停止点验收：

- 未开始时不能提交玩家命令。
- `StartBattle` 只初始化一次；重复调用不重复抽牌或重置能量。
- 首次进入 `PlayerAction` 时轮次为 1，当前玩家能量为 3，手牌数量正确。
- 构造两个 `PlayerCombatantData` 与两套卡区时，快照拥有两个独立能量与结束标记，证明根模型没有全局 `CurrentEnergy`。

预计代码范围：

- `TinySpire/Assets/Scripts/Battle/Turn/`
- `TinySpire/Assets/Editor/Tests/BattleTurnControllerTests.cs`
- 对应 `.meta`

### M4B · 能量与统一出牌命令

范围：费用校验和当前占位出牌，不执行 Effect。

- `GameConfig` 新增每轮基础能量，默认与 JSON 均为 3；目标手牌数继续使用 `InitialHandCount`。
- 实现 `TryPlayCard(actorId, cardId)` 的阶段、身份、结束状态、卡区归属和费用校验。
- 成功时扣除对应玩家能量并移动指定实例；失败时所有事实保持不变。
- 本步仍只验证控制器命令，不改变 `HandCardContainer` 的生产接线；UI 写入收口与 `DEP-002` 的最终解决放在 M4D。

停止点验收：

- 3 能量可打出费用 1、2 的牌并正确归零。
- 能量不足、错误阶段、错误玩家、已结束玩家或卡不在手中时均拒绝，且不移动卡、不扣能量。
- 两名玩家使用各自卡区和能量，彼此出牌不推进或扣减对方事实。
- 因 `Assets/GameData/game-config.json` 属于可寻址内容，本步交付前执行一次 `TinySpire/Addressables/Build Local Content` 并确认构建成功。

预计代码范围：

- `TinySpire/Assets/Scripts/Core/GameConfig.cs`
- `TinySpire/Assets/GameData/game-config.json`
- `TinySpire/Assets/Scripts/Battle/Turn/`
- 对应 EditMode 测试

### M4C · 全体玩家结束与敌人顺序交接

范围：完整轮次闭环，不实现敌人行为内容。

- 实现每玩家独立结束行动与全体完成门槛。
- 结束行动时只弃置该玩家剩余手牌。
- 按 Encounter 顺序进入每个存活敌人的 `EnemyAction`。
- 提供严格匹配当前敌人的完成命令；错误或重复完成不能推进。
- 在这一完整切片中把初始抽牌从 `BattleSession` 构造迁移到 `StartBattle -> PlayerRoundStart`，同时在 `BattleLifetimeScope` 注册控制器与启动/逐帧驱动入口，保证当前场景不会出现无手牌的中间状态。
- 当前 BattleScene 的时序适配器在下一帧完成无行为敌人，使阶段可观察且不会在一次同步调用中递归跑完整轮次。
- 下一轮重置所有玩家能量与结束标记，并重新抽到目标手牌数。

停止点验收：

- 两玩家测试中，一人结束后仍停留在 `PlayerAction`，且另一人仍可行动。
- 全部玩家结束后才进入敌人阶段。
- 敌人严格按 Encounter 顺序逐个完成，死亡敌人被跳过。
- 重复结束、错误敌人完成和一帧连续回调不会重复进入阶段。
- 下一轮轮次加一、能量重置、手牌重新抽取。

预计代码范围：

- `TinySpire/Assets/Scripts/Battle/Turn/`
- `TinySpire/Assets/Scripts/Battle/BattleSession.cs`
- `TinySpire/Assets/Scripts/UI/Battle/BattleTurnDriver.cs`
- `TinySpire/Assets/Scripts/Battle/BattleLifetimeScope.cs`
- 对应 EditMode 测试

### M4D · 当前单玩家 M3C 接线

范围：把已经存在的运行时事实显示并接到按钮，不扩展玩法。

- 复用 M4C 已注册的调度根与场景适配器，并补充 M3C View 的场景组件注册。
- `HandCardContainer` 不再直接调用 `DiscardFromHand`，只提交控制器命令，并从命令结果决定回弹。
- 新增能量与结束行动 View：只订阅当前玩家的能量、结束状态和阶段。
- 使用已有能量球、结束回合按钮与玩家回合横幅资源；不生成假意图、状态或结算占位数据。
- 非 `PlayerAction`、玩家已结束或调度器未启动时禁用拖拽和结束按钮。
- 当前按钮文案可沿用“结束回合”，领域命令仍命名为“结束玩家行动”，避免把 UI 文案当作多人模型。
- 完成本步后解决 `DEP-002`；`DEP-001` 目标检测与 `DEP-004` Effect/动画仍保持 open。

停止点验收：

- 从 Bootstrap 进入 BattleScene 后自动进入第 1 轮玩家阶段并显示 3 能量。
- 合法出牌立即刷新能量与手牌；能量不足时卡牌回弹且事实不变。
- 点击结束回合后锁定输入、弃置剩余手牌、依次跨过当前无行为敌人，再进入下一轮并恢复 3 能量。
- 不出现重复开始、重复结束、`InvalidKey`、资源地址或 VContainer 解析错误。
- 场景与 UI 资源接线完成后执行 `TinySpire/Addressables/Build Local Content`，再从 Bootstrap 实跑验收。

预计高影响范围：

- `TinySpire/Assets/Scenes/BattleScene.unity`
- `TinySpire/Assets/Scripts/UI/Battle/`
- `TinySpire/Assets/Scripts/Battle/BattleLifetimeScope.cs`
- 可能新增 UI Prefab 与对应 `.meta`

### M4E · 全量验证、复审与文档收口

- 定向运行 M4 EditMode 测试。
- 运行全量 EditMode 测试与 `dotnet build TinySpire/TinySpire.sln --no-restore --verbosity:minimal`。
- 通过当前 Unity Editor 从 Bootstrap 实跑至少两个完整轮次。
- 因 M4B 修改 `Assets/GameData/game-config.json`、M4D 修改 Addressable 场景与可寻址内容，最终执行 `TinySpire/Addressables/Build Local Content`。
- 做 Standards / Spec 双轴代码审查，修复后复核。
- 更新 `SESSION_LOG.md`、`CODE_DECISIONS.md`、`DEPENDENCIES.md` 与 `06_testing/` 验证记录。
- 按项目规则展示提交审查包，等待用户批准后才提交。

## TDD 测试 seam

用户已经确认的 seam 是 `BattleTurnController` 的公开命令与只读事实。测试不直接构造或断言内部状态节点，不读取私有队列，不以具体类数量或方法调用次数作为成功标准。

每一步按一条行为一个循环推进：

1. 写一个通过公开接口失败的测试。
2. 只实现让该行为通过的最小代码。
3. 运行定向测试。
4. 完成当前停止点后再进入下一步，不一次写完 M4A～M4D。

## 风险与回滚

| 风险 | 控制方式 | 回滚单位 |
|---|---|---|
| 单玩家结构渗入调度根 | 能量和结束状态始终按 `CombatantId` 建模；单玩家映射登记 `DEP-008` | M4A 新增 Turn 目录；M4C 生产映射接线 |
| UI 再次成为规则写入者 | `HandCardContainer`、结束按钮只提交命令 | M4D UI 接线提交 |
| 敌人阶段一次调用递归跑完整轮次 | 每个敌人完成必须由后续帧显式命令触发 | M4C 时序适配器 |
| M4 顺手实现 M5/M7/M9 | 只提供交接点；意图、Effect、胜败保持排除 | 对应分步提交 |
| 场景改动难审查 | M4A～M4C 先纯代码验证，M4D 单独修改场景 | M4D 场景接线提交 |

## 完成定义

M4 完成不等于战斗效果闭环。完成标准是：当前单玩家 BattleScene 已由多人兼容调度根驱动，能量和阶段成为权威事实，出牌与结束行动无法绕过统一命令入口，轮次可经过稳定敌人顺序闭环两次以上，并通过自动测试、Unity 实跑与 Addressables 本地构建。
