# 📋 STS2 机枪兵 Mod · 会话移交文档（2026-08-11）

> 供新会话快速接手。所有关键决策、当前状态、待办、技术坑都在这里。

## 0. 最近改动摘要（2026-08-11 会话末，重要）

### 0.1 联机"掉线/被踢/同步报错"修复（21:09 部署，根因=非确定性随机）

用户联机实测：打到一半提示同步错误/校验失败/被踢出，症状吻合 HANDOFF 教训里的 State divergence/checksum 不匹配。**根因：mod 内 8 处 `Random.Shared`（.NET 系统真随机，各端独立熵源）在联机确定性回放里必然产生不同结果 → 两端状态分叉 → checksum 对不上 → 被踢。**

**修复**：全部改用 RitsuLib 确定性 RNG `Entry.GetDeterministicRng(player, streamId)`（新辅助方法，内部 `ModRunRngRegistry.Get(player, ModId, streamId)`）——基于 `player.PlayerRng.Seed`（run 级 seed，联机两端一致）派生确定性随机流，state 经 run 存档同步，回放确定性。**规则：mod 任何"影响状态/战斗结果"的随机，禁止 `Random.Shared`，必须走 `GetDeterministicRng`；`streamId` 同一用途固定同值、不同用途不同值。**

8 处修复清单（streamId）：
- `Entry.MaybeTriggerSkyWrath` 天空之怒随机主目标（"sky_wrath"，owner.Player 为 null 直接 return）
- `MarineFireSupportPower` 火力支援随机目标（"fire_support"）
- `MarineNeedleStormPower` 钢针随机目标（"needle_storm"）
- `MarineStimPower` 兴奋剂额外射击随机目标（"stim_extra_shot"）
- `MarineUnstoppablePower` 势不可挡随机攻击卡（"unstoppable"）
- `MarineOpportunisticStrike` 趁势追击随机手牌（"opportunistic_strike"，Owner 是 Player）
- `MarineAirSupportRelic` 空军开道加权随机池（"air_support"，RelicModel.Owner 是 Player）
- `MarineFieryCocktailRelic` 热情鸡尾酒随机敌人（"fiery_cocktail"）

已构建部署 21:09（DLL+PCK）。**待用户联机实测验证不再掉线。** 游戏本体 `AttackCommand.TargetingRandomOpponents`（扫射/疾风肘击等）内部已用确定性 Rng，无需改。

⚠️ **已核实（2026-08-11，反编译 CombatManager 确认）：燃烧联机【不会】双倍结算，无需改**。玩家回合在联机中是"所有玩家一起结算"：`SetReadyToBeginEnemyTurn` 等到 `_playersReadyToBeginEnemyTurn.Count == _state.Players.Count` 才调用 `AfterAllPlayersReadyToBeginEnemyTurn` → `EndPlayerTurnPhaseTwoInternal`（含 `Hook.AfterTurnEnd(side=Player)`）每轮恰好一次，不是每玩家各一次。`IterateCombatHookListeners` 遍历所有监听器一次（幂等）。MarineBurnPower 只在 `side==Player` 结算，敌人回合结束 `side=Enemy` 直接 return——每轮一次，数值正确。**教训：勿把"hook 广播到所有玩家实例（需 owner 检查）"误当作"hook 每玩家各触发一次"（此误判曾致本隐患记录）。**

### 0.2 联机"参与者判断" bug（21:25 部署，真 bug——非首位玩家效果全失效）

排查燃烧双倍结算时发现**真正会破坏联机的 bug**：`AfterSideTurnEnd(side==Player)` 的 `participants` 参数在联机中 = **所有玩家**的 Creature（`playersEndingTurn = _state.Players`），而 5 处实现用 `participants.FirstOrDefault(c => c.IsPlayer)?.Player` **只取列表首位玩家** → 只有第一个玩家的遗物/power 会执行，**非首位玩家**的以下效果全部失效：

- `CmcArmorRelic` 能量跨回合保留（`_lastTurnEndEnergy` 不记录 → 非首位玩家每回合能量被重置）
- `CmcRoyalGuardRelic` 同上（+4 能量保留）
- `MarineInvisiblePower` 隐身回合末 -1（非首位玩家隐身永不衰减/不消失）
- `MarineShacklePower` 束缚回合末清除（非首位玩家束缚永不解）
- `MarineBurningOilPower` 烈火烹油回合末加燃（非首位玩家整场失效）

**修复**：全部改为 `participants.Any(c => c.IsPlayer && c.Player == Owner/Owner?.Player)`（检查主人的 Player 是否在 participants 中，而不是取首位）。Relic 的 `Owner` 是 Player；power 用 `Owner?.Player`。已构建部署 21:25（DLL+PCK），构建 0 错误。

**通用规则**：`AfterSideTurnEnd` 里判断"是不是我自己的回合"用 `participants.Any(c => c.Player == ownerPlayer)`，**禁用 `participants.FirstOrDefault(c => c.IsPlayer)`**。同理 `AfterPlayerTurnStart`/`AfterPlayerTurnEnd` 用参数 `player` 与 owner 比对（这些 hook 每玩家各调一次，与 AfterSideTurnEnd 语义不同）。

---


## 1. 工程位置与环境

- **mod 工程**：`marine-game\sts2-marine-mod\`（本工作区）
- **游戏**：`E:\SteamLibrary\steamapps\common\Slay the Spire 2`（v0.107.1）
- **.NET 9**：`C:\Users\Administrator\AppData\Local\Microsoft\dotnet\dotnet.exe`（新会话需用完整路径，`$env:DOTNET_ROOT` 指这里）
- **Godot 4.5.1 Mono**：`E:\Godot\Godot_v4.5.1-stable_mono_win64\Godot_v4.5.1-stable_mono_win64.exe`
- **RitsuLib**：用户通过 Steam Workshop 订阅（多版本兼容包，游戏自动选 0.107.1 变体）；**构建时 `local.props` 已设 `RitsuLibAutoCopy=false` 不要自动部署**
- **构建**：`& "$env:DOTNET_ROOT\dotnet.exe" build .\MarineSoldier.csproj`（产物自动部署到游戏 `mods\MarineSoldier\`）
- **启动游戏**：`Start-Process "d:\steam\steam.exe" -ArgumentList "-applaunch 2868840"`（必须经 Steam）
- **日志**：`%APPDATA%\SlayTheSpire2\logs\godot.log`（调试主要靠它）

## 2. 当前功能状态（全部实测通过）

- **角色**：陆战队员（70血/3能量基础，军绿→亮青主题色）
- **卡牌**：**83 张**（本地化 title 83 个，与卡类一一对应）。含 5 初始 + 原型 58 奖励 + 18 张新卡（防御姿态/霸凌/幻彩射击/先发制人/紧急散热/让我抽抽抽/给你吸吸二手烟/狂轰滥炸/天空之怒/标记/铝热炸弹/踏碎/钢针风暴/便携帮手/充能爆射/隐秘行动/私人改装/焚风）+ 先古卡超级强化剂 + 机枪扫射临时卡。**2026-08-09 已把 Web 原型全部 58 张卡实装完成**（详见 §4 待办历史）。卡池覆盖：基础技能/烟雾/浸油/燃烧/支援/炸弹/防御/隐身/游击/势不可挡/三连击等全部机制。**2026-08-11 新增**：2 张烟雾联动卡（让我抽抽抽/给你吸吸二手烟）+ 2 张支援强化联动卡（狂轰滥炸/天空之怒）+ 焚风（烟雾→燃烧）
- **生成专用池（Token 池）**：`MarineSoldierTokenCardPool`（IsColorless 无色池）。生成卡（机枪扫射）注册到这里——canonical 在 ModelDb（`CreateCard<T>` 可用）但**不在角色奖励池** `MarineSoldierCardPool`，不会被战斗奖励抽取。参考原版 Shiv 放 TokenCardPool 的做法
- **延迟伤害正确写法**：支援类（火力支援/燃烧轰炸/女妖/三连击延迟狙击）结算**必须用 `CreatureCmd.Damage(choiceContext, target, amount, ValueProp.Unpowered, null, null)`（6参版本 dealer+cardSource 双 null）**。**勿用 `AttackCommand.FromCard(null)`**——FromCard 会 get_Attacker + card.Player，传 null 抛 NullReferenceException（曾致支援类全失效）。注意 5参版本 `Damage(...ValueProp, X)` 有 CardModel/Creature 两个重载，传 null 编译歧义，必须用 6参
- **全息诱饵 = 原版 BufferPower**（2026-08-10）：直接 `Entry.ApplyPower<BufferPower>(creature, 1, bypassArtifact: true)`。曾自研 MarineDecoyPower（ModifyHpLostAfterOstyLate）未生效，已删除。原则：能用原版 power 别自研 hook。卡牌文本已改为"获得 1 层缓冲"；**稀有度 2026-08-11 改为稀有（Rare）**
- **CMC装甲**（唯一初始遗物，合并了 CMC自动供弹+能量核心）：
  - 每回合 +1 弹药
  - 能量上限 +2（引擎3→5）+ 跨回合保留 + 每回合恢复3（走引擎增益管线）
  - **2026-08-10 更名为「CMC-400动力装甲」**（仅改显示名，类名 CmcArmorRelic 不变）
- **先古升级系统（2026-08-10 实装，供通关后奖励 NPC 对话使用）**：
  - 原版 **Orobas 先古 NPC** 对话奖励正是两个遗物：`ArchaicTooth`（远古之牙：初始卡→先古卡「超越」）和 `TouchOfOrobas`（Orobas之触：初始遗物→「精炼」升级版）。这是原版内置机制，mod 只需注册映射
  - **CMC皇家卫队**（CmcRoyalGuardRelic）：CMC-400 的精炼升级版，每回合 +2 弹药、能量上限 +4（3→7）、跨回合保留、每回合恢复 4；`RelicRarity.Ancient` 稀有度（不进普通遗物池）
  - **超级强化剂**（MarineSuperStim）：兴奋剂的先古版，0 费技能：2 恢复（RegenPower）+ 抽 3 + 1 层兴奋剂；升级 3 恢复 + 2 层兴奋剂；`CardRarity.Ancient` 稀有度，注册到 Token 池（有 canonical 不进奖励池）
  - **注册方式 = RitsuLib 属性标注**（无需手动调 API）：
    - `[RegisterArchaicToothTranscendence(typeof(MarineSuperStim))]` 标注在**初始卡 MarineStim** 类上
    - `[RegisterTouchOfOrobasRefinement(typeof(CmcRoyalGuardRelic))]` 标注在**初始遗物 CmcArmorRelic** 类上
    - 自动注册机制（AttributeAutoRegistrationTypeDiscoveryContributor）会扫描程序集里所有继承 AutoRegistrationAttribute 的属性并生成注册操作——先古/精炼两个属性都继承 AutoRegistrationAttribute，与 RegisterCard 等同一管线，`RegisterModAssembly` 时自动生效
  - 玩家在 Orobas 事件获得 ArchaicTooth/TouchOfOrobas 时，牌组/遗物里的对应初始项会自动转换（保留升级与附魔）
- **弹药系统**（RitsuLib SecondaryResource）：
  - 上限5、开局满弹、战斗内持久、每回合+1（CMC）
  - 消耗弹药卡：射击1/扫射2/狙击2/钉刺1/击退1/精准3/六连1(+额外)
  - 猛烈发狂=耗尽全部弹药、12连=两波换弹
  - UI：副能量计数器（星辉图标、黑灰调色、右移）+ 卡牌成本角标（黑灰图标+数字）
- **兴奋剂**（MarineStimPower）：持续N回合，射击词条卡额外射击1发（耗1弹药，**复制该卡的虚弱/易伤 debuff**）
- **燃烧**（MarineBurnPower，真燃烧）：回合结束对携带者造成等层数可被格挡伤害；烈焰肘3层/燃烧瓶5层
- **射击分类词条**：shoot（射击）/sniper（狙击）/shotgun（霰弹占位）三词条。**设计原则（2026-08-11 用户确认）**：狙击/霰弹是"射击"子词条，各功能按需触发——
  - `IsShootCard`（仅 shoot）：**兴奋剂**（额外射击1发）
  - `IsShootCategory`（含狙击/霰弹）：**燃烧弹药**（命中上燃）、**开火**（射击伤害每层+1）、**帮手**（便携帮手每打击段攻击一次）、隐身攻击衰减（狙击特判不减）
  - `IsShootCategory` 排除（非射击判定）：连肘免费、功夫机甲、势不可挡、陈年机油
  - 狙击特判：对易伤/隐身×2、不吃兴奋剂、隐身衰减不减、**不吃自身烟雾减输出（只吃敌人身上烟雾减伤）**、**开火双倍（每层+2）**。**三连击延迟狙击也享受开火双倍**（power 结算 card=null 无法走 MarineFirePower.ModifyDamageAdditive，在 MarineTripleStrikeSupportPower 结算时手动 `Amount + fire*2`）
  - **钉刺射击 = 射击+狙击双词条（2026-08-11）**：同时吃兴奋剂（额外射击复制虚弱/易伤，两次伤害和buff）、燃烧弹药、开火、帮手 + 狙击增幅（目标已有易伤×2）、不会破隐、不吃自身烟雾
- **支援词条**（2026-08-11）：新增 **SupportTag**（"support"），挂在支援类效果卡上（火力支援/燃烧轰炸/女妖/三连击，三连击同时保留 SniperTag）；`Entry.IsSupportCard(card)` 判定。用于统一识别"产生延迟支援打击"的效果卡
- **支援强化/联动卡（2026-08-11 新增）**：
  - **狂轰滥炸**（MarineBombard，Uncommon Power 1费）：获得 **4 层**狂轰滥炸 buff（升级 6 层），**每层使支援类效果的伤害与附加状态层数提高 10%**（可叠层，不随回合衰减）→ 效果等同 40%/60%。实现：`MarineSupportBoostPower`（无属性，Amount=层数），**`Entry.SupportDamage` 结算时统一应用加成**（所有支援伤害入口）+ **`Entry.SupportStateAmount` 作用于附加状态层数**（燃烧轰炸的燃烧层数吃加成，四舍五入；2026-08-11 用户要求烟雾/燃烧等状态附加层数也吃增幅）。**先计算狂轰滥炸加成（作为造成伤害）→ 应用到怪物时再算易伤加成**
  - **天空之怒**（MarineSkyWrath，Rare Power 1费）：**获得 1 层天空之怒**（buff，可叠层、不随回合衰减），**每层发动一次打击**：对随机一敌 8 伤 + 其他敌 4 伤，升级 0 费。实现：`MarineSkyWrathPower`（`RandomDamage`/`SplashDamage` 属性，Amount=层数）。**触发规则（2026-08-11 确认）**：每个支援【打击段】触发一次（火力支援 5 段→5 次、燃烧轰炸 2 段→2 次、女妖 2 段→2 次【强化第二目标不额外触发】、三连击延迟狙击 1 段→1 次）；天空之怒自身算支援效果（吃狂轰滥炸/易伤加成）但**不递归触发天空之怒**；后续无伤害支援效果也会触发一次。触发点：支援 power 的 `MaybeTriggerSkyWrath` 放在**打击段循环内**
  - **破甲增幅燃烧/支援（2026-08-11）**：破甲三类伤害都生效——攻击（ModifyDamageAdditive）、支援（`Entry.SupportDamage` 内追加）、燃烧（`MarineBurnPower` 结算 `Amount + Entry.ArmorBreakBonus(owner, Amount)`）。统一用 `Entry.ArmorBreakBonus(target, baseAmount)` = 破甲层数 × 目标易伤倍率（只吃易伤）
  - **注意**：`Entry.SupportDamage` 签名已改为 `SupportDamage(Creature? attacker, Creature target, decimal baseAmount)`（attacker 传支援 power 的 Owner）——所有支援 power 调用点已同步
- **最近/最远目标（点击即打，2026-08-09 实装）**：肘击/快速肘击/连肘/爆炸肘/烈焰肘/刺刀招架/战术六连/不解释12连 = 最近敌人，狙击 = 最远敌人。用 RitsuLib `RegisterMultiTargetType`（**多目标类型**，点击即打不进入瞄准，区别于单目标类型仍会进瞄准流程），OnPlay 内用 `Entry.GetNearestEnemy`/`GetFarthestEnemy`/`GetSecondNearestEnemy(ICombatState)` 自行解析实际目标（`cardPlay.Target` 为 null）
- **失去力量（MarineLoseStrengthPower，2026-08-09 终版）**：**施加【负 StrengthPower -N】+【MarineLoseStrengthPower 补回标记 buff】**，`AfterSideTurnEnd` 用 `side==owner.Side`（在谁身上谁回合结束恢复）SetAmount 补回力量并移除。等价原版 TemporaryStrengthPower/DarkShackles，**不依赖 OriginModel**。
  - **Artifact 规则（用户指定）**：先 `ApplyPower<StrengthPower>(target,-N,allowNegative:true)` 扣负力量（GetTypeForAmount(-N)=Debuff 可被 Artifact 挡），**返回 false 则整次不生效、不挂补回 buff**；成功才挂 `MarineLoseStrengthPower`（bypassArtifact:true，仅记账不被二次挡）
  - **勿用原版 PiercingWailPower/DarkShacklesPower**（依赖 OriginModel 指向原版卡才触发扣力，直接 PowerCmd.Apply 只挂 buff 不生效）
  - **勿重写 ModifyDamageAdditive 返回总量**！该 hook 返回【增量】被 Hook.ModifyDamageInternal 用 Decimal.op_Addition 累加，返回总量致伤害指数叠加（曾致 11→44 严重 bug）
  - `Entry.ApplyPower<T>` 返回 bool（成功/被 Artifact 挡）+ 参数 `allowNegative`/`bypassArtifact`
- **击退射击（2026-08-09 实装）**：0 费 1 弹，对第一个敌人主伤害 7 + 失去力量 2，后续所有敌人次伤害 3 + 失去力量 2；升级 9/5/3。第二伤害/失去力量用 `IntVar("SecondDamage",3)`/`IntVar("LoseStrength",2)` 存（DynamicVarSet 按 name 索引），不能存私有字段
  - **快照遍历**：OnPlay 先快照存活敌人列表 alive[] 再按索引命中（alive[0]主伤、alive[1..]次伤）——首个被击杀后后续仍按快照命中，勿用 seenFirst 实时遍历（列表位移会把原第二个误当"已处理"跳过，曾致击杀首个后后续失效）
- **能量机制**：CMC装甲走引擎管线（ModifyMaxEnergy +2、ShouldPlayerResetEnergy=false、AfterPlayerTurnStart 接管）
- **核心扩容**：能量上限 +1（ModifyMaxEnergy，走 CMC 同管线）；升级 0 费 + 能量上限 +2（Repeat 1→2，SetCustomBaseCost(0)）
- **动力强化**：每回合多抽 1 张牌（MarineDrawBonusPower 覆写 ModifyHandDraw，层数叠加；升级费用0）
- **撤退**：格挡 + 下回合补满弹药（MarineNextTurnReloadPower，AfterPlayerTurnStart 一次性补弹）+ PlayerCmd.EndTurn 结束回合
- **X 费卡**（猛烈发狂/疾风肘击）：`MockSetEnergyCost(new CardEnergyCost(this, 0, costsX: true))` 设 X 费标志（canonical=0 + CostsX=true），OnPlay 用 `ResolveEnergyXValue()` 读 X。RitsuLib 无 X 费辅助，用公开 MockSetEnergyCost 替换
- **破甲（MarineArmorBreakPower，2026-08-11 新增，定稿）**：debuff，每层使目标受到的攻击伤害 +1（可叠加，不随回合衰减）。**实现于 `ModifyDamageAdditive`（格挡前）**——破甲加入总伤害后：有格挡则连基础伤害一起被格挡抵消（5伤+2破甲 vs 10格挡 → 7 vs 10，剩 3 格挡），无格挡/破防则随伤害穿透。**只需一行虚弱补偿**（攻击者虚弱会乘算阶段整体 ×0.75，预 ÷weakMult 抵消），乘算阶段的易伤 ×1.5 自然放大破甲 → 破甲 = Amount × 易伤，不吃虚弱/力量/烟雾（加算阶段加法交换律，破甲独立增量不被力量负值抵消）。演进史：①格挡后追加（穿透格挡，用户否）→ ②加算阶段+拉回技巧（过度复杂，用户否）→ ③**加算阶段直接返回 Amount（+一行虚弱补偿），终版**。**支援伤害也吃破甲**：`Entry.SupportDamage` 末尾按 `Amount × 易伤倍率` 追加（与攻击语义一致：只吃易伤）。本地化 powers.json 已加"破甲/Armor Break"
- **破甲来源卡（2026-08-11 新增 3 张）**：**标记**（MarineMark，Common 攻击 0费1弹：5伤+2破甲，升级7伤3破甲）、**铝热炸弹**（MarineThermiteBomb，Uncommon 技能 1费：全体4燃烧+2破甲，升级6燃3破甲；燃烧经 ApplyBurn 触发浸油）、**踏碎**（MarineCrush，Common 攻击 1费：最近敌人9伤+4破甲，升级12伤5破甲；点击即打最近目标）。破甲层数用 `IntVar("ArmorBreak", n)` 存。本地化中英已加
- **燃烧轰炸加浸油（2026-08-11）**：`MarineFireBombardment` 加 `IntVar("Oil", 3)`（升级 4），power 加 `OilAmount` 属性。**先火后油**：每轮先 `ApplyBurn`（触发已有浸油）再施加浸油（本轮新油不触发），油/燃层数都吃狂轰滥炸（`SupportStateAmount`）。本地化已注明"先火后油"
- **燃烧轰炸改多实例共存（2026-08-11）**：`MarineFireBombardmentPower` 加 `InstanceType=Instanced`（同钢针/炸弹）——**每次释放创建独立实例，重复释放下回合有多个轰炸效果**（各结算一次后移除）。OnPlay 属性设置用 `GetPowerInstances<T>().LastOrDefault()`（多实例陷阱）。本地化已注明"重复释放获得额外的轰炸（多实例共存）"
- **凝固汽油弹 2费→1费（2026-08-11）**：仅改 `BaseEnergyCost`，数值/机制不变
- **钢针风暴（MarineNeedleStorm，2026-08-11 新增）**：Common 技能 1费，获得 1 层钢针 buff（升级 2 层=持续 2 回合）。**多实例 buff（Instanced，同引导核弹炸弹 TheBombPower）**：`MarineNeedleStormPower`（StackType.Counter + `InstanceType=Instanced`），回合开始时随机敌人 4 次打击（每次 1 伤 + 1 破甲），每段触发天空之怒，然后各自 -1 归零移除。**每次施放创建独立钢针实例（多根共存，各自触发/递减，不叠加层数）**。伤害走支援管线（吃易伤/狂轰滥炸/破甲，不吃力量/虚弱/烟雾）。挂 SupportTag。数值全用变量（Hits=4/Damage=1/ArmorBreak=1）。**注意多实例陷阱**：`GetPower<T>` 返回第一个实例，设置属性必须用 `GetPowerInstances<T>().LastOrDefault()`（新创建的），否则新钢针属性为 0 不触发
- **便携帮手（MarinePortableHelper，2026-08-11 新增）**：Uncommon 能力卡 1费，获得 1 个便携帮手：每当你射击敌人时帮手攻击一次你的目标（1 伤）；**升级降为 0 费**（伤害不变）。`MarineHelperPower`（Instanced 多实例，不随回合衰减）：**AfterDamageGiven 每个打击段触发**（非 AfterCardPlayed 每卡一次），**判定 `Entry.IsShootCategory`（狙击/霰弹也算射击分类）**——充能爆射穿透 N 个目标，帮手对各命中目标各攻击一次；帮手的自身伤害 card=null 不递归。owner 检查防联机串扰。**帮手伤害 = (基础 1 + 开火层数) × 易伤 + 破甲×易伤**（Unpowered 管线：吃易伤/破甲/开火，不吃力量/虚弱/烟雾）。多实例陷阱同钢针：OnPlay 取 LastOrDefault 设属性
- **充能爆射（MarineChargedBurst，2026-08-11 新增）**：Uncommon 攻击 2费，**狙击词条**（SniperTag），TargetType.AllEnemies。**穿透伤害**：从最近到最远逐个狙击所有存活敌人，第 index 个（0 起）伤害 = `DamageVar(12) × (1 + 0.5×index)`（第1个基础、第2个+50%、第3个+100%…累加式）；每段都享受狙击增幅——目标已有易伤时基数 ×4/3（经 Vulnerable 自动×1.5 → 净×2，同狙击）。升级伤害 12→16。不吃兴奋剂（狙击词条）；本地化已注明"每穿透一个敌人+50%"
- **隐秘行动（MarineStealthAction，2026-08-11 新增）**：Uncommon 技能 1费，**固有（Innate）**：获得 1 层隐身（MarineInvisiblePower，受到攻击伤害减半）+ 抽 1 张；升级 2 层隐身 + 抽 2 张。`CanonicalKeywords=[CardKeyword.Innate]`、`RepeatVar(1)`+`CardsVar(1)`。参照光学迷彩/兴奋剂写法
- **私人改装（MarinePrivateMod，2026-08-11 新增）**：Uncommon 能力卡 1费，获得 1 弹药上限 + 1 层开火（射击伤害每层+1）；升级 2 弹药上限 + 2 层开火。`RepeatVar(1)` 同值双用：施放 `MarineAmmoCapPower`（弹药上限，同扩容弹夹）+ `MarineFirePower`（开火，同电磁增压）。本地化中英已加
- **空军开道（MarineAirSupportRelic，2026-08-11 新增，加权概率版）**：Uncommon（罕见）遗物，注册到 MarineSoldierRelicPool。**进入战斗时释放一张随机的支援卡效果**——`BeforeCombatStart`（战斗开始，玩家第一回合之前）按概率随机挑一个支援类型，把对应支援 power 挂到玩家身上 → **玩家第一回合开始自动触发**（一次性支援生效后移除；持续支援如女妖/钢针 -1 层）。**加权随机池（去三连击，整数权重放大 100 倍）**：火力支援 21.75%（2175）/ 燃烧轰炸 21.75%（2175）/ 女妖 21.75%（2175）/ 钢针风暴 21.75%（2175）/ **500磅 8%（800，2回合后全体60伤）**/ **引导核弹 5%（500，3回合后全体99伤）**，总和 10000。炸弹分支用 `MarineBombPower`。属性设置用 `GetPowerInstances<T>().LastOrDefault()`（多实例陷阱）。本地化中英 relics.json 已加
- **脑袋尖尖（MarinePointyHeadRelic，2026-08-11 新增）**：Uncommon（罕见）遗物，注册到 MarineSoldierRelicPool。**篝火自定义选项"打药"**：恢复 30% 生命值 + 最大生命值 +5 + 力量 +1。实现：遗物 `TryModifyRestSiteOptions` 添加 `PointyHeadDrugOption`（继承 RitsuLib `ModRestSiteOptionTemplate`，支持自定义图标/标题）：
  - **图标复用原版恢复**：`AssetProfile.IconPath = "res://images/ui/rest_site/option_heal.png"`（原版资源运行时加载，pck 加密但可引用）
  - **恢复走原版管线**：`HealRestSiteOption.GetHealAmount(Owner)`（基数=最大生命×30%，且吃 `Hook.ModifyRestSiteHealAmount`——**其他"增加篝火恢复"的遗物效果（如皇家枕头+15）也会作用于打药**）
  - 最大生命 `CreatureCmd.GainMaxHp`，力量 `Entry.ApplyPower<StrengthPower>`
  - 标题/描述放 static_hover_tips.json（`MARINE_POINTY_HEAD_DRUG.title/.description`）
- **弹夹（MarineMagazineRelic，2026-08-11 新增）**：Common（普通）遗物，**弹药上限 +2**。实现 `ISecondaryResourceHookListener.ModifyMaxSecondaryResource`（仅弹药资源 +2，同 MarineAmmoCapPower 思路）。注册到角色专属池 MarineSoldierRelicPool → **只有陆战队员（有弹夹的角色）能获得**。本地化中英 relics.json 已加
- **防暴盾牌（MarineRiotShieldRelic，2026-08-11 新增）**：Uncommon（罕见）遗物，**进入战斗时获得 6 层护甲**（MarineArmorPower：每回合开始获得等层数格挡，破防攻击每段 -1 层）。实现：`BeforeCombatStart` + `Entry.ApplyPower<MarineArmorPower>(Owner.Creature, 6)`。本地化中英 relics.json 已加
- **缓冲垫层（MarineBufferPaddingRelic，2026-08-11 新增）**：Rare（稀有）遗物，**每回合第一次抽到状态牌（伤口等 CardType.Status）时抽一张牌**。实现参考原版 IterationPower：`AfterCardDrawn` 检测 `card.Type == CardType.Status` + `CombatManager` 历史计数本回合已抽状态牌数（<=1 含当前）。**注册到共享遗物池**：新建 `MarineSoldierSharedRelicPool`（`[RegisterSharedRelicPool]`，TypeListRelicPoolModel）→ **所有角色都可获得（非陆战队员专属）**。联机检查：`card.Owner != Owner`（CardModel.Owner 是 Player）+ 历史 `e.Actor.Player == Owner`。本地化中英 relics.json 已加
- **小帮手（MarineLittleHelperRelic，2026-08-11 新增）**：Common（普通）遗物，**战斗开始获得 1 层帮手**（MarineHelperPower：每次射击段帮手攻击目标 1 伤，吃易伤/破甲/开火增幅）。**陆战队员限定**：注册到角色专属池 MarineSoldierRelicPool。实现：`BeforeCombatStart` + `Entry.ApplyPower<MarineHelperPower>(1)` + 新实例设 `Damage=1`（多实例陷阱取 Last）。本地化中英 relics.json 已加
- **陆战队员卡牌改深蓝（2026-08-11）**：`MarineSoldierCardPool` + `MarineSoldierTokenCardPool` 的卡牌颜色改为深蓝色系——`DeckEntryCardColor` 亮青 `ThemeColor` → 深蓝 `new Color("1B3A8B")`；`EnergyOutlineColor` → `(0.10,0.20,0.45)`；`PoolFrameTintMaterial` RGB 着色 `(0.30,0.55,0.32)` 绿调 → `(0.12,0.25,0.60)` 蓝调。**注意**：角色 `ThemeColor`（亮青，用于名字/地图/遗物描边）未改，只改卡牌/卡池配色
- **回归 bug 批量修复（2026-08-11）**：
  1. **弹夹上限不生效**：弹药注册 `hardMaxAmount: 5` 把加成 clamp 死（`SecondaryResourceStateStore` 的 FloorAndClamp）→ 改为 `99`。弹夹遗物/扩容弹夹/私人改装的弹药上限才生效
  2. **人工制品变负不消失**：`Entry.ApplyPower` 的 Artifact 拦截里 `artifact.SetAmount(Amount-1)` 归零不移除，变成负数继续挡 → 归零时 `RemoveInternal()`
  3. **兴奋剂不消失**：`MarineStimPower.AfterPlayerTurnStart` 注释写"归0自动移除"但实际只 `SetAmount(Amount-1)` → 改为 `Amount>1` 减层否则 `PowerCmd.Remove`
  4. **护甲莫名其妙消失**：`MarineArmorPower.ModifyHpLostBeforeOsty` 缺 `amount>0` 保护——完全被格挡的攻击也减层 → 加 `amount>0` 条件 + 归零 `RemoveInternal()`
  5. **战地手术可对队友用（2026-08-11 新增）**：`TargetType.Self` → `AnyAlly`（`(TargetType)6`）；目标是自己 → 自己恢复+束缚；目标是队友 → 队友恢复 + 自己和队友各 1 束缚（`cardPlay.Target ?? Owner.Creature` 单机 Target 为 null 指向自己）
  6. **兴奋剂卡图手牌边框**：卡图是正方形 1254×1254，与正常卡图横版 1000×760 比例不符，手牌小图露边 → 裁剪为 1254×953（同 1.316 比例）
- **通用原则**：`SetAmount` 归零**不会**自动移除 power，必须在递减处显式 `PowerCmd.Remove`/`RemoveInternal`。人工制品、兴奋剂、护甲都栽在这上面
- **回归排查（2026-08-11）**：全量复查之前修过的 bug，新增修复 **浸油减半归零残留**——`Entry.ApplyBurn` 里 `oil.SetAmount(floor(Amount/2))` 减到 0 不移除，残留 0 层浸油 buff。改为 `halved<=0 → RemoveInternal()`。其余全部完好：兴奋剂/护甲/人工制品归零移除、束缚 ShouldPlay owner 检查、燃烧弹药/陈年机油 attacker 检查、弹药 hardMax=99、支援 CreatureCmd.Damage 6参、失去力量增量语义、Token 池 RegisterSharedCardPool、本地化 key（无 NAPLAM）、功夫机甲/游击战/破甲/燃烧管线
- **热情鸡尾酒（MarineFieryCocktailRelic，2026-08-11 新增）**：Common（普通）遗物，**战斗开始对一个随机敌人施加 5 层燃烧**（真燃烧 MarineBurnPower，经 Entry.ApplyBurn 触发浸油）。**非限定**：注册到共享遗物池 MarineSoldierSharedRelicPool（所有角色可获）。实现：`BeforeCombatStart` + 随机存活敌人 `Entry.ApplyBurn(target, 5)`。本地化中英 relics.json 已加
- **燃烧血条预伤害显示（2026-08-11）**：`MarineBurnPower` 实现 RitsuLib `IHealthBarForecastSource`（生物身上的 power 实现此接口会被血条自动渲染预扣血段，同原版毒/末日机制）。显示回合结束燃烧将造成的伤害（层数 + `Entry.ArmorBreakBonus` 破甲增幅，Round 取整）；**颜色 = `StsColors.orange`（FFA518，攻击指示橙色）**，方向 `FromRight`（同毒）。**技术要点**：RitsuLib `HealthBarForecastRegistry` + `HealthBarForecastSegment(amount, color, direction, order)`；`HealthBarForecastGrowthDirection.FromRight=0`（从当前HP边向内延伸）/`FromLeft=1`（同末日）
- **联机判定全面排查 + 修复（2026-08-11）**：逐个核对全部 hook 的 owner 检查，修复 3 处串扰 bug——**束缚**（ShouldPlay 缺 owner 检查，队友也被禁攻）、**燃烧弹药**（AfterDamageGiven 缺 attacker==Owner，队友打射击卡命中触发我的上燃）、**陈年机油**（AfterDamageGiven 缺 attacker==Owner，队友打非射击攻击命中触发我的上油）。其余 hook 均已核实有 owner 检查（回合开始/出牌/伤害修正/回合结束/能量属性/遗物）。**排查要点**：AfterDamageGiven/AfterCardPlayed 类 hook 必须 `attacker==Owner`/`card.Owner==player`；ShouldPlay 必须 `card.Owner.Creature==Owner`；AfterPlayerTurnStart 必须 `Owner.Player==player`
- **炸弹体系（2026-08-11 改自定义）**：500磅/引导核弹改用**自定义 `MarineBombPower`**（原版 `TheBombPower` 是 sealed 无法改结算）。`InstanceType=Instanced` 多实例（倒计时/伤害各自独立），层数=剩余回合，玩家回合末递减归零引爆全体。**伤害口径（用户指定）：吃狂轰滥炸加成（`Entry.SupportBoostMultiplier`，每层+10%），不吃易伤**（保持 `ValueProp.Unpowered` 管线，非 IsPoweredAttack 天然不触发 VulnerablePower）；仍不吃力量/虚弱/烟雾、可被格挡、不触发天空之怒。`SetDamage` 存伤害到 DamageVar。空军开道随机池含炸弹（500磅参数）。**数值调整（2026-08-11）**：500磅基础伤害 40→**60**、升级 55→**90**（+30）；引导核弹升级 133→**199**（+100）

## 3. 关键技术坑（必须知道）

1. **Power 施加**：不能用 `new XxxPower()`（DuplicateModelException）→ `ModelDb.Power<T>()`；不能用 `ToMutable`（PoisonPower 等重写 SetAmount 的会崩）→ **`MutableClone()` + `ApplyInternal(target, amount, false)`**
2. **Artifact（人工制品）**：`ApplyInternal` 会绕过 Artifact 拦截，`Entry.ApplyPower` 已加手动拦截（目标有 ArtifactPower 且是 Debuff → 消耗1层跳过）
3. **单实例 power 叠加**：目标已有同类 power 时直接 `SetAmount(Amount+amount)`，否则报 "non-instanced power"（`Entry.ApplyPower` 已处理）
4. **卡牌数值存储**：升级数值用 DynamicVar（RepeatVar/PowerVar/IntVar），不能存私有字段（克隆丢失）
5. **本地化 key**：卡牌 `{MOD_ID}_CARD_{类名}`、power `{MOD_ID}_POWER_{类名}`、遗物 `{MOD_ID}_RELIC_{类名}`；**卡牌描述不能用 `{Cards:diff()}`**（报错），用 `{Damage:diff()}`/`{Block:diff()}`；RepeatVar 可用 `{Repeat}` 普通占位符
6. **power 本地化表**：`powers.json`（zhs/eng），标题/描述 key = `{MOD_ID}_POWER_{类名}.title/.description`
7. **计数器 UI 定位**：必须 `GetNode("%EnergyCounterContainer")` + offset Position（否则不可见）；`AlwaysShowInCombatUiForCharacter` 绑定角色
8. **构建被锁**：游戏运行中 dll 被锁，需先停游戏（有时需 `taskkill /PID x /F`）
9. **游戏 pck 加密**：无法直接提取原版素材；引用原版 res:// 路径（如 `res://images/atlases/ui_atlas.sprites/card/energy_defect.tres`）运行时加载
10. **先古/精炼映射**：原版 Orobas 先古 NPC 的 ArchaicTooth/TouchOfOrobas 遗物通过 `TranscendenceUpgrades`/`RefinementUpgrades` 字典（ModelId→ModelId）查映射，RitsuLib 用 Patch 注入 mod 映射。**映射属性必须标注在"初始项"类上**（初始卡/初始遗物），参数是目标先古卡/精炼遗物类型。先古卡/精炼遗物用 `CardRarity.Ancient`/`RelicRarity.Ancient` 稀有度 + 注册到 Token 池（不进奖励池）
11. **先古映射注册时机**：属性标注由自动注册机制处理（AttributeAutoRegistrationTypeDiscoveryContributor 扫描程序集），无需在 Entry.Initialize 手动调用 `RegisterArchaicToothTranscendenceMapping`/`RegisterTouchOfOrobasRefinementMapping`（那是命令式等价 API）。反射确认：`RegisterArchaicToothTranscendenceAttribute`/`RegisterTouchOfOrobasRefinementAttribute` 继承 `AutoRegistrationAttribute`，`BuildOperations` 用 `EnumerateEffectiveRegistrationAttributes` 遍历生成操作
12. **PCK 导出失败（hostfxr_initialize_for_runtime_config failed / .NET: Failed to load compatible .NET runtime）**：Godot Mono 导出 PCK 时找不到 .NET 运行时。**构建前先 `$env:DOTNET_ROOT = "C:\Users\Administrator\AppData\Local\Microsoft\dotnet"` 并加入 PATH**（本机 .NET 是用户级安装，非默认路径）。否则 dll 更新但 PCK 停留旧版，本地化改动（cards.json/relics.json）不生效——游戏仍显示旧文本/旧格式错误
13. **DynamicVarSet getter 会抛异常（多人不同步根因，2026-08-10）**：`DynamicVarSet.Weak/.Vulnerable/.Damage` 等属性 getter **用 `Dictionary.get_Item`（索引器）实现，键不存在时抛 `KeyNotFoundException`，不是返回 null**！访问"其他卡牌"（hook 传入的任意卡，如兴奋剂的 `cardPlay.Card`）的 DynamicVar 时**必须用 `TryGetValue("WeakPower"/"VulnerablePower"/"Damage", out var)`**（键名=变量注册名，PowerVar<WeakPower> 的键是 "WeakPower"）。曾因兴奋剂 AfterCardPlayed 访问 `vars.Weak` 打射击卡必崩 → PlayCardAction 反复异常 → **多人状态分叉（State divergence / checksum 不匹配）→ 客户端被踢**。访问"自己卡"的 DynamicVar（OnPlay 里 `DynamicVars.X`）安全（键必有）
14. **联机 hook 广播所有玩家实例（2026-08-10）**：多人模式下，`AfterPlayerTurnStart`/`AfterSideTurnEnd`/`AfterCardPlayed`/`ModifyMaxEnergy` 等 hook **会对所有玩家的遗物/power 实例各调用一次**（日志：`[CmcArmor] turn` 每回合出现 4 次 = 2玩家×2实例；`max=7` = 基础3 + 两玩家遗物各+2）。**所有这类 hook 必须加 owner 检查**：
    - 遗物：`player != Owner`（RelicModel.Player Owner）→ return；`ModifyMaxEnergy` 用 `if (Owner != player) return amount;`
    - power：`Owner?.Player != player` → return（AfterPlayerTurnStart）；`cardPlay.Card.Owner != p` → return（AfterCardPlayed，防队友触发我的兴奋剂/隐身/消耗我的弹药）
    - `AfterSideTurnEnd`：`participants.FirstOrDefault(c=>c.IsPlayer)?.Player` 与 Owner.Player 比对
15. **护甲破防判定参数顺序（2026-08-10）**：`ModifyHpLostBeforeOsty` 签名 = **`(Creature target, decimal amount, ValueProp props, Creature dealer, CardModel cardSource)`**——第一个参数是**被击者**，第四个是**攻击者**！曾把参数名误写为 `(attacker, amount, prop, target, card)` 导致判断 `dealer==Owner`（自己攻击敌人破防反而扣自己护甲）。注意原版是 `ModifyHpLostBeforeOstyLate`
16. **卡牌乱码 = 本地化 key 拼错（2026-08-10）**：凝固汽油弹本地化 key 写成 `MARINE_NAPLAM`（Napalm→Naplam 字母颠倒），RitsuLib 按类名生成正 key `MARINE_NAPALM` → 查找失败 → 名称+描述全乱码。**新卡务必用脚本核对**类名→SCREAMING_SNAKE key 与 JSON 一致性
17. **副能量 hover tip 需要 static_hover_tips.json（2026-08-10）**：弹药等副能量资源的标题/描述从 `static_hover_tips.json` 表读取，key = `{完整资源ID}.title/.description`（如 `MARINE_SOLDIER_SECONDARY_RESOURCE_AMMO.title`）。mod 缺失该表 → 弹药弹夹文本显示异常。已创建 zhs/eng
18. **敌人 debuff 衰减不能在 AfterPlayerTurnStart（2026-08-10）**：联机 owner 检查 `Owner.Player != player` 对敌人恒真（敌人 `Owner.Player` 为 null）→ 敌人身上的 power 在该 hook 永不衰减（烟雾曾因此卡死）。**敌人身上的 debuff 改到 `AfterSideTurnEnd` 且 `side == CombatSide.Enemy` 衰减**——敌人回合每轮恰好一次，天然联机安全。区分敌我：`Owner.Player == null` 即敌人（玩家 Creature 必有 Player）
19. **Token/先古卡池必须注册为共享卡池（2026-08-11）**：`MarineSoldierTokenCardPool`（先古卡超级强化剂/机枪扫射所在无色池）**必须加 `[RegisterSharedCardPool]`**（STS2RitsuLib.Interop.AutoRegistration）。否则 `CardModel.Pool` getter 在 `ModelDb.AllCardPools`（角色池+原版7共享池+RitsuLib共享池）找不到该池 → fallback 到 `MockCardPool.AllCardIds` → 运行时生成 mock 卡 → 触发游戏本体断言 `NeverEverCallThisOutsideOfTests_ClearOwner`（"You monster!"）→ **OROBAS 先古事件 hover 先古卡预览时崩溃卡住**（`get_EnergyIcon → get_Pool → MockCardPool.GenerateAllCards` 栈）。参考原版 TokenCardPool 也在 AllSharedCardPools。注册共享池只把池加入目录，不影响"不进角色奖励池"

## 4. 待办（按优先级）

- [ ] **卡图**：GPT 账号额度到位后批量生成（需求清单在 `docs/card-art-brief.md`，星际争霸2陆战队员风格）
- [x] **支援类吃易伤加成（2026-08-11）**：支援伤害（火力支援/燃烧轰炸/女妖/三连击延迟狙击）原走 `ValueProp.Unpowered` 管线（只过格挡、不吃力量），因 `VulnerablePower.ModifyDamageMultiplicative` 仅在 `props.IsPoweredAttack()`（Move 且不带 Unpowered）时生效 → 支援天然不吃易伤。修法：新增 `Entry.SupportDamage(target, baseAmount)` 辅助——保持 Unpowered 管线不变，结算前手动调 `VulnerablePower.ModifyDamageMultiplicative(target, baseAmount, ValueProp.Move, null, null)` 应用易伤倍率（兼容 PaperPhrog/Debilitate 等修正，传入 Move 仅作易伤触发标志，不影响 Unpowered 管线语义）。4 个支援 Power 伤害点全部改经 `Entry.SupportDamage` 结算。中英文卡面描述已注明"吃易伤"（PCK 已更新）
- [x] **新增 2 张烟雾卡（2026-08-11）**：
  - **让我抽抽抽**（MarineChainSmoke）：Common 技能 1费，自获 5 层烟雾（升级 7 层）；RepeatVar(5)
  - **给你吸吸二手烟**（MarineSecondhandSmoke）：Uncommon 技能，**0 费**（2026-08-11 从 1 费改为 0 费），对单目标施加等于自身当前烟雾层数的中毒（原版 PoisonPower）；**升级改为施加"自身与目标烟雾总和"的中毒（用 `IsUpgraded` 分支，费用保持 0 费）**；0 层烟雾时无效果（不施加）。读自身烟雾用 `Owner.Creature.GetPower<MarineSmokePower>()?.Amount ?? 0`
  - 两卡都注册到 MarineSoldierCardPool，中英文本地化已加
- [x] **先发制人改为"自己每状态抽1"（2026-08-11）**：原按**目标**身上状态数抽牌（`cardPlay.Target.Powers`），改为按**自己**状态数抽牌（`Owner.Creature.Powers?.Count`，buff/debuff 都算）。伤害 8→12 升级不变。本地化已同步
- [x] **稀有度调整（2026-08-11）**：先发制人 Common→Uncommon、踏碎 Common→Uncommon（均改 CardRarityValue）。构建部署成功，DLL 确认 (CardRarity)3=Uncommon
- [x] **连肘免费链**（2026-08-09 已实装）：覆写 `TryModifyEnergyCostInCombatLate`，`Owner.PlayerCombatState.PlayPile.Cards` 倒序找最近一张非本卡，`Type==Attack && !Entry.IsShootCategory` → 免费（modifiedCost=0）
- [x] **爆炸肘**（2026-08-09 已实装）：OnPlay 后目标有 `MarineBurnPower` → `CreatureCmd.Damage(choiceContext, target, burn.Amount, ValueProp.Unpowered, null, null)` 立即触发一次燃烧（层数保留，回合末仍结算）
- [x] **狙击**（2026-08-09 已实装对易伤×2）：伤害基数×4/3（目标已有 VulnerablePower 时），Vulnerable 自动×1.5 → 净×2 替代标准 1.5。CanonicalTags 已从 ShootTag 改 SniperTag（不吃兴奋剂）
- [x] **战术六连**：弹药不足时打不出（已用 SecondaryCosts 强制弹药成本实现）
- [x] **原型全部 58 张卡已实装**（2026-08-09）：最后一批大系统=游击战术(弹药消耗→格挡)、固定机枪(丢手牌生成机枪扫射临时卡)、势不可挡(击杀/破挡免费打非射击攻击)、三连击(隐身+2段狙击+下回合延迟狙击)
- [ ] **专属敌人与地图**（用户明确搁置，专注角色与卡牌）
- [x] **卡牌偏差修正（2026-08-09 已完成）**：动力强化=每回合多抽1张（原力量+2）、撤退=加结束回合+下回合补弹、猛烈发狂=X费耗光弹药+X次额外、疾风肘击=X费X次、精准点射=1费最多3弹逐发7伤升级4弹10伤
- [x] **9 张新卡实装（2026-08-09）**：快速机动/快速翻滚/过载供能/战地手术/扩容弹夹/燃烧弹药/战术突进/电磁增压/驻防。实现要点：下回合格挡=BlockNextTurnPower、下回合能量-1=自定义 ModifyEnergyGain power（EnergyNextTurnPower 不支持负值）、束缚=自定义 ShouldPlay 拦攻击卡、消耗=CanonicalKeywords=[CardKeyword.Exhaust]（CardTag 无 Exhaust）、弹药上限=实现 ISecondaryResourceHookListener.ModifyMaxSecondaryResource、射击附加燃烧=AfterDamageGiven 参考 EnvenomPower、下张攻击免费=FreeAttackPower、开火=自定义 ModifyDamageAdditive 返回增量、驻防=BlurPower+RetainHandPower
- [x] **电磁增压调整（2026-08-10）**：改为**罕见能力卡**（Skill/Common → Power/Rare）；**开火不再随回合减少**（MarineFirePower 移除 AfterSideTurnEnd 清除，改为战斗持续 buff，可叠层）。本地化已同步
- [x] **排气散热调整（2026-08-10）**：丢牌改为**消耗一张手牌**（`CardCmd.Discard` → `CardCmd.Exhaust(choiceContext, card, false, false)`；选牌提示用 `ExhaustSelectionPrompt`）。本地化中英已同步
- [x] **烟雾机制调整（2026-08-10）**：默认每回合减少一半（向下取整，至少减1）；有烟雾弥漫则每回合 -1。烟雾弥漫文本修正为"烟雾变为每回合 -1"
- [x] **批量数值调整（2026-08-10）**：快速机动8/12、翻滚换弹12/16、连肘12/15、爆炸肘1费8/11、汽油弹6/9浸油+虚弱1/2（PowerVar<WeakPower>）、燃烧轰炸燃烧施加2次（每次轰炸都施加）、战术突进12/16、驻防13/16、兴奋剂升级抽3（{Cards}动态显示）
- [x] **新增 5 张卡（2026-08-10）**：防御姿态（1费8挡下回+1能量升级12挡=EnergyNextTurnPower）、霸凌（0费6伤每状态抽1升级9）、幻彩射击（0费1弹6伤每状态重复9伤+兴奋剂每次重复触发一次，IntVar("RepeatDamage",9)）、先发制人（0费1弹8伤每状态抽1升级12）、紧急散热（1费8挡3烟升级12挡4烟）。状态数 = target.Powers.Count（buff+debuff 都算）
- [x] **功夫机甲加强（2026-08-10）**：每次非射击攻击造成伤害（每段打击）获得 4 格挡 + 所有敌人失去 2 生命；升级 5 格挡 + 3 生命。**失去生命 = `ValueProp.Unblockable`**（绕过格挡直接扣血、非攻击不吃力量、烟雾不减免——MarineSmokePower 只在 IsPoweredAttack 时生效）。MarineKungfuPower 增加 BlockAmount/HpLossAmount 两个实例属性，卡打出时 GetPower 后设置（升级值更大）
- [x] **功夫机甲修正（2026-08-10）**：触发时机从 `AfterCardPlayed`（每张卡一次）改为 **`AfterDamageGiven`（每段伤害一次）**——X 费多次打击/多段攻击的每一段都触发一次功夫机甲效果。本地化（中英卡+power）已同步
- [x] **初始遗物更名 + 先古升级系统（2026-08-10）**：
  - 初始遗物显示名改为「CMC-400动力装甲」（类名 CmcArmorRelic 不变，仅本地化改动）
  - 新遗物「CMC皇家卫队」CmcRoyalGuardRelic：精炼升级版（+2 弹药/回合、能量上限+4、每回合恢复4），`[RegisterTouchOfOrobasRefinement]` 标注在 CmcArmorRelic 上
  - 新先古卡「超级强化剂」MarineSuperStim：兴奋剂超越版（0费、2恢复+抽3+1兴奋剂，升级3恢复/2兴奋剂），`[RegisterArchaicToothTranscendence]` 标注在 MarineStim 上
  - 中英本地化（cards.json/relics.json）已同步；构建通过（RunPckExport=false，PCK 导出需 Godot Mono 运行时完整环境）
  - 待游戏实测：通关后 Orobas 对话出现 ArchaicTooth/TouchOfOrobas 奖励并正确转换
- [x] **联机串扰大修 + 文本修复（2026-08-10）**：
  - 卡牌乱码：凝固汽油弹 key `MARINE_NAPLAM`→`MARINE_NAPALM`（zhs+eng）
  - 弹夹文本：新建 static_hover_tips.json（弹药 hover tip，zhs+eng）
  - 能量上限7：CmcArmorRelic/CmcRoyalGuardRelic/CoreExpansionPower/MarineOutputAdjustPower/MarineEnergyDownNextTurnPower 全部加 owner 检查
  - 护甲消失：MarineArmorPower.ModifyHpLostBeforeOsty 参数顺序修正（target/dealer）
  - 队友消耗弹药：MarineStimPower.AfterCardPlayed 加 `cardPlay.Card.Owner` 检查
  - 其他加 owner 检查：MarineBansheePower/MarineFireBombardmentPower/MarineFireSupportPower/MarineTripleStrikeSupportPower/MarineNextTurnReloadPower/MarineSmokePower/MarineInvisiblePower/MarineShacklePower/MarineBurningOilPower
- [x] **敌人烟雾衰减修复（2026-08-11）**：上轮联机 owner 检查误伤——`AfterPlayerTurnStart` 里 `Owner.Player != player` 对敌人恒真（敌人无 Player），敌人烟雾永不衰减。拆分为：玩家烟雾在 `AfterPlayerTurnStart`（owner 检查）衰减；**敌人烟雾在 `AfterSideTurnEnd`（side==Enemy）衰减**（每轮恰好一次，天然联机安全）。"烟雾弥漫"对敌人烟雾改为任意玩家持有即生效
- [x] **稀有度调整（2026-08-11）**：霸凌 Rare→Uncommon（罕见）、全息诱饵 Uncommon→Rare（稀有）。仅改 CardRarityValue，数值/费用/机制不变
- [x] **势不可挡费用 3→1（2026-08-11）**：`BaseEnergyCost` 3→1，机制不变（击杀/破挡免费打非射击攻击）。本地化无费用字样无需改，构建部署成功
- [x] **电磁增压开火层数 2→3、升级 4（2026-08-11）**：`RepeatVar(2)`→`RepeatVar(3)`，升级 +1（3→4）。本地化用 `{Repeat}` 占位符无需改，构建部署成功
- [x] **防御靶机改消耗卡 + 弹药上限 6→8（2026-08-11，后续改为 9 弹/每3弹1缓冲）**：加 `CanonicalKeywords => [CardKeyword.Exhaust]`；OnPlay 额外消耗 `Math.Min(4→6, ...)`，总共最多 8 弹 → 最多 4 层缓冲。本地化"消耗最多 6 弹药"→"消耗。消耗最多 8 弹药"（中英），构建部署成功。**2026-08-11 终版改为每3弹1层缓冲、最多9弹**：`Math.Min(6→7,...)`，总最多 9 弹 → 最多 3 层缓冲。本地化已改"最多 9 弹药（最少 2）：每消耗 3 弹药获得 1 层缓冲"。**⚠️ 2026-08-11 会话末再改：用户明确要【无实体】不是缓冲**——实现改 `Entry.ApplyPower<IntangiblePower>`（原版，受击伤害 cap 到 1、每回合 -1），文本同步"无实体"。已部署 20:02。详见 §6"防御靶机改为无实体"

## 5. 当前已知问题（用户待测）

- **本轮 6 项已修复/新增待用户游戏实测（2026-08-11 会话末）**：①弹夹联机（队友不再享受我的弹夹、弹药不超上限）；②战斗角色视觉（进战斗能看到、联机两人正常间距）；③超级强化剂卡图（原比例不拉伸）；④卡牌能量宝石（青蓝）；⑤焚风卡（烟雾→燃烧、升级全体）；⑥防御靶机无实体/烟雾弥漫只对自己/势不可挡升级施异常触发。**新会话开场先问用户这些测完没、有没有新报错**，再读 §0 摘要
- 卡图全部是占位图（除星际争霸2风格样张 `docs/art-samples/`）
- 能量机制与"每回合+1能量"遗物的兼容性：CMC 走 GainEnergy 管线，能量+1遗物会加到 gain 上（设计如此）
- ~~燃烧联机双倍结算隐患~~ **（2026-08-11 反编译核实：不存在，已澄清）**：玩家回合在联机中是一起结算的（`SetReadyToBeginEnemyTurn` 等所有玩家 ready 才调 `AfterAllPlayersReadyToBeginEnemyTurn` → `EndPlayerTurnPhaseTwoInternal` → `Hook.AfterTurnEnd(side=Player)` 每轮恰好一次），`AfterSideTurnEnd(side==Player)` 不会每个玩家各触发一次。MarineBurnPower 挂敌人身上用 `side==Player` 结算 → 每轮一次，数值正确。**勿改成 side==Enemy**（会改变单机"玩家回合末先结算燃烧、敌人后行动"时序且无必要）。教训：hook"广播到所有玩家实例"≠"每玩家各触发一次"
- **先古奖励待实测（2026-08-11 更新）**：~~Orobas 先古 NPC 的 ArchaicTooth（兴奋剂→超级强化剂）/TouchOfOrobas（CMC-400→CMC皇家卫队）映射已注册（AutoRegister 107/107 成功），但尚未实际通关触发 Orobas 对话验证转换~~ **2026-08-11 修复先古卡预览崩溃**：原 OROBAS 事件 hover 先古卡必崩卡住（`MarineSoldierTokenCardPool` 未注册共享池 → `get_Pool` fallback 到 MockCardPool 触发游戏本体 "You monster!" 断言；表现即"选了卡住/卡乱码或不存在"，实际本地化 key 无误）。已给 Token 池加 `[RegisterSharedCardPool]` 并重新构建部署（DLL/PCK 已更新）。**仍需实际通关触发 Orobas 对话验证转换**
- **三连击升级未达注释预期**：~~注释写"升级延迟伤害 20→25"，但 `OnUpgrade` 只升级了 `DamageVar`（近战狙击伤害 12→15），延迟狙击的 `MarineTripleStrikeSupportPower` 仍是固定 20~~ **2026-08-11 已修复**：延迟伤害改为 `IntVar("DelayDamage", 20)` 变量，升级 +5（20→25），与注释一致

## 5.5 支援类数值变量化（2026-08-11，用户原则：尽量不写死、后续可能加影响变量）

所有支援类伤害数值改为**卡牌 DynamicVars 驱动 + power 实例属性**（参考功夫机甲 BlockAmount/HpLossAmount 模式），杜绝 power 内写死：
- **燃烧轰炸**：伤害 `RepeatVar(2)`、次数 `IntVar("Hits", 2)`、燃烧 `IntVar("Burn", 4)`；升级伤害 2→3 + 燃烧 4→6（本次需求）。power 属性 `Hits`/`BurnAmount`
- **火力支援**：伤害 `RepeatVar(2)`、次数 `IntVar("Hits", 5)`；升级伤害 2→3。power 属性 `Hits`
- **女妖**：层数 `RepeatVar(2)`、主目标 `IntVar("FrontDamage", 8)`、第二目标 `IntVar("SecondDamage", 4)`、段数 `IntVar("Hits", 2)`；升级层数 2→3（精英）。power 属性 `FrontDamage`/`SecondDamage`/`Hits`
- **三连击**：近战 `DamageVar(12)`、延迟狙击 `IntVar("DelayDamage", 20)`；升级 15 + 25。延迟伤害经 power Amount 传递（原写死 20）
- power 内**不再有写死数值**（未设置时 0，安全不触发）；本地化用 `{Hits}`/`{Burn}`/`{FrontDamage}`/`{SecondDamage}`/`{DelayDamage}` 占位符

## 6. 其他

- **弹夹/扩容弹夹上限 hook 串扰 + 超出上限（2026-08-11 已修复）**：用户联机实测暴露两个现象——①"只有我有弹夹，队友上限也跟着+2"、②"私人改装队友获得扩容弹夹3"、③"弹药会超出上限"。**根因**：`ModifyMaxSecondaryResource` 监听器在 RitsuLib 里是**全玩家广播**的——计算任意玩家的弹药上限时会遍历**所有玩家**的遗物+power，而 `MarineMagazineRelic`(+2)/`MarineAmmoCapPower`(+Amount) 都没做 owner 检查 → 我的遗物/power 加成漏给了全队。**注意：这同时证明上限 hook 其实是生效的**（能广播说明被收集了），之前单机"6/5"时上限 hook 也正常，真正问题只是 owner 过滤 + 另一处 Gain 不封顶。**修法**：① 弹夹遗物加 `if (context.Player != Owner) return max;`（RelicModel.Player==Owner）；② 扩容弹夹 power 加 `if (context.Player?.Creature != Owner) return max;`（PowerModel.Owner 是 Creature）；③ **弹药 Gain 封顶**：RitsuLib `SecondaryResourceCmd.Gain` 只 clamp 到 hardMax=99 不 clamp 动态上限 → 新增 `Entry.GainAmmo(player, amount, source)`（先 GetMax 再 Math.Min 差额，已满不再涨），CMC/CmcRoyalGuard/HoldLine 全部改走它；④ 游击战 power 的 `AfterSecondaryResourceSpent` 加 `context.Player.Creature != Owner` 检查（防队友耗弹触发我的格挡）。构建部署 18:36。
- **超级强化剂卡图（2026-08-11 最终方案）**：用户原图竖版 1024×1536。**先古卡整幅卡面都是贴图、没有外框（NCard 有独立 `%AncientPortrait` 节点，普通卡才是横版贴图区）**，所以先古卡贴图应该用**原始竖版图直接放**（卡面等比缩放显示），**不要转成 1000×760 横版**。曾三次误做：①裁剪成横版 1024×778 变形；②合成 1000×760 模糊背景铺满；③1000×760 透明居中。用户明确要"原比例不拉伸、等比缩放到卡上"。已恢复原始 1024×1536 直接部署（PCK 18:46）。原图副本在 `Data\Files\019ff03d-0f3b-73a3-b2b4-539b1052b96c.png` 可找回。
- **卡牌能量费用宝石换成陆战队员宝石（2026-08-11）**：卡牌右上角能量费用宝石的链路 = `CardPoolModel.EnergyIconPath` → `EnergyIconHelper.GetPath(EnergyColorName)` → 默认 `ui_atlas.sprites/card/energy_{color}.tres`（原版角色宝石图集）→ **RitsuLib patch 用 `BigEnergyIconPath` 替换**（各池已设 `energy_big.png`）。之前 `energy_big.png` 一直是**原版红色宝石**（未换）。已用 orb 层合成陆战队员青蓝宝石覆盖 `energy_big.png`（256×256）+ `energy_text.png`（24×24 缩略），清 .import 缓存，构建部署（PCK 18:57）。**注意**：`MarineSoldier_energy_orb_layer_1..5.png` 是能量计数器动画层（tscn 的 Layer1底/Layer2-3旋转/Layer4-5叠加），不是卡牌费用宝石来源；合成用非旋转层 Layer1+Layer4+Layer5 正常混合。
- **战斗角色视觉"看不到/联机离得远"修复（2026-08-11，重要）**：`TryCreateCreatureVisuals` 从贴图创建战斗视觉时，**贴图必须预缩放到战斗尺寸（约 250×250，同原版角色 Bounds）**，因为 **RitsuLib `RitsuNCreatureVisualsNodeFactory.FromTexture` 把 Bounds 尺寸设为贴图像素×1.1**（`val = img.GetSize() * 1.1f`），而 **`NCombatRoom.PositionPlayersAndPets` 用 `Visuals.Bounds.Size.X` 布局玩家位置与间距**。若用 1254×1254 大图 → Bounds=1379（原版 Ironclad 约 242×278）→ **玩家被推出屏幕外（看不到角色）、联机两人间距巨大（离得远）**。**不能用 `visuals.Scale` 缩小**——它只影响渲染（Scale 设 0.2 后 Sprite 显示 250px），但布局仍用 Bounds 原始 1379。正确做法：贴图本身缩到 250×250（原图备份 `MarineSoldier_character_combat_original.png`），去掉 `visuals.Scale`。构建部署 19:28。**日志血条 NaN（`!std::isfinite(p_size)`，来自 RitsuLib `SyncHpBarToHitbox`）多为战斗视觉异常的伴生**，角色修复后应消失。
- **势不可挡升级（2026-08-11）**：升级后额外在**对敌人施加异常状态（debuff）**时也触发随机免费攻击。实现：`MarineUnstoppablePower` 加 `TriggerOnDebuff` 属性 + override `AfterPowerAmountChanged`（`power.Type==Debuff`、`power.Owner` 是敌人、`applier==Owner` 联机保护、`amount>0`）；卡 `MarineUnstoppable.OnPlay` 按 `IsUpgraded` 设置属性（KungfuMech 模式）；`PlayRandomNonShootAttack` 加 `_isTriggering` 防递归（触发链中打出的随机攻击不再连锁，避免无限循环）。中英卡面描述更新。构建部署 19:59，反编译确认全部进 DLL。
- **防御靶机改为无实体（2026-08-11，修正实现与需求不符）**：用户之前明确要求"每 3 弹 1 层无实体最多 9 弹"（2026-08-11 消息 #35），但实现做成了**缓冲 BufferPower**、文本也写"缓冲"——实现与需求不符。已改为**原版 `IntangiblePower`（无实体）**：`Entry.ApplyPower<IntangiblePower>`（`MegaCrit.Sts2.Core.Models.Powers` 命名空间）。**原版无实体语义（反编译确认）**：`ModifyHpLostAfterOsty` 把受击伤害 cap 到 1，`AfterSideTurnEnd(side==Enemy)` 每回合 `Decrement` -1。中英文本同步改为"无实体…攻击伤害降为 1，每回合 -1 层"。构建部署 20:02，反编译确认进 DLL。
- **焚风（MarineFoehnWind，2026-08-11 新增）**：Uncommon 罕见技能卡 2 费，**对一名敌人施加等与你烟雾层数的燃烧，然后消耗你的全部烟雾**；**升级改为对所有敌人施加**。实现要点：**`TargetType` 是 `CardModel` 的 virtual 属性**——`public override TargetType TargetType => IsUpgraded ? TargetType.AllEnemies : TargetType.AnyEnemy` 实现升级后目标切换（基础单目标 2 / 升级全体 3，反编译显示为数值 `(IsUpgraded?3:2)`）；OnPlay 读自身烟雾 `Owner.Creature.GetPower<MarineSmokePower>()?.Amount ?? 0`（0 层无效果不消耗），燃烧经 `Entry.ApplyBurn`（触发浸油），消耗烟雾用 `await PowerCmd.Remove(smoke)`（清空自身烟雾）；升级分支遍历 `CombatState.Enemies` 全体施放。中英本地化已加。构建部署 20:05。
- **烟雾弥漫只对自己生效（2026-08-11）**：原实现敌人烟雾也因"任意玩家持有烟雾弥漫"而每回合 -1（`MarineSmokePower.AfterSideTurnEnd` 检查 `CombatState.Players.Any(HasPower<MarineSmokePersistPower>)`）。已改为**只对持有者自己的烟雾生效**：敌人烟雾始终默认减半（`AfterSideTurnEnd` 直接 `DecaySmoke(false)`），玩家烟雾衰减逻辑不变（`AfterPlayerTurnStart` 检查自己 `HasPower`）。中英文本改为"你的烟雾不再每回合清零，改为每回合 -1（只影响你自己）"。构建部署 20:02，反编译确认进 DLL。
- 用户偏好：直接做、做完报告，别反复询问；游戏 mod 相关修改无需确认
- 网页版原型在 `marine-game/` 根目录（battle.html/cards.json），机制参考它
- 用户之前提过：星际争霸2陆战队员是角色风格方向
