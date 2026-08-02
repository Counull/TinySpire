---
title: TinySpire BattleScene M8 · 敌人行动、状态时机与完整战斗循环
page_type: plan
lifecycle: archived
created: 2026-08-02
updated: 2026-08-02
scope: BattleScene M8A-M8E
status_source: ../SESSION_LOG.md
source: ../ROADMAP.md M8；../DEPENDENCIES.md DEP-009/DEP-013；../07_retrospective/2026-08-01-m5-architecture-roast.md；../06_testing/2026-08-02-m7e-full-validation-review.md
depends_on: 2026-08-02-m7-effect-executor.md（M7 已完成）
---

# TinySpire BattleScene M8 · 敌人行动、状态时机与完整战斗循环

## 当前结论

本页是 M8 实施期间的**唯一实施计划**，现已归档。M8 在 M4～M7 的 `BattleCommandQueue.Submit`、只读 `Queue`/`Turn`、确定性意图、显式目标、共享 Effect 公式、完整预构建和不可变结算记录上，闭合了敌人真实行动、Block/Vulnerable 时机、多敌顺序、死亡中止、规则层终局与表现屏障。

正式 M8 Goal 的实际起始 HEAD 为 `937b6fe50ec890cb3e71048da13a67c9d6815067`。M8 已按 **M8A → M8B → M8C → M8D → M8E** 串行完成，每个切片均先完成独立验收页与状态同步再继续；最终自动验证、Bootstrap、真实 Game View、范围审计与双轴复审见 `../06_testing/2026-08-02-m8e-full-validation-review.md`。并发出现的 Hermes/Candidates 美术改动始终作为用户范围排除并保护。

## 推荐 Goal 文案

> 完成 TinySpire BattleScene M8 · 敌人行动、状态时机与完整战斗循环。严格以 `Docs/Copilot_Daedalus/plans/2026-08-02-m8-enemy-actions-status-timing-battle-loop.md` 为唯一实施计划，遵守根 `AGENTS.md`、`Docs/Copilot_Daedalus/ARCHITECTURE_CONVENTIONS.md`、现有 `CODE_DECISIONS.md` 以及计划中的状态时机、联合预构建、失败原子性、queue fault、终局、停止点与验证要求，按 M8A → M8B → M8C → M8D → M8E 串行执行，每个切片完成对应验收页和 `SESSION_LOG.md` 同步后再继续。复用 M4～M7 的 `BattleCommandQueue.Submit`、只读 `Queue`/`Turn`、权威序号、玩家轮次栅栏、确定性意图、显式 Self/Enemy 目标、`BattleEffectExecutor`、共享公式和不可变结算记录；Queue 唯一拥有 Queued、非重入 drain、continuation 排序、展示屏障和 fault，Hand/Turn 通过统一 coordinator 在 Submit 前预注册 token/handle。敌人行动必须在首次写入前以初始权威快照联合预构建“Block 清理后的投影事实 → Effect → Vulnerable 衰减 → 下一意图/随机 → continuation”，单次验证后按计划无失败提交；普通失败必须全部权威事实零写入且结算为空。按计划落实死亡 source 跳过、玩家死亡中止剩余敌人、Encounter 稳定顺序、终局后稳定失败，以及当前唯一存活玩家目标；不得私定多玩家目标、为敌人伪造 `CardEffectBinding` 或复制公式。M8 不实现 M3E/M9 的 Block/状态 HUD、数字、抖动、死亡过渡、横幅、胜负面板、奖励、重开、最终动画或 LXX-6 美术；不实现 Weak、Dexterity、遗物、触发器、行为树、通用 DSL、多/随机/链式目标、Exhaust、Run/网络或多人生产装配；不修改 DataTables、生成配置、Localization、Addressables 内容、Scene、Prefab、ProjectSettings、asmdef、HybridCLR、启动流程或 DI 架构，`BattleLifetimeScope` 只允许必要的最小注册。开始前重读规则，执行 `git status --short` 与 `git rev-parse HEAD`，保护已有改动；新增 Meta 优先经当前 Unity MCP 生成，不得启动第二个 Editor、结束用户 Unity/Git 进程、删除未授权锁或清理 Library/Temp。最终完成 M8 定向、M2～M7 回归、全量 EditMode、串行 solution build、Bootstrap、真实 Game View 多回合物理验收、Console、文档同步和 Standards/Spec 双轴复审。本计划不预计 Luban/Addressables 重建；若需要配置/可寻址内容/排除路径，或遇到多玩家目标、敌人多 Effect、公式冲突、工具阻塞、真实证据不足，立即停止并请求确认。未经明确确认不 commit、不 push。

## 执行纪律

1. 开始时完整重读根规则、本计划、架构约定、CD-027～CD-042、DEP-008～DEP-013、M5 架构复盘与 M7E 验收；记录实际 HEAD 和全部 tracked/untracked 既有改动。
2. 测试先行，只测公开 `Submit`、只读事实或新深 module 的最小 interface；不测试私有方法，不为测试公开 `BattleTurnController`。所有新增函数至少有中文注释。
3. 共享战斗写入只经 Queue 排序；UI、runtime、presentation completion、R3 subscriber 不得直写 Turn、Combatant、Intent 或 CardZones。回调内 Submit 可入队但不得重入执行。
4. 普通失败在首次写入前返回：Combatant、Turn、CardZones、Intent Layout/history/random 与 settlement 全部零变化。写入后的意外异常只进入“可能部分写入”的显式 fault 并停止，不伪称回滚。
5. 新 Meta 优先复用当前 Unity MCP；不得启动第二 Editor、结束用户进程、删除未授权锁或清理 `Library`/`Temp`。
6. 本计划不预计配置/可寻址内容变化；一旦需要排除路径，停止请求扩大范围。每切片完成定向/相关回归、串行 build、diff/范围审计和文档停止点后才继续。
7. 未经确认不 commit、不 push；获准提交时只暂存显式路径，禁止 `git add .`。

## 锁定的 M8 MVP 契约

| 主题 | 唯一口径 |
|---|---|
| 敌人目标 | Self = 当前行动敌人；Enemy = 当前生产中的唯一存活玩家。0 名玩家直接终局；多名存活玩家进入 configuration fault，不按首项/最小 ID/随机私选。现有配置基线是 attack 6、defend 5，Behavior 仅一个 `effect_id`；适配为单项强类型 Effect 序列，不改表、不伪造 `CardEffectBinding`。 |
| 玩家状态时机 | 下一 `PlayerRoundStart`：先清该玩家 Block，再恢复能量、抽牌；成功 `EndPlayerActionCommand`：先按现有顺序弃手，再让该玩家 Vulnerable 减 1。 |
| 敌人状态时机 | 存活敌人行动前清自身 Block，Effect 完成后自身 Vulnerable 减 1，再提交下一意图。值为 0 或参与者死亡时不写入、不造记录。 |
| 联合预构建 | 捕获初始权威快照，在临时事实上模拟 Block=0，再以该投影标量预构建 Effect、Vulnerable、下一意图/history/random 与 continuation；首次写入前只执行一次联合 validate。commit 不再按初始快照复验中间状态，也不返回普通失败；禁止清 Block 后调用会自判漂移的现有 `ExecutePrepared`。 |
| 成功敌人事务 | Validate 初始快照 → 清 Block → 提交投影事实上的 ordered Effect → 减 Vulnerable → commit 下一意图/history/random → 返回冻结 settlement 与预定 continuation。致死玩家的动作仍完成一次下一意图 commit，以维持 CD-033，然后只进入终局。 |
| 死亡行动者 | 排队后死亡的 enemy 不解析目标、不清状态、不执行 Effect、不消费 intent/random；成功返回专用 `EnemyActionSkipped(SourceNotAlive)`，只要求 source，不伪造 EffectId/target。 |
| 终局 | 增加中立 `BattleEnded` phase；胜负从存活玩家/敌人派生，不保存可变 outcome 镜像。当前动作完整结算后进入终局；剩余敌人不行动。已有序号命令轮到时返回 `BattleAlreadyEnded`、零写入、空记录；终局后的新提交拒绝且不分配序号。 |
| 系统校验 | `PlayerActionWindowExpired`/submitted-round fence 仍只用于 `PlayCard`、`EndPlayerAction`。Enemy/continuation 以 expected phase、current actor 和一次性 token 重校验，不冒用玩家轮次栅栏。 |
| Queue 生命周期 | Queue 唯一分配序号并发布 accepted command 的 Queued。Coordinator 在 Submit 前预注册 opaque token/handle；Queued 携带同 token+sequence，拒绝时撤销 handle。Queued/pending 必须早于 Failed/Completed/Faulted，View 不保存序号、不手工 `PublishQueued`。 |
| 非重入与 continuation | Queue 有 drain guard 且是唯一 continuation owner。`Execute` 返回后、调用 presentation 前，Queue 把预定 continuation 作为下一条内部命令分配序号、入队并发布 Queued；已在此之前 accepted 的命令保持原序在前，presentation 期间的新提交排在 continuation 后。Enemy/continuation 必须携带 Queue 签发的一次性 token，重复/伪造系统命令不能再次行动；completion 只解除屏障，runtime driver 不再轮询或提交阶段命令。 |
| 展示屏障 | 每条命令至多发布一次可见阶段变化。可见 settlement 等待一次 completion；无可见结果的 system continuation 零等待。当前反馈完成前不切下一敌人/轮次；不再所有命令固定 0.35 秒。 |
| Fault | 阶段/行动者/目标失效/战斗结束等预期问题是普通失败。缺 Behavior/Effect、未知枚举、无下一意图、多玩家无策略、prepared 不变量错误是首次写入前 fault：零写入、空 settlement、保留 fault sequence/reason、冻结 drain、拒绝新提交并保留待处理项供诊断。 |
| Settlement | 新增 `BlockCleared`、`StatusReduced`、`EnergyRefilled`、`EnemyIntentAdvanced`、`EnemyActionSkipped`、`BattlePhaseChanged`；沿用 M7 Damage/Block/CardMoved/Reshuffled/Effect 内 OperationSkipped。Order 连续且等于真实写入顺序；lifecycle/fault metadata 不混入 battle settlement。 |

## 深 module 与边界

- 外部保持 `BattleCommandQueue.Submit`、只读 `Queue`/`Turn`；`BattleTurnController`、状态写入口、enemy transaction 与 intent plan 保持 internal。
- Effect 核心改为“source + explicit target + ordered `BattleEffectId`”；Card/Enemy 在边缘适配。迁移后删除 Card-only 核心，不保留两套 executor/公式/写链。
- `BattleEnemyIntentsData` 使用 `PrepareCompletion → ValidatePreparedCompletion → CommitPreparedCompletion`；prepare 零写入，validate 仅在联合事务首次写入前，commit 不再校验或抛普通失败。
- 具体 `BattleEnemyActionExecutor` 拥有联合 prepare/validate/commit；具体 `BattleStatusTiming` 隐藏清理/衰减；具体 `BattleCommandSubmissionCoordinator` 只管理预注册 token/handle 与反馈对账。Queue 拥有 Queued、continuation 排序、barrier 与 fault。
- 禁止 Queue 的 command 分支继续脚本化查表/Effect/状态/意图/阶段；禁止假 Binding、单实现 public `I*`、全局事件总线、反射、行为树或通用 DSL。

## 明确排除与停止条件

- 排除 M3E/M9 的 Block/状态 HUD、数字、抖动、死亡、抽弃/重洗动画、横幅、胜负/奖励/重开/退出、最终出牌动画及 LXX-6 美术接线。
- 排除 Weak、Dexterity、遗物/触发器、行为树/DSL、多/随机/链式目标、目标重选、命令中途选择、Exhaust、多人生产目标、Run/网络及 M10/G1 债务。
- 不修改 `DataTables/Datas/`、`TinySpire/Assets/Scripts/Core/Generated/`、`TinySpire/Assets/GameData/`、Localization、Addressables 内容、Arts/Scenes/Prefabs、ProjectSettings、asmdef、HybridCLR、启动/Run/网络或 DI 架构；`BattleLifetimeScope` 仅允许最小注册。
- 出现多存活玩家、敌人多 Effect、状态/致死口径冲突、任何排除路径需求、工具阻塞或无法形成真实证据时，停止并报告预计文件、风险、回滚与所需授权。

## M8A · 命令、状态与终局契约

状态：**已完成**

实施：以纯 C# 测试锁定 token/lifecycle、continuation 入队边界、non-reentrancy、fault、target、状态时机、联合快照、source-skipped、terminal 与新增 settlement；只建立被 M8B～D 立即消费的最小类型，不接生产写链，并记录实际新决策。

停止点：生产敌人仍不造成伤害，polling/pending/自动阶段未迁移；M8A 定向、M4～M7 契约回归、串行 build、diff/排除路径审计通过；新增 `06_testing/2026-08-02-m8a-command-status-terminal-contract.md`，同步测试索引、决策、计划状态和 `SESSION_LOG.md` 后才进入 M8B。

## M8B · 统一提交、Queue 生命周期与阶段屏障

状态：**已完成**

实施：Coordinator 预注册 token/handle，Queue 发布一次 Queued；迁移 Hand/Turn 并删除 View sequence/手工 Queued。落实 drain guard、`Execute` 后/`Present` 前的 continuation 入队点、一次性 system token、fault、terminal admission、每命令一次 phase 与按结果 barrier；移除 runtime `ITickable` 轮询和 continuation 提交。敌人仍保留 M5 占位，不接真实 Effect/状态。

停止点：三类命令生命周期、拒绝撤销 handle、旧反馈不清新 pending、callback Submit 不重入、旧 completion 无效、重复 system token 失败、fault 冻结、既有 accepted 命令/continuation/展示期间新提交的顺序、零表现直通全部经公开 seam 通过；定向/相关回归、串行 build、Bootstrap 基础实跑、真实 Game View 输入/pending 回归、diff/范围审计通过；新增 `06_testing/2026-08-02-m8b-command-lifecycle-presentation-barrier.md` 并同步后才进入 M8C。

## M8C · 敌人意图与 Effect 联合事务 module

状态：**已完成**

实施：把 Effect 核心迁到 ordered `BattleEffectId`，Card 在边缘保持 M7 顺序；建立 intent 三段式 plan 与 enemy 联合事务，在 Block 清理后的投影事实上 prepare Effect，并以一次初始快照 validate 后无失败 commit。**本切片只交付纯 module/fixture，不注册到 Queue、LifetimeScope 或生产循环。**

停止点：attack 共用 Strength/Vulnerable/Block/致死公式；已有 Block 的 attack 与 Self defend 不发生 snapshot 漂移，Defend 清理后最终 Block 恰为 5；intent prepare 零写入、validate 只在首次写入前、commit 恰好一次；配置/目标/随机/快照错误零写入，死亡 source 用专用记录。Card 四张牌、Effect/Intent/Queue 相关回归和串行 build 通过；不要求 Bootstrap/真实 Game View，生产敌人仍不造成伤害；新增 `06_testing/2026-08-02-m8c-enemy-effect-transaction.md` 并同步后才进入 M8D。

## M8D · 生产接线、状态时机、死亡与多回合

状态：**已完成**

实施：首次把 enemy transaction 接到 Queue/生产链；接入玩家 RoundStart、EndPlayerAction、敌人状态时机、双敌 Encounter continuation、死亡 skip/中止和 `BattleEnded`。保证 Draw/Discard/Reshuffle/Energy/Block/Vulnerable/Intent/Damage/Phase 记录有序，同种子多轮可重放。

停止点：单敌 fixture 与当前双敌生产均每轮按顺序各行动一次，反馈前不切换；0 值/死亡无伪状态记录；玩家死亡无剩余敌人行动，最后敌人死亡无需再结束行动，终局命令稳定失败；M8D 定向、M2～M7 回归、串行 build、Bootstrap 多轮、真实 Game View 物理顺序/状态/死亡/终局、Console 与范围审计通过；新增 `06_testing/2026-08-02-m8d-status-death-battle-loop.md` 并同步后才进入 M8E。

## M8E · 全量验证与双轴收口

状态：**已完成**

实施：运行 M8 定向、M2～M7 相关与全量 EditMode（0 failed/0 skipped），串行 `dotnet build TinySpire/TinySpire.sln --no-restore -m:1`；从 Bootstrap 生产链完成真实 Game View 双敌多轮、attack/defend、状态时机、反馈屏障、死亡/终局和 Console 清洁验收；审计所有排除路径；以 Goal 实际起始 HEAD 对完整 tracked/untracked diff 并行做 Standards/Spec 复审，修复后重跑并复审。

停止点：证据记录任务 ID/数量/build warning 新增性/真实指针结果；同步 `06_testing/2026-08-02-m8e-full-validation-review.md`、测试/计划索引、`SESSION_LOG.md`、`CODE_DECISIONS.md`、`DEPENDENCIES.md`、`ROADMAP.md`，按证据决定 DEP-009/013 状态并归档本计划；展示 review package，未经确认不 commit、不 push。

## 必跑测试与完成定义

- 定向覆盖 Queue/Presentation/Turn/Intent/Effect/CardRules/CardZones/Combatants/Hand/Turn HUD；明确测试 lifecycle 顺序、联合原子性、投影 Block、状态边界、稳定随机、多敌顺序、死亡中止和终局。
- M8 完成时：唯一 coordinator/Queue owner 成立；敌人 attack/defend 共用 M7 Effect；Block/Vulnerable 按本计划；多敌、死亡和终局闭环；反馈完成前不推进；自动与真实生产证据、文档、依赖和双轴复审齐全。
- DEP-009 仅在敌人真实 Effect/死亡闭环完成后 resolved；DEP-013 仅在状态时机/记录/多轮证据完成后 resolved。DEP-008/010/011/012 与 DEP-003/004 继续 open 并分别留给多人/网络/选择、Exhaust 与 M9。
