---
title: M4A 权威命令队列与回合事实骨架验收
page_type: testing
lifecycle: active
date: 2026-08-01
scope: TinySpire M4A 纯 C# 战斗命令队列与回合事实
source: ../plans/2026-07-31-m4-turn-scheduling-energy.md
status_source: ../SESSION_LOG.md
---

# M4A 权威命令队列与回合事实骨架验收

## 验收结论

M4A 已满足独立停止点：所有命令通过 `BattleCommandQueue.Submit` 进入本地权威顺序；`Queue` 与 `Turn` 以只读 R3 快照公开当前命令、pending、表现等待和按 `CombatantId` 保存的玩家事实。当前命令执行或等待表现时仍可接受后续提交，但只有队首命令写共享事实，且只有绑定当前权威序号的表现完成回调能推进下一条。

本结论只覆盖 M4A 纯 C# 骨架，不表示 M4 整体完成，也不表示当前 BattleScene 已接入命令队列。

## TDD 证据

测试只跨用户确认的公共 seam：`Submit`、`Queue`、`Turn`。表现测试 adapter 只扮演外部完成边界，没有调用队列内部推进方法或断言私有容器。

| Red | 失败证据 | Green 后锁定的行为 |
|---|---|---|
| 1 | Unity 编译报 `IBattleCommandPresentation` / `BattleCommandExecutionResult` 缺失 | 未开始时玩家命令结构性拒绝且不占序号 |
| 2 | Unity 编译报 `StartBattleCommand` / `PlayerAction` 缺失 | 开始命令取得序号 1，完成权威写入后等待表现 |
| 3 | 等待表现时提交抛出 `InvalidOperationException` | 两名玩家继续提交并取得序号 2/3，不提前改事实 |
| 4 | 完成当前表现后展示结果仍为 1 条 | 每次完成只按序启动下一条，pending 正确递减 |
| 5 | 执行结果缺少明确失败原因 | 重复开始出队后返回 `BattleAlreadyStarted`，不重新初始化 |
| 6 | Unity 编译报 `PlayCardCommand` / `CompleteEnemyActionCommand` 缺失 | 首批四类命令完整；M4B/M4C 命令在 M4A 明确失败且不改事实 |
| 7 | 执行期重入提交时错误显示正在等待表现 | 执行中和等待表现成为可区分的权威事实 |
| 8 | 重复调用旧表现回调会跳到序号 3 | 过期回调不越过当前队首 |

## 自动验证结果

| 检查 | 结果 |
|---|---|
| Unity MCP Meta 生成与脚本刷新 | 通过；新增目录、运行时代码和两份测试均有 `.meta` |
| M4A 定向 EditMode | **9/9 通过** |
| Unity Console Error | **0** |
| `dotnet build Assembly-CSharp.csproj --no-restore` | **0 error**；6 条既有依赖版本冲突 warning |
| `dotnet build Assembly-CSharp-Editor.csproj --no-restore` | **0 error**；12 条既有依赖版本冲突 warning |
| `git diff --check` | 通过 |

定向用例覆盖：

- 战斗开始前拒绝玩家命令，拒绝不消耗权威序号。
- `StartBattleCommand` 只成功初始化一次；重复命令提交可接受，但出队时重新校验并失败。
- 当前命令执行期间及等待表现期间，两名玩家均可继续提交。
- 权威序号单调递增，当前命令与 pending 数量稳定，表现严格按序号逐条开始。
- 重复的旧表现完成信号不会重复推进。
- `PlayCardCommand`、`EndPlayerActionCommand`、`CompleteEnemyActionCommand` 在 M4A 不扣能量、不移动卡牌、不设置结束状态、不推进敌人阶段。
- 两名玩家拥有不同的 `PlayerTurnData`，只通过 `CombatantId` 映射访问；`BattleTurnData` 不含 `CurrentPlayer` 或全局 `CurrentEnergy`。

## 明确未验证与未实施

- 未运行全量 EditMode 或任何 PlayMode / 场景实跑；这些属于 M4E 或生产接线后的相称验证。
- 未修改或验证场景、Prefab、ProjectSettings、asmdef、配置表、现有 UI、Luban 输出或 Addressables 内容。
- 未把队列注册到 `BattleLifetimeScope`，未迁移 `BattleSession` 的现有初始抽牌。
- 未实现 M4B 的能量扣除、卡牌执行期校验与卡区移动。
- 未实现 M4C 的结束玩家行动、敌人行动交接或完整轮次闭环。
- 未实施 M4D～M4E。
