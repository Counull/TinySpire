---
title: M4D 当前单玩家命令 UI 接线验收
page_type: testing
lifecycle: active
date: 2026-08-01
scope: TinySpire M4D 手牌命令接线、回合 HUD、命令反馈与单玩家轮次恢复
source: ../plans/2026-07-31-m4-turn-scheduling-energy.md
status_source: ../SESSION_LOG.md
---

# M4D 当前单玩家命令 UI 接线验收

## 验收结论

M4D 已满足独立停止点。现有手牌拖拽与结束行动按钮只向 `BattleCommandQueue` 提交命令；UI 不直接扣能量、移动卡区或推进阶段。生产展示 adapter 明确区分已排队、执行失败和执行完成，并在结果展示期间继续接受其他合法出牌意图。当前单玩家限制只存在于 View/生产装配边界，命令队列和回合事实仍按 `CombatantId` 与玩家映射表达。

能量球、结束回合按钮和玩家回合横幅复用既有 P1 美术，静态层级落在 `BattleTurnHud.prefab`；没有为尚不存在的敌人意图、Effect、状态、伤害、胜败或结算制造占位事实。`DEP-002` 已解决，`DEP-001` 与 `DEP-004` 保持 open；M4E 全量验证与复审尚未开始。

## 代码与接线证据

| 区域 | 结果 |
|---|---|
| 手牌提交 | `HandCardContainer` 越过出牌线后只构造并提交 `PlayCardCommand(actorId, cardId)`；UI 目录与场景中不存在 `DiscardFromHand`、`TryPlayCard`、`TryEndPlayerAction` 或 `TryCompleteEnemyAction` 直通调用 |
| 卡牌 pending | 以队列返回的权威序号关联 `CardInstanceId`；pending 只改变该 View 的透明度与射线状态，不预扣能量、不移动权威卡区，也不锁住其他卡牌 |
| 失败恢复 | 执行失败反馈清除对应卡牌 pending 并恢复交互；权威队列因校验失败不发布新的能量或卡区事实 |
| 成功收敛 | 出牌成功后由 `BattleCardZonesData.Layout` 移除离手 View，不另存 UI 手牌列表；结束行动成功后旧 View 在同帧先锁交互再销毁 |
| 回合 HUD | `BattleTurnHudView` 从 `Turn` 与展示反馈派生能量、轮次、阶段、状态和按钮可用性；结束按钮唯一写入口是 `EndPlayerActionCommand` |
| 生命周期 | `BattleLifetimeScope` 将 `BattleCommandPresentationAdapter` 注册为生产表现入口并注入手牌/HUD；队列和阶段核心未写入单玩家特例 |

## Bootstrap 运行行为

| 验收项 | 运行证据 |
|---|---|
| 首轮初始事实 | 从 Bootstrap 进入 `BattleScene` 后：`phase=PlayerAction; round=1; energy=3; hand=5; cardVisualCount=5`；HUD 为 `3 / 3`、`Round 1`、`PlayerAction`，结束按钮可用 |
| 快速连续出牌 | 在同一展示窗口通过实际 `HandleBeginDrag -> FollowPointerDelta -> HandleEndDrag` 运行时回调链提交实例 1、7（均费用 1）；首张执行后能量 2、手牌 4，第二张仍在权威手牌且自身为 pending；继续推进后能量 1、手牌 3、弃牌 2，展示顺序按权威序号完成 |
| 执行期失败 | 以剩余 1 能量提交费用 2 的实例 10；排队时能量、手牌、弃牌均不变，结果为 `InsufficientEnergy` 后仍保持 `energy=1; hand=3; discard=2`，该卡 pending 清除且交互/射线恢复 |
| 结束行动锁定 | 通过场景 `Button.onClick.Invoke()` 提交结束命令后立即进入 `EnemyAction`，轮次仍为 1，5 张剩余手牌移入弃牌堆，按钮不可用，全部旧卡 View 在销毁前已不可交互 |
| 下一轮恢复 | 生产驱动经同一队列完成无行为敌人后进入 `PlayerAction / Round 2`；能量恢复 3、手牌恢复 5、结束标记恢复 false，按钮和新卡 View 均重新可用 |
| Console | 最终 Bootstrap 运行期间 Error/Warning 均为 0；未出现 `InvalidKey`、VContainer 或本次改动引入的错误 |

上述连续出牌与失败用例运行了生产场景中的实际拖拽处理函数链，但 Unity MCP 不等同于物理鼠标手势；鼠标抓取偏移与拖拽手感可在审查时人工复核，不作为本记录伪称的自动化证据。

## 自动验证结果

| 检查 | 结果 |
|---|---|
| Unity MCP 定向 EditMode | **30/30 通过**，0 failed、0 skipped；任务 `cf3b0d7bae8044ccbfd51f63be589826`，耗时 `0.3453076s` |
| 覆盖测试 | `BattleCommandQueueTests`、`BattleTurnControllerTests`、`BattleCommandPresentationAdapterTests`、`BattleTurnHudPresentationTests`、`CardPlayTransitionTests` |
| `dotnet build Assembly-CSharp.csproj --no-restore --verbosity:minimal` | **0 error**；6 条既有依赖版本冲突 warning |
| `dotnet build Assembly-CSharp-Editor.csproj --no-restore --verbosity:minimal` | **0 error**；12 条既有依赖版本冲突 warning |
| Unity Console | 最终 Bootstrap 实跑 Error/Warning **0/0** |

## Addressables 与场景验证

- 全程复用当前唯一的 `TinySpire@8edf130c865b3957` Unity 6000.5.5f1 Editor，没有启动或结束其他 Unity 进程，也没有删除锁文件。脚本域重载和 Test Runner 边界曾使 MCP 临时断开；重连后始终固定同一实例继续验证。
- 新增 `BattleTurnHud.prefab` 并作为单一 Prefab 实例接到 `BattleScene` 主 Canvas。四个既有 P1 SVG 仅修正为 UGUI 可用的 `TexturedSprite` 导入，GUID/逻辑资产身份保持不变；源 SVG 未改。
- 执行 `TinySpire/Addressables/Build Local Content` 成功。报告 `TinySpire/Library/com.unity.addressables/BuildReports/buildlayout_2026.08.01.04.27.53.json`：`BuildError` 为空，`BuildResultHash=259e02cf2d79b5cd0bd291f571b46782`，`Duration=19.984942s`；Editor 日志记录内容构建成功。
- 未修改 `DataTables/Datas/`、表定义、生成代码或 `Assets/GameData` JSON，因此没有运行 Luban。

## 明确未实施

- 未实施 M4E 的全量 EditMode、至少两个完整轮次、全量 solution build 或 Standards / Spec 双轴收口审查。
- 未修改程序集边界、HybridCLR、启动流程或 Addressables 架构；VContainer 仅增加本阶段所需的 View 与 adapter 注册。
- 未实现真实网络、多人生产 UI、敌人行为/意图、卡牌 Effect、目标选择、伤害、格挡、状态、胜败、奖励或专属出牌动画。
- 未改变 `DEP-001` 的目标检测方案，也未解决依赖真实 Effect/动画的 `DEP-004`。
