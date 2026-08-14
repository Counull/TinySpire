---
title: 共享 Block 保留、Ironclad Barricade 与机枪兵 Garrison 运行时验收
page_type: testing
lifecycle: active
date: 2026-08-14
scope: Barricade 3157、Garrison 3246、共享 BattleBlockRetention 与精确双选手牌保留
status_source: ../SESSION_LOG.md
---

# 共享 Block 保留、Barricade 与 Garrison 运行时验收

## 当前结论

- 3157 / 3246 基础态已翻为 `Implemented`。全项目 168 张为 94/74，Ironclad 13/72，Marine 80/2（V1 62/2、V2 18/0），Effect 17。
- `BattleBlockRetention` 以一次性 Prepare / Validate / Commit 统一永久、计时和 PlayerRoundStart：Barricade 永久跳过 Block 清除；Garrison 的两个回合开始依次 `2→1→0`，降为 0 的当次仍保留，下一次才清 Block。
- Garrison 从来源以外的当前 Hand 精确选择 2 个不同实例；UI 收齐两张才提交。成功后只让两张所选牌跳过当前一次行动结束弃牌，下一次恢复普通规则。

## 数据与构建证据

| 作者表 | SHA-256 | bytes |
|---|---|---:|
| `DataTables/Datas/__enums__.xlsx` | `8fc42a27fced4998a3a72940bed75e32f09f3e8e0d5aff8e26369fabaa38e4b5` | 11084 |
| `DataTables/Datas/battle.card_effect.xlsx` | `c55948630183518cebf28e7516d782cceb388b02592410b6a3e71ebbd8e2eabb` | 4638 |
| `DataTables/Datas/battle.card.xlsx` | `2874c0df732f7aced41641437910beaca5bffdf3424c4d10895876f7a2f3e3c3` | 23210 |
| `DataTables/Datas/i18n.xlsx` | `7812301c2acdcadbf62e1cefc2c26ad56ad44879aab09e5afecc070d0e58a699` | 29098 |

- Luban 生成后 3157 为 `retention:4017`、Program 0、PowerPile；4017 为 `RetainBlock / None / 0`。3246 为 Program 46、DiscardPile。
- `Sync and Build All` 因两次域重载分阶段完成生成、Localization Import / Validate 与 Local Addressables。02:59:03 的 BuildLayout（SHA-256 `c0d8a2f6b9c26311d5134d278f2a795b4615e170cb2338e6829db5256574f20e`）把 Card / Effect JSON 放入 12245 bytes 的 `tinyspiregamedata_assets_all_7cc46da8b11cc1e16221ef7a586e071c.bundle`，Provider 为 `AssetBundleProvider`。

## 自动验证

| 门禁 | 结果 |
|---|---|
| 生成前精确任务（前缀 `006e…`） | 3/3 passed |
| 最终定向任务（前缀 `17e031…`） | 300/300 passed |
| 完整 EditMode（前缀 `b4d970…`） | 798/798 passed |
| 静态 build | 0 error / 12 条既有 warning |

覆盖包括：Barricade 经公开 Queue 出牌进入 PowerPile并永久保留 Block；Garrison 费用、12 Block、精确双选、选择错误零写、一次行动保留；计时层 `2→1→0` 两次跳过清除及下一次清除；UI 两步选择、取消和命令提交；正式目录、Localization、JSON 与真实 AB。最终未 commit、未 push。

## 未完成边界

- Barricade 升级 2 Energy、Garrison 升级 15 Block / 选择 3 张仅为作者表与本地化 metadata；升级 `CardInstance` 运行时未实现。
- 默认 Deck、奖励、Run、多人、Scene / Prefab 与剩余目录卡不在本切片范围。
- 代码决策见 [CD-107](../CODE_DECISIONS.md#cd-107barricade-与-garrison-以共享-block-保留授权接入手牌保留保持职业侧单行动语义)，计划状态见 [Ironclad](../plans/2026-08-06-sts2-v01071-ironclad-card-pool.md) 与 [Machine Gunner V2](../plans/2026-08-12-machine-gunner-v2-card-runtime-plan.md)。
