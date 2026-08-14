---
title: Marine Game 机枪兵 V2U 不解释12连两波换弹射击
page_type: testing
lifecycle: active
date: 2026-08-13
updated: 2026-08-13
status: verified-unity-native-2026-08-13
status_source: ../SESSION_LOG.md
source:
  - ../00_inbox/README web.md
  - ../01_requirements/2026-08-12-machine-gunner-v2-requirement-digest.md
related_plan: ../plans/2026-08-12-machine-gunner-v2-card-runtime-plan.md
related_decision: ../CODE_DECISIONS.md#cd-101v2u-不解释12连以纯两波资源计划冻结换弹射击
---

# Marine Game 机枪兵 V2U 不解释12连两波换弹射击

本页记录 `TwelveHits` (3257) 基础态、两波 Ammo 资源轨迹、波间换弹、Stim / IncendiaryAmmo / PortableHelper / Guerrilla 联动、V2T 免费 Attack 授权，以及正式作者表、Luban、本地化、Addressables 与 Unity 原生验收证据。

## 1. 验收对象与冻结行为

- Program 57 基础态为 3 Energy、Rare、Attack、自动最近敌人、Hand→DiscardPile。命令开始时按 Encounter 顺序冻结一个最近存活敌人，不接受显式目标；后续不会因目标死亡而重定向。
- Normal 支付先使用当前 Ammo 最多展开 6 个来源伤害段，再无条件补到命令开始时 AmmoMaximum，随后从补满资源最多展开第二波 6 个来源段。0 Ammo 可以施放：第一波为 0 次，仍产生换弹并支付第二波 Ammo。
- 每个来源段基础伤害为 5，经过既有 Attack / Shoot 伤害与逐 hit 后置链。目标投影死亡后跳过当前波及下一波剩余伤害，但不会取消已经冻结的换弹、第二波 Ammo 支付、成功归宿或费用授权消费。
- Stim 是整张卡最多一个额外来源段，只放在第二波；Normal 必须在第二波基础支付后仍有 Ammo 容量，Waived 则保留该段。每个实际展开的来源段依次触发既有 IncendiaryAmmo，再在目标存活时触发 PortableHelper；帮手伤害不递归触发来源链。

## 2. 深 resolver 与事务边界

| 层 | 已验收职责 |
|---|---|
| `BattleCardCostResolver` | 继续冻结 Energy 的 Normal / Waived actual、effect 与 nominal；不理解换弹、波次或 Ammo。 |
| `MachineGunnerReloadedVolleyResolver` | 纯输入 initial Ammo、AmmoMaximum、单波上限、Stim 与支付模式；冻结首/次波 effect shot、actual Ammo、补满前后、Stim、Guerrilla nominal 与 final Ammo，不读写战斗对象。 |
| 职业准备事务 | 在首次写入前联合冻结 Energy、目标、两波资源轨迹、逐 hit 伤害和卡区归宿；费用、目标、Shackle、计划或快照失败均零写入。 |
| 逐 hit 链 | 复用既有 `AppendPreparedHitAndPostHitOperations`，按同一目标投影顺序处理 Damage、IncendiaryAmmo 与 PortableHelper；目标死亡只截断伤害，不回头改资源计划。 |
| V2T 免费授权 | 两波实际 Ammo 与 Energy 均为 0，效果仍按每波 6 展开，波间仍补满；无 Stim 时 nominal Ammo 12，有 Stim 时 13，成功归宿后消费授权。 |

该 resolver 是机枪兵私有深模块；它把“射击、补满、再射击”的资源不变量封装在一个纯结果中，但不虚构通用两阶段资源协议，也不宣称 Ironclad 已有消费者。

## 3. 六项 TDD 红绿证据

| 切片 | 锁定行为 | 结果 | 任务 |
|---|---|---:|---|
| production red | 0 Ammo tracer 首次要求 Program 57 可执行。 | 1/1 failed | `5206873b56c84c27a462dd27edcaf375`；稳定暴露 Unsupported Program 57。 |
| tracer green | 0 Ammo 第一波 0 次、换弹补满、第二波经 Queue 支付并弃牌。 | 1/1 passed | `ea81efa9f48c408da2e3f51573805b23` |
| slice 2 | 普通 Ammo 的两波各自最多 6 发，波间补满，资源 settlement 顺序固定。 | 1/1 passed | `b0b40621fd3740798e9bd5dd91277507` |
| slice 3 | 首个来源段致死后不重定向、不再造成伤害，但换弹和第二波支付仍提交。 | 1/1 passed | `968e6f6870d84ab58a6d61caf9aae44d` |
| slice 4 | Stim 只增加第二波一个来源段；每个来源段按顺序触发 IncendiaryAmmo 与 PortableHelper。 | 1/1 passed | 首轮 `e2caeac9cbe1440598c3a2075de14075` 因测试场景供能前提错误失败；只修测试，生产未改；最终 `aa2f33f94df14b1a8913d299c325445c`。 |
| slice 5 | Waived 实际 Ammo 0、仍换弹并展开 13 个含 Stim 来源段，Guerrilla 读取 nominal 13，成功后消费授权。 | 1/1 passed | `b7eb8adef3de4e9f8b490093171de1c0` |
| slice 6 | Energy、显式目标与 Shackle 失败均保持资源、伤害、状态、随机、卡区与授权零写入；合法重试仍可使用授权。 | 1/1 passed | `4605fc4c790940f5a5a2eb4169ac1d2e` |

`e2caeac9cbe1440598c3a2075de14075` 不是 production red，也不代表生产回归；它只诊断测试 fixture 没有满足场景自身的供能前提。唯一用于推动 Program 57 实现的红灯是 `5206873b56c84c27a462dd27edcaf375`。

## 4. 正式 Unity 与静态证据

| 层级 | 结果 | 任务 / 说明 |
|---|---:|---|
| 六项逐片 TDD | 6/6 | 六个最终 green job 见上一节。 |
| Starter 运行时类 | 148/148 | `9fec961053ae45ea869ad9aa211c13fa` |
| 正式目录快照 | 37/37 | `4f7cbbf8343c472f9e51e2f1862c2a3c` |
| 正式聚合 | 220/220 | `a7041271f4f343588b30e74edfd5b741`；包含 CardIllustration 真实 Addressables AssetBundle 加载。 |
| 完整 EditMode | 728/728 | `08565f2677824aff8e45043cdd8dc1eb` |
| Runtime 静态编译 | 0 error / 6 warning | `Assembly-CSharp.csproj --no-restore`。 |
| Editor 静态编译 | 0 error / 12 warning | `Assembly-CSharp-Editor.csproj --no-restore`。 |

静态 warning 仍为既有程序集版本冲突类；本切片没有把静态编译冒充 Unity 原生验证，正式结果以上述 Unity 任务为准。

## 5. 数据、本地化与 Addressables

| 项目 | 结果 | 说明 |
|---|---:|---|
| 正式作者表 | 已复核 | 只把 `Sheet1!Q147` 从 `CatalogOnly` 翻为 `Implemented`；SHA-256 `7131597FD5F3D948921F54926C0205E24E31F747D7C9B1206B78902AE6BEF818`。 |
| Luban / 生成 JSON | 通过 | 2026-08-13 03:00:27；JSON SHA-256 `28324422913241FC627F5C3A0BCF715332E4F2B3DCDFA94E4B6E4FF3ED7A6306`。全项目 168，Marine 82 为 75/7、V1 59/5、V2 16/2。 |
| 3257 生成元数据 | 通过 | status 0 / Program 57 / Attack / Rare / 3E Fixed / upgraded cost 2 / Enemy / base+upgraded DiscardPile / has upgrade / 空 bindings / `art_placeholder` / 非 Innate。 |
| Localization | 通过 | import 7.350 秒；显式 validate 3.124 秒。 |
| `Sync and Build All` | 通过 | 端到端 18.482 秒；本地 Addressables 12.173 秒。 |
| BuildLayout | 已写出 | `Library/com.unity.addressables/BuildReports/buildlayout_2026.08.13.03.02.34.json`。 |

## 6. 验收边界

- 本切片只实现 3257 基础态；升级 2 Energy / 每段 6 伤仍只是作者表元数据，没有升级 `CardInstance` 运行时。
- 不加入默认 Deck、奖励、Run、UI 专属提示、多人、Scene、Prefab、ProjectSettings、asmdef、DI 或构建管线改动，也不实现自动免费攻击链或剩余 7 张目录卡。
- 两波 resolver 只属于机枪兵 Ammo 适配器；公共费用 resolver、Ironclad 与其他职业没有被扩展为两阶段资源消费者。
- 本切片未创建提交，也不记录或杜撰提交 SHA。
