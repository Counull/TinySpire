---
title: G4 RunDeck、普通战斗奖励与多级升级实施计划
page_type: plan
lifecycle: archived
date: 2026-08-25
scope: G4 only
source: 用户 2026-08-25 已 Grill 并冻结的 G4 玩法合同
status_source: ../STATUS.md
implementation_status: verified
---

# G4 RunDeck、普通战斗奖励与多级升级

> **归档状态：** G4-A～D 已完成并 `verified`；本页保留为实施与审计来源。当前状态与后续授权只查 [STATUS.md](../STATUS.md)，本轮没有进入 G5。

## 1. 目标、授权与硬边界

本轮只完成以下闭环：

```text
新 Run 一次展开有序 RunDeck
→ 普通战斗只读消费实例投影
→ 胜利冻结 3 张奖励
→ 选择 1 张或跳过并原子保存
→ 回图
→ 下一战仍以稳定实例身份抽到牌
```

同时实现实例级有限/无限升级的领域模型、合法性、存档恢复、文本/费用/规则投影与 Battle 真实执行；本轮没有玩家可点击的升级入口，首个主动升级入口仍留给 G6 篝火。

不实现刷新、补偿、保底计数、金币、遗物、药水、商店、事件、宝箱、真实 Boss、Boss 阶段、RunOutcome、云/多槽/战中存档、多人、广告或商业化。不修改 Scene、Prefab、asmdef、ProjectSettings、HybridCLR 或 DI 架构；若后续事实证明必须触碰其中任一项，立即停止对应分支并报告。

## 2. Seam audit 与所有权结论

现有 G1～G3 seam 可以原位深化：

- `RunStateStore` 继续是跨场景 Run 业务事实唯一写入口；RunDeck、PendingReward、奖励结算和实例升级只能由 Store 发布。
- `RunFlowService` 只解析配置、调用 Store、提交 save document 和编排 SceneFlow；它不保存第二份 RunDeck/Pending/升级状态。
- `IRunSaveStore` 的 `Load/Commit/Delete` port 足够承载 G4；原子文件适配器只需认识新 canonical 字段和等值语义。
- `BattleResultRunBridge` 的首个稳定结果 exactly-once 转发契约保持不变。
- `RunEntryPresenter/View` 继续使用单一 projection/action seam；奖励页在现有代码动态构建的页面树中添加，不复用 battle-only `CardView`，不改 Scene/Prefab。
- 现有 `Battle.CardInstanceId` 是 battle-local 身份且与临时卡共用分配器，不能改造成 Run 身份。新增 Run-owned `RunCardInstanceId`；Battle 卡另带 nullable origin，临时卡 origin 为 null。
- G4 前的 schema v2 只有 `DeckTemplateId`。G4-A 以 schema v3 canonical 保存 ordered RunCards；G4-B/C 再以 schema v4 保存冻结 Pending，保留 v2→v3/v4 的一次性 legacy fallback 与 v3→v4 的无损迁移；schema v1 依 CD-116 继续 fail-fast。
- 当前胜利直接 `ApplyVictory → MapReady`、胜利保存失败可回滚战前档，均与冻结 Pending 合同冲突，G4-C 必须替换为 fail-closed Pending 与 prepare/commit 结算。

以上审计未发现需要扩大到高影响文件或更换 Bootstrap/child-scope 生命周期的事实。

## 3. 预先同意的 TDD seam

本轮按垂直 RED → GREEN 工作，不先批量写完测试或生产实现：

1. 纯领域：`RunDeck`、奖励池门禁/抽取、Pending/结算、升级 catalog/resolver。
2. 最小状态 owner：`RunStateStore` 的创建/恢复/Battle 投影/Pending/prepare-commit/实例升级。
3. 持久化：`RunSaveDocumentMapper + Codec/Migrator + AtomicJsonRunSaveStore`。
4. 编排：`RunFlowService + BattleResultRunBridge + BattleSetupOptions/BattleSession`。
5. 表现：`RunEntryPresenter/View` 只渲染 projection 并提交稳定 command identity。
6. Battle 真实执行：共享只读升级投影进入通用 Effect 与 MachineGunner Program 的费用、文本、预演和结算路径。

每个 RED 必须先以预期的缺失行为失败；只写使该测试通过的最小实现，再运行相邻回归。A、B、C、D 每一站都形成独立测试证据，不把失败积压到最后。

## 4. G4-A · RunCard、ordered RunDeck、迁移与 Battle 投影

1. 新增 `RunCardInstanceId`、`RunCard` 与不可变 ordered `RunDeck`。每张 RunCard 只保存实例 ID、TemplateId、UpgradeLevel；G4 只有加牌，下一实例序号由现有最大序号 + 1 派生，不保存可派生 allocator。
2. 创建新 Run 时，Flow 从初始牌组模板按配置顺序展开一次，分配 1..N；Store 接收显式 RunDeck，之后不再以 DeckTemplate 作为牌组权威。
3. 新 schema canonical 保存 ordered RunCards。v2 以原 `DeckTemplateId` 做一次性 legacy fallback；Continue 必须在发布 active Run 前先耐久改写为 canonical 新 schema，失败则不发布，避免 legacy live 与首战 reward intent 冲突；v1 继续 `UnsupportedSchema`。重复/非正实例 ID、负升级级别、缺失卡模板及顺序漂移均类型化拒绝。
4. `RunBattleInput/BattleSetupOptions` 传防御性复制的不可变 RunCard projection。Battle 继续分配 battle-local ID，同时保存 `OriginRunCardInstanceId`；洗牌、抽弃牌与卡区移动保持 origin，临时卡 origin 为空，Battle 永不回写 RunDeck 或 Run save。
5. 保留 `DeckTemplateId` 只作为既有 debug/legacy setup fallback；同一次 setup 同时提供 canonical RunDeck 与 deck template 时拒绝歧义。

停止点结果：G4-A 已通过。RunDeck 领域、Store、schema v3 round-trip/v2 migration/v1 fail-fast、legacy Deck 逐卡门禁、atomic equality、canonical 冷启动→Battle setup、Battle origin identity 与相邻 G3 回归的最终聚合 job `c210e4b045aa454780e22a38d02e9445` 为 **120/120 passed、0 failed、0 skipped**；Rider 相关文件分析与 Unity 刷新编译均为 0 errors，清空工具日志后 Console Error=0。完整 RED→GREEN 证据见 [G4 验收记录](../06_testing/2026-08-25-g4-run-deck-rewards-upgrades.md)。

## 5. G4-B · Hero 奖励池、内容门禁、独立随机域与冻结 Pending

1. 两名 Hero 各显式配置独立 reward pool 与 Common/Uncommon/Rare 权重；不实现会累积/重置的稀有保底状态。用户在 2026-08-26 授权 Agent 自行裁量阻塞后，配表冻结为 `60/37/3`；它只是一组无状态独立抽取权重，不继承旧草稿中的保底语义。
2. 构建期拒绝：少于 3 个不同模板、重复 ID、缺失卡、CatalogOnly、Basic/Ancient、非 Common/Uncommon/Rare、非当前 Hero 池或零总权重。Basic 仅由 rarity 门禁排除，不额外禁止“同时存在于初始牌组”的 Common/Uncommon/Rare 模板；Battle 动态临时卡不能进入 Run 奖励池。
3. Reward 使用 `RunRandomDomains` 的独立 seed 派生，不读取或推进 Map/Battle 随机流。每个槽先在仍有合法模板的 rarity bucket 间按配表权重抽取，再在该 bucket 内等概率抽一个模板并移除；三张必不同，跨战斗允许重复模板。
4. 普通胜利只调用一次 generator，把稳定 PendingRewardId、按顺序的三个 TemplateId 与结算生命冻结进 Store/Save 的 `RewardPending`。UI 刷新、语言切换、Scene 重建、重进、读档与冷启动只重投影，不再调用随机。

停止点门禁：两 Hero 池门禁、稀有度权重边界、三模板不同、seed determinism、随机域隔离、冻结/恢复一致与重复 BattleResult 不生成第二份 Pending 全部通过。

停止点结果：Hero 1001/1002 分别配置 12/76 张互不共享的合法候选，独立 Reward seed 与 schema v4 Pending 已落地；最终 Unity 定向 job `29bb1f63a33f432695d7ef6833a1c0f9` 为 **30/30**。

## 6. G4-C · 奖励页、选择/跳过、exactly-once 与原子提交

1. 胜利后先让 Store 进入 `RewardPending`，先耐久保存该 Pending，再加载 RunEntry 奖励页。首次保存失败保持同一 Pending 并只允许 exact retry，不能回滚到战前地图档。
2. 奖励选择 command 必须携带 `PendingRewardId + CardTemplateId`；跳过携带 `PendingRewardId`。Store 最终验证 phase、Pending identity 与候选归属，重复、过期、伪造输入均零写入。
3. Store 提供 prepare/commit seam：prepare 只冻结唯一 settlement 和 save projection，不发布新 Run 状态；Flow 原子提交成功后才让 Store 发布 MapReady/完成路径/可选新增实例。失败时仍保持原 Pending，重试复用同一 document、同一实例 ID。
4. 选择产生 `max(existing InstanceId)+1` 的独立 RunCard 并追加到 ordered RunDeck；跳过不改牌组。二者成功后才完成 committed Combat 节点并回地图。
5. `RunEntryView` 在代码内构建三个候选按钮和 Skip；重复 Render 不叠加监听。Presenter 只本地化已冻结 TemplateId；语言变化不改变 Pending identity/顺序。
6. 下一战通过 `OriginRunCardInstanceId` 证明选择所得实例真实进入牌堆；不能只断言同模板出现。

停止点门禁：Store/Flow exactly-once、Pending save/settlement failure、选择/跳过、伪造/过期/重复零写入、Presenter/View 刷新与下一战实例链全部通过。

停止点结果：选择/跳过、save-before-publish、settlement intent 恢复、失败保持 Pending、三候选/Skip 表现与双 Hero 下一战实例链均已落地；最终 Unity 定向 job `4ba5196358a94341901a228f7ad61ec2` 为 **35/35**。

## 7. G4-D · 四张生产卡的有限/无限升级

只使用审计后的现有 Implemented runtime、现有卡表/effect/program 与既有升级元数据：

| Hero | 卡 | 轨道 | 已有生产事实与 G4 配置 |
|---|---|---|---|
| Warrior 1001 | Strike 3002（Basic） | 有限 | L0 费用 1 / 伤害 6；显式唯一 L1 费用 1 / 伤害 9，来自既有 `damage +3` 升级元数据。 |
| Warrior 1001 | Bludgeon 3123（Uncommon） | 无限 | L0 费用 3 / 伤害 32；固定类型化 `DamageValue +10/level`，L1=42、L2=52……，复用既有通用 DealDamage runtime。 |
| Machine Gunner 1002 | Shoot 3201（Basic） | 无限 | L0 费用 0 / 射击伤害 6；固定类型化 `DamageValue +3/level`，L1=9、L2=12……，投影到既有 Shoot program 的本次执行值。 |
| Machine Gunner 1002 | OutputAdjust 3207（Uncommon） | 有限 | L0 费用 1；显式唯一 L1 费用 0，既有 Power 规则与 PowerPile 归宿不变。 |

实现约束：

1. 配置上严格二选一：有限卡按 `CardId + NextUpgradeLevel` 查询显式 step；无限卡只有一条固定、类型化每级增量规则。没有“有限后接无限尾巴”。
2. 新配置只表达这四张真实卡所需的明确标量与合法性，不创建通用公式 DSL、事件总线或职业框架。选择 3002/3123 可用同一通用伤害 Effect 证明有限/无限；选择 3201 可用既有 Shoot program 证明职业规则投影；选择 3207 可用既有 Power runtime 证明有限费用与归宿保持。MachineGunner 的升级值只投影到当前实例的执行参数，不修改全局 registry。
3. Store 的实例升级 command 每次只把一个合法 RunCard 升 1 级；G4 仅提供领域 API 和测试，不接玩家按钮。有限轨道升满后拒绝，其他实例零变化；无限轨道使用 checked 算术并拒绝溢出。
4. 同一个共享只读规则投影驱动卡名级别、描述参数、费用、归宿与执行；Battle 合法性/预演/支付/Queue 执行不得各读一套 base/upgraded 值。
5. Save/restore、Battle setup、奖励展示与后续战斗始终保留实例级 UpgradeLevel；同模板副本可处于不同等级。

停止点门禁：四张生产卡配置门禁、有限升满/无限 L2+、仅改指定实例、保存恢复、文本/费用/规则投影、通用 Effect 与 MachineGunner 真实 Queue 执行全部通过。

停止点结果：有限轨道 3002/3207、无限轨道 3123/3201 的配置、构建门禁、共享投影及通用 Effect/MachineGunner runtime 接线已落地；最终 Unity 定向 job `d9d5a6efa72348df8cfb1a52d5bea13a` 为 **258/258**。

## 8. 完整验证门

以下验证门已全部在本轮通过，G4 已标记为 `verified`：

1. G4-A～D 的纯领域 RED→GREEN 与相称定向回归。
2. Rider/CLI 生产与 Editor 静态编译均 0 errors。
3. 唯一已连接 Unity Editor 内完整 EditMode：0 failed / 0 skipped，并记录 job ID、数量、耗时。
4. 修改 DataTables/i18n 后运行 Luban，再执行 `TinySpire/Build/Sync and Build All`；核对生成 JSON、Addressables Local Content 与 BuildLayout。
5. 使用 Local Addressables 的 Packed Play / Use Existing Build 或风险相称 Player 链，分别对 Hero 1001/1002 实走：`新 Run → 普通战斗胜利 → 3 张冻结奖励 → 选择或跳过 → 回图 → 下一战抽到正确实例`。
6. 实测 UI 刷新、语言切换、Scene 重建、读档和进程级冷启动均不刷新 Pending；重复/过期/伪造输入零写入；同模板实例身份独立。
7. 有限与无限轨道分别证明：只改指定实例、保存恢复不丢、文本/费用/规则实际一致，且四张生产卡真实执行。
8. 所有最终产品检查点 Console Error=0；不把工具自身错误或旧日志冒充产品证据。

最终证据：生产双 Hero 自动化 1/1、完整 Unity EditMode job `7cad4b02d38248f298227ea06804c949` **1093/1093**、Rider build 0 errors、`Sync and Build All`、Local Addressables/BuildLayout、Packed Play 双 Hero 选择/跳过与冷启动链、最终 Console Error=0。完整原始记录见 [G4 验收记录](../06_testing/2026-08-25-g4-run-deck-rewards-upgrades.md)。

## 9. 影响路径、回滚与工作区保护

- 产品修改限定在既有 Run state/flow/persistence、Battle setup/card projection、RunEntry presentation/view、Editor 构建校验、对应 tests、必要 Luban 表与生成文件、i18n 和本轮 Daedalus 文档。
- 保留用户已有 `TinySpire/ProjectSettings/ProjectSettings.asset` 修改与 `TinySpire/.codex/` 未跟踪内容；不执行 `git add/commit/push/reset/clean`。
- 回滚按 A（RunDeck/迁移）、B（reward config/generator/Pending）、C（settlement/UI）、D（upgrade config/projection/execution）窄路径分别撤销，不用破坏性 Git 命令。
- 若任一串行停止点暴露新的玩法选择、无法从现有事实唯一推出的规则或必须扩大到高影响文件，停在最近一个已测试停止点并报告，不自行推断。
