---
title: Marine Game 机枪兵 V2S 先发制人按来源起始状态种类抽牌
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
related_decision: ../CODE_DECISIONS.md#cd-098v2s-先发制人按命令开始时的来源活跃状态种类冻结普通抽牌数
---

# Marine Game 机枪兵 V2S 先发制人按来源起始状态种类抽牌

本页记录 V2S 已完成的 3277 基础态、来源状态种类快照、PreparedDraw 深事务复用、正式作者表、Luban、本地化、同步构建、真实 Addressables 加载、定向与完整 Unity EditMode 证据。

## 1. 验收对象与冻结行为

- `PreemptiveStrike` (3277) 基础态为 0 Energy、1 Ammo、Uncommon、Attack、显式 Enemy、`Tags.None`、Hand→DiscardPile；成功时造成基础 8 点普通 Attack，再抽取冻结数量的牌，最后把自身移入 DiscardPile。
- 抽牌数量 `N` 取命令开始时来源的活跃状态种类：Strength 非零计一种、Vulnerable 正层计一种，每种 `MachineGunnerCombatantStatus` 正层数各计一种；同种状态不论层数只计一次。
- 本页发布时的 16 种职业私有状态身份是 `Weakness`、`LoseStrength`、`Shackle`、`Smoke`、`Burn`、`Oil`、`FirePower`、`Armor`、`ArmorBreak`、`Intangible`、`Invisible`、`Buffer`、`NextRoundBlock`、`ReloadAmmoAtNextPlayerRound`、`NextRoundEnergyGainBonus` 与 `NextRoundEnergyGainPenalty`。CD-104 后当前集合追加 `Regeneration`，共 17 种；Power、Stim、scheduled effect、Block 与资源仍不计入 `N`。
- Shackle 身份仍属于上述精确集合，但上游攻击门禁会在首次写入前拒绝任何带 Shackle 的来源施放 3277，因此成功路径无法借本卡消费或绕过 Shackle；历史 15 种加新增 Regeneration 的回归共同证明当前其余 16 种私有状态可逐项正层计数。
- 伤害与既有 post-hit 链完成后才提交命令起点已经冻结的 PreparedDraw；来源或目标在伤害链中发生变化不回算 `N`，目标致死后仍按旧值抽牌。升级基础 12 点伤害仍只是作者表元数据。

## 2. 已变更表面

| 层 | 已验收口径 |
|---|---|
| Program 77 | 0 Energy、1 Ammo、显式 Enemy、普通 Attack 8、`Tags.None`、按来源起始活跃状态种类抽牌、DiscardPile。 |
| 状态计数 | 发布时为 Strength `!= 0`、Vulnerable `> 0`、16 种职业私有状态；CD-104 后追加 Regeneration 为当前第 17 种。仍分别按正层“种类”计数，并排除 Power、Stim、scheduled effect、Block 与资源。 |
| CardZones | 复用 `PrepareDraw` / `ValidatePreparedDraw` / `CommitPreparedDraw`，在首次战斗写入前冻结数量、容量、重洗随机、最终布局与 settlement。 |
| 时序 | 支付 0 Energy / 1 Ammo → Damage 与既有 post-hit → 冻结普通 Draw → 当前牌 Hand→DiscardPile；目标致死不取消 Draw。 |
| 作者表与门禁 | 仅 Q167 翻为 `Implemented`，U167 保持 false；目录 73/9、V1 57/7、V2 16/2。 |

## 3. 定向回归门禁

| 场景 | 必须锁定的事实 | 当前结果 |
|---|---|---|
| 基础成功路径 | 0 Energy、1 Ammo、显式 Enemy、基础 8 点普通 Attack、冻结 Draw、DiscardPile，Order 连续。 | 通过（最终 V2S 5/5；正式聚合 214/214） |
| 通用状态与排除项 | Strength 非零、Vulnerable 正层分别计一种；Power、Stim、scheduled effect、Block 与资源不计。 | 通过（最终 V2S 5/5；正式聚合 214/214） |
| 私有状态矩阵 | 历史 16 种由 V2S 锁定；CD-104 追加 Regeneration 后当前为 17 种。除 Shackle 的上游拒绝契约外，其余 16 种逐项正层各抽 1，同种多层不重复。 | 通过（V2S 历史 5/5 + CD-104 回归） |
| Shackle 上游门禁 | 来源有 Shackle 时 Attack 在首次写入前拒绝，Energy、Ammo、伤害、状态、随机、卡区与结果均不变化。 | 通过（最终 V2S 5/5） |
| 时点与致死 | Damage / post-hit 后提交旧 `N`；目标致死仍抽，后续变化不回算命令起点。 | 通过（最终 V2S 5/5） |
| 重洗与事务 | 普通抽牌沿用 CardZones 的容量、重洗随机、移动 settlement、布局快照与失败零写入。 | 通过（最终 V2S 5/5；正式聚合 214/214） |
| 目录快照 | 3277 的 Uncommon / 0E / Enemy / Discard / Program 77 / 非 Innate / Implemented 与 73/9、57/7、16/2 精确冻结。 | 通过（正式聚合 214/214） |
| 真实牌面加载 | CardIllustration 通过最新本地 Addressables AssetBundle 与 BuildLayout 证据加载。 | 通过（正式聚合 214/214） |

## 4. 来源、脑补与 TDD 证据

- source-stated：当前 `README web.md` 明确 3277 为 0 费、1 弹、基础 8 伤、“自己每有一种状态抽 1 张”，升级为 12 伤。
- 项目实现决定：命令开始时冻结、Strength / Vulnerable / 职业私有状态精确集合、同种多层只计一次、排除 Power/Stim/scheduled effect/Block/资源，以及 Damage / post-hit 后仍使用旧值，均是为确定性与事务安全冻结的“脑补”边界，不伪称来源逐字规定。数量从本页发布时 16 种演进为 CD-104 后 17 种。
- TDD 首轮任务 `634966a39d1a434886289cca3382e8f9` 为 **3/5 passed**；两项红色都是测试 oracle 偏差：一项误判 CardZones 重洗移动记录顺序，另一项尝试让带 Shackle 的来源进入成功 Attack，忽略了既有上游攻击门禁。两项都只修正测试预期，生产代码没有因此改变。
- 最终 V2S 任务 `73d5e79a25164857b48bb5b1fba5d92a` 为 **5/5 passed**（0.7261073 秒）。正式聚合首轮 `629f7e51d61d4bb49a6bdb6232239ca6` 为 **213/214 passed**，唯一红项是旧 Bully 操作名称 oracle；只修正测试，生产代码未改。production 审查没有发现 blocker。

## 5. 数据、同步与 Unity 证据

| 项目 | 结果 | 说明 |
|---|---|---|
| 正式作者表 | 已复核 | `battle.card.xlsx` SHA-256 `6C9120A317622F103F9A0DDEEEBB994B28F88230B679BA7E0B1D28201F8E2648`；仅 Q167 `CatalogOnly→Implemented`，U167 `is_innate=false`。 |
| Luban 与生成配置 | 通过 | 2026-08-12 23:26:12 成功；全项目 Card JSON 168 个，Marine 82 为 73/9、V1 57/7、V2 16/2；3277 为 status 0 / Program 77 / DiscardPile / 非 Innate。 |
| Runtime / Editor 静态编译 | 通过 | Runtime 0 error / 6 warning；Editor 0 error / 12 warning。 |
| 本地化导入/校验 | 通过 | Localization import 与显式 validate 均成功。 |
| `Sync and Build All` / Addressables | 通过 | 同步与本地内容构建成功，写出 BuildLayout；Addressables 13.966 秒。 |
| V2S 精确回归 | 通过 | `73d5e79a25164857b48bb5b1fba5d92a`：5/5 passed，0.7261073 秒。 |
| Unity 正式聚合定向 | 通过 | `ea21d256c5b840629a270eed7a10bd90`：214/214 passed，16.8155368 秒；包含 CardIllustration 真实 Addressables AB 加载。 |
| 完整 EditMode | 通过 | `1484317edb124dcdb6cb0f0862a8758a`：702/702 passed，25.5067985 秒。 |

3277 程序、来源命令起始状态种类快照、Damage / post-hit 后 PreparedDraw、致死仍抽、Shackle 上游零写入、作者表、Luban、本地化、同步构建、BuildLayout、真实 Addressables 加载、正式聚合与完整 EditMode 均已通过；V2S 按标准完整门禁收口。

## 6. 验收边界

### 2026-08-14 通用 Poison 后续口径

- 本页发布时的 16 种、CD-104 后 17 种私有状态与历史任务均保留。CD-106 之后，3277 共用 helper 还会把来源正层 Poison 计为一种，因此当前最大集合为 Strength + Vulnerable + Poison + 17 private = 20；另一参与者的 Poison 不计，同种多层只计一次。
- 任务前缀 `419c…` 2/2 已精确覆盖来源 Poison 计数且不消费双方层数；行为聚合任务前缀 `79a…` 289/289 保留为开发中证据。正式数据 / AB 已发布，最终定向任务 `88e36d2a5cbb47b7b4a67207dad00856` 为 9/9、完整 EditMode `9ca3d43a79d24b25a917fad7b6166584` 为 793/793；当前正式计数为全项目 92/76、Marine 79/3，但不覆盖本页 2026-08-12 的历史发布计数。

- 只实现 3277 基础态；不建立全局“所有状态”注册表，也不改变 Strength、Vulnerable 或职业私有状态各自的存储与生命周期。
- `Tags.None` 表示本卡不从名称推断 Shoot；它仍是普通 Attack，继续走既有伤害、Block/HP、致死与 post-hit 链。
- 不实现升级实例或升级 12 伤，不修改默认 Deck、奖励、Run、UI 或多人流程。
- 本切片未创建提交，也不记录或杜撰提交 SHA。
