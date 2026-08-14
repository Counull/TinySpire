---
title: Marine Game 机枪兵 V2R 霸凌按目标起始状态种类抽牌
page_type: testing
lifecycle: active
date: 2026-08-12
updated: 2026-08-14
status: verified-unity-native-2026-08-12
status_source: ../SESSION_LOG.md
source:
  - ../00_inbox/README web.md
  - ../01_requirements/2026-08-12-machine-gunner-v2-requirement-digest.md
related_plan: ../plans/2026-08-12-machine-gunner-v2-card-runtime-plan.md
related_decision: ../CODE_DECISIONS.md#cd-097v2r-霸凌按命令开始时的目标活跃状态种类冻结普通抽牌数
---

# Marine Game 机枪兵 V2R 霸凌按目标起始状态种类抽牌

本页记录 V2R 已完成的 3278 基础态、PreparedDraw 深事务复用、正式作者表、Luban、本地化、同步构建、真实 Addressables 加载、定向与完整 Unity EditMode 证据。

## 1. 验收对象与冻结行为

- `Bully` (3278) 基础态为 0 Energy、Uncommon、Attack、显式 Enemy、Hand→DiscardPile；成功时记录 `EnergySpent(0)`，造成基础 6 点普通 Attack，再抽取冻结数量的牌，最后把自身移入 DiscardPile。
- 抽牌数量取命令开始时目标的活跃状态种类：Strength 非零、Vulnerable 正层，以及每种 `MachineGunnerCombatantStatus` 正层数各计一种；同种状态不论层数只计一次。
- HP、Block、资源、PowerPile 卡实例、Stim 与 scheduled effect 不属于该计数。伤害消费 Buffer / Intangible / Armor、后置新增 Oil 或目标死亡都不改变冻结数量。
- 0 种状态合法抽 0；普通抽牌遵守 Hand 10 上限、DrawPile / DiscardPile 重洗、洗牌随机和真实移动 settlement。升级 9 点伤害没有升级实例，基础态仍只造成 6。

## 2. 已变更表面

| 层 | 已验收口径 |
|---|---|
| Program 78 | 0E、显式 Enemy、普通 Attack 6、`Tags.None`、按目标起始活跃状态种类抽牌、DiscardPile。 |
| 状态计数 | Strength / Vulnerable / 每种职业私有状态按“种类”计数；排除 HP、Block、资源、Power 实例、Stim 与延迟实例。 |
| CardZones | 复用 `PrepareDraw` / `ValidatePreparedDraw` / `CommitPreparedDraw`，在首次写入前冻结数量、容量、重洗随机、最终布局与 settlement。 |
| 时序 | `EnergySpent(0) → Damage 与既有 post-hit → 冻结普通 Draw → 当前牌 Hand→DiscardPile`，Order 连续。 |
| 作者表与门禁 | 仅 Q168 翻为 `Implemented`，U168 保持 false；目录 72/10、V1 57/7、V2 15/3。 |

## 3. 定向回归门禁

| 场景 | 必须锁定的事实 | 当前结果 |
|---|---|---|
| Burn + Oil 两种状态 | 先结算 6 点普通攻击，再抽 2；需要重洗时沿用既有随机与移动事实，最后弃置自身。 | 通过（补强 6/6；正式聚合 209/209） |
| 种类矩阵 | Strength、Vulnerable 与每个职业私有状态分别计数；同状态 7 层仍只抽 1，Block/HP 不计。 | 通过（补强 6/6） |
| 冻结时点 | 目标致死后仍按起始 Burn 抽 1；命中后新 Oil 不反哺起始 0 计数。 | 通过（补强 6/6） |
| 0 状态 / 满手 | 0 状态不伪造 Draw；Hand=10 时抽 0、不推进洗牌随机，当前牌弃置后 Hand=9。 | 通过（补强 6/6） |
| 目标与失败零写入 | 缺失显式目标在 Energy、伤害、状态、随机、卡区与结果写入前失败。 | 通过（补强 6/6；非表格聚合 150/150） |
| 目录快照 | 3278 的 Uncommon / 0E / Enemy / Discard / Program 78 / 非 Innate / Implemented 与 72/10、57/7、15/3 精确冻结。 | 通过（正式聚合 209/209） |
| 真实牌面加载 | CardIllustration 通过最新本地 Addressables AssetBundle 真实加载。 | 通过（正式聚合 209/209） |

## 4. 来源、脑补与 TDD 证据

- source-stated：当前 `README web.md` 明确 3278 为 0 费、基础 6 伤、目标每有一种状态抽 1 张，升级为 9 伤。
- 项目实现决定：命令开始时冻结、Strength / Vulnerable / 职业私有状态的精确集合、同种多层只计一次、排除 HP/Block/资源/Power/Stim/延迟实例，以及伤害后仍使用旧值，均是为确定性与事务安全冻结的“脑补”边界，不伪称来源逐字规定。
- TDD 任务 `36ffd31603d14de38de4912faf8fb4c1` 枚举 3 项并得到 2 绿 1 红；唯一红项是测试把 Damage 误当 Order 0，遗漏生产既有 `EnergySpent(0)`。仅修正测试预期，生产代码没有因该红项改变。
- 补强任务 `8d099f89842e4024925465adf9b3e370` 为 **6/6 passed**（0.4367945 秒）；非表格聚合 `94110e65e6b649ea99b901ad49ab4bdd` 为 **150/150 passed**（1.4560424 秒）。

## 5. 数据、同步与 Unity 证据

| 项目 | 结果 | 说明 |
|---|---|---|
| 正式作者表 | 已复核 | `battle.card.xlsx` SHA-256 `878812D99F68C8F9B9A7BC620E2794180F6E8A3F21B5252B16A12BDB70915499`；仅 Q168 `CatalogOnly→Implemented`，U168 `is_innate=false`。 |
| Luban 与生成配置 | 通过 | 2026-08-12 22:48:16 成功；全项目 Card JSON 168 个，Marine 82 为 72/10、V1 57/7、V2 15/3；3278 为 status 0 / Program 78 / DiscardPile / 非 Innate。 |
| Runtime / Editor 静态编译 | 通过 | Runtime 0 error / 6 warning；Editor 0 error / 12 warning。 |
| 本地化导入/校验 | 通过 | Localization import 与显式 validate 均成功。 |
| `Sync and Build All` / Addressables | 通过 | 同步与本地内容构建成功，写出 BuildLayout；Addressables 16.521 秒。 |
| TDD 补强 | 通过 | `8d099f89842e4024925465adf9b3e370`：6/6 passed，0.4367945 秒。 |
| 非表格聚合 | 通过 | `94110e65e6b649ea99b901ad49ab4bdd`：150/150 passed，1.4560424 秒。 |
| Unity 正式聚合定向 | 通过 | `9d67623c6fcc445ebb658b8eea6709c0`：209/209 passed，0 failed/skipped，30.7641594 秒；包含 CardIllustration 真实 Addressables AB 加载。 |
| 完整 EditMode | 通过 | `598d7b50593e463db922f1ad88472d99`：697/697 passed，0 failed/skipped，19.7357848 秒。 |

3278 程序、命令起始状态种类快照、PreparedDraw 深事务、失败零写入、作者表、Luban、本地化、同步构建、BuildLayout、真实 Addressables 加载、正式聚合与完整 EditMode 均已通过；V2R 按标准完整门禁收口。

## 6. 验收边界

### 2026-08-14 通用 Poison 后续口径

- 本页发布时的历史任务与 72/10、57/7、15/3 计数保持不变。CD-106 之后，3278 共用的当前状态种类 helper 在 Strength、Vulnerable、17 种职业私有状态之外，再把目标正层 Poison 计为一种；最大集合为 20，同一 Poison 不论层数仍只计一次。
- Poison 是通用 `CombatantData` 事实，不并入 `MachineGunnerCombatantStatus`。任务前缀 `419c…` 与 `79a…` 保留为开发中计数与行为聚合证据；正式 3270 数据发布后，最终定向任务 `88e36d2a5cbb47b7b4a67207dad00856` 为 9/9、完整 EditMode `9ca3d43a79d24b25a917fad7b6166584` 为 793/793。当前正式计数为全项目 92/76、Marine 79/3；本页 2026-08-12 的历史发布计数仍保持不变。

- 只实现 3278 基础态；不建立全局“所有状态”注册表，也不改变 Strength、Vulnerable 或职业私有状态各自的存储与生命周期。
- `Tags.None` 表示本卡不从名称推断 Shoot；它仍是普通 Attack，继续走已有伤害、Block/HP、致死与非射击后置链。
- 不实现升级实例或升级 9 伤，不修改默认 Deck、奖励、Run、多人、Scene 或 Prefab。
- 本切片未创建提交，也不记录或杜撰提交 SHA。
