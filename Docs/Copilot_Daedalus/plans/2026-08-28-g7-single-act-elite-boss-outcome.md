---
title: G7 单 Act、精英、Boss 与 Run 终局窄计划
page_type: plan
lifecycle: archived
date: 2026-08-28
updated: 2026-08-28
scope: G7 only
source: 用户 2026-08-28 G7 实现授权、Unity 控制授权与完成后 commit/push 授权；RUN_ROADMAP.md G7-A～E
status_source: ../STATUS.md
implementation_status: verified
---

# G7 单 Act、精英、Boss 与 Run 终局

> **归档状态：** G7-A～E 已完成并 `verified`；本页保留为实施 seam、串行停止点与验收来源。当前状态和后续授权只查 [STATUS.md](../STATUS.md)，本轮严格停在 G7。

本计划只闭合 [RUN_ROADMAP.md](../RUN_ROADMAP.md) 的 G7-A～E。当前状态与授权只查 [STATUS.md](../STATUS.md)；本页负责冻结实现 seam、串行停止点和验收证据，不授权 G8 或其他产品化范围。

## 1. 目标与完成定义

同一个新 Run 必须沿唯一 `MapDefinition` 完成：

```text
MainMenu → New Run
→ Combat → Rest → Chest → Shop → Event → Combat → Elite
→ BossGateReached → Boss Battle → RunOutcome
→ 结果页确认 → MainMenu
```

G7 只有在以下事实同时成立时才完成：

- 新 G7 profile 的确定性路线至少包含一个精英和一个可达 Boss；相同 root seed/save 重建相同地图、Boss 候选和冻结内容。
- Elite 与 Boss 都复用现有 `BattleSetupOptions`、BattleScene、`BattleResult` 与 `BattleResultRunBridge`；不得出现第二条结果通道。
- Boss 阶段事实只由 Battle 拥有并恰好切换一次；Run、save 与 UI 不写阶段事实。
- `RunStateStore` 持有唯一不可变 `RunOutcome(Victory/Defeat/Abandoned)`；Boss 胜利、任意稳定战败和主动放弃都只结算一次。
- 终局先耐久提交再发布，冷启动不可 Continue；确认离开安全清理终局并回主菜单。
- 构建期门禁覆盖 Act→Map→Node→Encounter/Reward/Event/Item 引用，并拒绝空池、坏引用、不可达 Boss、同一候选/结算中的重复唯一奖励、缺 i18n/素材键。
- Rider 编译、Unity 定向与完整 EditMode、Luban、`Sync and Build All`、BuildLayout、Packed Play 产品链全部给出本轮新证据，产品 Console Error/InvalidKey/配置初始化错误为 0。

## 2. 冻结的深模块与公共 seam

本轮深化既有 module，不另造 Boss map、Outcome store、Boss result bridge 或第二套节点状态机：

- `MapDefinition / ActMapProfile / MapReachability` 继续是完整路线的唯一事实；G3/G6 profile、generator version 与历史 fingerprint 保持不变。
- 新增窄 `ActContentManifest`，只把 G7 profile 的普通/Elite pool、Boss identity→Encounter 映射与 `BossVictory` completion rule 聚合为一份不可变内容清单。Manifest 不复制节点、边、路径或 Run 进度。
- `RunStateStore` 继续是 Run 可变事实唯一写入口；Flow 只做 save-before-publish、场景编排与重试，Presenter/View 只投影并提交稳定 identity。
- `BattleEnemyIntentsData` 继续拥有敌人行为选择、独立 RNG、prepared completion 与只读 layout；Boss phase 在这个 module 内深化，不进入 Queue、TurnController、BattleResult 或 Run save。
- 现有 `BattleSetupOptions`、`BattleCommandQueue.Submit`、`BattleResult`、`BattleResultRunBridge`、Bootstrap/DI、Scene、Prefab、asmdef 与 ProjectSettings 保持不变。

## 3. 生产内容矩阵

| 内容 | 固定身份 | 最小语义 |
|---|---:|---|
| 普通 Encounter | 5001 | 保留当前生产普通战斗 |
| Elite Encounter / Enemy / Group | 5101 / 2101 / 6101 | 单体高生命；Buff 与 Attack 受 cooldown/max-consecutive 约束形成差异化轮转；胜利继续走 G4 奖励 |
| Boss Encounter / Enemy | 5201 / 2201 | 单敌、同一 Battle session；不重生、不生成第二波 |
| Boss Phase I / II Group | 6201 / 6202 | Phase I 使用已明示意图；首次权威敌人行动完成后恰好一次切到 Phase II，再冻结下个意图 |
| Boss identity | 9001 / 9002 / 9003 | 保留 G3 冻结的多个候选身份和终点，但全部解析到唯一真实 Encounter 5201 |

新 profile ID 固定为 `tinyspire.act1.g7.v1`，继续使用 mixed generator v2 的 fixed-layer 算法，生产路线为：

```text
Combat(5001) → Rest(7101) → Chest(7201) → Shop(7301)
→ Event(7401) → Combat(5001) → Elite(5101) → Boss(9001..9003)
```

## 4. G7-C Boss phase 口径

用户要求阻断尽量由 Agent 自行决定；实施前已经向用户给出推荐口径且未收到反对，因此按下述最小模型继续。若后续用户明确改口，在产生不兼容存档或提交前优先服从新指令。

- `battle.encounter` 新增 nullable `int? phase_two_behavior_group_id`：`null/0` 表示没有二阶段，正值只允许单敌 Encounter。
- Boss 开场按 Phase I group 选择并明示意图；该意图必须完整执行，绝不在表现期间被替换。
- 第一次合法 `CompleteEnemyActionCommand` 的 prepared completion 同时冻结 phase、下一 BehaviorId、history 与 RNG；commit 一次发布 Phase II 和下一意图。后续永不回切。
- Phase 不是血量阈值、回合表达式或脚本字段，不引入通用 Boss DSL；敌人死亡时仍由既有 terminal rules 直接结束，不产生阶段切换。
- UI 若显示 phase，只读取同一不可变 intent layout；没有 UI 写入口。

## 5. RunOutcome 与持久化口径

- `RunProgressPhase.Terminal` 继续表达生命周期；`RunOutcomeKind` 闭合为 `Victory / Defeat / Abandoned`，`RunOutcome` 是终局唯一业务事实。
- Boss 节点选择后仍先追加到 Path 并耐久进入 `BossGateReached`。新增 `BeginBossBattle` 由 Store public 入口独立限制 G7 profile：Boss 已在 Path，不能再次追加；EncounterId 按当前 Boss identity 由 Manifest 解析，legacy/未知 profile 不能绕过 Flow，Battle attempt 只增加一次。
- Combat/Elite 胜利继续产生 G4 `RewardPending`；Boss 胜利直接产生 `Terminal(Victory)`，不发普通卡牌奖励；任意稳定战败产生 `Terminal(Defeat)`。
- 主动放弃不再直接删除活动档，而是从明确允许的稳定 RunEntry phase 产生 `Terminal(Abandoned)`，展示结果页后再由确认动作清理。
- canonical schema 提升到 v6。v5 `Terminal(Defeat)` 明确迁移成 Defeat outcome；v5 非终局存档继续按原 profile 恢复，但 legacy G3/G6 BossGate 不允许启动 G7 Boss。
- terminal intent 的 successor 验证按来源封闭：普通/Elite/Boss battle checkpoint 只能产生对应胜败，稳定可放弃状态只能产生 Abandoned；不得放宽为接受任意 terminal document。
- 终局已落盘或恢复后不可 Continue；普通战斗 Defeat 继续没有同节点 retry。

## 6. RED→GREEN 串行停止点

### G7-A · Act/Map/Manifest

先用测试固定 `MapNodeKind.Elite`、G7 profile 路线、同 seed 确定性、Boss 可达与旧 G3/G6 fingerprint 回归；再实现 Manifest 与 Boss resolver。通过前不改生产新 Run 入口。

### G7-B/C · Elite 与 Boss Battle

先用生产形状 fixture 得到 Elite setup/reward 与 Boss phase 的 RED；再增加最小表格内容和 phase prepared completion。必须覆盖 prepare 零写入、首次切换、重复/stale commit、同 seed 一致、Boss 单敌/行为组约束。

### G7-D · Outcome/终局

按 domain→Store→codec/migration→atomic journal→Flow→Presenter/View 顺序 RED→GREEN。每层覆盖伪造、过期、重复输入零写入、save 失败不发布、exact retry、冷恢复和确认删除。

### G7-E · 聚合门禁与产品链

新增独立 `RunActContentBuildValidator`，在现有配置 manifest validator 之后、Localization/Addressables 之前执行。生产 acceptance 从真实生成 JSON 和同一 Store/Flow 跑完整 Act；之后完成 Rider、Unity、Luban/Addressables、BuildLayout 与 Packed Play 证据。

## 7. 表格、生成与素材边界

预计编辑 `__beans__.xlsx`、`battle.encounter.xlsx`、`battle.enemy.xlsx`、`battle.enemy_behavior_group.xlsx`、`battle.enemy_behavior.xlsx` 与 `i18n.xlsx`，统一使用 Spreadsheet artifact 流程维护并渲染回读。之后必须运行 Luban，生成 C#/JSON，再执行 `TinySpire/Build/Sync and Build All`。

Elite/Boss 复用现有短键 `pfb_char_enemy` 和既有 Effect，不新增素材域。若实现审计证明确需 Scene、Prefab、asmdef、ProjectSettings、HybridCLR 或 DI 修改，必须停止并向用户说明影响、风险和回滚；当前计划不包含这些高影响文件。

## 8. 完成验收与交付

1. Rider MCP solution build 成功、Errors problems=0。
2. Unity MCP 定向测试全绿；随后 fresh full EditMode 全绿，记录 job id、数量、耗时。
3. Luban 与 `Sync and Build All` 成功；最新 BuildLayout 无 BuildError、所有 bundle `BuildStatus=0`，新增/修改 GameData 由 `AssetBundleProvider` 物理打包。
4. Packed Play 从 Bootstrap 的真实 UGUI 完成 MainMenu→新 G7 Run→非战斗节点/Elite→Boss→三种 outcome 必要分支→结果页→MainMenu；至少覆盖一次冷启动继续和终局不可继续。
5. 产品 Console Error、InvalidKey、ConfigInitializationException 均为 0；测试框架日志与产品错误分开记录。
6. 恢复用户原 `run-save.json` 与 Addressables play mode builder；Scene 不 dirty；用户 `.gitignore`、`AGENTS.md` 及其他 WIP 不被纳入。
7. 更新 `STATUS.md`、`SESSION_LOG.md`、`CODE_DECISIONS.md`、`RUN_ROADMAP.md` 和 `06_testing/`，逐项审计 G7-A～E。
8. 只暂存经过审计的 G7 路径，绝不使用 `git add .`；提交后推送当前分支，并分别报告本地 commit 与远端 push 结果。

## 9. 明确不做

多 Act、Ascension、每日挑战、多个真实 Boss Encounter/多 Boss 战内容、通用 Boss DSL、全量内容目录、联网排行榜、多人、云/多槽/战中存档、G8 设置/教程/正式表现/统计与发布改造均不在本轮。G3 已冻结的多个 Boss identity/终点继续保留，不等同于多个真实 Boss Encounter。

## 10. 2026-08-28 执行结果与最终停止点

G7-A～E 已按本计划完成：新 profile `tinyspire.act1.g7.v1`、ActContentManifest、Elite 5101、单一真实 Boss Encounter 5201、Battle-owned Phase I→II、schema v6 类型化 `RunOutcome(Victory/Defeat/Abandoned)`、严格终局持久化与 `RunActContentBuildValidator` 均已落地。G3/G6 profile、既有 setup/result bridge 与唯一 Store/Flow 所有权保持不变，没有进入 G8 或新增第二地图、第二结果通道、通用 Boss DSL。

终审来源闭合 RED job `e81cc15a7483467291b7b9d72094fc1f` 为 505/510；保持生产门禁后的 G7 定向 GREEN `60ec69d046b5442cb593a8bef123c0f1` 为 **510/510 passed、0 failed、0 skipped，8.0937884s**。最终 Rider build session `e750f929-d9bf-4cfd-bbf6-d715c237be51` 成功且 problems 0；完整 Unity EditMode job `9758c02e718540aa97e5e26f832794e3` 为 **1410/1410 passed、0 failed、0 skipped，23.0963649s**。Luban 与 `TinySpire/Build/Sync and Build All` 成功，最新 BuildLayout 无 BuildError，七个目标由 `AssetBundleProvider` 进入 `BuildStatus=0` 的物理 bundle。

Packed Play 真实 UGUI 已分别完成完整 Victory 路线、MapReady 主动 Abandoned 与首战真实 Defeat；三条分支均形成 schema v6 terminal、展示结果页、确认返回 MainMenu 且不可 Continue，Console Error、InvalidKey 与 ConfigInitializationException 均为 0。验收后已恢复用户原存档、Addressables builder 与干净 BootstrapScene。完整证据见 [G7 验收记录](../06_testing/2026-08-28-g7-single-act-elite-boss-outcome.md)。用户已授权完成后的精确 commit/push，但 Git 交付尚不属于本页已完成事实，必须由最终交付步骤分别记录本地与远端结果。
