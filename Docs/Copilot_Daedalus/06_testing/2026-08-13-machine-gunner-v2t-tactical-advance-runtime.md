---
title: Marine Game 机枪兵 V2T 战术推进二元免攻与共享费用冻结
page_type: testing
lifecycle: active
date: 2026-08-13
updated: 2026-08-13
status: verified-unity-native-2026-08-13
status_source: ../SESSION_LOG.md
source:
  - ../00_inbox/README web.md
  - ../01_requirements/2026-08-12-machine-gunner-v2-requirement-digest.md
related_plan: ../plans/2026-08-12-machine-gunner-v2-card-runtime-plan.md
related_decision: ../CODE_DECISIONS.md#cd-100v2t-战术推进以独立二元授权和共享费用解算冻结下一张成功攻击
---

# Marine Game 机枪兵 V2T 战术推进二元免攻与共享费用冻结

本页记录 V2T 已完成的 `TacticalAdvance` (3234) 基础态、下一张成功 Attack 的二元费用豁免、公共 Fixed / X 费用冻结 seam、机枪兵 Ammo / Stim / Guerrilla 组合口径，以及正式作者表、Luban、本地化、Addressables 与 Unity 原生验收证据。

## 1. 验收对象与冻结行为

- Program 34 基础态为 2 Energy、Skill、Self、Hand→DiscardPile；成功时先获得 10 Block，再授予“下一张成功 Attack 免费”的二元授权。连续成功施放只刷新一份授权，不叠加可用次数。
- 授权跨玩家回合保留；Skill 不消费，Shackle、目标错误、费用/计划校验或卡区容量失败也不消费。第一张成功 Attack 按豁免费用完成全部效果与成功归宿后才消费授权；即使该攻击致死也必须消费，下一张 Attack 恢复正常费用。
- Shackle 仍由既有上游 Attack 门禁在费用解算前拒绝，不能借免攻绕过。授权使用独立 `bool + revision` 生命周期，不加入 `MachineGunnerCombatantStatus`，因此它本身不增加 3277 / 3278 的状态种类计数。本页发布时私有状态为 16 种；CD-104 后因新增 Regeneration，当前为 17 种。
- 当前来源 `README web.md`、正式作者表与 i18n 一致给出基础 10 Block / 升级 14 Block；历史 `HANDOFF.md` 的 12/16 被当前来源覆盖。升级 14 仅是作者表元数据，尚无升级 `CardInstance` 运行时。

## 2. 共享费用冻结与职业适配

| 层 | 已验收口径 |
|---|---|
| `BattleCardCostResolver` | 纯解算并冻结 Fixed / X 的实际支付值、效果读取值与触发器名义值；Normal 与 Waived 使用同一结果模型。 |
| 通用卡牌 | `BattleCardPlayRules` 与 `BattleTurnController` 的普通 Fixed 费用是一个真实适配器；既有通用 X 路径未在本切片迁移或扩展。 |
| 机枪兵 | 职业适配器在同一准备成本中分别冻结 Energy、Ammo actual / effect / nominal 与 Stim 额外段，再由职业伤害、游击和成功生命周期消费各自字段。 |
| 免攻 | Waived 只把实际 Energy / Ammo 支付归零，不压扁效果规模或触发器名义值；成功前保持准备快照，成功归宿后才提交授权消费。 |
| Stim / Guerrilla | Fixed 与 UpToLimit 在免攻下继续保留 Stim 额外段，且该段和基础效果一起进入 Guerrilla 的名义耗弹；AllAvailable 保留既有免费 Stim 段，不把它伪造成名义 Ammo。 |
| 其他联动 | `ComboElbow` 的最近攻击分类与本授权正交，未借费用豁免改变其身份或生命周期。 |

该 seam 可供未来其他职业费用适配器复用，但本切片没有实现战士免攻卡或改变 Ironclad 运行时；也没有把通用 X 全面迁移到新解算器。

## 3. 关键事务与失败门禁

| 场景 | 必须锁定的事实 | 当前结果 |
|---|---|---|
| 二次施放 | 两张 3234 各支付 2 Energy、各获得 10 Block，只保留一份免攻授权。 | 通过（V2T 6/6） |
| Skill / 跨回合 | Skill 和玩家回合切换不消费授权；之后第一张成功 Attack 免费，下一张恢复正常费用。 | 通过（V2T 6/6） |
| Shackle / 缺目标 | Attack 在首次写入前失败，授权、资源、伤害、状态、随机、卡区和结果保持不变；合法重试仍免费。 | 通过（V2T 6/6） |
| Fixed / UpToLimit / X + AllAvailable | 豁免下实际 Energy / Ammo 为 0，既有固定段、按上限段、X 效果规模和 AllAvailable 弹药段保持原语义。 | 通过（V2T 6/6） |
| Stim + Guerrilla | Fixed 的 Stim 额外段保留并计入 Guerrilla 名义耗弹；Block 位于伤害之后、当前牌离手之前。UpToLimit 使用同一已审查费用分支，AllAvailable 保留既有免费 Stim 分支；后两项在本片没有各自独立的 Stim 组合运行用例。 | Fixed 组合通过（V2T 6/6）；UpToLimit / AllAvailable Stim 为结构证据 |
| 致死 / 效果溢出失败 | 成功致死 Attack 在归宿后消费授权；成功前的 `EffectValueOverflow` 保持授权和全部战斗事实零写入。 | 通过（补强 1/1） |
| 状态计数回归 | 本页发布时免攻不成为第 17 种私有状态；CD-104 后 Regeneration 才是当前第 17 种，免攻仍不进入 3277 / 3278 的状态种类计数。 | 通过（V2T 历史正式聚合 + CD-104 当前回归） |

## 4. TDD、审查与 Unity 证据

| 层级 | 结果 | 证据 |
|---|---:|---|
| 精确 TDD 红灯 | 1/1 failed | `5a0823dd6a2241e0818512b8855877a6`；3234 当时仍为不支持程序，精确暴露 Program 34 与成功授权链缺口。 |
| V2T 费用/生命周期矩阵 | 6/6 | `cddab7f295844f71999568465dc1f85e`；覆盖刷新、跨回合、失败重试、三类费用模式及 Stim / Guerrilla。 |
| 致死与溢出补强 | 1/1 | `da3a7e7e7c6540eca8400e99ba5c0ca4`；锁定成功致死消费与成功前 `EffectValueOverflow` 不消费。 |
| Starter 运行时类 | 142/142 | `e1dcce3dfc6c4078b769a921a98145b4`。 |
| 正式目录快照 | 36/36 | `d64c71abd6c2436a8f820efc976a9196`。 |
| 正式聚合 | 213/213 | `e4e6f701845547149384c1f6e792269e`；包含 CardIllustration 真实 Addressables AssetBundle 加载。 |
| 完整 EditMode | 721/721 | `fe108672fde44832a7fb4819116136c1`。 |

Runtime 静态编译为 0 error / 6 warning，Editor 为 0 error / 12 warning。最终双轴 production / spec review 均为 0 blocker；Standards 审查指出的冗余 `CardTemplateId` 准备字段已删除，复审未遗留数据或文档 blocker。

## 5. 数据、同步与 Addressables

| 项目 | 结果 | 说明 |
|---|---:|---|
| 正式作者表 | 已复核 | `battle.card.xlsx` SHA-256 `55D43141149D7A86D7957B1C43ED9303B9E9D091094E0CFAF2CF39FE2F73C569`；只把 Q124 从 `CatalogOnly` 翻为 `Implemented`。 |
| Luban 与生成配置 | 通过 | 2026-08-13 01:44:15 成功；全项目 Card JSON 168 个，Marine 82 为 74/8、V1 58/6、V2 16/2；3234 为 status 0 / Program 34 / 空 bindings / 非 Innate。 |
| 本地化 | 通过 | Localization import 与显式 validate 日志均成功；基础 10 Block 与升级 14 Block 保持当前来源口径。 |
| `Sync and Build All` | 通过 | 端到端 16.852 秒；本地 Addressables 构建 14.762 秒。 |
| BuildLayout | 已写出 | `Library/com.unity.addressables/BuildReports/buildlayout_2026.08.13.01.45.29.json`。 |

## 6. 验收边界

- 本切片只实现 3234 基础态与下一张成功 Attack 的费用豁免；不实现升级实例或升级 14 Block，也不加入默认 Deck、奖励、Run 或 UI 专属提示。
- 不修改多人、Scene、Prefab、ProjectSettings、asmdef、DI 或构建管线；不实现自动免费攻击链、保留/选择协议或其他剩余目录卡。
- 公共费用解算 seam 可以支持未来职业适配，但本切片未实现战士免攻策略，也未扩大既有通用 X 运行时。
- 本切片未创建提交，也不记录或杜撰提交 SHA。
