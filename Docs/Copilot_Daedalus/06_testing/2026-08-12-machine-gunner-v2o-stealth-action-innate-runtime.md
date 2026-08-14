---
title: Marine Game 机枪兵 V2O 隐秘行动与固有起手
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
related_decision: ../CODE_DECISIONS.md#cd-094v2o-innate-以强类型卡牌配置驱动首轮起手且隐秘行动复用普通状态与抽牌语义
---

# Marine Game 机枪兵 V2O 隐秘行动与固有起手

## 1. 验收对象与冻结行为

本切片只开放 `StealthAction` (3275) 基础态，并提供通用 Innate 首次起手协议：

- 3275 为 1 Energy、Uncommon、Skill、Self、Hand→DiscardPile；成功时按 `Invisible +1 → DrawCards(1)` 执行。升级 Invisible +2 / Draw 2 仍只是作者表元数据。
- `is_innate` 是 `Card` 的强类型非空布尔字段，默认 false；当前精确目录只允许 3275 为 true。Turn 在启动写入前按静态表收集每个现有 Deck 中的固有实例，但不按 3275 / Program 75 特判；CardZones 决定具体顺序与布局。
- CardZones 在既有 Deck 洗牌后按 DrawPile 的实际抽取顺序选择固有实例。固有数 0～5 时全部入手并用普通牌补到默认起手 5；6～10 时全部入手且不补普通牌；超过 Hand 上限 10 时启战在首次写入前失败。
- 成功起手只发布一次最终 `Layout` 且不推进洗牌随机；settlement 先固有后普通、各组保持洗牌后抽取顺序且 Order 连续。Innate 只作用于首次起手，后续回合继续走普通补牌。
- 3275 的普通 Draw 发生在本卡离开 Hand 之前：Hand=10 时抽 0，随后弃置本卡后 Hand=9。它不使用 V2N 的离手后抽至上限 seam。

本切片没有把 3275 加入默认 Deck、奖励或 Run，未修改 UI、多人、Scene、Prefab 或升级实例。

## 2. 已变更表面

| 层 | 变更 |
|---|---|
| 作者表/schema | `__beans__.xlsx` 的 Card bean 新增 `is_innate`；`battle.card.xlsx` 只让 3275 为 true 并翻为 `Implemented`，其余卡为 false。 |
| Luban 生成 | `Card` 生成 `IsInnate`，JSON 生成 `is_innate`；目录为 69/13，V1 为 55/9、V2 为 14/4。 |
| Turn/卡区 | Turn 在全部玩家启战写入前按静态 `IsInnate` 收集实例并准备计划；CardZones 统一拥有首次起手选择、快照校验、settlement 顺序与单次布局发布。 |
| 后续回合 | 保持普通补牌，不重复应用 Innate；通用起手路径不识别 3275 或 Program 75。 |
| Program 75 | 复用既有 Invisible 与普通 Draw 操作，成功后进入 DiscardPile；不新增伤害、Ammo 或射击联动。 |

## 3. 定向回归门禁

| 场景 | 必须锁定的事实 | 当前结果 |
|---|---|---|
| 单张固有、不同 seed | 3275 无论洗牌位置都进入首次 Hand，最终 Hand=5，只发布一次 Layout。 | 通过（非快照定向 140/140） |
| 多张固有 | 0～5 补普通至 5；6～10 全部固有入手且不补普通；顺序来自实际洗牌后抽取顺序。 | 通过（非快照定向 140/140） |
| 固有超过 Hand 上限 | 返回 `InvalidOpeningHandConfiguration`，全部玩家卡区、随机、Turn 与表现结果零写入；无职业运行时也共享上限 10。 | 通过（非快照定向 140/140） |
| 无固有 Deck | 与 V2O 前默认首轮起手的数量、seed 确定性和连续 settlement 保持一致。 | 通过（非快照定向 140/140） |
| 3275 正常出牌 | 支付 1 Energy，先 Invisible +1，再普通 Draw 1，最后进入 DiscardPile。 | 通过（非快照定向 140/140） |
| 3275 满 Hand | Hand=10 时 Draw=0，当前卡离手后 Hand=9，不额外补抽。 | 通过（非快照定向 140/140） |
| 失败与分类隔离 | 能量不足或显式目标零写；不触发 Shoot、Ammo、Stim、IncendiaryAmmo 或 PortableHelper。 | 通过（非快照定向 140/140） |
| 表与生成门禁 | 只有 3275 `IsInnate=true` 且 Implemented；精确冻结 69/13、V1 55/9、V2 14/4。 | Luban 与正式目录快照 21/21 已通过 |

## 4. 数据、同步与 Unity 证据

| 项目 | 结果 | 说明 |
|---|---|---|
| 正式作者表 | 已复核 | `battle.card.xlsx` SHA-256 `172FEB0A50DA4F3DC6A580F83C73C97266B6F70A72D1FA01CCDD3D15B1B9F6C9`；`__beans__.xlsx` SHA-256 `A899AC4D58890C5E2B5D75C9AF09A9B0769078F5218FE11889AD3F8688C178FB`。 |
| Luban 与生成配置 | 通过 | 3275 为 `Implemented` / `is_innate=true`；82 模板为 69/13，V1 55/9，V2 14/4。 |
| Editor 静态编译 | 通过 | 0 error；12 条既有程序集版本 warning。 |
| 本地化导入/校验 | 通过 | Console 明确记录 `TinySpire battle card localization validation passed.`。 |
| `Sync and Build All` / Addressables | 通过 | Console 明确记录 `TinySpire sync and local content build completed successfully.`；Addressables 18.363 秒。 |
| Unity 非快照定向 EditMode | 通过 | 任务 `3174fa1fc44f432ea6001ac5c9322c5f`：140/140 passed，0 failed/skipped，2.4455622 秒。 |
| Unity 正式目录快照 | 通过 | 任务 `8acfa22da51c4f2fb757bbe102fb945c`：21/21 passed，0 failed/skipped，0.6395992 秒。 |
| 旧 fixture 兼容聚合 | 通过 | 任务 `7f0a9531fa6e48abb58b21b5699a5b05`：7 classes、88/88 passed，0 failed/skipped，3.7314504 秒；统一通过 `StartBattle` 的 `initialHandCount`，未放宽生产门禁。 |
| Unity 最终聚合定向 | 通过 | 任务 `982a4f4c4af24ba78e678bf0e66f2ce1`：237/237 passed，0 failed/skipped，4.4515056 秒；同时覆盖起手、3275、表/schema 与构建门禁。 |
| 完整 EditMode | 通过 | 任务 `91d060c915ff4dfea42608b7c22669ab`：673/673 passed，0 failed/skipped，123.9614109 秒。 |

3275 程序、通用 Innate 起手、作者表/schema、Luban、本地化、同步构建、正式目录快照、最终聚合定向与完整 EditMode 均已通过；本切片按标准完整门禁收口。开发诊断中的失败任务不作为验收绿证据。

## 5. 验收后边界

- 只实现 3275 基础态和可复用的首次起手 Innate 规则；升级仍是元数据。
- 不修改默认 Deck 内容，因此正常产品 Deck 是否实际含 3275 由后续 Deck/奖励范围决定；测试可用专用 Deck 证明通用协议。
- 不修改奖励、Run、UI、多人、Scene 或 Prefab。
- 不把 Innate 当作每回合抽牌、保留、临时卡创建或免费出牌协议。
- 本切片未创建提交，也不记录或杜撰提交 SHA。
