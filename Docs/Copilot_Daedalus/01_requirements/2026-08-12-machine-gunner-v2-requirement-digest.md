---
title: Marine Game 机枪兵 V2 卡牌需求摘要
page_type: requirement
lifecycle: active
created: 2026-08-12
updated: 2026-08-14
status: unstoppable-base-verified-unity-native-2026-08-14
status_source: ../SESSION_LOG.md
source:
  - ../00_inbox/README web.md
related_plan: ../plans/2026-08-12-machine-gunner-v2-card-runtime-plan.md
supersedes: 2026-08-07-marine-game-card-requirement-digest.md
confidence: source-stated-and-code-observed
---

# Marine Game 机枪兵 V2 卡牌需求摘要

> 本页是 2026-08-12 后的日常实施摘要。行为规则以 `00_inbox/README web.md` 为唯一当前原始需求；同目录的 `READ.md` 与 `HANDOFF.md` 只保留历史线索，不能覆盖本页。

## 1. 范围与来源裁决

- 用户授权的范围仍限于 Hero 1002 的卡牌、单场战斗内必要规则、作者表、生成配置、本地化与验证。
- 不导入地图、敌人、意图、奖励三选一、篝火、事件、跨战斗生命/牌组、Run、Scene、Prefab、角色选择或通用 UI 流程；V2V 只在既有 Hand UI 内完成卡牌规则必需的单选交互。
- 新 README 目标目录为 5 个初始模板、76 个奖励模板和 1 个临时模板，共 82 个模板。作者表现已具备全部 82 个身份；V2 新增的 18 个奖励模板必须维持稳定目录映射和构建门禁，只有已具备独立程序和回归的精确身份才可翻为 `Implemented`。
- `README web.md` 中的升级文案继续是数据/策划事实；当前 `CardInstanceData` 没有升级态，除非某个独立切片先提供升级实例，运行时只实现基础态。
- README 中“战地手术可选友方”等多人目标描述超出现有单玩家 Session 和 TargetRule；当前不得用 Self 偷换为 AnyAlly，也不得伪造多人支持。

## 2. 当前伤害与词条口径

每段伤害由两个强类型输入声明：`MachineGunnerDamageKind` 表示结算语义，`MachineGunnerCardTag` 表示来源分类。卡牌程序不得自行传递力量、烟雾、易伤或开火的行为布尔值。

| 维度 | 当前口径 |
|---|---|
| `Attack` | Weakness → 攻受双方 Smoke → Vulnerable → Block → HP；可受 ArmorBreak；普通射击再读取 FirePower。 |
| `Support` | 只读取目标 Smoke、Vulnerable 与 ArmorBreak，不读取来源 Strength、Weakness 或来源 Smoke。 |
| `Bomb` | 只读取目标 Smoke 和 Block；不读取 Vulnerable、ArmorBreak、Strength、Weakness 或来源 Smoke。 |
| `Burn` | 不读取 Smoke；经 Block；ArmorBreak 附加值只受 Vulnerable 放大。 |
| `Shoot` tag | 读取 Stim、IncendiaryAmmo 与 FirePower。 |
| `Sniper` tag | 不读取 Stim 或 FirePower；读取 IncendiaryAmmo；免来源 Smoke，目标 Vulnerable 或玩家 Invisible 时按狙击倍率。 |
| `Shoot | Sniper` | 同时享有射击与狙击规则，供 SpikeShot 使用；仍可声明成功后保留 Invisible。 |

`Shotgun` 属于 `IsShootCategory`，因此未来若出现即时卡牌伤害段会进入便携帮手钩子；当前没有 Shotgun 卡实例，只有结构契约而没有直接运行验收，也不提前推断其与 Stim/燃烧弹药的未来联动。Debuff 伤害继续不走攻击修正。

## 3. V2A 已验证的变更

- `ElectroBoost` (3236) 已按新需求改为 1 Energy / Uncommon / Power / Hand→PowerPile，基础态施加可叠加、整场战斗持续的 `FirePower +3`；不再在玩家行动结束清除。
- `DefenseTarget` (3262) 已开放基础态：2 Energy、Self、Hand→ExhaustPile、最少消耗 2 弹且最多 9 弹、每实际 3 弹获得 1 层 `Intangible`。2 弹成功但得到 0 层是“每 3 弹”公式的显式实现约定，且不会生成 `0→0` 状态记录。
- `Intangible` 目前只拦截正值 incoming `Attack`：在 Block 前把本段攻击值封顶为 1，再消费一层；不随回合衰减。Buffer 与 Intangible 同时存在时，Buffer 优先且完全抵挡后不消耗 Intangible。这是来源未规定组合优先级时的受控实现决定。
- `SpikeShot` (3248) 已显式声明 `Shoot | Sniper`：吃 Stim、燃烧弹药、FirePower 与狙击易伤/隐身倍率，但不破 Invisible，也不吃来源 Smoke。

V2B 录入完成时，新增 18 张只具有身份、元数据与本地化，全部仍为 `CatalogOnly`。V2C 先打开其中两张来源完整的基础态卡，V2D 开放 V1 身份 3223，V2E 再开放 6 张 V1 延迟卡与 V2 身份 3274，V2F 开放 3269、3272、3280 三张 V2 即时卡，V2G 开放 3268 私人改装基础态，V2H 开放 3276 焚风基础态，V2I 开放 3282 充能爆射基础态，V2J 开放 V1 的 3213 与 V2 的 3271，V2K 开放 3267 便携帮手，V2L 开放 3265 狂轰滥炸，V2M 开放 3266 天空之怒，V2N 开放 V1 的 3260 极限过载，V2O 开放 3275 与通用 Innate，V2P 开放临时卡 3263，V2Q 开放 3261 与生产临时卡协议，V2R 开放 3278 霸凌基础态，V2S 开放 3277 先发制人基础态，V2T 开放 V1 的 3234 战术推进与下一张成功 Attack 的二元费用豁免，V2U 开放 V1 的 3257 不解释12连与两波换弹资源计划。当前已验证目录为 **75 张 `Implemented` / 7 张 `CatalogOnly`**，V1 为 59/5、V2 为 16/2。这些切片仍不表示奖励、Run、默认 Deck、UI、多人或升级已经可玩。

## 4. V2B 已验证的目录扩展

- 新增 3265–3282、Program 65–82 的 18 个奖励模板，使用独立快照键 `marine-game-v2-20260812-cards`，均带 `art_placeholder`、空 effect bindings 与升级元数据。其实现状态由 V2 扩展快照精确冻结，而不是以“所有新卡永远 CatalogOnly”这一过渡规则表达。
- V1 的 64 模板快照仍独立存在。构建验证同时检查 V1 与 V2 扩展，防止 Program、状态、插图或绑定漂移；V2C 验收时的其余 14 张 `CatalogOnly` 由运行时在程序查询前拒绝。
- V2B 已执行 Luban、本地化导入、`Sync and Build All` 与本地 Addressables 构建。Unity MCP 定向 EditMode 为 112/112，完整 EditMode 为 586/586；详情见 `../06_testing/2026-08-12-machine-gunner-v2b-catalog-extension.md`。

## 5. V2C 已验证的破甲即时卡

- `ThermiteBomb` (3273)：基础态为 1 Energy / Skill / AllEnemies / Hand→DiscardPile；对每名存活敌人先施加 `Burn +4`，再施加 `ArmorBreak +2`。Burn 使用既有 Oil 交互；ArmorBreak 不随回合衰减，并使后续 Attack、Support 与 Burn 的附加值走既有伤害公式。
- `Crush` (3281)：基础态为 1 Energy / Attack / 自动最近敌人 / Hand→DiscardPile；先进行 9 点普通 Attack，只有目标在该段后仍存活才施加 `ArmorBreak +4`。
- 当前无升级实例，因此 3273 的 6 Burn / 3 ArmorBreak 和 3281 的 12 Attack / 5 ArmorBreak 仅保留作者表元数据。两张都未加入默认 Deck、奖励或 Run。该切片已完成 Luban、本地化导入、Sync/Addressables、定向 81/81 与完整 589/589 EditMode；详见 `../06_testing/2026-08-12-machine-gunner-v2c-armor-break-instant-cards.md`。

## 6. V2D 已验证的击退射击与失去力量

- `KnockbackShot` (3223)：基础态支付 1 Ammo，施放时按 Encounter 顺序快照前两名存活敌人。最近目标先承受 7 点 Attack，存活时获得 `LoseStrength +2`；第二近目标再承受 3 点 Attack，存活时获得 `LoseStrength +2`。只有一名敌人时第二段跳过，首段击杀不递补；显式 `TargetId` 被拒绝。该卡没有 `Shoot` / `Sniper` 标签，因此不触发 Stim、FirePower 或 IncendiaryAmmo 的射击联动。
- `LoseStrength` 是职业私有非负状态，和 Weakness、Vulnerable、永久 Strength 分开存储。Attack 的来源力量项为 `max(0, baseDamage + Strength - LoseStrength)`，Burn 不读取该状态。携带者在自己的行动结束时清零并产生状态 settlement；敌人的清除位于本次 action Effect / completion 之后、intent advance 之前，玩家的清除位于回合末 Burn 之前。
- 当前没有升级实例，3223 的升级 9/5 Attack 与 `LoseStrength +3` 只保留作者表元数据；该卡未加入默认 Deck、奖励或 Run。本切片完成后目录为 49/33，Luban、本地化导入、Sync/Addressables、定向 10/10 与完整 597/597 EditMode 已通过；详见 `../06_testing/2026-08-12-machine-gunner-v2d-knockback-lost-strength-runtime.md`。

## 7. V2E 已验证的延迟效果与支援链

本切片只实现以下 7 张卡的基础态；升级列继续只是作者表元数据：

| 卡牌 | 已冻结的基础态行为 |
|---|---|
| `GuidedNuke` (3237) | 5 Energy、Self、Hand→DiscardPile；立即施加 Shackle +1。施放回合末把独立倒计时从 4 推进为 3，在第三个未来回合末对所有当前存活敌人造成 Bomb 99。Shackle 只阻止 Attack，Skill 仍可使用，并在玩家当前行动结束清除。 |
| `BansheeStrike` (3238) | 2 Energy；在后续两个玩家回合开始分别锁定当时最近的存活敌人并执行 Support 8×2。同一次触发内首段致死不会将第二段重定向到另一敌人。 |
| `FireSupport` (3239) | 1 Energy；下回合开始进行 5 次独立随机 Support 2，每段从当前投影存活敌人重新选择。 |
| `FireBombardment` (3240) | 2 Energy；下回合开始按两波处理当前存活敌人，每个目标依次执行 `Support 2 → 若存活则 Burn +4（沿用已有 Oil 交互）→ Oil +3`。 |
| `FiveHundredPounder` (3241) | 3 Energy；施放回合末把独立倒计时从 3 推进为 2，在第二个未来回合末对所有当前存活敌人造成 Bomb 60。 |
| `TripleStrike` (3264) | 4 Energy + 3 Ammo、显式 Enemy、Hand→ExhaustPile；先 Invisible +2，再执行 Sniper 12×2，并保留 Invisible。下回合开始对当前最远存活敌人执行 Support 20。 |
| `NeedleStorm` (3274) | 1 Energy；下回合开始进行 4 次独立随机 Delayed 1，每段之后仅在该目标仍存活时施加 ArmorBreak +1。 |

来源没有完整规定实现细节的部分已按用户授权的“脑补”冻结为当前项目口径，而不是伪称原文逐字声明：

- Support 读取目标 Smoke、Vulnerable 与 ArmorBreak，不读取来源 Strength、Weakness、Smoke 或其他来源修正；Bomb 只读取目标 Smoke，不读取 Vulnerable、ArmorBreak 或来源修正；钢针 Delayed 只读取目标 Smoke。
- 同阶段多实例按创建顺序处理；每个随机段从当时投影的存活敌人重新取候选。只有整个阶段计划成功才提交随机状态。终局发生后跳过剩余工作，遗留延迟实例随 BattleEnded 清空。
- round-start 延迟阶段位于最后一名敌人行动之后、敌方 Smoke 清理、玩家资源补充与抽牌之前。V2E 发布时的历史 round-end 顺序为弃牌 → 清 Shackle/LoseStrength/既有临时状态 → Bomb → Burn；后续 Field Surgery 已补上恢复，当前权威顺序为弃牌/既有清理 → Shackle → LoseStrength → Heal → Regeneration -1 → Bomb → Burn。
- round-start 阶段内部使用同一投影计划准备、校验与提交，但最后一名敌人的行动事务已在此前提交。当前没有“敌人行动 + round-start 延迟阶段”的跨事务回滚；正常 Queue 串行路径已有原生测试证据，异常提交故障下的完整跨域原子性仍是已知边界。

Luban、本地化导入、`Sync and Build All` 与 Addressables 构建均成功（14.252 秒）；定向 EditMode `586264ec18e549d89d1a063aac4d7b93` 为 101/101，完整 EditMode `89cfdfe8441b45d39d0cd57d939734c7` 为 606/606（46.847 秒）。详情见 `../06_testing/2026-08-12-machine-gunner-v2e-delayed-support-scheduler-runtime.md`。

## 8. V2F 已验证的烟雾、防御与标记即时卡

| 卡牌 | 已冻结的基础态行为 |
|---|---|
| `ChainSmoke` (3269) | 1 Energy、Skill、Self、Hand→DiscardPile；只为施放者增加 `Smoke +5`。名称或显示文案不参与程序判断，因此没有抽牌行为。 |
| `EmergencyCooling` (3272) | 1 Energy、Skill、Self、Hand→DiscardPile；严格先获得 8 Block，再为施放者增加 `Smoke +3`。 |
| `Mark` (3280) | 0 Energy、Attack、显式 Enemy、Hand→DiscardPile；支付 1 Ammo，先造成 5 点普通 Attack，目标仍存活时才增加 `ArmorBreak +2`。该卡显式为 `Tags.None`，不吃 Stim、FirePower 或 IncendiaryAmmo。 |

三张基础态都复用既有即时操作、资源门禁和后置存活检查，没有新增 Draw、状态、伤害种类或跨卡协议。Luban、本地化导入和 `Sync and Build All` 均成功，Addressables 构建耗时 11.414 秒；定向 EditMode `054f72bc921749b5bad6d2efcc358b73` 为 83/83，完整 EditMode `ba418ab34a6d44038dddddc0233a03f8` 为 611/611（18.618 秒）。详情见 `../06_testing/2026-08-12-machine-gunner-v2f-smoke-block-mark-runtime.md`。

## 9. V2G 已验证的私人改装基础态

- `PrivateMod` (3268)：基础态为 1 Energy / Uncommon / Power / Self / Hand→PowerPile；成功后 AmmoMaximum +1，但当前 Ammo 不变，同时增加 `FirePower +1` 与一层 PrivateMod Power。
- FirePower 继续由既有 Shoot 规则逐段读取，没有新增命中后钩子；后续装填按提高后的 AmmoMaximum 补充。能量不足时资源、状态、Power 层数、随机流与卡区均零写入。
- 当前没有升级实例，因此升级列只保留作者表元数据；3268 未加入默认 Deck、奖励或 Run。Luban、本地化导入与最终 `Sync and Build All` 均成功，最终 Addressables 构建为 4.376 秒（首轮 11.092 秒后重新导入再构建）；定向 EditMode `cfcf49e9a16e447fb4033af6108c8dd9` 为 85/85，完整 EditMode `0f17edd2f31d40d2aba328def4448f3c` 为 613/613（18.617 秒）。详情见 `../06_testing/2026-08-12-machine-gunner-v2g-private-mod-runtime.md`。

## 10. V2H 已验证的焚风基础态

- `FoehnWind` (3276)：基础态为 2 Energy / Skill / 显式 Enemy / Hand→DiscardPile；成功结算时读取施放者当前全部 Smoke。Smoke 大于 0 时，以该值作为基础 Burn 施加给目标，目标旧 Oil 继续增加本次 Burn 并按既有规则减半，随后来源 Smoke 清零。
- 专用复合操作联合预演来源 Smoke、目标 Burn 与目标 Oil，并按目标 Burn → 可选目标 Oil → 来源 Smoke 的 settlement 顺序提交。Smoke 为 0 时仍合法支付 2 Energy 并弃牌，但不制造私有状态写入；能量不足或目标规则错误时资源、状态、随机流与卡区均零写入。
- 当前没有升级实例，因此升级列只保留作者表元数据；3276 未加入默认 Deck、奖励或 Run。Luban、本地化导入与 `Sync and Build All` 均成功，Addressables 构建为 12.164 秒；定向 EditMode `69b8ded02aaa46368cad35e620567fd2` 为 89/89，完整 EditMode `4a90229794d24d3f8fd85154ab79c250` 为 617/617（17.657 秒）。详情见 `../06_testing/2026-08-12-machine-gunner-v2h-foehn-wind-runtime.md`。

## 11. V2I 已验证的充能爆射基础态

- `ChargedBurst` (3282)：基础态为 2 Energy / Attack / AllEnemies / Hand→DiscardPile，不消耗 Ammo；施放时按 Encounter 顺序快照全部存活敌人，第 `n` 名目标的基础伤害为 `12 + 6 × (n - 1)`，前三段为 12/18/24。前段致死不会重排后续目标的快照序号；显式 `TargetId` 被拒绝。
- 每段都只有 `Sniper` 标签：不读取 Stim 的额外段或 FirePower，读取 IncendiaryAmmo，并在成功攻击后保留来源 Invisible。能量不足或目标规则错误时参与者、资源、状态、随机流、卡区与表现结果零写入。
- 当前没有升级实例，因此升级列只保留作者表元数据；3282 未加入默认 Deck、奖励或 Run。Luban、本地化导入与 `Sync and Build All` 均成功，Addressables 构建为 11.456 秒；定向 EditMode `1d5c9e1d96fe4ebcadd990fcc73fccdc` 为 94/94，完整 EditMode `822d066bc54c43d78ac206072789f840` 为 622/622（18.193 秒）。详情见 `../06_testing/2026-08-12-machine-gunner-v2i-charged-burst-runtime.md`。

## 12. V2J 已验证的回合能量修正基础态

- `Overload` (3213)：基础态为 0 Energy / Skill / Self / Hand→DiscardPile；成功时即时获得 2 Energy，但不得超过 EnergyMaximum，并累计一层下一回合能量补给 Penalty。主动获得使用 `BattleEnergyGainedSettlement`，只记录上限裁剪后的实际变化量。
- `DefensiveStance` (3271)：基础态为 1 Energy / Skill / Self / Hand→DiscardPile；在同一出牌事务中先获得 8 Block，再累计一层下一回合能量补给 Bonus。能量不足或显式目标错误时，资源、Block、私有状态、卡区、随机流与表现结果均零写入。
- Bonus 与 Penalty 独立、非负且分别叠加。下一玩家回合开始的有效补给为 `max(0, baseGain + bonus - penalty)`，之后受 EnergyMaximum 裁剪；该补给使用 `BattleEnergyRefilledSettlement`，两项一次性状态随后分别清零。作者表只翻转 3213 与 3271，当前目录为 64/18、V1 为 54/10、V2 为 10/8。
- `LimitOverload` (3260) 继续为 `CatalogOnly`。它要求“抽到手牌满”，但当前卡在程序操作提交后才离开 Hand；若直接调用 `DrawCards(10)`，容量计算会包含 3260 自身并少抽一张。后续必须先提供以成功归宿后的投影 Hand 为输入的专用“抽至满手”卡区预演/提交 seam，不得用固定抽牌数、提前移牌或额外补抽代替。
- Luban、本地化导入与 `Sync and Build All` 均成功，Addressables 构建为 11.72 秒；补强后的定向 EditMode `3e73f867e7404be8a3180660e4999d20` 为 136/136，完整 EditMode `56274033527e4c78b50a78313bcc0f6c` 为 631/631（17.642 秒）。详情见 `../06_testing/2026-08-12-machine-gunner-v2j-round-energy-runtime.md`。

## 13. V2K 已验证的便携帮手基础态

- `PortableHelper` (3267)：基础态为 1 Energy / Rare / Power / Self / Hand→PowerPile；成功后获得一层可叠加、整场不衰减的便携帮手 Power。升级为 0 Energy 仍仅是作者表元数据。
- 每一段来自卡牌程序的即时 `IsShootCategory` 实际伤害，先完成来源 Damage、卡牌声明的 post-hit 和既有全局命中钩子；若同一目标仍存活，再按帮手层数逐段追加基础 1 点帮手伤害。来源段致死不触发，较早帮手致死后停止剩余帮手；不会寻找新目标或递归触发。
- 帮手伤害只读取来源 `FirePower`、目标 `Vulnerable` 与 `ArmorBreak`，并正常经过 Block/HP；不读取 Strength、Weakness、来源/目标 Smoke、目标 Invisible 或狙击倍率。帮手段没有 Shoot/Sniper/Shotgun 标签，所以不会自行触发 Stim、IncendiaryAmmo、AgedOil、KungfuMech、Ammo、Invisible 生命周期或再次帮手。
- IncendiaryAmmo 的直接回归锁定了 `来源 Damage → Burn → 帮手 Damage`，证明来源射击的既有后置效果先于帮手。Shotgun 当前无卡实例，只由 `IsShootCategory` 结构覆盖；延迟 Support、Bomb、Needle 与 TripleStrike 延迟段不进入即时卡牌命中入口。后两项属于代码结构证据，不作为实际跨模块运行验收。
- 作者表只翻转 3267，当前目录为 65/17、V1 为 54/10、V2 为 11/7。Luban、本地化导入、`Sync and Build All` 与 Addressables 构建均成功（12.163 秒）；定向 EditMode `95707f1918fa4633b671c6a10f9b0da3` 为 120/120，完整 EditMode `8c0ce8f925e94a35b893f5b5892ef447` 为 639/639（131.4561842 秒）。详情见 `../06_testing/2026-08-12-machine-gunner-v2k-portable-helper-runtime.md`。

## 14. V2L 已实现并完成定向验证的狂轰滥炸基础态

- `Bombard` (3265)：基础态为 1 Energy / Uncommon / Power / Self / Hand→PowerPile；每次成功施放增加 4 层可叠加、整场不衰减的狂轰滥炸 Power，自身不产生即时伤害或状态。升级列仍只是作者表元数据。
- 只有 `BansheeStrike`、`FireSupport`、`FireBombardment` 与 `TripleStrike` 四种 scheduled Support 在实际触发时读取当前层数；既有延迟实例不快照施放时层数。每层把声明载荷提高 10%，正值使用 `floor((baseValue × (100 + 10 × stacks) + 50) / 100)` half-up。来源没有规定小数取整方式，该口径是用户授权“脑补”后冻结的实现决定。
- 女妖打击、火力支援与三连击只缩放延迟 Support 伤害；燃烧轰炸分别缩放 Support Damage、Burn 与 Oil。缩放后的伤害继续进入目标 Smoke、Vulnerable、ArmorBreak 的既有 Support 管线；Burn/Oil 继续遵守原 Oil 交互、目标存活门禁和 `Damage → Burn → Oil` 顺序。
- GuidedNuke / FiveHundredPounder 的 Bomb、NeedleStorm 的 Delayed、回合末 Burn、即时攻击、便携帮手及其他未声明来源不受增幅；命中数、波次数、倒计时、目标选择与调度生命周期也不变化。本切片没有增加通用伤害事件或修改 Support 伤害档案。
- 作者表只翻转 Q155（3265），当前目录为 66/16、V1 为 54/10、V2 为 12/6。Luban、本地化导入与 `Sync and Build All` 均成功，Addressables 构建耗时 12.963 秒；定向任务 `9c21aa7c79b94f1980988945d35636dd` 为 134/134（1.4521749 秒），精确素材真实加载任务 `da1d1e3969014e81b06cb57a2392de13` 为 1/1（106.8572486 秒）。两次完整任务与素材配置整类任务均只在相同冷加载用例保留 180 秒 timeout，故本切片按组合门禁验收，不声称完整套件单任务全绿。详情见 `../06_testing/2026-08-12-machine-gunner-v2l-bombard-runtime.md`。

## 15. V2M 已验证的天空之怒基础态

- `SkyWrath` (3266)：基础态为 1 Energy / Rare / Power / Self / Hand→PowerPile；成功后增加 1 层整场不衰减、可叠加的天空之怒 Power，卡本身不造成即时伤害或随机推进。升级列仍只是作者表元数据。
- “每次支援”按当前 README 的四类原始 Support 逻辑段冻结：BansheeStrike 每 hit、FireSupport 每 hit、FireBombardment 每 wave、TripleStrike 延迟 Support 一次。燃烧轰炸先完成一波全部目标的 `Damage → 存活后 Burn → Oil`，再触发该波天空之怒。旧 HANDOFF 把 NeedleStorm 纳入触发的描述被当前 README 的支援分类覆盖；Needle Delayed、Bomb、回合末 Burn、即时攻击、PortableHelper 和天空之怒自身均不触发。
- 每一层分别从当时投影中的存活敌人随机选择主目标，单候选也推进一次随机流；先对主目标造成基础 8 点 Support，再按该层开始时快照的 Encounter 顺序对其余目标各造成基础 4 点 Support。下一层重取候选，因此前层致死会影响后层；无存活敌人时停止且不推进随机流。
- 天空之怒的基础 8/4 先受当前 Bombard 层数按既有正值 half-up 规则缩放，再进入目标 Smoke、Vulnerable 与 ArmorBreak 的 Support 管线；4 层 Bombard 对应 11/6。触发仍在同一 scheduled-effect 联合计划中完成，没有新增全局事件、通用伤害监听或第二条共享写入路径。
- 作者表只翻转 Q156（3266），当前目录为 67/15、V1 为 54/10、V2 为 13/5。Luban、本地化导入与 `Sync and Build All` 均成功，Addressables 构建耗时 12.956 秒；翻表前运行时任务 `eefded85c7aa4a099d3b16ee4577e704` 为 117/117，正式定向任务 `3a279411d63749abaf8eca64ec4236cc` 为 139/139，完整任务 `a46a25a9da924131965130d6e2b07b8b` 为 650/650（174.2163423 秒）。详情见 `../06_testing/2026-08-12-machine-gunner-v2m-sky-wrath-runtime.md`。
- 开发中的两轮红测来自测试前提而非生产回归：首版场景总费用 6 超过 fixture 的 5 Energy 上限，后改用 4 Energy 的 Banshee 场景；随后随机 oracle 把 raw state 误当构造 seed，改为与生产一致的初始化后赋 State。两次均没有修改生产行为。

## 16. V2N 已验证的极限过载基础态

- `LimitOverload` (3260)：基础态为 0 Energy / Rare / Skill / Self / Hand→DiscardPile。成功时先产生 0 费 `BattleEnergySpentSettlement`，再获得 1 Energy；获能受 EnergyMaximum 裁剪，已在上限时不伪造 `BattleEnergyGainedSettlement`。
- 随后的“抽至满手”以当前牌成功离手后的投影 Hand 计算 10 张上限。`BattleCardZonesData` 的 Prepare / Validate / Commit 准备阶段零写入地冻结原布局、洗牌随机前/后状态、最终布局和 settlement；校验拒绝所属、布局、随机或一次性漂移；提交不再随机，只发布一次最终 `Layout`，不暴露 11 张手牌的中间状态。
- 联合 settlement 顺序为 `EnergySpent(0) → 可选 EnergyGained(1) → 当前牌 Hand→DiscardPile → 旧 DiscardPile→DrawPile/重洗/抽牌 → NextRoundEnergyGainPenalty +3`。同次重洗只使用解算前的旧弃牌，3260 在抽牌计算后才进入弃牌堆，不会在同次解算中自抽。
- Penalty 每张累计 3 层。下一玩家回合开始继续使用 V2J 的 `max(0, baseGain + bonus - penalty)`，再按 EnergyMaximum 裁剪，之后清除一次性 Bonus/Penalty。3260 不是 Attack/Shoot，不使用 Ammo、Stim、IncendiaryAmmo 或 PortableHelper 入口。
- 作者表只翻转 Q150（3260），当前目录为 68/14、V1 为 55/9、V2 为 13/5。Luban、本地化导入/校验、`Sync and Build All` 均成功，Addressables 构建耗时 15.828 秒；正式定向 Unity MCP 任务 `feda36c5daef4fffab34065ba5988686` 为 169/169（0 failed/skipped，2.2836982 秒），完整任务 `a84b5bb4f7dd4ca1b9791c81bb930973` 为 659/659（0 failed/skipped，282.0044831 秒）。CardArt 与 Character Prefab 的 Addressables 冷加载较慢但均通过。详情见 `../06_testing/2026-08-12-machine-gunner-v2n-limit-overload-runtime.md`。
- 升级“+2 能量”仍只是作者表元数据；本切片没有新增升级 `CardInstance`，也没有把 3260 加入默认 Deck、奖励或 Run，未修改 UI 或多人流程。

## 17. V2O 隐秘行动与固有首次起手冻结口径

- `StealthAction` (3275) 基础态为 1 Energy / Uncommon / Skill / Self / Hand→DiscardPile，具有 Innate。成功时先获得 `Invisible +1`，再按普通抽牌规则抽 1；升级为 Invisible +2 / Draw 2 仍只是元数据，不得在没有升级实例时作用于基础态。
- “固有”是卡牌静态内容事实，作者表以强类型 `is_innate` 表达，当前只允许 3275 为 true；运行时不得从名称、描述、卡牌 ID 或 ProgramId 推断。默认 Deck 并未因本切片增加 3275，但任意实际 Deck 只要含固有模板，其对应实例就必须进入首次起手。
- 首次起手以既有洗牌结果为基础，固有实例按 DrawPile 实际抽取顺序优先进入 Hand。固有数不超过默认起手 5 时，用普通牌补到 5；6～10 时全部固有入手且不补普通牌；超过 Hand 上限 10 时启战必须在首次可见写入前失败，不能静默丢弃固有。
- Turn 必须在 `StartBattle` 的任何状态、资源或布局写入前为全部存活玩家冻结起手，任一玩家固有配置无效时返回 typed failure 且所有玩家零写入。成功起手只发布一次最终卡区布局，不推进洗牌随机，移动 settlement 的顺序连续；Innate 不在后续玩家回合重复生效。没有固有牌时，原起手数量和确定性抽牌结果必须保持不变。
- 3275 的 Draw 是现有普通出牌语义：抽牌时当前卡仍在 Hand，之后才进入 DiscardPile。因此 Hand=10 时抽 0 并在离手后成为 9，不得为凑回 10 而复用 V2N 的离手后抽牌 seam。
- Luban 后目录为 69/13、V1 为 55/9、V2 为 14/4；本地化与 Sync/Addressables 已通过（18.363 秒），正式目录快照 21/21、最终聚合定向 237/237 与完整 EditMode 673/673 均通过。

## 18. V2P 已验证的机枪扫射临时卡基础态

- `MachinegunBurst` (3263) 是来源声明只能由 `FixedMachinegun` (3261) 创建的临时 Attack：0 Energy、RandomEnemy、两段基础 5 点伤害、Exhaust、无升级。每段都从当时仍存活的敌人重新随机选择；首段击杀后第二段必须看到更新后的候选。
- 实际 Ammo 消耗为 0，不改变当前 Ammo，也不生成弹耗 settlement；来源另行规定游击战术把它视为消耗 2 Ammo，因此只有游击结算读取名义值 2。两层 Guerrilla 对应 4 Block，顺序位于两段伤害之后、当前卡离手之前。
- 来源没有为 3263 声明 Shoot 标签，当前项目冻结 `Tags.None`，不从“机枪扫射”名称推断 Shoot：Stim、IncendiaryAmmo、FirePower 与 PortableHelper 均不参与。它也显式排除 KungfuMech、AgedOil 与 `NonShootAttackRecent`，但仍是普通 Attack，继续读取该伤害类型既有的来源/目标修正、Block、HP 与致死规则。
- 正式作者表只翻转 3263，当前目录为 70/12、V1 为 56/8、V2 为 14/4；表 SHA-256 为 `B65D97253A43B2FF8575BCEE6F230B651EFD36FE84A10B7ACBFC0BCC62A0AB29`。Luban、本地化与 `Sync and Build All` 均通过，Addressables 为 11.757 秒；Unity MCP 最终定向 154/154、域重载后 CardArt 探针 1/1、完整 EditMode 678/678 均通过。详情见 `../06_testing/2026-08-12-machine-gunner-v2p-machinegun-burst-runtime.md`。
- 3261 仍为 `CatalogOnly` 且没有生产临时卡创建入口，所以当前只证明 3263 可通过直接运行时夹具执行；不得把 `Implemented` 解读为正常产品流程已经能生成、抽取或奖励该卡。奖励排除、默认 Deck、Run、升级、UI 与多人仍未实现。

## 19. V2Q 已验证的固定机枪与临时卡生产

- `FixedMachinegun` (3261) 基础态为 2 Energy / Rare / Skill / Self / Hand→ExhaustPile。成功时先获得 10 Block，再让来源卡进入 ExhaustPile；其余 Hand 按原顺序全部进入 DiscardPile，并按被弃旧手牌数量创建等量 `MachinegunBurst` (3263) 到 Hand。空余手牌场景创建 0 张；升级 15 Block 仍只是元数据。
- 整个替换由 CardZones 单一 Prepare / Validate / Commit 深计划拥有，冻结原布局、实例分配状态、来源归宿、旧手牌原序、新实例、最终布局与连续 settlement，成功时只发布一次 `Layout`。新实例用 `CardCreated` 表达，不得伪装为普通 Draw、DrawPile→Hand 或直接 UI 写牌。
- 表现层按权威 settlement 区分其余 Hand→Discard、来源 `HandToExhaust` 与新实例 `CreatedToHand`。3263 的动态模板依赖由职业 registry 声明，经 `Session.AvailableCardTemplateIds` 传递给 Hand 异步预载；这只保证运行时可显示创建牌，不把 3263 加入 Deck 或奖励池。
- 正式 `battle.card.xlsx` 只把 3261 翻为 `Implemented`，SHA-256 为 `02F549502D14214C98B4BA97212962B05E58A9B768EF1D7E4CAD441E1DCD6FB7`，`is_innate=false`。Luban 于 22:00:11 成功生成全项目 168 个 Card JSON；Marine 目录为 71/11、V1 57/7、V2 14/4，3261 精确为 status 0 / Program 61 / Exhaust / 非 Innate。Localization import/validate、Sync/Addressables（13.42 秒）均通过；force scripts 域重载后，最终聚合定向 `ba19d1744f084167927568f5572f91e6` 为 262/262（30.1698095 秒），完整 EditMode `dc6a1453b602487c8bfbbe7e42c3968d` 为 690/690（20.8279366 秒），均为 0 failed/skipped。
- Runtime 静态编译为 0 error / 6 warning，Editor 为 0 error / 12 warning。TDD 红测任务前缀 `404d20…` 锁定多弃牌 prelude 异常，`2045cc…` 锁定 `CardCreated` 结果 guard；修复后核心任务前缀 `d6db34…` 为 12/12、非表格定向任务前缀 `f415877…` 为 195/195。最终审查发现并修复动态 3263 插画预载 blocker；registry→`Session.AvailableCardTemplateIds`→Hand async preload 的动态精确任务前缀 `6bf4…` 为 2/2，最终任务也覆盖该链路。
- 本切片不实现升级实例或 15 Block，不修改默认 Deck、奖励排除、Run、多人、Scene 或 Prefab；也不把临时创建扩大为普通抽牌、保留或 Innate 协议。

## 20. V2R 已验证的霸凌基础态

- `Bully` (3278) 为 0 Energy / Uncommon / Attack / 显式 Enemy / Hand→DiscardPile；成功时先保留真实 `EnergySpent(0)`，再造成基础 6 点普通 Attack，然后按命令开始时目标活跃状态种类的冻结数量抽牌，最后弃置当前卡。升级 9 伤仍只是作者表元数据。
- 来源明确的是“目标每有一种状态抽 1 张”；状态集合和计数时点没有逐字定义。本项目冻结 Strength 非零、Vulnerable 正层及每一种正层数 `MachineGunnerCombatantStatus` 各计一种，同种多层只计一次；HP、Block、资源、PowerPile 实例、Stim 与延迟实例不计。伤害过程中消费状态、命中后新 Oil 或目标死亡仍使用命令起点的旧值。这部分属于受控“脑补”实现边界，不冒充 source-stated。
- 抽牌使用 CardZones 既有 `PrepareDraw` / `ValidatePreparedDraw` / `CommitPreparedDraw` 深事务，统一冻结 Hand 10 上限、DrawPile / DiscardPile、重洗随机、最终布局和移动 settlement。0 状态抽 0；满手不抽且不推进随机；目标错误或快照漂移保持资源、伤害、状态、随机流和卡区零写入。
- 正式表只翻转 Q168，SHA-256 为 `878812D99F68C8F9B9A7BC620E2794180F6E8A3F21B5252B16A12BDB70915499`，U168 `is_innate=false`。Luban 于 22:48:16 成功，目录为 72/10、V1 57/7、V2 15/3；Localization、Sync/Addressables（16.521 秒）、静态编译、正式聚合 209/209 与完整 EditMode 697/697 均通过。详情见 `../06_testing/2026-08-12-machine-gunner-v2r-bully-runtime.md`。

## 21. V2S 已验证的先发制人基础态

- `PreemptiveStrike` (3277) 为 0 Energy / 1 Ammo / Uncommon / Attack / 显式 Enemy / `Tags.None` / Hand→DiscardPile；成功时造成基础 8 点普通 Attack，Damage 与既有 post-hit 链结束后按命令开始时来源活跃状态种类的冻结数量抽牌，目标致死仍抽，最后弃置当前卡。升级 12 伤仍只是作者表元数据。
- 来源明确的是“自己每有一种状态抽 1 张”；状态集合和计数时点没有逐字定义。本项目冻结 Strength 非零、Vulnerable 正层及每一种正层数 `MachineGunnerCombatantStatus` 各计一种，同种多层只计一次；Power、Stim、scheduled effect、Block 与资源不计。V2S 发布时集合为 16 种；CD-104 追加 Regeneration 后当前为 17 种。Shackle 仍被上游 Attack 门禁在首写前拒绝，其余 16 种可进入成功计数；历史逐项证据与新增 Regeneration 回归共同锁定当前集合。
- 抽牌使用 CardZones 既有 `PrepareDraw` / `ValidatePreparedDraw` / `CommitPreparedDraw` 深事务，在首次战斗写入前冻结数量、Hand 上限、DrawPile / DiscardPile、重洗随机、最终布局和移动 settlement。Damage / post-hit 后只提交旧计划；目标错误、Ammo 不足、Shackle 或快照漂移保持资源、伤害、状态、随机流和卡区零写入。
- 正式表只翻转 Q167，SHA-256 为 `6C9120A317622F103F9A0DDEEEBB994B28F88230B679BA7E0B1D28201F8E2648`，U167 `is_innate=false`。Luban 于 23:26:12 成功，目录为 73/9、V1 57/7、V2 16/2；Localization、Sync/Addressables（13.966 秒、BuildLayout）、静态编译、最终 V2S 5/5、正式聚合 214/214 与完整 EditMode 702/702 均通过。TDD 与正式聚合首轮红项均为测试 oracle，生产未改；详情见 `../06_testing/2026-08-12-machine-gunner-v2s-preemptive-strike-runtime.md`。

## 22. V2T 已验证的战术推进与共享费用冻结

- `TacticalAdvance` (3234) 基础态为 2 Energy / Skill / Self / Hand→DiscardPile；成功时先获得 10 Block，再刷新一份“下一张成功 Attack 免费”的二元授权。重复施放不叠加，授权跨回合保留；Skill、Shackle、目标/费用/计划或卡区失败均不消费，第一张成功 Attack（含致死）完成成功归宿后才消费。Shackle 继续在费用解算前由既有 Attack 门禁拒绝。
- 授权使用职业运行时独立 bool + revision，不作为 `MachineGunnerCombatantStatus`；该授权没有增加 3277 / 3278 的状态种类计数。V2T 发布时私有状态为 16 种，后续 CD-104 只因新增 Regeneration 把当前集合扩为 17 种。当前行为来源、正式表与 i18n 的基础/升级 Block 为 10/14；历史 HANDOFF 的 12/16 已由当前 `README web.md` 覆盖。升级 14 仍只是元数据，没有升级实例。
- `BattleCardCostResolver` 为 Fixed / X 冻结实际支付、效果值与触发器名义值；通用普通 Fixed 和机枪兵是两个真实适配器，既有通用 X 未扩展。机枪兵独立冻结 Ammo actual / effect / nominal 与 Stim：Waived 只把实际 Energy / Ammo 归零，Fixed / UpToLimit 的 Stim 额外段保持并进入 Guerrilla 名义耗弹，AllAvailable 保留既有免费 Stim 段；ComboElbow 分类与生命周期不变。
- 正式表只翻转 Q124，SHA-256 为 `55D43141149D7A86D7957B1C43ED9303B9E9D091094E0CFAF2CF39FE2F73C569`。Luban 于 2026-08-13 01:44:15 成功；全项目 Card JSON 168 个，Marine 为 74/8、V1 58/6、V2 16/2，3234 为 status 0 / Program 34 / 空 bindings / 非 Innate。Localization、Sync/Addressables（端到端 16.852 秒、Addressables 14.762 秒、最新 BuildLayout）、静态编译、V2T 6/6、致死/溢出补强 1/1、Starter 142/142、正式快照 36/36、含真实 AB 的正式聚合 213/213 与完整 EditMode 721/721 均通过。详情见 `../06_testing/2026-08-13-machine-gunner-v2t-tactical-advance-runtime.md`。
- 本切片没有实现自动免费攻击、战士免攻策略、升级实例、默认 Deck、奖励、Run、UI 专属提示或多人；共享 resolver 只提供未来适配 seam。

## 23. V2U 已验证的不解释12连基础态

- `TwelveHits` (3257) 为 3 Energy / Rare / Attack / 自动最近敌人 / Hand→DiscardPile。普通支付先消耗当前 Ammo 最多射击 6 次，再无条件补满 Ammo，并从补满后的资源执行第二波最多 6 次；0 Ammo 也能出牌，第一波为 0 次后仍换弹。每段基础 5 点 Attack，目标死亡停止后续伤害且不改选目标，但已冻结的换弹与第二波资源支付仍生效。
- 机枪兵私有纯 resolver 在命令开始时冻结两波 effect / actual Ammo、波间补满、全卡唯一 Stim、Guerrilla nominal Ammo 与最终 Ammo；逐 hit 继续走既有 Damage、IncendiaryAmmo、PortableHelper 顺序。V2T 免费 Attack 授权下实际 Energy/Ammo 为 0，但两波、换弹与效果保留；Stim 激活时名义弹耗为 13，成功归宿后才消费授权，失败保持零写入。
- 正式表只翻转 Q147，SHA-256 为 `7131597FD5F3D948921F54926C0205E24E31F747D7C9B1206B78902AE6BEF818`；生成 JSON SHA-256 为 `28324422913241FC627F5C3A0BCF715332E4F2B3DCDFA94E4B6E4FF3ED7A6306`。Luban 于 03:00:27 成功；全项目 168 张，Marine 75/7、V1 59/5、V2 16/2。Localization、Sync/Addressables、六项逐片 TDD、Starter 148/148、正式快照 37/37、含真实 AB 的正式聚合 220/220 与完整 EditMode 728/728 均通过；详见 `../06_testing/2026-08-13-machine-gunner-v2u-twelve-hits-runtime.md`。
- 升级 2 Energy / 每段 6 伤仍只是作者表元数据；本切片不完成升级实例、默认 Deck、奖励、Run、UI、多人、自动免费攻击链或通用两阶段资源协议。

## 24. V2V 已验证的排气散热基础态与共享手牌单选

- `VentHeat` (3244) 为 0 Energy / Skill / Self / Hand→DiscardPile。来源之外存在合法手牌时必须精确选择一个当前实例；所选牌先 Hand→ExhaustPile，随后按上限实际获得 1 Energy，最后来源牌弃置。来源是唯一手牌时无需选择，直接弃置且不获得能量；能量已满时仍消耗所选牌，但不产生 `EnergyGained`。
- settlement 严格为 `EnergySpent(0) → selected HandToExhaust → EnergyGained(仅实际增加时) → source HandToDiscard`。`BattlePreparedHandCardSelectionResolution` 联合冻结两张牌及完整 Layout，并在 Commit 中只发布一次布局；空/多选、选中来源、跨 owner、重复提交或 Layout / Turn / Queue 漂移均在首次写入前失败，保持 Turn、资源、卡区和 settlement 零写入；已提交的非法命令仍保留 Queue typed failure lifecycle。
- `PlayCardCommand.SelectedCardIds`、`BattleHandCardSelectionRequest`、`BattlePreparedHandCardSelectionResolution` 与 UI 局部 `HandCardSelectionSession` 是可复用的普通手牌单选 seam。候选左键确认，来源左键或任意右键取消；选择期间禁拖并显示候选/非候选角色，事实漂移、禁用或销毁时清除。双 transient 不伪造 prelude，按所选牌 Exhaust、来源牌 Discard 的真实步骤依次清理。
- 正式表只翻转 Q134，SHA-256 为 `B3BA678FBC0C021F49C3F9FEDE4190099960EE109FFC302D96C77F29D54F4A6D`；i18n 只修改 B/C404-405，SHA-256 为 `8833E99F546B2C1195C4F0317A1B9208535ED083743F1ABF183874EFFFD23D77`。Luban 于 14:55:40 成功，生成 JSON SHA-256 为 `5988DA20801C8BF724EF0E471466A0A746A5E732DE3450BD7680F00A735F2615`；全项目 168 张为 85/83，Marine 76/6、V1 60/4、V2 16/2。Localization、Sync/Addressables（15.85 秒、BuildLayout）、静态编译、行为 15/15、目录 38/38、含真实 AB 的正式聚合 306/306 和完整 EditMode 744/744 均通过；详见 `../06_testing/2026-08-13-machine-gunner-v2v-vent-heat-runtime.md`。
- 该共享 seam 可以供未来 Ironclad `Burning Pact` 适配，但本切片没有实现、翻表或验证战士卡。3244 升级获得 2 Energy 仍仅为元数据；Deck、奖励、Run、多人、Scene、Prefab 与其他目录卡仍未实现。

## 25. 后续实施顺序

1. 普通手牌单选、通用封顶 Heal 与 concrete repeated-damage plan 都已有共享 seam；新消费者仍须提供自身规则、表状态、适配器和独立回归。自动免费攻击、保留、AnyAlly、任意多选、跨玩家选择、全体/链式重复伤害与升级实例仍需各自协议，不得复用其他受限入口偷换语义。
2. 每次只从当前剩余 4 张 `CatalogOnly` 中开放已具备程序、表状态和独立回归的精确身份，不因已有操作可复用而批量宣称可玩。

每个切片仍必须先实现运行时、再翻转精确卡表状态、执行 Luban、导入本地化、`Sync and Build All` 和 Unity 原生测试。

## 26. 当前验收事实

V2A 至 V2V、Field Surgery 与 Prismatic Shot 均已完成各自作者表/Luban、本地化、`TinySpire/Build/Sync and Build All`、本地 Addressables 与 Unity 原生门禁。当前 BuildLayout 为 `TinySpire/Library/com.unity.addressables/BuildReports/buildlayout_2026.08.13.21.31.49.json`（134612 bytes），GameData 物理 bundle 为 12182 bytes；双卡 repeated-damage 定向 `6932f72f288a477ca5869c21e3ac3996` 为 11/11、正式门禁 `908e5fb8b93e437d89533bb1b727231a` 为 53/53、回归修复代表集 `6ee679521f4c45d9a69b9984110c51bb` 为 5/5、最终行为聚合 `4ea4eff81b3c4ce786e318d0902c1ed4` 为 243/243，完整任务 `3e0a091d891e4f918668b99cb4a20157` 为 **776/776 passed**（77.7525946 秒）。全项目 168 张为 90/78，Marine 为 78/4、V1 61/3、V2 17/1；这不代表剩余目录卡、任何升级值、Deck、奖励、Run、多人、自动免费攻击、全体/链式重复伤害、Scene/Prefab 或升级实例完成。

## 27. Field Surgery 与 Regeneration（已验证）

- `Field Surgery`（3231）基础态为 1 Energy / Rare Skill / Self / ExhaustPile。Program 31 出牌时只施加 Regeneration 5 与 Shackle 1，不立即恢复生命；Shackle 溢出在 Regeneration、Energy、Health 和卡区首写前失败。
- 玩家行动结束按来源冻结的 `Shackle → LoseStrength → Heal → Regeneration -1 → Bomb → Burn` 顺序执行。缺 2 HP 时 Regeneration 5 实际恢复 2；满生命仍记录 requested 5 / actual 0 并减至 4。治疗 outcome、生命写入口、settlement 与正实际值表现复用普通 Heal；状态生命周期仍留在职业适配器。
- Field Surgery 发布时正式目录为 Marine 77/5、V1 61/3、V2 16/2；全项目 88/80。最终精确行为 9/9、正式目录 50/50、治疗视图 1/1、含真实 AB 聚合 243/243、完整 EditMode 766/766 均通过；详见 `../06_testing/2026-08-13-shared-heal-not-yet-field-surgery-runtime.md`。
- 升级 Regeneration 6、AnyAlly、对队友治疗和双方 Shackle、多玩家、默认 Deck、奖励、Run、Scene / Prefab 仍未实现。

## 28. Prismatic Shot 与共享重复伤害计划（已验证）

- `Prismatic Shot / 幻彩射击`（3279）基础态为 0 Energy / Rare Attack / 显式 Enemy / Hand→DiscardPile，Program 79、基础 Ammo 1、Shoot 标签。命令开始时冻结目标状态种类数 `S`：Strength 非零、Vulnerable 正层及每个正层数的 17 种 `MachineGunnerCombatantStatus` 各计一种，同种多层只计一次；HP、Block、资源、PowerPile、Stim 和 scheduled effect 不计。
- 逻辑伤害段固定为 `[6, 9 × S]`。Stim 激活时，每个逻辑段后立即复制一段相同基础值，整卡费用冻结为 `1 + logicalCount` Ammo；例如 `S=2` 时从 6/9/9 展开为 6/6/9/9/9/9，所需 Ammo 为 4。费用不足时不能支付部分 Ammo 或执行部分命中。
- 每个来源段与 Stim 复制段严格执行 `main Damage → IncendiaryAmmo Burn → PortableHelper`；固定目标投影死亡后停止所有剩余段且不改选目标，Helper 不递归。升级仅把首段 6 改为 9，重复段仍为 9；当前升级数值仍只是作者表与本地化元数据。
- 共享 `BattleRepeatedDamageExecutor` 只冻结目标策略、来源/敌人标量、段 outcome、终态投影、随机快照和计划生命周期。机枪兵 `MachineGunnerRepeatedDamageHitSequence` 独占 Ammo/Stim/职业后效；固定目标幻彩射击不消费 Turn-owned CardTarget RNG，也没有为职业建立第二随机流。
- 正式 `battle.card.xlsx` 只把 3279 与同批 Ironclad 3116 翻为 `Implemented`，Marine 达到 78/4（V1 61/3、V2 17/1），全项目 90/78，Effect 15 项。工作簿 SHA-256 为 Card `EA90C1A34FBDD9C54EBE2832C6CCC796DC4752A6B90C15F6A42BDB8C03A2CDF1`、Effect `35BF163D09E6F8AA6478C134D90A5FBAC304CC3135357D8237909DBC87ECAE64`、i18n `B80CD6EDCD0EAE2F52812B1CFF5DDAD96C1AB0507CD05E012C919DB05122215F`。Luban、Localization、Sync/Addressables、真实 BuildLayout、双卡 11/11、正式 53/53、行为 243/243 与完整 EditMode 776/776 均通过；详见 `../06_testing/2026-08-13-shared-repeated-damage-sword-boomerang-prismatic-shot-runtime.md`。

## 29. Secondhand Smoke 与通用 Poison（已完成 Unity 原生验证）

- source-stated 的基础规则是用来源当前 Smoke 向显式目标施加同值 Poison；当前基础 Program 70 在命令起点冻结该值，不消费 / 清空来源 Smoke。Smoke 为 0 时仍是合法出牌，只是不产生 `0→0` Poison 记录。
- 升级描述为来源与目标 Smoke 总和，但当前升级机制仍未实现；因此“读取目标 Smoke”只保留在 metadata，不得进入基础实例。原始 `README web.md` 继续 source-only，不回写实现裁决。
- Poison 是通用参与者状态，不是第 18 个机枪兵私有状态：tick 在参与者自己的行动开始绕 Block 结算并减 1 层。它作为一种通用活跃状态加入 3277 / 3278 / 3279 的冻结计数，使最大集合从 19 扩为 20；同层数无关，只按存在计一种。
- 本切片不获 Prefab 修改授权，故没有常驻 Poison 图标、层数 HUD 或 pulse。开发中任务前缀 `419c…` 2/2、`b5f…` 8/8、`79a…` 289/289 保留为前置证据；正式表、Luban、Localization、`Sync and Build All`、BuildLayout / `AssetBundleProvider` 均已成功，最终定向任务 `88e36d2a5cbb47b7b4a67207dad00856` 为 9/9，完整 EditMode `9ca3d43a79d24b25a917fad7b6166584` 为 793/793。当前正式 Marine 为 **79/3**（V1 **61/3**、V2 **18/0**），全项目为 **92/76**、Effect 为 **16**。

## 30. Unstoppable 基础态需求摘要（已完成 Unity 原生验证）

- `Unstoppable / 势不可挡`（3250）基础态冻结为 1 Energy / Rare Power / Self / PowerPile / Program 50 / 空 Effect bindings / 非 Innate；基础与升级费用 metadata 都是 1 Energy。
- 基础运行时只在持有者造成致死，或一条伤害把目标的正 Block 从正值降到 0 时触发；普通非致死、未破挡伤害不触发。升级追加“施加 debuff 时触发”只保留为 metadata，不得写成已实现。
- 候选卡是按静态表顺序冻结的 `Implemented` / Attack / 非 Shoot / 目标可自动解析模板。共享 settlement trigger 随机选一张并创建唯一临时手牌实例；Queue 在父表现屏障后以零实际费用执行完整出牌，并强制子牌进入 ExhaustPile。当前 registration ID 在自己派生链中被抑制，防止自递归。
- 本切片无新 HUD / Prefab / Scene，不扩大为任意 Power event bus、升级实例、Deck / 奖励 / Run / 多人。正式生成后 Marine **82/0**（V1 **64/0**、V2 **18/0**）、全项目 **98/70**、Effect **19**。Luban 通过；首次 Sync 因 Juggernaut i18n 缺少 `{triggerDamage}` 被正确拒绝，单点修复后同步与 Addressables 成功。定向 `054b6bcd5d734f729a2f1f95c4e7a80d` 7/7，完整 EditMode `d156b8e2537546ef9e83da0ef5dadd2a` 807/807；完整证据见 `../06_testing/2026-08-14-shared-settlement-trigger-juggernaut-unstoppable-runtime.md`。
