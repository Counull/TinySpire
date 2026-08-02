---
title: M8E 全量验证、真实 Game View、双轴复审与文档收口
page_type: testing
lifecycle: active
date: 2026-08-02
status: passed
scope: M8A～M8D 全量回归、Bootstrap、真实 Game View、范围审计与 Standards / Spec 复审
plan: ../plans/2026-08-02-m8-enemy-actions-status-timing-battle-loop.md
status_source: ../SESSION_LOG.md
---

# M8E 全量验证、真实 Game View、双轴复审与文档收口

## 当前结论

M8E 已通过，M8 完成。生产敌人已经通过 `BattleCommandQueue.Submit` 排序的系统命令执行 M7 同一套 ordered Effect、显式 Self/Enemy 目标、公式与不可变结算；玩家/敌人 Block、Vulnerable、稳定 Encounter 顺序、死亡跳过/中止、表现屏障、queue fault 与 `BattleEnded` 形成完整多回合闭环。

## EditMode、回归与静态构建

| 检查 | 结果 |
|---|---|
| M8 Queue/状态/终局/联合事务定向 EditMode | **84/84 passed，0 failed，0 skipped**；任务 `3a5af905f4b1434ea4397c2f78a4555a` |
| M2～M7 相关回归 | **200/200 passed，0 failed，0 skipped**；任务 `6bc09fcecf4f48e89b93d6fba205dbf4` |
| Standards 修正后的 Intent/Enemy/Terminal 聚焦回归 | **86/86 passed，0 failed，0 skipped**；任务 `4d51ecf7ceba4a9ebcb69e2d0cca3879` |
| 最终全量 EditMode | **285/285 passed，0 failed，0 skipped**；最终任务 `63967ec19cf64333921c72ea27293f67` |
| `dotnet build TinySpire/TinySpire.sln --no-restore -m:1` | **0 error，12 warnings**；warning 均为既有 Unity/R3/UniTask 依赖程序集版本冲突 |
| `git diff --check` | 最终文档收口后通过；未跟踪 authored C#/Markdown 与 Unity Meta 亦经范围和格式审计 |

M8 定向与 M2～M7 相关集合在 Standards 修正前已分别独立通过；收窄 public seam 与合并联合 guard 后先跑 86 项聚焦回归，再以最终 285 项全量重新覆盖全部集合。串行 build 在最终代码与文档状态上执行。

## Bootstrap 与真实 Game View

### 干净生产实跑

1. 复用唯一 Unity 6000.5.5f1 Editor，从 `Assets/Scenes/BootstrapScene.unity` 进入 Play Mode，并经生产启动链进入 `BattleScene`。
2. 初始生产事实为 `PlayerAction / Round 1`、玩家 `30 HP / 0 Block / 3 Energy`、两名敌人各 `20 HP`、Queue `pending=0 / waiting=false / fault=false`。
3. Standards 代码修正后又由 Unity MCP 调用生产 `EndActionButton` 的既有 listener 做短 sanity（不冒充物理鼠标）：最终进入 `PlayerAction / Round 2`，玩家 `24 HP`、次敌 Block 5、Queue `pending=0 / waiting=false / fault=false`；截图 `m8e_bootstrap_round2_sanity.png`。
4. 多轮物理路线、独立玩家致死路线和一次性屏障探针均只修改 Play Mode 内存；验收后正常退出 Play Mode。
5. 最终 Console 的 Error/Warning 为 **0/0**；没有启动第二个 Editor、结束用户 Unity/Git 进程、删除锁或清理 Library/Temp。

### 真实系统指针多回合胜利路线

| 场景 | 可观察结果 |
|---|---|
| Round 1 玩家出牌并结束行动 | 两张 Defend 与一张 Strike 后玩家 Block 10、首敌 `20 → 14`；敌人阶段结束进入 Round 2 |
| 玩家 Block 时机 | Round 2 开始时残余 Block 4 先清为 0，再恢复能量与抽牌；随后新 Defend 得到 Block 5 |
| 敌人 Block 时机 | 次敌先前 Defend 得到 Block 5；其下一次 attack 前旧 Block 清为 0，没有与新行动叠加 |
| Encounter 死亡跳过 | Round 3 玩家行动期先击杀首敌；结束行动后 Encounter 从当前存活事实选择次敌，首敌没有获得敌人行动命令，次敌只行动一次 |
| Vulnerable 与稳定顺序 | Bash 使次敌 `20 → 12`、Vulnerable `0 → 2`；其行动后衰减 `2 → 1`，下一轮易伤 Strike 造成 9 点 |
| 最后敌人死亡 | 次敌 `3 → 0` 的同一出牌命令立即进入 `BattleEnded`；无需再次结束行动，Queue 空闲且无 fault |
| 终局稳定失败 | 终局后真实 End 点击不分配新序号，不改变玩家、敌人、Turn、Intent、RNG 或卡区事实 |

关键截图保存在忽略目录 `TinySpire/Temp/CodexEvidence/`：`m8d_victory_round3_status_before_enemy.png`、`m8d_victory_terminal.png`。

真正的“敌人命令排队后 source 才死亡”由 `BattleCommandQueueM8DTests.EnemyAction_SourceDiesAfterQueued_SkipsSourceOnlyThenContinues` 证明：测试在 End 已提交、首名敌人仍被表现屏障阻塞时杀死后继 source；该 enemy transaction 的 action-specific 记录仅为 `EnemyActionSkipped(SourceNotAlive)`，Queue 随 Encounter 交接再追加 `BattlePhaseChanged`。目标、状态、Effect、Intent/RNG 均不读取或推进，物理路线不承担这条更窄的证据。

### 玩家死亡中止与表现屏障

- 独立路线让玩家以 5 HP 进入 Round 4；首名敌人 attack 将玩家 `5 → 0` 后立即 `BattleEnded`。剩余敌人保持 `20 HP / 0 Block / 0 Vulnerable / Behavior 7003`，Intent RNG 在行动前后均为 `853394020`，证明剩余敌人未行动。
- 一次性只读探针只在首个 `CompleteEnemyAction` 建立等待时暂停 Editor：Queue 为 `current=3 / pending=1 / waiting=true / fault=false`，首敌伤害已原子提交使玩家 `30 → 24`，但次敌仍为 `20 HP / 0 Block / Behavior 7003`。恢复后才进入 `PlayerAction / Round 2`，次敌随后完成 Defend 得到 Block 5。
- 对应截图为 `m8d_defeat_first_enemy_stops_remaining.png` 与 `m8d_enemy_feedback_barrier_paused.png`。探针只读 Queue/Turn/Combatant/Intent 事实，completion 只解除屏障，没有写入任何权威战斗事实。

## Standards / Spec 双轴复审

固定复审点为 Goal 实际起始 HEAD `937b6fe50ec890cb3e71048da13a67c9d6815067`。两轴均读取完整 tracked diff 与全部 M8 未跟踪 C#/Meta/测试/文档，并明确排除用户并发的 Hermes/Candidates 美术。

### Standards 首轮与修正

- **Hard · 已修正**：旧 public `CompleteAndSelectNext` 可直接提交 Intent/history/random/Layout。现已收窄为 internal，生产公开命令 seam 仍只有 Queue `Submit`。
- **Judgement · 已修正**：敌人目标解析与终局规则只供 internal Queue/Turn/enemy transaction 使用，已把 resolver/result/enum 与 terminal rules/outcome 全部收窄为 internal。
- **Judgement · 已修正**：`BattlePreparedEnemyActionPlan` 与联合 guard 重复保存 validation/commit 状态。现由 `BattleEnemyActionJointCommitGuard` 唯一消费一次完整 component validation 与一次 commit，删除锁步状态。
- `AssemblyInfo.cs` 的 `InternalsVisibleTo("Assembly-CSharp-Editor")` 保留：scheduling、status、intent plan、joint snapshot 与 enemy executor 的深 module 契约仍由 Editor 测试直接验证；它不扩大生产程序集 public API。

最终 Standards 复核为 **0 Hard / 0 Judgement finding**，首轮 **1 Hard / 2 Judgement** 均已关闭，未引入新 finding。

### Spec 首轮与收口

首轮唯一 Hard finding 是本页、索引、依赖、路线图与计划归档当时尚未完成，属于 M8E 正在执行的明确收口项，不是生产行为缺陷。生产规格缺失、行为错误与 scope creep 均为 0；联合快照、投影 Block、单次 validate、失败原子性、fault partial、死亡规则、唯一玩家目标、状态时机、FIFO/token/barrier、终局和 settlement 顺序均未发现偏离。

最终文档同步后 Spec 复核为 **0 Hard / 0 Judgement finding**；首轮 **1 Hard** 已关闭，未引入新 finding。

## 范围与工作区保护

- M8 没有修改 `DataTables/Datas/`、生成配置、`TinySpire/Assets/GameData/`、Localization、Addressables 内容、Scene、Prefab、ProjectSettings、asmdef、HybridCLR、启动流程或 DI 架构，因此无需 Luban 或 Addressables 重建。
- `BattleLifetimeScope` 只新增统一 submission coordinator 的必要 singleton，并让 Queue 复用它；runtime driver 从轮询收敛为一次 Start 提交，没有 M8D 额外注册。
- 没有实现 M3E/M9 的 Block/状态 HUD、数字、抖动、死亡过渡、横幅、胜负面板、奖励、重开、最终动画或 LXX-6 美术，也没有实现 Weak、Dexterity、遗物、触发器、DSL、多/随机/链式目标、Exhaust、Run/网络或多人生产装配。
- 用户并发改动 `Docs/Hermes_Pegasus/art/asset-index.md`、`Docs/Hermes_Pegasus/art/assets/art-style/scenes/candidates/**` 与 `TinySpire/Assets/Arts/Runtime/UI/Battle/Candidates*` 未修改、未审查、未暂存或回退。
- 未 commit、未 push，也未清理、覆盖或还原任何已有改动。

## 最终结论与后续

M8A～M8E 已按独立停止点串行完成。`DEP-009` 与 `DEP-013` 已由真实 enemy Effect/死亡闭环、状态记录和多轮物理证据关闭；`DEP-003/004/007/008/010/011/012` 保持 open。M3E/M9 继续承接 HUD、死亡/胜负表现、最终动画与重开，M8 不提前实现。
