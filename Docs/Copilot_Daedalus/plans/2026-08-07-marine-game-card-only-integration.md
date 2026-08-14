---
title: Marine Game 机枪兵卡牌单场战斗接入
page_type: plan
lifecycle: superseded
created: 2026-08-07
updated: 2026-08-12
scope: 机枪兵卡牌目录及单场战斗内的卡牌执行规则；不含地图、敌人、奖励、篝火、Run 或场景流程
status: superseded-by-v2-2026-08-12
status_source: ../SESSION_LOG.md
source:
  - ../00_inbox/marine-game.zip
  - ../01_requirements/2026-08-07-marine-game-card-requirement-digest.md
depends_on:
  - 2026-08-06-machine-gunner-card-pool-integration.md (MG1 Hero 资源档案已完成)
supersedes: 2026-08-06-machine-gunner-card-pool-integration.md
---

# Marine Game 机枪兵卡牌单场战斗接入

> 已由 [2026-08-12 Marine Game 机枪兵 V2 卡牌单场战斗接入](2026-08-12-machine-gunner-v2-card-runtime-plan.md) 取代。本页只保留旧 64 模板来源链和历史切片，避免把 V2 README 的规则混入历史验收。

## 1. 目标与硬边界

目标是在保持 `BattleCommandQueue.Submit` 为唯一共享写入入口、默认战士与 M10 基线不变的前提下，按可验证切片把新版机枪兵卡牌接入单场战斗。

- 只处理 5 个 starter 模板、58 张奖励模板及 1 张临时模板，及其真正需要的单场规则。
- 不导入压缩包中的 `map`、`enemies`、`patterns`、奖励权重/三选一、篝火、事件、跨战斗状态或 Run。
- 不修改 Scene、Prefab、ProjectSettings、asmdef、HybridCLR、启动/DI 流程，亦不触碰受保护 Targeting/Candidates/Hermes 美术路径。
- 不把 `cards.json` 直接作为运行时脚本，不按卡牌 ID、卡名、外部 key 或文本写规则分支。
- 不在所有 starter 都真实 `Implemented` 前新增 Hero、Deck 或角色选择；CatalogOnly 卡不得引用进 Deck。

## 2. 当前来源裁决与风险

日常实施先读关联需求摘要。它把 `cards.json` 作为目录数值源，把 `battle.html` 作为后续行为参考，把 README 作为补充语义。当前可安全执行的目录切片不需要决定易伤遗漏、地图冲突、未结构化状态或复杂选择；这些问题只能在对应运行时切片冻结，不能用表格占位伪造已完成机制。

回滚单位是单个切片明确审阅的作者表、生成文件、本地化资产、测试与文档；绝不清理既有 MG1 或其他工作树改动。

## 3. 串行切片

| 切片 | 独立交付物 | 精确验收 | 明确不做 |
|---|---|---|---|
| MG2A（本轮） | 64 张 `CatalogOnly` 目录冻结 | 作者表、Luban、同步构建、本地化/Addressables、目录门禁、快照/相关 EditMode 回归全部通过 | Hero/Deck、程序、效果、状态、最终插画 |
| MG2B（初始牌，已完成） | 深 Card Program 的预构建、投影和一次性提交 | 射击、肘击、防御、装填、兴奋剂均经 Queue 成功结算；弹药不足保持 Energy/Ammo/卡区/参与者零写入 | 不自动把其余 59 张目录卡翻为可玩 |
| MG3（已完成） | 稳定步骤选择器与卡牌随机流 | 最近/第二近/全体/随机/重复命中在固定种子下可复现；死亡跳过不污染随机流 | 不中途向 UI 索取选择 |
| MG4（已完成） | 状态与攻击伤害链 | Weakness、Smoke、Vulnerable、Block、HP 的顺序和取整；Burn/Oil/Armor 的时机有回归 | 不把 Weakness 映射为 Vulnerable |
| MG5 | 抽牌、X 费、资源修饰、延迟和结束行动 | 手上限、Stim、X 值冻结、延迟资源和终止行动均由一次卡牌事务记录 | 不做奖励、地图或跨局升级 |
| MG6 | Power/Modifier 的唯一归属与触发 | 归属、叠层、触发顺序、清理和 settlement 稳定 | 不建第二命令队列或全局事件总线 |
| MG7 | 逐批翻转 starter 与后续卡牌 | 每张 Implemented 卡有程序、配置、文本、素材状态和真实 BattleScene 证据；五张 starter 完成后才接 Deck | 不宣称奖励流程或 Run 完成 |

## 4. MG2A 设计

静态 ID 预留 `3201`--`3264`：starter 5、reward 58、temporary 1。每行使用 `MARINE_*` 外部 key 和 `marine-game-v1-20260807-cards` 快照键；类型、稀有度、固定/X 费、升级费用、基础输入目标与归宿来自 `cards.json`。Power 的目录归宿记为 `Power`，`exhaust: true` 记为 `ExhaustPile`，其余记为 `DiscardPile`。复杂目标仅以当前已有枚举做目录级降级映射，详情见需求摘要第 5 节。

`effect_bindings` 保持空、`implementation_status` 必须是 `CatalogOnly`、`illustration_key` 必须是现有 `art_placeholder`。每张表项都有 en/zh-CN 名称、基础说明和升级说明；中文基础/升级说明分别保留 `cards.json.desc` 与 `known_upgrades.change`，而不是用“未实现”通用文案覆盖规则。源文件没有英文规则原文，en 列以压缩包的结构化字段与行为说明生成项目内英文翻译；中文来源仍是规则追溯依据，英文文案不表示行为已由运行时实现或完成最终策划校对。

## 5. 验收与同步

MG2A 完成前必须：

1. 对作者表进行内容、格式和视觉检查；确保表行、ID、外部 key、本地化 key 和升级声明无重复或遗漏。
2. 运行 Luban，并检查生成 `battle_tbcard.json` 中快照身份、计数、CatalogOnly、空程序和占位图。
3. 运行 `TinySpire/Build/Sync and Build All`，验证本地化导入、卡牌目录/Deck 门禁和 Addressables 本地内容构建。
4. 运行新增的 snapshot/构建门禁测试及相关现有卡牌、配置与本地化回归。
5. 同步 `SESSION_LOG.md`、`CODE_DECISIONS.md`、本计划、需求摘要和 `06_testing/`；未通过前不推进 MG2B。

截至 2026-08-07，MG2A 的步骤 1--5 已完成。后续 MG2B 只翻转首批 5 个初始牌：Hero 1002 的 `MachineGunner` 会话私有运行时通过同一 Queue 完成射击、肘击、防御、装填与兴奋剂，默认 Hero 1001 保持 Legacy。`TinySpire/Build/Sync and Build All` 成功完成，生成字段兼容的定向回归为 58/58，完整 EditMode 为 500/500。

MG3 已在同一会话私有模块内完成基础选择器与随机事务：最近、最远、全体、显式、自身和随机目标都只从存活敌人的 Encounter 顺序派生；第二近由同一只读快照按索引取得。随机解析先使用本地 `GameRandom` 副本，只有整张卡成功移动离开手牌后才回写职业随机状态；伪造目标、无存活敌人及后续失败均不消耗随机流。定向 EditMode 任务 `c9c735c3070342d6879a1d4d1d01b462` 为 9/9 passed。

MG4 已完成职业私有状态和攻击公式：Weakness、双向 Smoke、Vulnerable、Block、HP、Burn/Oil 与 Armor 均由同一 Hero 1002 会话运行时处理；敌方 Effect 也通过同一公式覆盖，并在破防后才消耗 Armor。卡区同时预留 `PowerPile` 和 10 张职业手牌上限，但没有把目录能力牌翻为生产可玩。定向 EditMode 任务 `d283762aa2ea454ab4638a8ff6165cde` 为 33/33 passed。MG5 才处理 X 费、资源修饰、延迟和结束行动；59 张目录卡、复杂 Power、自动连锁出牌与 PendingResolution 选择协议仍须串行验收。

## 6. MG5 首批：X 费、变动弹药与多段程序（已完成）

- 数据表仅开启 3214、3220、3224--3227、3230、3232、3233、3256、3258；构建门禁按 `MARINE_*` 外部 key 精确锁定 16 张已实现卡，拒绝连续 ID 假设。
- 职业私有运行时在同一张卡的首次写入前冻结 X、Energy/Ammo 支付和 Stim 附加命中。`RandomLivingEnemy` 每段从投影存活目标中选择，并以候选随机状态预演；全卡成功移出手牌后才提交该状态。
- 已运行 Luban、单一 Unity Editor 的 `Sync and Build All`，以及 EditMode `e7a502caaa4c4d738cb9a9a96ae6c6d7` **15/15 passed**。完整证据见 `../06_testing/2026-08-07-machine-gunner-mg5-x-multishot-runtime.md`。

## 7. MG5 第二批即时状态（已完成，历史快照）

- MG5 第二批已开启 `StunGrenade` (3215)、`SmokeBomb` (3221)、`KidneyShot` (3228)、`PainfulElbow` (3229) 和 `SniperShot` (3247)。程序以伤害/状态投影顺序预构建，死亡目标不再接收后置状态；私有 Weakness/Smoke 不伪造 Effect ID，Vulnerable 复用现有通用状态和展示脉冲。机枪兵快照现为 21 张 `Implemented` / 43 张 `CatalogOnly`。
- 本切片已运行 Luban、单一 Unity Editor 的 `Sync and Build All`，并由同次 Test Runner 结果文件记录定向 2/2 passed。Unity MCP 测试任务的初始化回调未及时返回，故该连接器状态不能替代原生 Test Runner 结果；完整记录见 `../06_testing/2026-08-07-machine-gunner-mg5-immediate-status-runtime.md`。

## 8. MG5 验收时的后续风险（历史）

- 下一步优先实现 `SpikeShot` 所需的逐段 `OnShotHit` 形态：每段伤害后立即施加状态，使 Stim 的后续命中能读取前段新增的 Vulnerable；不得将它降级成整张卡结束后只上一次状态。
- `GasPump` 与 Burn 相关卡等待“玩家行动结束、敌人行动前”的燃烧结算和伤害可否被 Block 阻挡的口径冻结；`IncompleteCombustion` 另需 Exhaust、燃烧者×实时存活目标的交叉结算和 Burn→Smoke 转换。
- 延迟伤害、下回合资源/装填/格挡、结束行动、手牌选择、动态临时卡、自动连锁、Power 持续触发和全息诱饵受击实体保持独立切片。
- `Overload` / `LimitOverload` 是否允许临时超过 Energy 上限会影响共享 `PlayerTurnData` 语义；在取得专门确认前不得借职业程序静默绕过现有上限。

## 9. MG6：已有 Power 程序的精确配置门禁（已完成）

- 目录与源代码审计确认 `CoreExpansion`、`OutputAdjust`、`BlastShield`、`MagExpansion`、`SmokePersist` 与 `PowerOverclock` 已具备完整的注册、提交、PowerPile 归宿和回合接线。作者表只翻转这六张的 `implementation_status`，构建门禁与快照测试同步更新为精确的 27 张可执行集合。
- 工作簿差异、重新导入、渲染、Luban 和单一 Unity Editor 的 `Sync and Build All` 全部通过；Unity MCP 定向 EditMode 3/3 通过，覆盖 Power 真实行为与生成目录。未增加奖励、Run、Power UI、升级实例或第二写入口。

## 10. MG7：Burn/Oil 生命周期与首批依赖卡（已完成）

- `MachineGunnerBattleRuntime.ResolvePlayerRoundEnd` 只由最后一名存活玩家的 `EndPlayerAction` 在既有 Queue 命令内调用一次：先按 Encounter 顺序结算存活敌人，再结算机枪兵玩家。Burn 走既有 `Debuff` 管线，读取 Burn 本身，不受 Weakness/Smoke/Vulnerable/Armor 影响，但可被 Block 吸收且不衰减。
- 若敌方 Burn 消灭最后一名敌人，运行时立即停止，不再施加玩家自燃，派生 Victory；若玩家自燃死亡，则同一命令内派生 Defeat，均不会继续推进敌方阶段。没有额外 Burn 命令、第二队列或全局事件总线。
- `ApplyBurn` 以同一纯计算函数同时冻结 Burn 与 Oil：`Burn += baseBurn + oldOil`，`Oil = floor(oldOil / 2)`。Napalm 的操作顺序固定为先 Burn（只消费旧 Oil）再增加本次 Oil，防止新 Oil 在同次施加中自触发。
- 作者表精确开放四张基础值可执行卡：`GasPump` (3217，所有存活敌人 Oil +5)、`Napalm` (3218，所有存活敌人 Burn 3 后 Oil +5)、`Molotov` (3219，显式敌人 Burn +5)、`FlameElbow` (3255，最近敌人攻击 6 后仅对仍存活目标 Burn +3)。快照现为 **31 张 `Implemented` / 33 张 `CatalogOnly`**。未假设 CardInstance 升级态，升级数值仍需要通用升级实例切片。
- 作者工作簿经单元格值差异、重新导入、渲染与 SHA-256 部署复核；Luban、单一 Unity Editor 的 `Sync and Build All` 和 Unity MCP EditMode `5db8f11868324b7788a2ef822c9b0ec9`（37/37 passed）均通过。完整证据见 `../06_testing/2026-08-07-machine-gunner-mg7-burn-oil-runtime.md`。

## 11. MG8：功夫机甲、开火与连肘（已完成）

- `KungfuMech` (3212) 复用 `PowerPile` 与 `_powerStacks`，每层在每张成功非射击攻击完成后只给一次 4 Block；`ElectroBoost` (3236) 以私有 `FirePower +2` 表达，可叠加、在玩家行动结束清零，并只在常规射击段进入 Weakness 前加伤；`ComboElbow` (3242) 为最近目标 10 点攻击，当前玩家回合紧邻前一张成功牌是非射击攻击时本张固定费用冻结为 0。
- `TryPreviewCost` 是规则层与队首提交共享的只读预览，防止 0 Energy 的免费连肘被通用静态成本拒绝。卡区归宿成功后才更新“最近成功卡”分类，失败卡不污染、能力/技能/射击会断链、连肘自身可续链，玩家新回合必清空。
- 作者表精确翻转 3212、3236、3242；快照为 **34 张 `Implemented` / 30 张 `CatalogOnly`**。工作簿值差异、重导入/渲染、Luban、单一 Editor 的 `Sync and Build All` 与 Unity MCP 定向 EditMode 51/51 均通过。`battle.html` 实际狙击分支不读取 `firePower`，故本切片将其排除并以回归测试锁定；升级值仍等待通用 CardInstance 升级态。

## 12. MG8 完成时的下一切片（历史快照）

- 下一优先级是 `IncendiaryAmmo`、`AgedOil` 与 `SpikeShot` 所需的逐段命中后交错预演/提交：每段要按“伤害 → 卡牌命中后状态 → 全局命中钩子”更新投影与 settlement，不能退化成整张卡结束后只上一次状态。`FlameElbow`、`KidneyShot` 与 `PainfulElbow` 也要随该基础重排其命中后操作，防止陈年机油改变燃烧的旧 Oil 读取顺序。
- `BurningOil` 的回合末增长不可复用会消耗 Oil 的 `ApplyBurn`，`IncompleteCombustion` 还要求 Exhaust 和动态交叉结算；`TwelveHits`、临时卡、延迟伤害、下回合资源/装填/格挡、结束行动、手牌选择、自动连锁、Power 持续触发和全息诱饵受攻击实体保持独立切片。

## 13. MG9：逐段命中后置状态（已完成）

- `MachineGunnerCardProgram.PostHitOperations` 将攻击型程序的命中后状态限制为当前命中目标的私有状态、Burn 或 Vulnerable。每一段实际伤害在本地投影中按“伤害 → 程序命中后状态 → 全局命中钩子”预演，随后仍作为单个 `ExecutePlayerCard` 队首事务提交；伤害后死亡不留状态，存活的零伤害/全格挡命中仍会触发。
- `SpikeShot` (3248) 每段为 `Damage 1 → Weakness +1 → Vulnerable +1 → Incendiary Burn`，Stim 追加段完整重复且第一段 Vulnerable 影响第二段；`IncendiaryAmmo` (3210) 叠层并作用于所有射击命中（含狙击）；`AgedOil` (3253) 仅对非射击攻击每段固定 `Oil +2`，多张只启用不放大。`FlameElbow`、`KidneyShot`、`PainfulElbow`、`SniperShot` 同步改为这一顺序，避免旧 Oil 读取漂移。
- 作者表精确翻转 3210、3248、3253，快照为 **37 张 `Implemented` / 27 张 `CatalogOnly`**。工作簿值差异、重导入/渲染、Luban、单一 Editor 的同步菜单调用和 Unity MCP 定向 EditMode **57/57 passed** 均留有记录；升级实例仍未硬编码。

## 14. MG10A：烈火烹油回合末增长（已完成）

- `BurningOil` (3254) 进入既有 `PowerPile` 后只启用回合末规则。`ResolvePlayerRoundEnd` 在原有 Burn 伤害前一次性取得存活敌人的 Encounter 顺序，若能力层数大于零，则只对已有 Burn 的敌人写入 `Burn += 1 + Oil`；Oil 不减半、不消耗，玩家自身也不获得增长。
- 所有增长均以现有私有状态 settlement 写入，并在任意 Burn Debuff 伤害前完成。之后仍走已有 Block、死亡与 Victory/Defeat 中断：增长后 Burn 杀死最后敌人时继续跳过玩家自燃。多张副本保留 Power 层数但只表示启用，不把增长固定值倍增。
- 作者表精确翻转 3254，快照为 **38 张 `Implemented` / 26 张 `CatalogOnly`**。工作簿值差异、重导入/渲染、Luban、单一 Editor 的 `Sync and Build All` 和 Unity MCP 定向 EditMode **60/60 passed** 均已取证；完整记录见 `../06_testing/2026-08-07-machine-gunner-mg10a-burning-oil-runtime.md`。

## 15. MG10B：不充分爆燃（已完成）

- `IncompleteCombustion` (3222) 以专用职业 Program 操作实现：先冻结“开始时存活且 Burn > 0”的敌人来源，再让每个来源按 Encounter 顺序对其出手时仍存活的敌人逐个造成等于冻结 Burn 的 Debuff 伤害。来源若被前一来源杀死，仍保留其已冻结的一轮伤害；死亡目标则不再被命中。
- 只有全部伤害完成后，才按 Encounter 顺序对仍存活敌人写入 `Smoke += Burn`、`Burn = 0`。这两类状态使用直接 `Set`，不调用普通 `ApplyBurn`，故 Oil 完全不变；伤害 settlement 归属燃烧敌人，状态 settlement 归属玩家。3222 支付 3 Energy、无显式目标输入并从 Hand 移至 ExhaustPile，表内 `Self` 目标规则不被误当作运行时伤害目标。
- 作者表仅翻转 3222，生成快照为 **39 张 `Implemented` / 25 张 `CatalogOnly`**；工作簿差异/重导入/渲染/公式扫描、Luban 与静态编译均已取证。四项新增 EditMode 运行时用例已覆盖完整交叉结算、死亡来源快照、无燃烧空操作和全敌死亡后的 Exhaust→BattleEnded 顺序，生成 JSON 快照另锁定其费用、升级元数据、Self、ExhaustPile、状态和 Program。2026-08-11 已在唯一已连接的既有 Unity Editor 执行 `Sync and Build All`；菜单调用期间 MCP 传输断连，但重连后的 Console 明确记录本地 Addressables 内容构建成功（25.627 秒）且无 Error。定向 Unity MCP EditMode `94b4d610258b4b05a896adfd20ca6428` 为 **65/65 passed，0 failed，0 skipped**。完整记录见 `../06_testing/2026-08-08-machine-gunner-mg10b-incomplete-combustion-runtime.md`。

## 16. MG10B 验收完成后的边界（历史快照）

- 2026-08-11 已完成单一既有 Unity Editor 的 `TinySpire/Build/Sync and Build All`，并运行包含 `MachineGunnerStarterRuntimeTests`、伤害管线、卡牌规则、目录快照与构建门禁的定向 EditMode（65/65 passed）。后续每张 CatalogOnly 卡仍须先完成独立机制切片、再生成配置、同步构建与定向回归，不得以目录状态代替运行时验收。
- `TwelveHits`、临时卡、延迟伤害、下回合资源/装填/格挡、结束行动、手牌选择、自动连锁、Power 持续触发和全息诱饵受攻击实体继续保持独立切片；不因此扩展奖励/Run、Scene、Prefab、默认 Hero/Deck 或第二条写入链。

## 17. MG11：爆炸肘（已完成）

- `ExplosiveElbow` (3252) 自动选择最近存活敌人，支付 2 Energy 后先进行基础 10 点 Attack；普通攻击后目标仍存活时，程序在既有逐段投影内依次完成卡牌后置状态、`IncendiaryAmmo`、`AgedOil`，最后读取该时刻的 Burn 追加一次等值 Debuff。该追加段不消耗或改写 Burn/Oil，只读 Debuff 的 Block/HP 路径，不读取 Weakness、Smoke、Vulnerable，且不消耗 Armor。
- 当前 `marine-game` 来源链（`cards.json`、`battle.html`、README）支持 2 Energy / 基础 10 / 立即触发 Burn；`00_inbox/HANDOFF.md` 的 STS2 Mod 1 Energy / 8 历史说明不在本计划所依赖的摘要来源链内，故未改变作者表数值。表内 `Enemy` 保留目录输入分类，自动最近目标只在职业 Program 内解析。
- 作者表精确翻转 3252，快照为 **40 张 `Implemented` / 24 张 `CatalogOnly`**。工作簿值差异、重导入/渲染、Luban、静态编译、单一 Editor 的 `Sync and Build All` 与 Unity MCP 定向 EditMode **70/70 passed** 均已取证；完整记录见 `../06_testing/2026-08-11-machine-gunner-mg11-explosive-elbow-runtime.md`。

## 18. MG11 验收完成后的边界（历史快照）

- 下一张 CatalogOnly 卡仍须先审计其权威来源、现有运行时 seam 和最小测试矩阵，再单独翻转表项。不得以 3252 的专用“当前 Burn Debuff”声明推导普通的延迟伤害、升级实例或跨卡泛化接口。
- `TwelveHits`、动态临时卡、延迟及下回合时机、选择、自动连锁、Power 持续触发、全息诱饵受攻击实体和超上限 Energy 继续独立切片；本完成态不扩展奖励/Run、Scene、Prefab、默认 Hero/Deck 或第二条写入链。

## 19. MG12：光学迷彩（已完成）

- `OpticalCamo` (3249) 以 Self 输入支付 2 Energy，使用既有职业私有状态操作施加 `Invisible +2`，并从 Hand 进入 DiscardPile；作者表的升级费用 1 只保持为元数据，未假设升级实例。既有伤害管线的隐身受击减半保持原样，本切片不把需求中的透明表现扩张为 UI 或场景改动。
- 隐身在玩家行动结束减少 1 层；普通攻击只在卡牌已成功完成卡区归宿后减少 1 层，失败攻击不消耗。3247 `SniperShot` 与 3248 `SpikeShot` 通过受限的 `PreservesInvisibleAfterSuccessfulAttack` 声明保留隐身；该声明独立于 `IsSniper`，所以 3248 保持现有开火加成，不被错误改成狙击伤害公式。
- 作者表仅翻转 3249，快照为 **41 张 `Implemented` / 23 张 `CatalogOnly`**。工作簿值差异、重导入/渲染、Luban、静态编译、单一 Editor 的 `Sync and Build All` 与 Unity MCP 定向 EditMode **75/75 passed** 均已取证；完整记录见 `../06_testing/2026-08-11-machine-gunner-mg12-optical-camo-runtime.md`。

## 20. MG12 验收完成后的边界

- 后续 CatalogOnly 卡仍须先审计权威来源、现有运行时 seam 和最小测试矩阵，再独立翻转表项。3249 的保留隐身声明只能表达成功攻击后的生命周期，不能作为延迟伤害、双词条伤害公式、升级实例或下回合时机的通用接口。
- `TwelveHits`、动态临时卡、延迟及下回合时机、选择、自动连锁、Power 持续触发、全息诱饵受攻击实体和超上限 Energy 继续独立切片；尤其 3264 的延迟狙击必须单独处理，不因本卡预先翻转。完成态仍不扩展奖励/Run、Invisible HUD/透明表现、Scene、Prefab、默认 Hero/Deck 或第二条写入链。

## 21. MG13：全息诱饵（已完成）

- `HoloDecoy` (3259) 以 Self 输入支付 1 Energy，使用既有职业私有状态写入 `Buffer +1`，并从 Hand 进入 ExhaustPile。Buffer 可叠层、不会在回合结束衰减；它只抵挡一次正值 incoming Attack，令该次伤害不改变 Block/HP，随后消费一层。零值攻击不消费，Buffer 完全抵挡时不消耗 Armor。
- 受击截断不在预演阶段写入真实状态：每个 Effect 链持有局部伤害公式序列，先预留 Buffer、再按“Damage settlement → Buffer 状态 settlement”提交。`PlannedSettlementCount` 包含后续状态 settlement，保证多段同链只消费一次，且敌人意图/玩家卡牌的 Order 连续。
- 作者表仅翻转 3259，快照为 **42 张 `Implemented` / 22 张 `CatalogOnly`**。工作簿值差异、重导入/渲染、Luban、静态编译、单一 Editor 的 `Sync and Build All` 与 Unity MCP 定向 EditMode **119/119 passed** 均已取证；完整记录见 `../06_testing/2026-08-12-machine-gunner-mg13-holo-decoy-runtime.md`。

## 22. MG13 验收完成后的边界

- `README web.md` 的“3259 升级后不消耗”与作者表 `upgraded_play_destination = ExhaustPile` 相冲突，且当前没有 CardInstance 升级状态。基础值以作者表 ExhaustPile 运行；本切片不擅自改升级字段，也不把这项冲突当作已支持。
- Buffer 序列 seam 只处理本次正值攻击的职业伤害覆盖，不等同于无实体、攻击重定向、诱饵生命、延迟伤害、选择、临时卡或 Power 事件总线。剩余 22 张继续按独立机制切片处理；不扩展奖励/Run、HUD、Scene、Prefab、默认 Hero/Deck、升级实例或第二条写入链。

## 23. MG14A：撤退与快速翻滚（已完成）

- `Retreat` (3216) 成功支付 2 Energy 后获得 15 Block、预约下回合补满当前 Ammo 上限、Hand→DiscardPile，并以 Queue 签发的系统 `EndPlayerActionCommand` 结束本次玩家行动。职业运行时不嵌套提交命令；控制器先冻结同 actor 的强制结束锁，再发布结果，系统 continuation 完成后清锁。
- `QuickRoll` (3235) 成功支付 1 Energy 后立即获得 5 Block，并叠加 `NextRoundBlock +5`。下一玩家回合开始固定按“清既有 Block → 清下挡并加 Block → 普通资源补充 → 清补满弹预约并填满 Ammo → 抽牌”结算；下挡可叠加、只在下次开始转化一次。
- 作者表仅翻转 3216、3235，快照为 **44 张 `Implemented` / 20 张 `CatalogOnly`**。Luban、静态编译、单一 Editor 的 `Sync and Build All`、定向 EditMode **7/7 passed** 与收紧普通卡断言后的完整 EditMode **571/571 passed** 均已取证；完整记录见 `../06_testing/2026-08-12-machine-gunner-mg14a-retreat-quick-roll-runtime.md`。

## 24. MG14A 验收完成后的边界

- `TacticalAdvance` (3234) 继续 CatalogOnly。它的免费攻击与 Stim 额外射击之间的弹药支付优先级尚未有权威组合语义，且需求声明的 Bound 前置规则没有运行时状态；本切片不猜测任一结果，也不因相邻两张卡完成而翻转 3234。
- 本次的两个私有下回合状态和 Queue 强制结束意图不是通用延迟/事件总线。延迟伤害、选择、动态临时卡、Power 持续触发、超上限 Energy、奖励/Run、HUD、Scene、Prefab、默认 Hero/Deck、升级实例及第二条写入链继续独立切片处理。

## 25. MG14B：游击战术（已完成）

- `GuerrillaTactics` (3251) 以 Self 输入支付 1 Energy 后从 Hand 进入 PowerPile；基础态每张卡获得 2 层游击，而不是沿用普通 Power 的固定 1 层。PowerPile 中的每张卡仍是独立实例，数值层数单独累加；当前没有升级 CardInstance，升级 3 层只保留为作者表元数据。
- 每张成功出牌的成本预览同时冻结实际 `AmmoSpent` 和游击触发用的 `AmmoSpentForGuerrilla`。当前普通卡令两者相等；原卡操作及既有功夫机甲后置钩子完成后，在同一投影事务内追加 `游击层数 × 名义弹耗` 的 Block。这样资源、Block、卡区与失败零写入仍由一次 Queue 事务保证，能力牌自身不耗弹时也不会虚构即时 Block。
- 作者表仅翻转 3251，生成快照为 **45 张 `Implemented` / 19 张 `CatalogOnly`**。Luban、静态编译、单一 Editor 的 `Sync and Build All`、定向 EditMode **6/6 passed** 与完整 EditMode **574/574 passed** 均已取证；完整记录见 `../06_testing/2026-08-12-machine-gunner-mg14b-guerrilla-tactics-runtime.md`。

## 26. MG14B 验收完成后的边界

- 名义弹耗字段只是后续免费攻击和虚拟耗弹的受控接缝，不等同于已实现 `TacticalAdvance`、固定机枪或临时 `MachinegunBurst`。尤其免费攻击与 Stim 的支付优先级仍需独立需求裁决，不能由“当前两字段相等”反向推导。
- 其余 19 张 CatalogOnly 卡继续按独立机制切片处理；不扩展奖励/Run、Power HUD、升级实例、动态临时卡创建、HUD、Scene、Prefab、默认 Hero/Deck、地图/敌人或第二条写入链。
