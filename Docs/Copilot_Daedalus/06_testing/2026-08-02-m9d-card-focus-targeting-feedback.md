---
title: M9D 不可用样式、目标聚焦与正式目标素材
page_type: testing
lifecycle: active
date: 2026-08-02
status: passed
scope: Disabled/VisualOnly/Playable、Enemy focus、正式 Targeting Sprite、五宽高比、输入/清理、Prefab、Addressables、Bootstrap 与范围审计
plan: ../plans/2026-08-02-m9-sts-feedback-outcome-restart.md
status_source: ../SESSION_LOG.md
---

# M9D 不可用样式、目标聚焦与正式目标素材

## 当前结论

M9D 已通过。Hand 继续从现有 `BattleCardPlayRules`、阶段、能量、pending/fault 与 M9C readiness 即时派生 `Disabled / VisualOnly / Playable`，没有保存第二份合法性或战斗事实。Enemy 卡首次越过出牌线后移动到序列化 focus anchor，旋转归零、轻微放大并呼吸；箭头起点逐帧跟随卡牌，终点继续使用当前拖拽指针事实。Self、Enemy 与无效释放仍走 M6 同一目标 seam，Tween 回调不提交命令。

`BattleTargetingArrow.prefab` 与 `ParticipantHudView.prefab` 已接入既有 Runtime/Targeting 四张正式 Sprite。没有修改这些 PNG/Meta，也没有读取、引用或修改 Hermes/Candidates。M9F 胜负面板尚未出现。

五宽高比交互采用用户明确授权的 `InputSystemUIInputModule → EventSystem raycast → BeginDrag / Drag / EndDrag` 跨帧注入替代 OS 物理鼠标；没有直接调用 listener、`Handle*` 或目标容器入口。截图只作画面佐证，时序结论来自连续 frame 与只读事实。

## 测试先行与红绿证据

| 契约 | 红灯 | 最小实现 | 最终绿灯 |
|---|---|---|---|
| 三态卡牌表现 | 新增测试先证明现有 View 没有 Disabled 覆盖层、VisualOnly 费用色与 Playable 复位入口 | `HandCardVisual.SetInteractionPresentation` 只改变 Canvas/raycast、运行时灰层与费用颜色；Container 仍复用现有规则评估 | 三态、阶段/readiness/fault 与释放规则进入最终 98 项聚焦回归 |
| Enemy focus 与箭头逐帧跟随 | 新增 focus 测试先因缺少 anchor、私有 focus Tween、呼吸及 LateUpdate 起点更新而失败 | 在 `BattleHandUI.prefab` 增加序列化 anchor/参数；View 独占 transition/breath Tween ID，Container 仅在首次进入 EnemyTargeting 时启动并逐帧更新箭头起点 | focus、取消、队首失败、Prefab 与箭头测试通过 |
| 正式箭头与合法/悬停高亮 | Prefab 合约先检测到旧功能性箭头与单一高亮，不满足四张正式 Sprite、左右互斥和 raycast 关闭 | 箭身/箭头分别接线；左右目标各自使用 Legal/Hovered 根，始终不接收 raycast | Sprite/Prefab 合约与真实左右/空白切换通过 |
| 16:7 不遮挡 | 初版 anchor `(0,-40)` 在 1600×700 下，聚焦卡 tight bounds 与左敌世界 Sprite 约重叠 **1.9 px**，因此未判通过 | 只把 anchor 改为 `(-8,-40)`；未改 Scene、目标规则、公式或布局事实 | 五种实际分辨率均在屏内，且与参与者 tight mesh / 活动 HUD Graphics 无交叠 |
| 屏幕坐标测试稳定性 | 五宽高比运行改变 Canvas 缩放后，旧测试把屏幕位移误当锚点位移，98 项中 1 项失败：期望 `60`、实际 `88` | 测试改为直接断言最终 `GetScreenCenter()` 到达目标屏幕坐标；运行时代码不变 | 单项任务 `dc4fc8ed05434fd0890bf21ca5fe076f` **1/1 passed**；合并任务 `5de9234f03b24c629ea650747a6cf21b` **98/98 passed** |

## Disabled / VisualOnly / Playable

- `Playable` 保持正常颜色、交互与目标规则；合法卡在表现屏障期间仍可由系统指针命中并提交，Queue 继续负责排序。
- `VisualOnly` 用精确序列化费用色 `(0.95, 0.2, 0.2, 1)` 提示费用不足；仍允许既有视觉拖拽，但不进入 Self/Enemy 提交、箭头或高亮路径，释放前后权威事实不变。
- `Disabled` 使用非交互灰化覆盖层并关闭 `CanvasGroup.blocksRaycasts/interactable`；BattleEnded 实跑中剩余卡全部不可命中。恢复 Playable 时覆盖层和费用色均从当前状态重新派生。
- 没有读取 `IsWaitingForPresentation` 形成全局输入锁。只有 M9C readiness、阶段/终局、pending/fault 与当前卡的局部 transient/focus 状态参与现有入口判断。

## Enemy focus、箭头与清理

- Enemy 卡只在首次越线进入 `EnemyTargeting` 时启动 focus；随后拖拽帧不会重启 transition。到达后 rotation 为 0，scale 进入序列化 focus/breath 范围。
- `LateUpdate` 以 `HandCardVisual.GetScreenCenter()` 更新箭头起点，以 Container 当前 `_lastPointerScreenPosition` 更新终点；指针静止期间卡牌继续移动时，箭头仍逐帧贴合。
- VisualOnly 降级、有效/无效释放、阶段变化、普通队首失败、fault、对象销毁与 Scene 销毁都会精确结束 focus transition/breath、箭头和高亮；恢复时以当前权威 Layout 的 base pose 为准。
- 队首普通失败/fault 的清理由自动测试覆盖，没有伪称做过物理 fault 注入。Scene 生命周期证据则从真实 EnemyTargeting 状态加载 `BootstrapScene`：旧 card、arrow 与六个高亮根全部变为销毁对象，两个私有 Tween ID 均不再活动，之后生产启动链可重新进入新的 BattleScene。

## 四张正式 Targeting Sprite 与 Prefab

- 箭身：`ui_battle_target_arrow_body.png`，拉伸显示，`preserveAspect=false`。
- 箭头：`ui_battle_target_arrow_head.png`，固定头部尺寸，`preserveAspect=true`。
- 合法目标：`ui_battle_target_legal_highlight.png`。
- 悬停目标：`ui_battle_target_hover_highlight.png`。
- 箭头 Image、左右 Legal/Hovered Image 全部 `raycastTarget=false`；同一参与者只显示 Legal 或 Hovered 之一。
- Scene 依赖静态合约确认 Hand、Arrow、Participant HUD 与四张正式 Sprite 均可由现有 BattleScene/Addressables 链解析，并含“不得引用 Candidates”的负向断言。

## 五宽高比连续帧验收

实际 Game View 分辨率依次为 `1600×700 / 900 / 1000 / 1100 / 1400`，对应 16:7、16:9、16:10、16:11、16:14。每一项都由生产规则选择合法 Enemy 卡，先用 `EventSystem.RaycastAll` 证明卡中心命中真实 `CardContent`，再经当前 `UnityEngine.InputSystem.UI.InputSystemUIInputModule` 完成跨帧拖拽。

| 检查 | 五种比例结果 |
|---|---|
| 连续时序 | 每种比例均取得至少三帧严格递增的 EnemyTargeting 事实；卡中心持续移动，transition 完成后 breath 活动 |
| 箭头 | 每帧 `originDelta=0.0000`；`endDelta=0.0000`，终点等于 `_lastPointerScreenPosition`；rotation 为 0 |
| 不遮挡 | 聚焦卡 tight bounds 始终在屏幕内；与玩家/敌人 world Sprite tight mesh 及活动 Participant HUD Graphics 均为 `overlaps=none` |
| 高亮 | 左敌悬停为 `left Hovered / right Legal`，右敌相反；空白保持两敌 Legal 且无 Hovered |
| 清理与事实 | 释放后 `Idle / arrow=false / all highlights off / transition=false / breath=false`，Combatants、Energy、CardZones、Turn 与 Queue 快照不变 |

代表性 breath 帧：1600×700 卡中心 `(793.333,316.667)`、tight rect `(686.2,155.9,900.5,477.4)`；1600×900 为 `(793.333,416.667)`、`(671.8,234.4,914.8,598.9)`；1600×1400 为 `(793.333,666.667)`、`(641.8,439.4,944.9,894.0)`，均无交叠。最终画面证据位于 `TinySpire/Temp/CodexEvidence/M9D/m9d-final-1600x*-right-hover.png`；时序仍以连续帧事实为准。

## 生产交互矩阵

- Self Playable：真实 raycast/拖拽/释放使 Energy `3→2`、Hand `5→4`、Discard `0→1`、玩家 Block `0→5`。下一帧 Queue 正在表现该 PlayCard 时，另一张合法卡仍可 raycast，证明没有全局表现锁。
- 左右 Enemy：相同生产规则与真实目标释放分别使左敌 `20→14`、右敌 `20→14`，Energy 依次 `2→1→0`；左右 Hovered/Legal 互斥正确。
- VisualOnly：费用不足卡仍可视觉拖拽，arrow/highlight 均不出现，释放前后事实相等。
- 空白与玩家：Enemy 卡释放到空白或玩家均回到 Idle，箭头、高亮和 focus Tween 清空，事实相等。
- 死亡目标：先经真实卡牌和 End Action 推进杀死右敌，再把 Enemy 卡拖到其原屏幕位置；死亡参与者不进入候选或高亮，释放零写入，存活敌仍保持 Legal。
- 阶段与 End Action：真实按钮中心 raycast 后经历 `EnemyAction round 1 → PlayerAction round 2`，最终 Queue idle、Energy 3、Hand 5；阶段切换期间没有旧 focus/arrow/highlight。
- BattleEnded：杀死最后敌人后剩余 Hand 卡为 Disabled，`blocksRaycasts=false / interactable=false / overlay=true`，EventSystem 无卡牌命中；终局权威事实来自既有规则，M9F outcome 面板仍未出现。

## 最终自动验证与静态构建

| 检查 | 结果 |
|---|---|
| M9D target/drag/style/focus/Prefab、Queue/Adapter/feedback routing 合并回归 | **98/98 passed，0 failed，0 skipped**；任务 `5de9234f03b24c629ea650747a6cf21b` |
| Canvas 缩放修正后的 focus 单项 | **1/1 passed**；任务 `dc4fc8ed05434fd0890bf21ca5fe076f` |
| `dotnet build TinySpire/TinySpire.sln --no-restore -m:1` | **0 error，12 warnings**；均为既有 Unity / R3 / UniTask 依赖程序集版本冲突 |
| Local Content | Prefab 最终 anchor 修订后执行 `TinySpire/Addressables/Build Local Content`；catalog 时间为 **2026-08-03 10:19:50 +08:00** |
| Bootstrap / Console | 最终生产交互与场景销毁链完成后退出 Play，Editor 位于 `BootstrapScene`；Console Error / Warning **0/0** |

最终禁改范围检查只发现 Unity YAML 的空 `m_Name: ` 序列化尾空格：`ParticipantHudView.prefab` 13 处、`BattleHandUI.prefab` 1 处、`BattleTargetingArrow.prefab` 3 处；M9D C# 与测试没有 `git diff --check` 问题。HEAD 仍为 M8 基线 `6545640963e3f184bcd7915706e87bea4a142afa`。

## 范围与工作区保护

M9D 生产改动只位于：

- `TinySpire/Assets/Scripts/UI/Battle/Hand/HandCardContainer.cs`、`HandCardVisual.cs`；
- `TinySpire/Assets/Scripts/UI/Battle/Targeting/BattleTargetingArrowView.cs`、`HandCardReleaseTargetResolver.cs`；
- `TinySpire/Assets/Scripts/UI/Battle/ParticipantHudView.cs`；
- `TinySpire/Assets/Prefabs/UI/Battle/Hand/BattleHandUI.prefab`、`Targeting/BattleTargetingArrow.prefab` 与 `TinySpire/Assets/Arts/Runtime/Prefabs/ParticipantHudView.prefab`；
- 对应 M9 Editor 测试与 `HandCardTargetFocusTests.cs.meta`。

没有修改 Queue、Turn、settlement、Combatant、Intent、CardZones、Effect 公式/执行、目标规则、终局规则、`BattleLifetimeScope`、BattleScene、DataTables、Localization、GameData、ProjectSettings、asmdef、HybridCLR、启动/DI/Run/网络/多人。正式 Targeting PNG/Meta 只被 Prefab 引用，文件本身无 diff。`TinySpire/Packages/packages-lock.json` 的外部 hash 差异继续排除；受保护 Hermes/Candidates 改动未引用、修改、回退、移动、清理或暂存。未 commit、未 push。

## 停止点判定与后续

三态样式、Enemy focus、逐帧箭头、正式目标素材、Self/左右 Enemy、空白/玩家/死亡/非法释放、费用不足、阶段变化、队首失败、对象/Scene 销毁、表现期间合法输入和五宽高比均已有自动或生产证据；聚焦回归、串行 build、Local Content、Bootstrap 与 Console 通过。

M9D 停止点完成，`DEP-003` 改为 resolved。下一步只进入 M9E · 出牌、弃牌、抽牌与重洗运动；`DEP-004`、M9F 胜负/重开/退出及 M9G 最终全量验收仍保持待实施。
