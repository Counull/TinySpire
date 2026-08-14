---
title: Marine Game 机枪兵 MG7 Burn/Oil 生命周期与首批依赖卡验收
page_type: testing
lifecycle: active
created: 2026-08-07
status_source: ../SESSION_LOG.md
related_plan: ../plans/2026-08-07-marine-game-card-only-integration.md
---

# Marine Game 机枪兵 MG7 Burn/Oil 生命周期与首批依赖卡验收

## 范围

本记录只验收 Hero 1002 单场私有运行时的 Burn/Oil 回合末生命周期，以及四张以其为前置的基础值卡：`GasPump` (3217)、`Napalm` (3218)、`Molotov` (3219)、`FlameElbow` (3255)。不创建奖励入口、Run、升级实例、Burn HUD、场景或第二条写入链。

## 已证实规则

- 最后一名存活玩家的 `EndPlayerAction` 在丢弃手牌与既有结束行动状态之后，且敌方行动前，只结算一次全场 Burn；顺序为存活敌人的 Encounter 顺序，再到机枪兵玩家。
- Burn 走 `MachineGunnerDamageKind.Debuff`：可被 Block 吸收，不吃 Weakness、双向 Smoke、Vulnerable 或 Armor，不减少自身层数。
- Burn 消灭最后一名敌人时跳过玩家自燃并立即 Victory；玩家自燃死亡时立即 Defeat，均不会继续后续阶段。
- `ApplyBurn` 使用施加前 Oil：`Burn += baseBurn + oldOil`、`Oil = floor(oldOil / 2)`。Napalm 的程序顺序固定为 Burn 3、再 Oil +5，因此新 Oil 不在同次施加中触发。
- FlameElbow 先结算最近敌人的 6 点攻击伤害，只对投影仍存活的目标施加 Burn +3。

## 配置与生成复核

| 项目 | 结果 |
| --- | --- |
| 作者工作簿 | 使用 `@oai/artifact-tool` 导入、值差异、重新导入和渲染复核；仅 Q107、Q108、Q109、Q145 的 `implementation_status` 从 `CatalogOnly` 改为 `Implemented`。 |
| 工件部署 | 部署后的 `DataTables/Datas/battle.card.xlsx` SHA-256 为 `C92F40E7E70A8F69E65B6D6B67CAA401116E439CD184A592E2373C801EF303D0`，与复核工件一致。 |
| Luban | 等价 Luban 命令 validation/生成成功；生成器移除的 `TinySpire/Assets/GameData/game-config.json` 已从 `DataTables/game-config.json` 恢复。 |
| 生成目录 | `battle_tbcard.json` 已直接核对为 31 张 `Implemented` / 33 张 `CatalogOnly`；四张新增卡均带现有 `program_id`，且精确集合不依赖连续 ID。 |

## Unity 验收

| 项目 | 结果 |
| --- | --- |
| Editor | 仅使用唯一已连接的 Unity 6000.5.5f1 Editor；未启动、终止或驱动第二个 Editor/游戏窗口。 |
| 同步与本地内容 | `TinySpire/Build/Sync and Build All` 成功；控制台记录 Addressable content successfully built（10.631 秒）及 TinySpire sync and local content build completed successfully。 |
| 定向 EditMode | Unity MCP 任务 `5db8f11868324b7788a2ef822c9b0ec9`：**37/37 passed，0 failed，0 skipped**，总时长 0.3330463 秒。 |
| 覆盖用例 | `MachineGunnerStarterRuntimeTests`、`MachineGunnerDamagePipelineTests`、`MachineGunnerCatalogSnapshotMG2ATests`、`BattleCardCatalogBuildValidatorTests` 的对应集合。 |

首次采用十个精确测试名的 MCP 任务 `547990575054409ca86affd800563672` 被连接器在 15 秒初始化门限后标为失败；控制台显示 Test Runner 已开始并结束，且没有产品脚本错误。随后以类筛选、120 秒初始化门限执行的 `5db8f11868324b7788a2ef822c9b0ec9` 返回完整通过结果，因此后者是本记录的测试结论；前者只作为 Unity MCP 回调时序观察保留。

只读复核后新增“前一敌人死亡但后续敌人与玩家仍继续结算”及“Oil 结算记录必须为负向减半”两条回归。刷新编译无错误，Unity MCP 任务 `f2194e4553304b2892deca56de629f3e` 对 `MachineGunnerStarterRuntimeTests`、`MachineGunnerDamagePipelineTests` 与 `MachineGunnerCatalogSnapshotMG2ATests` 返回 **28/28 passed，0 failed，0 skipped**，总时长 0.4372064 秒。

只读复核后新增“前一敌人死亡但后续敌人与玩家仍继续结算”及“Oil 结算记录必须为负向减半”两条回归。刷新编译无错误，Unity MCP 任务 `f2194e4553304b2892deca56de629f3e` 对 `MachineGunnerStarterRuntimeTests`、`MachineGunnerDamagePipelineTests` 与 `MachineGunnerCatalogSnapshotMG2ATests` 返回 **28/28 passed，0 failed，0 skipped**，总时长 0.4372064 秒。

## 未包含

- 升级字段仍只是配置来源：项目目前没有通用 CardInstance 升级状态，未把 +8 Oil、+7 Burn、+9 Damage 等数值硬编码为升级行为。
- 未实现 KungfuMech、ElectroBoost、ComboElbow、逐段命中、BurningOil、Exhaust、延迟/下回合效果、选择协议、临时卡、自动连锁、奖励/Run 或 Power HUD。
- 未修改 Scene、Prefab、默认 Hero/Deck、ProjectSettings、asmdef、HybridCLR、启动/DI，或受保护的 Targeting/Candidates/Hermes 美术路径；未暂存、提交或推送。
