---
title: Marine Game 机枪兵 MG10A 烈火烹油运行时验收
page_type: testing
lifecycle: active
created: 2026-08-07
status_source: ../SESSION_LOG.md
related_plan: ../plans/2026-08-07-marine-game-card-only-integration.md
---

# Marine Game 机枪兵 MG10A 烈火烹油运行时验收

## 范围

本记录只验收 Hero 1002 单场私有运行时中的 `BurningOil` (3254)。本切片不修改奖励入口、Run、Power HUD、升级实例、Scene、Prefab 或第二条共享写入链。

## 已证实规则

- `BurningOil` 成功出牌后按既有 `Power` 规则支付 2 Energy 并进入 `PowerPile`；其 `MachineGunnerPowerKind` 只作为持续能力启用标记，不在出牌时立即改写 Burn 或 Oil。
- 最后一名存活玩家结束行动时，`ResolvePlayerRoundEnd` 先取得按 Encounter 顺序的存活敌人。在既有 Burn 伤害循环前，若 `GetPowerStack(BurningOil) > 0`，逐一只为原本 `Burn > 0` 的存活敌人写入 `Burn += 1 + Oil`。玩家自身不会增长 Burn，原有 Oil 完全不减少、不减半且不产生 Oil settlement。
- 每条增长使用现有 `MachineGunnerPrivateStatusChangedSettlement`，来源为机枪兵玩家。所有敌人增长 settlement 均先于任一 Burn Debuff 伤害 settlement；之后仍复用既有 Burn/Block/胜负收口。增长后的 Burn 消灭最后敌人时继续跳过玩家自燃并进入 Victory。
- 多张 `BurningOil` 可以保留在 `PowerPile` 并计入层数，但层数只表示效果已启用，不将增长值叠为 `+2 + Oil` 或更高。

## 配置与生成复核

| 项目 | 结果 |
| --- | --- |
| 作者工作簿 | 使用 `@oai/artifact-tool` 导入、值差异、重导入、公式错误扫描和前后渲染复核；仅 Q144（3254）的 `implementation_status` 从 `CatalogOnly` 改为 `Implemented`。 |
| 工作簿部署 | 最终 `DataTables/Datas/battle.card.xlsx` SHA-256 为 `E005A9548A02D79D67C1CF9F8EC848F66399B951C7B6A1FCE88F408DC57406F8`，与已复核导出一致。 |
| Luban | 等价 Luban 命令 validation/生成成功；生成器移除的 `TinySpire/Assets/GameData/game-config.json` 已从 `DataTables/game-config.json` 恢复。 |
| 生成目录 | `battle_tbcard.json` 已直接核对为 38 张 `Implemented` / 26 张 `CatalogOnly`；3254 为 `MARINE_BURNING_OIL`，`implementation_status=0`、`program_id=54`、Cost 2、Power → PowerPile。 |

## Unity 验收

| 项目 | 结果 |
| --- | --- |
| Editor | 只使用唯一已连接的 Unity 6000.5.5f1 Editor；未启动、终止或驱动第二个 Editor/游戏窗口。 |
| 同步与本地内容 | 已通过同一 Editor 执行 `TinySpire/Build/Sync and Build All`；Console 明确记录 `TinySpire sync and local content build completed successfully.`，Addressables 本地内容构建耗时 10.402 秒。 |
| 定向 EditMode | Unity MCP 任务 `4afefa7766cb454eb0aeb9b8da061afe`：**60/60 passed，0 failed，0 skipped**，总时长 0.5972826 秒。 |
| 覆盖用例 | `MachineGunnerStarterRuntimeTests` 覆盖全体增长先于伤害、Oil 不消耗、玩家不增长、无 Burn 敌人跳过、重复 Power 仅启用一次，以及增长击杀最后敌人后的 Victory 收口；`MachineGunnerDamagePipelineTests`、`BattleCardPlayRulesTests`、`MachineGunnerCatalogSnapshotMG2ATests`、`BattleCardCatalogBuildValidatorTests` 共同覆盖共享公式、卡牌规则与 38/26 门禁。 |

## 未包含

- 未实现 CardInstance 升级状态，故 `burn_growth` 的升级值不会被硬编码。
- 未实现 `IncompleteCombustion` 的 Exhaust、动态燃烧者×存活目标交叉结算或 Burn→Smoke 转换；`TwelveHits`、临时卡、延迟/下回合时机、选择、自动连锁和超上限 Energy 仍须分别切片。
- 未修改奖励/Run、Power HUD、Scene、Prefab、默认 Hero/Deck、ProjectSettings、asmdef、HybridCLR、启动 DI 或受保护的 Targeting/Candidates/Hermes 美术路径；未暂存、提交或推送。
