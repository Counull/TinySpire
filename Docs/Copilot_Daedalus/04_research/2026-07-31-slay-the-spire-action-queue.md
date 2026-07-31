---
title: 《杀戮尖塔》动作队列与《杀戮尖塔 2》联机排序核验
page_type: digest
lifecycle: active
date: 2026-07-31
updated: 2026-07-31
scope: Slay the Spire 1/2 战斗动作排序、联机提交与表现播放
---

# 《杀戮尖塔》动作队列与《杀戮尖塔 2》联机排序核验

## 当前结论

用户提出的方向**基本正确，但应明确写成参考《杀戮尖塔 2》的联机模型，而不是笼统写成“和《杀戮尖塔》一样”**：

- 《杀戮尖塔 1》是单人游戏。其战斗确实由动作管理器依次推进，但实现中同时存在 action、card、pre-turn、monster 等多个队列，并允许把动作插到头部；因此不能把它严谨地描述成“所有命令进入一个简单 FIFO”。
- 《杀戮尖塔 2》合作模式采用同时回合：玩家可在回合内随时出牌，无需等待其他玩家。各端可以独立产生输入，但会把影响战斗状态的行动收敛到一致的权威顺序，再按这个顺序确定性执行。
- “同时”发生在**提交侧**，不代表规则效果并行结算。所有节点必须采用同一个逻辑顺序；表现播放可以速度不同，不要求同一帧或同一时刻播放到相同位置。
- 公开材料能支持“统一排序入口 / 动作队列同步器”，但不足以证明当前正式实现就是一个没有优先级、续接或其他辅助结构的单一 FIFO。TinySpire 文档应描述语义保证，而不是过早锁死容器实现。

## 事实与证据

| 事实 | 结论 | 置信度 |
|---|---|---|
| 《杀戮尖塔 1》仅有单人模式；《杀戮尖塔 2》的回合为同时回合，玩家可以在回合中随时出牌，不等待队友。 | 《杀戮尖塔 2》直接负责联机实现的开发者明确说明；这也是“玩家互不阻塞地提交”的直接证据。 | **Source-stated · 高** |
| 多人测试需要覆盖“多人同时出牌”等单人无法制造的时序问题。 | 多个玩家确实能在相近时间产生出牌输入，系统必须处理竞争排序。 | **Source-stated · 高** |
| 卡牌出牌使用依赖消息顺序的可靠传输；客户端只向主机发包，主机再转发；战斗中的 `ActionQueueSynchronizer` 负责让行动正确排序。 | 联机输入不是各客户端各算各的；存在统一排序与广播路径。 | **Source-stated · 高** |
| 确定性模型要求所有玩家以相同顺序执行行动；例如 Bash 与 Strike 的先后会改变结果。各客户端可以用不同速度回放，只要逻辑顺序相同。 | 应区分“权威逻辑顺序”与“表现同步速度”。 | **Source-stated · 高** |
| 早期架构方案由主机对 `ActionQueue` 排序，客户端向主机请求入队，主机确认后广播；单人模式走相同 API，但同步层退化为透传。 | 这是理解当前接口边界的强证据，但作者明确提示两年前的方案不一定完全等同于当前实现。 | **Source-stated · 中** |
| 《杀戮尖塔 1》的 `GameActionManager` 分别持有 `actions`、`preTurnActions`、`cardQueue`、`monsterQueue`；`addToBottom` 追加，`addToTop` 插入首位；运行时只推进一个 `currentAction`。 | 原作是串行推进的动作系统，但不是一个纯 FIFO，也不是“出牌命令本身等于全部效果动作”。 | **Code-observed · 中**（非官方反编译移动端仓库） |

## 来源

1. Edward Lu（《杀戮尖塔 2》合作模式主要实现者），[Creating the Multiplayer of Slay the Spire 2](https://straypixels.net/sts2-multiplayer-timeline/)：同时回合、玩家无需等待队友、多人同时出牌的时序问题；同时说明其附带的早期设计文档不保证与当前架构完全一致。
2. Edward Lu，[Slay the Spire 2 Multiplayer - Base Layers](https://straypixels.net/sts2-multiplayer-architecture/)：主机/客户端消息路径、卡牌出牌采用可靠有序消息，以及 `ActionQueueSynchronizer` 的职责。
3. Edward Lu，[Slay the Spire 2 Multiplayer - Determinism](https://straypixels.net/sts2-multiplayer-determinism/)：所有节点必须使用相同操作顺序；逻辑执行顺序一致即可，各端表现可按不同速度回放。
4. Edward Lu，[STS2 Multiplayer Code Architecture Plan](https://straypixels.net/assets/sts2-multiplayer-timeline/design-doc.pdf)：早期 `NetActionQueue`、主机权威排序、客户端请求入队与单人透传方案。该文档是历史设计证据，不作为当前实现细节的唯一事实源。
5. Mega Crit，[Slay the Spire 2 Steam 页面](https://store.steampowered.com/app/2868840/Slay_the_Spire_2/)：官方确认最多四人合作模式，但未描述底层排序语义。
6. `tldyl/ModbileTheSpire`，[GameActionManager.java](https://github.com/tldyl/ModbileTheSpire/blob/master/android/src/com/megacrit/cardcrawl/actions/GameActionManager.java)：反编译移动端代码，用于观察原作多个队列、头尾入队和单动作推进。该仓库不是 Mega Crit 官方源码，故只给中等置信度。

## 对 TinySpire M4 的口径影响

建议把“玩家可交错提交出牌命令”替换为：

> 玩家在共享 `PlayerAction` 阶段内可并行提交出牌命令，无需等待其他玩家；所有命令进入统一的权威排序入口，确定全局逻辑顺序后串行结算。表现层按同一顺序播放结算结果，但不要求不同客户端同帧或同速播放。并行仅发生在提交侧，不代表战斗规则并行执行。

并补充以下边界：

- 每条命令仍携带提交者 `CombatantId`，能量、手牌与结束行动状态归属于各自玩家。
- 提交入口、权威排序器、规则执行器与表现播放器应分层；UI 不直接修改战斗状态。
- 不把实现锁死为“一个简单 FIFO”。反应、优先插入、行动内选择和续接都可能需要受控的排序规则或辅助队列，但对外只暴露一个权威逻辑顺序。
- 玩家结束行动后停止提交新的主动命令；只有全部玩家结束且已接受命令完成结算，才能进入敌方阶段。

## 尚未由公开材料确认

- 《杀戮尖塔 2》当前版本对“行动执行到一半需要拥有者选择卡牌/目标”的最终实现尚无已发布的详细技术文章。早期设计文档讨论过本地/共享队列、等待表和 continuation action，但作者明确标注该文档可能已过时。
- 因此 TinySpire 可以预留“暂停后续接”的命令协议，但不应把某一种内部结构写成“《杀戮尖塔 2》已确认采用”的事实。
