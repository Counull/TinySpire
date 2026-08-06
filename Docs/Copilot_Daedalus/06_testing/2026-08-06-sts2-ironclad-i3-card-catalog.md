---
title: STS2 Ironclad I3 85 张目录与占位素材
page_type: testing
lifecycle: active
created: 2026-08-06
updated: 2026-08-06
scope: STS2 v0.107.1 Ironclad 单人卡池 I3
status_source: ../SESSION_LOG.md
---

# STS2 Ironclad I3 85 张目录与占位素材

## 验收结论

I3 验收时 I4 尚未开始。冻结快照 `sts2-v0.107.1-23811903-59260271` 的 85 张单人战士卡均已进入 `battle.Card`：当时 3 张现有 STS2 卡为 `Implemented`，其余 82 张为 `CatalogOnly`；加上 TinySpire 自有 `Strength`，生产 Card 表当时共 86 行、4 张 `Implemented`、82 张 `CatalogOnly`。I4 的后续状态见 [成功归宿与 Tremble 验收](2026-08-06-sts2-ironclad-i4-success-destination.md)。

“录入”不等于“可玩”。82 张 `CatalogOnly` 继续在既有 Queue 规则入口以 `CardNotImplemented` 零写入失败，没有空程序扣费、移动卡区或修改参与者事实。I3 没有实现 Twin Strike、Bludgeon 或其他新卡效果。

## 冻结目录与 schema

- 外部来源固定为 v0.107.1 / Steam build `23811903` / commit `59260271` 的 85 张单人快照；明确排除多人专用 `DEMONIC_SHIELD` 与 `TANK`。
- `battle.Card` 在既有字段上新增稳定 `external_key`、`catalog_snapshot_key`、类型、稀有度、Fixed/X 费用类型、升级费用、基础/升级成功归宿、升级说明 key 与 `has_upgrade`。
- 类型聚合为 Attack 37 / Skill 29 / Power 19；稀有度为 Basic 3 / Common 20 / Uncommon 35 / Rare 25 / Ancient 2；目标为 Self 45 / Enemy 32 / AllEnemies 7 / RandomEnemy 1；基础归宿为 Discard 56 / Exhaust 10 / Power 19；X 费身份仅为 `CASCADE` 与 `WHIRLWIND`。
- `TargetRule` 只扩展目录表达能力。卡牌的 AllEnemies/RandomEnemy 仍因 `CatalogOnly` 不可玩；敌人行为初始化继续只允许 Self/Enemy，避免新增枚举被误判为已实现玩法。
- 目录说明采用项目自有的 en/zh-CN 结构化表述，没有提交完整官方规则原文。

## 美术与 AssetBundle

没有生成、下载或引用任何 STS2 官方卡图，也没有保留此前试生成的图片。82 张缺图卡统一复用仓库既有灰色占位图 `Assets/Arts/Runtime/Card/Texture/art_placeholder.png`，配置只保存短键 `art_placeholder`，运行时地址为 `card-art/art_placeholder`。

占位图已由当前唯一 Unity Editor 校验为 `Sprite / Single / no mipmap`。`AddressablesBuildTools` 只显式纳入这一条既有 Texture 路径，没有扩大正式 `Illustrations` 根目录扫描；`TinySpire Card Art` 最终为 5 个地址、PackTogether、`AssetBundleProvider`。真实 `Addressables.LoadAssetAsync<Sprite>` 已加载四张正式牌面与占位图。

82 张原创替代素材的逐卡短键/文件名清单见 `../10_communication/2026-08-06-sts2-ironclad-card-art-checklist.md`。后续 Agent 不得自行生成或下载卡图；没有用户提供的原创素材时继续占位并维护清单。

## 双语与构建门禁

- 三张作者工作簿经 Artifact Tool 候选检查与渲染后写入：Card 90 行（含四行 Luban 表头）、枚举 33 行、i18n 273 行；公式/错误扫描均为 0。
- 首次候选复跑发现 TargetRule 项重复，脚本已改为幂等；首次 Luban 暴露枚举嵌套表头合并丢失，恢复 `H1:L1` merge 后 `DataTables/gen.bat` 成功完成 validation、C# 与 JSON 生成。
- `BattleCardCatalogBuildValidator` 在 Localization 前校验 85 个精确外部身份、全表身份唯一、聚合、X 费、升级字段、3/82 实现状态、多人卡排除与占位短键。
- Card 的 name / description / upgraded description 共 258 个唯一 key，全部存在于 en 与 zh-CN，说明项均为 Smart String；`TinySpire/Build/Sync and Build All` 已完成 Luban、清单门禁、Localization 同步与 Local Addressables 构建。

## 自动验证

| 层级 | 结果 | 证据 |
|---|---:|---|
| 精确红灯 | 0/1 | 任务 `f7b8315680f54d539e70a446d989b1fa`：期望 85、实际 0 |
| I3 身份/漂移/双语 | 5/5 | 任务 `c3a9df29333e45448edf17447a4f84fc` |
| I2+I3 构建门禁 | 10/10 | 任务 `078210dc2aad49d2894a404a29bab357` |
| 敌人目标 fail-fast | 20/20 | 任务 `c865f8dab8f145b8bf5666f1e5174798` |
| 牌面静态与真实 AB 加载 | 5/5 | 任务 `0b6e50d98b7245098c312d06839fed8e`；四张正式牌面加 `art_placeholder` |
| 新 Card 构造字段相关回归 | 86/86 | 任务 `b0fe67cd2f45443a82a0d51de15c2d8c`，只补七类手写 JSON 夹具 |
| 完整 EditMode | 479/479 | 任务 `7e7738c02c4c411294596b1e9d040324`，0 failure |
| Solution build | 通过 | `dotnet build TinySpire.sln --no-restore`：0 error、12 条既有程序集版本冲突 warning |
| 同步与 Local Content | 通过 | Console：`TinySpire sync and local content build completed successfully.`；Addressables 构建约 31.002 秒 |

真实 AB 测试在 Editor 未聚焦时由服务短暂标记 `editor_unfocused`，但未抢焦点、未改 ProjectSettings；同一任务最终 5/5 通过。最终 Console `error` 类型过滤保留 2 条 TestRunner/验证成功记录：`TinySpire battle card localization validation passed.` 与保存 `TestResults.xml`，二者都不是编译或运行时失败；`InvalidKey` 精确过滤为 0。

## 边界与停止点

- 未修改 `BattleCommandQueue`、`BattleTurnController`、settlement、公式、BattleSession、CardZones、DI、Scene、Prefab、ProjectSettings、asmdef 或 HybridCLR。
- 未创建第二份 Hand/CardZones/Combatant/Intent/Power 权威状态，未新增牌、敌人、状态、Run、多人或网络玩法。
- 未生成、下载、移动或引用官方/候选美术；唯一图片内容仍是项目既有 `art_placeholder.png`，本切片只修改其 importer Meta。
- 在本页记录的 I3 停止点，I4 尚待单独确认；后续 I4 已按 [独立验收页](2026-08-06-sts2-ironclad-i4-success-destination.md) 完成。
- 未暂存、提交或推送 I0-I3 工作区改动。
