---
title: M8A 命令、状态与终局契约
page_type: testing
lifecycle: active
date: 2026-08-02
plan: ../plans/2026-08-02-m8-enemy-actions-status-timing-battle-loop.md
status_source: ../SESSION_LOG.md
---

# M8A 命令、状态与终局契约

## 验收范围

- 新增预提交不透明 `BattleCommandHandle`、统一 `BattleCommandSubmissionCoordinator` 与 internal `BattleCommandSchedulingCore` 契约。纯核心锁定唯一 Queued、权威序号、迭代 drain、continuation FIFO、按非空结算自动建立的表现屏障、精确 completion、一次性 Queue system token 与冻结 fault；尚未接现有生产 Queue。
- 生命周期明确区分 Queued、普通执行失败、成功与 fault。普通失败携带冻结空结算；确定性 fault 不允许声明部分写入，只有提交后不可预期异常可显式标记 `MayHavePartialWrites=true`。Queue fault 是独立只读事实，不伪装成 battle settlement。
- 新增 `BlockCleared`、`StatusReduced`、`EnergyRefilled`、`EnemyIntentAdvanced`、`EnemyActionSkipped` 与 `BattlePhaseChanged` 六类不可变结算，以及中立 `BattleEnded` phase；Turn 不保存 victory/defeat 镜像。
- 敌人目标规则固定为：死亡 source 优先成功跳过且只记录 Source；活 source 先检查存活玩家数量，零名进入 terminal、多名进入 configuration fault，恰好一名时 Self 指向 source、Enemy 指向该玩家。
- internal `BattleStatusTiming` 锁定四个命名时点和现有标量上的投影：玩家/敌人行动前清 Block，玩家/敌人行动后各把 Vulnerable 减 1；死亡或零值不写、不造记录。玩家时机口径记录为 Block → Energy → Draw 与 Discard → Vulnerable。
- 联合初始快照真实冻结 source/target 四标量、完整 Turn、当前 Intent Layout、目标敌人完整 history、随机状态、恰好一个 ordered `BattleEffectId` 与 `CompleteEnemyAction` continuation。guard 只允许一次联合 validate、一次 commit，commit 不接收当前事实因而不复验中间写入。

## TDD 证据

1. 首轮 settlement 契约先以六个缺失类型形成编译红灯；补齐 sealed/getter-only 记录和中立 phase 后转绿，并补充字段、nullable 关联和 EnemyAction → EnemyAction 行动者变化语义。
2. scheduling 契约先以缺失 handle/lifecycle/core/fault API 形成编译红灯；最小实现后继续以 Queue fault 只读事实、伪造 system command、句柄错配、ExecutionFailed、跨 owner/重复 token、post-write presentation fault 等用例逐项收紧。
3. target/terminal 与状态时机分别先以缺失具体 module 形成编译红灯；随后补充 Self 在零/多玩家下的优先级、死亡 source-only skip、零值/死亡状态、Block=0 投影和 Vulnerable-1。
4. 联合快照审计指出旧测试只做手工标量投影；新增真实 intent Layout/history/random capture 与 joint guard 后，source、target、Turn、Layout/history、random 任一漂移都令唯一 validate 失败，成功 validate 后的中间事实变化不触发第二次校验。
5. 首轮只读复审发现 public 状态写 seam、system token 可绕过、Self 在玩家死亡后继续行动、表现屏障 bool 可绕过、联合快照与 post-write fault 缺口；全部修正后最终 M8A 定向任务 `d0ba59205b67451c97a895f99afb6a28` 为 **58/58 通过**。

覆盖行为包括：

- 外部伪造 `CompleteEnemyActionCommand`、错配 handle、Queue fault 后新提交均稳定拒绝且不分配序号；拒绝撤销预注册 handle。
- 执行期间已接受命令、Execute 返回的 continuation、表现期间新提交保持 FIFO；同步 completion 不重入，system token 只由所属核心消费一次。
- 非空结算无法绕过表现屏障；提交后表现异常冻结 current/pending，并显式标记可能部分写入。
- 状态普通失败返回冻结空结算且参与者标量零写；完整 enemy transaction 的普通失败原子性继续由 M8C 真实 executor 覆盖。
- 玩家状态相对 Energy/Draw/Discard 的 M8A 用例是纯 settlement 顺序口径，不冒充生产接线；M8D 必须用公开 Queue 的真实结算顺序测试替代该手工组合契约。

## 回归与静态编译

| 检查 | 结果 |
|---|---|
| M8A 定向 EditMode | **58/58 通过**，0 failed、0 skipped；任务 `d0ba59205b67451c97a895f99afb6a28` |
| M4～M7 契约回归 | **145/145 通过**，0 failed、0 skipped；任务 `940eaf0766564474b95e04800ab257cd` |
| Unity 脚本刷新与 Meta | 当前唯一 Unity 6000.5.5f1 Editor 完成；新增 C# Meta 均由该 Editor 生成，最终 Console Error 0 |
| `dotnet build TinySpire/TinySpire.sln --no-restore -m:1` | **0 error**；12 条既有 Unity/R3/UniTask 依赖版本冲突 warning，无新增 warning 类别 |
| diff / 排除路径审计 | tracked 与全部 untracked 路径逐项检查，`git diff --check` 及新增 authored C# 尾随空白检查通过 |
| 只读复审 | Standards **0 Hard**；Spec **0 Hard / 1 Judgement**，唯一 judgement 为玩家状态相对阶段操作尚未经过真实生产 Queue，按计划强制留给 M8D |

## 停止点结论

- 实际起始 HEAD 为 `937b6fe50ec890cb3e71048da13a67c9d6815067`，开始时工作区干净。实施期间出现的 `TinySpire/Assets/Arts/Runtime/UI/Battle/Candidates*` 是用户并发未跟踪美术，始终排除并未触碰。
- 现有 `BattleCommandQueue`、View 手工 Queued/sequence、pending、runtime `ITickable` polling 与自动阶段路径均未迁移；生产敌人仍只推进旧占位意图/回合，不造成伤害。
- `AssemblyInfo.cs` 的 Editor friend access 只服务 M8 internal contract tests，不是生产 seam；M8B～D 应优先把测试迁回公开 Queue/executor seam，M8E 必须复审并在不再需要时删除。
- 未修改 DataTables、生成配置、GameData、Localization、Addressables 内容、Arts（并发 Candidates 除外且未触碰）、Scene、Prefab、ProjectSettings、asmdef、HybridCLR、启动/Run/网络、DI 或 `BattleLifetimeScope`；无需 Luban/Addressables 重建。
- M8A 独立停止点完成；下一步严格进入 M8B 统一提交、Queue 生命周期与阶段屏障。
