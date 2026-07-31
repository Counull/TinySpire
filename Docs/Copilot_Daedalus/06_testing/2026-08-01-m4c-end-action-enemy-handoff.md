---
title: M4C 队列化结束行动与敌人顺序交接验收
page_type: testing
lifecycle: active
date: 2026-08-01
scope: TinySpire M4C 完整轮次闭环、初始抽牌迁移与生产生命周期接线
source: ../plans/2026-07-31-m4-turn-scheduling-energy.md
status_source: ../SESSION_LOG.md
---

# M4C 队列化结束行动与敌人顺序交接验收

## 验收结论

M4C 已满足独立停止点：玩家结束行动、全体存活玩家门槛、敌人 Encounter 顺序、死亡跳过、系统完成命令和下一轮重置都只经 `BattleCommandQueue` 权威队首写入。`BattleSession` 不再提前发首轮手牌，首轮和后续轮次统一由 `PlayerRoundStart` 重置能量/结束标记并抽到目标手牌数。

生产 `BattleLifetimeScope` 已注册队列、即时表现 adapter 和启动/逐帧驱动；当前无行为敌人每帧最多经同一 `Submit` seam 完成一个。当前生产 Session 仍只有唯一玩家卡区，完整多人装配继续由 `DEP-008` 跟踪；拖牌、结束按钮及能量/回合显示仍属于 M4D。

## 行为证据

除生产驱动用例外，测试只通过公共 `BattleCommandQueue.Submit`、`Queue` 与 `Turn` seam 观察结果；可控制表现 adapter 只决定结果何时回报完成，没有调用内部阶段推进方法。

| 验收项 | 结果 |
|---|---|
| 分别结束行动 | 第一名玩家结束后只弃置自己的剩余手牌并保持 `PlayerAction`；第二名玩家随后排队的出牌正常执行 |
| 重复结束 | 第一条命令只弃牌一次；第二条在队首返回 `PlayerActionAlreadyEnded`，弃牌数量、阶段与另一玩家事实不变 |
| 全体完成门槛 | 第二名玩家结束后才进入 `EnemyAction`；排在结束命令后的旧玩家出牌返回 `InvalidTurnPhase` |
| Encounter 顺序 | 当前敌人严格来自显式 `EnemyCombatantIdsInEncounterOrder`，不读取参与者字典枚举顺序 |
| 死亡与错误完成 | 死亡敌人跳过；非当前敌人和已经完成敌人的重复命令返回 `EnemyNotCurrentActor`，不越过当前阶段 |
| 下一轮 | 最后一名敌人完成后轮次 1 → 2，当前敌人清空，每玩家能量恢复 3、结束标记恢复 false，并重新抽到目标手牌数 |
| 初始发牌唯一入口 | `BattleSession.FromConfig` 只留下 10 张洗牌后抽牌堆和空手牌；`StartBattleCommand` 进入首个 `PlayerRoundStart` 后才抽 5 张 |
| 每帧系统完成 | 两名无行为敌人需要两次 `BattleCommandRuntimeDriver.Tick` 才全部完成；第三次玩家阶段 Tick 不重复推进轮次 |

## 自动验证结果

| 检查 | 结果 |
|---|---|
| Unity MCP 相关 EditMode | **27/27 通过**，0 failed、0 skipped；耗时 `0.9079768s` |
| Unity Console Error | 脚本刷新后 **0**；Bootstrap → BattleScene 实跑后 **0** |
| Bootstrap 生产事实 | `phase=PlayerAction; round=1; energy=3; ended=False; hand=5; queueIdle=True` |
| Bootstrap 加载日志 | `game-config.json 已加载。`；未出现 `InvalidKey`、VContainer 或资源地址错误 |
| `dotnet build Assembly-CSharp.csproj --no-restore --verbosity:minimal` | **0 error**；6 条既有依赖版本冲突 warning |
| `dotnet build Assembly-CSharp-Editor.csproj --no-restore --verbosity:minimal` | **0 error**；12 条既有依赖版本冲突 warning |
| `git diff --check` | 退出码 0；仅提示 `GameConfig.cs` 后续 Git 写入时的 CRLF/LF 归一化 warning |

## Unity MCP 补充

- 使用当前唯一的 `TinySpire@8edf130c865b3957` Editor，没有启动或结束其他 Unity 进程。
- 脚本刷新和 Test Runner 等待期间 MCP 插件会话曾在域重载边界断开；重新固定同一实例后继续读取原测试任务，最终任务状态为 `succeeded`、27/27 通过。
- Bootstrap 实跑从当前 `BootstrapScene` 进入 `BattleScene`，再通过场景中的 `BattleLifetimeScope.Container` 解析生产 `BattleCommandQueue` 与 `BattleSession`，读取上述首轮事实后正常退出 Play Mode。

## 明确未验证与未实施

- 本切片没有修改 `DataTables/Datas`、表定义、生成 JSON 或手写 `game-config.json`，因此未运行 Luban；没有改 Addressables 可寻址内容，因此未重复构建本地 Addressables。M4B 对能量配置的本地内容构建证据继续有效。
- 未修改场景、Prefab、ProjectSettings、asmdef、HybridCLR 或现有 `HandCardContainer`。
- 当前试玩中队列会自动启动并持有第 1 轮、3 能量和 5 张手牌，但拖牌仍未改为提交 `PlayCardCommand`，也没有 M4D 的结束按钮、能量/回合 View 或执行失败反馈。
- 未实现敌人意图/行为、真实卡牌 Effect、目标选择、伤害、格挡、状态或专属动画。
