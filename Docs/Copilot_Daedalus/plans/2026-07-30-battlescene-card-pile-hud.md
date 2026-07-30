---
title: BattleScene M3B 卡牌堆计数 HUD
page_type: plan
lifecycle: active
date: 2026-07-30
scope: BattleScene、BattleCardZonesData、i18n.xlsx、Addressables 本地内容
source: M3 路线图中 M3B 的后续实施请求
status_source: ../SESSION_LOG.md
---

# BattleScene M3B 卡牌堆计数 HUD

## 目标

在 BattleScene 底部显示抽牌堆、弃牌堆与消耗牌堆的当前数量。数字始终从 `BattleCardZonesData.Layout` 的已发布完整快照派生；UI 不持有计数、卡牌 ID 列表或区域归属的第二份可变状态。

## 实施边界

- 新增一个场景内 `BattleCardPileHudView`，显示三个只读文本：抽牌堆、弃牌堆、消耗牌堆。
- View 注入 `BattleSession` 与 `LocalizationService`；订阅 `CardZones.Layout` 和 `LocaleChanged`。
- 每次布局发布时直接读取 `layout.DrawPile.Count`、`layout.DiscardPile.Count`、`layout.ExhaustPile.Count` 重派生文本；语言变化时仅重取标签并以当前布局重新格式化。
- 标签写入现有 `DataTables/Datas/i18n.xlsx`：`battle.card_pile.draw.name`、`battle.card_pile.discard.name`、`battle.card_pile.exhaust.name`，并由现有 Unity Localization 导入链路提供运行时文本。
- HUD 固定在主 Canvas 底部左右两侧；不新增可点击行为、卡牌动画、重洗动画、能量、结束回合或假定的牌堆美术。

## 唯一事实与生命周期

| 项目 | 归属 |
|---|---|
| 卡牌实例与区域顺序 | `BattleCardZonesData` |
| 抽牌堆 / 弃牌堆 / 消耗牌堆数量 | 从 `CardZoneLayoutData` 四个区域列表按需派生 |
| 当前语言 | `LocalizationService` |
| HUD 文本 | 上述事实的短生命周期渲染结果 |

场景卸载时 `BattleCardPileHudView` 的 R3 订阅随 GameObject 自动释放；它不需要、也不得手工修改卡区。

## 验收

1. 首次进入战斗显示洗牌、初始抽牌后的抽牌堆数量 `5`、弃牌堆数量 `0` 与消耗牌堆数量 `0`。
2. 将一张手牌拖过当前最小打出线后，抽牌堆计数不变，弃牌堆计数加一；未来通过 `ExhaustFromHand` 移动的卡牌应只增加消耗牌堆计数。
3. 语言切换时标签变为当前语言，计数不改变。
4. UI 不保存独立计数；`BattleCardZonesData` 仍是唯一写入入口。
5. 运行 `TinySpire/Build/Sync and Build All` 后，Addressables 本地内容包含更新后的本地化资源，Bootstrap → BattleScene 不出现 `InvalidKey` 或地址错误。

## 后续边界

M3C 需要 M4 的回合阶段和能量事实；M3D 需要 M5 的敌人意图事实；M3E 需要 M7～M9 的格挡、状态、死亡与结算记录。它们不在 M3B 中预置字段、面板或占位状态。
