---
title: BattleScene 拖拽打出（最小判定）
page_type: plan
lifecycle: proposal
date: 2026-07-29
scope: BattleScene MVP · UI + 最小手牌数据聚合
source: 与用户的 grilling 交互式确认（本会话），承接 2026-07-29-battlescene-hand-ui-sts-style.md
status_source: ../SESSION_LOG.md
depends_on: 2026-07-29-battlescene-hand-ui-sts-style.md（手牌 UI 已实施，见 06_testing 同名记录）
---

# BattleScene 拖拽打出（最小判定）

## 目标

在现有杀戮尖塔式手牌 UI（拖拽跟手视觉已实现，无判定）基础上，加上最小可行的"出牌判定"：拖过一条可调的出牌线即视为打出。**同时把手牌数量的归属权从 UI 组件收回到一个最小的纯 C# 数据聚合类**，UI 只读 + 调用，不再自己持有/修改权威状态。本轮仍不做目标选择、费用检查、Effect 执行。

## 影响层

- 计算层：新增 `HandState`（纯 C# 类，不依赖 Unity），是本轮唯一新增的"状态归属"边界。
- 状态层：暂不接入 R3；`HandState` 用最简单的 `event Action` 做变化通知，作为过渡形态。
- 时序层：拖过线视觉反馈使用现有 DOTween 管线（沿用手牌 UI 已有的 Tween 生命周期管理）。
- UI 层：`HandCardContainer` 改为订阅 `HandState` 重建视觉，不再自己持有 `handCount` 并自减；`HandleEndDrag` 增加出牌线判定。

## 前置事实

- `HandCardContainer.HandleEndDrag` 目前松手无条件回弹，无任何判定逻辑（见 `TinySpire/Assets/Scripts/UI/Battle/Hand/HandCardContainer.cs`）。
- `handCount` 当前是 `HandCardContainer` 的 `SerializeField`，由容器自己在 `Awake` 时读取并据此实例化对应数量的 `CardView`；本轮之前没有任何独立的手牌数据类。
- `CardView.prefab` 上的 `CostText` 只是显示占位，项目尚无能量池/费用系统。

## 设计决策（本轮已与用户逐条确认）

1. **出牌判定机制**：新增一个可调的 Y 轴阈值（如 `playLineY`，相对手牌安全区）。`HandleEndDrag` 时检查被拖卡当前锚点位置是否超过该线：超过判定为打出，否则沿用已有回弹逻辑。不做区域/多边形碰撞判定。
2. **无目标出牌**：本轮不实现任何目标选择/检测逻辑。出牌事件数据里预留一个恒为 `null` 的 `targetId`（`int?`）字段，不引入独立的目标选择/检测抽象类（对应 DEP-001）。
3. **手牌数据归属权收回**：新增 `HandState`（纯 C# 类，不依赖 `MonoBehaviour`/Unity API）：
   - 内部持有卡牌 ID 列表（本轮用占位 ID，如 `0..N-1`，初始数量取自现有 `handCount` 字段，作为构造参数传入，不再由 UI 持续持有）。
   - 对外只暴露：只读快照（当前卡牌 ID 列表，供 UI 渲染）、`PlayCard(int cardId)` 方法（从列表移除并返回是否成功）、一个变化通知（`event Action`，暂不用 R3）。
   - `HandCardContainer` 不再自己持有/自减 `handCount`；改为持有一个 `HandState` 实例，订阅其变化事件去重建视觉；出牌判定成立时调用 `HandState.PlayCard(cardId)`，不自己做数量增减。
4. **费用/能量检查**：本轮不做任何费用检查，无条件允许打出（对应 DEP-002，未来的费用扣减逻辑应并入 `HandState` 或其后续演化的聚合，不散落在 UI 里）。
5. **拖过出牌线的视觉反馈**：加一个未经美术设计的最简占位反馈——沿用现有 `CardContent` 上的 `Image`/`CanvasGroup`，拖拽中根据是否超过出牌线切换一个透明度或颜色微调值，退出线内恢复正常。代码中用 `// TODO(DEP-003): ...` 标记最终视觉样式待策划/美术确认（对应 DEP-003）。
6. **打出后卡牌对象处理**：`HandCardContainer` 订阅到 `HandState` 变化后，对应的 `HandCardVisual` GameObject **立即 `Destroy`**，不做淡出/飞向弃牌堆的过渡动画。代码中用 `// TODO(DEP-004): ...` 标记未来需要按卡牌效果类型（攻击/增益/弃牌等）做不同的销毁前动作（对应 DEP-004）。

## 依赖项清单（Dependency Ledger）

> 完整登记表（阻塞条件、状态、解决记录）以 [DEPENDENCIES.md](../DEPENDENCIES.md) 为唯一事实源，本节只列本轮涉及的 ID + 一句话摘要。代码中用 `// TODO(DEP-xxx): <一句话说明>` 标记对应位置。

- **DEP-001**：目标检测方式（UGUI `GraphicRaycaster` vs 2D `Collider`/`OverlapPoint`），待怪物/玩家锚点方案定案。
- **DEP-002**：费用/能量系统与检查逻辑，待能量池数据结构落地。
- **DEP-003**：拖过出牌线的最终视觉样式，待策划/美术确认。
- **DEP-004**：打出后卡牌的销毁前过渡动作（按效果类型区分），待 Effect 系统/卡牌数据结构落地。

## 边界与非目标（本轮明确不做）

- 不实现目标选择、合法目标高亮、出牌线以外的碰撞/射线检测。
- 不接入真实卡牌数据、费用/能量系统、Effect 执行链、BattleState（战斗层面的）。
- 不修改 `CardView.prefab` 本体。
- 不做弃牌堆动画、抽牌逻辑。
- `HandState` 不接入 R3、不做持久化，仅是把数据归属权从 UI 里收回的最小过渡形态。

## 验收点

- 拖拽任意一张卡越过出牌线并松手：该卡从手牌中消失（`Destroy`），其余卡重新扇形排布，且 `HandState` 内部列表同步减少一项。
- 拖拽任意一张卡未越过出牌线松手：沿用已有回弹逻辑，卡牌数量不变。
- 拖拽过程中越过出牌线时能看到占位视觉反馈（透明度/颜色变化），退回线内时反馈消失。
- `HandCardContainer` 代码中不再出现对 `handCount` 的自增/自减操作，`handCount` 只作为 `HandState` 的初始手牌大小输入。
- 代码中出现 4 处 `TODO(DEP-00X)` 标记，分别对应上表四个依赖项。
- Unity Console 无新增错误或警告。

## 后续（明确不在本轮范围内，留给下一切片）

- DEP-001 ~ DEP-004 的正式落地（分别依赖怪物/玩家锚点方案、能量系统设计、美术资源、Effect 系统）。
- `HandState` 演化为真正的战斗层 `BattleState`，并接入 R3 响应式属性。
- Effect 执行链：`Card → Effect → BattleState`，让"力量+3"测试牌真正生效。
