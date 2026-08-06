---
title: STS2 Ironclad I2 CatalogOnly 构建隔离
page_type: testing
lifecycle: active
created: 2026-08-06
updated: 2026-08-06
scope: STS2 v0.107.1 Ironclad 单人卡池 I2
status_source: ../SESSION_LOG.md
---

# STS2 Ironclad I2 CatalogOnly 构建隔离

## 验收结论

I2 已完成。`TinySpire/Build/Sync and Build All` 现在会在同次 Luban 生成与四源表清单校验之后、任何 Localization 或 Addressables 写入之前，执行独立的 `BattleCardCatalogBuildValidator`。它拒绝 Deck 引用缺失卡或 `CatalogOnly` 卡、未知实现状态、`Implemented` 空程序/空或重复参数键/缺失 Effect 引用、非法牌面短键，以及生成 JSON 顶层键与运行时记录 `id` 漂移。

`CatalogOnly` 行必须显式使用唯一占位短键，且同样要通过现有 Addressables 真实素材解析器；不存在的文件或伪造 `Assets/...` 路径不能通过。本切片测试阶段预留名为 `card_art_catalog_placeholder`，当时尚无生产 `CatalogOnly` 行。I3 已按 CD-059 在第一张目录卡进入表前最终锁定为项目既有 `art_placeholder`，并验证其满足 `Sprite / Single / no mipmap` 与 `card-art/{key}` 逻辑地址契约；I2 的门禁原则不变。

I2 没有录入新卡，也没有把“已录入”冒充为“可玩”。

## 精确红灯与最小实现

首个测试 `BattleCardCatalogBuildValidatorTests.Validate_CatalogOnlyCardReferencedByDeck_Throws` 使用 Deck `7101` 引用 `CatalogOnly` Card `9101`，并为该卡提供合法短键与可解析 Effect，隔离唯一失败原因。

- 首次 solution build 红灯：`CS0103`，缺少 `BattleCardCatalogBuildValidator`。
- 期望错误：`Deck 7101 references CatalogOnly card 9101.`
- 最小实现：新增独立 Editor validator，并只在 `TinySpireBuildTools.SyncAndBuildAll` 的表清单校验后、Localization 前调用。

随后逐项取得并转绿的红灯覆盖：

- `Implemented` 无 `effect_bindings`、引用缺失 Effect、参数键为空或重复；
- 未知 `implementation_status`；
- 完整 `Assets/...png` 伪路径牌面键；
- `CatalogOnly` 借用正式牌面而未声明专用占位键；
- 牌面短键不存在于真实素材目录；
- 当前项目真实三表与四张真实牌面的完整成功路径；
- Effect JSON 顶层键 `9910` 与记录内 `id=9909` 漂移。该审计红灯任务 `0107906ab3824efea6cb20e40902b798` 先误报缺 Effect，加入三表统一记录身份校验后任务 `76cf944b4efc4ae7a1c037efcd7b9122` 为 1/1 passed。

“有效程序”在 I2 的发布边界明确为：binding 非空、参数键非空且卡内唯一、Effect 引用存在。EffectType/Attribute/公式与新机制的可执行语义仍由唯一 `BattleEffectExecutor` 和对应后续机制切片的 Queue 红灯验证；Editor 不复制第二份执行规则。

同次 Luban 成功生成保证字段 shape；I2 validator 负责跨表身份与发布语义。若生成 JSON 被外部损坏，构建仍会在 Localization 前失败，但本切片不承诺把所有 Newtonsoft 字段/形状异常统一成新的 schema 错误类型。

## 自动与构建验证

| 层级 | 结果 | 证据 |
|---|---:|---|
| Luban | 通过 | `DataTables/gen.bat` 于 2026-08-06 12:05 成功完成 validation、C# 与 JSON 生成 |
| Solution build | 通过 | 最终 0 error；保留 12 条既有程序集版本冲突 warning |
| 原始精确 Deck 红灯回归 | 1/1 | Unity EditMode 任务 `cfaf05a194e34796b9c3f96808126cea` |
| Validator 全类 | 10/10 | 已包含在最终相关任务；覆盖生产成功路径与全部发布门禁 |
| 同步构建后相关回归 | 102/102 | 任务 `e35a3b7e3f0f4bacab7c34ce9e6d0e31`；包含 I1 七类 86 项、I2 10 项、牌面静态 4 项与表清单 2 项 |
| Local Content | 通过 | Console：`TinySpire Addressables content built: Library/com.unity.addressables/aa/Windows/settings.json` 与 `TinySpire sync and local content build completed successfully.`；最终 settings 时间 2026-08-06 12:21:10 +08:00 |
| 物理 AssetBundle | 通过 | 最新 BuildLayout：`TinySpire Card Art` 为 PackTogether，四个 `card-art/*` 地址同属 `tinyspirecardart_assets_all_698e23f0c7066ef2c6742748769e6b02.bundle`，Provider 为 `AssetBundleProvider`、ResultType 为 `IAssetBundleResource` |
| 真实逻辑地址加载 | 1/1 | `CardArtLogicalAddresses_LoadSprites` 最终任务 `b6391ca3dde14ff189837a5401bcd310`，四个 `card-art/{key}` 均加载为 Sprite |
| InvalidKey | 0 | 最终 Console 精确过滤无 `InvalidKey` 记录 |
| 双轴只读复核 | 通过 | 规格复核无 P0/P1/P2；代码复核确认 record key/id 绕过已封闭、无新 P0-P2 |

真实逻辑地址测试的第一次任务 `e0341032e107400aade6152fd0b55352` 在 Editor 未聚焦时被服务标记 `blocked_reason: editor_unfocused`，最终 `TestResults.xml` 记录 180 秒超时；没有 InvalidKey。Editor 后续处于 focused/idle 时，完全相同的测试以 `b6391ca3dde14ff189837a5401bcd310` 通过，因此前一次环境超时没有被隐藏，也没有通过增加超时、修改 ProjectSettings 或抢建第二个 Editor 绕过。

Addressables 自身的“成功构建”和 TestRunner 保存 `TestResults.xml` 会以 Warning/Exception 类型写 Console；它们已与产品编译、运行时和 InvalidKey 错误区分，不能被描述为 0 条 Console 记录。

## 边界与停止点

- 未修改 `BattleCommandQueue`、`BattleTurnController`、settlement、公式、BattleSession、CardZones、DI 或场景启动。
- 未修改 Deck、Localization 作者内容、Scene、Prefab、ProjectSettings、asmdef、HybridCLR 或任何官方/候选美术。
- 复用 `AddressablesBuildTools` 的唯一真实牌面解析 seam；运行时仍只通过 `card-art/{key}` 与 Addressables 加载，不读取文件系统路径。
- 当前生成表仍为 4 张 `Implemented`、0 张 `CatalogOnly`，当前 Deck 共 10 个引用且 `CatalogOnly` 引用为 0。
- I2 独立停止点当时完成；后续 I3 已录入 85 张目录、复用项目既有占位素材，并保持当前 Deck 只引用 `Implemented`。当前标准见 CD-059 与 I3 验收页。
- 未暂存、提交或推送 STS2 I0/I1/I2 工作区改动。
