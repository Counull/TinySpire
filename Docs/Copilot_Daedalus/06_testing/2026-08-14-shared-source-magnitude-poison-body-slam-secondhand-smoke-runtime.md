---
title: 共享来源动态伤害、Poison、Ironclad Body Slam 与机枪兵 Secondhand Smoke 运行时
page_type: testing
lifecycle: active
date: 2026-08-14
updated: 2026-08-14
status: verified-unity-native-2026-08-14
status_source: ../SESSION_LOG.md
source:
  - ../04_research/2026-08-06-sts2-v01071-ironclad-card-snapshot.md
  - ../00_inbox/README web.md
  - ../01_requirements/2026-08-13-sts2-v01071-ironclad-first-four-runtime-digest.md
  - ../01_requirements/2026-08-12-machine-gunner-v2-requirement-digest.md
related_plan:
  - ../plans/2026-08-06-sts2-v01071-ironclad-card-pool.md
  - ../plans/2026-08-12-machine-gunner-v2-card-runtime-plan.md
related_decision: ../CODE_DECISIONS.md#cd-106body-slam-与二手烟以来源快照动态值和通用-poison-生命周期接入
---

# 共享来源动态伤害、Poison、Ironclad Body Slam 与机枪兵 Secondhand Smoke 运行时

本页记录本切片从开发中运行时门禁到正式数据、Localization、真实 AssetBundle 与最终 Unity 原生验证的完整闭环。开发中任务保留为前置证据，最终权威结果是生成后定向 9/9、完整 EditMode 793/793，以及强枚举清理后的精确 1/1。

## 1. 当前结论

- `Body Slam`（3105）基础态把 Prepare 时的来源当前 Block 冻结为普通攻击 base magnitude，继续经过 Strength、目标 Vulnerable、目标 Block / HP 与致死公式，且不消费来源 Block。
- Body Slam 基础态与升级 metadata 的正式文本均为 EN `Deal {damage} damage, equal to your Block.`、ZH `造成 {damage} 点伤害，数值等同于你当前的格挡。`；`{damage}` 是 Localization validator 所需占位符，并由运行时动态显示来源 Block。升级实例仍未实现。
- `Secondhand Smoke / 二手烟`（3270）基础 Program 70 把 Prepare 时的来源当前 Smoke 作为目标 Poison 增量；不改变来源 Smoke。Smoke 为 0 时仍成功支付与弃牌，但没有 Poison 状态 settlement。
- Poison 是 `CombatantData` 的通用权威状态，而非机枪兵私有枚举。Apply / Tick 均遵守 Prepare / Validate / Commit 和一次性计划契约。
- 3105 / 3270 已正式翻为 `Implemented`；Luban、Localization Import / Validate、`Sync and Build All`、BuildLayout / `AssetBundleProvider`、生成后定向与完整 EditMode 均已通过。当前正式目录为全项目 92/76、Ironclad 12/73、Marine 79/3，Effect 16。

## 2. 来源动态值与两张基础卡

| 对象 | 当前基础态契约 | 明确排除 |
|---|---|---|
| `SourceBlock` magnitude | 冻结来源当前 Block；作为 `DealDamage` 基础值进入普通攻击公式 | 不是真实伤害；不忽略 Strength / Vulnerable / 目标 Block；不消费来源 Block |
| Body Slam 3105 | 已正式绑定 `damage:4016` 与强枚举 `DealDamageFromSourceBlock`；运行时使用来源 Block 动态伤害 | 基础态与升级 metadata 共用正式 `{damage}` 文本；升级实例仍未实现 |
| Secondhand Smoke 3270 | 0 Energy / Program 70 / 显式敌方目标 / Discard；冻结来源 Smoke 并施加同值 Poison | 基础态不读取目标 Smoke；不清空来源 Smoke；零值不伪造 settlement |

Secondhand 的升级文本仍描述“来源与目标 Smoke 总和”。这是 source / metadata 事实，不是当前基础实例的运行规则；没有升级 `CardInstance` 前不得把目标 Smoke 加入基础值。

## 3. 通用 Poison Apply / Tick 契约

- Apply 冻结 source、target、Poison before / after 和 settlement order；零增量合法但不写 `0→0`。
- Tick 在参与者自己的行动开始执行，绕过 Block；实际生命损失为 `min(PoisonBefore, HealthBefore)`，`PoisonAfter = max(0, PoisonBefore - 1)`。致死 tick 同样减一层，零 Poison 不写事实。
- 敌人非致死 tick 后继续旧 action / status / intent advance；致死则在行为与目标解析前产生 source-not-alive skip，不推进当前 intent，并保留 Encounter continuation。
- 玩家非致死 tick 后继续 Block / 职业状态 / 资源 / 抽牌；致死则跳过这些 reset，并把终局提交推迟到状态机调用栈退出，避免回调重入。

玩家 Poison plans 能按稳定玩家顺序联合准备，但 Poison 后整个 round reset 尚不是一个跨模块 joint plan；这是 P2，而非当前正常路径失败。未来若敌人开放 Regeneration，其治疗计划必须使用 tick 后投影 Health 准备，不能复用 tick 前快照。

## 4. 状态种类与表现

- 3277 / 3278 / 3279 共用当前活跃状态集合：Strength、Vulnerable、Poison 与 17 种 `MachineGunnerCombatantStatus`，最大 20 种。同一种状态不论层数只计一次。
- 3277 冻结来源状态；3278 / 3279 冻结目标状态。Poison 5 层仍只增加 1 种，另一参与者的 Poison 不串入计数。
- Poison tick 只派生 `HealthLossNumber`；致死再追加 `DeathTransition`。它不会派生攻击 hit-shake、Block absorbed 或攻击轨迹。
- 本切片没有 Prefab 授权，也未修改 Participant HUD。常驻 Poison 图标、层数文字与 pulse 均未实现；M9B 的 “Prefab 没有 Poison 节点” 继续是当前事实。

## 5. 冻结来源牌归宿

Program 70 没有其他卡区深操作，因此可在 Poison 首写前准备并校验普通 played-card departure，最后按冻结最终 Layout 一次提交。测试以同步 Poison observer 把另一张手牌移出 Hand 制造漂移，最终仍由冻结计划恢复预期布局并只让来源牌进入弃牌堆。

该 seam 不覆盖会主动改变卡区的程序：普通 Draw、DrawToHandLimit、ReplaceRemainingHand、选择后复合归宿等仍使用各自既有 Prepare / Validate / Commit 计划；它们完成后不会再被普通 departure 的旧布局覆盖。

## 6. 当前 Unity 行为证据

| 门禁 | 当前结果 | 结论边界 |
|---|---|---|
| Body Slam 公式 + 3277 Poison 计数 | 任务前缀 `419c…`，2/2 passed | 证明来源 Block 公式与来源 Poison 计数，不是正式表证据 |
| 原子归宿与回归修复 | 任务前缀 `b5f…`，8/8 passed | 覆盖 observer 漂移及相关既有卡区路径 |
| 行为聚合 | 任务前缀 `79a…`，289/289 passed | 不依赖正式生成后的 Catalog 状态 |
| 生成前完整 EditMode | 任务前缀 `fd6…`，共 791 项、7 failed | 7 项均是 3105 / 3270 尚为 `CatalogOnly` 的预期目录红灯；不能写为全量通过 |

此前各片精确 Poison apply / tick、玩家 / 敌人非致死与致死、表现与状态计数任务仍保留在测试运行历史中；本页不杜撰未提供的完整任务 ID。

## 7. 正式发布与最终 Unity 证据

### 7.1 作者工作簿与 Luban 生成物

| 文件 | Size | SHA-256 |
|---|---:|---|
| `DataTables/Datas/__enums__.xlsx` | 10982 | `48aa59ec32cba63429678f34d2f88d8010d0ba2842865e021d3578b93ce2ef5e` |
| `DataTables/Datas/battle.card_effect.xlsx` | 4603 | `cac78b6069764a037275b3261125e379de9a8f75a358f34c9d430ac98dff6d14` |
| `DataTables/Datas/battle.card.xlsx` | 23197 | `01c1613de65ee7e9b6fb49a774fecb4e31c53535c2186cb0b5e9bbac03358be0` |
| `DataTables/Datas/i18n.xlsx` | 29057 | `0bb37d8ba79bff9c3d8853b95af7c436373893385c0c62055e5400be2fbd8d0b` |

Luban 成功。生成物 `TinySpire/Assets/Scripts/Core/Generated/Config/battle/EffectType.cs` 为 1083 bytes、SHA-256 `d901715fe9802566b137215d9b8d655d68c3bef60bd41c972885dce4c846b9b9`；`TinySpire/Assets/GameData/battle_tbcard.json` 为 123848 bytes、SHA-256 `5b84dcf7a0d1757fae7a901b5e8bab990b702c0a69cb1a9df8897811241984aa`；`TinySpire/Assets/GameData/battle_tbcardeffect.json` 为 1541 bytes、SHA-256 `7b435ce2cde44571c988f93dc1ef6c00668d2220519c52749062734eb919cabb`。

正式计数为全项目卡牌 **168 = 92 Implemented / 76 CatalogOnly**、Ironclad **12/73**、Marine **79/3**（V1 **61/3**、V2 **18/0**）、Effect **16**。

### 7.2 Localization 与真实 AssetBundle

- Localization Import 与 Validate 日志均成功。`TinySpire/Assets/Localization/Battle Cards_en.asset` 为 93547 bytes、SHA-256 `2d717822eb9a32ef6374908d4c32c3508233b5f74e23d6df0c67638af5d2e32a`；`TinySpire/Assets/Localization/Battle Cards_zh-CN.asset` 为 111579 bytes、SHA-256 `204bac90744eff72501a8f7b8b70de8fbcf0712ba3dcf2700a61cd8320c74b90`；二者 mtime 均为 2026-08-14 01:42:26。
- `Sync and Build All` 成功，Addressables 子构建耗时 50.667 秒。
- `TinySpire/Library/com.unity.addressables/buildlayout.json` 为 134621 bytes、SHA-256 `74a51e87ebc1e938caca6eacd7e0f6cd8a7ccbd8f23ff4c4217f670ef79aff3`、mtime 2026-08-14 01:43:17；对应 settings artifact 为 864 bytes，mtime 同为 2026-08-14 01:43:17。
- 物理 bundle `tinyspiregamedata_assets_all_2779cc5206157ad3345f769bdba15759.bundle` 为 12201 bytes、SHA-256 `5711d9ce71d7da896535340d2c843ff6faffcb12b38384f12375336814f33eea`、mtime 2026-08-14 01:43:14。BuildLayout 同时包含 Card / Effect 两份目标 JSON，Provider 为 `AssetBundleProvider`。

### 7.3 生成后最终门禁

| 门禁 | 最终结果 | 结论 |
|---|---|---|
| 定向目录与行为 | `88e36d2a5cbb47b7b4a67207dad00856`，9/9 passed，1.1633036 秒 | 正式生成数据下的切片定向权威结果 |
| 完整 EditMode | `9ca3d43a79d24b25a917fad7b6166584`，793/793 passed，20.0509052 秒 | 最终全量权威结果 |
| 强枚举清理后精确回归 | `40af8c25ba4442ffbe9e98451890f01c`，1/1 passed | 生产、Queue 测试与 I3 已移除裸数值 7 后的精确验证 |

最终 assets barrier 为 `refresh_triggered=true`、`compile=false`、idle；强枚举清理后的静态 Editor build 为 0 error、12 条既有 warning。Console 再清空后为 0 error，tests 为 idle / null，且只有一个 Editor。前述 `419c…`、`b5f…`、`79a…` 与生成前 `fd6…` 仍保留为开发过程证据，但最终状态以本节两个正式生成后任务及清理后 1/1 为准。

## 8. 范围与停止点

- 不实现升级实例、Body Slam 升级差异或 Secondhand 升级的目标 Smoke 读取。
- 不修改默认 Deck、奖励、Run、多人、Scene、Prefab 或专属 Poison HUD / 资产。
- 不把玩家整轮 reset P2 或未来敌人 Regeneration 投影当作已完成事务。
- 未 commit、未 push；本页状态已收口为 `verified-unity-native-2026-08-14`。
