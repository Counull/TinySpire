---
title: BattleScene M3B 卡牌堆计数 HUD 验证记录
page_type: testing
lifecycle: active
date: 2026-07-30
source: ../plans/2026-07-30-battlescene-card-pile-hud.md
status_source: ../SESSION_LOG.md
---

# BattleScene M3B 卡牌堆计数 HUD 验证记录

## 已执行

- `DataTables/gen.bat`：成功完成，`Assets/GameData/` 已按当前表格重新生成。
- `i18n.xlsx` 结构检查：`battle.card_pile.draw.name`、`battle.card_pile.discard.name`、`battle.card_pile.exhaust.name` 存在于第 14～16 行，工作表范围已扩展到 `A1:D16`。
- 静态程序集编译：`Assembly-CSharp.csproj` 与 `Assembly-CSharp-Editor.csproj` 均为 0 error；保留既有程序集版本冲突警告（分别为 6、12 条）。首次并行编译因共享 `obj` 输出文件锁定失败，已改为串行复跑并通过。
- `BattleCardPileHudPresentationTests` 已加入 EditMode 测试程序集并通过编译，覆盖中英文标签与计数的两行展示格式。
- `BattleScene.unity` 静态引用检查：`BattleCardPileHud` 的全部场景本地 ID 唯一，且脚本 GUID 与两个 `Text` 引用完整。
- `git diff --check`：通过；仅有既有生成文件的 CRLF 转换提示，无空白错误。

## 待在现有 Unity Editor 人工验收

1. 执行 `TinySpire/Build/Sync and Build All`，导入 Excel 新增 key、执行本地化校验并重建 Addressables 本地内容。
2. 从 Bootstrap 进入 BattleScene：英文显示 `Draw Pile\n5`、`Discard Pile\n0`、`Exhaust Pile\n0`；中文显示 `抽牌堆\n5`、`弃牌堆\n0`、`消耗牌堆\n0`。
3. 将一张手牌拖过当前最小打出线：弃牌堆数字变为 `1`，抽牌堆仍为 `5`、消耗牌堆仍为 `0`；未来通过 `ExhaustFromHand` 触发的移动应只令消耗牌堆数字增加。
4. 再次进入和退出 BattleScene，确认 HUD 与其 R3 订阅随场景销毁，不保留跨场景对象。

## 未实施

- 未实现 M3C 的能量和结束回合、M3D 的敌人意图，或 M3E 的格挡/状态/死亡/胜败层。
- 未实现抽牌、弃牌或重洗动画；本切片只显示已发生的卡区事实。
