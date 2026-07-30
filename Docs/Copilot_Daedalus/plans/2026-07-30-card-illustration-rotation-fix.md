---
title: CardView 旋转插图裁剪修复
page_type: plan
lifecycle: active
date: 2026-07-30
scope: BattleScene 手牌 CardView
source: 用户报告的旋转卡图灰边
status_source: ../SESSION_LOG.md
---

# CardView 旋转插图裁剪修复

## 当前结论

`CardContent` 旋转时，`IllustrationMask` 的 `RectMask2D` 会以轴对齐区域裁剪子图，露出 `CardBase`。改用不显示自身 Graphic 的 UGUI `Mask`，让遮罩与卡片一起旋转。

## 实施范围

- 修改 `TinySpire/Assets/Arts/Runtime/Card/Prefab/CardView.prefab`。
- 保留 `IllustrationMask` 的 `Image`、RectTransform 和 `Illustration` 子节点。
- 将 `RectMask2D` 替换为 `Mask`，并设为 `m_ShowMaskGraphic: 0`。

## 非目标

- 不改动 `HandCardVisual`、手牌扇形角度、贴图资源、场景、数据表或 Addressables 配置。

## 验收

1. 中央未旋转卡图显示保持不变。
2. 左右旋转卡图的插图区不再露出灰色 CardBase。
3. 悬停归零旋转与拖拽状态的卡图裁剪正常。
4. 当前 Unity Editor 重建 `TinySpire/Addressables/Build Local Content` 后，Console 无新增错误。
