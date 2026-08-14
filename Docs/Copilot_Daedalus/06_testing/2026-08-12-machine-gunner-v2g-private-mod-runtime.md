---
title: Marine Game 机枪兵 V2G 私人改装基础态
page_type: testing
lifecycle: active
date: 2026-08-12
updated: 2026-08-12
status: verified-unity-native-2026-08-12
status_source: ../SESSION_LOG.md
source:
  - ../00_inbox/README web.md
  - ../01_requirements/2026-08-12-machine-gunner-v2-requirement-digest.md
related_plan: ../plans/2026-08-12-machine-gunner-v2-card-runtime-plan.md
related_decision: ../CODE_DECISIONS.md#cd-086v2g-私人改装基础态复用既有-power-事务
---

# Marine Game 机枪兵 V2G 私人改装基础态

## 1. 验收对象与冻结行为

本切片只开放 `PrivateMod` (3268) 的基础态：

- 1 Energy、Uncommon、Power、Self、Hand→PowerPile。
- 成功施放后 `AmmoMaximum +1`，但当前 Ammo 保持原值，不把“提高上限”偷换为立即装填。
- 同一出牌事务中增加 `FirePower +1`，并记录 `PrivateMod` Power 层数；既有 Shoot 伤害管线对后续每一段射击读取这层 FirePower。
- 后续装填按提高后的 AmmoMaximum 补充；当前没有升级 `CardInstance`，升级列不参与本次运行时。

本切片没有把卡加入默认 Deck、奖励或 Run，也没有修改 UI、多人或卡牌选择流程。

## 2. 已变更表面

| 层 | 变更 |
|---|---|
| 作者表/生成配置 | `battle.card.xlsx` 只将 3268 的 `implementation_status` 翻为 `Implemented`；Luban 生成后的目录为 82 项、60 `Implemented` / 22 `CatalogOnly`。 |
| 职业程序 | `MachineGunnerBattleRuntime` 注册 `PrivateMod` Power：Power 应用阶段只扩展 AmmoMaximum，已有程序操作在同一事务增加 FirePower。 |
| 既有伤害/资源规则 | 不新增命中后钩子；FirePower 继续由 Shoot 的逐段伤害规则读取，装填继续补至当时的 AmmoMaximum。 |
| 目录门禁 | V2 扩展快照只新增 3268 为 `Implemented`；V1 保持 53/11，V2 扩展更新为 7/11。 |

## 3. 已加入的定向回归

| 用例 | 锁定事实 |
|---|---|
| `PrivateMod_RaisesAmmoMaximumWithoutRefillAndBuffsEveryShootHit` | 支付 1 Energy 后进入 PowerPile；AmmoMaximum 5→6、当前 Ammo 仍为 2、FirePower 0→1、PrivateMod Power 层数为 1；后续 Shoot 的两段伤害都获得 FirePower 加成，装填再补至新上限 6。 |
| `PrivateMod_InsufficientEnergyLeavesResourcesStatusesAndZonesUnchanged` | 能量不足时当前/最大 Ammo、FirePower、Power 层数、随机流和卡区均零写入。 |
| `PowerProgramRegistry_ContainsImplementedPowerKinds` | Power 程序注册表包含 `ProgramId.PrivateMod → PowerKind.PrivateMod` 的精确映射。 |
| `GeneratedCatalog_MarineGameV2ExtensionKeepsAuthoredMetadataAndImplementationGate` | 18 个 V2 扩展身份的元数据和 7/11 精确实现状态保持冻结。 |

## 4. 数据、同步与 Unity 证据

| 项目 | 结果 | 说明 |
|---|---|---|
| Luban 与生成配置 | 通过 | 3268 的基础态状态翻转成功；82 模板为 60 `Implemented` / 22 `CatalogOnly`，V1 为 53/11，V2 为 7/11。 |
| 本地化 | 通过 | 从 Excel 导入战斗卡本地化成功；首轮同步后重新导入，再执行最终同步构建。 |
| Sync 与本地 Addressables | 通过 | 首轮 `Sync and Build All` 的 Addressables 构建为 11.092 秒；重新导入本地化后的最终构建为 4.376 秒，最终有效内容已重建。 |
| Unity 定向 EditMode | 通过 | MCP 任务 `cfcf49e9a16e447fb4033af6108c8dd9`：85/85 passed，0 failed。 |
| Unity 完整 EditMode | 通过 | MCP 任务 `0f17edd2f31d40d2aba328def4448f3c`：613/613 passed，0 failed，耗时 18.617 秒。 |

首次以 `9e20` 开头的 MCP 任务在测试初始化阶段超时，实际执行 0 项；它不构成失败回归。确认没有可用测试结果后重试，上表记录的是随后真实执行完成的定向与完整任务。

## 5. 验收后边界

- 本切片只实现 3268 基础态；没有实现升级实例。
- 没有修改默认 Deck、奖励、Run、UI、多人、Scene 或 Prefab。
- 没有接入临时卡、手牌选择、自动免费攻击、帮手/支援命中后钩子或其他跨卡协议。
- 其余 22 张 `CatalogOnly` 继续由精确目录门禁拒绝。
- 本切片未创建提交，也不记录或杜撰提交 SHA。
