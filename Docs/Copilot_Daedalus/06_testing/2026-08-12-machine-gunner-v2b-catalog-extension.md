---
title: Marine Game 机枪兵 V2B 82 模板目录扩展
page_type: testing
lifecycle: active
date: 2026-08-12
updated: 2026-08-12
status: verified-unity-native-2026-08-12
status_source: ../SESSION_LOG.md
source:
  - ../00_inbox/README web.md
  - ../01_requirements/2026-08-12-machine-gunner-v2-requirement-digest.md
related_plan: ../plans/2026-08-12-machine-gunner-v2-card-runtime-plan.md
related_decision: ../CODE_DECISIONS.md#cd-081v2-扩展目录以独立快照冻结-catalogonly-身份
---

# Marine Game 机枪兵 V2B 82 模板目录扩展

## 1. 验收对象与冻结行为

本记录覆盖新版 README 的目录扩展，不覆盖新增卡的战斗程序。目标是把目录从既有 64 个模板补齐为 82 个模板，同时让尚未实现行为的卡保持不可打出。

- 新增 3265–3282 共 18 个奖励模板，以及 Program 65–82、双语标题/描述、升级元数据和占位插图键。
- V2B 初始录入时，新身份使用 `marine-game-v2-20260812-cards` 快照，全部为 `CatalogOnly`、`art_placeholder`、空 `effect_bindings` 且 `has_upgrade=true`。
- 既有 V1 的 64 模板快照仍独立校验。当前扩展快照精确允许 ThermiteBomb (3273) 和 Crush (3281) 为 `Implemented`，其余 16 张保持 `CatalogOnly`；当前总数为 48 `Implemented` / 34 `CatalogOnly`。
- V2B 初始录入时，新模板没有运行时 Program 注册、没有加入默认 Deck、奖励、Run 或升级实例流程；V2C 只为 ThermiteBomb 和 Crush 补入基础态程序，剩余 16 张 `CatalogOnly` 仍在程序查询前被拒绝。

## 2. 已变更表面

| 层 | 变更 |
|---|---|
| 作者表 | `battle.card.xlsx` 新增 18 个 V2 奖励身份；`i18n.xlsx` 补齐 54 条中英文本；`__enums__.xlsx` 扩展 MachineGunner Program 65–82。 |
| 生成配置 | Luban 生成 82 项 `battle_tbcard.json` 与 Program enum；`game-config.json` 按既有生成器行为恢复为作者版本。 |
| 构建门禁 | `BattleCardCatalogBuildValidator` 保留 V1 64 模板校验，并新增 V2 扩展快照、连续编号和精确实现状态门禁。 |
| 回归 | 新增 `MachineGunnerCatalogSnapshotV2BTests`，覆盖全部扩展元数据/精确状态和把 Mark 非法翻为 Implemented 的负向门禁。 |

## 3. 已加入的回归

| 用例 | 锁定事实 |
|---|---|
| `GeneratedCatalog_MarineGameV2ExtensionKeepsAuthoredMetadataAndImplementationGate` | 18 个扩展身份的 ID、Program、类型、费用、目标、归宿、升级、占位图、空 binding 与精确实现状态。 |
| `SnapshotValidator_ImplementedV2ExtensionCard_Throws` | 将 Mark 改为 Implemented 时，V2 扩展构建门禁给出稳定失败。 |
| 既有 `MachineGunnerCatalogSnapshotMG2ATests` | V1 64 模板/46 Implemented 快照不因 V2B 扩展而漂移。 |

## 4. 本轮证据与结论

| 项目 | 结果 | 证据/说明 |
|---|---|---|
| Luban 与生成配置 | 通过 | `battle_tbcard.json` 中的机枪兵 V1+V2 快照现为 82 项，并生成 Program 65–82；随后恢复生成器移除的 `game-config.json`。 |
| 本地化 | 通过 | Unity 菜单 `TinySpire/Localization/Import Battle Card Text from Excel` 输出 `TinySpire battle card localization imported from Excel and validated.` |
| Sync 与本地 Addressables | 通过 | 单一既有 Unity 6000.5.5f1 Editor 的 `TinySpire/Build/Sync and Build All` 输出 Addressable content built（18.781 秒）和整体同步成功。 |
| Unity 定向 EditMode | 通过 | MCP 任务 `40db6aacf30e4bfbbebfe725d734695f`：112/112 passed，0 failed，0 skipped，0.794531 秒。 |
| Unity 完整 EditMode | 通过 | MCP 任务 `a5646a7a960d43348acdf39083b23f95`：586/586 passed，0 failed，0 skipped，18.2594869 秒。 |

## 5. 验收后边界

本切片在 V2B 时只完成身份目录、生成数据、本地化与防误开放门禁。V2C 已为 ThermiteBomb 和 Crush 单独补入程序与战斗测试；剩余 16 张新模板没有程序注册或战斗测试，不能从手牌打出。其标签、即时状态、延迟效果、随机/选择、临时卡、AnyAlly、升级实例、奖励/Run、UI、Scene、Prefab 和第二条写入链均留给后续独立切片。
