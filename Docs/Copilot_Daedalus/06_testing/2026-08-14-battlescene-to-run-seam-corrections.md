---
title: BattleScene → Run 交接最小 seam 修复验收
page_type: testing
lifecycle: verified
created: 2026-08-14
updated: 2026-08-14
status: passed
scope: B2R-101/201 typed exactly-once BattleResult；B2R-102/202 BattleSetupOptions 父 Scope 输入来源
plan: ../plans/2026-08-14-battlescene-to-run-seam-corrections.md
source: 2026-08-14-battlescene-to-run-audit.md
status_source: ../SESSION_LOG.md
---

# BattleScene → Run 交接最小 seam 修复验收

## 当前结论

W1 与 W2 的代码、TDD、自动回归和唯一 Unity Editor 原生串行验收均已完成。`BattleResult` 现在由 Queue 在终局 settlement 与 continuation 完全冻结后创建，同一实例交给表现层，并在表现屏障完成后经只读 `Result` exactly-once 公开；它同时冻结按 `CombatantId` 稳定排序的不可变玩家结算快照。`BattleSetupOptions` 可由父 Scope 来源注入 hero / encounter / seed，无来源时保持 Inspector 默认行为。最终完整 EditMode 为 **811/811 passed**。

本页不把这两个 seam 扩写成完整 Run 契约：结果已经包含结算后玩家身份与生命事实，但没有牌组、奖励或 RunState 写回；输入也没有初始 HP / 牌组。原生验收已覆盖真实 Queue 胜败、HUD Restart / Exit 接线、Scope 重建 / 销毁与无晚到结果。由于运行环境是 Editor，Exit 只验证 listener 与重复提交 guard，没有证明 Player OS 进程实际退出；这是本页明确保留的验证边界，不影响当前 seam 的 Editor 验收结论。

## 冻结边界

| 维度 | 已实现 | 未实现 |
|---|---|---|
| 结果种类 | `Victory`、`Defeat` | `Abandoned` |
| 结果事实 | Queue-owned immutable `BattleResult`；同一实例进入 execution / presentation / public result；`Players` 按 `CombatantId` 稳定排序且每项含 `CombatantId / TemplateId / Health / MaxHealth / IsAlive` | 牌组、奖励、RunState 写回 |
| 发布时间 | 对应表现屏障完成后一次发布 | `BattleEnded` 刚进入时提前公开 |
| 终局 UI | typed `Kind` → 既有 victory / defeat localization key | 战后 UI 或流程重构 |
| 输入注入 | 父 `IBattleSetupOptionsSource` → child singleton；Inspector fallback | RunScope 生产注册、初始 HP / 牌组快照 |
| 随机 | 单一 seed 沿用现有各域派生 | Run 根种子、战斗标识、恢复规则 |

## 红灯到最小实现

| 契约 | RED | GREEN |
|---|---|---|
| W1 · 胜利结果只在表现屏障后一次发布 | `2ca45fe6a4aa4eb0a542518cebbfd5ec`：**1/1 failed**；旧实现没有可发布的 typed 结果 | `db9bf0f16e0d47f0819e7726a2e4dbfd`：**1/1 passed**；同一结果对象先进入 terminal execution，completion 后公开 |
| W2 · child Scope 冻结父输入来源 | `41d98d7a27f14accbd7060b642062ed5`：**1/1 failed**；接口与注册 seam 尚不存在 | `68bf7bc1820248bd8d4bf0c40b5ebc4f`：**1/1 passed**；注入值精确生效、来源一次求值、两次 resolve 为同一实例 |
| W1 补强 · 结果冻结结算后玩家事实 | Editor build **6 errors**；测试引用的 `Players` / 玩家快照类型尚不存在 | `cdbee956af0449ceb154b459d9115ab6`、`9b2cc0f1a9c94bd3883bac240cfeba79`：各 **1/1 passed**；胜败结果均携带与权威参与者一致的稳定不可变快照 |

后续补强覆盖失败结果、重复 completion、连续两个独立 Battle、Inspector fallback 与不同 seed 的不同洗牌轨迹。UI 表现测试同时断言 `BattleOutcome` step 消费 execution 上的同一 `BattleResult`，不再通过 `BattleTerminalRules` 二次推导文案。

## 自动验证

| 检查 | 结果 |
|---|---|
| QueueM8D 定向 | `439414c167ee4058ae6ce48bfd6e137b`：**14/14 passed** |
| Queue、Session、M9F UI、输入注入与确定性相关 9 个 fixture | `445e2407b7494d7291c9d192b12ba0fe`：**127/127 passed** |
| 完整 EditMode | `7057ee5000a24d739b347076ee766c6e`：**811/811 passed**，`18.8400845s` |
| Editor 静态 build | **0 error / 12 warnings**；warning 均为既有 Unity / R3 / UniTask 程序集版本冲突类别 |
| Runtime 静态 build | **0 error / 6 warnings**；warning 均为既有 MSB3277 程序集版本冲突类别 |

## Unity 原生生产路径

- 上述任务全部由当前 Unity Test Runner 执行，证明公开 seam、终局 UI 映射、确定性与 Scope 输入注册的自动契约。
- 唯一 Unity Editor 串行从 BootstrapScene 进入 BattleScene，并通过生产 `BattleCommandQueue` 完成胜利；公开 `Result` 发布时已经越过表现屏障，`Players` 与结算后的权威玩家事实一致。
- 真实 HUD Restart 接线执行后旧 `BattleLifetimeScope` 消失，新 Scope 的 `Result` 从空值开始；随后通过生产 Queue 完成失败，没有沿用上一场结果。
- 真实 HUD Exit seam 以临时 probe 连续触发两次，只调用一次并锁定按钮；等待 2 秒后 Result 没有晚到或重复发布，`activeTweens=0`。这是 Editor 对 Exit listener / exactly-once guard 的验收，`Application.Quit()` 在 Editor 中不能证明 Player OS 进程实际退出。
- 停止 Play 后回到 BootstrapScene，`BattleLifetimeScope=0`；Console error=0。全过程只使用唯一 Editor 串行执行，没有启动第二实例。

## 范围与不变量

- `BattleEnded` 阶段、终局后的 `BattleAlreadyEnded` 拒绝、M9F 稳定面板与 Restart / Exit 按钮门控保持原行为。
- 生产 Bootstrap 没有注册 `IBattleSetupOptionsSource`，所以当前场景仍使用 Inspector `1001 / 5001 / 5`；父来源只是未来 Run 的可替换边界。
- `BattleSession` 仍从 Hero MaxHealth 与 Deck 模板建立运行时事实；结果中的玩家生命只是终局冻结快照，不是第二份可写 HP 事实。没有第二份 seed / deck，也没有改变既有随机盐或引入规则层 `UnityEngine.Random`。
- 没有修改 DataTables、Luban 生成物、Localization、Addressables、Scene、Prefab、ProjectSettings、asmdef、HybridCLR、Run、地图、奖励、存档、升级实例或 HUD/动画布局。
- 工作区原有 `ProjectSettings.asset`、Daedalus 文档、`game-config.json` 等脏改均按基线保留；本切片未执行 stash / reset / clean / checkout，里程碑提交仅按审查包精确暂存。

## B2R-203 · owner open

Pegasus `STATUS.md`、`project-definition.md` 与 `decision-locks.md` 的阶段口径漂移仍待所有者裁决。本轮没有修改 `Docs/Hermes_Pegasus/**`；该 DocumentationDrift 不阻塞 seam 自动门禁，也没有被代码实现暗中裁决。

## 停止点

W1 / W2 代码、自动回归与 Editor 原生串行验收到此完成。G1 仍为 `needs-grill`；后续必须另行 Grill `RunState → BattleSession` 的初始 HP / 牌组输入、`BattleResult → RunState` 写回、Abandoned、奖励、战后过渡与 Run 生命周期，不能从本切片推导其实现授权。
