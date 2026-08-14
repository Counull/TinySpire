---
title: BattleScene → Run 交接只读审计（外部 Harness · 复核归档）
page_type: testing
lifecycle: active
created: 2026-08-14
updated: 2026-08-14
status: passed
scope: BattleScene MVP（e07e39a）与 Run 交接语义（18d9023）；基线核验、Standards/Architecture 轴、Spec/Transition 轴
audit_brief: ../10_communication/2026-08-14-battlescene-to-run-audit-brief.md
status_source: ../SESSION_LOG.md
---

SAFE_TO_START_G1_GRILL

# BattleScene → Run 交接只读审计报告

## 1. Verdict

SAFE_TO_START_G1_GRILL — 基线可靠，两轴均未发现 ExistingDefect、PreG1Blocker、P0 或 P1。BattleScene 里程碑（M0~M10）的权威写链、纯 C# 状态聚合、确定性随机、场景生命周期均符合冻结约定；全部 4 条 P2 均为 G1DesignInput（G1 进出契约设计输入，属尚未设计的 Run 功能），1 条 P3 为 DocumentationDrift（需所有者裁决，不阻塞 Grill）。不存在必须在 G1 首片 Grill 之前修复的真实缺陷。

## 2. Baseline verification

| 检查项 | 结果 |
|---|---|
| `git rev-parse "milestone-battlescene-mvp-2026-08-14^{commit}"` | `e07e39a29efe6395f79c2d9e63b1ae3b740263b5` — 与固定基线一致（annotated tag `cbc5565…`，指向 `feat(battle): complete BattleScene content milestone`） |
| `git rev-parse 18d9023494a9da1975d158cf0b176f0fc45d28c9` | 可解析，且是当前 HEAD（分支 `main`，`chore(repo): ignore local agent artifacts`） |
| 祖先关系 | `merge-base --is-ancestor e07e39a 18d9023` → exit 0；反向 exit 1 |
| `git diff --name-only e07e39a..18d9023 -- TinySpire/` | 空（要求满足） |
| 区间内全部变更 | 仅 `.gitignore` 与 Docs（`RUN_ROADMAP.md` 新增、`ROADMAP/SESSION_LOG/CODE_DECISIONS/README` 等修改），无产品代码 |
| 工作区 `TinySpire/**` 未提交改动 | 仅 `TinySpire/ProjectSettings/ProjectSettings.asset`（简报 §2 明确排除）；审计范围内源码/测试/Scene/Prefab 无任何未提交改动 |
| 其他脏文件 | `00_inbox`、`10_communication`、`game-config.json` 等按简报不采为正式证据 |
| 审计范围路径 | 简报 §4 的 22 个代码/Scene/资产路径全部存在于 `e07e39a` |

结论：提交对象基线可靠，不触发 BASELINE_UNRELIABLE。产品代码只审 `e07e39a`，Run 交接语义读取 `18d9023` 文档（`SESSION_LOG.md` 顶部交接记录为 committed 版第 6-13 行）。审计过程中对全部承重引证行做了独立只读复核，与各轴 Evidence 一致。

## 3. Standards

### B2R-101 · 缺少与表现层无关、typed、exactly-once 的 Battle 终局输出 seam（Victory/Defeat 仅 internal 且瞬态）

- Category: G1DesignInput
- Severity: P2
- Action timing: during-g1-plan
- Confidence: high
- Contract: `Docs/Copilot_Daedalus/RUN_ROADMAP.md:66` — BattleResult → RunState 原子写回契约；`Docs/Copilot_Daedalus/CODE_DECISIONS.md:126` — CD-009 前瞻
- Evidence:
  - `TinySpire/Assets/Scripts/Battle/BattleTerminalRules.cs:6,15,26-47` — internal 枚举/类，胜负即时派生即弃、不保存到 Turn
  - `TinySpire/Assets/Scripts/Battle/Turn/BattleTurnData.cs:10-22` — BattleEnded 阶段不区分胜负（`BattleTurnController.cs:1465-1482` 发布中立终局阶段）
  - `TinySpire/Assets/Scripts/Battle/Commands/BattleCommandQueue.cs:25-28` — 对外只暴露只读 Queue/Turn，无结果对象；BattleSession 为场景级 Singleton，卸载即 Dispose
- Failure path: Run 只能反读 Combatants 存活重新推导（类型为 internal），或反解 UI 文案键，形成第二推导链。
- Impact: RUN_ROADMAP §1/§4 的结果接回无落点；属 G1 边界设计输入，不是 BattleScene 基线缺陷。
- Minimal correction boundary: BattleCommandQueue / BattleTurnController 增加公开只读终局结果事实（Victory/Defeat + 恰好一次），形态由 G1 Grill 决定。
- Required validation: 胜/败各发布恰好一次 typed 结果；连续两场无结果残留。

### B2R-102 · Battle 输入 seam 是场景内 Inspector 常量，HP/牌组/种子尚无 Run 注入点

- Category: G1DesignInput
- Severity: P2
- Action timing: during-g1-plan
- Confidence: high
- Contract: `Docs/Copilot_Daedalus/DEPENDENCIES.md:32` — DEP-007（open）；`Docs/Copilot_Daedalus/RUN_ROADMAP.md:27,64-66`
- Evidence:
  - `TinySpire/Assets/Scripts/Battle/BattleLifetimeScope.cs:11-19` — Inspector 常量 + `TODO(DEP-007)`，无父 Scope 注入点
  - `TinySpire/Assets/Scripts/Battle/BattleSession.cs:141,151,160-186` — 生命恒取模板 MaxHealth、牌组恒取模板；单一种子派生全部随机域
  - 正面事实：`BattleSetupOptions`（`BattleSession.cs:25-37`）是公开、不可变、带校验的构造接缝，无 HP/牌组/种子复制
- Failure path: Run 持久生命/牌组/种子无法进入战斗；若拆成多条随机流注入会破坏 M10C 确定性轨迹。
- Impact: 已知前瞻缺口 DEP-007 的更广输入快照契约；输入形状已满足要求，无硬阻塞，非旧代码缺陷。
- Minimal correction boundary: BattleSetupOptions / BattleSession.FromConfig 增加可注入初始 HP/牌组/种子；BattleLifetimeScope 常量改 Run 注入。
- Required validation: 同种子两场轨迹一致、不同种子不同；无第二份 seed/HP/牌组事实。

> 本轴 reviewer 的第 3 条 finding（Pegasus 事实源漂移，DocumentationDrift·P3·backlog，引用 `Docs/Hermes_Pegasus/design/decision-locks.md:126-130`、`project-definition.md:39,84`）与 Spec 轴 B2R-203 为同一漂移问题。受简报 §8「最多 5 条」上限约束，在轻量聚合中保留证据更完整、时机更靠前的 B2R-203，此条仅作裁剪记录，不跨轴重排。

## 4. Spec / Transition

### B2R-201 · 缺少 typed / exactly-once 的 Battle 结果输出 seam；胜负只由 UI 即时派生，战后过渡由 HUD 直接拥有

- Category: G1DesignInput
- Severity: P2
- Action timing: during-g1-plan
- Confidence: high
- Contract: `Docs/Copilot_Daedalus/RUN_ROADMAP.md:27,66-67` — UI 只能派生和提交命令；结果写回契约与战后过渡待 Grill
- Evidence:
  - `TinySpire/Assets/Scripts/UI/Battle/BattleCommandPresentationAdapter.cs:293-312` — UI 临时 `new BattleTerminalRules(combatants).Evaluate()` 映射文案键，注释明确「不保存 outcome / 不公开规则 / 不注册新 seam」
  - `TinySpire/Assets/Scripts/UI/Battle/BattleTurnHudView.cs:283-299` + `BattleCommandPresentationAdapter.cs:279-291` — Restart 直接重载场景、Exit 直接 `Application.Quit()`，不经过 Queue/Run；「放弃」在数据层无表达
  - 正面边界：BattleEnded 阶段是 exactly-once 数据事实（`BattleCommandQueue.cs:138-143,268-272`）；终局按钮仅在表现序列 `reachedStableEnd` 后激活（`BattleTurnHudView.cs:248-258`），无屏障前重开/退出路径
- Failure path: Run 写回只能 internal 反射式读取 / 重推导 / 反解文案；战后流程由 HUD 成为权威，违背 RUN_ROADMAP:27。
- Impact: 不破坏基线；G1 必须先定义结果边界与战后过渡所有权，否则被迫建立第二权威写链或 UI 驱动流程。
- Minimal correction boundary: 终局边界新增公开 typed `BattleResult`（Victory/Defeat/Abandoned，含结算后事实）并 exactly-once 发布；战后过渡从 HUD 收回到流程层。
- Required validation: 单测证明终局命令在 settlement+continuation 冻结后恰好发布一次；UI 回调不是结果唯一载体。

### B2R-202 · Battle 输入源为 Inspector 常量，尚无 Run 注入接缝（DEP-007 open）

- Category: G1DesignInput
- Severity: P2
- Action timing: during-g1-plan
- Confidence: high
- Contract: `Docs/Copilot_Daedalus/DEPENDENCIES.md:32`；`Docs/Copilot_Daedalus/RUN_ROADMAP.md:64-66`
- Evidence: `TinySpire/Assets/Scripts/Battle/BattleLifetimeScope.cs:11-14,19`；`TinySpire/Assets/Scripts/Battle/BattleSession.cs:25-37,160-186`（单一种子派生、无事实复制，正面结论）
- Failure path / Impact / Minimal correction boundary / Required validation: 同 B2R-102。

### B2R-203 · Pegasus 阶段事实源漂移：仍写 BattleScene MVP planning、P0 未勾选、D-003 外层循环仍标 Deferred

- Category: DocumentationDrift
- Severity: P3
- Action timing: during-g1-plan
- Confidence: high
- Contract: `Docs/Copilot_Daedalus/SESSION_LOG.md:6-13`（committed `18d9023`）；`Docs/Copilot_Daedalus/CODE_DECISIONS.md:1222-1232` — CD-110 换轨
- Evidence:
  - `Docs/Hermes_Pegasus/STATUS.md:8-10,58-66` — 「状态：planning」，P0 BattleScene MVP 7 条未勾选
  - `Docs/Hermes_Pegasus/design/project-definition.md:22` — 「BattleScene MVP planning / early implementation」
  - `Docs/Hermes_Pegasus/design/decision-locks.md:126-130` — D-003 完整 roguelike 外层循环仍 Deferred
- Failure path: 后续 Agent 读 Pegasus 文档会误以为 BattleScene MVP 未完成、Run 被延期。
- Impact: 纯事实源冲突；需用户显式裁决 reopen D-003 并同步 STATUS/project-definition；不得用旧文档否决 CD-110，本审计不改 Pegasus 文档。
- Minimal correction boundary: 所有者裁决后同步上述三处；无代码改动。
- Required validation: 所有者给出 reopen 或「保留为设计参考、以 Daedalus 为现行事实源」的裁决记录。

## 5. Not findings（已核验不成立）

- 唯一写 seam 未被绕过（AC-009/CD-027）：全 Scripts 仅 3 处外部 Submit（`BattleTurnHudView.cs:403`、`HandCardContainer.cs:521`、`BattleLifetimeScope.cs:121`）；UI 目录无任何 `ReactiveProperty.Value=` 写入；CombatantData internal 写方法仅经 `BattleCommandQueue.Execute` 可达。
- 权威状态由纯 C# 聚合持有（AC-001/AC-004/AC-007）：CombatantData/BattleTurnData/BattleCardZonesData 纯 C# 持有，R3 只读公开。
- 确定性随机无跨域耦合、无第二事实源（AC-008）：各域独立 GameRandom 实例；`UnityEngine.Random` 仅 `UI/Loading/RandomLoadingCover.cs:17` 纯表现一处。
- 场景生命周期与订阅销毁合规（AC-006/CD-008）：`BattleScene.unity:323-324` `parentReference: Bootstrap`；`BootstrapScene.unity:166-167` 根为空父；`Bootstrap.cs:20` DontDestroyOnLoad；`SceneFlowService.cs:53` LoadSceneMode.Single；订阅全部 `.AddTo(this)`；M10C/M10D 记录 scope 归零、DOTween 回基线。
- 完成定义与 807/807 一致：M10D 两项失败明确为非 M10 的 M9 UI/Targeting 套件异常（`06_testing/2026-08-05-m10d-delivery-validation.md:78-85`），已单独授权承接。
- UI/动画品质债、CatalogOnly、升级实例等按任务书排除。
- asmdef 缺失 / `BattleCardZonesData` public mutator 仅靠约定：grep 证明无队列外调用者，属 G1 可选硬化项，不凑数。

## 6. Questions for later Grill

1. BattleResult 形态与 exactly-once 语义；Run 在 BattleEnded 阶段还是表现屏障完成后观察？
2. 战后过渡（胜利→地图/奖励/独立「恭喜」页）由谁拥有，如何替换 HUD 直接持有的 Restart/Exit？「放弃」是否一等结果？
3. RunScope（CD-009）如何成为 BattleLifetimeScope 父 Scope，parentReference 改指时不破坏 CD-008？
4. Run 根种子如何派生 Battle 种子与各域盐（当前仅敌人意图有盐）；DEP-007 恢复规则。
5. 是否引入 asmdef、收窄 BattleCardZonesData public mutator，把写 seam 升级为编译期强制？
6. Pegasus D-003/project-definition/STATUS 的 Deferred 表述是否由 Pegasus 所有者 reopen？
7. DOTween 销毁是否由 SetAutoKill 完整覆盖（表现债，非 blocker）。

## 7. 两轴 finding 数量与最严重项

| 轴 | finding 数 | 最严重项 |
|---|---|---|
| Standards | 3（1 条 P3 漂移与 Spec 轴同源，按 5 条上限裁剪） | B2R-101 · G1DesignInput · P2 |
| Spec / Transition | 3 | B2R-201 · G1DesignInput · P2 |

合计呈报 5 条：4 × G1DesignInput·P2·during-g1-plan + 1 × DocumentationDrift·P3·during-g1-plan。无 P0/P1、无 ExistingDefect、无 PreG1Blocker、无 TestGap。Harness 输出仅为外部 source-only 审查材料，不自动成为项目事实（简报 §9）。

## 8. 归档与后续动作

- 本记录是外部 Harness source-only 审查材料的复核归档：全部 finding 的行级证据已在审计过程中只读复核，未发现失实引用；本记录本身不构成修复授权。
- 无确认 blocker：按简报 §9 不建立 `plans/YYYY-MM-DD-g1-preflight-fix-*.md`；G1 仍为 `needs-grill`，不存在开始 G1 首片 Grill 前必须纠正的真实缺陷。
- 4 条 P2 为 G1DesignInput，进入 G1 首片 Grill 的接口候选（`RUN_ROADMAP.md` §4）；B2R-203 待所有者裁决 reopen/同步 Pegasus 事实源。
- 交给外部实现 Agent 的最小修复切片 Prompt 由所有者在对话中另行复制，未写入本库。
