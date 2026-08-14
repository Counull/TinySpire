---
title: 共享 Heal、Ironclad Not Yet 与机枪兵战地手术运行时
page_type: testing
lifecycle: active
date: 2026-08-13
updated: 2026-08-13
status: verified-unity-native-2026-08-13
status_source: ../SESSION_LOG.md
source:
  - ../04_research/2026-08-06-sts2-v01071-ironclad-card-snapshot.md
  - ../00_inbox/README web.md
  - ../01_requirements/2026-08-13-sts2-v01071-ironclad-first-four-runtime-digest.md
  - ../01_requirements/2026-08-12-machine-gunner-v2-requirement-digest.md
related_plan:
  - ../plans/2026-08-06-sts2-v01071-ironclad-card-pool.md
  - ../plans/2026-08-12-machine-gunner-v2-card-runtime-plan.md
related_decision: ../CODE_DECISIONS.md#cd-104not-yet-与战地手术通过共享-heal-结果和行动结束再生适配接入
---

# 共享 Heal、Ironclad Not Yet 与机枪兵战地手术运行时

本页记录通用封顶治疗、`Not Yet`（3171）基础态、`Field Surgery / 战地手术`（3231）基础态与 Regeneration 行动结束生命周期，并保存作者表、Luban、本地化、真实 AssetBundle 和 Unity 原生门禁证据。

## 1. 验收结论

- 公共 `EffectType.Heal = 6` 已进入普通 Effect 执行链。治疗在首次写入前冻结请求量、目标生命上限、前后生命与实际恢复量；实际恢复不得超过剩余生命空间，也不通过直接相加制造整数溢出。
- `Not Yet` 基础态为 2 Energy、Rare Skill、Self、Hand→ExhaustPile，通过 `heal:4014` 请求恢复 10 点生命。治疗后来源牌进入 ExhaustPile；满生命仍支付、产生实际值为 0 的治疗记录并消耗来源牌。
- `Field Surgery` 基础态为 1 Energy、Rare Skill、Self、Hand→ExhaustPile，Program 31 在出牌时只获得 Regeneration 5 与 Shackle 1，不立即治疗。玩家行动结束按 `Shackle 清零 → LoseStrength 清零 → Heal → Regeneration -1 → Bomb → Burn` 顺序结算。
- Regeneration 追加为第 17 个 `MachineGunnerCombatantStatus`，没有改变既有枚举值。它继续参与 3277 / 3278 的“活跃状态种类”规则；Shackle 成功路径仍被既有 Attack 门禁拒绝，其余 16 种私有状态可按各自正层数计一种。
- Ironclad 85 张现为 **10 `Implemented` / 75 `CatalogOnly`**；Marine 82 张为 **77/5**，其中 V1 为 **61/3**、V2 为 **16/2**。全项目 168 张为 **88/80**，Effect 共 14 项。

## 2. 共享 Heal 契约与复用边界

| 层 | 当前契约 |
|---|---|
| 纯计算 | `BattleHealthRestorationOutcomeResolver` 根据 requested/current/max 冻结 `RequestedAmount`、`HealthBefore`、`HealthAfter` 与实际 `Amount`。 |
| 普通 Effect | `BattleEffectExecutor` 把 `EffectType.Heal` 预演成 prepared health restoration，并保留配置 Effect ID。 |
| 唯一生命写入口 | `BattleCombatantEffectOperations.ApplyPreparedHealthRestoration` 校验旧生命快照后调用 `CombatantData.ApplyHealthRestorationOutcome`；普通 Effect 与职业 Regeneration 共用该入口。 |
| 结算事实 | `BattleHealthRestoredSettlement` 保存请求量、前后生命和实际量；Field Surgery 的周期治疗没有伪造配置 Effect ID。 |
| 表现 | 只有 `Amount > 0` 才派生 `HealthRestoredNumber`，显示实际 `+N`；零治疗仍保留领域记录，但不显示 `+0`。 |

共享范围仅包含治疗 outcome、prepared 校验、生命写入口、settlement 与表现适配。Field Surgery 的 Regeneration / Shackle 生命周期继续属于机枪兵职业适配器；普通 Heal 不读取卡牌 ID、名称、职业状态或回合阶段。

## 3. 两张卡的权威结算

| 场景 | 结算顺序与结果 | 验收 |
|---|---|---|
| Not Yet，缺 7 HP | `EnergySpent(2) → HealthRestored(requested 10, actual 7) → source HandToExhaust` | 通过 |
| Not Yet，满生命 | `EnergySpent(2) → HealthRestored(requested 10, actual 0) → source HandToExhaust`，无 `+0` 表现 | 通过 |
| Heal 后存在缺失 Effect | 整体在首次写入前失败；Energy、Health、卡区、随机流、Turn、settlement 与发布次数均不变 | 通过 |
| Field Surgery 出牌 | `EnergySpent(1) → Regeneration 0→5 → Shackle 0→1 → source HandToExhaust`，无立即 Heal | 通过 |
| Field Surgery 行动结束，68/70 HP | `Shackle 1→0 → LoseStrength 2→0 → Heal requested 5 / actual 2 → Regeneration 5→4 → PhaseChanged` | 通过 |
| Field Surgery 行动结束，满生命 | 仍记录 `Heal requested 5 / actual 0`，随后 Regeneration 5→4 | 通过 |
| Field Surgery 的 Shackle 溢出 | 在 Regeneration、Energy、Health、卡区、随机流与 settlement 首写前返回 `EffectValueOverflow` | 通过 |

来源 `00_inbox/README web.md` 冻结的完整玩家回合末顺序为：弃牌与既有临时状态清理后，先清 Shackle / LoseStrength，再执行 Regeneration 治疗并减 1 层，然后处理 Bomb，最后处理会伤害玩家的 Burn。顺序红灯证明旧实现曾把 Heal 放在 Shackle / LoseStrength 之前；修复后没有把该偏差登记成新玩法决定。

## 4. TDD 红绿证据

| 切片 | 红灯 / 初始证据 | 绿灯 |
|---|---|---|
| Not Yet 封顶治疗 | `4b1ef61209f749fe87138f6e9a767175`，1/1 failed，暴露 Heal 尚未受支持 | `94d085dec3c74d2287831846b0baddba`，1/1 passed |
| Not Yet 满生命 | — | `1615e33b3d8c4a159a258f2baebaff43`，1/1 passed |
| Heal 后缺失 Effect 原子失败 | — | `00240b3902af4ee395c7da2fad1cf6b4`，1/1 passed |
| 正实际治疗表现计划 | — | `0726442131f944748c7d92768a23726a`，1/1 passed |
| 治疗飘字 factory | — | `649b881e43ac4601bd2ae04a51f3a959`，1/1 passed |
| Field Surgery 出牌 | `32984ebfa0e14118a9f16f5ea3606c0a`，1/1 failed，暴露 Program 31 尚未受支持 | `5084062eb6b041e8a64512cfcde701dc`，1/1 passed |
| Field Surgery 封顶 / 满血 / 溢出 | — | `759b0511f9cb46a6abeb74f8fcc0a856`、`0c1166819dab48af8dd5a917aa72535e`、`e71e489206fe4fd4bf5026baea6b6bec`，各 1/1 passed |
| 来源回合末顺序 | `c8d4aa4ddf7347dc8d6515f297c9ed90`，1/1 failed，证明 Heal 早于 Shackle / LoseStrength | `ed358fb765e74283848a28d40c9ae3ce`，1/1 passed |
| 治疗飘字视图 | — | `4d5e4253e93840bd849571512f5f0a43`，1/1 passed；锁定 `+7` 与 RGBA `(105,235,185,255)` |

来源顺序修正后的最终精确行为任务 `b511f5ddcd2041a9b264c0f982c4b600` 为 **9/9 passed**（0.3818676 秒），覆盖两张卡、零实际治疗、失败零写入、`FieldSurgery_EndPlayerAction_CleansTemporaryStatusesBeforeCappedHealAndRegenerationDecay` 和治疗表现。

## 5. 正式数据与生成证据

| 项目 | 正式结果 |
|---|---|
| `DataTables/Datas/__enums__.xlsx` | SHA-256 `dc35fc55df7a4223347f81054c09df88ddea3b6eb88da36de41499562dd7618e`；`Heal = 6`。 |
| `DataTables/Datas/battle.card_effect.xlsx` | SHA-256 `34eef4012c2b858e43fb0f7cb7c2417e1a3caa34d5afa3dcb46dfbd61c465af0`；Effect 4014=`Heal / None / 10`。 |
| `DataTables/Datas/battle.card.xlsx` | SHA-256 `7c57c0a024d445d990ee275e7474a5460f7055504b1169f0b74dfd525d3665f3`；3171 与 3231 精确翻为 `Implemented`。 |
| `DataTables/Datas/i18n.xlsx` | SHA-256 `bd37b5660cbd5b1ceff8c07a58410c4f49e124acbdc3b97d893d4754b8551f5e`；Not Yet 使用 `{heal}`，Field Surgery 保留 5/6 Regeneration 文本。 |
| Luban | 2026-08-13 19:31:59 成功。 |
| `TinySpire/Assets/GameData/battle_tbcard.json` | SHA-256 `A47F249F2007ED80707354C263B3154313C96DD3C41FF58F743FB9494A7A1752`；168 张为 88/80。 |
| `TinySpire/Assets/GameData/battle_tbcardeffect.json` | SHA-256 `32036F53048206871D39C22A56CCDF74B3FA01976078AA49047E79DA4308986B`；14 项。 |

Localization Import / Validate 均成功。`TinySpire/Build/Sync and Build All` 完成，本地 Addressables 子构建耗时 11.968 秒；最新报告为 `TinySpire/Library/com.unity.addressables/BuildReports/buildlayout_2026.08.13.19.37.10.json`，GameData 物理包为 `TinySpire/Library/com.unity.addressables/aa/Windows/StandaloneWindows64/tinyspiregamedata_assets_all_77a1973868c636fe147c61465e862169.bundle`。

## 6. 静态、目录、真实 AB 与完整 Unity 门禁

| 门禁 | 任务 / 结果 |
|---|---|
| Runtime 静态编译 | 0 error / 6 warning |
| Editor 静态编译 | 0 error / 12 warning |
| 正式目录与数据 | `c3e5c7dbcb534cd18a85b635761fb8d7`，**50/50 passed** |
| 治疗视图精确补强 | `4d5e4253e93840bd849571512f5f0a43`，**1/1 passed**；`+7` 与 RGBA `(105,235,185,255)` |
| 相关聚合、治疗视图与真实 AB | `818f8283386b4d86aa625c6d95284245`，**243/243 passed**（10.3679275 秒）；从最新物理 bundle 真实加载 |
| 完整 EditMode | `c6a86ba528804a13b1c84fe38c28b48b`，**766/766 passed**（18.1031754 秒） |

所有正式任务均为 0 failed / 0 skipped。最终聚合同时包含治疗飘字视图与 Addressables 真实 AssetBundle 加载，不以 Fast Mode 或静态 JSON 检查替代物理包证据。

## 7. 验收边界

- Not Yet 升级恢复 13、Field Surgery 升级 Regeneration 6 仍只是作者表与本地化元数据；没有升级 `CardInstance` 或升级数值切换。
- Field Surgery 的多人 AnyAlly、对队友恢复以及双方各加 Shackle 均未实现；当前只验证单玩家 Self 基础态。
- 默认 Deck、奖励、Run、多人 Session、Scene / Prefab 和其余 75 张 Ironclad、5 张 Marine `CatalogOnly` 不在本切片范围。
- Not Yet 只开放 I12 的“治疗不超过战斗生命上限”子能力；失血、Fatal、永久 Max HP 与失血历史仍未因此完成。
