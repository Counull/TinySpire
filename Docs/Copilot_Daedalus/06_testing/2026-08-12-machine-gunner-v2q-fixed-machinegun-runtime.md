---
title: Marine Game 机枪兵 V2Q 固定机枪与临时卡生产
page_type: testing
lifecycle: active
date: 2026-08-12
updated: 2026-08-12
status: verified-unity-native-2026-08-12
status_source: ../SESSION_LOG.md
source:
  - ../00_inbox/README web.md
  - ../01_requirements/2026-08-12-machine-gunner-v2-requirement-digest.md
related_plan: ../plans/2026-08-12-machine-gunner-v2-card-runtime-plan.md
related_decision: ../CODE_DECISIONS.md#cd-096v2q-固定机枪以-cardzones-深事务原子替换剩余手牌并显式发布临时卡创建
---

# Marine Game 机枪兵 V2Q 固定机枪与临时卡生产

本页记录 V2Q 已完成的正式作者表、Luban、本地化、同步构建、真实 Addressables 加载、定向与完整 Unity EditMode 证据。

## 1. 验收对象与冻结行为

- `FixedMachinegun` (3261) 基础态为 2 Energy、Rare、Skill、Self、Hand→ExhaustPile；成功时先获得 10 Block。
- 来源卡随后进入 ExhaustPile；当时其余 Hand 按原有顺序全部进入 DiscardPile，并按被弃旧手牌数量创建等量 `MachinegunBurst` (3263) 到 Hand。没有其余手牌时合法创建 0 张。
- 临时 3263 是本次 BattleSession 内的新 `CardInstanceData`，只进入 Hand，不写回 Deck、奖励或 Run。每张新卡使用 `CardCreated` settlement，不伪装为 DrawPile→Hand、普通 Draw 或 Innate。
- CardZones 以单一 Prepare / Validate / Commit 计划冻结原布局、实例分配状态、来源归宿、其余手牌原序、新实例、最终布局与连续 settlement；成功只发布一次最终 `Layout`，失败与快照漂移必须零写入。
- 表现层区分既有 Hand→Discard、来源 `HandToExhaust` 与临时卡 `CreatedToHand`。3263 模板由职业 registry 经 `Session.AvailableCardTemplateIds` 传递给 Hand 异步预载，不依赖该模板预先存在于 Deck。
- 升级 15 Block 仍只是作者表元数据；没有升级 `CardInstance`，基础态只获得 10 Block。

## 2. 已变更表面

| 层 | 已验收口径 |
|---|---|
| Program 61 | 注册基础态 2 Energy / Self / ExhaustPile，声明 10 Block 与复合手牌替换意图。 |
| CardZones | 以一个深事务计划原子完成来源 Exhaust、剩余 Hand 原序 Discard、等量实例创建与一次最终 `Layout`。 |
| Settlement | 保留真实 Hand→Exhaust、Hand→Discard，并新增 `CardCreated`→Hand；所有 Order 连续。 |
| 表现 | 消费 `HandToExhaust`、既有 Hand→Discard 与 `CreatedToHand`，不从最终 Hand 差异猜测创建。 |
| 动态模板 | registry 声明 3263 依赖，Session 汇总为 `AvailableCardTemplateIds`，Hand 初始化时异步预载。 |
| 作者表与门禁 | 只将 3261 翻为 `Implemented`；Luban 后为 71/11、V1 57/7、V2 14/4。 |

## 3. 定向回归门禁

| 场景 | 必须锁定的事实 | 当前结果 |
|---|---|---|
| 正常多手牌成功 | 先 10 Block，来源进 Exhaust；其余 Hand 原序进 Discard；创建等量 3263 到 Hand；只发布一次 `Layout`。 | 通过（核心 12/12；正式聚合 262/262） |
| 空余手牌 | 仍支付 2 Energy、获得 10 Block并 Exhaust 来源；不生成虚假 `CardCreated`。 | 通过（核心 12/12） |
| 创建身份 | 新 CardId 唯一且模板均为 3263；实例只属于当前 Session，数量与被弃旧手牌严格相等。 | 通过（核心 12/12） |
| 原子失败 | 能量不足、显式目标、布局/分配状态漂移或重复提交时，Energy、Block、CardId 分配、Layout、卡区与表现结果零写入。 | 通过（核心 12/12） |
| settlement 顺序 | Energy/Block 后依次为来源 Exhaust、旧 Hand 原序 Discard、新实例创建到 Hand；Order 连续且无伪 Draw。 | 通过（核心 12/12） |
| 表现 cue | 来源走 `HandToExhaust`，旧牌走 Hand→Discard，新牌走 `CreatedToHand`；完成屏障只触发一次。 | 通过（非表格定向 195/195；正式聚合 262/262） |
| 动态模板预载 | Deck 不含 3263 时，registry→`Session.AvailableCardTemplateIds`→Hand async preload 仍能创建新牌视图。 | 通过（动态精确任务 2/2；正式聚合 262/262） |
| V1/V2 快照 | 3261 的 Skill/Rare/2E/Self/Exhaust/Program 61/非 Innate/Implemented 与 71/11、V1 57/7、V2 14/4 被精确冻结。 | 通过（正式聚合 262/262） |

## 4. TDD 与审查修复证据

- 红测任务前缀 `404d20…` 首先证明旧表现 prelude 在同一结果含多张 Hand→Discard 时会抛错；修复后表现只从权威 settlement 区分来源 Exhaust、旧手牌 Discard 和新卡 Created。
- 红测任务前缀 `2045cc…` 锁定缺少 `CardCreated` 结果 guard 的失败路径；对应 CardZones/结果校验补齐后，核心任务前缀 `d6db34…` 为 **12/12 passed**。
- 非表格定向任务前缀 `f415877…` 为 **195/195 passed**。最终审查发现“3263 不在 Deck 时插画模板不会预载”的生产 blocker；现已由 registry→`Session.AvailableCardTemplateIds`→Hand async preload 修复，动态精确任务前缀 `6bf4…` 为 **2/2 passed**。
- 上述红测只作为问题定位证据；最终绿色验收采用修复后的核心、动态精确、正式聚合与完整 EditMode 任务。

## 5. 数据、同步与 Unity 证据

| 项目 | 结果 | 说明 |
|---|---|---|
| 正式作者表 | 已复核 | `battle.card.xlsx` SHA-256 `02F549502D14214C98B4BA97212962B05E58A9B768EF1D7E4CAD441E1DCD6FB7`；只将 3261 的状态翻为 `Implemented`，`is_innate=false`。 |
| Luban 与生成配置 | 通过 | 22:00:11 成功；全项目 Card JSON 168 个，Marine 82 模板为 71/11、V1 57/7、V2 14/4；3261 为 status 0 / Program 61 / Exhaust / `is_innate=false`。 |
| Runtime / Editor 静态编译 | 通过 | Runtime 0 error / 6 warning；Editor 0 error / 12 warning。 |
| 本地化导入/校验 | 通过 | Localization import 与显式 validate 均成功。 |
| `Sync and Build All` / Addressables | 通过 | 同步与本地内容构建成功；Addressables 13.42 秒，随后通过 force scripts 完成域重载。 |
| Unity 核心定向 | 通过 | 任务前缀 `d6db34…`：12/12 passed。 |
| Unity 非表格定向 | 通过 | 任务前缀 `f415877…`：195/195 passed。 |
| 动态模板精确回归 | 通过 | 任务前缀 `6bf4…`：2/2 passed；覆盖 Deck 不含 3263 时的异步插画预载。 |
| Unity 正式聚合定向 | 通过 | 任务 `ba19d1744f084167927568f5572f91e6`：262/262 passed，0 failed/skipped，30.1698095 秒；包含目录快照、CardIllustration 真实 Addressables 加载、Session、Hand、Queue 与 UI。 |
| 完整 EditMode | 通过 | 任务 `dc6a1453b602487c8bfbbe7e42c3968d`：690/690 passed，0 failed/skipped，20.8279366 秒。 |

3261 程序、CardZones 深事务、显式创建 settlement、表现 cue、动态插画预载、作者表、Luban、本地化、同步构建、真实 Addressables 加载、正式聚合与完整 EditMode 均已通过；V2Q 按标准完整门禁收口。

## 6. 验收边界

- 只实现 3261 基础态和 3263 的生产创建协议；V2P 已验证的 3263 直接执行语义保持不变。
- 不实现升级实例或升级 15 Block，不修改默认 Deck、奖励排除、Run、多人、Scene 或 Prefab。
- `CardCreated` 只表示本局临时实例进入 Hand，不表示普通 Draw、奖励获取、牌组永久加入或跨战斗保存。
- UI 变更限于消费新的权威 settlement 与动态模板预载，不扩大到场景布局或 Prefab 改造。
- 本切片未创建提交，也不记录或杜撰提交 SHA。
