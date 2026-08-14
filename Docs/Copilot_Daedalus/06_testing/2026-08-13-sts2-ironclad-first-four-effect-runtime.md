---
title: STS2 Ironclad 首批四张基础卡与通用 DrawCards
page_type: testing
lifecycle: active
date: 2026-08-13
updated: 2026-08-13
status: verified-unity-native-2026-08-13
status_source: ../SESSION_LOG.md
source:
  - ../04_research/2026-08-06-sts2-v01071-ironclad-card-snapshot.md
  - ../01_requirements/2026-08-13-sts2-v01071-ironclad-first-four-runtime-digest.md
related_plan: ../plans/2026-08-06-sts2-v01071-ironclad-card-pool.md
related_decision: ../CODE_DECISIONS.md#cd-099ironclad-首批四张基础卡通过通用-effect-序列与-prepareddraw-接入
---

# STS2 Ironclad 首批四张基础卡与通用 DrawCards

本页记录 Pommel Strike、Shrug It Off、Twin Strike、Bludgeon 四张基础态，以及公共 `DrawCards` Effect、有序 Effect 组合和 `PreparedDraw` 深事务的正式作者表、生成、本地化、Addressables 与 Unity 原生验收证据。

## 1. 验收结论

- Pommel Strike（3113）按 `Damage 9 → Draw 1 → Discard` 结算；Shrug It Off（3115）按 `Block 8 → Draw 1 → Discard` 结算。
- Twin Strike（3120）以 `damage:4008, damageRepeat:4008` 按序独立结算两次 5 点伤害；Bludgeon（3123）结算 32 点伤害。四张卡均通过现有费用、目标、战斗 Effect 与成功归宿链，不含卡牌 ID 或文本分支。
- 新增公共 `EffectType.DrawCards = 4` 与 Effect 4010；`BattleCardEffectSequenceExecutor` 在首次写入前冻结 Draw 前 Effect、至多一次 Draw、Draw 后 Effect 的完整投影和连续记录顺序。
- `BattleCardZonesData.PrepareDraw` / `ValidatePreparedDraw` / `CommitPreparedDraw` 继续独占 Hand 10 上限、旧 DiscardPile 重洗、洗牌随机、最终布局与一次性提交。Draw 前致死仍抽；满手抽 0 不推进随机；非法目标、费用或绑定保持完整零写入。
- 冻结 Ironclad 85 张目录当前为 **8 `Implemented` / 77 `CatalogOnly`**。四张新卡只有基础态运行路径通过本页自动门禁，其余目录与升级实例没有被顺带开放。

## 2. 正式数据快照

| 卡牌 | 精确正式绑定 | 基础结果 |
|---|---|---|
| 3113 `POMMEL_STRIKE` | `damage:4009, cards:4010` | DealDamage 9；DrawCards 1。 |
| 3115 `SHRUG_IT_OFF` | `block:4011, cards:4010` | GainBlock 8；DrawCards 1。 |
| 3120 `TWIN_STRIKE` | `damage:4008, damageRepeat:4008` | DealDamage 5 两次。 |
| 3123 `BLUDGEON` | `damage:4007` | DealDamage 32。 |

| 工作簿 | 正式 SHA-256 |
|---|---|
| `battle.card.xlsx` | `54DA52D0C80885A2D55AEC8E260207E2D4E27AC8251304BF0710DB180EBC4EBB` |
| `battle.card_effect.xlsx` | `B616F993F5373AFF2DDD764E9C431A2C13F66CD3C5B2F39595B4A813FB7863BC` |
| `__enums__.xlsx` | `B9F8DD24C77EE64FA36C6DC7FEA5C0D83229011F45463A99ABAE30B3A7870B26` |
| `i18n.xlsx` | `7E91C7F46AEBBBF20188690EC49B1B5C3F6C84C2EF3A9531D22D42E3E23644F8` |

Effect 4007～4011 精确为 `DealDamage 32`、`DealDamage 5`、`DealDamage 9`、`DrawCards 1` 与 `GainBlock 8`；全部使用 `Attribute.None`。`DrawCards` 的枚举整数值为 4。

## 3. 事务与顺序门禁

| 场景 | 锁定事实 | 结果 |
|---|---|---|
| Bludgeon / Twin Strike | Energy → 每段 Damage → 当前牌 Discard；Twin 每段独立读写投影。 | 通过 |
| Pommel / Shrug | Energy → Damage 或 Block → Draw → 当前牌 Discard。 | 通过 |
| Twin 首击致死 | 第二段保留 `BattleOperationSkippedSettlement`，不重复伤害；当前牌和终局阶段仍正确提交。 | 通过 |
| Draw 前致死 | 已冻结 Draw 不因目标死亡取消。 | 通过 |
| 旧弃牌重洗 | DiscardPile→DrawPile 记录、reshuffle 记录、DrawPile→Hand 记录连续且随机只在成功提交时推进。 | 通过 |
| Hand 已满 10 | Draw 0、布局与随机保持不变；当前牌随后正常进入 DiscardPile，最终 Hand 为 9。 | 通过 |
| Draw 后 Effect | 使用 Draw 前完整战斗投影；中间卡区操作不丢失 Strength / HP 等投影事实。 | 通过 |
| 非法输入 | 第二个 Draw、Draw 非 `Attribute.None`、负值、能量不足或缺少显式目标均在首次写入前失败。 | 通过 |
| PreparedDraw 漂移 | 跨 Owner、重复提交、布局或随机快照漂移均拒绝；失败不发布局。 | 通过 |

## 4. TDD 与正式 Unity 证据

| 层级 | 结果 | 证据 |
|---|---:|---|
| 精确 TDD 红灯 | 2 passed / 2 failed | 任务 `2f8fa9d405e94893b9a0cc600faff777`；Pommel / Shrug 的唯一红因均为 `UnsupportedEffectType`，精确暴露公共 Draw Effect 缺口。 |
| 单元绿灯 A | 7/7 | 任务前缀 `8e1a…`。 |
| 单元绿灯 B | 3/3 | 任务前缀 `839889…`。 |
| 单元绿灯 C | 42/42 | 任务前缀 `0f8efd…`。 |
| 正式 smoke | 20/20 | `49d34997a550459f98b80d6ee88deec0`，1.0011866 秒。 |
| 正式聚合 | 67/67 | `c3281b04224845eaa4138ea5024904a0`，26.3892428 秒。 |
| 完整 EditMode | 713/713 | `0856b63a9ad44ea08a8a37d0df803571`，22.4028613 秒。 |

正式 smoke、聚合与完整任务均无 failed / skipped。Runtime 静态编译为 0 error / 6 warning，Editor 为 0 error / 12 warning。

## 5. Luban、本地化与 Addressables

| 项目 | 结果 | 说明 |
|---|---:|---|
| Luban | 通过 | 2026-08-13 00:42:37 成功；生成数据锁定四张卡、Effect 4007～4011、`DrawCards=4` 与 Ironclad 8/77。 |
| Localization import | 通过 | 四张卡 en / zh-CN 基础与升级说明已导入。 |
| Localization validate | 通过 | 最终参数键使用 `damageRepeat`；未放宽参数 validator。 |
| `Sync and Build All` | 通过 | 同次同步与本地内容构建完成。 |
| Addressables | 通过 | 13.595 秒；BuildLayout `buildlayout_2026.08.13.00.44.31.json`。 |

Localization 诊断保留两轮事实：第一轮先确认失败读取的是 stale config；刷新到当前生成配置后，第二轮才显露参数规范不允许下划线。最终把 Twin Strike 的第二绑定统一为 `damageRepeat`，说明文本只使用 `{damage}`，没有以放宽 validator 掩盖不合规的 `damage_repeat`。

## 6. 验收边界

- 本页只证明四张基础卡和公共 Draw / PreparedDraw 垂直切片，不代表原计划 I5 的每步独立目标能力整体完成。
- 升级实例、升级数值、其余 77 张 `CatalogOnly`、默认 Deck、奖励选择、Run 持久牌组、UI 新流程与多人卡牌均未实现；I14 的逐卡真实 BattleScene / Game View 验收仍未完成。
- 四张卡仍使用既有牌面 Addressables 契约；本轮没有新增素材域、Scene、Prefab、ProjectSettings、asmdef、DI 或第二条命令队列。
- 本切片未创建提交，也不记录或杜撰提交 SHA。
