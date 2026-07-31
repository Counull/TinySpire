---
title: 战斗 UI 首批美术与牌面配置链路接入
page_type: plan
lifecycle: completed
updated: 2026-07-31
---

# 战斗 UI 首批美术与牌面配置链路接入

## 目标

把素材提交 `688e49955e19423a1821313c0cc8608eadaaf80f` 与 `1c0619772291f3101fc8eb27838be3c607b5257b` 中已经具备运行时事实支撑的战斗素材接入现有 UI，并让四张牌面经过 Luban 与 Addressables 的正式内容链路加载。

## 范围

- P0：抽牌堆、弃牌堆、消耗牌堆共用计数面板与各自图标；参与者生命框、横向生命填充与力量图标。
- P4：力量、打击、防御、猛击四张牌面；卡牌模板保存完整稳定地址，手牌运行时异步加载。
- 保留 `BattleSession`、`BattleCardZonesData`、`CombatantData` 与 Localization 的既有唯一事实来源，不创建展示状态副本。

## 非范围

- 不接入 P1-P3 的能量、结束回合、敌人意图、格挡/状态、死亡、胜败与结算覆盖层；它们仍等待 M4-M9 的运行时事实。
- 不改动卡牌费用、目标、效果执行、拖拽判定、扇形布局或本地化正文。
- 不替换用户现有的 `CardView` Stencil Mask 旋转裁剪修复。

## 实现

1. 通过 Unity MCP 把 P0 SVG 导入为 UI Sprite，为生命框与计数面板配置九宫格 border；修改 `ParticipantHudView.prefab` 与 `BattleScene.unity` 的现有展示结构。
2. 使用用户授权的 OpenXML 定向为 `DataTables/Datas/battle.card.xlsx` 新增 `illustration_address`；仅 worksheet 与 sharedStrings 条目发生变化。
3. 运行与 `DataTables/gen.bat` 等价的 Luban 命令，生成 `Card.IllustrationAddress` 与 `Assets/GameData/battle_tbcard.json`。
4. 四张牌面统一为 `Sprite / Single / no mipmap`。`AddressablesBuildTools` 从生成 JSON 收集、去重并校验牌面地址，使 `TinySpire Card Art` 本地组与配置集合完全同步并清除旧条目。
5. `HandCardContainer` 按本场牌组唯一模板预加载并持有 Sprite handle；`HandCardVisual` 让横向牌面保持原始比例 cover 插图区，再由用户既有的 Stencil Mask 裁切；容器销毁时释放全部 handle。

## 影响层

- 配置：卡牌模板新增牌面地址。
- 资源：P0 UI Sprite 导入设置、四张牌面导入设置与 Addressables 本地组。
- UI：参与者 HUD、牌堆 HUD、手牌牌面展示。
- 时序：手牌首次创建前等待本场牌面预加载；失败直接记录异常并禁用容器，不使用占位回退。
- 领域/战斗计算：无修改。

## 回滚

撤销本计划涉及的表格列、生成输出、牌面资源组、三处手牌代码接线以及 P0 Prefab/Scene 修改，然后重新运行 Luban 与 `TinySpire/Addressables/Build Local Content`。不要撤销同一 `CardView.prefab` 中用户已有的 `Mask` 旋转裁剪修改。

## 验收点

- 生成卡牌 JSON 的四个模板均存在完整 `Assets/.../card_art_*.png` 地址。
- 四张 PNG 可作为主 `Sprite` 加载，且牌面组恰好包含四个稳定地址。
- Bootstrap 进入 BattleScene 后初始 5 张手牌均有非空牌面，比例无拉伸并完整 cover 遮罩；无 `InvalidKey` 或资源加载异常。
- 牌堆计数和参与者 HUD 保持读取既有运行时事实。
- Luban、全量 EditMode 与 Addressables 本地构建通过。

## Open Question

无。P1-P3 是否接入由对应运行时里程碑单独决定。
