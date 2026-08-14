---
title: Marine Game 机枪兵 V2 卡牌单场战斗接入
page_type: plan
lifecycle: active
created: 2026-08-12
updated: 2026-08-14
scope: Hero 1002 的 V2 卡牌目录与单场战斗规则；不含地图、敌人、奖励流程、Run、场景或多人流程
status: unstoppable-settlement-trigger-verified-unity-native-2026-08-14
status_source: ../SESSION_LOG.md
source:
  - ../00_inbox/README web.md
  - ../01_requirements/2026-08-12-machine-gunner-v2-requirement-digest.md
supersedes: 2026-08-07-marine-game-card-only-integration.md
---

# Marine Game 机枪兵 V2 卡牌单场战斗接入

## 1. 目标与硬边界

以 `README web.md` 的 82 模板 V2 目录为行为来源，在不改变 `BattleCommandQueue.Submit` 唯一共享写入入口的前提下，逐个可验证切片扩展 Hero 1002 运行时。

- 只处理卡牌配置、职业运行时、必要的通用伤害/结算 seam、本地化和测试。
- 不把新版 README 的地图、敌人、奖励选择、篝火、事件、跨战斗状态、Run、Scene、Prefab 或通用 UI 改造视为本计划授权范围；V2V 只在现有 Hand UI 内接入卡牌必需的选择会话。
- 不用卡牌 ID、名称、文本或 UI 私有状态直接写战斗事实；职业程序只经生成的 `MachineGunnerProgramId` 注册。
- 不把当前没有的升级实例、AnyAlly、任意多选/跨玩家选择或免费攻击自动链伪装为基础态已完成；普通手牌单选协议只按 V2V 已验证边界成立。

## 2. V2A：伤害语义、词条、电磁增压与防御靶机（已完成）

- `MachineGunnerDamageRequest` 统一以 `DamageKind + CardTag` 声明每段伤害。私有规则档案集中解析 Attack、Support、Bomb、Burn 与 Debuff 的 Strength/Weakness/Smoke/Vulnerable/ArmorBreak/FirePower 规则；调用方不持有策略布尔值。
- `ElectroBoost` (3236) 更新为 Uncommon Power，基础态 `FirePower +3` 整场持续；`SpikeShot` (3248) 更新为 `Shoot | Sniper`，使原有射击联动与狙击倍率并存。
- `DefenseTarget` (3262) 开放为 Exhaust 卡并施加 Intangible。通用 Effect 链使用局部投影预约 Buffer/Intangible 后效，提交顺序保持 `Damage → 私有状态`，确保多段伤害逐段消费、失败路径零写入与 Order 连续。
- 作者表只翻转 3262，3236 已存在的实现状态保留但其类型/稀有/归宿按 V2 更正；当前既有目录为 46 `Implemented` / 18 `CatalogOnly`。Luban、本地化导入、Sync/Addressables、定向 110/110 与完整 584/584 EditMode 已验证。

## 3. V2B：目录扩展（已完成）

- 新增 3265–3282 的 18 张奖励模板、Program 65–82 与双语文本，目录达到 82 个模板；V2B 初始录入时它们复用 `art_placeholder` 并保持 `CatalogOnly`，目录录入本身不接入奖励、Run 或默认 Deck。
- 保留 V1 的 64 模板快照，V2 扩展快照验证这 18 个身份、连续 ID/Program、精确实现状态、空 bindings、占位图与升级元数据。V2C 验收后只允许 3273/3281 为 `Implemented`，其余 16 张 V2 身份仍为 `CatalogOnly`；该时点目录为 48 `Implemented` / 34 `CatalogOnly`。
- Luban、本地化导入、Sync/Addressables、定向 112/112 与完整 586/586 EditMode 已验证；V2B 初始时新身份尚未注册为运行时程序，随后只有 3273/3281 在 V2C 中获得独立基础态程序。

## 4. V2C：即时状态与生命周期（首个破甲即时卡切片已完成）

- `ThermiteBomb` (3273) 复用全体 Burn/Oil 与持续 `ArmorBreak`：对所有存活敌人先 `Burn +4`，再 `ArmorBreak +2`，基础态 Hand→DiscardPile。
- `Crush` (3281) 复用自动最近目标、普通 Attack 与逐段命中后置状态：先 9 点 Attack，目标仍存活时才 `ArmorBreak +4`，基础态 Hand→DiscardPile。
- V2C 验收时，`LoseStrength`、`Regeneration`、`Shackle`、格挡修正和其生命周期仍需独立、可预演的操作；其中 `LoseStrength` 已由后续 V2D 切片接入。通用 Strength/Block 事实继续沿用原有结算入口，不复制第二套伤害链；每张后续卡的基础态、卡区归宿、失败零写入与回合时序均需独立回归。

## 5. V2D：击退射击与失去力量（已完成）

- `KnockbackShot` (3223) 支付 1 Ammo；施放时按 Encounter 顺序快照前两名存活敌人，分别执行 7 点 Attack→存活后 `LoseStrength +2` 与 3 点 Attack→存活后 `LoseStrength +2`。缺第二名时跳过，首段击杀不递补，显式 `TargetId` 拒绝；不从卡名或弹耗推断 `Shoot` / `Sniper`。
- `LoseStrength` 是独立非负职业私有状态，不复用 Weakness、Vulnerable 或永久 Strength。Attack 使用 `max(0, baseDamage + Strength - LoseStrength)`，Burn 不受影响；携带者在自己的行动结束清零并写 settlement，敌方清除位于 action Effect / completion 后、intent advance 前，玩家清除位于回合末 Burn 前。
- 作者表仅翻转 3223。当前 82 模板为 49 `Implemented` / 33 `CatalogOnly`，V1 为 47/17，V2 扩展为 2/16。升级 9/5/+3 仍仅为元数据；Luban、本地化、Sync/Addressables、定向 10/10 与完整 597/597 EditMode 已验证。

## 6. V2E：延迟效果与支援链（已完成）

- Runtime 私有持有独立 `ScheduledEffect` 实例，冻结类型、来源、倒计时/剩余触发、基础态数值与插入顺序；每次施放创建独立实例，多实例不折叠成 Power stack，也未增加全局事件总线。随机段在触发时按当前投影存活敌人选择，完整计划成功才提交随机状态。
- 回合开始计划固定在最后一名敌人行动成功之后、敌方 Smoke 清理、玩家资源补充和抽牌之前。V2E 发布时的历史回合结束顺序为弃牌 → 清 Shackle/LoseStrength/既有临时状态 → Bomb → Burn；后续 Field Surgery 已把当前顺序补全为弃牌/既有清理 → Shackle → LoseStrength → Heal → Regeneration -1 → Bomb → Burn。
- `GuidedNuke` (3237) 基础态为 5 Energy、Shackle +1、第三个未来回合末全体 Bomb 99；`FiveHundredPounder` (3241) 为 3 Energy、第二个未来回合末全体 Bomb 60。两者都从施放回合末开始推进倒计时，多个炸弹按创建顺序独立结算；Shackle 只拒绝 Attack、允许 Skill，并在当前玩家行动结束清除。
- `BansheeStrike` (3238) 在后续两个玩家回合开始各锁定当前最近敌人并执行 Support 8×2，同一次触发首段致死不重定向；`FireSupport` (3239) 在下回合开始进行 5 次独立随机 Support 2；`FireBombardment` (3240) 在下回合开始进行两波全体 `Support 2 → 存活后 Burn +4 → Oil +3`。
- `TripleStrike` (3264) 支付 4 Energy 与 3 Ammo，在同一出牌事务先加 Invisible +2，再对显式目标执行两次 Sniper 12并保留 Invisible，进入 ExhaustPile；下回合开始对当前最远存活敌人执行 Support 20。`NeedleStorm` (3274) 支付 1 Energy，下回合开始进行 4 次独立随机 Delayed 1，并仅在对应目标存活时追加 ArmorBreak +1。
- “脑补”语义已冻结：Support 读取目标 Smoke、Vulnerable 与 ArmorBreak，不读来源修正；Bomb 只读取目标 Smoke，不读 Vulnerable、ArmorBreak 或来源修正；钢针 Delayed 只读取目标 Smoke。战斗终局跳过后续工作并清空遗留实例。作者表只翻转上述 7 个身份；82 模板现为 56 `Implemented` / 26 `CatalogOnly`，V1 为 53/11、V2 为 3/15。
- Luban、本地化导入、Sync/Addressables 均成功，Addressables 14.252 秒；Unity MCP 定向 EditMode `586264ec18e549d89d1a063aac4d7b93` 为 101/101，完整 EditMode `89cfdfe8441b45d39d0cd57d939734c7` 为 606/606（46.847 秒）。round-start 阶段内部按联合投影计划工作，但最后一名敌人行动已经先行提交；当前不存在横跨这两次事务的回滚，这是保留的原子性边界。

## 7. V2F：烟雾、防御与标记即时卡（已完成）

- `ChainSmoke` (3269) 为 1 Energy / Self / Discard，基础态只给施放者 `Smoke +5`；本地化名称不驱动程序，因此没有因卡名中的“抽”而伪造 Draw。
- `EmergencyCooling` (3272) 为 1 Energy / Self / Discard，在同一事务中严格执行 `GainBlock 8 → Smoke +3`；失败路径沿用既有能量门禁并保持零写入。
- `Mark` (3280) 为 0 Energy / 1 Ammo / 显式 Enemy / Discard，先造成 5 点普通 Attack，再只对存活目标追加 `ArmorBreak +2`。该程序显式为 `Tags.None`，不读取 Stim、FirePower 或 IncendiaryAmmo。
- 作者表只翻转上述 3 个 V2 身份。82 模板现为 59 `Implemented` / 23 `CatalogOnly`，V1 为 53/11、V2 为 6/12；Luban、本地化导入与 Sync/Addressables 均成功（11.414 秒），Unity MCP 定向 `054f72bc921749b5bad6d2efcc358b73` 为 83/83，完整 `ba418ab34a6d44038dddddc0233a03f8` 为 611/611（18.618 秒）。

## 8. V2G：私人改装基础态（已完成）

- `PrivateMod` (3268) 为 1 Energy / Self / PowerPile；成功后 AmmoMaximum +1，但当前 Ammo 原样保留，同时增加 `FirePower +1` 与一层 PrivateMod Power。
- 该卡复用既有 Power 事务和 Shoot 逐段 FirePower 规则，没有新增卡牌/支援命中后钩子。后续装填补至新上限；能量失败路径保持资源、状态、Power、随机流和卡区零写入。
- 作者表只翻转 3268。82 模板现为 60 `Implemented` / 22 `CatalogOnly`，V1 为 53/11、V2 为 7/11；Luban、本地化导入与最终 Sync/Addressables 均成功（最终 4.376 秒；首轮 11.092 秒后重新导入再构建），Unity MCP 定向 `cfcf49e9a16e447fb4033af6108c8dd9` 为 85/85，完整 `0f17edd2f31d40d2aba328def4448f3c` 为 613/613（18.617 秒）。

## 9. V2H：焚风基础态（已完成）

- `FoehnWind` (3276) 为 2 Energy / Skill / 显式 Enemy / Discard；成功时读取来源当前全部 Smoke。Smoke 大于 0 时，以该值作为基础 Burn 施加给目标，沿用目标旧 Oil 的 Burn 加成与减半规则，随后把来源 Smoke 清零。
- 专用复合操作联合预演和核对来源 Smoke、目标 Burn 与目标 Oil，再按 Burn → 可选 Oil → Smoke 顺序提交；Smoke 为 0 时仍成功支付费用并弃牌，但不生成私有状态写入。能量不足与目标规则错误保持资源、状态、随机流和卡区零写入。
- 作者表只翻转 3276。82 模板现为 61 `Implemented` / 21 `CatalogOnly`，V1 为 53/11、V2 为 8/10；Luban、本地化导入与 Sync/Addressables 均成功（12.164 秒），Unity MCP 定向 `69b8ded02aaa46368cad35e620567fd2` 为 89/89，完整 `4a90229794d24d3f8fd85154ab79c250` 为 617/617（17.657 秒）。

## 10. V2I：充能爆射基础态（已完成）

- `ChargedBurst` (3282) 为 2 Energy / Attack / AllEnemies / Discard；施放时按 Encounter 顺序快照全部存活敌人，第 `n` 名目标的基础伤害为 `12 + 6 × (n - 1)`，前三段为 12/18/24。前段致死不会删除槽位或重排后续目标的序号；显式 `TargetId` 被拒绝。
- 该程序只有 `Sniper` 标签，不读取 Stim 的额外段或 FirePower，逐目标读取 IncendiaryAmmo，并在成功攻击后保留 Invisible。所有伤害、燃烧弹药、2 Energy 支付与弃牌继续在同一权威出牌事务中提交；能量或目标门禁失败保持零写入。
- 作者表只翻转 3282。82 模板现为 62 `Implemented` / 20 `CatalogOnly`，V1 为 53/11、V2 为 9/9；Luban、本地化导入与 Sync/Addressables 均成功（11.456 秒），Unity MCP 定向 `1d5c9e1d96fe4ebcadd990fcc73fccdc` 为 94/94，完整 `822d066bc54c43d78ac206072789f840` 为 622/622（18.193 秒）。

## 11. V2J：回合能量修正基础态（已完成）

- `Overload` (3213) 为 0 Energy / Skill / Self / Discard；成功时即时获得 2 Energy，但不超过 EnergyMaximum，并累计一层下一回合能量补给 Penalty。即时获得通过独立的 `BattleEnergyGainedSettlement` 记录实际变化量。
- `DefensiveStance` (3271) 为 1 Energy / Skill / Self / Discard；同一事务严格执行 `GainBlock 8 → NextRoundEnergyGainBonus +1`。能量不足与显式目标错误在首次写入前失败，保持资源、Block、私有状态、卡区、随机流和表现结果零写入。
- Bonus 与 Penalty 是两项独立、非负、可叠加的一次性状态。下一玩家回合开始以 `max(0, baseGain + bonus - penalty)` 计算本轮补给，再按 EnergyMaximum 裁剪；补给使用 `BattleEnergyRefilledSettlement`，随后两项状态分别清零，且不会把回合开始补给伪装成主动获得。
- 作者表只翻转 3213 与 3271。82 模板现为 64 `Implemented` / 18 `CatalogOnly`，V1 为 54/10、V2 为 10/8；Luban、本地化导入与 Sync/Addressables 均成功（11.72 秒），补强后的 Unity MCP 定向 `3e73f867e7404be8a3180660e4999d20` 为 136/136，完整 `56274033527e4c78b50a78313bcc0f6c` 为 631/631（17.642 秒）。
- `LimitOverload` (3260) 明确延期：当前卡在程序操作提交后才移出 Hand，直接使用 `DrawCards(10)` 实现“抽到手牌满”会把本卡仍计入容量并少抽一张。后续必须先提供基于成功归宿后投影 Hand 的专用“抽至满手”预演/提交 seam；本切片不使用固定抽牌数、提前移牌或额外补抽绕过该边界。

## 12. V2K：便携帮手即时射击后置伤害（已完成）

- `PortableHelper` (3267) 为 1 Energy / Power / Self / PowerPile；每次成功施放增加一层整场持续的帮手，多层共存且跨回合保留。
- 每一段卡牌即时 `IsShootCategory` 伤害完成来源 Damage 与全部既有命中后/全局钩子后，若原目标仍存活，每层帮手依序向同一目标追加一次独立基础 1 点伤害。来源段致死不触发，帮手段致死停止剩余层数，不重定向、不递归。
- 帮手专用伤害只读取 FirePower、目标 Vulnerable 与 ArmorBreak，并经过 Block/HP；忽略 Strength、Weakness、双方 Smoke、目标 Invisible 与狙击倍率。帮手段为 `Tags.None`，不触发 Stim、IncendiaryAmmo、AgedOil、KungfuMech、Ammo、Invisible 生命周期或再次帮手。
- 作者表只翻转 3267。82 模板现为 65 `Implemented` / 17 `CatalogOnly`，V1 为 54/10、V2 为 11/7；Luban、本地化导入和 Sync/Addressables 均成功（12.163 秒），Unity MCP 定向 `95707f1918fa4633b671c6a10f9b0da3` 为 120/120，完整 `8c0ce8f925e94a35b893f5b5892ef447` 为 639/639（131.4561842 秒）。
- Shotgun 属于同一 `IsShootCategory`，但当前没有实际卡实例，因此只有结构契约而无直接运行用例；延迟 Support/Bomb/Needle/TripleStrike 延迟段不经过即时卡牌命中入口，结构上不会触发。本切片不把两项结构证据扩大为已验证的跨模块玩法。

## 13. V2L：狂轰滥炸延迟支援载荷增幅（组合验收已完成）

- `Bombard` (3265) 为 1 Energy / Power / Self / PowerPile；每次成功施放增加 4 层整场持续的 Power。既有延迟实例不快照层数，四类声明的 scheduled Support 在各自触发时读取当前层数。
- 白名单只包含 `BansheeStrike`、`FireSupport`、`FireBombardment` 与 `TripleStrike`。每层把相应载荷提高 10%，正值按 `floor((baseValue × (100 + 10 × stacks) + 50) / 100)` half-up；该取整规则是用户授权“脑补”后冻结的实现决定。
- 女妖、火力支援与三连击只缩放延迟 Support 伤害；燃烧轰炸分别缩放 Support Damage、Burn 与 Oil，随后继续按 Damage→存活后 Burn→Oil 处理。伤害缩放发生在现有 Support 管线前，因此目标 Smoke、Vulnerable 与 ArmorBreak 的读取口径不变。
- Bomb、Needle Delayed、回合末 Burn、即时攻击、便携帮手及其他来源不受影响；命中数、波次数、倒计时、目标选择和调度生命周期不变。没有新增全局伤害事件，也没有修改 Support 伤害档案。
- 作者表只翻转 Q155（3265）。82 模板现为 66 `Implemented` / 16 `CatalogOnly`，V1 为 54/10、V2 为 12/6；Luban、本地化与 Sync/Addressables 均成功（12.963 秒）。Unity MCP 定向 `9c21aa7c79b94f1980988945d35636dd` 为 134/134（1.4521749 秒），精确素材真实加载 `da1d1e3969014e81b06cb57a2392de13` 为 1/1（106.8572486 秒）。两次 645 项完整任务与一次素材测试类任务均只在相同冷加载用例触发 180 秒 timeout，因此以组合门禁收口，不声称完整套件单任务全绿。

## 14. V2M：天空之怒逐层随机支援追击（已完成）

- `SkyWrath` (3266) 为 1 Energy / Rare / Power / Self / PowerPile；每次成功施放增加 1 层整场持续的 Power，卡本身没有即时伤害或随机推进。
- 受限钩子只位于四类原始 Support 逻辑段结束点：Banshee 每 hit、FireSupport 每 hit、FireBombardment 每 wave、TripleStrike 延迟 Support 一次。FireBombardment 必须先完成该波全部目标的 Damage/Burn/Oil；NeedleStorm、Bomb、回合末 Burn、即时攻击、PortableHelper 与天空之怒自身均不触发。
- 每个 Power 层分别重取当前投影中的存活敌人并调用一次随机流；单候选也消费 `NextInt(1)`。该层先对随机主目标造成基础 8 点 Support，再按层开始时快照的 Encounter 顺序对其余目标各造成基础 4 点 Support；下一层看到前层致死后的新候选，无存活目标时停止。
- Bombard 先按既有正值 half-up 规则缩放天空之怒基础 8/4，再进入 Support 的目标 Smoke、Vulnerable 与 ArmorBreak 管线；4 层 Bombard 对应 11/6。没有新增全局事件、伤害类型或共享写入路径。
- 作者表只翻转 Q156（3266）。82 模板现为 67 `Implemented` / 15 `CatalogOnly`，V1 为 54/10、V2 为 13/5；Luban、本地化与 Sync/Addressables 均成功（12.956 秒）。Unity MCP 翻表前任务 `eefded85c7aa4a099d3b16ee4577e704` 为 117/117，最终定向 `3a279411d63749abaf8eca64ec4236cc` 为 139/139，完整 `a46a25a9da924131965130d6e2b07b8b` 为 650/650（174.2163423 秒）。
- 开发中两轮红测分别由场景总能量 6 超过 fixture 上限 5、以及随机 oracle 把 raw state 误当构造 seed 引起；两次都只修正测试场景/oracle，生产实现未改变。

## 15. V2N：极限过载离手后抽至满手（已完成）

- `LimitOverload` (3260) 为 0 Energy / Rare / Skill / Self / DiscardPile；成功时顺序为 `EnergySpent(0) → 可选 GainEnergy(1) → 当前卡离手并抽至 Hand 10 → NextRoundEnergyGainPenalty +3`。获能受 EnergyMaximum 裁剪，已在上限时不生成伪 `BattleEnergyGainedSettlement`。
- `BattleCardZonesData` 新增 Prepare / Validate / Commit 深事务 seam。Prepare 零写入地冻结所属聚合、原 `Layout`、洗牌随机前/后状态、最终布局与 settlement；Validate 拒绝所属、布局、随机或一次性漂移；Commit 不再随机，只发布一次最终 `Layout`，没有暴露超过 10 张的中间 Hand。
- 抽牌容量以当前卡成功离手后的投影 Hand 计算。同次重洗只使用原 DrawPile 和原 DiscardPile；3260 在抽牌计算后才进入弃牌堆，不会被同次重洗抽回。普通 `DrawCards` 不变，也没有增加第二条 Queue 写入路径。
- Penalty 可按多张 3260 叠加；下一玩家回合沿用 V2J 的 `max(0, baseGain + bonus - penalty)`，再按 EnergyMaximum 裁剪并在同次回合开始清除一次性状态。3260 不是 Attack/Shoot，不触发 Ammo、Stim、IncendiaryAmmo 或 PortableHelper。
- 作者表只翻转 Q150（3260）。82 模板现为 68 `Implemented` / 14 `CatalogOnly`，V1 为 55/9、V2 为 13/5；Luban、本地化导入/校验与 Sync/Addressables 均成功（15.828 秒），Unity MCP 正式定向 `feda36c5daef4fffab34065ba5988686` 为 169/169（2.2836982 秒），完整 `a84b5bb4f7dd4ca1b9791c81bb930973` 为 659/659（282.0044831 秒）。CardArt 与 Character Prefab 的 Addressables 冷加载较慢但均通过，本切片已按标准完整门禁收口。

## 16. V2O：隐秘行动与通用 Innate 首次起手（已完成）

- `Card` bean 与作者表增加强类型非空布尔字段 `is_innate`，默认 false；当前只让 `StealthAction` (3275) 为 true。运行时消费生成字段 `IsInnate`，不从卡名、描述、ProgramId 或卡牌 ID 推断固有。
- Turn 在 `StartBattle` 的任何写入前，按静态 `IsInnate` 收集每个存活玩家牌组中的固有实例，再让 CardZones 统一准备起手；该路径不识别 3275 或 Program 75。CardZones 先沿用既有 Deck 洗牌，再按实际 DrawPile 抽取顺序把固有实例选入 Hand：0～5 张固有时以普通牌补至默认起手 5，6～10 张时全部固有入手且不补普通牌，超过 Hand 上限 10 时返回 typed failure 且零写入。
- 首次起手 Prepare/Validate/Commit 冻结所属、初始布局、洗牌随机状态、最终布局与起手顺序；成功不推进随机，只发布一次最终 `Layout`，移动 settlement 先固有后普通、各组保持已洗牌抽取顺序且 Order 连续。Innate 只影响首次起手；后续回合保持普通“补至目标”逻辑，不重复挑选固有。
- Program 75 基础态为 1 Energy / Uncommon / Skill / Self / Hand→DiscardPile，严格执行 `Invisible +1 → DrawCards(1)`。出牌事务保留现有 `EnergySpent → 程序操作 → 成功归宿`；满 10 Hand 时抽牌受容量裁剪为 0，随后当前卡进入弃牌堆，最终 Hand 为 9。
- 作者表只开放 3275，生成目录为 69 `Implemented` / 13 `CatalogOnly`，V1 为 55/9、V2 为 14/4；升级 Invisible +2 / Draw 2 保持元数据。Luban、本地化、`Sync and Build All` 与 Addressables 已通过（18.363 秒）；Unity 正式目录快照 `8acfa22da51c4f2fb757bbe102fb945c` 为 21/21，最终聚合定向 `982a4f4c4af24ba78e678bf0e66f2ce1` 为 237/237，完整 EditMode `91d060c915ff4dfea42608b7c22669ab` 为 673/673。

## 17. V2P：机枪扫射临时卡基础态（已完成）

- `MachinegunBurst` (3263) 为 0 Energy / Attack / RandomEnemy / ExhaustPile / 无升级；执行两段独立的基础 5 点普通 Attack，每段都从当时投影的存活敌人重新随机选择目标。首段击杀会改变第二段候选，显式 `TargetId` 由目标门禁拒绝，失败不推进卡牌随机流。
- 实际 Ammo 成本为 0，不扣资源或生成 `AmmoSpent`；游击战术单独读取名义弹耗 2，因此其 Block 在全部伤害后、当前卡离手前结算。名义覆盖不改变其他资源、UI 或 settlement 事实。
- 来源没有声明 Shoot 标签，项目冻结 `Tags.None`，不从卡名推断射击，因此 Stim、IncendiaryAmmo、FirePower 与 PortableHelper 均不参与。同时 3263 以统一派生属性显式退出 KungfuMech、AgedOil 与 `NonShootAttackRecent`；它仍使用普通 Attack 伤害公式、Block/HP 和致死生命周期。
- 作者表只翻转 3263。82 模板现为 70 `Implemented` / 12 `CatalogOnly`，V1 为 56/8、V2 扩展保持 14/4；正式表 SHA-256 为 `B65D97253A43B2FF8575BCEE6F230B651EFD36FE84A10B7ACBFC0BCC62A0AB29`。Luban、本地化与 Sync/Addressables 均成功（11.757 秒）；Unity MCP 最终定向 `0f60a2e799904069ab68ae6f13a91953` 为 154/154，域重载后的 CardArt 探针 `f87e7034664a4126bb0b32c2888751e9` 为 1/1，完整 EditMode `a078688b69bd4f198bb736c6285ab5e7` 为 678/678。
- 3261 仍为 `CatalogOnly`，没有生产临时卡创建入口；本切片只证明 3263 可由直接运行时夹具正确执行，不声称它已能在正常产品流程中生成或获得，也不声称奖励排除逻辑已实现。

## 18. V2Q：固定机枪与临时卡生产（已完成）

- `FixedMachinegun` (3261) 基础态冻结为 2 Energy / Rare / Skill / Self / ExhaustPile。成功顺序为 `GainBlock 10 → 来源 Hand→ExhaustPile → 剩余 Hand 按原顺序进入 DiscardPile → 为每张被弃旧手牌创建一张 3263 到 Hand`；剩余 Hand 为空时创建 0 张。升级 15 Block 仍仅是作者表元数据。
- `BattleCardZonesData` 通过单一 Prepare / Validate / Commit 深计划冻结原布局、卡实例分配状态、来源归宿、其余手牌原序、新实例与最终布局；Commit 不再分配并只发布一次 `Layout`。临时牌用 `CardCreated` settlement 表达从无到有，不伪装成 DrawPile→Hand，也不进入 Deck。
- 表现层为来源提供 `HandToExhaust`，为每张创建牌提供 `CreatedToHand`，并继续按权威顺序消费其余 Hand→Discard。职业程序 registry 声明 3263 的动态模板依赖，Session 汇总为 `AvailableCardTemplateIds` 后交给 Hand 异步预载，使本局 Deck 不含 3263 时也能显示生产实例。
- 正式作者表只翻转 3261，SHA-256 为 `02F549502D14214C98B4BA97212962B05E58A9B768EF1D7E4CAD441E1DCD6FB7`，`is_innate=false`。Luban 于 22:00:11 成功生成全项目 168 个 Card JSON；Marine 目录为 71/11、V1 57/7、V2 14/4，3261 为 status 0 / Program 61 / Exhaust / 非 Innate。Localization import/validate 与 Sync/Addressables 已通过（13.42 秒）；force scripts 域重载后，最终聚合定向 `ba19d1744f084167927568f5572f91e6` 为 262/262（30.1698095 秒），完整 EditMode `dc6a1453b602487c8bfbbe7e42c3968d` 为 690/690（20.8279366 秒），均为 0 failed/skipped。
- TDD 红测先后暴露旧 prelude 不支持多张 Hand→Discard（任务前缀 `404d20…`）与缺少 `CardCreated` 结果 guard（任务前缀 `2045cc…`）；修复后核心任务前缀 `d6db34…` 为 12/12、非表格定向任务前缀 `f415877…` 为 195/195。最终审查发现并修复动态 3263 插画未沿 registry→`Session.AvailableCardTemplateIds`→Hand async preload 的 blocker，动态精确任务前缀 `6bf4…` 为 2/2，最终聚合与完整任务均已覆盖。
- 本切片不实现升级实例、升级 15 Block、默认 Deck、奖励排除、Run、多人、Scene 或 Prefab；3263 仍只由 3261 生产或测试夹具直接提供，不因此进入普通抽牌或奖励来源。

## 19. V2R：霸凌按目标起始状态种类抽牌（已完成）

- `Bully` (3278) 基础态为 0 Energy / Uncommon / Attack / 显式 Enemy / DiscardPile。成功时保留 `EnergySpent(0)`，再造成基础 6 点普通 Attack；伤害与既有非射击命中后链结束后，按命令开始时目标活跃状态种类的冻结数量执行普通抽牌，最后当前牌 Hand→DiscardPile。升级 9 伤仍只是作者表元数据。
- 来源只声明“目标每有一种状态抽 1”，没有定义计数集合与时点。项目冻结 Strength 非零、Vulnerable 正层，以及每种正层数 `MachineGunnerCombatantStatus` 各计一种，同种多层不重复；HP、Block、资源、PowerPile 卡实例、Stim 与延迟实例不计。伤害消费状态、命中后新增 Oil 或目标致死不会改变冻结数量；这是受控实现决定，不伪称为来源逐字规则。
- CardZones 复用 `PrepareDraw` / `ValidatePreparedDraw` / `CommitPreparedDraw` 在首次写入前冻结手牌上限、抽/弃牌堆、洗牌随机状态、最终布局与 settlement。0 种状态抽 0；Hand 10 时不抽且不推进随机，当前牌最后离手后 Hand 为 9。目标错误、快照漂移及其他失败维持完整零写入。
- 正式作者表只翻转 Q168，SHA-256 为 `878812D99F68C8F9B9A7BC620E2794180F6E8A3F21B5252B16A12BDB70915499`，`is_innate=false`。Luban 于 22:48:16 成功；Marine 目录为 72/10、V1 57/7、V2 15/3，3278 为 status 0 / Program 78 / Discard / 非 Innate。Localization、Sync/Addressables（16.521 秒、BuildLayout）、补强 6/6、非表格聚合 150/150、正式聚合 209/209 与完整 EditMode 697/697 均通过。
- TDD 任务 `36ffd31603d14de38de4912faf8fb4c1` 的 3 项中唯一红项是测试误忽略生产既有 `EnergySpent(0)`；只修测试预期，生产实现未改。默认 Deck、奖励、Run、升级实例、多人、Scene 与 Prefab 不在本切片范围。

## 20. V2S：先发制人按来源起始状态种类抽牌（已完成）

- `PreemptiveStrike` (3277) 基础态为 0 Energy / 1 Ammo / Uncommon / Attack / 显式 Enemy / `Tags.None` / DiscardPile。成功时造成基础 8 点普通 Attack；Damage 与既有 post-hit 链完成后，按命令开始时来源活跃状态种类的冻结数量执行普通抽牌，目标致死仍抽，最后当前牌 Hand→DiscardPile。升级 12 伤仍只是作者表元数据。
- 来源只声明“自己每有一种状态抽 1”，没有定义集合与时点。项目冻结 Strength 非零、Vulnerable 正层，以及每种正层数 `MachineGunnerCombatantStatus` 各计一种，同种多层不重复；Power、Stim、scheduled effect、Block 与资源不计。V2S 发布时为 16 种；CD-104 追加 Regeneration 后当前为 17 种。Shackle 保持上游 Attack 门禁零写入，其余 16 种可进入成功计数。这是受控实现决定，不伪称来源逐字规则。
- CardZones 复用 `PrepareDraw` / `ValidatePreparedDraw` / `CommitPreparedDraw` 在首次写入前冻结请求数量、Hand 上限、抽/弃牌堆、重洗随机状态、最终布局与 settlement。Damage / post-hit 后只提交旧计划；目标错误、Ammo 不足、Shackle 或快照漂移继续保持 Energy、Ammo、伤害、状态、随机流和卡区零写入。
- 正式作者表只翻转 Q167，SHA-256 为 `6C9120A317622F103F9A0DDEEEBB994B28F88230B679BA7E0B1D28201F8E2648`，U167 `is_innate=false`。Luban 于 23:26:12 成功；Marine 目录为 73/9、V1 57/7、V2 16/2，3277 为 status 0 / Program 77 / Discard / 非 Innate。Localization、Sync/Addressables（13.966 秒、BuildLayout）、静态编译、最终 V2S 5/5、正式聚合 214/214 与完整 EditMode 702/702 均通过。
- TDD 首轮 `634966a39d1a434886289cca3382e8f9` 为 3/5，重洗记录顺序与 Shackle 上游门禁两项均为测试 oracle；最终 `73d5e79a25164857b48bb5b1fba5d92a` 为 5/5。正式聚合首轮 `629f7e51d61d4bb49a6bdb6232239ca6` 的唯一红项是旧 Bully 操作名称 oracle，生产未改；最终 `ea21d256c5b840629a270eed7a10bd90` 为 214/214，完整 `1484317edb124dcdb6cb0f0862a8758a` 为 702/702，production 审查无 blocker。

## 21. V2T：战术推进二元免攻与共享费用冻结（已完成）

- `TacticalAdvance` (3234) 基础态为 2 Energy / Skill / Self / DiscardPile；成功时先获得 10 Block，再刷新一份“下一张成功 Attack 免费”的独立二元授权。重复施放不叠加，授权跨回合保留；Skill、Shackle、目标/费用/计划或卡区失败不消费，第一张成功 Attack（包括致死）在成功归宿后消费。Shackle 保持在费用解算前的既有 Attack 门禁。
- 授权由职业运行时的独立 bool + revision 持有，不进入 `MachineGunnerCombatantStatus`，因此它本身不改变 3277 / 3278 的状态种类计数。V2T 发布时集合为 16 种；后续 CD-104 仅因新增 Regeneration 将当前集合扩为 17 种。当前来源 `README web.md`、正式表与 i18n 一致采用基础 10 Block / 升级 14 Block；历史 HANDOFF 12/16 被当前来源覆盖，升级 14 仍只是元数据。
- 公共 `BattleCardCostResolver` 统一冻结 Fixed / X 的实际支付、效果值与触发器名义值。通用普通 Fixed 和机枪兵费用是两个真实适配器；既有通用 X 未扩展。机枪兵继续分离 Ammo actual / effect / nominal 与 Stim：Waived 只把实际 Energy / Ammo 归零，Fixed / UpToLimit 的 Stim 段保留并进入 Guerrilla 名义耗弹，AllAvailable 保留既有免费 Stim 段；ComboElbow 分类保持独立。
- 正式作者表只翻转 Q124，SHA-256 为 `55D43141149D7A86D7957B1C43ED9303B9E9D091094E0CFAF2CF39FE2F73C569`。Luban 于 01:44:15 成功；Marine 目录为 74/8、V1 58/6、V2 16/2。Localization、Sync/Addressables（14.762 秒、BuildLayout）、静态编译、V2T 6/6、致死/溢出补强 1/1、Starter 142/142、正式快照 36/36、含真实 AB 的正式聚合 213/213 与完整 EditMode 721/721 均通过；详情见 `../06_testing/2026-08-13-machine-gunner-v2t-tactical-advance-runtime.md`。
- 本切片不实现升级实例、自动免费攻击链、默认 Deck、奖励、Run、UI 专属提示、多人或战士免攻策略；共享 resolver 只是未来适配 seam，不代表这些消费者已经接入。

## 22. V2U：不解释12连两波换弹射击（已完成）

- `TwelveHits` (3257) 基础态为 3 Energy / Rare / Attack / 自动最近敌人 / DiscardPile。普通支付按命令起点冻结第一波当前 Ammo 最多 6 发，随后无条件补满 Ammo，再冻结第二波最多 6 发；0 Ammo 仍可施放，第一波为 0 次后照常换弹。每个来源段基础 5 伤，目标死亡停止剩余伤害且不重定向，但冻结的换弹、第二波支付与成功生命周期继续提交。
- 机枪兵私有纯 `MachineGunnerReloadedVolleyResolver` 只解析两波 effect / actual、补满前后、Stim、Guerrilla nominal 与 final Ammo；公共 `BattleCardCostResolver` 继续负责 Energy normal/waived。免费 Attack 的实际 Energy/Ammo 为 0，但仍换弹并按两波上限展开，Stim 激活时整卡 nominal Ammo 为 13；逐 hit 伤害复用既有 IncendiaryAmmo / PortableHelper 后置链。
- 正式作者表只翻转 Q147，SHA-256 为 `7131597FD5F3D948921F54926C0205E24E31F747D7C9B1206B78902AE6BEF818`；生成 JSON SHA-256 为 `28324422913241FC627F5C3A0BCF715332E4F2B3DCDFA94E4B6E4FF3ED7A6306`。Luban 于 03:00:27 成功，Marine 为 75/7、V1 59/5、V2 16/2。Localization、Sync/Addressables（12.173 秒、BuildLayout）、静态编译、六项逐片 TDD、Starter 148/148、正式快照 37/37、含真实 AB 的正式聚合 220/220 与完整 EditMode 728/728 均通过；详情见 `../06_testing/2026-08-13-machine-gunner-v2u-twelve-hits-runtime.md`。
- 升级 2 Energy / 每段 6 伤仍只是作者表元数据；默认 Deck、奖励、Run、UI、多人、自动免费攻击链、通用两阶段资源协议和剩余目录卡不在本切片范围。

## 23. V2V：排气散热与共享手牌单选协议（已完成）

- `VentHeat` (3244) 基础态为 0 Energy / Skill / Self / DiscardPile。存在另一张合法手牌时必须精确选择一个不同实例，结算顺序为 `EnergySpent(0) → selected HandToExhaust → EnergyGained(仅实际 +1 时) → source HandToDiscard`；来源是唯一手牌时直接弃置且不获能，能量已满时仍消耗选择牌但不产生伪获能记录。
- `PlayCardCommand.SelectedCardIds`、`BattleHandCardSelectionRequest`、`BattlePreparedHandCardSelectionResolution` 和 `HandCardSelectionSession` 形成共享普通手牌选择 seam。卡区计划一次冻结并提交两张牌的归宿，只发布一次 `Layout`；规则或 Layout / Turn / Queue 快照失效时不写 Turn、资源、卡区或 settlement。UI 会话保持局部不可变：候选左键确认，来源左键或任意右键取消，选择中禁拖并展示候选角色，漂移/禁用/销毁时清除。
- 双 transient 直接消费真实 settlement，不创建伪 prelude；selected 先飞向 Exhaust 并清理，source 后飞向 Discard 并清理。该选择协议可供后续 Ironclad `Burning Pact` 编写独立适配器，但 V2V 没有实现或翻转任何战士卡。
- 正式作者表只翻转 Q134，SHA-256 为 `B3BA678FBC0C021F49C3F9FEDE4190099960EE109FFC302D96C77F29D54F4A6D`；i18n 只修改 B/C404-405，SHA-256 为 `8833E99F546B2C1195C4F0317A1B9208535ED083743F1ABF183874EFFFD23D77`。Luban 于 14:55:40 成功，生成 JSON SHA-256 为 `5988DA20801C8BF724EF0E471466A0A746A5E732DE3450BD7680F00A735F2615`；全项目 168 张为 85/83，Marine 为 76/6、V1 60/4、V2 16/2。Localization、Sync/Addressables（15.85 秒，BuildLayout `buildlayout_2026.08.13.14.59.24.json`）、静态编译、行为 15/15、目录 38/38、含真实 AB 的正式聚合 306/306 与完整 EditMode 744/744 均通过；详情见 `../06_testing/2026-08-13-machine-gunner-v2v-vent-heat-runtime.md`。
- 升级获得 2 Energy 仍只是作者表元数据；默认 Deck、奖励、Run、多人、Scene、Prefab、战士 Burning Pact 和剩余 6 张目录卡不在本切片范围。

## 24. 后续切片：跨卡协议

- V2Q 的临时卡生产、V2R 的目标冻结状态计数与 V2S 的来源冻结状态计数普通抽牌均已完成标准门禁；后续不得把 `CardCreated` seam 扩大为普通 Draw、奖励获取或永久 Deck 写入，也不得把两种受限计数变成所有模块共享的状态注册表。
- 普通手牌单选与原子双归宿已由 V2V 提供共享 seam；Burning Pact 等消费者仍须各自冻结规则、效果和回归，不能仅因 seam 存在便宣称已实现。自动免费攻击、手牌保留、AnyAlly、升级实例、任意多选与跨玩家选择仍分别需要权威协议或共享模型扩展，不能借用 TacticalAdvance 授权或 Innate 入口偷换语义。
- Prismatic Shot 已作为共享 concrete repeated-damage plan 的固定目标消费者完成；共享 planner 只负责目标、投影、随机快照与 Prepare/Validate/Commit 生命周期，Ammo、Stim、IncendiaryAmmo、PortableHelper 和职业状态集合仍由机枪兵适配器拥有。不得因已有固定/随机两种策略而推导全体、权重、链式、动态改选或全局伤害事件。

### Field Surgery 与共享 Heal（已完成）

- `Field Surgery`（3231）基础态为 1 Energy / Rare Skill / Self / ExhaustPile；Program 31 出牌只按序获得 Regeneration 5 与 Shackle 1，不立即治疗。Shackle 溢出在 Regeneration、Energy、Health、卡区与 settlement 首写前失败。
- Regeneration 作为第 17 个职业私有状态追加而不改变旧枚举值。行动结束计划冻结 actor、Health、MaxHealth、Regeneration、Shackle 与 LoseStrength，并按 `Shackle → LoseStrength → Heal → Regeneration -1` 提交；随后才进入 Bomb 与 Burn。普通 Heal 和 Regeneration 共用封顶 outcome、内部生命写入口、settlement 和正实际值表现，职业生命周期不进入通用 executor。
- 红灯 `32984ebfa0e14118a9f16f5ea3606c0a` 暴露 Program 31 未支持；来源顺序红灯 `c8d4aa4ddf7347dc8d6515f297c9ed90` 暴露旧 Heal 过早。最终精确行为 9/9、正式目录 50/50、治疗视图 1/1、含真实 AB 聚合 243/243、完整 EditMode 766/766 均通过。
- Field Surgery 发布时剩余 5 张 `CatalogOnly`；当前 Prismatic Shot 完成后剩余 **4 张**，继续由精确目录门禁阻止运行时执行。升级 6、AnyAlly / 多玩家、默认 Deck、奖励、Run、Scene / Prefab 和升级 CardInstance 仍未实现。

### Prismatic Shot 与共享重复伤害计划（已完成）

- `Prismatic Shot / 幻彩射击`（3279）基础态为 0 Energy / Rare Attack / 显式 Enemy / DiscardPile，Program 79、基础 Ammo 1、Shoot 标签。命令开始冻结目标状态种类 `S`：Strength 非零、Vulnerable 正层与 17 种职业私有状态正层各计一次；逻辑段为 `[6, 9 × S]`。
- Stim 激活时每个逻辑段后立即复制一次同基础值，整卡 Ammo 冻结为 `1 + logicalCount`；例如 `S=2` 时逻辑段 6/9/9，Stim 后为 6/6/9/9/9/9，所需 Ammo 为 4。费用必须全额满足，不能支付部分 Ammo 后执行部分段。
- 每个来源或 Stim 复制段都由 `MachineGunnerRepeatedDamageHitSequence` 冻结并按 `main Damage → IncendiaryAmmo Burn → PortableHelper` 提交；固定目标投影死亡后停止全部尾段，不重定向。共享 `BattleRepeatedDamageExecutor` 不读取 Program、Ammo、Stim 或职业状态，机枪兵适配器也不复制目标/RNG/计划生命周期。
- 初次广义行为聚合暴露的 settlement 前缀回归已以“先把本地 EnergySpent / 可选 AmmoSpent 放入 `settlements`，再 Prepare 所有深卡区计划”修复，首次权威写仍在全部 Validate 之后。代表回归 5/5、行为聚合 243/243 与完整 EditMode 776/776 均通过。
- 正式目录现为 Marine **78/4**（V1 **61/3**、V2 **17/1**），全项目 **90/78**。正式门禁 `908e5fb8b93e437d89533bb1b727231a` 53/53、双卡定向 `6932f72f288a477ca5869c21e3ac3996` 11/11、完整 `3e0a091d891e4f918668b99cb4a20157` 776/776；Luban、Localization、Sync/Addressables 与真实 BuildLayout 均已收口。升级首段 9 / 重复段 9、升级实例、默认 Deck、奖励、Run、多人、Scene/Prefab 和剩余 4 张目录卡未实现；详见 `../06_testing/2026-08-13-shared-repeated-damage-sword-boomerang-prismatic-shot-runtime.md`。

### Secondhand Smoke 与通用 Poison（已完成 Unity 原生验证）

- `Secondhand Smoke / 二手烟`（3270）基础 Program 70 在首写前冻结来源当前 Smoke，并向显式目标施加同值通用 Poison；Smoke 不被清除或改变。Smoke 为 0 时仍成功支付和弃置来源牌，但不产生 Poison settlement。升级“来源与目标 Smoke 总和”仍只是 metadata，基础态不得提前读取目标 Smoke。
- Poison 是参与者通用权威事实：行动开始 tick 绕过 Block，生命损失为 `min(Poison, Health)`，层数随后减 1，致死也减层。敌人致死时不执行行为 / intent advance，玩家致死时不执行 Block / 资源 / 抽牌 reset；非致死才继续旧流程。表现只显示 Health loss number，致死再做 death transition，不伪造 Attack / Block 反馈。
- 3277 / 3278 / 3279 当前共用 Strength + Vulnerable + Poison + 17 种职业私有状态的 20 种最大集合；Poison 正层只计一种，3277 读来源，3278 / 3279 读目标。Secondhand 的普通来源归宿在 Poison 写入前冻结，observer 漂移时由最终单次 Layout 发布收口；改写卡区的其他程序仍走既有深事务。
- 本切片不修改 Prefab，因此常驻 Poison 图标、层数 HUD 和 pulse 未实现。开发中任务前缀 `419c…` 2/2、`b5f…` 8/8、`79a…` 289/289 保留为前置证据；正式表、Luban、Localization、`Sync and Build All`、BuildLayout / `AssetBundleProvider` 均已收口，最终定向任务 `88e36d2a5cbb47b7b4a67207dad00856` 为 9/9，完整 EditMode `9ca3d43a79d24b25a917fad7b6166584` 为 793/793。当前正式 Marine 为 **79/3**（V1 **61/3**、V2 **18/0**），全项目为 **92/76**；详见 `../06_testing/2026-08-14-shared-source-magnitude-poison-body-slam-secondhand-smoke-runtime.md`。

### Garrison 与共享 Block 保留 / 精确双选手牌保留（已完成 Unity 原生验证）

- `Garrison`（3246）基础态为 2 Energy / Uncommon Skill / Self / DiscardPile；要求从来源以外的当前手牌精确选择 2 个不同实例，成功后获得 12 Block 与 2 层 Garrison。候选足够但选择缺失、数量错误、重复或漂移都在首次写入前失败。
- UI 会话在收齐两张候选前只更新选择，不提交命令；来源左键或右键取消。成功选择只让这两张牌跳过当前一次 `EndPlayerAction` 的弃牌，授权随后消费，下一次行动结束恢复普通全手弃置。
- Garrison 复用公共 `BattleBlockRetention` 的计时授权：两个 PlayerRoundStart 分别产生 `2→1`、`1→0` 且均保留 Block，第三次才清除；手牌授权仍由职业 / Turn 卡区适配器拥有，公共模块不接触卡牌。
- 正式 Marine 为 **80/2**（V1 **62/2**、V2 **18/0**），全项目 **94/74**。生成前 3/3、最终定向 300/300、完整 EditMode 798/798 与真实 AB 均通过；升级 15 Block / 选 3 张仍只是 metadata，详见 `../06_testing/2026-08-14-shared-block-retention-barricade-garrison-runtime.md`。

### Opportunistic Strike 与共享触发出牌（已完成 Unity 原生验证）

- `Opportunistic Strike`（3243）基础态在上一张成功牌为 Attack / Shoot 后，从当前 Hand 的 Attack 候选中以确定性随机选择一张，并通过 Queue-owned system-token continuation 免费执行完整出牌；没有合法候选或前置牌型不符时不触发。
- 触发牌继续走原目标、Effect、职业程序与普通归宿；continuation 只在前一命令成功提交后串行消费，不递归直调 Turn。随机候选、费用 0 与命令快照在首写前冻结，失败不伪造成功触发。
- 3243 已翻为 `Implemented`，Marine **81/1**（V1 **63/1**、V2 **18/0**），全项目 **96/72**；定向 8/8、完整 EditMode 802/802 与真实 AB 通过。升级改为选择攻击手牌仍只是 metadata，详见 `../06_testing/2026-08-14-shared-triggered-play-havoc-opportunistic-strike-runtime.md`。

### Unstoppable 与共享 settlement-derived trigger（已完成 Unity 原生验证）

- `Unstoppable / 势不可挡`（3250）基础态冻结为 1 Energy / Rare Power / Self / PowerPile / Program 50 / 空 Effect bindings / 非 Innate。基础态与升级元数据都保持 1 Energy；升级追加“施加 debuff 时触发”仍只是 metadata，当前运行时只实现基础致死 / 破 Block 触发。
- 成功打出 Power 时，职业适配器通过共享引擎以 Prepare / Validate / Commit 注册 `FatalOrBlockBroken -> RandomCardPlay`。匹配条件是 owner 造成的 `BattleDamageAppliedSettlement` 为致死，或 `BlockBefore > 0 && BlockAfter == 0`；普通未致死 HP 伤害不触发。
- 职业侧按静态表顺序冻结 `Implemented` / Attack / 非 Shoot / 可由共享目标策略自动解析的候选模板；共享引擎以独占确定性随机流选一张，创建唯一临时手牌实例，然后由 Queue 在父表现屏障后以 `Waived` 费用完整出牌并强制 Exhaust。当前 registration ID 在自己派生链中被抑制，防止同一 Unstoppable 自递归。
- 本切片不新增 HUD / Prefab / Scene，也不扩大为任意 Power event bus、升级实例、Deck / 奖励 / Run / 多人。正式生成后 Marine **82/0**（V1 **64/0**、V2 **18/0**）、全项目 **98/70**、Effect **19**。Luban 通过；首次 Sync 因 Juggernaut i18n 缺少 `{triggerDamage}` 被正确拒绝，单点修复后 Localization / Sync / Addressables 成功。定向 `054b6bcd5d734f729a2f1f95c4e7a80d` 7/7，完整 EditMode `d156b8e2537546ef9e83da0ef5dadd2a` 807/807；哈希、BuildLayout 与耗时见 `../06_testing/2026-08-14-shared-settlement-trigger-juggernaut-unstoppable-runtime.md`。

## 25. 每批验收与回滚

每个切片按以下顺序闭环：作者表/本地化差异检查 → Luban → 导入本地化 → `Sync and Build All` → 定向与完整 Unity EditMode → 更新状态、决策、需求、计划和测试记录。回滚单位是该切片的程序、表项、生成数据、测试和文档；不清理用户已有工作区改动。
