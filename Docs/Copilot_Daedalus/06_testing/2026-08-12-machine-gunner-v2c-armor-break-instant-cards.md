---
title: Marine Game 机枪兵 V2C 破甲即时卡
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
related_decision: ../CODE_DECISIONS.md#cd-082v2c-破甲即时卡以既有状态与统一伤害链接入
---

# Marine Game 机枪兵 V2C 破甲即时卡

## 1. 验收对象与冻结行为

本切片只开放 V2 扩展目录中复用现有状态与伤害链的两个基础态程序：

- `ThermiteBomb` (3273)：1 Energy、Skill、AllEnemies、Hand→DiscardPile；按程序操作顺序，对所有存活敌人先施加 `Burn +4`，再施加持续 `ArmorBreak +2`。前一操作保持既有 Oil 消耗/减半语义。
- `Crush` (3281)：1 Energy、Attack、自动最近敌人、Hand→DiscardPile；先造成 9 点普通 Attack，目标仍存活时才施加 `ArmorBreak +4`。

两张作者表的升级数值保留为元数据，当前 `CardInstanceData` 尚无升级态。本切片未把卡加入默认 Deck、奖励、Run、多人、UI、Scene 或 Prefab，也没有实现其他 V2 扩展卡。

## 2. 已变更表面

| 层 | 变更 |
|---|---|
| 作者表/生成配置 | `battle.card.xlsx` 仅将 3273、3281 的 `implementation_status` 翻为 `Implemented`；Luban 生成后的 `battle_tbcard.json` 保持其余元数据不变。 |
| 职业程序 | `MachineGunnerBattleRuntime` 注册 ThermiteBomb 的全体 Burn→ArmorBreak 操作和 Crush 的最近敌人 Attack→后置 ArmorBreak 操作。 |
| 目录门禁 | V2 扩展快照由过渡期“全部 CatalogOnly”变为精确状态门禁：ThermiteBomb、Crush 为 `Implemented`，其余 16 张为 `CatalogOnly`。 |
| 回归 | 增加铝热炸弹的 Encounter 顺序、Oil、易伤破甲 Burn、失败零写入，以及踏碎的最近目标、Block、后置破甲和弃牌归宿覆盖。 |

## 3. 已加入的回归

| 用例 | 锁定事实 |
|---|---|
| `ThermiteBomb_AppliesBurnThenArmorBreakToAllEnemiesAndAmplifiesLaterBurn` | 全体 Burn/Oil settlement 先于全体 ArmorBreak settlement；下一回合 Burn 读取 ArmorBreak，易伤只放大破甲附加值。 |
| `ThermiteBomb_InsufficientEnergyFailsWithoutWritingStatusesOrZones` | 能量不足时不写入状态、卡区或职业随机流。 |
| `Crush_AutomaticallyHitsNearestThenAppliesArmorBreakAndDiscards` | 自动最近目标先承受 9 点普通攻击；仅其仍存活时获得 ArmorBreak +4，随后 Hand→DiscardPile。 |
| `GeneratedCatalog_MarineGameV2ExtensionKeepsAuthoredMetadataAndImplementationGate` | 18 个 V2 身份的元数据与精确实现状态保持冻结。 |
| `SnapshotValidator_ImplementedV2ExtensionCard_Throws` | 未开放的 Mark 被改为 Implemented 时，构建门禁稳定失败。 |

## 4. 本轮证据与结论

| 项目 | 结果 | 证据/说明 |
|---|---|---|
| Luban 与生成配置 | 通过 | 3273、3281 的生成 `implementation_status` 均为 0；机枪兵目录为 82 项、48 `Implemented` / 34 `CatalogOnly`。 |
| 本地化 | 通过 | Unity 菜单 `TinySpire/Localization/Import Battle Card Text from Excel` 输出 `TinySpire battle card localization imported from Excel and validated.` |
| Sync 与本地 Addressables | 通过 | 唯一既有 Unity 6000.5.5f1 Editor 的 `TinySpire/Build/Sync and Build All` 输出本地内容构建成功，耗时 19.571 秒。 |
| Unity 定向 EditMode | 通过 | MCP 任务 `f60e712386064ac1b558bfc3f66a0c8f`：81/81 passed，0 failed，0 skipped。 |
| Unity 完整 EditMode | 通过 | MCP 任务 `65b4d39df04142409f9b8f0355a6d063`：589/589 passed，0 failed，0 skipped，15.3872738 秒。 |

首次定向 MCP 作业在测试尚未开始前超时；确认没有运行中作业后安全重试。上述 81/81 是实际执行的定向结果，而不是初始化超时的替代口径。

## 5. 验收后边界

该切片不新增 ArmorBreak 状态、回合生命周期、延迟调度、目标选择协议、伤害种类或第二条写入路径。`Mark` 的射击/狙击标签、`Knockback` 的失去力量和双目标时序、`FieldSurgery` 的多人目标/恢复/束缚，以及其余 V2 卡的延迟、选择、临时卡和升级实例仍需各自独立裁决与实现。
