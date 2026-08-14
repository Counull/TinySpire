---
title: Marine Game 机枪兵 MG6 已有 Power 程序门禁验收
page_type: testing
lifecycle: active
created: 2026-08-07
status_source: ../SESSION_LOG.md
related_plan: ../plans/2026-08-07-marine-game-card-only-integration.md
---

# Marine Game 机枪兵 MG6 已有 Power 程序门禁验收

## 范围

本记录只验收 Hero 1002 已有运行时已完整覆盖的六张能力牌配置门禁：`CoreExpansion` (3206)、`OutputAdjust` (3207)、`BlastShield` (3208)、`MagExpansion` (3209)、`SmokePersist` (3211) 和 `PowerOverclock` (3245)。不创建奖励入口、Power UI、升级实例或新的职业写入链。

## 配置与生成复核

| 项目 | 结果 |
| --- | --- |
| 作者工作簿 | 通过 `@oai/artifact-tool` 导入、值差异、重新导入和前后渲染复核；仅六个 `implementation_status` 单元格由 `CatalogOnly` 改为 `Implemented`。 |
| 工件部署 | 部署后的 `battle.card.xlsx` 与已验证导出工件 SHA-256 相同。 |
| Luban | 等价 Luban 命令成功完成 validation 与 `battle_tbcard.json` 生成；随后恢复生成器会移除的 `Assets/GameData/game-config.json` 基础设施清单。 |
| 生成目录 | `marine-game-v1-20260807-cards` 为 64 张，精确 27 张 `Implemented` / 37 张 `CatalogOnly`；六张 Power 都保留原有 `program_id`。 |

## Unity 验收

| 项目 | 结果 |
| --- | --- |
| Editor | 仅使用已连接的 Unity 6000.5.5f1 Editor；未启动、终止或驱动第二个 Editor/游戏窗口。 |
| 同步与本地内容 | `TinySpire/Build/Sync and Build All` 成功；控制台记录 Addressable content successfully built（6.285 秒）和 TinySpire sync and local content build completed successfully。 |
| 定向 EditMode | Unity MCP 任务 `f46ca19e2cfe4785bbca0da4c1769487`：**3/3 passed，0 failed，0 skipped**，总时长 0.2187675 秒。 |
| 覆盖用例 | `MachineGunnerCatalogSnapshotMG2ATests.GeneratedCatalog_MarineGameV1SnapshotPassesStarterRuntimeValidation`、`MachineGunnerStarterRuntimeTests.PowerProgramRegistry_ContainsImplementedPowerKinds`、`MachineGunnerStarterRuntimeTests.PowerPrograms_ActivatePrivateStateAndMoveCardsToPowerPile`。 |

## 已证实行为

- 六张卡都继续通过既有 `BattleCommandQueue.Submit` 写入链执行，并按 `Hand → PowerPile` 归宿结算。
- `CoreExpansion` 改变能量上限；`OutputAdjust` 改变上限与每回合恢复；`BlastShield` 提供职业私有 Armor；`MagExpansion` 改变弹药上限；`SmokePersist` 改变烟雾的回合开始衰减；`PowerOverclock` 改变下一玩家回合的抽牌目标。
- 目录校验使用精确 `MARINE_*` 外部 key 集合，因此任一仍未实现的目录卡被错误翻转时会在验证中失败。

## 未包含

- 其余 37 张卡、Burn/Oil 回合末生命周期、逐段命中、Exhaust、延迟实体、手牌选择、临时卡、奖励/Run、Power HUD、升级实例和动态插画加载均未在本切片实现。
- 未修改 Scene、Prefab、默认 Hero/Deck、ProjectSettings、asmdef、HybridCLR、启动/DI，或受保护的 Targeting/Candidates/Hermes 美术路径；未暂存、提交或推送。
