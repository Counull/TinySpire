---
title: M4B 队列化出牌、能量与执行期校验验收
page_type: testing
lifecycle: active
date: 2026-08-01
scope: TinySpire M4B 纯 C# 出牌命令、每玩家能量与卡区写入
source: ../plans/2026-07-31-m4-turn-scheduling-energy.md
status_source: ../SESSION_LOG.md
---

# M4B 队列化出牌、能量与执行期校验验收

## 验收结论

M4B 已满足独立停止点：`PlayCardCommand` 仍只携带玩家与运行时卡牌实例标识；命令到达权威队首时，从当前参与者、该玩家卡区和 Luban 静态 `Card` 模板重新校验身份、手牌归属、费用与能量。全部校验通过后才移动指定实例并扣除该玩家能量，任何失败都不发布新的回合或卡区事实。

当前实现保持纯 C# 且未接生产场景。`EndPlayerActionCommand`、敌人阶段、初始抽牌迁移、DI 与 UI 接线仍属于 M4C/M4D。

## 行为证据

测试只通过公共 `BattleCommandQueue.Submit`、`Queue` 与 `Turn` seam 观察结果；可控制表现 adapter 仅决定当前结果何时完成，没有调用内部队列推进方法。

| 验收项 | 结果 |
|---|---|
| 每玩家基础能量 | `EnergyPerRound` 默认 3；开始战斗后两名玩家分别获得 3 点，没有全局能量字段 |
| 费用 1 + 2 | 权威序号 2、3 依次执行，能量从 3 → 2 → 0，两张指定实例按序进入弃牌堆 |
| 展示等待 | 第一名玩家的牌等待展示时，第二名玩家提交成功但其 3 点能量与手牌不提前变化；完成回调后才执行 |
| 旧能量重校验 | 两张费用 2 的牌可基于旧预览连续提交；第一张执行后剩 1 点，第二张返回 `InsufficientEnergy` 且留在手牌 |
| 卡牌离手 | 命令排队期间实例被移出手牌后返回 `CardNotInHand`，能量与当时卡区快照保持不变 |
| 错误参与者 | 敌人标识返回 `InvalidPlayer`；死亡玩家返回 `PlayerNotAlive`，均不改变玩家事实 |
| 卡区与模板缺失 | 缺少该玩家卡区返回 `PlayerCardZonesNotFound`；实例模板缺失返回 `CardTemplateNotFound`，均无副作用 |
| 后续里程碑命令 | 结束行动与完成敌人行动仍返回 `UnsupportedCommand`，未提前实施 M4C |

阶段和结束行动标记的执行期分支已经位于上述校验顺序中；当前 M4B 状态机在战斗开始后只稳定停留于 `PlayerAction`，且结束行动写入尚属 M4C，因此这两个后续可达状态不通过测试专用旁路伪造。战斗前玩家命令的结构性拒绝由既有公共 seam 用例继续覆盖。

## 自动验证结果

| 检查 | 结果 |
|---|---|
| `DataTables/game-config.json` 与 `Assets/GameData/game-config.json` | 内容一致；`initialHandCount = 5`、`energyPerRound = 3` |
| Unity MCP 相关 EditMode | **18/18 通过**，0 failed、0 skipped |
| Unity Console Error | 脚本刷新后 **0**；Addressables 构建完成后 **0** |
| Bootstrap 短时启动 | `game-config.json 已加载。`；Error 与 `InvalidKey` **0**，随后正常退出 Play Mode |
| `dotnet build Assembly-CSharp.csproj --no-restore --verbosity:minimal` | **0 error**；6 条既有依赖版本冲突 warning |
| `dotnet build Assembly-CSharp-Editor.csproj --no-restore --verbosity:minimal` | **0 error**；12 条既有依赖版本冲突 warning |
| `git diff --check` | 通过 |

## Addressables 证据

- 执行入口：`TinySpire/Addressables/Build Local Content`。
- 构建报告：`TinySpire/Library/com.unity.addressables/BuildReports/buildlayout_2026.08.01.01.41.10.json`。
- `BuildError` 为空；`BuildResultHash = 4877b4655f41f300d0ffc1bb4c37fb25`；耗时 `52.5617219s`。
- `Assets/GameData/game-config.json` 位于 `TinySpire GameData`，AddressableName 继续使用完整稳定地址 `Assets/GameData/game-config.json`，标签为 `GameData`。

## 启动链补充

- 当前唯一 Unity Editor 从已打开的 `BootstrapScene` 进入 Play Mode，等待配置与启动链加载后读取 Console，再正常退出。
- `ConfigService` 明确输出 `game-config.json 已加载。`；未出现 Error、`InvalidKey` 或资源地址错误。
- 模式切换期间有三条 MCP WebSocket 关闭握手 warning；它们来自 `MCPForUnity` 传输层，不是 TinySpire 游戏逻辑、配置或 Addressables 加载错误。

## 明确未验证与未实施

- 未运行全量 EditMode 或完整 BattleScene 出牌功能实跑；本次 Play Mode 只验证 Bootstrap 配置加载链，生产队列接线仍未发生，相称的场景验收留给 M4C～M4E。
- 未修改或运行 Luban Excel/代码生成；`game-config.json` 是手写规则配置，两份运行时 JSON 已直接同步。
- 未修改场景、Prefab、ProjectSettings、asmdef、HybridCLR、现有手牌 UI 或 `BattleLifetimeScope`。
- 未执行真实卡牌 Effect、伤害、格挡、状态、目标选择或卡牌专属动画。
- 未实现 M4C 的结束玩家行动、敌人顺序交接、下一轮重置和初始抽牌迁移，也未实施 M4D～M4E。
