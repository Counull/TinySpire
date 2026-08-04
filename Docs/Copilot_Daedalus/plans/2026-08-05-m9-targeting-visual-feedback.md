---
title: M9 目标箭头与锁定框视觉反馈
page_type: plan
lifecycle: active
created: 2026-08-05
updated: 2026-08-05
status: implemented-validated
scope: 出牌目标箭头与怪物锁定框的表现形态
status_source: ../SESSION_LOG.md
source: 用户 2026-08-05 反馈及随后直接实施授权
---

# M9 目标箭头与锁定框视觉反馈

## 实施结果（2026-08-05）

用户随后授权直接实施，本页所列两项已完成并通过定向 EditMode 验证：

- `BattleTargetingArrowView` 保持 `Show`、`UpdateArrow`、`Hide` 的外部调用面；内部改为三次贝塞尔曲线采样、独立箭头和可复用的多段箭身。每个箭身 fragment 与终点箭头均按所在位置的曲线切线旋转。
- `ParticipantHudView` 的合法与悬停锁定框各改为四个独立角件；运行时投影怪物 `SpriteRenderer.bounds` 的八个世界角，按实际屏幕边界加可调 16 像素留白后定位框体。
- 两个 Prefab 继续复用正式 Targeting 素材；未修改 `TinySpire/Assets/Arts/Runtime/UI/Battle/Targeting/` 内的源图片或 Meta。

完整自动化结果见 `../06_testing/2026-08-05-m9-targeting-visual-feedback.md`。

## 当前结论

本页只记录用户连续反馈，不实施代码、Prefab 或美术资源修改；待用户明确“反馈完成”后，再以本页为唯一需求输入确认最小实现范围。

## 已确认的视觉口径

### 攻击箭头

- 箭头分为独立的箭头（head）和箭身（shaft），不是单张拉伸的长图。
- 箭身由多个 fragment 组成，沿目标路径顺序排布。
- 每个 fragment 的朝向采用它所在路径位置的局部切线；若路径以 `P(t)` 表示，方向为 `normalize(P(t + ε) - P(t - ε))`。
- 箭头朝向采用路径终点的切线，不用起点到终点的直线方向替代曲线局部方向。

### 怪物锁定框

- 锁定框位于怪物视觉的后方，而不是盖在角色正面。
- 它由四个独立角件组成，四角共同围住怪物；不是一张完整矩形底图。
- 框的包围范围应以当前怪物视觉边界为依据，并预留可调留白；具体留白、角件尺寸与动画待后续反馈确定。

## 仍待反馈确认

- 箭身 fragment 的间距、数量/密度、首尾留白、淡入淡出与是否随距离自适应。
- 目标路径的控制点形状与箭头/箭身使用的现有正式素材。
- 锁定框四角的精确样式、颜色、呼吸/缩放反馈，以及多目标/死亡过渡时的收口规则。

## 范围边界

- 当前不修改 `TinySpire/Assets/Arts/Runtime/UI/Battle/Targeting/` 或其 Meta，也不触碰 Candidates 资源。
- 当前不修改目标合法性、`BattleCommandQueue`、Turn、settlement、卡牌运动、Scene、Canvas、Prefab 或 Addressables。
- 后续实施必须复用既有 target-selection/arrow seam，并以最小范围单独补充测试、验收与资源变更证据。
