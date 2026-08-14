---
title: 共享触发出牌、Ironclad Havoc 与机枪兵 Opportunistic Strike 运行时验收
page_type: testing
lifecycle: active
date: 2026-08-14
scope: Havoc 3108、Opportunistic Strike 3243、Queue-owned system-token continuation
status_source: ../SESSION_LOG.md
---

# 共享触发出牌、Havoc 与 Opportunistic Strike 运行时验收

## 结论

- Queue 以内部 system token 在当前命令成功后串行消费 frozen continuation；触发牌仍走正常出牌管线，没有公开第二写入口或 Turn 内递归提交。
- Havoc 从 DrawPile 顶部免费打出并强制 Exhaust；Opportunistic Strike 仅在上一张成功牌为 Attack / Shoot 后，从当前 Hand 随机选择 Attack 免费打出。
- 3108 / 3243 已翻为 `Implemented`：全项目 96/72，Ironclad 14/71，Marine 81/1（V1 63/1、V2 18/0），Effect 18。两张升级仅为 metadata。

## 数据与 AssetBundle

| 产物 | SHA-256 | bytes |
|---|---|---:|
| `DataTables/Datas/__enums__.xlsx` | `EA91547F88FBB05C74A8DFDBFA5864A36F72FC309F5B51421564AF3CEF8EB7CF` | 11140 |
| `DataTables/Datas/battle.card_effect.xlsx` | `2639CE5F87BAA6774D32C199CB4A31A82A0DE47EF4CD9E2B8E3BA419F74EE73D` | 4671 |
| `DataTables/Datas/battle.card.xlsx` | `D5ECD06EE838ED239E0BBB60D8449396F82EEF14662639B551DF8A9A51200DE1` | 23225 |
| `DataTables/Datas/i18n.xlsx` | `602005AA8DD5749BCB3BC9E5ACA917401B5C85A859CF487A137C7309F897477B` | 29123 |
| `TinySpire/Assets/GameData/battle_tbcard.json` | `E5152D0C0CD954986AF2128D737A5D7856F03DC25470AE265F353A155DF2AB5B` | 124030 |
| `TinySpire/Assets/GameData/battle_tbcardeffect.json` | `F925AC30F476ED41DC66E86AAB378D249E87B774ED93E17576F2B685DD7F3379` | 1733 |

`TinySpire/Library/com.unity.addressables/BuildReports/buildlayout_2026.08.14.04.03.07.json` 证明 GameData 由 `AssetBundleProvider` 进入 12277 bytes 的 `tinyspiregamedata_assets_all_2459a0905c4d39297dbbacf298b41106.bundle`。

## 自动验证

| 门禁 | 结果 |
|---|---|
| 定向 `a35d7b7a38f64ad5936132655e7f5318` | 8/8 passed |
| Localization cleanup `be23e45bedbe430f87173f0e3e913a0c` | 1/1 passed |
| 完整 EditMode `dd5ba1f2b6004e0a85a3aee6de4256e4` | 802/802 passed，19.3797688 秒 |
| 静态 Editor build | 0 error / 12 条既有 warning |

初次 full 暴露强枚举与非展示 Effect 本地化校验口径不一致；修正后上述最终 full 全绿。未 commit、未 push。

## 范围外

- Havoc 升级费用 0、Opportunistic Strike 升级改为选择攻击手牌及升级实例运行时。
- 自动选择非 Attack、任意卡区触发、无限触发链、默认 Deck、奖励、Run、多人、Scene / Prefab。
- 决策见 [CD-108](../CODE_DECISIONS.md#cd-108havoc-与-opportunistic-strike-通过-queue-owned-system-token-continuation-触发免费出牌)，计划见 [Ironclad](../plans/2026-08-06-sts2-v01071-ironclad-card-pool.md) 与 [Machine Gunner V2](../plans/2026-08-12-machine-gunner-v2-card-runtime-plan.md)。
