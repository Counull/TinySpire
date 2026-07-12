---
title: BattleScene 基础手牌 UI 验证
page_type: testing
lifecycle: active
date: 2026-07-12
scope: TinySpire/Assets/Scenes/BattleScene.unity
source: Unity 6000.5.2f1 Editor Game View 与 EventSystem
status_source: ../SESSION_LOG.md
---

# BattleScene 基础手牌 UI 验证

## 验证范围

- `BattleCardUI` 场景层级和 1920×1080 安全区布局。
- 5 个 `CardView` 实例的可见性与完整性。
- `ToggleGroup` 单选行为和选中高亮切换。
- Screen Space - Camera Canvas 与背景 Sprite 的遮挡关系。
- Unity Console 状态及 Play Mode 退出状态。

## 验证步骤与结果

| 检查 | 方法 | 结果 |
|---|---|---|
| 初始画面 | Unity Game View 截图 | 5 张卡牌完整显示在底部托盘；第一张显示红色选中底框。 |
| Canvas 遮挡 | 对比 `planeDistance=100` 与 `planeDistance=1` | 100 时 UI 位于背景之后并被遮挡；调整为 1 后正常显示。 |
| 单选交互 | Play Mode 中通过 EventSystem 向 `CardSlot_02` 发送标准指针点击 | `CardSlot_01=False`，`CardSlot_02=True`；高亮切换到第二张。 |
| Console | 读取 error 与 warning | 0 条错误，0 条警告。 |
| 编辑器状态 | 交互验证后退出 Play Mode | 已退出，`BattleScene` 已保存且非 dirty。 |
| 范围检查 | Git diff 与文件列表 | 场景实现仅修改 `BattleScene.unity`；未修改脚本与 `CardView.prefab`。 |

## 结论

基础手牌展示和 UI 单选行为通过验收。尚未验证动态卡牌生成、业务选择同步、出牌流程与非 16:9 极端比例；这些不属于本次纯 UI 构造范围。
