---
title: G7 单 Act、精英、Boss 与 Run 终局验收记录
page_type: testing
lifecycle: active
date: 2026-08-28
updated: 2026-08-28
scope: G7 only
status_source: ../STATUS.md
source: ../plans/2026-08-28-g7-single-act-elite-boss-outcome.md
implementation_status: verified
---

# G7 单 Act、精英、Boss 与 Run 终局验收记录

## 1. 当前结论

G7-A～E 的代码、配表、生成物、构建期门禁与聚合产品链均已完成。本轮取得最终 Rider、Unity 定向与完整 EditMode、Luban、`TinySpire/Build/Sync and Build All`、最新 BuildLayout 以及 Packed Play 三种终局分支的真实证据，因此 G7 阶段为 **`completed`**，G7-A～E 切片均为 **`verified`**。

本轮严格停在 G7。G8 仍为 `not-started`，首个切片仅是 `candidate`，没有获得 Grill、计划或实施授权；多 Act、Ascension、每日挑战、多个真实 Boss Encounter、通用 Boss DSL、云/多槽/战中存档、联网排行榜和多人均未实现。Scene、Prefab、asmdef、ProjectSettings、HybridCLR、DI 与启动流程没有纳入功能修改。

## 2. 已实现范围

### G7-A · Act、地图与内容清单

- 新生产 profile 为 `tinyspire.act1.g7.v1`，继续使用 mixed generator v2；G3/G6 profile、generator 与既有 fingerprint 均保持不变。
- 确定性生产路线为 `Combat(5001) → Rest(7101) → Chest(7201) → Shop(7301) → Event(7401) → Combat(5001) → Elite(5101) → Boss(9001/9002/9003)`。
- `ActContentManifest` 只聚合普通/Elite pool、Boss identity 到 Encounter 的解析、`BossVictory` 完成规则与唯一 Relic 8001，不复制 MapDefinition、边、路径或 Run 进度。
- `RunActContentBuildValidator` 从同一 G7 manifest 贯通 Map、Node、Encounter、Enemy、Behavior Group、Behavior、Reward、Event、Item 与 i18n 引用，并拒绝空池、坏引用、不可达 Boss、非法单敌/阶段组、重复唯一奖励和缺失文本。

### G7-B/C · Elite 与 Battle-owned Boss phase

- Elite 使用既有 Battle setup/result seam：Encounter 5101、Enemy 2101、Behavior Group 6101；胜利继续进入 G4 冻结卡牌奖励，失败进入统一 Defeat 终局。
- Boss identity 9001/9002/9003 均解析到唯一真实 Encounter 5201；Boss Enemy 2201 的 Phase I/II 分别使用 Behavior Group 6201/6202，同一 Battle session 内不重生、不生成第二波。
- `battle.encounter` 新增 nullable `int? phase_two_behavior_group_id`：`null/0` 表示没有二阶段，Encounter 5201 配置为 6202。该字段已由 Luban 生成到 C# 与 JSON。
- Boss 开场冻结 Phase I 意图；首次权威敌人行动完成时，在同一 prepared completion 中恰好一次切到 Phase II，并冻结下一意图。重复或 stale completion 零写入；phase 不进入 Run、save 或 UI 写入口。

### G7-D · RunOutcome 与终局持久化

- `RunStateStore` 持有唯一不可变 `RunOutcome(Victory / Defeat / Abandoned)`；RunEntry 只投影并提交稳定命令。
- canonical save 提升到 schema v6。v5 `Terminal(Defeat)` 显式迁移为 Defeat outcome；非终局 v5 继续按原 profile 恢复，旧 G3/G6 BossGate 不会被误解析成 G7 Boss。
- BossGate 先作为稳定检查点耐久提交；Flow 先校验生产 Encounter 表，Store public 入口再独立要求 G7 profile 并按当前 Boss identity 从同一 manifest 解析 Encounter 5201，legacy/未知 profile 不能绕过 Flow；之后继续走既有 `BattleSetupOptions`、BattleScene、`BattleResult` 与 result bridge。
- Combat/Elite 胜利继续生成 G4 `RewardPending`；Boss 胜利直接生成 `Terminal(Victory)` 且不发普通卡牌奖励。任意真实战败生成 `Terminal(Defeat)`；玩家从稳定 RunEntry 主动放弃先生成 `Terminal(Abandoned)`，不直接删除活动档。
- 三类 outcome 均先耐久保存再发布，提交失败保留 exact successor 供 retry；Atomic 对 RewardPending 和 terminal 都从 live profile/version/seed 重建 recipe，核对 fingerprint 与完整路径，并只接受当前节点直达 Combat/Elite 或合法 BossGate/outcome 的后继。terminal 冷启动不可 Continue，结果页确认后才清理并回主菜单。普通战斗失败仍没有同节点 retry。

### G7-E · 聚合门禁与完整 Run

- 构建入口在配置 manifest validator 之后、Localization/Addressables 之前执行 G7 内容聚合校验；坏内容无法进入本地内容构建。
- 同一生产 Store/Flow/Map/Battle seam 已覆盖完整单 Act，以及 Victory、Defeat、Abandoned 三种互斥终局；没有第二地图、第二 Battle result 通道或第二 Outcome store。

## 3. RED → GREEN 与自动化证据

### 定向 G7 集合

- 首次聚合运行得到 **425/428 passed、3 failed**。三项失败分别暴露旧 Boss 候选集合断言仍假设候选不重复、v4 fixture 残留 v6 `outcomeKind` 字段，以及 schema 版本断言仍固定为 v5。
- 修复仅更新 fixture/兼容期望：允许冻结 Boss identity 按确定性结果重复出现，构造真实 v4 JSON 时移除未来字段，并统一断言 `RunSaveDocument.CurrentSchemaVersion`；没有放宽生产迁移器或内容 validator。
- 该轮阶段性回归为 **428/428 passed、0 failed**；它保留为首次集成历史，不是终审后的最终定向数字。

### Store 来源闭合终审 RED → GREEN

- 最终只读审查发现两类 seam 缺口：Atomic 普通 RewardPending/Defeat 后继尚未证明 committed node 是 live recipe 当前路径的直达 Combat/Elite；Store public Boss 入口也必须独立拒绝 legacy/未知 profile 并从 manifest 解析 Encounter。生产形状矩阵同时补齐 Elite Victory→RewardPending 与 Boss Defeat→Terminal/no reward。
- Unity RED job `e81cc15a7483467291b7b9d72094fc1f`：**510 total / 505 passed / 5 failed**。四项来自 Atomic 旧 fixture 与新增来源门禁，一项来自 Boss round-trip catalog profile；这些失败证明新门禁先于兼容修复生效。
- 保持生产门禁，改用真实 recipe/fingerprint/direct-edge fixture 并让 round-trip catalog 支持 G7 profile 后，Unity GREEN job `60ec69d046b5442cb593a8bef123c0f1`：**510/510 passed、0 failed、0 skipped，8.0937884s**。
- Atomic 现在对新 RewardPending、损坏 intent 修复、既有 intent retry 与普通 Defeat 都重建 live map，逐边验证 path，并只接受当前节点直达的 Combat/Elite；未知、非战斗、间接节点和伪造 residual intent 都不能覆盖 live。

### Rider 与完整 Unity EditMode

- Rider MCP 最终 solution build session `e750f929-d9bf-4cfd-bbf6-d715c237be51`：`Completed / buildIsSuccess=true / problems=[]`。
- Unity MCP 完整 EditMode job `9758c02e718540aa97e5e26f832794e3`：**1410/1410 passed、0 failed、0 skipped，23.0963649s**。
- 上述 1410 项包含 G7 map/manifest、Elite/Boss、prepared phase completion、RunOutcome/schema v6/migration、Atomic RewardPending/terminal predecessor closure、Flow/Presenter/View、生产内容 acceptance 与相邻 G1～G6 回归。

## 4. 表格、Luban、Localization 与生成物

- 通过 Spreadsheet artifact 流程修改并回读 `DataTables/Datas/battle.encounter.xlsx`、`battle.enemy.xlsx`、`battle.enemy_behavior_group.xlsx`、`battle.enemy_behavior.xlsx` 与 `i18n.xlsx`；公式错误扫描为 0。
- Luban 生成成功，更新 `battle_tbencounter.json`、`battle_tbenemy.json`、`battle_tbenemybehaviorgroup.json`、`battle_tbenemybehavior.json` 及对应 generated C#；Encounter 5201 的 `phase_two_behavior_group_id=6202`，无二阶段 Encounter 为 null/0。
- Localization 同步更新 `Battle Cards Shared Data.asset`、`Battle Cards_en.asset` 与 `Battle Cards_zh-CN.asset`，覆盖 Elite/Boss、阶段、地图节点和三种 outcome 的运行时文本。
- Elite/Boss 复用既有 `pfb_char_enemy` 短键和现有 Effect，没有新增素材域、Addressables 逻辑地址前缀或 Group。

## 5. Sync and Build All 与 BuildLayout

- 唯一 Unity Editor 中执行 `TinySpire/Build/Sync and Build All` 成功；本轮构建起点为 `2026-08-27T18:59:45.644Z`。
- 最新报告为 `TinySpire/Library/com.unity.addressables/BuildReports/buildlayout_2026.08.28.02.59.50.json`，mtime `2026-08-27T19:00:02.6212682Z`，SHA-256 `BAF93C72F09D968197A9B54DF56803E8EE16160FF8E67EDD00B0BBAEE424B015`。
- 报告 `BuildError` 为空，`BuildScript=Default Build Script`。本轮精确提取的 7 个目标是四份 G7 GameData JSON、`pfb_char_enemy.prefab`、RunEntryScene 与 BattleScene；它们均位于 `BuildStatus=0` 的物理 bundle，bundle provider 为 `UnityEngine.ResourceManagement.ResourceProviders.AssetBundleProvider`，对应 bundle 文件均存在。三份 Localization 资产已由成功的同步构建更新，但本轮没有把它们冒充为这组七目标提取证据。
- 这份报告与下述 Use Existing Build 产品链共同证明真实 AssetBundle 加载；Fast Mode 没有被当作 AB 证据。

## 6. Packed Play 真实 UGUI 产品链

UnityMCP 将 Addressables builder 从 index 0 临时切到 index 1（Use Existing Build），三条分支均从 Bootstrap 的真实 UGUI 进入生产 Run 流程，并在结束后返回主菜单。

### Victory · 完整单 Act

- 实际路线为 `Combat → Rest → Chest → Shop → Event → Combat → Elite → Boss`，与 `tinyspire.act1.g7.v1` 冻结路线一致。
- Boss 的实际行为意图序列为 `5 → 8 → 8`：首次权威行动后从 Phase I 切至 Phase II，后续没有再次切换。
- 最终磁盘文档为 schema v6、`Terminal/Victory`，没有 pending reward；结果页显示章节完成，确认后回 MainMenu，`Continue=false`。

### Abandoned · 稳定地图主动放弃

- 从 `MapReady` 经真实确认动作放弃，最终文档为 schema v6、`Terminal/Abandoned`。
- 结果页显示“本局已放弃”，确认后清理终局并回主菜单，不能继续该 Run。

### Defeat · 真实首战失败

- 首战通过真实 EndAction 连续执行三次，玩家生命按 `30 → 18 → 6 → 0` 结算。
- 最终文档为 schema v6、`Terminal/Defeat`；结果页显示战斗失败，确认后回主菜单，没有恢复普通战斗失败重试。

三条产品分支各自查询的 Console Error、`InvalidKey` 与 `ConfigInitializationException` 均为 **0**。

## 7. 环境恢复、范围与 Git 状态

- 验收前用户原 `run-save.json` 为 302 bytes、SHA-256 `419058435D82A48EA08DBF3121F6127417EAC700D302388BFFFA4586DFEE54B9`；三分支完成后已精确恢复同一文件与哈希。
- Addressables builder 已从 index 1 恢复到 index 0；`AddressableAssetSettings.asset` 验收前后 SHA-256 比较不变（记录标识 `071B…57E`）。BootstrapScene 最终 `dirty=false`。
- 本轮没有修改 Scene、Prefab、asmdef、ProjectSettings、HybridCLR、DI 或启动流程，也没有进入 G8。
- 用户已有 `.gitignore` 与 `AGENTS.md` WIP 保留且必须排除在 G7 暂存之外；没有执行 broad reset/clean 或删除 Unity 锁文件。
- 用户已经授权 G7 完成后的精确 commit 与 push，但本验收记录形成时尚未执行 Git 交付。最终本地 commit 与远端 push 结果必须由交付步骤及最终回复分别报告，不能与 `completed / verified` 验收状态混称。
