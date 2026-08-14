---
title: 06_testing · 测试记录
page_type: testing
lifecycle: active
updated: 2026-08-14
---

## 最新记录

- [2026-08-14 共享 settlement-derived trigger、Ironclad Juggernaut 与机枪兵 Unstoppable](2026-08-14-shared-settlement-trigger-juggernaut-unstoppable-runtime.md) — 基础运行时、强枚举、Queue 表现屏障、正式表 / AB 与 Unity 原生验证已收口；全项目 98/70、Ironclad 15/70、Marine 82/0、Effect 19，定向 7/7、完整 EditMode 807/807
- [2026-08-14 共享触发出牌、Ironclad Havoc 与机枪兵 Opportunistic Strike](2026-08-14-shared-triggered-play-havoc-opportunistic-strike-runtime.md) — Queue-owned system-token continuation、DrawPile 顶牌免费强制 Exhaust 与前置 Attack/Shoot 后随机手牌 Attack 均完成正式表、真实 AB 与 Unity 原生验证；定向 8/8、完整 EditMode 802/802
- [2026-08-14 共享 Block 保留、Ironclad Barricade 与机枪兵 Garrison](2026-08-14-shared-block-retention-barricade-garrison-runtime.md) — 永久 / 计时 Prepare-Validate-Commit、Garrison 精确双选与一次行动手牌保留均完成正式表、Localization、真实 AB 与 Unity 原生验证；定向 300/300、完整 EditMode 798/798
- [2026-08-14 共享来源动态伤害、Poison、Ironclad Body Slam 与机枪兵 Secondhand Smoke](2026-08-14-shared-source-magnitude-poison-body-slam-secondhand-smoke-runtime.md) — 来源 Block 普通攻击、来源 Smoke→Poison、玩家/敌人行动开始 tick、20 种状态计数与冻结归宿已完成正式表、Localization、真实 AB 与 Unity 原生验证；定向 9/9、完整 EditMode 793/793，未实现常驻 Poison HUD
- [2026-08-13 共享重复伤害、Ironclad Sword Boomerang 与机枪兵幻彩射击](2026-08-13-shared-repeated-damage-sword-boomerang-prismatic-shot-runtime.md) — concrete Prepare/Validate/Commit 计划、Turn-owned CardTarget RNG、3116 RandomEnemy 3×3 与 3279 `[6,9×S]`/Stim/main→Burn→Helper 均通过；回归代表 5/5、行为 243/243、正式 53/53、完整 EditMode 776/776
- [2026-08-13 共享 Heal、Ironclad Not Yet 与机枪兵战地手术](2026-08-13-shared-heal-not-yet-field-surgery-runtime.md) — `Heal=6`、封顶/零实际治疗、3171 2E→Heal10→Exhaust、3231 Regeneration5+Shackle1 与来源顺序均通过；正式目录 50/50、治疗视图 1/1、含真实 AB 聚合 243/243、完整 EditMode 766/766
- [2026-08-13 STS2 Ironclad Burning Pact 与通用选择消耗抽牌事务](2026-08-13-sts2-ironclad-burning-pact-runtime.md) — 3125 基础 1E、可选另一手牌 Exhaust 后抽 2、无候选仍支付/抽牌、Hand10 只抽 1 与来源最后 Discard 均通过；Ironclad 9/76，正式行为 9/9、目录 22/22、含真实 AB 聚合 172/172、完整 EditMode 754/754 通过
- [2026-08-13 Marine Game 机枪兵 V2V 排气散热与共享手牌单选协议](2026-08-13-machine-gunner-v2v-vent-heat-runtime.md) — 3244 基础 0E、选另一手牌 Exhaust 后实际 +1 Energy、来源 Discard；唯一手牌/满能、原子双归宿、UI 会话与双 transient 均通过；目录 76/6、V1 60/4、V2 16/2，正式聚合 306/306、完整 EditMode 744/744 通过
- [2026-08-13 Marine Game 机枪兵 V2U 不解释12连两波换弹射击](2026-08-13-machine-gunner-v2u-twelve-hits-runtime.md) — 3257 基础 3E/5 伤两波、0 Ammo 后换弹、自动最近敌人、Stim/Incendiary/Helper 与免费 nominal 13 均通过；目录 75/7、V1 59/5、V2 16/2，正式聚合 220/220、完整 EditMode 728/728 通过
- [2026-08-13 Marine Game 机枪兵 V2T 战术推进二元免攻与共享费用冻结](2026-08-13-machine-gunner-v2t-tactical-advance-runtime.md) — 3234 基础 2E/10 Block 后刷新跨回合二元免攻，Skill/失败不消费、成功 Attack 归宿后消费；Fixed/X actual/effect/nominal 与 Ammo/Stim/Guerrilla 组合通过，目录 74/8、V1 58/6、V2 16/2，正式聚合 213/213、完整 EditMode 721/721 通过
- [2026-08-13 STS2 Ironclad 首批四张基础卡与通用 DrawCards](2026-08-13-sts2-ironclad-first-four-effect-runtime.md) — Pommel 9+Draw1、Shrug Block8+Draw1、Twin 5×2、Bludgeon 32；Ironclad 8/77，Luban/Localization/Sync/Addressables（13.595 秒）、正式 smoke 20/20、聚合 67/67 与完整 EditMode 713/713 通过
- [2026-08-12 Marine Game 机枪兵 V2S 先发制人按来源起始状态种类抽牌](2026-08-12-machine-gunner-v2s-preemptive-strike-runtime.md) — 3277 基础 8 点普通 Attack 后按命令起始来源状态种类冻结普通抽牌，PreparedDraw 保证容量/重洗/随机/布局原子性并在目标致死后仍抽；目录 73/9、V1 57/7、V2 16/2，Addressables 13.966 秒，正式聚合 214/214、完整 EditMode 702/702 通过
- [2026-08-12 Marine Game 机枪兵 V2R 霸凌按目标起始状态种类抽牌](2026-08-12-machine-gunner-v2r-bully-runtime.md) — 3278 基础 6 点普通 Attack 后按命令起始目标状态种类冻结普通抽牌，PreparedDraw 保证容量/重洗/随机/布局原子性；目录 72/10、V1 57/7、V2 15/3，Addressables 16.521 秒，正式聚合 209/209、完整 EditMode 697/697 通过
- [2026-08-12 Marine Game 机枪兵 V2P 机枪扫射基础态](2026-08-12-machine-gunner-v2p-machinegun-burst-runtime.md) — 3263 基础态翻为 Implemented；0E、随机 5×2 逐段重选、实际 Ammo 0 / 游击名义 2、Exhaust，显式排除 Shoot 与普通非射击联动；目录 70/12、V1 56/8、V2 14/4，Luban/本地化/Sync/Addressables（11.757 秒）、Unity 定向 154/154、域重载探针 1/1 与完整 678/678 通过；3261 生产生成入口仍未实现
- [2026-08-12 Marine Game 机枪兵 V2O 隐秘行动与固有起手](2026-08-12-machine-gunner-v2o-stealth-action-innate-runtime.md) — 3275/Innate 已使目录达到 69/13、V1 55/9、V2 14/4；Luban、本地化、Sync/Addressables（18.363 秒）、正式快照 21/21、聚合定向 237/237 与完整 EditMode 673/673 均通过
- [2026-08-12 Marine Game 机枪兵 V2N 极限过载基础态](2026-08-12-machine-gunner-v2n-limit-overload-runtime.md) — 3260 基础态翻为 Implemented；以 CardZones Prepare/Validate/Commit 按当前牌离手后投影抽至 10，只重洗旧弃牌并排除自抽；目录 68/14、V1 55/9、V2 扩展 13/5，Luban/本地化/`Sync and Build All` 通过，Addressables 15.828 秒，Unity MCP 定向 169/169 与完整 659/659 通过
- [2026-08-12 Marine Game 机枪兵 V2M 天空之怒基础态](2026-08-12-machine-gunner-v2m-sky-wrath-runtime.md) — 3266 基础态翻为 Implemented；四类原始 Support 按 hit/wave 粒度触发逐层随机 8/4，支持 Bombard 缩放并排除 Needle/Bomb/Burn/即时/帮手；目录 67/15、V1 54/10、V2 扩展 13/5，Luban/本地化/`Sync and Build All` 通过，Addressables 12.956 秒，Unity MCP 定向 139/139 与完整 650/650 通过
- [2026-08-12 Marine Game 机枪兵 V2L 狂轰滥炸基础态](2026-08-12-machine-gunner-v2l-bombard-runtime.md) — 3265 基础态翻为 Implemented；目录 66/16、V1 54/10、V2 扩展 12/6；Luban/本地化/`Sync and Build All` 通过，Addressables 12.963 秒，Unity MCP 定向 134/134 与精确真实加载 1/1 通过；两次完整任务均只保留同一 180 秒冷加载 timeout，不写为完整套件通过
- [2026-08-12 Marine Game 机枪兵 V2K 便携帮手基础态](2026-08-12-machine-gunner-v2k-portable-helper-runtime.md) — 3267 基础态翻为 Implemented；目录 65/17、V1 54/10、V2 扩展 11/7；Luban/本地化/`Sync and Build All` 通过，Addressables 12.163 秒，Unity MCP 定向 EditMode 120/120 与完整 EditMode 639/639 通过；Shotgun/延迟排除仅记结构证据
- [2026-08-12 Marine Game 机枪兵 V2J 回合能量修正基础态](2026-08-12-machine-gunner-v2j-round-energy-runtime.md) — 3213、3271 基础态翻为 Implemented；目录 64/18、V1 54/10、V2 扩展 10/8，3260 因缺少“抽至满手”卡区 seam 延期；Luban/本地化/`Sync and Build All` 通过，Addressables 11.72 秒，补强后 Unity MCP 定向 EditMode 136/136 与完整 EditMode 631/631 通过
- [2026-08-12 Marine Game 机枪兵 V2I 充能爆射基础态](2026-08-12-machine-gunner-v2i-charged-burst-runtime.md) — 3282 基础态翻为 Implemented；目录 62/20、V2 扩展 9/9，Luban/本地化/`Sync and Build All` 通过，Addressables 11.456 秒，Unity MCP 定向 EditMode 94/94 与完整 EditMode 622/622 通过
- [2026-08-12 Marine Game 机枪兵 V2H 焚风基础态](2026-08-12-machine-gunner-v2h-foehn-wind-runtime.md) — 3276 基础态翻为 Implemented；Luban/本地化/`Sync and Build All` 通过，Addressables 12.164 秒，Unity MCP 定向 EditMode 89/89 与完整 EditMode 617/617 通过
- [2026-08-12 Marine Game 机枪兵 V2G 私人改装基础态](2026-08-12-machine-gunner-v2g-private-mod-runtime.md) — 3268 基础态翻为 Implemented；Luban/本地化/最终 `Sync and Build All` 通过，Addressables 4.376 秒，Unity MCP 定向 EditMode 85/85 与完整 EditMode 613/613 通过
- [2026-08-12 Marine Game 机枪兵 V2F 烟雾、防御与标记即时卡](2026-08-12-machine-gunner-v2f-smoke-block-mark-runtime.md) — 3269、3272、3280 基础态翻为 Implemented；Luban/本地化/`Sync and Build All` 通过，Addressables 11.414 秒，Unity MCP 定向 EditMode 83/83 与完整 EditMode 611/611 通过
- [2026-08-12 Marine Game 机枪兵 V2E 延迟效果与支援链](2026-08-12-machine-gunner-v2e-delayed-support-scheduler-runtime.md) — 7 张基础态翻为 Implemented；Luban/本地化/`Sync and Build All` 通过，Addressables 14.252 秒，Unity MCP 定向 EditMode 101/101 与完整 EditMode 606/606 通过
- [2026-08-12 Marine Game 机枪兵 V2D 击退射击与失去力量](2026-08-12-machine-gunner-v2d-knockback-lost-strength-runtime.md) — 3223 翻为 Implemented；Luban/本地化/`Sync and Build All` 通过，Unity MCP 定向 EditMode 10/10 与完整 EditMode 597/597 通过
- [2026-08-12 Marine Game 机枪兵 V2C 破甲即时卡](2026-08-12-machine-gunner-v2c-armor-break-instant-cards.md) — 3273 铝热炸弹与 3281 踏碎翻为 Implemented；Luban/本地化/`Sync and Build All` 通过，Unity MCP 定向 EditMode 81/81 与完整 EditMode 589/589 通过
- [2026-08-12 Marine Game 机枪兵 V2B 82 模板目录扩展](2026-08-12-machine-gunner-v2b-catalog-extension.md) — 18 张新增身份完成目录与精确状态门禁；V2C 仅打开 3273/3281，余 16 张仍 CatalogOnly；初始 Luban/本地化/`Sync and Build All` 与 112/112、586/586 证据保留
- [2026-08-12 Marine Game 机枪兵 V2A 伤害语义与防御靶机](2026-08-12-machine-gunner-v2a-damage-taxonomy-defense-target.md) — 3236 按 V2 更新、3262 翻为 Implemented；Luban/本地化/`Sync and Build All` 通过，Unity MCP 定向 EditMode 110/110 与完整 EditMode 584/584 通过
- [2026-08-12 Marine Game 机枪兵 MG14B 游击战术](2026-08-12-machine-gunner-mg14b-guerrilla-tactics-runtime.md) — 1 张卡精确翻为 Implemented；工作簿/Luban/静态编译/`Sync and Build All` 通过，Unity MCP 定向 EditMode 6/6 与完整 EditMode 574/574 通过
- [2026-08-12 Marine Game 机枪兵 MG14A 撤退与快速翻滚](2026-08-12-machine-gunner-mg14a-retreat-quick-roll-runtime.md) — 2 张卡精确翻为 Implemented；工作簿/Luban/静态编译/`Sync and Build All` 通过，Unity MCP 定向 EditMode 7/7 与完整 EditMode 571/571 通过
- [2026-08-12 Marine Game 机枪兵 MG13 全息诱饵](2026-08-12-machine-gunner-mg13-holo-decoy-runtime.md) — 1 张卡精确翻为 Implemented；工作簿/Luban/静态编译/`Sync and Build All` 通过，Unity MCP 定向 EditMode 119/119 通过
- [2026-08-11 Marine Game 机枪兵 MG12 光学迷彩](2026-08-11-machine-gunner-mg12-optical-camo-runtime.md) — 1 张卡精确翻为 Implemented；工作簿/Luban/静态编译/`Sync and Build All` 通过，Unity MCP 定向 EditMode 75/75 通过
- [2026-08-11 Marine Game 机枪兵 MG11 爆炸肘](2026-08-11-machine-gunner-mg11-explosive-elbow-runtime.md) — 1 张卡精确翻为 Implemented；工作簿/Luban/静态编译/`Sync and Build All` 通过，Unity MCP 定向 EditMode 70/70 通过

- [2026-08-08 Marine Game 机枪兵 MG10B 不充分爆燃](2026-08-08-machine-gunner-mg10b-incomplete-combustion-runtime.md) — 1 张卡精确翻为 Implemented；工作簿/Luban/静态编译/`Sync and Build All` 通过，Unity MCP 定向 EditMode 65/65 通过

- [2026-08-07 Marine Game 机枪兵 MG10A 烈火烹油](2026-08-07-machine-gunner-mg10a-burning-oil-runtime.md) — 1 张卡精确翻为 Implemented；工作簿/Luban/Sync and Build All 成功，Unity MCP 定向 EditMode 60/60 通过

- [2026-08-07 Marine Game 机枪兵 MG9 逐段命中后置状态](2026-08-07-machine-gunner-mg9-per-hit-runtime.md) — 3 张卡精确翻为 Implemented；工作簿/Luban 复核完成，Unity MCP 定向 EditMode 57/57 通过；同步菜单调用已成功发起，Console 未返回可存档完成行

- [2026-08-07 Marine Game 机枪兵 MG8 功夫机甲、开火与连肘](2026-08-07-machine-gunner-mg8-kungfu-firepower-combo-runtime.md) — 3 张卡精确翻为 Implemented；工作簿/Luban/Sync and Build All 成功，Unity MCP 定向 EditMode 51/51 通过

- [2026-08-07 Marine Game 机枪兵 MG7 Burn/Oil 生命周期与首批依赖卡](2026-08-07-machine-gunner-mg7-burn-oil-runtime.md) — 4 张燃烧/油料卡精确翻为 Implemented；工作簿/Luban/Sync and Build All 成功，Unity MCP 定向 EditMode 37/37 通过

- [2026-08-07 Marine Game 机枪兵 MG6 已有 Power 程序门禁](2026-08-07-machine-gunner-mg6-existing-power-runtime.md) — 6 张已有完整 Power 程序精确翻为 Implemented，工作簿/Luban/Sync and Build All 成功，定向 EditMode 3/3 通过

- [2026-08-07 Marine Game 机枪兵 MG5 即时状态首批运行时](2026-08-07-machine-gunner-mg5-immediate-status-runtime.md) — 5 张即时状态程序精确翻为 Implemented；Luban/Sync and Build All 成功，原生 Unity Test Runner 定向 2/2 通过，并记录 Unity MCP 测试任务状态偏差
- [2026-08-07 Marine Game 机枪兵 MG5 X 费与多段射击运行时](2026-08-07-machine-gunner-mg5-x-multishot-runtime.md) — 11 张新增程序精确翻为 Implemented；X=0、支付快照、Stim 与随机多段投影均通过，Luban/Sync and Build All 成功，定向 EditMode 15/15 通过

- [2026-07-30 BattleScene LifetimeScope](2026-07-30-battle-lifetime-scope.md)

# 06_testing · 测试记录

- 角色：NUnit 用例说明、验收范围、回归结果。
- 与实现计划（`../plans/`）对应，记录"验证了什么、结论如何"。
- 当前状态见状态源 `../SESSION_LOG.md`。

## 验证记录

- [2026-08-13 共享重复伤害、Ironclad Sword Boomerang 与机枪兵幻彩射击](2026-08-13-shared-repeated-damage-sword-boomerang-prismatic-shot-runtime.md) — 通用随机/固定目标 planner 与职业 hit-sequence 适配边界、每击存活候选、RNG 原子性、Stim 全额 Ammo、固定目标死亡停止和既有 MG settlement 前缀回归修复均已收口；正式表/Luban/Localization/Sync/BuildLayout 与 Unity 11/11、53/53、5/5、243/243、776/776 通过
- [2026-08-13 共享 Heal、Ironclad Not Yet 与机枪兵战地手术](2026-08-13-shared-heal-not-yet-field-surgery-runtime.md) — 普通 Effect 与 Regeneration 共用封顶 outcome、prepared 内部生命写入口、`BattleHealthRestoredSettlement` 与正实际值表现；Not Yet 满血仍记录 0 并 Exhaust，Field Surgery 按 `Shackle→LoseStrength→Heal→Regeneration-1→Bomb→Burn`，正式 9/9、50/50、视图 1/1、含真实 AB 243/243、完整 766/766 已收口
- [2026-08-13 STS2 Ironclad Burning Pact 与通用选择消耗抽牌事务](2026-08-13-sts2-ironclad-burning-pact-runtime.md) — `ExhaustSelectedHandCard=5` 与选择后 Draw 语法、Rules→UI Session→Queue 协议、selected Exhaust→重洗/Draw→source Discard 单 Layout 深事务、零候选/Hand10/RNG 漂移/表现顺序均通过；正式表/Luban/Localization/Sync/BuildLayout 与 Unity 9/9、22/22、172/172、754/754 已收口
- [2026-08-13 Marine Game 机枪兵 V2V 排气散热与共享手牌单选协议](2026-08-13-machine-gunner-v2v-vent-heat-runtime.md) — 3244 的 selected→Exhaust、实际获能、source→Discard 与单 Layout 深事务，Layout/Turn/Queue 漂移零写入，Hand UI 确认/取消/禁拖/候选视觉/生命周期和双 transient 均通过；正式表/Luban/Localization/Sync/BuildLayout 与 Unity 15/15、38/38、306/306、744/744 已收口；该 V2V 历史切片未实现战士卡，后续 Burning Pact 已独立复用其 seam
- [2026-08-13 STS2 Ironclad 首批四张基础卡与通用 DrawCards](2026-08-13-sts2-ironclad-first-four-effect-runtime.md) — 四张基础态、有序 Effect 绑定、`DrawCards=4`、PreparedDraw 的手牌上限/旧弃牌重洗/随机快照/致死仍抽/失败零写入均通过；正式表/Luban/Localization/Sync/BuildLayout 与 Unity 20/20、67/67、713/713 已收口
- [2026-08-12 Marine Game 机枪兵 V2S 先发制人按来源起始状态种类抽牌](2026-08-12-machine-gunner-v2s-preemptive-strike-runtime.md) — 历史发布时按 Strength/Vulnerable/16 种职业私有状态冻结抽牌；CD-104 追加 Regeneration 后当前为 17 种，Shackle 仍由上游拒绝，其余 16 种可计数；原 V2S 聚合 214/214、完整 702/702 保留
- [2026-08-12 Marine Game 机枪兵 V2R 霸凌按目标起始状态种类抽牌](2026-08-12-machine-gunner-v2r-bully-runtime.md) — 3278 保留 `EnergySpent(0)`，普通 Attack 6 后按命令起始 Strength/Vulnerable/职业私有状态种类抽牌，同种多层只计一次；PreparedDraw、0 状态/满手、致死冻结、失败零写、正式表/Luban/Localization/Sync/Addressables 均通过，正式聚合 209/209、完整 EditMode 697/697
- [2026-08-12 Marine Game 机枪兵 V2Q 固定机枪与临时卡生产](2026-08-12-machine-gunner-v2q-fixed-machinegun-runtime.md) — 3261 基础 10 Block、来源 Exhaust、剩余 Hand 原序 Discard、等量 3263 `CardCreated`→Hand、单次 Layout、`HandToExhaust` / `CreatedToHand` 与异步动态模板预载；Luban/Localization/Sync/Addressables 通过，正式聚合 262/262、完整 EditMode 690/690 通过
- [2026-08-12 Marine Game 机枪兵 V2P 机枪扫射基础态](2026-08-12-machine-gunner-v2p-machinegun-burst-runtime.md) — 3263 基础态、实际零弹耗与游击名义二弹耗分离、逐段随机重选及双侧联动排除；Unity MCP 最终定向 154/154、域重载后 CardArt 1/1、完整 EditMode 678/678 通过
- [2026-08-12 Marine Game 机枪兵 V2O 隐秘行动与固有起手](2026-08-12-machine-gunner-v2o-stealth-action-innate-runtime.md) — 3275 基础态与通用 Innate 首次起手协议；目录 69/13、V1 55/9、V2 14/4，同步构建、正式快照 21/21、最终聚合 237/237 与完整 EditMode 673/673 通过
- [2026-08-12 Marine Game 机枪兵 V2N 极限过载基础态](2026-08-12-machine-gunner-v2n-limit-overload-runtime.md) — 3260 基础态翻为 Implemented；0 费获能后以 CardZones 深事务在当前牌离手后抽至 10，只重洗旧弃牌并排除自抽，最后累计 Penalty +3；Unity MCP 正式定向 169/169 与完整 659/659 通过
- [2026-08-12 Marine Game 机枪兵 V2M 天空之怒基础态](2026-08-12-machine-gunner-v2m-sky-wrath-runtime.md) — 3266 基础态翻为 Implemented；女妖/火力支援按 hit、燃烧轰炸按 wave、三连击延迟段触发天空之怒，每层重取随机主目标并对其余目标造成 Support；Unity MCP 翻表前 117/117、正式定向 139/139 与完整 650/650 通过
- [2026-08-12 Marine Game 机枪兵 V2L 狂轰滥炸基础态](2026-08-12-machine-gunner-v2l-bombard-runtime.md) — 3265 基础态翻为 Implemented；四类 scheduled Support 触发时读取当前层数并按正值 half-up 缩放，其他伤害来源排除；Unity MCP 定向 134/134 与精确真实加载 1/1 通过，完整套件单任务保留相同冷加载 timeout 边界
- [2026-08-12 Marine Game 机枪兵 V2K 便携帮手基础态](2026-08-12-machine-gunner-v2k-portable-helper-runtime.md) — 3267 基础态翻为 Implemented；目录 65/17、V1 54/10、V2 扩展 11/7；Luban/本地化/`Sync and Build All` 通过，Addressables 12.163 秒，Unity MCP 定向 EditMode 120/120 与完整 EditMode 639/639 通过；Shotgun/延迟排除仅记结构证据
- [2026-08-12 Marine Game 机枪兵 V2J 回合能量修正基础态](2026-08-12-machine-gunner-v2j-round-energy-runtime.md) — 3213、3271 基础态翻为 Implemented；目录 64/18、V1 54/10、V2 扩展 10/8，3260 因缺少“抽至满手”卡区 seam 延期；Luban/本地化/`Sync and Build All` 通过，Addressables 11.72 秒，补强后 Unity MCP 定向 EditMode 136/136 与完整 EditMode 631/631 通过
- [2026-08-12 Marine Game 机枪兵 V2I 充能爆射基础态](2026-08-12-machine-gunner-v2i-charged-burst-runtime.md) — 3282 基础态翻为 Implemented；目录 62/20、V2 扩展 9/9，Luban/本地化/`Sync and Build All` 通过，Addressables 11.456 秒，Unity MCP 定向 EditMode 94/94 与完整 EditMode 622/622 通过
- [2026-08-12 Marine Game 机枪兵 V2H 焚风基础态](2026-08-12-machine-gunner-v2h-foehn-wind-runtime.md) — 3276 基础态翻为 Implemented；Luban/本地化/`Sync and Build All` 通过，Addressables 12.164 秒，Unity MCP 定向 EditMode 89/89 与完整 EditMode 617/617 通过
- [2026-08-12 Marine Game 机枪兵 V2G 私人改装基础态](2026-08-12-machine-gunner-v2g-private-mod-runtime.md) — 3268 基础态翻为 Implemented；Luban/本地化/最终 `Sync and Build All` 通过，Addressables 4.376 秒，Unity MCP 定向 EditMode 85/85 与完整 EditMode 613/613 通过
- [2026-08-12 Marine Game 机枪兵 V2F 烟雾、防御与标记即时卡](2026-08-12-machine-gunner-v2f-smoke-block-mark-runtime.md) — 3269、3272、3280 基础态翻为 Implemented；Luban/本地化/`Sync and Build All` 通过，Addressables 11.414 秒，Unity MCP 定向 EditMode 83/83 与完整 EditMode 611/611 通过
- [2026-08-12 Marine Game 机枪兵 V2E 延迟效果与支援链](2026-08-12-machine-gunner-v2e-delayed-support-scheduler-runtime.md) — 7 张基础态翻为 Implemented；Luban/本地化/`Sync and Build All` 通过，Addressables 14.252 秒，Unity MCP 定向 EditMode 101/101 与完整 EditMode 606/606 通过
- [2026-08-12 Marine Game 机枪兵 V2D 击退射击与失去力量](2026-08-12-machine-gunner-v2d-knockback-lost-strength-runtime.md) — 3223 翻为 Implemented；Luban/本地化/`Sync and Build All` 通过，Unity MCP 定向 EditMode 10/10 与完整 EditMode 597/597 通过
- [2026-08-12 Marine Game 机枪兵 V2C 破甲即时卡](2026-08-12-machine-gunner-v2c-armor-break-instant-cards.md) — 3273 铝热炸弹与 3281 踏碎翻为 Implemented；Luban/本地化/`Sync and Build All` 通过，Unity MCP 定向 EditMode 81/81 与完整 EditMode 589/589 通过
- [2026-08-12 Marine Game 机枪兵 V2B 82 模板目录扩展](2026-08-12-machine-gunner-v2b-catalog-extension.md) — 18 张新增身份完成目录与精确状态门禁；V2C 仅打开 3273/3281，余 16 张仍 CatalogOnly；初始 Luban/本地化/`Sync and Build All` 与 112/112、586/586 证据保留
- [2026-08-12 Marine Game 机枪兵 V2A 伤害语义与防御靶机](2026-08-12-machine-gunner-v2a-damage-taxonomy-defense-target.md) — 3236 按 V2 更新、3262 翻为 Implemented；Luban/本地化/`Sync and Build All` 通过，Unity MCP 定向 EditMode 110/110 与完整 EditMode 584/584 通过
- [2026-08-12 Marine Game 机枪兵 MG14B 游击战术](2026-08-12-machine-gunner-mg14b-guerrilla-tactics-runtime.md) — 1 张卡精确翻为 Implemented；工作簿/Luban/静态编译/`Sync and Build All` 通过，Unity MCP 定向 EditMode 6/6 与完整 EditMode 574/574 通过
- [2026-08-12 Marine Game 机枪兵 MG14A 撤退与快速翻滚](2026-08-12-machine-gunner-mg14a-retreat-quick-roll-runtime.md) — 2 张卡精确翻为 Implemented；工作簿/Luban/静态编译/`Sync and Build All` 通过，Unity MCP 定向 EditMode 7/7 与完整 EditMode 571/571 通过
- [2026-08-12 Marine Game 机枪兵 MG13 全息诱饵](2026-08-12-machine-gunner-mg13-holo-decoy-runtime.md) — 1 张卡精确翻为 Implemented；工作簿/Luban/静态编译/`Sync and Build All` 通过，Unity MCP 定向 EditMode 119/119 通过
- [2026-08-11 Marine Game 机枪兵 MG12 光学迷彩](2026-08-11-machine-gunner-mg12-optical-camo-runtime.md) — 1 张卡精确翻为 Implemented；工作簿/Luban/静态编译/`Sync and Build All` 通过，Unity MCP 定向 EditMode 75/75 通过
- [2026-08-11 Marine Game 机枪兵 MG11 爆炸肘](2026-08-11-machine-gunner-mg11-explosive-elbow-runtime.md) — 1 张卡精确翻为 Implemented；工作簿/Luban/静态编译/`Sync and Build All` 通过，Unity MCP 定向 EditMode 70/70 通过

- [2026-08-08 Marine Game 机枪兵 MG10B 不充分爆燃](2026-08-08-machine-gunner-mg10b-incomplete-combustion-runtime.md) — 1 张卡精确翻为 Implemented；工作簿/Luban/静态编译/`Sync and Build All` 通过，Unity MCP 定向 EditMode 65/65 通过

- [2026-08-07 Marine Game 机枪兵 MG10A 烈火烹油](2026-08-07-machine-gunner-mg10a-burning-oil-runtime.md) — 1 张卡精确翻为 Implemented；工作簿/Luban/Sync and Build All 成功，Unity MCP 定向 EditMode 60/60 通过

- [2026-08-07 Marine Game 机枪兵 MG9 逐段命中后置状态](2026-08-07-machine-gunner-mg9-per-hit-runtime.md) — 3 张卡精确翻为 Implemented；工作簿/Luban 复核完成，Unity MCP 定向 EditMode 57/57 通过；同步菜单调用已成功发起，Console 未返回可存档完成行

- [2026-08-07 Marine Game 机枪兵 MG8 功夫机甲、开火与连肘](2026-08-07-machine-gunner-mg8-kungfu-firepower-combo-runtime.md) — 3 张卡精确翻为 Implemented；工作簿/Luban/Sync and Build All 成功，Unity MCP 定向 EditMode 51/51 通过

- [2026-08-07 Marine Game 机枪兵 MG7 Burn/Oil 生命周期与首批依赖卡](2026-08-07-machine-gunner-mg7-burn-oil-runtime.md) — 4 张燃烧/油料卡精确翻为 Implemented；工作簿/Luban/Sync and Build All 成功，Unity MCP 定向 EditMode 37/37 通过

- [2026-08-07 Marine Game 机枪兵 MG6 已有 Power 程序门禁](2026-08-07-machine-gunner-mg6-existing-power-runtime.md) — 6 张已有完整 Power 程序精确翻为 Implemented，工作簿/Luban/Sync and Build All 成功，定向 EditMode 3/3 通过

- [2026-08-07 Marine Game 机枪兵 MG5 X 费与多段射击运行时](2026-08-07-machine-gunner-mg5-x-multishot-runtime.md) — 11 张新增程序精确翻为 Implemented；X=0、支付快照、Stim 与随机多段投影均通过，Luban/Sync and Build All 成功，定向 EditMode 15/15 通过
- [2026-08-07 Marine Game 机枪兵 MG4 职业私有状态与伤害链](2026-08-07-machine-gunner-mg4-private-runtime.md) — Hero 1002 私有状态、攻击公式、护甲/烟雾时机及 PowerPile 基础；定向 EditMode 33/33 通过，额外卡牌仍为 CatalogOnly
- [2026-08-07 Marine Game 机枪兵 MG3 目标与卡牌随机流](2026-08-07-machine-gunner-mg3-target-random.md) — Encounter 顺序目标选择、固定种子重放与失败零推进；定向 EditMode 9/9 通过，尚未翻转额外卡牌
- [2026-08-07 Marine Game 机枪兵 MG2B 初始牌运行时](2026-08-07-machine-gunner-mg2b-starter-runtime.md) — 独立职业运行时接入射击/肘击/防御/装填/兴奋剂；同步构建成功、完整 EditMode 500/500，通过但其余 59 张仍为 CatalogOnly
- [2026-08-07 Marine Game 机枪兵 MG2A 卡牌目录](2026-08-07-marine-game-mg2a-card-catalog.md) — 64 张 CatalogOnly、双语文案、Luban、Unity 同步构建与 40 项定向 EditMode 回归均通过；仍不代表卡牌可玩
- [2026-08-06 机枪兵 MG1 Hero 资源档案](2026-08-06-machine-gunner-mg1-hero-resource-profile.md) — 每 Hero 静态资源档案、首回合 3、后续 capped 补充、立即裁剪与共享补至 5 的权威路径；定向 8/8、相关 93/93 与 fixture 回归 27/27 通过，Sync and Build All 成功
- [2026-08-06 STS2 Ironclad I4 成功归宿与 Tremble](2026-08-06-sts2-ironclad-i4-success-destination.md) — Tremble 以 3 层易伤和 Exhaust 真实归宿翻为 Implemented；相关 61/61、完整 EditMode 482/482、Luban/Localization/Local Content 通过，不含 Exhaust 飞行动画
- [2026-08-06 STS2 Ironclad I3 85 张目录与占位素材](2026-08-06-sts2-ironclad-i3-card-catalog.md) — 冻结单人卡 85/85 录入，82 张 CatalogOnly 复用既有占位并走真实 AB；完整 EditMode 479/479、真实牌面加载 5/5 通过
- [2026-08-06 STS2 Ironclad I2 CatalogOnly 构建隔离](2026-08-06-sts2-ironclad-i2-build-isolation.md) — Deck/程序/牌面/记录身份在 Localization 与 Addressables 前 fail-fast；最终相关 102/102、Local Content 与真实逻辑地址 1/1 通过
- [2026-08-06 STS2 Ironclad I1 CatalogOnly 运行时隔离](2026-08-06-sts2-ironclad-i1-catalog-runtime-gate.md) — Queue typed failure 在费用、卡区与 Effect 写入前终止；精确 1/1、相关与同步构建后回归各 86/86，Luban 与 Local Content 成功
- [2026-08-05 DOTween Pro 仓库净化与免费版独立验证](2026-08-05-dotween-pro-repository-sanitization.md) — 当前树与 GitHub `main` 可达历史已移除 Pro；免费版无 Pro 编译、459/459 EditMode、真实 Bootstrap、精确非 Pro LFS 补传与远端回读审计均通过
- [2026-08-05 配置素材短键与真实 AssetBundle 加载](2026-08-05-config-asset-logical-keys.md) — Hero/Enemy 表迁移为短键，构建期漂移校验、逻辑地址组、Luban/本地内容构建、Packed Play Mode 物理 bundle 与真实 Game View 证据
- [2026-08-05 M10D 交付级验证与性能基线](2026-08-05-m10d-delivery-validation.md) — M10 定向 EditMode 25/25、默认 Game View/Console 和可重复微基线已取证；完整 451 项中的两项非 M10 UI/Targeting 异常保留为历史事实，不将其伪报为 M10 全量全绿
- [2026-08-05 M10C 确定性、帧率无关与生命周期回归](2026-08-05-m10c-determinism-lifecycle.md) — Submit/只读事实轨迹在 30/60/120 FPS、加速和立即完成下相同；取消、重启、Scope/Scene 生命周期定向回归 3/3、相关聚合 53/53 与真实 Bootstrap Play Mode 证据
- [2026-08-05 M10B Bootstrap 可见失败路由与默认内容黄金基线](2026-08-05-m10b-bootstrap-golden-baseline.md) — typed 配置失败停止路由、作者表/生成 JSON/Localization 三方黄金断言、运行时流程 key 门禁与正常 Bootstrap 实测；M10A+M10B 定向 EditMode 21/21 通过
- [2026-08-05 M10A 配置原子性与表清单 fail-fast](2026-08-05-m10a-config-fail-fast.md) — 配置 typed failure、原子发布、重试与四份清单构建期校验；定向 EditMode 9/9 通过
- [2026-08-05 M9 目标箭头与锁定框视觉反馈验收](2026-08-05-m9-targeting-visual-feedback.md) — 分段切线箭身、四角锁定框与相关 Prefab 契约；Unity 定向类集 26/26 通过
- [2026-08-05 M9 出牌不飞向怪物验收](2026-08-05-play-card-no-target-flight.md) — Prelude 仅持有 transient，卡牌只飞向弃牌堆；Unity 定向类集 26/26 通过
- [2026-08-05 M9 验收后 BUG 分诊与结构审查关联](2026-08-05-m9-post-validation-bug-triage.md) — 两项 Hand motion、生命 HUD 头顶投影及 `BUG-UI-002` 伤害飘字局部排序均有精确红绿证据；当前完整 EditMode 460/460，玩家/敌人真实 HUD 前景与 Console 已复核
- [2026-08-02 M9G 全量验证、真实交互、Player 退出与双轴复审](2026-08-02-m9g-full-validation-review.md)
- [2026-08-02 M9F 阶段横幅、胜负面板、重开与退出](2026-08-02-m9f-turn-terminal-restart-exit.md)
- [2026-08-02 M9E 出牌、弃牌、抽牌与重洗运动](2026-08-02-m9e-card-zone-motion.md)
- [2026-08-02 M9D 不可用样式、目标聚焦与正式目标素材](2026-08-02-m9d-card-focus-targeting-feedback.md)
- [2026-08-02 M9C 结算反馈、受击与死亡过渡](2026-08-02-m9c-settlement-combat-feedback-death.md)
- [2026-08-02 M9B 参与者状态、Block 与既有意图 HUD](2026-08-02-m9b-combatant-status-hud.md)
- [2026-08-02 M9A 有序表现时间线、一次 completion 与取消](2026-08-02-m9a-ordered-presentation-timeline.md)
- [2026-08-02 M8E 全量验证、真实 Game View 与双轴复审](2026-08-02-m8e-full-validation-review.md)
- [2026-08-02 M8D 状态时机、死亡与完整战斗循环](2026-08-02-m8d-status-death-battle-loop.md)
- [2026-08-02 M8C 敌人意图与 Effect 联合事务](2026-08-02-m8c-enemy-effect-transaction.md)
- [2026-08-02 M8B 命令生命周期、continuation 与表现屏障](2026-08-02-m8b-command-lifecycle-presentation-barrier.md)
- [2026-08-02 M8A 命令、状态与终局契约](2026-08-02-m8a-command-status-terminal-contract.md)
- [2026-08-02 M7E 全量验证、真实 Game View 与双轴复审](2026-08-02-m7e-full-validation-review.md)
- [2026-08-02 M7D 出牌事务与卡区结算记录](2026-08-02-m7d-card-effect-transaction.md)
- [2026-08-02 M7C 有序 Effect 执行 module](2026-08-02-m7c-ordered-effect-executor.md)
- [2026-08-02 M7B 参与者权威状态与伤害操作](2026-08-02-m7b-combatant-effect-operations.md)
- [2026-08-02 M7A 结算记录与公式契约](2026-08-02-m7a-settlement-formula-contract.md)
- [2026-08-02 M6D 全量验证、双轴复审与文档收口](2026-08-02-m6d-full-validation-review.md)
- [2026-08-01 M6C Self / Enemy 目标选择 UI](2026-08-01-m6c-self-enemy-target-selection.md)
- [2026-08-01 M6B 队首目标重校验与权威写链](2026-08-01-m6b-queue-head-target-revalidation.md)
- [2026-08-01 M6A 目标契约与纯合法性 module](2026-08-01-m6a-card-play-rules.md)
- [2026-08-01 M5D 全量验证与复审](2026-08-01-m5d-full-validation-review.md)
- [2026-08-01 M5C 敌人意图 HUD](2026-08-01-m5c-enemy-intent-hud.md)
- [2026-08-01 M5B Session、权威命令队列与生产接线](2026-08-01-m5b-session-command-queue-wiring.md)
- [2026-08-01 M5A 敌人行为配置与确定性选择核心](2026-08-01-m5a-enemy-behavior-selection.md)
- [2026-08-01 M4E 全量验证与双轴复审](2026-08-01-m4e-full-validation-review.md)
- [2026-08-01 M4D 当前单玩家命令 UI 接线](2026-08-01-m4d-single-player-command-ui.md)
- [2026-08-01 M4C 队列化结束行动与敌人顺序交接](2026-08-01-m4c-end-action-enemy-handoff.md)
- [2026-08-01 M4B 队列化出牌、能量与执行期校验](2026-08-01-m4b-queued-card-play-energy.md)
- [2026-08-01 M4A 权威命令队列与回合事实骨架](2026-08-01-m4a-authoritative-command-queue.md)
- [2026-07-31 牌面短键与 Addressables 逻辑地址迁移](2026-07-31-card-illustration-logical-keys.md)
- [2026-07-31 DataTables 工作簿简易配色](2026-07-31-datatables-simple-colors.md)
- [2026-07-31 战斗 UI 首批美术与牌面配置链路接入](2026-07-31-battle-ui-art-integration.md)
- [2026-07-30 BattleScene M3A 参与者配置与 Prefab 工厂](2026-07-30-battlescene-participant-views.md)
- [2026-07-30 Addressables 迁移](2026-07-30-addressables-migration.md)
- [2026-07-30 卡牌本地化与动态文本](2026-07-30-card-localization-dynamic-text.md)
- [2026-07-30 卡牌区域与确定性洗牌](2026-07-30-card-zones-deterministic-random.md)
- [2026-07-30 战斗配置接入运行时](2026-07-30-battle-config-runtime-integration.md)
- [2026-07-30 战斗静态配置表](2026-07-30-battle-static-config-tables.md)
- [2026-07-30 BattleState 运行时参与者模型](2026-07-30-battle-runtime-state.md)
- [2026-07-30 最小状态机 Core](2026-07-30-state-machine-core.md)
- [2026-07-30 BattleScene 拖拽出牌（最小判定）](2026-07-30-battlescene-drag-to-play-minimal.md)
- [2026-07-29 BattleScene 手牌 UI（杀戮尖塔式）](2026-07-29-battlescene-hand-ui-sts-style.md)
- [2026-07-12 BattleScene 基础手牌 UI](2026-07-12-battlescene-card-ui.md)
- [2026-07-12 LoadingScene 最短展示时间](2026-07-12-loading-scene-minimum-duration.md)
