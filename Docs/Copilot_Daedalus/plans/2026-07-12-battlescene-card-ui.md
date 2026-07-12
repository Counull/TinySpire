---
title: BattleScene 基础手牌 UI
page_type: plan
lifecycle: active
date: 2026-07-12
scope: BattleScene MVP · UI
source: 用户请求与现有 CardView.prefab
status_source: ../SESSION_LOG.md
---

# BattleScene 基础手牌 UI

## 目标

在不实现卡牌运行时代码的前提下，为 `BattleScene` 构造一套可直接观察和点击选择的基础手牌 UI。

## 影响层

- 计算层：不影响。
- 状态层：不影响；选择仅保存在 UGUI `Toggle.isOn`。
- 时序层：不影响。
- UI 层：在现有 Canvas 下增加手牌安全区、5 个卡槽、`CardView` 实例和单选高亮。

## 实现方案

1. 复用 `TinySpire/Assets/Arts/Runtime/Card/Prefab/CardView.prefab`，不修改预制体本体。
2. 在 1920×1080 基准安全区底部放置半透明手牌托盘，保留中上部战斗展示空间。
3. 每个卡槽使用 UGUI `Toggle`，同属一个不允许全部关闭的 `ToggleGroup`；第一张默认选中。
4. 通过卡槽底层红色选中面板表现当前选择，悬停与按下继续使用 `Selectable` 原生颜色过渡。
5. 将 Screen Space - Camera Canvas 的 `planeDistance` 设为 1，确保 UI 位于背景 Sprite 之前。

## 边界与风险

- 本轮不实现动态手牌、卡牌数据、ViewModel、出牌按钮、效果链或战斗状态。
- 5 张牌沿用预制体内已有占位内容，不新增卡牌文案。
- 每张 `CardView` 当前自带嵌套 Canvas；MVP 可接受，后续若手牌数量或批次显著增加，再评估合批与预制体结构。
- 运行时业务层接入后，应把 UGUI 选择状态同步到明确的 ViewModel/命令入口，而不是让战斗逻辑直接读取 Toggle。

## 验收点

- Game View 中能完整显示 5 张卡牌，且不遮挡主要战斗展示区域。
- 任意时刻只有一个卡槽处于选中状态。
- 点击另一张卡后，高亮从旧卡切换到新卡。
- Unity Console 无新增错误或警告。
- 不新增或修改运行时 C# 文件，不修改 `CardView.prefab`。

## 当前结论

上述 UI 已在 `BattleScene` 中完成并通过 Game View 与 EventSystem 点击验证。动态数据与业务选择接入留给后续独立切片。
