---
title: Marine Game 机枪兵 V2F 烟雾、防御与标记即时卡
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
related_decision: ../CODE_DECISIONS.md#cd-085v2f-烟雾防御与标记复用既有即时事务
---

# Marine Game 机枪兵 V2F 烟雾、防御与标记即时卡

## 1. 验收对象与冻结行为

本切片只开放 3 张 V2 扩展卡的基础态，全部复用现有即时事务与职业私有状态：

| 卡牌 | 验收行为 |
|---|---|
| `ChainSmoke` (3269) | 1 Energy、Skill、Self、Hand→DiscardPile；只为施放者增加 `Smoke +5`。本地化卡名中的“抽”不构成抽牌指令，程序不产生 Draw 或卡区补牌。 |
| `EmergencyCooling` (3272) | 1 Energy、Skill、Self、Hand→DiscardPile；严格先获得 8 Block，再为施放者增加 `Smoke +3`。 |
| `Mark` (3280) | 0 Energy、Attack、显式 Enemy、Hand→DiscardPile；支付 1 Ammo，先造成 5 点普通 Attack，目标仍存活时才追加 `ArmorBreak +2`。程序显式使用 `Tags.None`，不触发 Stim 额外段、FirePower 或 IncendiaryAmmo。 |

三张卡的升级列仍只作为作者表元数据保留；当前没有升级 `CardInstance`。本切片也没有把卡加入默认 Deck、奖励或 Run。

## 2. 已变更表面

| 层 | 变更 |
|---|---|
| 作者表/生成配置 | `battle.card.xlsx` 只将 3269、3272、3280 的 `implementation_status` 翻为 `Implemented`；Luban 生成后的目录为 82 项、59 `Implemented` / 23 `CatalogOnly`。 |
| 职业程序 | `MachineGunnerBattleRuntime` 注册来源 Smoke、Block→Smoke 和普通 Attack→存活后 ArmorBreak 三条基础程序；没有增加新状态或新伤害种类。 |
| 分类约束 | `Mark` 显式声明 `MachineGunnerCardTag.None`；Ammo 成本与 Attack 类型均不会被用来推断 `Shoot` 或 `Sniper`。 |
| 目录门禁 | V2 扩展快照只新增上述 3 个 `Implemented` 身份，V1 保持 53/11，V2 扩展更新为 6/12。 |

## 3. 已加入的定向回归

| 用例 | 锁定事实 |
|---|---|
| `ChainSmoke_AddsFiveSourceSmokeAndDiscards` | 支付 1 Energy，只为施放者增加 Smoke +5，敌人状态、玩家 Block 与抽牌事实不被改写，成功后进入 DiscardPile。 |
| `EmergencyCooling_GainsBlockBeforeSmokeAndEnergyFailureWritesNothing` | 8 Block settlement 严格位于 Smoke +3 之前；能量不足时资源、状态、卡区与职业随机流零写入。 |
| `Mark_DamagesBeforeApplyingArmorBreakToLivingTarget` | 支付 1 Ammo，先结算 5 点普通 Attack，再对存活目标紧邻写入 ArmorBreak +2。 |
| `Mark_LethalHitSkipsArmorBreakAndAmmoFailureWritesNothing` | 致死命中不向死亡目标写破甲；弹药不足时伤害、状态、资源、卡区与随机流零写入。 |
| `Mark_NoneTagsIgnoreStimFirePowerAndIncendiaryAmmo` | `Tags.None` 下只有一段 5 点 Attack，不吃 Stim、FirePower 或 IncendiaryAmmo。 |
| `GeneratedCatalog_MarineGameV2ExtensionKeepsAuthoredMetadataAndImplementationGate` | 18 个 V2 扩展身份的元数据和 6/12 精确实现状态保持冻结。 |

## 4. 数据、同步与 Unity 证据

| 项目 | 结果 | 说明 |
|---|---|---|
| Luban 与生成配置 | 通过 | 3269、3272、3280 的基础态状态翻转成功；82 模板为 59 `Implemented` / 23 `CatalogOnly`，V1 为 53/11，V2 为 6/12。 |
| 本地化 | 通过 | 从 Excel 导入战斗卡本地化成功。 |
| Sync 与本地 Addressables | 通过 | 唯一既有 Unity Editor 的 `TinySpire/Build/Sync and Build All` 成功；Addressables 构建耗时 11.414 秒。 |
| Unity 定向 EditMode | 通过 | MCP 任务 `054f72bc921749b5bad6d2efcc358b73`：83/83 passed，0 failed。 |
| Unity 完整 EditMode | 通过 | MCP 任务 `ba418ab34a6d44038dddddc0233a03f8`：611/611 passed，0 failed，耗时 18.618 秒。 |

首次 MCP 任务（ID 以 `056109` 开头）在测试初始化阶段超时，实际执行 0 项；它不构成失败回归。确认没有可用测试结果后进行了环境重试，上表记录的是随后真实执行完成的定向与完整任务。

## 5. 验收后边界

- 本切片没有新增 Draw、卡牌创建、Power 触发、跨卡事件或回合调度；卡名或显示文本不参与程序语义判断。
- 未实现升级实例，也未修改默认 Deck、奖励、Run、UI、多人、Scene 或 Prefab。
- 其余 23 张 `CatalogOnly` 仍由精确目录门禁拒绝，临时卡、选择、自动免费攻击、恢复与其他跨卡协议仍需独立切片。
- 本切片未创建提交，也不记录或杜撰提交 SHA。
