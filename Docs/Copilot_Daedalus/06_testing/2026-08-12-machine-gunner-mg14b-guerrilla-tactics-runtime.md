---
title: Marine Game 机枪兵 MG14B 游击战术运行时
page_type: testing
lifecycle: active
date: 2026-08-12
updated: 2026-08-12
status: verified-unity-native-2026-08-12
status_source: ../SESSION_LOG.md
source:
  - ../00_inbox/README web.md
  - ../01_requirements/2026-08-07-marine-game-card-requirement-digest.md
related_plan: ../plans/2026-08-07-marine-game-card-only-integration.md
related_decision: ../CODE_DECISIONS.md#cd-079游击战术将实际支付与名义触发弹耗分离
---

# Marine Game 机枪兵 MG14B 游击战术运行时

## 1. 验收对象与冻结行为

本记录覆盖 Hero 1002 的基础态职业程序 `GuerrillaTactics` (3251)。作者表元数据为 1 Energy / Self / Hand→Power；当前 CardInstance 没有升级态，因此本切片只实现每张能力牌增加 2 层游击，升级 3 层继续只作为作者表元数据。

PowerPile 的实体卡和游击层数是两个不同事实：一张 3251 成功归宿后，PowerPile 有一个卡牌实例、`GuerrillaTactics` 为 2 层；两张成功归宿后保留两个不同实例、总层数为 4。能力牌自身不耗弹，因此不会在本次激活中产生格挡。

每张成功出牌都在成本预览阶段冻结 `AmmoSpent` 与 `AmmoSpentForGuerrilla`。前者是实际扣除的 Ammo 并决定是否写出 `BattleAmmoSpentSettlement`；后者只用于游击格挡触发。当前普通支付令两者相等，原卡操作和既有功夫机甲后置钩子完成后，再在同一投影事务内追加 `游击层数 × AmmoSpentForGuerrilla` 的 Block。Stim 射击实际/名义耗 2 弹时，2 层游击给 4 Block。

本切片不实现 `TacticalAdvance` (3234)、固定机枪、临时 `MachinegunBurst`、免费攻击、升级实例、奖励/Run、Power HUD、场景或第二条写入链。名义弹耗字段仅为这些后续机制预留显式声明位置，不能据此猜测免费攻击与 Stim 的组合支付规则。

## 2. 已变更表面

| 层 | 变更 |
|---|---|
| 职业程序与 Power | `MachineGunnerBattleRuntime.cs` 新增 `MachineGunnerPowerKind.GuerrillaTactics`；Program 51 使用 `PowerStackGain = 2` 注册，并在成功归宿时按声明层数累加。 |
| 成本与预演 | `MachineGunnerCostResolution` 新增 `AmmoSpentForGuerrilla`；当前常规成本预览令它等于实际 `AmmoSpent`。游击格挡作为既有 `TryPrepareOperations` 的尾部投影操作加入，不另建事件总线或写入入口。 |
| 原子提交 | Block 仍由已准备操作在同一张卡的成功事务内写入；费用不足、目标非法或其他预演失败不会写 Power、资源、Block、卡区或随机状态。 |
| 作者表与目录门禁 | `DataTables/Datas/battle.card.xlsx` 仅将 Q141 (3251) 从 `CatalogOnly` 翻为 `Implemented`；Luban JSON、构建校验和机枪兵目录快照更新为 45 / 19。 |
| 回归 | `MachineGunnerStarterRuntimeTests` 新增游击的支付、Stim、叠层、实例归宿与失败零写入用例；目录快照直接锁定 3251 元数据。 |

## 3. 已加入的回归用例

| 用例 | 锁定事实 |
|---|---|
| `GuerrillaTactics_GrantsBlockFromActualAmmoSpendIncludingStimBonus` | 一张 3251 成功后只有 2 层、无即时 Block；普通射击实际 1 弹给 2 Block，Stim 后射击实际/名义 2 弹给 4 Block，Ammo settlement 先于游击 Block settlement。 |
| `GuerrillaTactics_StacksTwoPerPowerCardAndInsufficientEnergyDoesNotActivate` | 两张 3251 分别 Hand→Power、实例 ID 不同，最终 PowerPile 为 2 张、总层数为 4；随后 1 弹射击给 4 Block。费用不足时不写 Power、卡区、Block、资源或展示结果。 |
| `PowerProgramRegistry_ContainsImplementedPowerKinds` | Program 51 已注册为可执行 Power，不再停留在 CatalogOnly 集合。 |
| `GeneratedCatalog_GuerrillaTacticsKeepsAuthoredMetadata` | 直接读取 Luban JSON，锁定 1/1 Energy、Self、基础/升级 Power、升级标记、Implemented 状态与 Program 51。 |
| `ValidateCurrentProject_ProductionCatalogPasses` | 构建校验器的精确 `MARINE_*` 已实现集合纳入 `MARINE_GUERRILLA_TACTICS`。 |

## 4. 本轮证据与验收结论

| 项目 | 结果 | 证据/说明 |
|---|---|---|
| 作者工作簿 | 通过 | 仅将 Q141 (3251) 的 `implementation_status` 从 `CatalogOnly` 改为 `Implemented`；表格值、重新导入与渲染复核完成。 |
| Luban 与生成配置 | 通过 | 已执行 Luban 并同步 `game-config.json`；生成 `battle_tbcard.json` 中 3251 为 Program 51 / `implementation_status = 0`，目录为 45 张 `Implemented` / 19 张 `CatalogOnly`。 |
| 静态编译 | 通过 | `dotnet build TinySpire/Assembly-CSharp-Editor.csproj --no-restore -v:q` 为 0 errors；保留项目既有的 12 条 `MSB3277` 引用版本警告。 |
| `Sync and Build All` 与本地 Addressables | 通过 | 2026-08-12 在唯一既有 Unity 6000.5.5f1 Editor 运行菜单；Console 记录 `Addressable content successfully built (duration : 0:00:22.118)` 与 `TinySpire sync and local content build completed successfully.` |
| Unity 定向 EditMode | 通过 | MCP 任务 `02370c5357374fb1aaff48682cf22532`：6/6 passed，0 failed，0 skipped，0.317476 秒。 |
| Unity 完整 EditMode | 通过 | MCP 任务 `d3968d32a61f4a8cb9bf9c3396b905b0`：574/574 passed，0 failed，0 skipped，57.9375737 秒。 |

## 5. 验收后边界

其余 19 张 `CatalogOnly` 卡仍按独立机制切片处理。`AmmoSpentForGuerrilla` 只定义“触发用名义弹耗”的承载位置，不会把免费攻击、Stim、虚拟弹耗或临时卡生成的组合规则静默变成当前默认。`TacticalAdvance`、固定机枪和临时 `MachinegunBurst` 仍需要各自的需求裁决、运行时接线与原生验证；本切片没有扩展奖励/Run、Power UI、升级实例、HUD、Scene、Prefab、默认 Hero/Deck 或第二条写入链。
