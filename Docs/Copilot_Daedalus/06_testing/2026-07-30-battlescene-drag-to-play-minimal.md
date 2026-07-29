---
title: BattleScene 拖拽出牌（最小判定）验证记录
page_type: testing
lifecycle: active
date: 2026-07-30
scope: TinySpire/Assets/Scripts/UI/Battle/Hand/
source: 实现计划、纯状态检查、静态编译、UnityMCP Play Mode 与用户 Game View 手工验收
status_source: ../SESSION_LOG.md
---

# BattleScene 拖拽出牌（最小判定）验证记录

## 已完成验证

| 检查 | 方法 | 结果 |
|---|---|---|
| `HandState` 行为 | 以初始 ID `0,1,2` 直接调用 `PlayCard(1)` 两次 | 第一次成功并留下 `0,2`；第二次失败；`Changed` 仅触发一次；调用前快照仍为 3 项。 |
| 手牌数量归属 | 审查 `HandCardContainer` | Inspector 的 `initialHandCount` 仅传入 `HandState` 构造函数；运行期层级与布局张数读取 `HandState.CardIds.Count`。 |
| 拖拽坐标路径 | 审查 `HandCardContainer.HandleDrag` | 只调用 `FollowPointerDelta(eventData.delta)`；不再使用根 Canvas 的 `ScreenPointToLocalPointInRectangle`，避免跳到 `(0,0)`。 |
| 出牌线判定 | 审查 `HandCardContainer.HandleEndDrag` | 仅在 `CurrentAnchoredY > playLineY` 时调用 `HandState.PlayCard`；否则执行原有重排回弹。 |
| 视觉重建 | 审查 `HandState.Changed` 订阅 | 状态变更时缺失 ID 的 `HandCardVisual` 调用 `Destroy`，其余 ID 按状态快照顺序重排。 |
| 越线反馈 | 审查 `HandleDrag` 与 `HandCardVisual` | 拖拽时根据 Y 阈值切换 `CanvasGroup` 透明度，离开阈值或松手时恢复。 |
| 依赖项标记 | 检索 `TODO(DEP-00` | 共 4 处：DEP-001 至 DEP-004 各一处。 |
| 编译 | `dotnet build TinySpire/TinySpire.sln --no-restore` | 通过：0 错误；9 条既有第三方程序集版本冲突警告。 |
| Unity Play Mode | UnityMCP Console | Console 为 0 条错误、0 条警告。 |

## 手工交互验收

用户已在 Game View 手工确认以下交互：

- 按住任意卡牌并移动时，卡牌保持抓取偏移、持续跟随鼠标，且不跳到屏幕中心。
- 将卡拖过 `playLineY` 后松手时，卡牌销毁、其余手牌补位，并出现透明度反馈；线内松手会回弹并恢复透明度。

## 结论

纯状态、编译、Unity 运行初始化与 Game View 手工交互验收均已通过。拖拽坐标实现已排除上一版的零尺寸根 Canvas 绝对坐标换算问题；Phase 1 无剩余验收项。
