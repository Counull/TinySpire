---
title: Marine Game 机枪兵 MG11 爆炸肘运行时
page_type: testing
lifecycle: active
date: 2026-08-11
updated: 2026-08-11
status: verified-unity-native-2026-08-11
status_source: ../SESSION_LOG.md
source:
  - ../00_inbox/marine-game.zip (marine-game/cards.json, battle.html, README.md)
  - ../01_requirements/2026-08-07-marine-game-card-requirement-digest.md
related_plan: ../plans/2026-08-07-marine-game-card-only-integration.md
related_decision: ../CODE_DECISIONS.md#cd-075爆炸肘将立即触发现有-burn建模为全局命中钩子之后的-debuff-追加段
---

# Marine Game 机枪兵 MG11 爆炸肘运行时

## 1. 验收对象与冻结行为

本记录覆盖 Hero 1002 的 `ExplosiveElbow` (3252) 基础值程序。当前 `marine-game` 来源链冻结为：支付 2 Energy，自动选择最近存活敌人并先造成 10 点普通 Attack；若目标仍存活，则在该段原有卡牌后置状态、`IncendiaryAmmo` 与 `AgedOil` 之后，读取目标此刻的 Burn 并立即追加同值 Debuff 伤害。

该立即触发不消耗或改写 Burn/Oil，故下一次回合末自燃仍读取同一 Burn。Debuff 复用既有 Block/HP 伤害路径，不读取 Weakness、Smoke、Vulnerable，也不消耗 Armor；普通攻击已经致死时后置操作、AgedOil 和立即 Burn 都不会发生。卡牌最后从 Hand 移至 DiscardPile，若追加段杀死最后敌人，仍由整张卡提交后的既有终局逻辑派生 `BattleEnded`。

`00_inbox/HANDOFF.md` 中的 STS2 Mod 1 Energy / 8（升级 11）记录不在本摘要采用的来源链，未用于改写此卡的作者表或运行时数值。表内 `Enemy` 保留为目录输入分类，自动最近目标只在职业 Program 内派生；升级值仍等待通用 CardInstance 升级状态。

## 2. 已变更表面

| 层 | 变更 |
|---|---|
| 职业运行时 | `TinySpire/Assets/Scripts/Battle/MachineGunner/MachineGunnerBattleRuntime.cs` 为攻击 Program 增加受限的“全局命中钩子后触发当前 Burn Debuff”声明，注册 3252 的最近目标/10 Attack 行为，并保持整卡投影/提交。 |
| EditMode 回归 | `TinySpire/Assets/Editor/Tests/MachineGunnerStarterRuntimeTests.cs` 增加四项 3252 行为锁定；fixture 以表内 `Enemy` 目标规则、2 Energy 和 DiscardPile 构造该卡。 |
| 作者表 | `DataTables/Datas/battle.card.xlsx` 仅 Q142 从 `CatalogOnly` 翻为 `Implemented`。 |
| 生成和门禁 | `TinySpire/Assets/GameData/battle_tbcard.json`、卡牌目录构建校验与机枪兵快照更新到 40 / 24。 |

未修改升级实例、奖励/Run、Power HUD、Scene、Prefab、默认 Hero/Deck、ProjectSettings、asmdef、HybridCLR、启动 DI 或受保护美术路径。

## 3. 已加入的回归用例

| 用例 | 锁定事实 |
|---|---|
| `ExplosiveElbow_AttackThenAgedOilThenCurrentBurnDebuff` | 普通 Attack 后目标存活时，严格断言 Attack → AgedOil → 当前 Burn Debuff → Hand→Discard 的 settlement 顺序；Attack 使用弱点/烟雾/易伤/Block/Armor 规则，Debuff 仅使用 Block/HP，Burn 保留。 |
| `ExplosiveElbow_WithoutBurnOnlyAttacksAndDiscards` | 没有 Burn 时只产生普通 Attack 与卡区归宿；AgedOil 仍按非射击攻击生效，但不会伪造 Debuff 写入。 |
| `ExplosiveElbow_NormalLethalHitSkipsAgedOilAndCurrentBurnDebuff` | 普通 Attack 致死时不写 AgedOil、不追加 Burn Debuff，既有 Burn/Oil 保持原值，卡牌仍 Discard。 |
| `ExplosiveElbow_CurrentBurnDebuffKillsLastEnemy_DiscardsBeforeBattleEnds` | 普通 Attack 后追加 Debuff 杀死最后敌人时，先 Hand→Discard，随后由既有控制器进入 `BattleEnded`。 |
| `GeneratedCatalog_ExplosiveElbowKeepsAuthoredMetadata` | 直接读取 Luban 生成 JSON，锁定 3252 的基础/升级费用、Enemy、两处 DiscardPile、升级标记、Implemented 状态与 Program 52。 |

## 4. 本轮证据与验收结论

| 项目 | 结果 | 证据/说明 |
|---|---|---|
| 作者工作簿 | 通过 | 精确值差异、重新导入、渲染和公式错误扫描均只确认 Q142；最终 SHA-256 为 `47490664EFAFB9553BC80CD181301FE65F810B2B1F433734094A38346DF0B7D6`。 |
| Luban | 通过 | 已执行生成命令，生成器移除的 `TinySpire/Assets/GameData/game-config.json` 已立即从 `DataTables/game-config.json` 恢复。 |
| 生成 JSON | 通过 | 64 张机枪兵目录为 40 张 `Implemented` / 24 张 `CatalogOnly`；3252 为 Program 52、Cost 2、Enemy、DiscardPile。 |
| 静态编译 | 通过 | `dotnet build TinySpire/Assembly-CSharp-Editor.csproj --no-restore`：0 errors；保留 Unity 依赖图既有的 12 条 `MSB3277` 警告。 |
| `Sync and Build All` 与本地 Addressables | 通过 | 2026-08-11 在唯一既有 Unity 6000.5.5f1 Editor 执行菜单。首次 MCP 传输在编辑器域重载时断连；重连后的 Console 明确记录 BuildLayout 写入、`Addressable content successfully built (duration : 0:00:52.963)` 以及整体同步完成，未出现 Error。 |
| Unity EditMode | 通过 | Unity MCP 任务 `e336ac7ff03548d6929571c4b9c5f803`：70/70 passed，0 failed，0 skipped，1.1632452 秒；在“当前 Burn”读取收敛到同一职业状态投影后复跑，覆盖 `MachineGunnerStarterRuntimeTests`、`MachineGunnerDamagePipelineTests`、`BattleCardPlayRulesTests`、`MachineGunnerCatalogSnapshotMG2ATests` 与 `BattleCardCatalogBuildValidatorTests`，含四项 3252 行为回归。 |

## 5. 验收完成后的后续顺序

本记录的同步构建与定向 EditMode 门禁均已完成。后续 CatalogOnly 卡必须继续沿用“独立机制切片 → 精确表项翻转 → Luban → `Sync and Build All` → 定向 EditMode”的顺序；3252 的专用当前 Burn Debuff 声明不能作为延迟伤害、升级实例、奖励/Run、场景、Prefab、默认 Hero/Deck 或第二写入链的实现授权。
