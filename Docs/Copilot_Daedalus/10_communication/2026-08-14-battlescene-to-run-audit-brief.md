---
title: BattleScene → Run 阶段交接只读审计任务书
page_type: communication
lifecycle: active
created: 2026-08-14
updated: 2026-08-14
scope: milestone-battlescene-mvp-2026-08-14
status_source: ../SESSION_LOG.md
---

# BattleScene → Run 阶段交接只读审计任务书

> 本页是交给外部代码审计 Agent 的稳定任务书，不是 G1 实施计划，也不授权修改代码。当前动态状态只查 [SESSION_LOG.md](../SESSION_LOG.md)。

## 1. 审计目标

判断已提交的 BattleScene 基线中，是否存在必须在 G1 首片 Grill 之前修复的真实缺陷、生命周期阻塞或事实源冲突。

必须区分四类结论：

1. **ExistingDefect**：已完成 BattleScene 规格中的可复现缺陷。
2. **PreG1Blocker**：不先纠正就会迫使 G1 建立第二权威写链、错误生命周期或不可恢复数据边界的问题。
3. **G1DesignInput**：G1 必须 Grill 的接口或所有权选择；缺少尚未设计的 Run 功能本身不是旧代码缺陷。
4. **DocumentationDrift / TestGap**：事实源漂移或证据缺口，不得冒充已确认的运行时错误。

审计不设计完整 G1，不实施修复，不把 UI / 动画品质债改判为 BattleScene 规则里程碑失败。

## 2. 冻结基线

| 项目 | 固定值 |
|---|---|
| BattleScene 里程碑 tag | `milestone-battlescene-mvp-2026-08-14` |
| tag 解引用 commit | `e07e39a29efe6395f79c2d9e63b1ae3b740263b5` |
| Roadmap 交接快照 | `18d9023494a9da1975d158cf0b176f0fc45d28c9` |

已知事实：从 `e07e39a` 到 `18d9023`，已提交的 `TinySpire/**` 没有变化；后者只增加 Roadmap 交接文档与仓库忽略规则。因此：

- 产品代码只审 `e07e39a` 的提交对象。
- Run 交接语义读取 `18d9023` 的 Roadmap / CD-110，以及本任务书。
- 当前工作区未提交内容不属于审计证据。若审计范围内的源码、测试、Scene 或 Prefab 相对基线存在未提交改动，返回 `BASELINE_UNRELIABLE` 并停止，不得自行 stash、checkout、reset 或清理。
- `ProjectSettings`、`00_inbox` 和根级本地配置的既有工作区改动明确排除，不得读取为正式规格，也不得修改。

开始前必须只读验证：

```text
git rev-parse "milestone-battlescene-mvp-2026-08-14^{}"
git rev-parse 18d9023494a9da1975d158cf0b176f0fc45d28c9
git diff --name-only milestone-battlescene-mvp-2026-08-14..18d9023494a9da1975d158cf0b176f0fc45d28c9 -- TinySpire
```

第一条必须得到完整 `e07e39a...`；第三条必须为空。

## 3. 必读来源

### 3.1 Standards / Architecture

- `AGENTS.md`
- `Docs/AI_COLLABORATION_RULES.md`
- `Docs/Copilot_Daedalus/AGENTS.md`
- `Docs/Copilot_Daedalus/README.md`
- `Docs/Copilot_Daedalus/ARCHITECTURE_CONVENTIONS.md`：重点 AC-001、AC-002、AC-006～AC-009 与 Open 部分
- `Docs/Copilot_Daedalus/CODE_DECISIONS.md`：CD-008、CD-009、CD-027、CD-110
- `Docs/Hermes_Pegasus/architecture.md`
- `Docs/Hermes_Pegasus/design/decision-locks.md`：重点 L-005、L-006、L-007、D-003

### 3.2 Spec / Acceptance

- `Docs/Copilot_Daedalus/RUN_ROADMAP.md`：§1、§2、§4、§7
- `Docs/Copilot_Daedalus/SESSION_LOG.md`：顶部 2026-08-14 交接记录
- `Docs/Copilot_Daedalus/DEPENDENCIES.md`：DEP-007
- `Docs/Copilot_Daedalus/ROADMAP.md`：BattleScene 完成定义与阶段交接
- `Docs/Copilot_Daedalus/06_testing/2026-08-02-m9f-turn-terminal-restart-exit.md`
- `Docs/Copilot_Daedalus/06_testing/2026-08-02-m9g-full-validation-review.md`
- `Docs/Copilot_Daedalus/06_testing/2026-08-05-m10c-determinism-lifecycle.md`
- `Docs/Copilot_Daedalus/06_testing/2026-08-05-m10d-delivery-validation.md`

`Docs/Hermes_Pegasus/STATUS.md`、`design/project-definition.md` 和 `design/decision-locks.md` 仍可能保留 BattleScene 阶段表述或把完整 Run 循环列为 Deferred。审计应把冲突报告为 `DocumentationDrift / NeedsOwnerDecision`，不能自行用旧文档否决 CD-110 的阶段换轨，也不能修改 Pegasus 的事实源。

## 4. 最小代码范围

### 启动与场景流

- `TinySpire/Assets/Scripts/Core/Bootstrap.cs`
- `TinySpire/Assets/Scripts/Core/GameLauncher.cs`
- `TinySpire/Assets/Scripts/Core/SceneFlowService.cs`

### Battle 输入、状态与生命周期

- `TinySpire/Assets/Scripts/Battle/BattleLifetimeScope.cs`
- `TinySpire/Assets/Scripts/Battle/BattleSession.cs`
- `TinySpire/Assets/Scripts/Battle/BattleCombatantsData.cs`
- `TinySpire/Assets/Scripts/Battle/CombatantData.cs`
- `TinySpire/Assets/Scripts/Battle/BattleCardZonesData.cs`

### 命令、终局与输出

- `TinySpire/Assets/Scripts/Battle/BattleTerminalRules.cs`
- `TinySpire/Assets/Scripts/Battle/Commands/BattleCommandQueue.cs`
- `TinySpire/Assets/Scripts/Battle/Commands/BattleCommandResults.cs`
- `TinySpire/Assets/Scripts/Battle/Commands/IBattleCommandPresentation.cs`
- `TinySpire/Assets/Scripts/Battle/Turn/BattleTurnData.cs`
- `TinySpire/Assets/Scripts/Battle/Turn/BattleTurnController.cs` 的终局相关路径
- `TinySpire/Assets/Scripts/Battle/Effects/BattleSettlementRecord.cs` 的阶段变更事实

### 当前终局表现与场景边界

- `TinySpire/Assets/Scripts/UI/Battle/BattleCommandPresentationAdapter.cs`
- `TinySpire/Assets/Scripts/UI/Battle/BattleTurnHudView.cs`
- `TinySpire/Assets/Scripts/UI/Battle/Feedback/BattleCommandPresentationPlan.cs`
- `TinySpire/Assets/Scripts/UI/Battle/Feedback/BattleFlowFeedbackTweenFactory.cs`
- `TinySpire/Assets/Scenes/BootstrapScene.unity`
- `TinySpire/Assets/Scenes/BattleScene.unity`
- `TinySpire/Assets/AddressableAssetsData/AssetGroups/TinySpire Scenes.asset`

### 对应测试

只定位与以下主题直接相关的测试：`BattleSession`、M8D Queue/终局、M9F 终局 UI、M10C 生命周期、Bootstrap / GameStartup。不要重新审计全部卡牌或表现测试。

## 5. 必答问题

1. Battle 输入未来能否由 Run 提供，而不复制 hero、encounter、HP、牌组、seed 或随机流事实？
2. 是否存在与表现层无关、typed、exactly-once 的 Battle 终局输出 seam？如果尚不存在，它是现有缺陷还是 G1DesignInput？
3. Run 写回能否等待结算、continuation 与必要表现边界，而不让 UI 成为流程权威？
4. `BattleLifetimeScope` 当前父子关系是否允许未来插入 Run 生命周期，还是存在必须先修的硬阻塞？
5. 场景卸载后 Battle Session、Queue、订阅与 Tween 是否销毁；未来 Run 生命周期能否继续存活？
6. Run 与 Battle 随机域能否保持确定性且互不推进？
7. Pegasus 与 Daedalus 的阶段事实源是否需要在 G1 Grill 前显式 reopen 或同步？

## 6. 明确排除

- UI、动画、美术品质重做。
- 卡牌内容、职业运行时、数值、Effect 与状态规则复审。
- 地图、奖励、存档 schema、商店、事件、遗物等 G2+ 设计。
- 联机、多人装配、预测或重放。
- 大规模 DI、程序集、Scene 或 Prefab 重构建议。
- DataTables、生成物、Localization、Addressables、Unity、build 或测试运行。
- 创建、修改、删除、格式化、生成、暂存、提交、推送或切换任何文件/分支。

## 7. 证据与严重度

每条 finding 必须同时具备：

- 对应 Spec / Standard 的精确位置。
- 代码或测试的 `项目相对路径:行号`。
- 可观察失败路径，而非“某类型不存在”的推测。
- 为什么会影响已完成 BattleScene 或 G1 入口。
- 最小纠正边界与最小验证建议；不得直接给完整补丁。
- `high / medium / low` 置信度；证据不足时写“未确认”。

严重度：

- `P0`：当前基线会破坏数据、无法结束/退出战斗或形成不可控共享写入。
- `P1`：高概率阻塞 G1，且无法只靠 G1 局部 adapter 解决。
- `P2`：应在 G1 计划中处理或进入明确 repair slice，但不阻止开始 Grill。
- `P3`：证据、文档或低风险债务；进入 backlog。

风格偏好、投机性重构和“未来可能更优雅”不能成为 blocker。

## 8. 固定输出

报告顶部必须三选一：

- `SAFE_TO_START_G1_GRILL`
- `PRE_G1_CORRECTION_REQUIRED`
- `BASELINE_UNRELIABLE`

随后分别输出 `Standards` 与 `Spec / Transition`，不得合并两条轴或用一条轴掩盖另一条。最多给出 5 条 actionable findings：

```markdown
## B2R-001 · 标题

- Category: ExistingDefect | PreG1Blocker | G1DesignInput | DocumentationDrift | TestGap
- Severity: P0 | P1 | P2 | P3
- Action timing: before-g1-grill | during-g1-plan | backlog
- Confidence: high | medium | low
- Contract: `path:line`
- Evidence:
  - `path:line` — 直接事实
- Failure path: 可观察失败路径
- Impact: 对 BattleScene 基线或 G1 的实际影响
- Minimal correction boundary: 最小影响文件/符号，不提供补丁
- Required validation: 最小测试或 Unity 证据
```

最后必须附：

- `Not findings`：已检查但没有成立的问题。
- `Questions for later Grill`：属于 G1 选择、不能冒充现有缺陷的问题。
- 两轴各自 finding 数量与最严重项；不选跨轴“总冠军”。

## 9. 结果回收规则

- Harness 输出只是外部 `source-only` 审查材料，不自动成为项目事实。
- 不允许 Harness 在同一会话修复。
- 返回本仓后逐条复现；只有确认的 blocker 才建立 `plans/YYYY-MM-DD-g1-preflight-fix-<slug>.md` 并单独授权。
- 经复核的最终审计记录再进入 `06_testing/`；若没有确认问题，可以明确记录通过，而不是为了“审计有产出”制造修改。
