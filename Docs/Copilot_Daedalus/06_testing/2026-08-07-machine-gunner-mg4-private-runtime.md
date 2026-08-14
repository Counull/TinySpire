---
title: Marine Game 机枪兵 MG4 职业私有状态与伤害链验收
page_type: testing
lifecycle: active
created: 2026-08-07
status_source: ../SESSION_LOG.md
related_plan: ../plans/2026-08-07-marine-game-card-only-integration.md
---

# Marine Game 机枪兵 MG4 职业私有状态与伤害链验收

## 范围

本记录验收 Hero 1002 已装配的 `MachineGunnerBattleRuntime`。目标是让职业状态、攻击公式和职业卡区事实只存在于该场战斗的私有运行时内；默认 Hero 1001、通用卡牌效果和唯一 `BattleCommandQueue.Submit` 写入入口不改。

## 实现结论

- `MachineGunnerCombatState` 只保存在 Hero 1002 的 Session 私有运行时，维护 Weakness、Smoke、Burn、Oil、Armor、Invisible；它不向通用 `CombatantData` 增加职业字段，也不提供第二条命令入口。
- 攻击伤害通过内部公式覆盖在 Effect 预构建期冻结，顺序为力量 → Weakness → 攻击者/受击者 Smoke → Vulnerable → Block → HP。Debuff 不读取攻击修正；Burn 施加只读取施加前已有 Oil，再将 Oil 减半。
- 机枪兵受到敌方攻击同样进入同一公式链。Armor 仅在攻击实际穿透生命后消耗一层；Smoke 在玩家回合开始清空，已有 SmokePersist 时改为减一；Weakness 在所属参与者行动结束时减一。
- 卡区新增 `PowerPile`、职业手牌上限 10 和按原手牌顺序的 `DiscardHandExcept`。已有真实私有规则的六张能力程序（核心扩容、出力调整、防爆护盾、扩容弹夹、烟雾弥漫、动力强化）可进入 `PowerPile`；它们目前只由测试夹具覆盖，生产表仍全部为 `CatalogOnly`。
- `Hand → PowerPile` 保留普通 `CardMoved` 事实，但当前表现计划没有 Power 锚点或飞行步骤。因此本切片不把任何能力牌加入可获得/可打出的生产路径，也不假称已有玩家可见的 Power UI。

## Unity 验证

使用当前已连接的单一 Unity 6000.5.5f1 Editor，未启动第二实例、未操作 Game View。

| 项目 | 结果 |
| --- | --- |
| 编译刷新 | Console 产品错误为 0 |
| EditMode 任务 | `d283762aa2ea454ab4638a8ff6165cde` |
| 汇总 | **33/33 passed，0 failed，0 skipped** |
| 覆盖 | 5 张初始牌、敌方伤害/护甲生命周期、攻击与 Debuff 公式、Burn/Oil、Smoke 时机、PowerPile/手牌上限/保留顺序、Card Zone 表现路由、随机失败零推进、默认 Hero 1001 不装配职业运行时且仍补至 5 张手牌 |

## 未包含

- 其余 59 张卡仍为 `CatalogOnly`，没有奖励、三选一、地图、Run、升级实例或跨战斗状态；
- X 费冻结、免费攻击、延迟伤害、炸弹、女妖、支援、临时机枪扫射、自动连锁出牌和权威待决选择；
- PowerPile 的 HUD、图标、飞行动画、动态插画预加载或真实 BattleScene 手工演示；
- Scene、Prefab、ProjectSettings、asmdef、HybridCLR、启动/DI、默认 Hero 和受保护 Targeting/Candidates/Hermes 美术路径。
