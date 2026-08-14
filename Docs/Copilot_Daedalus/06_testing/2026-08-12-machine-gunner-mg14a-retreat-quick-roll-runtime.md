---
title: Marine Game 机枪兵 MG14A 撤退与快速翻滚运行时
page_type: testing
lifecycle: active
date: 2026-08-12
updated: 2026-08-12
status: verified-unity-native-2026-08-12
status_source: ../SESSION_LOG.md
source:
  - ../00_inbox/README web.md
  - ../01_requirements/2026-08-07-marine-game-card-requirement-digest.md
related_plan: ../plans/2026-08-07-marine-game-card-only-integration.md
related_decision: ../CODE_DECISIONS.md#cd-078下回合私有状态和队列签发的强制结束行动
---

# Marine Game 机枪兵 MG14A 撤退与快速翻滚运行时

## 1. 验收对象与冻结行为

本记录覆盖 Hero 1002 的两个基础态职业程序：`Retreat` (3216) 与 `QuickRoll` (3235)。作者表元数据分别为 2 Energy / Self / Hand→DiscardPile，以及 1 Energy / Self / Hand→DiscardPile；当前 CardInstance 没有升级态，因此本切片只实现基础值，不伪造升级数值。

`Retreat` 成功支付后获得 15 Block、预约一次 `ReloadAmmoAtNextPlayerRound`，卡牌归入 DiscardPile，并请求结束本次玩家行动。该请求不是职业运行时嵌套提交命令：控制器在成功归宿且战斗仍为 Ongoing 后冻结强制结束锁，Queue 以系统 token 追加 `EndPlayerActionCommand` continuation。下一玩家回合开始时，先按既有档案补充 Ammo，再清除预约状态并将 Ammo 补至当前最大值。

`QuickRoll` 成功支付后立即获得 5 Block，并叠加 `NextRoundBlock +5`。下一玩家回合开始时清除该私有状态，再获得其冻结总值的 Block；多张卡的预约值相加，且只结算一次。

本切片不实现 `TacticalAdvance` (3234)、免费攻击、束缚、奖励/Run、升级实例、HUD、场景或第二条写入链。3234 的“免费攻击×兴奋剂”弹药优先级以及束缚前置规则仍须单独裁决。

## 2. 已变更表面

| 层 | 变更 |
|---|---|
| 职业状态与程序 | `MachineGunnerCombatState.cs` 新增 `NextRoundBlock` 与 `ReloadAmmoAtNextPlayerRound`；`MachineGunnerBattleRuntime.cs` 注册 Program 16/35 的基础行为，并返回受控结束行动意图。 |
| 回合开始时机 | `BattleTurnController.cs` 在既有 Block 清除后结算下挡，在常规资源档案补充后结算补满弹药；所有状态、Block 和 Ammo 变化继续写入统一 settlement。 |
| 命令续延 | `BattleTurnOperationResult` 只携带请求结束的 actor；`BattleCommandQueue.cs` 冻结并签发系统 `EndPlayerActionCommand`，`BattleCommandSchedulingCore.cs` 只允许受 token 认证的该类 continuation。 |
| 失败与辅助断言 | 控制器在强制结束锁期间拒绝普通 Play/End；`MachineGunnerStarterRuntimeTests` 的普通 `Play` 辅助保持“恰好一条结果”断言，撤退改用显式 `Submit` 读取续延链。 |
| 作者表与目录门禁 | `DataTables/Datas/battle.card.xlsx` 仅将 Q106 (3216) 与 Q125 (3235) 从 `CatalogOnly` 翻为 `Implemented`；Luban JSON、构建校验和机枪兵目录快照更新为 44 / 20。3234 未改。 |

## 3. 已加入的回归用例

| 用例 | 锁定事实 |
|---|---|
| `Retreat_EndsActionThroughQueueAndRefillsAmmoAtNextPlayerRound` | 支付 2 Energy、获得 15 Block、预约状态 0→1、Hand→Discard；权威链首两条为 PlayCard→EndPlayerAction，下一回合 Ammo 先 0→1、再 1→5，预约状态 1→0。 |
| `Retreat_InsufficientEnergyLeavesZonesAndScheduledReloadUnchanged` | 费用不足时不增加 Block、不移动卡、不创建预约状态，也不生成系统结束行动。 |
| `QuickRoll_StacksNextRoundBlockAndConsumesItOnceAtPlayerRoundStart` | 两张卡使预约状态 0→5→10、即时 Block 为 10；下回合清空预约并获得 10 Block，后续回合不重复结算。 |
| `QuickRoll_InsufficientEnergyLeavesBlockAndNextRoundStateUnchanged` | 费用不足时不写 Energy、Block、私有状态或卡区。 |
| `CompleteCurrent_AllowsEndPlayerActionAsSystemContinuation` | 调度核心只以系统 token 接受并消费一次 EndPlayerAction continuation，authority sequence 保持连续。 |
| `GeneratedCatalog_RetreatKeepsAuthoredMetadata` / `GeneratedCatalog_QuickRollKeepsAuthoredMetadata` | 直接读取 Luban JSON，锁定两张卡的费用、Self、双 DiscardPile、升级标记、Implemented 状态与 Program ID。 |

## 4. 本轮证据与验收结论

| 项目 | 结果 | 证据/说明 |
|---|---|---|
| 作者工作簿 | 通过 | 只修改 Q106/Q125，最终 `battle.card.xlsx` SHA-256 为 `DFDA339D3E1654176A75E6A8F7E3875B33021676477D37FE55CB7179D5128E05`。 |
| Luban 与生成配置 | 通过 | 已执行生成并恢复生成器移除的 `game-config.json`；`DataTables/game-config.json` 与 `TinySpire/Assets/GameData/game-config.json` SHA-256 同为 `048CDC9E8DB80F80BE9E43D409ED1A91A011E0118CBAB18EC207509B3C904CF8`。生成目录为 44 张 `Implemented` / 20 张 `CatalogOnly`。 |
| 静态编译 | 通过 | `dotnet build TinySpire/Assembly-CSharp-Editor.csproj --no-restore -v:q` 为 0 errors；保留项目既有的 12 条 `MSB3277` 引用版本警告。 |
| `Sync and Build All` 与本地 Addressables | 通过 | 2026-08-12 在唯一既有 Unity 6000.5.5f1 Editor 运行菜单；Console 记录 `Addressable content successfully built (duration : 0:00:20.219)` 与 `TinySpire sync and local content build completed successfully.` |
| Unity 定向 EditMode | 通过 | MCP 任务 `534c896788734535bf40275aadf41083`：7/7 passed，0 failed，0 skipped，0.0630653 秒。 |
| Unity 完整 EditMode | 通过 | 在收紧普通卡辅助断言后，MCP 任务 `352921ecce6246cda6cf792348a0c393`：571/571 passed，0 failed，0 skipped，221.6869278 秒。资源 Addressables 用例的进度心跳会延迟，但最终结果为 Passed。 |

## 5. 验收后边界

后续 20 张 `CatalogOnly` 卡仍按独立机制切片处理。`TacticalAdvance` (3234) 继续保持 CatalogOnly：本轮没有为“下一张攻击免费”猜测兴奋剂额外射击的弹药支付，也没有把未实现的束缚状态伪装成可玩规则。撤退和快速翻滚的延迟状态仅服务于本次明确行为，不等同于通用延迟效果总线、奖励/Run、升级实例或 UI 表现系统。
