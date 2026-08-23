---
title: TinySpire · 实现架构约定（Implementation Architecture Conventions）
owner: Daedalus
page_type: convention
lifecycle: active
created: 2026-07-29
updated: 2026-08-24
status_source: STATUS.md
note: 本文件只锁定代码实现层面的通用约定，不涉及玩法设计决策（玩法决策见 ../Hermes_Pegasus/design/decision-locks.md）。不修改、不复制 architecture.md，只交叉引用。
---

# TinySpire · 实现架构约定

> 这些约定从 BattleScene 手牌 UI + 拖拽打出的实现过程中提炼出来，目的是让**任何未来的实现者**（Daedalus 自己、或交给 Codex 等外部 Agent）默认遵守同一套代码架构习惯，而不是每个功能都重新讨论一遍"数据应该放哪""要不要抽接口"。

## 1. 与现有文档的关系

- 三层架构基线（计算层/状态层/时序层/UI）仍以 [`../Hermes_Pegasus/architecture.md`](../Hermes_Pegasus/architecture.md) 为准，本文件不重复、不覆盖它。
- 玩法级 Locked/Provisional/Deferred/Open 决策仍以 [`../Hermes_Pegasus/design/decision-locks.md`](../Hermes_Pegasus/design/decision-locks.md) 为准。
- 本文件只管"代码怎么组织"这一层，是 [`CODE_DECISIONS.md`](CODE_DECISIONS.md) 里具体决策（如 CD-005）的**归纳/提炼**，具体决策仍以 CD 编号记录为准，本文件负责让这些决策变成可复用的默认习惯。

## 2. 锁定等级

沿用 Pegasus 的分级概念，作用域限定在代码架构：

| 等级 | 含义 | 改动规则 |
|---|---|---|
| Locked | 当前实现地基，新功能默认必须遵守 | 只能通过新增 CD 决策显式 reopen，并在本文件更新对应条目 |
| Provisional | 先这样做，允许在后续实现中调整细节 | 可在同条目下补充修订记录 |
| Open | 还没决定 | 不能被代码或 Prompt 假装成已决定 |

## 3. Locked 约定

### AC-001：最小状态聚合（Minimal State Aggregate）

任何"会在运行时被改变的权威数据"，必须由一个**不依赖 `MonoBehaviour`/Unity API 的纯 C# 类**持有；UI 组件（`MonoBehaviour`）只允许：读取该类暴露的只读快照、调用它暴露的方法、订阅它的变化通知。

- **不允许**：UI 组件自己持有并自增/自减/直接修改本该由聚合类管理的运行时数量或状态字段。
- **允许的例外**：纯配置/初始值字段（例如"初始手牌大小"）可以留在 `SerializeField` 上，但它只作为聚合类的构造参数，一经运行时开始变化就必须交给聚合类持有。
- **变化通知**：持续变化的运行时事实以只读 R3 `ReactiveProperty` 对外公开；复合且必须原子更新的事实发布新的不可变数据快照。不要以 `Unit` 广播替代可绑定的事实值（见 AC-004）。

来源与先例：`HandState`（`CD-005`，现已演化为 `BattleCardZonesData`，见 `CD-015`），详见 `plans/2026-07-29-battlescene-drag-to-play-minimal.md`。

### AC-002：数据驱动优先，但不做投机性抽象

只有在**已经确定会替换数据源**时才引入接口/抽象边界；否则用最简单的具体实现（例如 Inspector 字段）+ 一句清晰注释标记"这里未来会被什么替换"，不要提前建 `interface`/`abstract class` 去"应对可能的未来"。

来源与先例：手牌数量来源决策——评估后放弃 `IHandCountSource` 接口方案，改为 Inspector `int` 字段 + 注释标记。

### AC-003：未决细节必须登记依赖项 ID，不允许隐性占位

任何本轮先占位、留给未来解决的实现细节，必须：

1. 在 [`DEPENDENCIES.md`](DEPENDENCIES.md) 分配一个全局唯一的 `DEP-NNN`。
2. 在代码对应位置写 `// TODO(DEP-NNN): <一句话说明>`。

不允许出现"看起来能用但没人知道是占位"的隐性简化实现。

### AC-004：运行时数据以 R3 事实值驱动 UI

`CombatantData`、`BattleCardZonesData` 等纯 C# 运行时数据属于状态层，但不用 `State` 作为类型尾缀。标量事实（如生命、力量）由私有 `ReactiveProperty<T>` 唯一持有并只读公开；多区卡牌归属由 `ReactiveProperty<CardZoneLayoutData>` 一次发布完整布局，观察者不会看到半次移动。UI 使用 `Subscribe(...).AddTo(this)` 将订阅绑定到 `MonoBehaviour` 的销毁生命周期。此条由 CD-019 修订。

### AC-005：交给外部实现 Agent 的 Prompt 必须显式引用本文件

任何 Daedalus 产出的、交给外部 Agent（如 Codex）的实施 Prompt，**必须显式引用本文件路径**（`Docs/Copilot_Daedalus/ARCHITECTURE_CONVENTIONS.md`）作为"不要重新决策"的强制前提之一。不能假设外部 Agent 已经知道这些约定——外部 Agent 没有本仓库的会话记忆，只会看到 Prompt 里写了什么。

### AC-006：场景级服务用挂载在场景内的 LifetimeScope

场景专属的运行时服务（例如战斗局内的回合调度器、抽牌堆、弃牌堆）对应的 `LifetimeScope`，必须作为 GameObject 挂载在该场景（或场景引用的 prefab）里，`parentReference` 按类型指向根 Scope；**不允许**由 `SceneFlowService` 或其他代码路径动态 `CreateChild`/手动持有场景级子 Scope。生命周期完全依赖 Unity 场景加载/卸载触发的 `Awake`/`OnDestroy`。

来源与先例：`CD-008`；此前一版动态创建子 Scope 的方案已撤回，验证记录归档于 `99_archive/2026-07-30-scene-child-scope.md`。

### AC-007：派生数据不得成为第二份运行时状态

任何可由唯一权威状态计算或筛选得出的数据（例如存活参与者、玩家阵营参与者、敌人阵营参与者），必须在实际业务需要时按需派生；不得预先维护一个可变的镜像列表、缓存计数或平行索引作为第二事实源。若将来因性能确实需要索引或缓存，必须由同一聚合在单个写入口内原子更新，并通过新的 CD 记录失效与一致性策略。

来源与先例：`BattleCombatantsData`（`CD-010`）。

### AC-008：规则随机必须使用实例随机流

影响游戏规则、存档或可复现结果的随机调用必须通过实例持有的 `GameRandom`，不得直接调用全局 `UnityEngine.Random`。每个会被不同系统独立推进的随机域（例如洗牌、敌人行为、地图、奖励）持有独立随机流；一个域增加随机调用不得改变另一个域的结果。

- `GameRandom.State` 是该流后续序列位置的唯一事实；不并存调用次数、下一值缓存或 Unity 全局随机状态镜像。
- 洗牌等随机事件执行后产生的牌序/候选结果是新的运行时事实，不要求每次读取时从初始种子重算。
- 纯表现且不影响规则的随机可以继续使用 `UnityEngine.Random`。
- 当前实现复用 `Unity.Mathematics.Random`，不自研 PRNG；见 `CD-015`。

### AC-009：共享战斗写入必须经统一权威命令顺序

玩家输入、系统阶段、敌人行动与后续 Effect 对共享战斗事实的修改，都必须先形成战斗命令并经统一提交 seam 排序。玩家可以在当前命令执行或展示期间继续提交，但同一时刻只有队首命令可以写入能量、卡区、参与者或阶段；提交接受不得提前修改权威事实。

- 单机为命令本地分配权威序号；未来联机 adapter 必须把 Host 已确认顺序送入同一个执行路径，不能建立第二条网络专用写链。
- 最终合法性在执行期依据当时事实校验；排队时的 UI 预览或本地反馈不是权威结果。
- 表现 adapter 可以局部查找 View，但必须按命令执行顺序完成展示，不能反向决定规则状态。
- 锁定的是逻辑上的统一顺序，不要求底层只能使用单一物理 FIFO。

来源：`CD-027`；玩法口径见 `../Hermes_Pegasus/design/decision-locks.md` L-005。

## 4. Provisional（先这样做，允许调整细节）

### AC-P001：R3 公开事实值，而非泛化失效信号

UI 需要随着某项运行时事实变化而更新时，订阅流必须携带该事实值或完整原子快照；不得用 `Subject<Unit>`/`Changed` 迫使观察者重新遍历无关聚合。每个 R3 属性本身是该事实的唯一持有者或其原子快照，不能并存另一份可写镜像。

### AC-P002：占位 ID 用连续整数

该过渡约定已由 CD-014 收束：卡牌数据现已接入，运行时使用独立 `CardInstanceId`，静态模板继续使用 Luban `Card.Id`。二者不得混用；重复模板卡必须拥有不同运行时实例 ID。

## 5. Open（还没决定，不能假装已决定）

- 未来是否所有最小运行时数据聚合（`BattleCardZonesData` 等）都要合并成同一个 `BattleCombatantsData` 的子聚合，还是保持多个独立聚合并存？
- 聚合类之间（例如手牌聚合 vs 未来的战场聚合）如何互相引用，是否需要一个统一的聚合根？
- G1～G3 已在 Bootstrap root 上落实 `RunStateStore` / `RunFlowService`、child Scene Scope、recipe-only persistence 与冻结地图所有权；当前权威边界见 `CD-112`、`CD-113`、`CD-116`。G4+ 不得从这些已完成 seam 推断奖励、遗物或新 Scope 已获授权。

## 6. Reopen 流程

要修改 Locked 约定：

1. 在 [`CODE_DECISIONS.md`](CODE_DECISIONS.md) 新增一条 CD 决策，说明为什么要 reopen 哪一条 `AC-NNN`。
2. 回到本文件，更新对应条目内容，并在条目下补一句"本条已被 CD-XXX 修订"。
3. 不在实现任务中顺手改约定内容而不留记录。
