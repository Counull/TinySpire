---
title: Marine Game 机枪兵 V2H 焚风基础态
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
related_decision: ../CODE_DECISIONS.md#cd-087v2h-焚风以跨参与者预演原子转换烟雾与燃烧
---

# Marine Game 机枪兵 V2H 焚风基础态

## 1. 验收对象与冻结行为

本切片只开放 `FoehnWind` (3276) 的基础态：

- 2 Energy、Skill、显式 Enemy、Hand→DiscardPile。
- 成功结算时读取施放者当时的全部 Smoke。Smoke 大于 0 时，以该值作为一次 `ApplyBurn` 的基础值施加给目标；目标已有 Oil 继续按既有规则增加本次 Burn，并在施加后减半。
- 同一事务内的私有状态 settlement 顺序固定为目标 Burn → 可选的目标 Oil → 施放者 Smoke 清零，随后才记录卡区归宿。Smoke 只在 Burn/Oil 成功提交后清零。
- 施放者 Smoke 为 0 时仍是合法成功出牌：支付 2 Energy 并弃牌，但不制造任何私有状态写入或 `0→0` settlement。

本切片没有把卡加入默认 Deck、奖励或 Run，也没有实现升级 `CardInstance`、UI、多人或其他跨卡协议。

## 2. 已变更表面

| 层 | 变更 |
|---|---|
| 作者表/生成配置 | `battle.card.xlsx` 只将 3276 的 `implementation_status` 翻为 `Implemented`；Luban 生成后的目录为 82 项、61 `Implemented` / 21 `CatalogOnly`。 |
| 职业程序 | `MachineGunnerBattleRuntime` 注册 `FoehnWind`，以专用 `ConvertSourceSmokeToTargetBurn` 操作联合预演来源 Smoke、目标 Burn 与目标 Oil。 |
| 提交顺序 | 提交前核对跨参与者快照，再复用既有 `ApplyBurn` 规则按 Burn → 可选 Oil → Smoke 顺序写 settlement；没有新增 Queue 外写入入口。 |
| 目录门禁 | V2 扩展快照只新增 3276 为 `Implemented`；V1 保持 53/11，V2 扩展更新为 8/10。 |

## 3. 已加入的定向回归

| 用例 | 锁定事实 |
|---|---|
| `FoehnWind_ConsumesSmokeAfterBurnAndOilThenDiscards` | 施放者 Smoke 5、目标 Burn 2/Oil 3 时，目标 Burn 2→10、Oil 3→1、施放者 Smoke 5→0；settlement 及弃牌顺序连续。 |
| `FoehnWind_ZeroSmokeStillPaysEnergyAndDiscardsWithoutStatusChanges` | Smoke 为 0 时仍支付 2 Energy 并进入 DiscardPile，但没有私有状态 settlement。 |
| `FoehnWind_InsufficientEnergyLeavesStatusesAndZonesUnchanged` | 能量不足时 Energy、Smoke、目标 Burn/Oil、随机流与卡区均零写入。 |
| `FoehnWind_PlayerTargetLeavesResourcesStatusesAndZonesUnchanged` | 把显式敌方目标错误指向玩家时返回 `TargetRuleMismatch`，资源、私有状态、随机流与卡区均零写入。 |
| `GeneratedCatalog_MarineGameV2ExtensionKeepsAuthoredMetadataAndImplementationGate` | 18 个 V2 扩展身份的元数据和 8/10 精确实现状态保持冻结。 |

## 4. 数据、同步与 Unity 证据

| 项目 | 结果 | 说明 |
|---|---|---|
| Luban 与生成配置 | 通过 | 3276 的基础态状态翻转成功；82 模板为 61 `Implemented` / 21 `CatalogOnly`，V1 为 53/11，V2 为 8/10。 |
| 本地化 | 通过 | 从 Excel 导入战斗卡本地化成功。 |
| Sync 与本地 Addressables | 通过 | `Sync and Build All` 完成，Addressables 构建耗时 12.164 秒。 |
| Unity 定向 EditMode | 通过 | MCP 任务 `69b8ded02aaa46368cad35e620567fd2`：89/89 passed，0 failed。 |
| Unity 完整 EditMode | 通过 | MCP 任务 `4a90229794d24d3f8fd85154ab79c250`：617/617 passed，0 failed，耗时 17.657 秒。 |

首次以 `902bbc` 开头的 MCP 任务在测试初始化阶段超时，实际执行 0 项；它不构成失败回归。确认没有可用测试结果后重试，上表记录的是随后真实执行完成的定向与完整任务。

## 5. 验收后边界

- 本切片只实现 3276 基础态；没有实现升级实例。
- 没有修改默认 Deck、奖励、Run、UI、多人、Scene 或 Prefab。
- 没有接入临时卡、手牌选择、自动免费攻击、AnyAlly、帮手/支援命中后钩子或其他跨卡协议。
- 其余 21 张 `CatalogOnly` 继续由精确目录门禁拒绝。
- 本切片未创建提交，也不记录或杜撰提交 SHA。
