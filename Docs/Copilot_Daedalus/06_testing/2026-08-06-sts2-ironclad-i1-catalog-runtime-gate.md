---
title: STS2 Ironclad I1 CatalogOnly 运行时隔离
page_type: testing
lifecycle: active
created: 2026-08-06
updated: 2026-08-06
scope: STS2 v0.107.1 Ironclad 单人卡池 I1
status_source: ../SESSION_LOG.md
---

# STS2 Ironclad I1 CatalogOnly 运行时隔离

## 验收结论

I1 已完成。`battle.Card` 现在显式区分 `Implemented` 与 `CatalogOnly`；后者通过唯一生产写入口 `BattleCommandQueue.Submit` 提交时稳定返回 `CardNotImplemented`，且在费用、目标与 Effect 执行前终止。失败不会消耗能量、移动卡牌、修改参与者/回合事实、发布 settlement 或令 Queue fault。

本切片只建立运行时隔离，没有录入新的 Ironclad 卡、修改当前 Deck、开放新玩法或声称 `CatalogOnly` 可玩。构建期 Deck/程序/牌面校验属于 I2，尚未开始。

## 精确红灯

新增 `BattleEffectCommandQueueTests.Submit_CatalogOnlyCard_FailsBeforeEnergyOrCardZoneWrites`，测试先引用尚不存在的 `cfg.battle.CardImplementationStatus.CatalogOnly`，并通过公开 Queue seam 提交目录占位卡。

- 红灯命令：`dotnet build TinySpire/TinySpire.sln --no-restore -m:1`
- 红灯结果：`BattleEffectCommandQueueTests.cs(651,20): error CS0234`，缺少 `cfg.battle.CardImplementationStatus`
- 预期零写入：Energy、Hand/Discard/Exhaust、Health/Strength/Block/Vulnerable、Turn、Queue fault 与表现结果均不得因该命令变化

该红灯精确指向“缺少可表达的实现状态”，没有通过修改 Queue、Turn 或 settlement 制造失败。

## 最小实现

- `DataTables/Datas/__enums__.xlsx` 新增 `battle.CardImplementationStatus`：`Implemented=0`、`CatalogOnly=1`。
- `DataTables/Datas/battle.card.xlsx` 新增必填 `implementation_status`；四张既有生产卡均显式填写 `Implemented`。
- Luban 生成新的 `CardImplementationStatus.cs`，并同步 `Card.cs` 与 `Assets/GameData/battle_tbcard.json`。
- `BattleCardPlayRules.Evaluate` 在费用、目标与 Effect 之前拒绝一切非 `Implemented` 状态。
- `BattleCommandExecutionFailureReason` 在枚举末尾追加稳定失败原因 `CardNotImplemented`，不改变任何既有失败原因的整数值。
- 所有受 Card JSON schema 影响的测试夹具显式填写 `Implemented`，不依赖缺字段默认值。
- 精确零写入测试给目录卡绑定真实 `ModifyAttribute(Strength +9)` Effect，并比较玩家与敌人的 Health/Strength/Block/Vulnerable 标量，避免只比较只读属性对象身份而漏掉内部写入。
- 插列后的作者工作簿已恢复原 Effect 蓝色列与 Illustration 绿色列样式；新状态列保留 Effect 同类样式，Luban 语义与作者表可读性同时复核。

## 自动验证

| 层级 | 结果 | 证据 |
|---|---:|---|
| Luban | 通过 | `DataTables/gen.bat` 成功生成枚举、Card 类型与 JSON；四张既有卡均为 `implementation_status: 0` |
| Solution build | 通过 | 0 error；保留 12 条既有程序集版本冲突 warning |
| 精确 Queue 红绿测试 | 1/1 | 最终 `TinySpire/Build/Sync and Build All` 后 Unity EditMode 任务 `119aeec7577640109aa4173c41c2566b` |
| 同步构建后相关回归 | 86/86 | `BattleCardPlayRulesTests`、Queue/M8D/Effect Queue、Session、反馈路由与目标聚焦；最终任务 `9e54ef937764492ba2ef41bcdfcad930` |
| Local Content | 通过 | Console：`TinySpire Addressables content built: Library/com.unity.addressables/aa/Windows/settings.json` 与 `TinySpire sync and local content build completed successfully.` |
| Unity Console | 通过（代码/构建） | 同步构建完成时 0 error；最终测试后 error 过滤仅有 Unity TestRunner 的三条 `Saving results to .../TestResults.xml` 结果保存记录，没有编译、运行时或 InvalidKey 错误 |
| 额外完整 EditMode | 未作为全绿证据 | 任务 `ad9b7b3e47a340bba4ce38e368c8628a` 完成 461 项；Editor 未聚焦时 `CardArtLogicalAddresses_LoadSprites` 与 `CharacterLogicalAddresses_InstantiatePrefabs` 各超时 180 秒，服务此前明确报告 `blocked_reason: editor_unfocused`，未报告其他失败 |

`Sync and Build All` 的 MCP 调用等待窗口先返回超时，但同一唯一 Editor 已在 Console 明确报告完整同步与本地内容构建成功；随后 Editor 回到 idle，并完成构建后的精确 1/1 与相关 86/86 回归，因此未把调用超时误报为构建失败或成功依据本身。

额外完整 EditMode 不是 I1 的通过依据。两项超时均是既有 Addressables 真实加载测试，完整运行期间 Unity 一直未聚焦；本轮没有改测试超时、ProjectSettings 或窗口状态来绕过环境限制。需要全量绿色时，应在用户允许 Editor 保持聚焦后只重跑这两项，再补一次完整套件。

## 边界与停止点

- 未修改 `BattleCommandQueue`、`BattleTurnController`、settlement 类型/顺序、公式、CardZones 写契约或 DI/启动流程。
- 未修改 Localization、Scene、Prefab、ProjectSettings、asmdef、HybridCLR、官方/候选美术或现有 Addressables 地址规则。
- 本切片没有新增素材域或地址，因此 Local Content 成功只证明表生成与既有可寻址内容仍可构建，不冒充新素材的 Packed/Player AssetBundle 取证。
- I1 独立停止点完成；I2 必须从 `Validate_CatalogOnlyCardReferencedByDeck_Throws` 的构建期红灯开始。
