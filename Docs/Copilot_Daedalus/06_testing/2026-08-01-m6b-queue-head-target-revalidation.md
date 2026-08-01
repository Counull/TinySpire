---
title: M6B 队首目标重校验与权威写链
page_type: testing
lifecycle: active
date: 2026-08-01
plan: ../plans/2026-08-01-m6-card-play-legality-target-selection.md
status_source: ../SESSION_LOG.md
---

# M6B 队首目标重校验与权威写链

## 验收范围

- `BattleTurnController.TryPlayCard` 在首次写入前复用 M6A `BattleCardPlayRules`，不建立第二套 validator 或执行链。
- 目标在排队或展示等待期间失效时，以队首当前事实失败，且 Turn、卡区、生命对象和值均零额外写入。
- 合法 Self/Enemy 仍只扣一次能量、只移动一次指定实例；Enemy 不执行 Effect，生命保持不变。
- 队列测试卡可显式配置 `TargetRule`；测试命令全部显式携带目标，生产 UI 迁移保留给 M6C。
- 保持轮次栅栏和 `BattleCommandQueue.Submit`、只读 `Queue` / `Turn` public seam 不变。

## TDD 证据

1. 在控制器尚未接入规则时，`QueuedPlayCard_WhenEnemyTargetDiesBeforeHead_FailsWithoutMutation` 任务 `c6e33127647f460fa6b6cbabe8decc4e` 得到预期行为红灯：期望 `TargetNotAlive`，旧链路却返回成功。
2. 在首次写入前接入同一规则后，按既定失败优先级保留第二名存活敌人，使测试只隔离“指定目标死亡”条件；任务 `4ba17c23333e47ac850c53070c0023a8` 为 **1/1 通过**。
3. 新的 `BattleAlreadyEnded` 前置条件令 9 个未声明存活敌人的旧出牌夹具得到预期回归红灯，任务 `9073865b6aad4d1cab2b3cb96117d9ba`。逐用例显式加入存活敌人与 Encounter 顺序后，队列与 presentation 任务 `c9760b45d233435ca18b402d0b32ebe4` 为 **28/28 通过**；没有通过全局工厂默认值掩盖事实。
4. 新增合法 Enemy 成功用例，断言提交和表现完成前后均为能量 `3 → 2`、指定实例只出现于弃牌堆一次、目标 `Health` 对象和值不变。加入后队列与 presentation 任务 `add47b4922414eef8db83905536e1979` 为 **29/29 通过**。
5. 最终强化死亡目标用例，校验失败结果与提交权威序号一致，完成失败表现后队列回到空闲；单项任务 `ee71dcce161646ce90c5bb0ad204ea62` 为 **1/1 通过**。

## 自动验证

| 检查 | 结果 |
|---|---|
| `BattleCardPlayRulesTests`、`BattleCommandQueueTests`、`BattleCommandPresentationAdapterTests`、`BattleTurnControllerTests` | 与 M5 敌人意图两组回归合并为 **60/60 通过**，0 failed、0 skipped；任务 `7be5618ef7924c059416fe6e9743283d` |
| 目标排队后死亡 | `TargetNotAlive`；Turn 与卡区快照同对象、能量不变、手牌不变、目标 `Health` 对象和值不变；失败表现完成后队列空闲 |
| 合法 Self / Enemy | Self 既有顺序用例继续通过；Enemy 只扣一次费用、只移动一次指定实例，目标生命对象和值不变 |
| 跨轮旧命令 | 继续优先 `PlayerActionWindowExpired`，同一卡牌下一轮重抽也不执行旧命令 |
| Unity 脚本刷新 | 现有唯一 Unity Editor 完成编译与 domain reload；最终 Console Error 0 |
| `dotnet build TinySpire/TinySpire.sln --no-restore -m:1` | **0 error**；12 条既有 Unity/R3/UniTask 依赖版本冲突 warning |
| `git diff --check` | 通过 |

## 停止点结论

- `BattleCommandQueue` 的提交轮次栅栏仍在控制器之前；公共 `Submit`、`Queue`、`Turn` interface 未改变。
- 费用透支、卡牌离手、死亡玩家、错误玩家、另一玩家排队、旧展示回调和 M5 敌人意图回归均继续通过。
- 未修改 `BattleScene.unity`、任何 Prefab、配置表、Localization、Addressables、生产 UI、Effect、伤害、格挡、状态、死亡结算或胜负。
- 本切片未运行 Addressables、Bootstrap 或 Game View；它们不属于 M6B 停止点，将按唯一计划在 M6C/M6D 执行。
- M6B 已达到独立停止点；下一步只能进入 M6C Self/Enemy 目标选择 UI。
