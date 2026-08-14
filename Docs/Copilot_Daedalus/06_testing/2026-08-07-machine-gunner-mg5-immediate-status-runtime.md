---
title: Marine Game 机枪兵 MG5 即时状态首批运行时验收
page_type: testing
lifecycle: active
created: 2026-08-07
status_source: ../SESSION_LOG.md
related_plan: ../plans/2026-08-07-marine-game-card-only-integration.md
---

# Marine Game 机枪兵 MG5 即时状态首批运行时验收

## 范围

本记录验收 Hero 1002 会话私有 `MachineGunnerBattleRuntime` 的第二批 MG5 卡牌程序。默认 Hero 1001、共享 `BattleCommandQueue.Submit` 写入入口、场景和 UI 输入均未改变。

本切片仅将下列 5 张卡从 `CatalogOnly` 翻为 `Implemented`；它们与既有 16 张形成精确的 21 张可执行集合：`StunGrenade` (3215)、`SmokeBomb` (3221)、`KidneyShot` (3228)、`PainfulElbow` (3229) 与 `SniperShot` (3247)。

## 已验收行为

- 状态程序和前序伤害在首次共享写入前一并预演。投影死亡的敌人会跳过后置 Weakness/Vulnerable，故全体伤害不会给已死亡目标写状态。
- `StunGrenade` 对全体存活敌人造成 8 点攻击伤害后，只给仍存活者 `Weakness +1`；`KidneyShot` 对显式敌人造成 8 点攻击伤害后 `Weakness +1`。
- `SmokeBomb` 给玩家 `Block +10`，并给玩家与每个存活敌人 `Smoke +3`。Weakness/Smoke 都由职业私有状态结算记录承载，不伪造通用 `BattleEffectId`，也不增添未实现的状态 UI cue。
- `PainfulElbow` 对显式敌人造成 10 点攻击伤害后 `Vulnerable +2`；该通用状态继续走已有时机和 `VulnerableIconPulse`，但职业程序 settlement 的 `EffectId` 明确为空。
- `SniperShot` 自动取最远存活敌人、支付 1 Energy/2 Ammo、使用狙击伤害倍率、不接收 Stim 额外命中，伤害后施加 `Vulnerable +1`。

## 配置与构建验证

| 项目 | 结果 |
| --- | --- |
| `battle.card.xlsx` | 工作簿导入、值差异、重新导入和渲染复核通过；仅 3215、3221、3228、3229、3247 的 `implementation_status` 单元格由 `CatalogOnly` 改为 `Implemented`。 |
| Luban | 直接等价命令成功完成 validation 与 `battle_tbcard.json` 生成；随后恢复 Luban 会移除的 `game-config.json` 基础设施清单。 |
| 生成 JSON | `marine-game-v1-20260807-cards` 快照共 64 张，其中 21 张 `Implemented`、43 张 `CatalogOnly`；已实现 ID 为 3201--3205、3214--3215、3220--3221、3224--3230、3232--3233、3247、3256、3258。 |
| Unity 同步构建 | 已连接的单一 Unity 6000.5.5f1 Editor 执行 `TinySpire/Build/Sync and Build All` 成功；控制台记录 Addressable content successfully built（10.27 秒）和整体同步构建成功。 |

## Unity 定向回归与 MCP 观察

| 项目 | 结果 |
| --- | --- |
| 刷新编译 | Unity 刷新与 domain reload 完成；控制台未发现产品脚本编译错误。 |
| 请求的测试 | `MachineGunnerStarterRuntimeTests.MG5ImmediateStatusPrograms_ResolveThroughPrivateRuntimeInDeclaredOrder` 与 `MachineGunnerCatalogSnapshotMG2ATests.GeneratedCatalog_MarineGameV1SnapshotPassesStarterRuntimeValidation`。 |
| 原生结果 | 同一次 Unity Test Runner 写入的 `TestResults.xml` 记录 **2/2 passed，0 failed，0 skipped**，总时长 0.2131117 秒；两条请求的测试均为 Passed。 |
| MCP 任务观察 | Unity MCP 任务 `bd475e4578fe4572a6751c80e7f1cf47` 因未在 60 秒内收到初始化回调而显示 failed；控制台同时有 TestRunner 的启动/恢复日志及结果文件保存记录。原生结果文件证明产品断言通过，但该 MCP 状态传递偏差应另行复现，不把它写成测试失败。 |
| 控制台说明 | error-filter 仅返回 Unity Test Framework 的“Saving results to TestResults.xml” Exception 输出，无产品堆栈或编译错误。 |

## 未包含

- `SpikeShot` (3248) 需要逐段 `OnShotHit`：每一段伤害后立即 Weakness/Vulnerable，使 Stim 的后续命中读取前段状态；本切片没有把它降级为整卡结束时的一次状态写入。
- `GasPump`、Napalm、Molotov、ExplosiveElbow、FlameElbow 等 Burn 相关卡仍等待玩家行动结束后的燃烧结算与伤害是否可被 Block 阻挡的口径冻结。
- `IncompleteCombustion` 仍需 Exhaust、燃烧者×实时存活目标的交叉结算、Burn→Smoke 转换和死亡顺序专项测试。
- 未修改升级实例、奖励/Run、动态临时卡、选择协议、Hero/Deck、Scene、Prefab、ProjectSettings、asmdef、HybridCLR、启动 DI 或受保护 Targeting/Candidates/Hermes 美术路径；未暂存、提交或推送。
