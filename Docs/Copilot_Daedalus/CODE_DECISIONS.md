---
created: 2026-07-06
updated: 2026-07-29
---

# Daedalus · 代码决策记录

> 代码级决策，补充 `Hermes_Pegasus/design/decisions.md`（玩法级决策）。

## CD-001：LoadingScene 采用最短展示时间

**问题**：资源准备很快时，LoadingScene 会一闪而过，无法形成稳定过渡。

**选择**：LoadingScene 完成切入后开始真实时间计时，目标场景加载前保证累计展示至少 1 秒；内容准备耗时计入展示时间。

**理由**：快加载时补足视觉过渡，慢加载时不叠加无意义的固定等待；真实时间延迟避免受到游戏暂停或 `Time.timeScale` 的影响。

**影响**：`TinySpire/Assets/Scripts/Core/SceneFlowService.cs` 的所有带 LoadingScene 的场景切换。

## CD-002：BattleScene 基础选牌 UI 复用 UGUI CardView

**问题**：BattleScene 需要先展示并选择卡牌，但当前切片尚未实现卡牌数据、ViewModel 和出牌链路。

**选择**：在现有 UGUI Canvas 下直接实例化 `CardView.prefab`，由场景级 `Toggle` + `ToggleGroup` 提供单选表现；选择结果暂时只存在于 UGUI 控件状态，不建立运行时业务模型。

**理由**：`CardView` 已经是 UGUI 预制体，复用它可以用最小场景改动先验证布局和交互；同时避免把 UI 构造任务扩成数据与战斗逻辑实现。现有 Canvas 继续使用 Screen Space - Camera，并把平面距离设为 1，使 UI 位于背景 Sprite 之前。

**影响**：`TinySpire/Assets/Scenes/BattleScene.unity`。后续接入动态手牌时，需要由运行时层生成卡牌并把 UI 选择同步到明确的 ViewModel/命令入口。

## CD-003：引入 DOTween 作为 UI 补位/过渡动画方案

**问题**：手牌 UI 需要悬停抬起、拖拽回弹、重排补位等平滑过渡，需要一个缓动方案；项目此前未正式引入 DOTween（`Hermes_Pegasus/STATUS.md` 中曾是 Open Question）。

**选择**：使用 DOTween（用户已将 DOTween/DOTweenPro 导入 `TinySpire/Assets/Plugins/Demigiant/`）驱动手牌 UI 的悬停与补位动画：悬停抬起/落下约 0.15s、`Ease.OutBack`；手牌重排补位约 0.2~0.25s、`Ease.OutQuad`/`Ease.OutCubic`；每张卡自行维护 Tween 引用，新动作前先 `Kill` 旧 Tween。

**理由**：DOTween 是 UI 缓动最成熟的现成方案，避免自建 easing/可中断的补位小工具；项目技术栈本来就把 DOTween 列入过考虑范围。

**影响**：`TinySpire/Assets/Scripts/UI/` 下新增的手牌交互脚本；不影响计算层、状态层。详见 `plans/2026-07-29-battlescene-hand-ui-sts-style.md`。

## CD-004：手牌交互模型由单选 Toggle 替换为悬停/扇形/拖拽视觉

**问题**：CD-002 的 `Toggle` + `ToggleGroup` 单选高亮与杀戮尖塔式的悬停抬起 + 拖拽跟手交互互斥，无法共存。

**选择**：移除 `BattleScene` 中的 `Toggle`/`ToggleGroup` 组件与选中高亮面板，替换为：悬停抬起（位移+旋转归零+缩放+`Canvas.sortingOrder` 临时提升）、扇形布局（基于归一化位置 `t` 的旋转/下沉曲线）、拖拽跟手视觉（其余卡重排填空，松手不打出则按原顺序回弹）。仍然不做出牌判定/合法目标选择，复用 `CardView.prefab` 不做本体改动。

**理由**：新交互模型是本轮明确的产品方向（杀戮尖塔式手牌体验），与旧的单选语义无法叠加；CD-002 记录保留作为历史决策，不删除，仅在此说明已被本决策替换。

**影响**：`TinySpire/Assets/Scenes/BattleScene.unity`；`TinySpire/Assets/Scripts/UI/` 下新增手牌容器与单卡交互脚本。详见 `plans/2026-07-29-battlescene-hand-ui-sts-style.md`。

## CD-005：新增 HandState 收回手牌数量的数据归属权

**问题**：`handCount` 一直是 `HandCardContainer`（UI 组件）自己持有并自增自减的字段，一旦接入“出牌”判定就需要修改这个数量，会让 UI 组件变成事实上的权威运行时状态持有者，与三层架构（计算/状态/时序/UI 分层）冲突。

**选择**：新增 `HandState`（纯 C# 类，不依赖 `MonoBehaviour`/Unity API）：内部持有手牌卡牌 ID 列表（本轮仍是占位 ID），对外只暴露只读快照、`PlayCard(int cardId)` 方法、一个 `event Action` 变化通知。`HandCardContainer` 不再自己持有/自减 `handCount`，只订阅 `HandState` 的变化去重建视觉；出牌判定成立时调用 `HandState.PlayCard`。

**理由**：把手牌数据的归属权从 UI 里收回，是后续真正接入 Luban 数据源、Effect 系统、BattleState 的必要前提；现在不做，将来所有涉及手牌数量的改动都要同时改 UI 和数据两处。`event Action` 是过渡形态，暂不引入 R3，避免在状态层设计未定时提前锁定响应式方案。

**影响**：`TinySpire/Assets/Scripts/UI/Battle/Hand/` 下新增 `HandState`；`HandCardContainer` 改为订阅它而非自持状态。详见 `plans/2026-07-29-battlescene-drag-to-play-minimal.md`。

## CD-006：拖拽出牌用可调 Y 轴阈值判定，不做目标/费用检查

**问题**：现有拖拽跟手视觉松手无条件回弹，没有“怎么算打出”的判定；同时打出后是否需要选目标、扣费用、播放过渡动画都还没有数据支撑（怪物/玩家锚点、能量系统、Effect 系统均未落地）。

**选择**：加一条可调的 Y 轴出牌线阈值，松手时对比被拖卡当前位置，超过判定为打出，否则回弹。判定为打出后：不做目标选择/检测（预留恒为 `null` 的 `targetId` 字段）、不做费用检查、`HandCardVisual` 立即 `Destroy`、拖过线时只做未经美术设计的最简占位视觉反馈。四处均在代码中用 `TODO(DEP-001)`~`TODO(DEP-004)` 标记为待后续依赖项解决。

**理由**：目标检测方式依赖尚未确定的怪物/玩家锚点形态（UI 还是 World Space），费用系统、过渡动画依赖尚未落地的能量系统与 Effect 系统；在这些前置条件明确前设计会建立在错误地基上。用带 ID 的 TODO 标记，方便未来按依赖项逐条替换实现，而不是现在做投机性设计。

**影响**：`TinySpire/Assets/Scripts/UI/Battle/Hand/HandCardContainer.cs`、`HandCardVisual.cs`。详见 `plans/2026-07-29-battlescene-drag-to-play-minimal.md`。

## 决策模板

```markdown
## CD-XXX：决策标题

**问题**：一句话描述

**选项**
- A: ...
- B: ...

**选择**：X

**理由**：为什么选 X。

**影响**：哪些模块/文件受影响。
```

## CD-007：新增最小纯 C# 状态机 Core

**问题**：项目需要一个可跨多帧运行的状态机基础，但游戏领域状态、事件队列、异步调度和嵌套协议尚未确定；提前把这些语义放进 Core 会制造不必要的复杂度。

**选择**：新增 `TinySpire.Core.StateMachine` 下的最小同步状态机：状态提供 `Enter`、`Tick(TimeSpan)`、`Handle(event)`、`Exit` 四个生命周期入口；状态通过 `StateTransition` 返回“保持当前状态”或“切换到新状态”；调用方负责 Update/Tick 驱动和事件排队；`Stop()` 终止当前实例，重新运行时创建新实例。

**暂不包含**：Context 抽象、内部事件队列、异步/定时器、嵌套状态协议、并行状态、游戏数据查找和领域事件。

**理由**：先保留一个小而明确的接口，验证跨帧保持、同步事件转换和生命周期顺序；后续只有在真实用例证明需要时，才通过外部组合或新的决策扩展能力。

**影响**：新增 `TinySpire/Assets/Scripts/Core/StateMachine.cs` 及其 Unity 元数据；不接入现有游戏代码，不改变 HandState、配置服务或 BattleScene。
