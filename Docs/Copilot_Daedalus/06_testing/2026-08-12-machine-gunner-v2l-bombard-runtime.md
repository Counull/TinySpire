---
title: Marine Game 机枪兵 V2L 狂轰滥炸基础态
page_type: testing
lifecycle: active
date: 2026-08-12
updated: 2026-08-12
status: verified-composite-with-full-suite-timeout-boundary
status_source: ../SESSION_LOG.md
source:
  - ../00_inbox/README web.md
  - ../01_requirements/2026-08-12-machine-gunner-v2-requirement-digest.md
related_plan: ../plans/2026-08-12-machine-gunner-v2-card-runtime-plan.md
related_decision: ../CODE_DECISIONS.md#cd-091v2l-狂轰滥炸在四类延迟-support-触发时读取当前层数并先缩放载荷
---

# Marine Game 机枪兵 V2L 狂轰滥炸基础态

## 1. 验收对象与冻结行为

本切片只开放 `Bombard` (3265) 基础态：

- 1 Energy、Power、Self、Hand→PowerPile；每次成功施放增加 4 层整场不衰减的狂轰滥炸 Power，多张卡线性叠加。
- 已创建的延迟效果不快照 Power 层数；`BansheeStrike`、`FireSupport`、`FireBombardment` 与 `TripleStrike` 的延迟 Support 在各自实际触发时读取当前层数。
- 每层把声明的支援载荷提高 10%；正值按 `floor((baseValue × (100 + 10 × stacks) + 50) / 100)` 进行 half-up 取整。每张基础态狂轰滥炸增加 4 层，因此等价于每张卡为受支持载荷增加 40%。该正数取整是原始来源未规定时、经用户授权“脑补”后冻结的实现决定。
- 女妖打击、火力支援与三连击只缩放延迟 Support 伤害；燃烧轰炸的 Support 伤害、Burn 与 Oil 三项载荷分别缩放。缩放后的伤害再进入既有 Support 管线，继续读取目标 Smoke、Vulnerable 与 ArmorBreak；燃烧轰炸仍保持 `Damage → 存活后 Burn → Oil`。
- 不放大 GuidedNuke / FiveHundredPounder 的 Bomb、NeedleStorm 的 Delayed 伤害、回合末 Burn、即时攻击、便携帮手或其他非声明来源，也不改变命中数、波次数、倒计时、目标选择与生命周期 settlement。

本切片没有实现升级 `CardInstance`，也没有把卡加入默认 Deck、奖励或 Run；未修改 UI、多人、Scene 或 Prefab。

## 2. 已变更表面

| 层 | 变更 |
|---|---|
| 作者表/生成配置 | `battle.card.xlsx` 只将 Q155（3265）的 `implementation_status` 翻为 `Implemented`；目录为 66/16，V1 为 54/10、V2 为 12/6。 |
| Power 程序 | 3265 注册为 Self / PowerPile，每次成功施放增加 4 层；卡本身没有即时伤害、Block 或私有状态写入。 |
| 延迟支援缩放 | 在四类既有 scheduled Support 准备路径集中读取当前 Power 层数并换算声明载荷；没有新增全局伤害事件。 |
| 伤害与状态链 | 缩放发生在既有 Support 伤害管线前；燃烧轰炸的 Damage、Burn、Oil 分别缩放，并保留致死跳过后置状态及原生命周期顺序。 |
| 目录门禁 | 仅 V2 扩展身份 3265 新增为 `Implemented`；其余 16 张保持 `CatalogOnly`。 |

## 3. 已加入的定向回归

| 用例 | 锁定事实 |
|---|---|
| `Bombard_StacksFourPerPowerCardWithoutImmediateCombatEffect` | 每张成功施放增加 4 层并进入 PowerPile；两张叠至 8 层，卡本身不产生即时战斗效果。 |
| `Bombard_FailedCostOrExplicitTargetWritesNothing` | 能量不足或 Self 卡携带显式目标时，能量、Power、卡区、随机流与表现结果零写入。 |
| `Bombard_FireSupportReadsCurrentStacksAtTriggerAndRoundsLinearly` | 先创建火力支援、后叠 Power 仍按触发时层数读取；4 层与 8 层分别把基础 2 half-up 为 3 与 4。 |
| `Bombard_FourStacksBoostEveryDeclaredSupportPayload` | 四层分别放大女妖伤害、燃烧轰炸的 Damage/Burn/Oil 与三连击延迟 Support。 |
| `Bombard_PreservesSupportPipelineOrderAndExcludesNonDeclaredDamage` | 先缩放支援基值再走 Smoke/Vulnerable/ArmorBreak；钢针 Delayed、Bomb 与回合末 Burn 不受影响。 |
| `Bombard_FireBombardmentFatalHitSkipsStatusesAndKeepsLifecycleOrder` | 放大后的燃烧轰炸致死时跳过该目标 Burn/Oil，同时保持波次、触发、倒计时、移除和连续 Order。 |

开发中曾发现管线回归把触发前的目标 Vulnerable 只预置为 1，忽略敌方行动阶段会先衰减一层；夹具已改为预置 2，使实际触发时保留 1 层。该修正只校准测试前提，没有改变生产公式。

## 4. 数据、同步与 Unity 证据

| 项目 | 结果 | 说明 |
|---|---|---|
| Luban 与生成配置 | 通过 | 只开放 3265；82 模板为 66 `Implemented` / 16 `CatalogOnly`，V1 为 54/10，V2 为 12/6。 |
| 本地化 | 通过 | 从 Excel 导入战斗卡本地化成功。 |
| Sync 与本地 Addressables | 通过 | `Sync and Build All` 完成，Addressables 构建耗时 12.963 秒。 |
| Unity 定向 EditMode | 通过 | MCP 任务 `9c21aa7c79b94f1980988945d35636dd`：134/134 passed，0 failed，耗时 1.4521749 秒。 |
| 第一次完整 EditMode | 保留超时边界 | MCP 任务 `828d66e749a54e66813b3e5d492d4d80` 完成 645 项枚举，唯一非绿项为 `CardArtLogicalAddresses_LoadSprites` 的 180 秒冷加载 timeout；API 未返回可用 summary / duration。 |
| 精确真实素材加载 | 通过 | 单独重跑同一用例，MCP 任务 `da1d1e3969014e81b06cb57a2392de13`：1/1 passed，耗时 106.8572486 秒。 |
| 第二次完整 EditMode | 保留同一超时边界 | MCP 任务 `492466a3ac7240c29bb227a60945a3c0` 再次完成 645 项枚举，仍只有同一用例触发 180 秒 timeout；不写为完整套件通过。 |
| 素材配置测试类 | 保留同一超时边界 | `CardIllustrationConfigurationTests` 整类 MCP 任务 `2f3a0ddf6a254a5ab7d8eff8ff5116d5` 完成 5 项枚举，仍只有同一冷加载用例 timeout。 |

因此 V2L 使用组合门禁收口：3265 相关定向 134/134 全绿，且精确 Addressables 真实加载用例 1/1 全绿；两次完整任务与一次素材类任务的一致非绿项均收敛到同一个测试顺序/冷加载 180 秒边界。该证据足以确认本卡切片与真实素材加载，但**不等于完整 EditMode 存在一次全绿任务**。

## 5. 结构证据与排除边界

- 增幅入口只接受 `BansheeStrike`、`FireSupport`、`FireBombardment` 与 `TripleStrike` 四种 scheduled effect；这四种现有触发段都使用 `Support` 伤害类型。它不是按卡名、显示文案或任意 Damage settlement 推断。
- FireBombardment 的三项载荷先分别按同一层数与取整规则换算，随后才进入既有 Damage/Burn/Oil 准备与存活门禁；已有 Oil 对本次 Burn 的交互继续由原 Burn 逻辑处理。
- GuidedNuke、FiveHundredPounder、NeedleStorm、回合末 Burn、即时攻击和便携帮手均没有调用该缩放入口。本切片没有改动 `MachineGunnerDamagePipeline` 的 Support 规则，也没有建立通用增伤状态或事件总线。

## 6. 验收后边界

- 本切片只实现 3265 基础态；升级数值仍只是作者表元数据。
- 没有修改默认 Deck、奖励、Run、UI、多人、Scene 或 Prefab。
- 没有实现 3260、天空之怒、临时卡、选择、自动免费攻击、AnyAlly 或其他跨卡协议。
- 其余 16 张 `CatalogOnly` 继续由精确目录门禁拒绝。
- 本切片未创建提交，也不记录或杜撰提交 SHA。
