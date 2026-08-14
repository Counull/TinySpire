---
title: Marine Game 机枪兵 MG9 逐段命中后置状态验收
page_type: testing
lifecycle: active
created: 2026-08-07
status_source: ../SESSION_LOG.md
related_plan: ../plans/2026-08-07-marine-game-card-only-integration.md
---

# Marine Game 机枪兵 MG9 逐段命中后置状态验收

## 范围

本记录只验收 Hero 1002 单场私有运行时中的 `IncendiaryAmmo` (3210)、`SpikeShot` (3248) 与 `AgedOil` (3253)，以及为统一逐段时序而迁移的 FlameElbow、KidneyShot、PainfulElbow、SniperShot 后置状态。不创建奖励入口、Run、Power HUD、升级实例、Scene、Prefab 或第二条共享写入链。

## 已证实规则

- 攻击型 `MachineGunnerCardProgram` 的 `PostHitOperations` 只允许作用于当前命中目标的私有状态、Burn 或 Vulnerable。每一段实际命中在本地投影中依次执行“伤害 → 卡牌后置状态 → 全局命中钩子”，然后作为同一张 `ExecutePlayerCard` 的准备结果由既有 `BattleCommandQueue.Submit` 原子提交。
- 伤害后目标已经死亡时，不再产生程序后置状态或全局钩子；伤害为零或被 Block 完全吸收但目标仍存活时，后置状态照常产生。X/随机多段命中的后置值不按段数二次缩放，失败路径不提交资源、卡区、随机或状态写入。
- `IncendiaryAmmo` 可叠层，任何实际 `IsShoot` 命中（包含 SniperShot）在伤害后且目标仍存活时施加层数 × 1 Burn。它不改变 MG8 的 `FirePower` 分类：狙击不读取开火，但仍触发燃烧弹药。
- `SpikeShot` 每一段为 `Damage 1 → Weakness +1 → Vulnerable +1 → Incendiary Burn`。Stim 追加命中会完整重复该顺序，故第一段 Vulnerable 参与第二段伤害计算。
- `AgedOil` 仅对非射击攻击的每一段存活命中固定施加 `Oil +2`。多个副本按原型的固定赋值语义只启用该钩子，不将数值相乘；HurricaneElbow 的 X=3 因而是三次 +2，X=0 不写入也不推进随机流。FlameElbow 先使用旧 Oil 生成 Burn，再施加本次 AgedOil，防止新 Oil 反过来放大同次 Burn。

## 配置与生成复核

| 项目 | 结果 |
| --- | --- |
| 作者工作簿 | 使用 `@oai/artifact-tool` 导入、值差异、重导入、公式错误扫描和前后渲染复核；仅 Q100、Q138、Q143 的 `implementation_status` 从 `CatalogOnly` 改为 `Implemented`。 |
| 工作簿部署 | 最终 `DataTables/Datas/battle.card.xlsx` SHA-256 为 `024B76E9E284B00247FD111EB5E0349CD988782C5CC281BB91C5808B54C1623E`，与复核导出文件一致。 |
| Luban | 等价 Luban 命令 validation/生成成功；生成器移除的 `TinySpire/Assets/GameData/game-config.json` 已从 `DataTables/game-config.json` 恢复。 |
| 生成目录 | `battle_tbcard.json` 已直接核对为 37 张 `Implemented` / 27 张 `CatalogOnly`；新增精确 ID 为 3210、3248、3253，且 Program ID 分别为 10、48、53。 |

## Unity 验收

| 项目 | 结果 |
| --- | --- |
| Editor | 仅使用唯一已连接的 Unity 6000.5.5f1 Editor；未启动、终止或驱动第二个 Editor/游戏窗口。 |
| 同步与本地内容 | 已通过同一 Editor 调用 `TinySpire/Build/Sync and Build All`，MCP 返回调用成功。本轮 MCP Console 未返回可存档的完成日志；随后全量 Refresh 导入和编译无产品错误。 |
| 定向 EditMode | Unity MCP 任务 `2ec0afd4a36a46358aaba107ca8a5d2d`：**57/57 passed，0 failed，0 skipped**，总时长 0.7477155 秒。 |
| 覆盖用例 | `MachineGunnerStarterRuntimeTests` 覆盖燃烧弹药叠层/狙击、钉刺射击 Stim 逐段交错、致死与全格挡边界、陈年机油叠层语义、HurricaneElbow X 边界和 FlameElbow 的旧 Oil 顺序；`MachineGunnerDamagePipelineTests`、`BattleCardPlayRulesTests`、`MachineGunnerCatalogSnapshotMG2ATests`、`BattleCardCatalogBuildValidatorTests` 覆盖共享公式、卡牌规则与 37/27 门禁。 |

## 未包含

- 未实现升级字段对应的 CardInstance 升级状态，未硬编码 + 值。
- 未实现 BurningOil 的回合末非消耗 Oil 增长，亦未实现 IncompleteCombustion 的 Exhaust、动态存活目标交叉结算或 Burn→Smoke；TwelveHits、临时卡、延迟/下回合时机、选择、自动连锁和超上限 Energy 仍需独立切片。
- 未修改奖励/Run、Power HUD、Scene、Prefab、默认 Hero/Deck、ProjectSettings、asmdef、HybridCLR、启动 DI 或受保护的 Targeting/Candidates/Hermes 美术路径；未暂存、提交或推送。
