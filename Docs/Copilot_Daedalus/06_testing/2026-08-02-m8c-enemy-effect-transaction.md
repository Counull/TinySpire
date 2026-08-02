---
title: M8C 敌人意图与 Effect 联合事务
page_type: testing
lifecycle: active
date: 2026-08-02
plan: ../plans/2026-08-02-m8-enemy-actions-status-timing-battle-loop.md
status_source: ../SESSION_LOG.md
---

# M8C 敌人意图与 Effect 联合事务

## 验收范围

- `BattleEffectExecutionRequest` 的核心输入已从 Card-only binding 收敛为 source、显式 target 与 ordered `BattleEffectId`。Card 只在 `BattleTurnController` 边缘校验并保持 binding 原顺序；敌人事务不构造 `CardEffectBinding`，公式和唯一状态写入口仍复用 M7 module。
- `BattleEnemyIntentsData` 已提供 `PrepareCompletion → ValidatePreparedCompletion → CommitPreparedCompletion`。Prepare 复制 history，并以恢复到同一权威状态的本地 `GameRandom` 预演下一意图；固定单候选不推进随机流，真实 history/random/Layout 只在已验证 commit 中按顺序发布一次。
- internal concrete `BattleEnemyActionExecutor` 从一份初始 source/target/Turn/Intent/history/random 快照联合预构建：行动开始清 Block 的投影 → ordered Effect → Effect 后 source 的 Vulnerable 衰减 → 下一意图/random → 调用方 continuation 副本。首次写入前只验证一次，成功提交固定为 Block → Effect → Vulnerable → Intent，期间不复验本事务自己的中间写入。
- Self defend 从 Block=0 的投影执行，因此旧 Block 8 最终精确为 5；attack 完全复用 Strength、Vulnerable、Block 与致死公式。状态时机 commit 只写当前时点拥有的一个标量，不会用旧快照覆盖 Self Effect 的新 Block。
- 死亡 source 在 Behavior、目标、Effect 与 Intent 读取前返回 source-only `EnemyActionSkipped(SourceNotAlive)`，即使存在多玩家或损坏配置也不执行状态时机或推进意图。活 source 只支持当前唯一存活玩家；零玩家返回 `BattleEnded`，多玩家、缺 Behavior/Effect、未知枚举、无下一意图、序号溢出与 prepared 漂移均在首次写入前结构化 fault、空结算且零权威写入。

## 自动验证

| 检查 | 结果 |
|---|---|
| M8C 敌人联合事务定向 | **25/25 通过**，0 failed、0 skipped；任务 `93fb4cb0fd384ea6a4acec931616ae27` |
| Effect / Intent / Card / Queue 相关回归 | **200/200 通过**，0 failed、0 skipped；任务 `9ee5346a6ecd4ea08712d01af8a9aa5b` |
| Card 边缘、Intent 三段式与联合事务聚焦回归 | **52/52 通过**，0 failed、0 skipped；任务 `f664c9a9cbf34152afb5cbf7c57f6303` |
| `dotnet build TinySpire/TinySpire.sln --no-restore -m:1` | **0 error**；12 条既有 Unity/R3/UniTask 依赖版本冲突 warning，无新增 warning 类别 |
| 静态接口审计 | Effect 核心和 enemy executor 均为 0 个 `CardEffectBinding`；Card adapter 只存在于 `BattleTurnController`；enemy executor 不含 Strength/Vulnerable/Block/Damage 算术副本 |
| diff / 排除路径审计 | `git diff --check` 通过；M8C 未注册 Queue/LifetimeScope，未修改 DataTables、生成内容、Localization、Addressables、Scene、Prefab、ProjectSettings、asmdef、HybridCLR、启动流程或 DI 架构 |

定向测试还覆盖：初始权威事实全部零写入、固定/加权 RNG、一次性 validate/commit、source/target/Turn/intent 漂移、配置与目标 fault、普通阶段/行动者失败、结算序号容量、Card null/零/负 Effect binding 原子失败、死亡 source 保留既有非零 Block/Vulnerable，以及 intent 结算在写入前完整构造。

## 停止点结论

- M8C 已交付纯 C# 联合事务 module/fixture；`BattleCommandQueue`、`BattleLifetimeScope` 和生产敌人占位链没有接入 `BattleEnemyActionExecutor`，因此生产敌人此时仍不造成伤害，符合切片边界。
- 本切片按计划不要求 Bootstrap 或真实 Game View；真实双敌多轮、状态时机、死亡中止、终局和表现屏障物理验收严格留给 M8D。
- 实际 M8 起始 HEAD 仍为 `937b6fe50ec890cb3e71048da13a67c9d6815067`。并发的 `Docs/Hermes_Pegasus/art/**` 与 `TinySpire/Assets/Arts/Runtime/UI/Battle/Candidates*` 始终作为用户改动排除并未触碰。
- 无需 Luban 或 Addressables 重建。完成本页、测试索引、CD-046、计划状态与 `SESSION_LOG.md` 同步后，下一步严格进入 M8D 生产接线。
