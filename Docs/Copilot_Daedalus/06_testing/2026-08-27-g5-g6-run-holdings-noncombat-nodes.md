---
title: G5/G6 Run 持有物与非战斗节点验收记录
page_type: testing
lifecycle: active
date: 2026-08-27
updated: 2026-08-27
scope: G5 and G6 only
status_source: ../STATUS.md
source: ../plans/2026-08-26-g5-g6-run-holdings-noncombat-nodes.md
implementation_status: verified
---

# G5/G6 Run 持有物与非战斗节点验收记录

## 1. 当前结论

G5/G6 计划内代码、配表、生成物与聚合产品验收均已完成。本机 Unity 6000.5.5f1 先前两次批处理因没有可用 license/entitlement 返回 code 198，但许可证恢复后，`Sync and Build All`、完整 EditMode、最新 BuildLayout 与 Packed Play 产品链均取得本轮通过证据。因此本记录的最终状态是 **completed / verified**。

本轮严格停在 G6。没有实现 G7、真实 Boss/Boss 阶段、精英、RunOutcome、云存档、多槽、战中存档、多人、动态经济、事件 DSL 或架构重构；没有修改 Scene、Prefab、asmdef、ProjectSettings、HybridCLR 或 DI 架构。

## 2. 已实现范围

### S0 · 共同持有物与持久化边界

- `RunHoldings` 是 Gold、有序唯一遗物和最多三瓶有序药水的唯一 Run 事实；新 Run Gold=100，实例 ID 与金币算术均 checked。
- canonical save 提升为 schema v5，显式覆盖 holdings、attached loot 和类型化 `PendingNodeVisit`；v4/v3/v2 迁移，v1 fail-fast，Atomic durable equality 深比较有序事实与 payload。
- NodeVisit 进入、动作和完成均使用 preview 后的精确 Source/Successor，并由 Flow save-before-publish；失败保持旧状态，exact retry 不重算。

### G5 · 遗物与药水

- Relic 8001 在每场 BattleStart、玩家可提交普通行动前按 Run 顺序精确一次增加 1 Strength。
- Potion 9001 通过权威 Battle command 恢复最多 10 HP；Battle 只记录 accepted consumption，稳定 `BattleResult` 经唯一 bridge exactly-once 回写并移除实例。
- 第一次普通战斗的冻结卡牌奖励附加样本遗物/药水；选择或 Skip 在同一原子后继中获得，重进、冷启动或提交失败重试不重算。
- RunEntry 只读显示 Gold、遗物与药水；UI 不拥有库存事实。

### G6 · 混合地图与四类节点

- 保留旧 G3 profile/generator v1；新增 mixed generator/profile v2，固定可验收路线为 `Combat → Rest → Chest → Shop → Event → Combat → BossGate`。Boss 仍只是既有 gate。
- Rest：冻结 `ceil(MaxHP×30%)` 治疗值与合法升级实例，只能选择一次。
- Chest：冻结 Potion 9001，Claim/Skip exactly-once；满三瓶时 Claim 拒绝但 Skip 保持可用。
- Shop：冻结 Relic 8001/75、Potion 9001/25 与 Hero 奖励池 Card/50；每笔购买原子保存扣款、入库和售罄，仍停在同一 Pending，Leave 才完成节点。
- Event：A checked 增加 50 Gold；B 要求 Gold≥25 且未满血，原子扣 25 Gold并恢复最多 15 HP。
- Rest、Chest、Shop、Event 都使用类型化命令和 Store 终审；没有 Back 软退出、通用事件 DSL 或第二节点状态机。

## 3. 已通过的自动化证据

### G5 离线聚合

当前源码经 Unity Bee response file 在临时目录重编译为 0 errors；纯 BCL reflection runner 结果为 **155 passed、0 failed、4 skipped**：

| 测试组 | 结果 |
|---|---:|
| `RunHoldingsTests` | 16/16 |
| `BattleSessionTests` 遗物/药水 | 10/10 |
| `BattleCommandQueueTests` StartBattle/药水 | 7/7 |
| `BattleResultRunBridgeTests` | 5/5 |
| `RunCardRewardG4Tests` attached loot | 1/1 |
| `RunStateStoreTests` G5 | 12/12 |
| `RunFlowServiceTests` G5 | 15/15 |
| `RunSaveDocumentTests` G5 | 36/36 |
| `AtomicJsonRunSaveStoreTests` G5 | 48/48 |
| `BattleTurnHudPresentationTests` 纯投影 | 2/2 |
| `RunEntryPresenterTests` 持有物/奖励 | 3/3 |

四个真实 `GameObject` 用例在离线 runner 中按边界明确跳过；最终完整 Unity EditMode 已执行并补齐这些用例，最终 skipped 为 0。

### G6 滚动与最终聚合

| 停止点 | 非 Unity 定向结果 |
|---|---:|
| G6-A mixed map / entry | 29/29 |
| G6-B Rest 聚合 | 143/143 |
| G6-C Chest 聚合 | 274/274 |
| G6-D Shop 聚合 | 285/285 |
| G6-E 最终 G6 聚合 | **312/312** |

最终 312 项覆盖 NodeVisit 12、Store 35、Flow 49、RunSaveDocument 93、Atomic 86、Presenter 35、Localization key 1 与纯 View action 1，结果为 0 failed、0 skipped。独立 G6-D/G6-E 只读审查未发现其他 P1/P2 或假绿；审查指出的 Shop 零购买 Leave、Leave 保存失败 exact retry 与已完成节点重进三处直接证据已先补测试再复审通过。

### 最终范围审查的 attached loot RED→GREEN

最终只读审查发现：schema v5 恢复只校验 attached loot 模板存在，未把它与已完成普通 Combat 路径及当前 holdings 对照；篡改第二场 `RewardPending` 的 `potionTemplateId` 为合法 9001 后，旧实现会恢复成功并在选择/Skip 时再次发药水。新增 `CreateRestore_LaterRewardWithForgedAttachedPotion_ReturnsInvalidDocument` 先得到 **0 passed / 1 failed**，实际为 `Expected InvalidDocument / But was Success`。

最小修复让恢复后 Run 依据已完成普通 Combat 数量与 holdings 重建权威 attached loot，并逐字段拒绝伪造或删除应冻结事实；后续奖励伪造药水与首战删除应有药水两项均 GREEN。之后完整重跑得到上述 G5 155 passed 与 G6 312/312；最终审查未发现其他 P1/P2。

### 编译、生成与资源

- 生产 `Assembly-CSharp.csproj`：**0 errors / 6 warnings**。
- Editor `Assembly-CSharp-Editor.csproj`：**0 errors / 12 warnings**。
- warnings 是既有 Unity/R3 引用冲突；没有本轮编译错误。Unity 生成 csproj 尚未列出新增 `RunNodeVisitEntryFactory.cs`，离线静态 build 通过临时 `CustomAfterMicrosoftCommonTargets` 只为编译注入该已存在源码，没有改项目文件。
- `DataTables/gen.bat` 于 2026-08-27 03:02:45 成功完成 Luban 生成；生产 workbook 回读确认新增 Relic/Potion 表和 Event i18n 值准确。
- 12 个新增 Unity `.meta` 均存在，GUID 在新增集合和完整 `Assets` 树内唯一。
- Rider MCP 增量 build session `493c9cdb-1508-4e04-ab29-deb3bad19211` 成功，最终 project problems 为 **0**。

这些定向证据与下述 Unity Editor、Addressables bundle 和 Packed Play 产品链共同构成本轮验收。

## 4. Unity 聚合验证

在确认没有 Unity 进程占用项目后，先后运行：

```powershell
E:\Env\Unity\6000.5.5f1\Editor\Unity.exe -batchmode -nographics -quit -projectPath E:\Project\TinySpire -executeMethod TinySpireBuildTools.SyncAndBuildAll -logFile E:\Project\TinySpire\Logs\g56-sync-build-all.log

E:\Env\Unity\6000.5.5f1\Editor\Unity.exe -batchmode -quit -projectPath E:\Project\TinySpire -executeMethod TinySpireBuildTools.SyncAndBuildAll -logFile E:\Project\TinySpire\Logs\g56-sync-build-all-license-retry.log
```

两个 Unity 日志都显示当时没有有效许可证/可用 entitlement，第二次仍返回 **code 198**；两次都没有进入项目编译或 `TinySpireBuildTools.SyncAndBuildAll`。这是已解除的历史阻塞，不再代表最终交付状态；验证过程中没有删除现有 `Temp/UnityLockfile`，也没有通过手工改 Localization 或 Addressables 绕过产品流程。

### 许可证恢复后的 Sync/Build 与 BuildLayout

- 在唯一 Unity Editor 中执行 `TinySpire/Build/Sync and Build All` 成功；Luban、Localization 同步与 Local Addressables 均完成。
- 最新 `TinySpire/Library/com.unity.addressables/buildlayout.json` 的 `BuildError` 为空，12/12 bundles `BuildStatus=0`。
- `run_tbrelic.json` 与 `run_tbpotion.json` 位于 `TinySpire GameData` 的 PackTogether bundle，bundle provider 为 `UnityEngine.ResourceManagement.ResourceProviders.AssetBundleProvider`，asset provider 为 `BundledAssetProvider`。
- 物理 bundle 存在，SHA-256 为 `BB0FB1DB789F8851538E07C4A6ECF50014C4987CF93792F54D7FDE2925FA9610`。

### 完整 Unity EditMode

- `TestResults.xml`：2026-08-27 21:43:42，duration **74.8235407s**，**1348/1348 passed、0 failed、0 skipped**。
- 首次完整运行得到 1347 passed / 1 failed，暴露 `RunG4ProductionAcceptanceTests.BothProductionHeroes_ColdRestoreFrozenRewardAndDrawSelectedInstanceNextBattle` 仍假定奖励结算后下一个节点必为 Combat；G6 production v2 路线插入 Rest/Chest/Shop/Event 后，该旧假设不成立。
- 验收测试改为按真实 G6 必经 NodeVisit 逐一完成 Rest、Chest Skip、Shop Leave 与 Event GainGold，再进入下一场 Combat；定向测试转绿后，完整 1348 项重跑全绿。此修复只更新相邻 G4 acceptance 对当前生产路线的接线，不改变 G4/G6 玩法语义。

### Packed Play 真实 bundle 启动链

- UnityMCP 将 Addressables active builder 切到 index 1（Use Existing Build / Packed Play），从 Bootstrap 进入 RunEntry。
- 以 Hero 1001 新建 schema v5 Run；实际 profile 为 `tinyspire.act1.g6.v1`，实际地图为 `Combat → Rest → Chest → Shop → Event → Combat → BossGate`，Boss 仍只是既有 gate。
- 启动链运行期间 Console Error、`InvalidKey` 与 `ConfigInitializationException` 计数均为 **0**。`ConfigService` 在切场景前强制初始化包括 Relic/Potion 在内的 required tables；结合 BuildLayout 的 `AssetBundleProvider` 归属，这证明新增 Run GameData 经真实 bundle 加载。
- Packed 范围停在 schema v5 `MapReady` 与 mixed route 展示，没有声称手工重走 Rest/Chest/Shop/Event 的全部互斥与失败分支；这些玩法语义由完整 1348/1348 Unity EditMode acceptance 覆盖。
- 验收后已退出 Play 并恢复 active builder index 0（Use Asset Database / Fast Mode）。
- 用户原 `run-save.json` 已按 SHA-256 `419058435D82A48EA08DBF3121F6127417EAC700D302388BFFFA4586DFEE54B9` 精确恢复；测试生成档未覆盖用户存档。

## 5. 最终验收结论

G5/G6 的领域、持久化、生成、完整 Unity EditMode、真实 bundle 与 Packed Play 启动链证据均通过，阶段标记为 `completed`，S0、G5-B～D 与 G6-A～E 标记为 `verified`。本轮仍严格停在 G6，不进入 G7。

## 6. 表格、生成物与资源清单

本轮 DataTables/生成资产边界：

- 修改 `DataTables/Datas/__tables__.xlsx`、`DataTables/Datas/i18n.xlsx`。
- 新增 `DataTables/Datas/run.relic.xlsx`、`DataTables/Datas/run.potion.xlsx`。
- 修改生成入口 `TinySpire/Assets/Scripts/Core/Generated/Config/Tables.cs`。
- 新增 `TinySpire/Assets/Scripts/Core/Generated/Config/run/` 下 Relic/Potion bean 与 table C# 及 `.meta`。
- 新增 `TinySpire/Assets/GameData/run_tbrelic.json`、`run_tbpotion.json` 及 `.meta`。
- Localization 资产、Addressables settings/group manifest 与 BuildLayout 已由成功的 `TinySpire/Build/Sync and Build All` 同步/重建；本轮没有 package manifest/lock 变化。

实现同时在现有 Run/Battle/Map/Persistence/RunEntry seam 内增加或扩展 holdings、node visit、settlement、presentation 与相应 Editor tests；没有为了清单制造 Scene/Prefab/asmdef/package 改动。

## 7. 工作区与交付状态

- 用户原有 `TinySpire/ProjectSettings/ProjectSettings.asset` 中 `Standalone: APP_UI_EDITOR_ONLY;DOTWEEN` 保持不变，本轮不纳入交付。
- 用户原有未跟踪 `TinySpire/.codex/` 保留且未清理。
- 验收过程没有执行 `git add .`、reset、clean 或删除项目锁；最终 Git 暂存、commit 与 push 由精确交付步骤单独执行并报告。
- 当前产品验证已完成；Git 本地 commit 与 remote push 状态不得与验证结果混称。
