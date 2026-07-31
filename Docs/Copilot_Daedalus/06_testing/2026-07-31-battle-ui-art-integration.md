---
title: 战斗 UI 首批美术与牌面配置链路接入验收
page_type: testing
lifecycle: verified
updated: 2026-07-31
---

# 战斗 UI 首批美术与牌面配置链路接入验收

## 环境

- Unity `6000.5.5f1`，WindowsEditor。
- MCP for Unity `10.1.0`，使用用户已打开的 `TinySpire` Editor；未启动或结束其他 Editor。
- 活动入口：`Assets/Scenes/BootstrapScene.unity`。

## 配置与资源检查

- OpenXML 修改前后逐条目 SHA-256 对比：`battle.card.xlsx` 只有 `xl/worksheets/sheet1.xml` 与 `xl/sharedStrings.xml` 改变。
- H 列为 `illustration_address`；3001-3004 分别指向 strength、strike、defend、bash 的完整 `Assets/Arts/Runtime/Card/card_art_*.png` 地址。
- Luban 生成成功，`Assets/Scripts/Core/Generated/Config/battle/Card.cs` 含 `IllustrationAddress`，`Assets/GameData/battle_tbcard.json` 含四条对应地址。
- 四张牌面均验证为 `TextureImporterType.Sprite`、`SpriteImportMode.Single`、`mipmapEnabled = false`。
- Unity MCP 验证 `TinySpire Card Art` 组恰好包含上述四个稳定地址。

## P0 静态检查

- `ParticipantHudView.prefab`：生命框使用 `ui_battle_health_frame.svg` 的 Sliced Image；生命填充使用 `ui_battle_health_fill.svg` 的 Horizontal Filled Image；力量区域使用 `ui_battle_icon_strength.png`。
- `BattleScene.unity`：Draw/Discard/Exhaust 三个现有文本引用保留，各自使用共用 `ui_battle_pile_counter_panel.svg` 与对应牌堆图标。

## 自动化结果

- 牌面配置定向 EditMode：`1/1` 通过。
- 全量 EditMode：`35/35` 通过，失败 0，跳过 0。
- 最终 `TinySpire/Addressables/Build Local Content`：成功，耗时 `19.026s`，`BuildError` 为空；生成含四张牌面的 `tinyspirecardart` bundle。首次构建菜单调用期间 MCP WebSocket 回执曾短暂断开，但 Unity 构建记录与产物均确认成功。

## 运行时验收

- 从 Bootstrap 进入 BattleScene，场景成功加载。
- 找到 5 个运行时 `HandCardVisual`；每个 `_illustrationImage` 均有非空 Sprite，实际地址来自 strike、bash、defend 三种本场初始手牌模板。
- 五张横向牌面均以 `862.5×575` 显示覆盖 `682×575` 遮罩；显示比例与 Sprite 原始 `1.5` 比例一致，居中且 `preserveAspect = true`，未发生竖向拉伸。
- Unity Console：Error `0`；`InvalidKey` `0`；`Failed to load card` `0`。
- 验收后 Editor 已退出 Play Mode，活动场景恢复为 BootstrapScene，状态 idle。

## 结论

P0 与 P4 在当前已实现的战斗事实范围内接入完成。P1-P3 未实施，不计为本次缺陷或遗漏。

交付前双轴复审结果：Standards finding `0`，Spec finding `0`。初审发现的横向牌面拉伸、已完成计划路由和 Addressables 陈旧条目问题均已修正并复审关闭。
