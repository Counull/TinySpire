---
title: Marine Game 机枪兵 MG10B 不充分爆燃运行时
page_type: testing
lifecycle: active
date: 2026-08-08
updated: 2026-08-11
status: verified-unity-native-2026-08-11
status_source: ../SESSION_LOG.md
source:
  - ../00_inbox/marine-game.zip (marine-game/cards.json, battle.html)
  - ../01_requirements/2026-08-07-marine-game-card-requirement-digest.md
related_plan: ../plans/2026-08-07-marine-game-card-only-integration.md
related_decision: ../CODE_DECISIONS.md#cd-074不充分爆燃以专用预演记录冻结来源快照--动态存活目标
---

# Marine Game 机枪兵 MG10B 不充分爆燃运行时

## 1. 验收对象与冻结行为

本记录覆盖 Hero 1002 的 `IncompleteCombustion` (3222) 基础值程序。原型行为被冻结为：

1. 按 Encounter 顺序捕获开始时存活且 `Burn > 0` 的敌人来源，并冻结每名来源的 Burn 值。
2. 每名冻结来源按其开始出手时仍存活的敌人目标顺序造成等于冻结 Burn 的 Debuff 伤害；来源在此前死亡仍会出伤，死亡目标跳过。
3. 全部交叉伤害完成后，才按 Encounter 顺序对仍存活敌人写入 `Smoke += Burn`、`Burn = 0`；玩家不参与。
4. 全过程不读取、不写入 Oil，也不调用会消费 Oil 的 `ApplyBurn`；支付 3 Energy 后卡牌从 Hand 移至 ExhaustPile。

伤害继续使用既有 Debuff 管线，因此 Block 仍可吸收伤害，且没有把 Weakness、Vulnerable、Smoke 或 Armor 改造成攻击修正。终局仍在整张卡的既有事务提交后派生，而不是在任一交叉伤害段中新增控制器写入口。

## 2. 已变更表面

| 层 | 变更 |
|---|---|
| 职业运行时 | `TinySpire/Assets/Scripts/Battle/MachineGunner/MachineGunnerBattleRuntime.cs` 增加专用预演/提交记录、3222 Program 注册，以及注册卡的 Exhaust 归宿。 |
| EditMode 回归 | `TinySpire/Assets/Editor/Tests/MachineGunnerStarterRuntimeTests.cs` 增加四项 3222 行为锁定；fixture 以表内 `Self` 目标规则和 `ExhaustPile` 归宿构造该卡。 |
| 作者表 | `DataTables/Datas/battle.card.xlsx` 仅 Q112 从 `CatalogOnly` 翻为 `Implemented`。 |
| 生成和门禁 | `TinySpire/Assets/GameData/battle_tbcard.json`、卡牌目录构建校验与机枪兵快照更新到 39 / 25。 |

未修改升级实例、奖励/Run、Power HUD、Scene、Prefab、默认 Hero/Deck、ProjectSettings、asmdef、HybridCLR、启动 DI 或受保护美术路径。

## 3. 已加入的回归用例

| 用例 | 锁定事实 |
|---|---|
| `IncompleteCombustion_UsesBurnersForCrossDamageThenConvertsLivingEnemiesAndExhausts` | 两名燃烧敌人的完整交叉伤害、Block/HP 前后值、全部伤害先于 Smoke/Burn 转换、Oil/玩家状态不变以及 Hand→Exhaust。 |
| `IncompleteCombustion_CapturedDeadBurnerStillDamagesLivingTargetsWithoutConvertingDeadEnemy` | 第一来源杀死第二来源后，第二来源仍以冻结 Burn 命中剩余目标；死亡目标没有 Smoke/Burn 转换。 |
| `IncompleteCombustion_WithoutBurners_OnlyPaysEnergyAndExhausts` | 没有燃烧来源时不产生伤害或私有状态写入，仅支付费用并 Exhaust，Oil 不变。 |
| `IncompleteCombustion_KillsAllEnemies_ExhaustsBeforeBattleEnds` | 本卡击杀全部敌人时不在任一伤害段中提前终局；先 Hand→Exhaust，再由既有控制器进入 `BattleEnded`。 |
| `GeneratedCatalog_IncompleteCombustionKeepsAuthoredMetadata` | 直接读取 Luban 生成 JSON，锁定 3222 的基础/升级费用、Self、ExhaustPile、升级标记、Implemented 状态与 Program 22。 |

## 4. 本轮证据与验收结论

| 项目 | 结果 | 证据/说明 |
|---|---|---|
| 作者工作簿 | 通过 | 精确值差异、重新导入、渲染和公式错误扫描均只确认 Q112；最终 SHA-256 为 `1AEDDC31EF90888F8B37A4A4B69807E74B3E287E21E15C2E920351AA58347471`。 |
| Luban | 通过 | 已执行生成命令，生成器移除的 `TinySpire/Assets/GameData/game-config.json` 已立即从 `DataTables/game-config.json` 恢复。 |
| 生成 JSON | 通过 | 64 张机枪兵目录为 39 张 `Implemented` / 25 张 `CatalogOnly`；3222 为 Program 22、Cost 3、Self、ExhaustPile。 |
| 静态编译 | 通过 | `dotnet build TinySpire/Assembly-CSharp-Editor.csproj --no-restore`：0 errors；保留 Unity 依赖图既有的 12 条 `MSB3277` 警告。 |
| `Sync and Build All` 与本地 Addressables | 通过 | 2026-08-11 在唯一已连接的既有 Unity 6000.5.5f1 Editor 执行菜单。调用期间 MCP 传输断连，但重连后的 Console 明确记录 `Addressable content successfully built (duration : 0:00:25.627)` 和 BuildLayout 写入，未出现 Error。 |
| Unity EditMode | 通过 | Unity MCP 任务 `94b4d610258b4b05a896adfd20ca6428`：65/65 passed，0 failed，0 skipped，2.2295 秒；覆盖 `MachineGunnerStarterRuntimeTests`、`MachineGunnerDamagePipelineTests`、`BattleCardPlayRulesTests`、`MachineGunnerCatalogSnapshotMG2ATests` 与 `BattleCardCatalogBuildValidatorTests`，含四项 3222 行为回归。 |

## 5. 验收完成后的后续顺序

本记录的同步构建与定向 EditMode 门禁均已完成。下一张 CatalogOnly 卡必须继续沿用“独立机制切片 → 精确表项翻转 → Luban → `Sync and Build All` → 定向 EditMode”的顺序，并保持奖励/Run、升级实例、场景、Prefab、默认 Hero/Deck 和第二写入链不在本计划范围内。
