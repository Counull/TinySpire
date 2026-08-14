---
title: Marine Game 机枪兵 MG12 光学迷彩运行时
page_type: testing
lifecycle: active
date: 2026-08-11
updated: 2026-08-12
status: verified-unity-native-2026-08-11
status_source: ../SESSION_LOG.md
source:
  - ../00_inbox/README web.md
  - ../01_requirements/2026-08-07-marine-game-card-requirement-digest.md
related_plan: ../plans/2026-08-07-marine-game-card-only-integration.md
related_decision: ../CODE_DECISIONS.md#cd-076光学迷彩将攻击后的隐身消耗与伤害词条解耦为受限程序声明
---

# Marine Game 机枪兵 MG12 光学迷彩运行时

## 1. 验收对象与冻结行为

本记录覆盖 Hero 1002 的 `OpticalCamo` (3249) 基础值程序。作者表冻结为：支付 2 Energy、Self 输入、施加 `Invisible +2`、从 Hand 移至 DiscardPile；升级费用 1 仍只是作者表元数据，因为项目没有通用 CardInstance 升级状态。

隐身的既有受击减半伤害管线不在本轮重写。新增的生命周期接线是：玩家行动结束减少 1 层；普通攻击仅在整张卡已成功进入卡区归宿后减少 1 层；资源、目标或卡区失败路径不减少。3247 `SniperShot` 和 3248 `SpikeShot` 声明成功攻击后保留隐身；3248 的“射击 + 狙击/不破隐”保持独立于现有开火伤害语义。

本切片不实现 Invisible HUD、角色透明度、场景表现、升级实例、奖励/Run、默认 Deck/Hero 或第二条写入链。

## 2. 已变更表面

| 层 | 变更 |
|---|---|
| 职业运行时 | `TinySpire/Assets/Scripts/Battle/MachineGunner/MachineGunnerBattleRuntime.cs` 注册 3249 的 Self `Invisible +2` 程序；攻击程序增加受限 `PreservesInvisibleAfterSuccessfulAttack` 声明，3247/3248 显式保留。 |
| 状态生命周期 | `TinySpire/Assets/Scripts/Battle/MachineGunner/MachineGunnerCombatState.cs` 的 `ReduceDuration` 只执行减层；运行时在玩家行动结束和成功攻击归宿后分别调用它。 |
| EditMode 回归 | `TinySpire/Assets/Editor/Tests/MachineGunnerStarterRuntimeTests.cs` 增加四项 3249 行为锁定；fixture 使用 2 Energy、Self、Skill 和 Program 49。 |
| 作者表 | `DataTables/Datas/battle.card.xlsx` 仅 Q139 从 `CatalogOnly` 翻为 `Implemented`。 |
| 生成和门禁 | `TinySpire/Assets/GameData/battle_tbcard.json`、卡牌目录构建校验与机枪兵快照更新到 41 / 23，并新增 3249 元数据快照。 |

## 3. 已加入的回归用例

| 用例 | 锁定事实 |
|---|---|
| `OpticalCamo_AppliesTwoInvisibleAndDiscards` | 支付成功后以私有状态 settlement 写入 Invisible 0→2，且 Hand→DiscardPile。 |
| `OpticalCamo_OrdinaryAttacksConsumeButDeclaredPreservingAttacksDoNot` | 普通肘击和普通射击各消耗一层；3247/3248 保留两层，且 3248 仍读取既有 FirePower。 |
| `OpticalCamo_PlayerActionEndConsumesRemainingInvisibleBeforeIncomingDamage` | 先由普通攻击将 2 层减为 1，玩家行动结束再减为 0；随后敌方 10 点伤害不再被减半，锁定时机。 |
| `OpticalCamo_FailedNonSniperAttackDoesNotConsumeInvisible` | 弹药不足的普通射击返回失败，Invisible、手牌与其他事实均不写入。 |
| `GeneratedCatalog_OpticalCamoKeepsAuthoredMetadata` | 直接读取 Luban JSON，锁定 3249 的基础/升级费用、Self、两处 DiscardPile、升级标记、Implemented 状态与 Program 49。 |

## 4. 本轮证据与验收结论

| 项目 | 结果 | 证据/说明 |
|---|---|---|
| 作者工作簿 | 通过 | 精确值差异、重新导入、渲染和公式错误扫描只确认 Q139；最终 SHA-256 为 `4F46A9D6D7F570686D898394AC0D249E4150BBD9BC3661204CDC11495546327F`。 |
| Luban | 通过 | 已执行生成命令，生成器移除的 `TinySpire/Assets/GameData/game-config.json` 已立即从 `DataTables/game-config.json` 恢复并以 SHA-256 核对一致。 |
| 生成 JSON | 通过 | 64 张机枪兵目录为 41 张 `Implemented` / 23 张 `CatalogOnly`；3249 为 Program 49、Cost 2、升级 Cost 1、Self、DiscardPile。 |
| 静态编译 | 通过 | `dotnet build TinySpire/Assembly-CSharp-Editor.csproj --no-restore`：0 errors；保留 Unity 依赖图既有的 12 条 `MSB3277` 警告。 |
| `Sync and Build All` 与本地 Addressables | 通过 | 2026-08-11 在唯一既有 Unity 6000.5.5f1 Editor 重新执行菜单。Console 记录 BuildLayout 写入、`Addressable content successfully built (duration : 0:00:28.345)` 及 `TinySpire sync and local content build completed successfully.`；新的 BuildLayout 同时列出 `Assets/GameData/battle_tbcard.json`，并由 `AssetBundleProvider` 打包。 |
| Unity EditMode | 通过 | 最终 Unity MCP 任务 `e2f9b873188a4ed7a12a2f073f90b492`：75/75 passed，0 failed，0 skipped，1.5422825 秒；覆盖 `MachineGunnerStarterRuntimeTests`、`MachineGunnerDamagePipelineTests`、`BattleCardPlayRulesTests`、`MachineGunnerCatalogSnapshotMG2ATests` 与 `BattleCardCatalogBuildValidatorTests`。同步后的首次测试请求经历编辑器域重载断连，稳定后复跑的本任务才是验收事实。 |

## 5. 验收完成后的后续顺序

后续 CatalogOnly 卡继续采用“独立机制切片 → 精确表项翻转 → Luban → `Sync and Build All` → 定向 EditMode”的顺序。`PreservesInvisibleAfterSuccessfulAttack` 仅表达成功攻击后的隐身生命周期，不能据此提前实现 3264 的延迟狙击、双词条伤害公式、升级实例、HUD/透明表现、场景、Prefab、默认 Hero/Deck、奖励/Run 或第二写入链。
