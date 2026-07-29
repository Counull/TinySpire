---
title: BattleScene 手牌 UI（杀戮尖塔式）验证记录
page_type: testing
lifecycle: active
date: 2026-07-29
scope: TinySpire/Assets/Scenes/BattleScene.unity
source: 实现计划、静态编译与 UnityMCP 连接状态
status_source: ../SESSION_LOG.md
---

# BattleScene 手牌 UI（杀戮尖塔式）验证记录

## 已完成的静态验证

| 检查 | 方法 | 结果 |
|---|---|---|
| C# 编译 | `dotnet build TinySpire/TinySpire.sln --no-restore` | 通过：0 错误；9 条既有第三方程序集版本冲突警告。 |
| 扇形布局 | 审查 `HandCardLayout.Calculate` | `t∈[-1,1]`，旋转为 `-t × maxFanAngle`，下沉为 `-verticalDrop × t²`；单卡居中，卡牌轴线向手牌下方汇聚。 |
| 扇轴方向 | 以纯布局测试计算 3 张手牌 | 修正前左 `-15°` / 右 `15°`（轴线朝上）；修正后左 `15°` / 右 `-15°`，测试通过。 |
| 溢出压缩 | 审查布局计算 | 间距为 `min(baseSpacing, maxHandWidth / (count - 1))`；未按数量缩放卡牌。 |
| Tween 生命周期 | 审查 `HandCardVisual` | 每张实例各自持有 Tween，新动作前调用 `Kill()`。 |
| 旧单选 UI | 检索 `BattleScene.unity` | 未发现 `Toggle` 或 `ToggleGroup`。 |
| 范围 | 检查变更清单 | 未修改 `CardView.prefab`；未新增依赖、未接入真实数据或出牌逻辑。 |

## Unity Play Mode 验收

| 检查 | 方法 | 结果 |
|---|---|---|
| 3 张手牌 | UnityMCP Game View 截图 | 居中对称扇形，左右卡旋转与下沉正常。 |
| 5 张手牌 | UnityMCP Game View 截图 | 默认 5 张扇形可见，卡牌层叠顺序正确。 |
| 10 张手牌 | UnityMCP Game View 截图 | 间距明显压缩、卡牌显示比例不随数量改变，仍处于屏幕安全区。 |
| 扇轴方向 | UnityMCP Game View 截图 | 5 张手牌的左右卡牌向外上扬，延长轴线在手牌下方汇聚。 |
| Console | 最后一次 Play Mode 读取 | 0 条错误、0 条警告。 |

## 待人工交互确认

`HandCardInteraction` 通过标准 `IPointerEnterHandler` / `IPointerExitHandler` / `IBeginDragHandler` / `IDragHandler` / `IEndDragHandler` 转发事件，代码级检查覆盖了 Tween 终止、排序恢复、补位和原 index 回弹。当前 UnityMCP 没有指针事件注入能力，因此以下两项没有伪造为已完成的手工验收：

- hover 的抬起、归零旋转、缩放和层级恢复。
- 拖拽的鼠标跟随、其余牌补位、松手后原 index 回弹。

## 结论

代码级、场景序列化、3 / 5 / 10 张 Game View 和 Console 验收通过。hover 与拖拽的最终交互手感需在 Game View 手动确认。
