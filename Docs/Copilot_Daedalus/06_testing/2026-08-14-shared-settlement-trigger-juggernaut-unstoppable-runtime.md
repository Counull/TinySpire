---
title: 共享 settlement-derived trigger、Ironclad Juggernaut 与机枪兵 Unstoppable
page_type: testing
lifecycle: active
created: 2026-08-14
updated: 2026-08-14
status: verified-unity-native
status_source: ../SESSION_LOG.md
source:
  - ../plans/2026-08-06-sts2-v01071-ironclad-card-pool.md
  - ../plans/2026-08-12-machine-gunner-v2-card-runtime-plan.md
  - ../CODE_DECISIONS.md
---

# 共享 settlement-derived trigger、Ironclad Juggernaut 与机枪兵 Unstoppable

## 1. 当前结论

共享 `BattleSettlementTriggerEngine` 与 Juggernaut / Unstoppable 基础态已完成 production、正式作者表、Luban、Localization / Addressables 同步与 Unity 原生验证。首次 Sync 对缺少 `{triggerDamage}` 的 i18n 正确 fail-fast；单点修复后重跑成功。

## 2. 基础态语义

| 消费者 | 基础态 | settlement 触发 | 子动作 |
|---|---|---|---|
| Juggernaut（3169） | 2 Energy / Rare Power / Self / PowerPile；`triggerDamage:4019`，raw type 10 / `None` / 6 | 目标为持有者且 `Amount > 0` 的 `BattleBlockGainedSettlement` | 随机存活敌人受 6 点伤害；不读 Strength / Vulnerable，仍经 Block / HP / 致死写链 |
| Unstoppable（3250） | 1 Energy / Rare Power / Self / PowerPile / Program 50；空 bindings；非 Innate | owner 造成致死，或一条伤害把正 Block 从正值降到 0 | 从表顺序冻结的 `Implemented` / Attack / 非 Shoot / 可自动解析目标候选中随机创建一张临时卡，免费完整出牌并强制 Exhaust |

Juggernaut 升级伤害与 Unstoppable 升级追加 debuff 触发均只是 metadata，升级卡实例尚未实现。本切片没有新增 HUD、Prefab 或 Scene。

## 3. 共享深模块与原子性

1. Power 父事务以 Prepare / Validate / Commit 冻结 owner、trigger/action kind、动作值、候选模板与注册表 revision；失败不留下半注册。
2. 父命令已提交的 settlement 按 settlement 顺序、再按注册顺序冻结 intent batch。共享模块不读卡牌 ID、名称、Program 或职业分支。
3. `BattleCommandQueue` 只在父命令的表现屏障完成后，以内部 system token 执行 `ResolveSettlementTriggersCommand`。外部仍只能经 `BattleCommandQueue.Submit`，没有第二权威写入口。
4. 每个子动作再次 Prepare / Validate / Commit 来源/敌人标量、Encounter 顺序、随机 before/after、伤害 outcome 或卡区/模板快照；成功首写后才推进引擎独占随机流。
5. Unstoppable 生成的子牌继续走完整出牌管线；当前 registration ID 在自己派生链中被抑制，阻止同一 Power 自递归，但不吞掉其他注册的有序观察。

## 4. 目标数据门禁

| 范围 | 发布后目标 |
|---|---:|
| 全项目 Card | 98 Implemented / 70 CatalogOnly |
| Ironclad | 15 / 70 |
| Marine V1 | 64 / 0 |
| Marine 总计 | 82 / 0（V2 保持 18 / 0） |
| Effect | 19 |

上述计数已由正式生成数据、validator / snapshot 与 Unity 门禁确认；Effect 强枚举已替代开发期 raw 10 引用。

## 5. 发布证据清单

| 证据 | 当前状态 |
|---|---|
| 正式 Enums xlsx SHA-256 | `D899B0C39E01A5829A8FDC0BA4EB0F4A36609E4BF177EF92D17BC2976E6BF194` |
| 正式 Card xlsx SHA-256 | `C22E2380915C4D847CB073228785EC20453C9170832C12E3977DDFE8B831A253` |
| 正式 Effect xlsx SHA-256 | `3224852248155DC34A0ADE73A2C7693E8F4AB8DFD5D041406E3448581EE15A9D` |
| 正式 i18n xlsx SHA-256 | `E6329D49F669DB3FA4223CF5EE7CCCBAF5DA5F9B3102A8C8DDB1D7F009987617` |
| 生成 Card JSON SHA-256 | `DDDC4CE73D93A3C40939EE096C2E1CA6CCDE82D187D8FEDAF9533CA39FEA0FDD` |
| 生成 Effect JSON SHA-256 | `67A5865E17F803CCE614B617B207C407B42F20959F615B9FCD04C5B62FBD9868` |
| 生成 `EffectType.cs` SHA-256 | `BA13A3CF7D0584C44A4C2AB74C3F3B5C4B4FEF5AF79F82CD8985923F1ED526FA` |
| Luban 生成 | GREEN |
| Localization Import / Validate | 首跑正确拒绝缺失 `{triggerDamage}`；单点 i18n 修复后 GREEN，en/zh 更新于 05:25:02 |
| `TinySpire/Build/Sync and Build All` / 真实 AB | 修复后 GREEN；Addressables 15.175 秒 |
| BuildLayout SHA-256 | `429C1CD806275B7095205307B67DAE71F39678C19E53E3C39B574193ACDAA769` |
| Runtime / Editor 静态编译 | Runtime 0 error / 6 warning；Editor 0 error / 12 warning |
| targeted Unity 任务 ID / 结果 | `054b6bcd5d734f729a2f1f95c4e7a80d`；7/7 passed；0.6658563 秒 |
| aggregate Unity 任务 ID / 结果 | 未单独重跑另一 aggregate job；full 807/807 已包含本切片相关聚合范围 |
| full EditMode 任务 ID / 结果 | `d156b8e2537546ef9e83da0ef5dadd2a`；807/807 passed；19.3037496 秒 |

## 6. 本次文档校验边界

- 只执行 UTF-8、相对链接与 scoped diff-check。
- 本次文档草稿不运行 build、Unity、Luban、Localization 或 Addressables，也不修改表格与 generated 资产。
- 升级实例、debuff 触发、通用 event bus、Deck / 奖励 / Run / 多人、HUD / Prefab / Scene 均不在当前发布切片。
