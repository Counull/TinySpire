---
title: TinySpire · 实现架构约定（Implementation Architecture Conventions）
owner: Daedalus
page_type: convention
lifecycle: active
created: 2026-07-29
updated: 2026-07-30
status_source: SESSION_LOG.md
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
- **变化通知**：当前阶段统一用最简单的 `event Action`，不提前引入 R3（见 AC-004）。

来源与先例：`HandState`（`CD-005`），详见 `plans/2026-07-29-battlescene-drag-to-play-minimal.md`。

### AC-002：数据驱动优先，但不做投机性抽象

只有在**已经确定会替换数据源**时才引入接口/抽象边界；否则用最简单的具体实现（例如 Inspector 字段）+ 一句清晰注释标记"这里未来会被什么替换"，不要提前建 `interface`/`abstract class` 去"应对可能的未来"。

来源与先例：手牌数量来源决策——评估后放弃 `IHandCountSource` 接口方案，改为 Inspector `int` 字段 + 注释标记。

### AC-003：未决细节必须登记依赖项 ID，不允许隐性占位

任何本轮先占位、留给未来解决的实现细节，必须：

1. 在 [`DEPENDENCIES.md`](DEPENDENCIES.md) 分配一个全局唯一的 `DEP-NNN`。
2. 在代码对应位置写 `// TODO(DEP-NNN): <一句话说明>`。

不允许出现"看起来能用但没人知道是占位"的隐性简化实现。

### AC-004：新状态类当前归属"状态层"的过渡实现

按 `architecture.md` 的三层模型，像 `HandState` 这样的纯 C# 状态类，目前对应"状态层"的**早期过渡实现**（用 `event Action` 而非 R3 `ReactiveProperty`）。这不是绕开 R3，而是状态层设计本身还没定（`STATUS.md` Open Question 里的"是否引入 VContainer/R3/UniTask"仍未答复）。一旦项目正式接入 R3，需要一次专门的迁移任务，不能在某个具体功能开发中顺手引入 R3 打破这个约定的一致性。

### AC-005：交给外部实现 Agent 的 Prompt 必须显式引用本文件

任何 Daedalus 产出的、交给外部 Agent（如 Codex）的实施 Prompt，**必须显式引用本文件路径**（`Docs/Copilot_Daedalus/ARCHITECTURE_CONVENTIONS.md`）作为"不要重新决策"的强制前提之一。不能假设外部 Agent 已经知道这些约定——外部 Agent 没有本仓库的会话记忆，只会看到 Prompt 里写了什么。

### AC-006：场景级服务用挂载在场景内的 LifetimeScope

场景专属的运行时服务（例如战斗局内的回合调度器、抽牌堆、弃牌堆）对应的 `LifetimeScope`，必须作为 GameObject 挂载在该场景（或场景引用的 prefab）里，`parentReference` 按类型指向根 Scope；**不允许**由 `SceneFlowService` 或其他代码路径动态 `CreateChild`/手动持有场景级子 Scope。生命周期完全依赖 Unity 场景加载/卸载触发的 `Awake`/`OnDestroy`。

来源与先例：`CD-008`；此前一版动态创建子 Scope 的方案已撤回，验证记录归档于 `99_archive/2026-07-30-scene-child-scope.md`。

### AC-007：派生数据不得成为第二份运行时状态

任何可由唯一权威状态计算或筛选得出的数据（例如存活参与者、玩家阵营参与者、敌人阵营参与者），必须在实际业务需要时按需派生；不得预先维护一个可变的镜像列表、缓存计数或平行索引作为第二事实源。若将来因性能确实需要索引或缓存，必须由同一聚合在单个写入口内原子更新，并通过新的 CD 记录失效与一致性策略。

来源与先例：`BattleState`（`CD-010`）。

## 4. Provisional（先这样做，允许调整细节）

### AC-P001：变化通知用 `event Action`

直到状态层正式接入 R3 之前，所有新状态聚合类的变化通知都用 `event Action`，保持写法一致，方便以后统一批量迁移到 `ReactiveProperty`。

### AC-P002：占位 ID 用连续整数

尚未接入真实卡牌数据前，聚合类内部的卡牌/实体 ID 统一用从 0 开始的连续整数占位（如 `0..N-1`），不要为占位阶段设计正式的 ID 方案，避免和未来 Luban 数据的真实 ID 规则冲突。

## 5. Open（还没决定，不能假装已决定）

- 未来是否所有"最小状态聚合"类（`HandState` 等）都要合并成同一个 `BattleState` 的子聚合，还是保持多个独立聚合并存？
- R3 正式接入状态层的具体触发条件/时间点是什么？
- 聚合类之间（例如手牌聚合 vs 未来的战场聚合）如何互相引用，是否需要一个统一的聚合根？
- 存档层 `RunScope`/`RunFlowService`/`RunState`（`CD-009`）目前只是前瞻记录，`RunFlowService` 的具体触发时机、`RunState` 的字段结构、地图是否需要独立 Scope 均未实现，不能当作已落地约定使用。

## 6. Reopen 流程

要修改 Locked 约定：

1. 在 [`CODE_DECISIONS.md`](CODE_DECISIONS.md) 新增一条 CD 决策，说明为什么要 reopen 哪一条 `AC-NNN`。
2. 回到本文件，更新对应条目内容，并在条目下补一句"本条已被 CD-XXX 修订"。
3. 不在实现任务中顺手改约定内容而不留记录。
