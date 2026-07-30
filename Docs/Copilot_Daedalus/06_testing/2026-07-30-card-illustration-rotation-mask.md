---
title: CardView 旋转插图裁剪修复验收
page_type: testing
lifecycle: active
date: 2026-07-30
source: ../plans/2026-07-30-card-illustration-rotation-fix.md
status_source: ../SESSION_LOG.md
---

# CardView 旋转插图裁剪修复验收

## 静态验证

- `IllustrationMask` 仍保留原有 `Image`、682 x 575 RectTransform 与 `Illustration` 子节点。
- 裁剪组件已由 `UnityEngine.UI.RectMask2D` 替换为 `UnityEngine.UI.Mask`。
- `m_ShowMaskGraphic` 为 `0`，遮罩 Graphic 不会额外覆盖卡图。

## 待当前 Unity Editor 人工验收

1. 在 BattleScene 观察左右旋转手牌，确认插图区无灰色底图外露。
2. 悬停任意倾斜卡并回到手牌，确认归零与恢复旋转时插图完整。
3. 执行 `TinySpire/Addressables/Build Local Content`，确认 Console 无新增错误。

## 未执行项

- 未启动新的 Unity Editor 或批处理实例；检测到用户现有 Unity 进程正在运行。
- 因此尚未完成 Game View 人工验收及本地 Addressables 内容重建。
