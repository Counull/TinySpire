---
title: BattleScene → Run 交接最小 seam 修复
page_type: plan
lifecycle: completed
created: 2026-08-14
updated: 2026-08-14
status: implemented-and-verified
scope: B2R-101/201 typed exactly-once BattleResult；B2R-102/202 BattleSetupOptions 父 Scope 输入来源
source: ../06_testing/2026-08-14-battlescene-to-run-audit.md；2026-08-14 所有者实施授权
status_source: ../SESSION_LOG.md
---

# BattleScene → Run 交接最小 seam 修复

## 当前结论

本页记录所有者在交接审计完成后另行授权的两个最小边界加固：Battle 终局结果输出 seam，以及 Battle 输入注入 seam。审计的 `SAFE_TO_START_G1_GRILL`、无 P0/P1/PreG1Blocker 结论仍然成立；本切片不是对审计结论的推翻，也不是 G1 的首个实施计划。G1 仍为 `needs-grill`。

实现已经完成 TDD、定向回归、完整 EditMode 811/811、Runtime / Editor 静态编译，以及唯一 Unity Editor 中串行执行的 Bootstrap → BattleScene、胜利、Restart、失败、Exit、Scope 销毁与无晚到事件原生验收。当前证据及 Editor 对 `Application.Quit()` 的验证边界见 `../06_testing/2026-08-14-battlescene-to-run-seam-corrections.md`。

## 所有者冻结的局部选择

| 问题 | 本切片选择 | 明确延后 |
|---|---|---|
| 终局结果种类 | 只公开 `Victory` / `Defeat` | `Abandoned` 留给 G1 的放弃/销毁语义 |
| 公开发布时机 | 终局表现屏障完成后 exactly-once 发布 | 不在刚进入 `BattleEnded` 时提前公开 |
| 战后过渡所有权 | 保留当前 HUD Restart / Exit 接线 | 回地图、奖励、流程层收权留给 G1 |
| Battle 输入 | 父 Scope 可提供 hero / encounter / seed；无来源时保留 Inspector 默认值 | 初始 HP、牌组与 `RunState → BattleSession` 契约留给 G1 |

## W1 · typed、exactly-once 的 Battle 结果

- 新增公开不可变 `BattleResult`，冻结 `Kind`、终局命令 `AuthoritySequence`、`RoundNumber`，以及按 `CombatantId` 稳定排序的不可变 `Players`。每个玩家结算快照包含 `CombatantId / TemplateId / Health / MaxHealth / IsAlive`；本切片不加入牌组、奖励或 `RunState` 写回。
- `BattleCommandQueue` 在首次成功进入 `BattleEnded`，且终局 settlement 与 continuation 已完全冻结后才创建唯一结果，避免结果早于结算后事实。该同一对象附着在 `BattleCommandExecutionResult` 上供表现计划消费；UI 只把 `Kind` 映射为既有本地化键，不再从参与者事实重新推导胜负。
- Queue 的只读 `Result` 在对应表现 completion 真正解除屏障后才发布该对象。旧、迟到或重复 completion 不能重复发布；新 Battle Scope 从空结果开始，不继承上一场对象。
- 现有 `BattleEnded`、`BattleAlreadyEnded`、稳定终局面板和 Restart / Exit 门控保持不变。

## W2 · Battle 输入注入 seam

- `BattleSetupOptions` 继续是 hero / encounter / seed 的唯一不可变输入载体。
- `BattleLifetimeScope` 为每个 child Scope 注册一个 `BattleSetupOptions` singleton：若父 Scope 能解析 `IBattleSetupOptionsSource`，只求值一次并冻结其返回对象；否则使用当前 Inspector 默认值。来源返回空对象会立即失败，不静默回退。
- `BattleSession` 仍从 Hero / Deck 模板建立生命与牌组，并从同一个 seed 派生现有全部随机域；本切片没有增加第二份 seed、HP、牌组存储，也没有改变随机盐或引入 `UnityEngine.Random`。
- 当前生产 Bootstrap 尚未注册 Run 输入来源，因此默认 BattleScene 仍走 Inspector `1001 / 5001 / 5`。这只建立未来父 Scope 的注入边界，不解决 DEP-007 的 Run 根种子派生与恢复。

## 影响路径

生产代码：

- `TinySpire/Assets/Scripts/Battle/Commands/BattleCommandResults.cs`
- `TinySpire/Assets/Scripts/Battle/Commands/BattleCommandQueue.cs`
- `TinySpire/Assets/Scripts/Battle/BattleSession.cs`
- `TinySpire/Assets/Scripts/Battle/BattleLifetimeScope.cs`
- `TinySpire/Assets/Scripts/UI/Battle/BattleCommandPresentationAdapter.cs`
- `TinySpire/Assets/Scripts/UI/Battle/Feedback/BattleCommandPresentationPlan.cs`
- `TinySpire/Assets/Scripts/UI/Battle/Feedback/BattleFlowFeedbackTweenFactory.cs`

测试只修改既有 `TinySpire/Assets/Editor/Tests/` 文件，重点为 `BattleCommandQueueM8DTests.cs`、`BattleSessionTests.cs` 及 M9 表现计划 / adapter / flow factory 回归。没有新增 Scene、Prefab、asmdef、DataTables、Localization 或 Addressables 内容。

## TDD、验收与停止点

1. W1 精确红灯证明旧 Queue 不提供屏障后 typed 结果；最小实现后胜利、失败、重复 completion 与连续独立 Battle 均走同一结果事实。
2. W2 精确红灯证明 child Scope 尚不能消费父输入来源；最小实现后注入值、单次求值、singleton、Inspector fallback 与同/异 seed 轨迹均可验证。
3. 结算快照补强先得到 Editor compile RED（6 个缺失类型 / `Players` 错误），随后两个精确 GREEN 均为 1/1；最终门禁包括 QueueM8D 14/14、相关 9 个 fixture 127/127 与完整 EditMode 811/811，精确任务号见验收页。
4. 唯一 Editor 中串行完成真实 Queue 胜利、HUD Restart、新 Scope 空结果、真实 Queue 失败、HUD Exit exactly-once guard、2 秒无晚到结果、零 active tween，以及退出 Play 后 Battle Scope 归零；Editor 只能证明 Exit listener / guard，不能证明 Player OS 进程实际退出。
5. 本切片完成后停止，不实现 RunScope、RunState、RunFlowService、地图、奖励、存档、升级实例或战后流程重构。

## 回滚方式

- W1 可按 `BattleResult` 类型、Queue 发布接线、表现消费接线及其测试作为一个回滚单元；回滚后恢复 M9F 的 UI 即时胜负派生。
- W2 可按 `IBattleSetupOptionsSource`、`BattleLifetimeScope` 注册 helper 及其测试作为另一个回滚单元；回滚后恢复 Inspector 直接构造 `BattleSetupOptions`。
- 两个单元都不需要回滚 Scene、Prefab、表格、生成物或 Addressables。

## Open owner item

`B2R-203` 继续是待所有者裁决的 DocumentationDrift：是否 reopen Pegasus D-003 并同步 `STATUS.md` / `project-definition.md` / `decision-locks.md`，或明确 Daedalus 为当前阶段事实源。本切片不读取该问题为代码约束，也不修改 `Docs/Hermes_Pegasus/**`。
