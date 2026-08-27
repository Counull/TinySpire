---
title: G5/G6 Run 持有物与非战斗节点连续交付窄计划
page_type: plan
lifecycle: archived
date: 2026-08-26
updated: 2026-08-27
scope: G5 and G6 only
source: 用户 2026-08-26 连续交付授权；用户随后明确取消逐片 Grill 并授权 Agent 直接冻结最小默认语义
status_source: ../STATUS.md
implementation_status: verified
---

# G5/G6 Run 持有物与非战斗节点连续交付

> **归档状态：** S0、G5-B～D 与 G6-A～E 已完成并 `verified`；本页保留为实施与审计来源。当前状态与后续授权只查 [STATUS.md](../STATUS.md)，本轮严格停在 G6。

> **授权说明：** 用户最初要求每个有独立玩法选择或失败语义的切片先 Grill，随后于 2026-08-26 明确改为“直接执行，不用 Grill”。因此本页把原本待 Grill 的最小玩法与失败语义一次冻结为本轮合同，并授权按本页的串行停止点实施、验证和沉淀。若实施中出现本页没有覆盖的新玩法选择、高影响文件或架构改造需求，仍必须停在最近一个已验证停止点并报告，不能借“免 Grill”扩大范围。

当前状态、授权与阻塞的唯一可变来源仍是 [STATUS.md](../STATUS.md)；阶段候选和共用门禁见 [RUN_ROADMAP.md](../RUN_ROADMAP.md)。实现必须遵守 [ARCHITECTURE_CONVENTIONS.md](../ARCHITECTURE_CONVENTIONS.md)，尤其是 AC-001、AC-007、AC-008 与 AC-009。

## 1. 目标、顺序与完成定义

本轮只完成以下连续闭环：

```text
共同 S0：RunHoldings + canonical save + NodeVisit hard-commit
→ G5-B：首个 BattleStart 遗物
→ G5-C：首个无目标治疗药水
→ G5-D：首次普通战斗奖励附加真实 loot + 持有物只读表现
→ G6-A：确定性混合地图与非战斗访问接线
→ G6-B：休息点
→ G6-C：宝箱
→ G6-D：商店
→ G6-E：事件
→ G5/G6 聚合产品链
```

G5 完成必须证明一个遗物和一个药水都能真实获得、保存、恢复并进入 Battle：遗物在每场 BattleStart 稳定触发一次，药水由权威命令接受后只消费一次。G6 完成必须证明同一新 Run 可按确定性混合路线完成休息点、宝箱、商店和事件；每个节点都能跨 Scene 重建和进程冷启动恢复，且任何奖励、扣款、治疗、升级、购买与路径完成都不会重复。

执行严格串行。每片只有在本片 RED→GREEN、相邻回归、持久化失败窗和相称 Unity 产品证据通过后，才进入下一片；不得为了“先把全链跑起来”越过失败的停止点。

## 2. 本轮冻结的共同合同

### 2.1 RunHoldings 是唯一持有物事实

`RunStateStore` 继续是跨场景 Run 业务事实唯一写入口。新增不可变 `RunHoldings`，随每份 `RunState` 原子发布；Flow 只编排，Presenter/View 只读 projection 并提交稳定身份，Battle 只读消费 setup projection，均不得保存第二份可写库存。

`RunHoldings` 冻结以下事实：

- `Gold` 属于 Run；新 Run 固定为 **100**。Gold 必须非负，增加与扣减使用 checked 算术；余额不足或溢出时整条命令零写入。
- 遗物按获得顺序保存稳定有序实例；同一 Relic Template 在一个 Run 内唯一，重复获得拒绝且零写入。遗物顺序同时是同一 Battle 时机的稳定执行顺序，UI 排序不能反向改变规则顺序。
- 药水是最多 **3 瓶的有序药水带**。每瓶保存稳定 Potion InstanceId 与 TemplateId；允许同一模板的多个实例。移除后保留其余实例的相对顺序，新药水追加到末尾；已有 3 瓶时获得请求拒绝且零写入。
- 新 Potion InstanceId 由当前药水带中的最大实例 ID checked `+1` 派生，空列表从 1 开始；schema 不另存实例分配序号。遗物的稳定实例顺序与药水带相对顺序都不得在恢复、迁移或 UI 排序时重排。
- 所有 public 快照都防御性复制；空键、非正模板/实例 ID、重复实例、药水超过 3 瓶、重复遗物模板和实例 ID 派生溢出均在领域或恢复边界类型化拒绝。

本轮不实现删遗物、换遗物、药水主动重排、扩容、堆叠数量、持久充能或装备栏。

### 2.2 一次 canonical schema v5 覆盖 G5/G6 共同边界

在共同 S0 把当前 schema v4 升为本轮唯一的 canonical **schema v5**；G5/G6 后续切片只填充已定义的类型化事实，不为每种节点再叠加一次临时迁移。文档新增：

- 完整 `RunHoldings`：Gold、有序遗物与最多三瓶的有序药水数组；Potion InstanceId 的下一值只由当前数组最大值派生；
- 可空 `PendingNodeVisit`：稳定 NodeVisitId、NodeId、节点种类、内容身份，以及严格按 kind 区分的 Rest/Chest/Shop/Event payload；
- G5-D 所需的冻结 card-reward attached loot；
- 既有 ordered RunDeck、PendingCardReward、地图 recipe/fingerprint、实际路径、终局事实全部保留。

迁移合同：

- schema v4 → schema v5：Gold 固定 **100**，遗物为空，药水数组为空，不存在 PendingNodeVisit；不得从 UI、当前时间或随机流猜持有物。
- v4 `RewardPending` 若代表本 Run 首次未完成的普通战斗奖励，则迁移时按 G5-D 合同冻结相同样本 attached loot；是否首次只由已完成 Combat path 与当前 committed node 推导。其他 legacy Pending 不追发 loot。
- v3/v2 继续经过既有显式链进入新 canonical；v1 依 CD-116 继续 fail-fast，不补地图或持有物事实。
- 首次 Continue 必须在发布 active Run 前先耐久改写 canonical；改写失败不得发布内存 Run，也不得覆盖最后一份已提交事实。
- 读档必须验证所有 Relic/Potion/Card/Node content 引用、map profile/version/fingerprint、NodeVisit phase/payload 对应关系和 durable equality；未知 kind、缺字段、多余互斥 payload、坏引用或不一致路径全部 fail-closed。
- Atomic adapter 的等值比较必须逐项覆盖 Gold、遗物顺序、药水数组顺序/实例、attached loot、NodeVisit identity/payload/库存售罄状态，不能只比较顶层 phase。

### 2.3 NodeVisit 使用 hard commit 与 save-before-publish

非战斗节点不另建第二套节点状态机。它复用既有 Map → `RunStateStore` → `RunFlowService` → `IRunSaveStore` → RunEntry presentation seam，并在 `RunProgressPhase` 中增加一个互斥、可恢复的 NodeVisit pending 阶段。

稳定 `NodeVisitId` 由本 Run 与冻结 NodeId 唯一决定；一个地图节点最多产生一个 visit。所有节点遵守：

1. **进入 preview：** Store 在 `MapReady` 下验证可达性、NodeId/kind/content，并一次冻结该节点全部选择、价格和候选；不发布状态。
2. **进入 hard commit：** Flow 先提交含 `PendingNodeVisit` 的 canonical document；成功后 Store 才发布 pending 并显示对应页面。提交失败时仍停在原 `MapReady`，不导航、不推进路径、不消耗随机。
3. **pending 不可软退出：** Back、Scene 重建、应用重启或重复 Enter 只能恢复同一 Pending；没有“离开后刷新”。商店只有显式 Leave，宝箱只有 Claim/Skip，其他节点只有合法选择，才可结束访问。
4. **动作 preview：** Store 最终校验 phase、NodeVisitId、choice/stock/instance identity 与当前业务条件，形成唯一后继；重复、过期、伪造或已失效输入零写入。
5. **动作 hard commit：** Flow 先写后继 document，成功后才发布 holdings/HP/deck/库存或 MapReady，并在节点完成动作中精确追加一次 PathNodeId。失败保持旧 Pending 和旧可见事实，exact retry 复用相同冻结输入。
6. **商店中间购买：** 每次购买都是同样的 save-before-publish；成功后仍保持同一 NodeVisit，只把对应 stock 标为已购买并原子更新 Gold/持有物/牌组。显式 Leave 才完成节点。

这里的“统一 Pending”只是一套生命周期 envelope；每个 kind 使用明确的类型化 payload 和领域命令，不创建任意字段字典、通用 settlement DSL、反射执行器或事件脚本语言。

### 2.4 G5 生产样本

- 首个遗物：每场 **BattleStart 为玩家增加 1 Strength**。它在玩家可提交普通行动前，经既有权威 Battle command/settlement 顺序执行；同一 Battle 只触发一次，多遗物按 Run 中稳定获得顺序执行，Scene Scope 卸载后无遗留订阅。
- 首个药水：Battle 内无目标使用，合法接受时为玩家恢复 **10 HP**，不超过 MaxHP；满血、死亡、非法阶段、未知/已消费实例均拒绝且零写入。
- Battle 只记录本场已接受消费的 Potion InstanceId，并经唯一稳定 `BattleResult` bridge 回写。胜利或失败的稳定结果在同一 Run 后继中 exactly-once 移除这些实例；结果提交失败可重试但不可二次消费。
- 若进程在稳定 BattleResult 前退出，按现有“无战中存档”边界恢复战前检查点，药水仍在；不得把 Battle transient consumption 另写一份旁路存档。Defeat 一旦稳定提交则仍进入既有 `Terminal(Defeat)`，不恢复普通战斗重试。
- G5-D 的真实来源：本 Run **第一次成功结算普通战斗卡牌奖励**时，在同一原子后继中附加样本遗物（尚未持有时）与样本药水（当前少于 3 瓶时）。是否附加及模板身份在 `PendingCardReward` 生成时一次冻结；选择卡或 Skip 都应用同一 attached loot。保存失败保留原 Pending，不重算、不重复发放。

### 2.5 G6 生产样本

- **Rest：** 二选一。Heal 恢复 `ceil(MaxHealth × 30%)`，不超过 MaxHealth；满血时禁用且 Store 拒绝。Upgrade 让玩家从进入时冻结的合法 RunCard 实例列表中选择一张升 1 级，复用 G4 升级能力；没有合法实例时禁用。任一成功选择都原子完成节点。
- **Chest：** 进入时冻结一个样本药水。Claim 把新实例追加到药水带末尾；已有 3 瓶时 Claim 禁用并由 Store 拒绝。Skip 始终允许；Claim 或 Skip 都 exactly-once 完成节点，不补偿、不刷新。
- **Shop：** 冻结三个 stable stock entry：样本遗物 **75 Gold**、样本药水 **25 Gold**、按 Hero 奖励池冻结的一张合法卡牌 **50 Gold**。每项最多购买一次，可连续购买多个项目，最后显式 Leave。每笔 purchase 与 stock 已购状态逐次 save-before-publish；遗物已持有、药水已有三瓶、余额不足或 stock 已购时该项禁用且 Store 拒绝。卡牌购买追加独立 RunCard 实例，候选使用独立 Shop random domain，不推进 Map/Battle/Reward/Event 随机流。
- **Event：** 两个冻结选项只能选择一次。A：获得 **50 Gold**。B：支付 **25 Gold** 并恢复 **15 HP**（不超过 MaxHealth）；余额不足或满血时 B 禁用且 Store 拒绝。成功选择在一个后继内完成加/扣 Gold、治疗与节点完成。

## 3. Seam audit 与影响路径基线

本轮在现有深模块内原位扩展，不替换 Bootstrap、Scene Scope、DI、Battle queue 或原子文件适配器：

- Run 领域与所有权：`TinySpire/Assets/Scripts/Run/RunState.cs`、`RunStateStore.cs`，以及新建窄领域文件 `TinySpire/Assets/Scripts/Run/RunHoldings.cs`、`TinySpire/Assets/Scripts/Run/RunNodeVisit.cs`。
- Flow/结果边界：`TinySpire/Assets/Scripts/Run/RunFlowService.cs`、`BattleResultRunBridge.cs`、`RunCardReward.cs`。
- 持久化：`TinySpire/Assets/Scripts/Run/Persistence/RunSaveDocument.cs`、`RunSaveDocumentCodec.cs`、`RunPersistenceState.cs`、`IRunSaveStore.cs` 与 `TinySpire/Assets/Scripts/Infrastructure/Persistence/AtomicJsonRunSaveStore.cs`。
- 地图：`TinySpire/Assets/Scripts/Run/Map/ActMapProfile.cs`、`ActMapGenerator.cs`、`ActMapValidator.cs`、`MapDefinition.cs`、`MapReachability.cs`。
- Battle 输入/结果/命令：`TinySpire/Assets/Scripts/Battle/BattleSession.cs`、既有 `Commands/`、combatant effect/settlement 与 HUD seam；新增 relic start 与 potion command 只接入既有权威顺序。
- 表现：`TinySpire/Assets/Scripts/UI/Run/RunEntryPresentation.cs`、`RunEntryView.cs` 与现有 Battle HUD 代码构建入口；不改 Scene/Prefab。
- 配置与生成：`DataTables/Datas/__beans__.xlsx`、`__enums__.xlsx`、`__tables__.xlsx`、必要的 `DataTables/Datas/run.*.xlsx`，以及 Luban 生成的 `TinySpire/Assets/Scripts/Core/Generated/Config/` 和 `TinySpire/Assets/GameData/` 对应 JSON。所有素材引用若出现，继续使用短键契约；本轮优先用 TMP/i18n 和程序化最小表现，不新建素材域。
- 测试：`TinySpire/Assets/Editor/Tests/` 下新增 G5/G6 窄测试，并扩展 `RunStateStoreTests.cs`、`RunSaveDocumentTests.cs`、`AtomicJsonRunSaveStoreTests.cs`、`RunFlowServiceTests.cs`、`BattleResultRunBridgeTests.cs`、`RunEntryPresenterTests.cs`、`RunEntryViewTests.cs`、`BattleSessionTests.cs` 及地图/配置构建门禁测试。
- 文档闭环：本计划之外，主实施任务完成每个停止点后按需更新 `../STATUS.md`、`../CODE_DECISIONS.md`、`../SESSION_LOG.md`、`../RUN_ROADMAP.md` 与 `../06_testing/`；本页不替代当前状态或原始测试证据。

所有新增或修改函数至少保留一条中文注释说明职责。任何路径若在实施审计中证明确实不需要，不为了匹配清单制造空改动；任何新增影响路径若超出上述 seam，必须先判断是否触碰硬排除项。

## 4. 固定 RED→GREEN 方法

每片采用同一垂直顺序，不先批量写完整功能：

1. **纯领域 RED：** 值对象、不可变快照、合法性、stable identity、checked 算术、随机域和 exactly-once 后继。
2. **Store RED：** 唯一写入口、preview/commit、重复/过期/伪造输入零写入、失败后状态不变。
3. **Persistence RED：** canonical round-trip、迁移、坏数据 fail-closed、Atomic durable equality、commit 各失败窗。
4. **Flow/Bridge RED：** save-before-publish、结果 exactly-once、导航只发生在 durable commit 之后。
5. **Presenter/View 或 Battle RED：** 只读投影、重复 Render 不叠监听、按钮 disabled 只是表现且 Store 始终终审；Battle 命令进入既有 Queue。
6. 写通过该 RED 的最小 GREEN，立即运行相邻 G1～G4 回归；本片通过后记录真实 job/数量/耗时，不沿用旧 1093/1093 作为本轮证据。

## 5. S0 · RunHoldings、存档与 NodeVisit 共同基础

### 影响路径

第 3 节列出的 Run domain/store/persistence/flow/map 基线；S0 不接 Battle HUD 或四类完整页面，只允许最小 projection 证明恢复路由。

### RED→GREEN seam

1. 先以纯测试证明 Gold、遗物唯一有序、药水带最多三瓶且保持相对顺序、同模板重复、实例 ID 从当前最大值 +1 派生、满容量拒绝和输入防御性复制。
2. 让 `RunState`/`RunStateStore` 原子持有 `RunHoldings`；创建、恢复、clear、任何既有 G4 reward/upgrade 后继都必须原样保留 holdings。
3. 建立唯一 schema v5、v4/v3/v2 迁移、严格 codec/mapper/catalog 校验和 Atomic 深等值；先覆盖损坏/截断/互斥 payload/配置漂移，再接 production adapter。
4. 建立 `NodeVisitId`、typed pending envelope 与 `MapReady ↔ NodeVisitPending` 合法阶段；用最小 fake kind 证明进入与结算两次 save-before-publish、hard commit、冷启动恢复和 exact retry。
5. Flow 不保存 Pending 镜像；刷新 save availability、Continue、RetryPendingCommit 和 Scene navigation 必须识别新的稳定 phase。

### 验收与停止点

- 新 Run 与 v4 migration 都精确得到 Gold=100、空遗物、空药水数组；canonical round-trip 每一项等值。
- 非法获得/移除/扣款、重复遗物、满药水、过期实例、坏存档全部零写入或 fail-closed。
- NodeVisit 进入保存失败不发布；进入成功后冷启动恢复同一 identity/payload；结算保存失败不发布任何收益或路径；重试只提交同一后继。
- 既有 G4 `RewardPending`、MapReady、BossGateReached 与 Terminal(Defeat) 存档仍可恢复；schema v1 仍拒绝。
- S0 完成只证明共同边界，不把 G5/G6 标记完成，也不提前接入真实非战斗页面。

### 回滚

S0 的 domain/store/schema/mapper/atomic tests 必须作为一个窄组回滚，不能只删 DTO 字段而留下新状态。新 schema 一旦写入非测试 live save，不做静默降级；后续失败优先前向修复。实施验证使用受控测试档，不删除用户真实存档。

## 6. G5-B · 一个无选择遗物

### 影响路径

Run relic projection、`BattleSession.cs`/setup options、既有 Battle command/settlement 与 combatant attribute seam、相关 Battle/Run tests；必要 Relic 配置及 i18n。不得新增全局事件总线或新 Scope。

### RED→GREEN seam

1. RED：setup 防御性复制有序 relic projection；未知配置、重复模板或顺序漂移拒绝。
2. RED：玩家建立后、首个普通行动前，样本遗物经系统命令使 Strength 精确 `+1`；重复 bootstrap/start callback 不再触发，同一时机按 holdings 顺序稳定。
3. GREEN：只增加最小 relic resolver/start executor，把效果翻译到现有 command/settlement；不直接从 UI 或旁路 service 修改 combatant。
4. 覆盖 Scene Scope dispose 后无残留订阅，下一场 Battle 会重新按 Run projection 触发一次。

### 验收与停止点

- 无遗物 Battle 完全保持基线；有样本遗物 BattleStart 后 Strength=base+1，并影响既有伤害计算。
- 同一 Battle 精确一次、两场各一次；setup/reload/重复回调和多来源排序都有定向测试。
- 本停止点不发放遗物、不实现药水或非战斗节点；测试可用显式 holdings fixture。

### 回滚

可独立撤销 relic config/resolver/setup projection/start command 与对应 tests；不回滚 S0 schema/holdings，也不改 Battle Queue 既有协议。

## 7. G5-C · 一个无目标治疗药水

### 影响路径

Run potion setup projection、Battle command/result/bridge、既有 HUD 代码构建路径、Store battle-result settlement、相关 tests 与 Potion 配置/i18n。

### RED→GREEN seam

1. RED：Battle setup 按药水带稳定顺序投影 potion instances；同模板实例保持独立 identity，Run 快照不被 Battle 修改。
2. RED：`UsePotion` 只携带 Potion InstanceId；合法阶段且玩家非满血时进入既有 Queue，恢复 min(10, missing HP) 并记录一次 accepted consumption。
3. RED：双击、旧/伪造 ID、满血、死亡、非法阶段和已经消费实例均拒绝且没有 heal/consume settlement。
4. RED：稳定 BattleResult 带去重的 consumed instance IDs；Store 在 victory reward successor 或 defeat terminal successor 内一次移除，commit 失败不发布，重复结果不二次删除。
5. GREEN：HUD 只渲染 Battle-owned 可用 projection 并提交命令；结果前退出按战前 checkpoint 恢复药水，不新增战中 save。

### 验收与停止点

- 10 HP 治疗、上限 clamp、零收益拒绝、同模板双实例逐个消费和最多三瓶的有序 identity 均通过。
- victory/defeat、结果重复、结果保存失败重试和进程在结果前退出的语义均有直接证据。
- 本停止点仍不提供真实获得来源；只证明显式 fixture 的跨边界行为。

### 回滚

独立撤销 potion command/HUD/result 字段与 bridge/store 接线；保留 S0 potion inventory。不得以清空 save 或恢复 Battle restart 旧语义作为回滚手段。

## 8. G5-D · 首次奖励附加 loot 与持有物只读表现

### 影响路径

`RunCardReward.cs`、Store/Flow/reward save DTO、RunEntry presentation/view、Relic/Potion 配置与 i18n、Luban 生成物和 G5 产品验收 tests。

### RED→GREEN seam

1. RED：第一次普通战斗胜利冻结 card reward 时，同时冻结样本 relic/potion attached loot；Scene 重建、语言切换、读档或 retry 不重算。
2. RED：选择任一卡或 Skip 的唯一后继同时处理卡牌结算、可用 attached loot、Combat path 完成和 MapReady；任一步保存失败全部不发布。
3. RED：已有遗物时不附加重复；药水已有三瓶时不附加；这些决定在 Pending 创建时冻结，不在点击时漂移。
4. GREEN：Run 页面显示 Gold、有序遗物和最多三瓶的有序药水带；重复 Render 不叠 listener，View 不自行加减数量。

### 验收与停止点

- `新 Run → 首战胜利 → Pending 冷启动 → 选择/Skip → 得到 relic+potion → 再冷启动` 全部等值。
- 第二次普通战斗奖励不再附加首领 loot；重复/过期 reward command 不重复发放。
- 下一场 Battle 真实接收遗物与药水，遗物触发和药水消费均由 G5-B/C 已验证路径完成。
- DataTables/i18n 修改后执行 Luban 与 `TinySpire/Build/Sync and Build All`；G5 到此才可标记 completed/verified。

### 回滚

撤销 attached-loot generation/settlement 与持有物 UI，不撤销 S0 holdings 或 G5-B/C 能力；已有 canonical save 中已获得的合法持有物继续可读，不做向下迁移或删除。

## 9. G6-A · 确定性混合地图与非战斗访问接线

### 影响路径

`Run/Map/` profile/generator/validator/definition/reachability、Store/Flow 节点路由、RunEntry map identity/presentation、Node content 配置与地图 tests。

### RED→GREEN seam

1. 新增独立稳定 profile（建议 ID `tinyspire.act1.g6.v1`）与新 generator version；旧 `tinyspire.act1.g3.v1` 继续由原 version 生成，原 seed 的 node/edge/fingerprint 必须逐字节不漂移。
2. 新 profile 保证一条可验收路线按层经过：`Combat → Rest → Chest → Shop → Event → Combat → BossGate`。非战斗 Node kind 与 ContentId 属于冻结 MapDefinition/fingerprint；Boss 仍只到既有 BossGate，不进入真实战斗。
3. Map/Reward/Shop/Event/Battle 随机域显式分开。节点 kind/内容、Shop card 候选与任何 Event 选项不能因其他域新增随机调用而漂移。
4. 点击非战斗节点只调用 S0 NodeVisit enter preview/commit；成功后仍使用 RunEntry 代码构建页面，不新建 Scene/Prefab。

### 验收与停止点

- 同 profile/version/seed 的 mixed map、NodeVisit identities 和指纹一致；坏 kind/content/profile/version 被 validator/restore 拒绝。
- G3 v1 旧档与历史 fingerprint 继续恢复；新 profile 不改写旧档 profile ID。
- 进入任一 fake non-combat 节点的保存失败、冷启动和重复 Enter 符合 S0；本停止点不实现四类具体结算。

### 回滚

按新 profile/version、non-combat enum/validator 与 routing 一组撤销；保留 S0 NodeVisit envelope。禁止通过修改旧 v1 fingerprint、丢弃旧档或把旧 profile 指向新 generator 来回滚。

## 10. G6-B · 休息点

### 影响路径

Rest typed payload/commands、Store/Flow、G4 card-upgrade catalog、RunEntry page/projection、Rest config/i18n 与 tests。

### RED→GREEN seam

1. 进入时冻结 heal amount 与 ordered legal upgrade instance IDs；满血 Heal disabled，无合法卡 Upgrade disabled。
2. Heal command 携带 NodeVisitId；Store 重验并形成 clamp 后 HP + 节点完成的唯一后继。
3. Upgrade command 携带 NodeVisitId + RunCardInstanceId；复用 G4 每次升一级能力，与节点完成同一后继。
4. 两条命令都先保存后发布；重复、过期、伪造或 disabled choice 零写入。

### 验收与停止点

- `ceil(MaxHP×30%)` 的边界、满血、接近满血 clamp、有限卡升满、无限卡和无合法目标均通过。
- 选择后只改 HP 或指定卡之一，Path 精确追加一次并回 MapReady；commit failure 保持同一 Rest Pending。

### 回滚

只撤销 Rest payload/commands/page/config 与 tests；NodeVisit 公共生命周期和 G4 upgrade 能力保留。

## 11. G6-C · 宝箱

### 影响路径

Chest typed payload/Claim/Skip、Store/Flow、RunEntry page、Potion catalog/i18n 与 tests。

### RED→GREEN seam

1. 进入时一次冻结样本 Potion TemplateId；重进/读档不再抽取。
2. Claim 终审当前数量，并以 `max(current instance id)+1`（空列表为 1）创建新 instance、追加到药水带末尾；已有三瓶时拒绝。Skip 不改 holdings。
3. Claim/Skip 都与节点完成同一 save-before-publish 后继，重复 identity 零写入。

### 验收与停止点

- 首次候选固定；未满容量 Claim、三瓶时禁用/拒绝、Skip、commit failure retry、冷启动与重复按钮全部通过。
- Claim 后药水数组顺序和派生 instance ID canonical 等值，重进已完成节点不能再领。

### 回滚

只撤销 Chest payload/page/config；不得重排既有药水数组或引入额外 instance allocator。

## 12. G6-D · 商店

### 影响路径

Shop typed payload/stock/purchase/leave、独立 Shop random domain、Store/Flow、RunEntry page、Relic/Potion/Card catalogs、i18n 与 tests。

### RED→GREEN seam

1. 进入时冻结三个 stable stock entries：Relic/75、Potion/25、Hero Card/50；卡牌候选合法且由 Shop seed 决定，库存和价格完整入 save。
2. Purchase command 携带 NodeVisitId + StockEntryId。Store 终审余额、stock、遗物唯一、药水容量和卡牌模板，并 preview `扣 Gold + 入库/加卡 + 标记已购` 的一个后继。
3. 每笔购买先保存后发布，成功后继续显示同一 Shop Pending；失败时余额、持有物和 stock 全不变。
4. Leave 先保存节点完成后继再回图；未购买也可 Leave。没有 Back 软退出、刷新、出售、回购或库存重掷。

### 验收与停止点

- 三类购买分别证明正确价格、独立实例、售罄、余额不足、已持有遗物、满药水、重复/伪造命令与 checked Gold。
- 连买两项、每次购买后的进程冷启动、购买 commit failure exact retry、最后 Leave 和已完成节点重进均通过。
- 新增 Shop random 调用不改变 Map/Battle/Reward/Event 的固定结果。

### 回滚

只撤销 Shop payload/random domain/page/config；已 canonical 保存的合法 Gold/holdings/deck 不做向下删除。若 stock schema 已产生 live save，使用前向修复而非降级。

## 13. G6-E · 事件

### 影响路径

Event typed payload/choice、Store/Flow、RunEntry page、Event config/i18n 与 tests。

### RED→GREEN seam

1. 进入时冻结 A/B identity 与数值；不创建表达任意效果的 DSL。
2. A preview：checked `Gold + 50`；B preview：终审 `Gold ≥ 25` 且 HP 未满，再原子 `Gold - 25` 与 `Health + min(15, missing)`。
3. 任一选择与节点完成同一 save-before-publish 后继；选择后 Pending 消失，重复/过期/伪造 choice 零写入。

### 验收与停止点

- A 的 checked overflow、B 的 24/25 Gold 边界、满血、少于 15 missing HP、commit failure、冷启动与 exactly-once 全部通过。
- UI disabled 与 Store 终审一致；Event 只调用明确 Run 命令，没有字符串脚本或通用事件执行器。

### 回滚

只撤销 Event payload/page/config 与 tests；不撤销共同 NodeVisit/Gold 事实。

## 14. G5/G6 聚合验收门

所有切片定向门通过后，仍必须完成以下本轮证据才能关闭 G5/G6：

1. 生产与 Editor 静态编译 0 errors；按当前可用工具记录 Rider 检查/构建与 Unity 编译证据，不能用旧记录代替。
2. 唯一 Unity Editor 中运行完整 EditMode，记录本轮 job ID、通过/失败/跳过数量和耗时；Console 产品错误为 0。
3. 修改 DataTables/i18n 后运行 Luban，再执行 `TinySpire/Build/Sync and Build All`；核对生成 C#/JSON、Local Addressables 与最新 BuildLayout。若没有新增素材域，不为验收虚构素材；若实际新增素材，则按根规则补专用 Group、短键校验与 Packed/Player bundle 证明。
4. 进程级验收采用分层证据：Packed Play（Use Existing Build）或风险相称 Player 必须至少从 Bootstrap 真实加载到 RunEntry、创建 schema v5 新 Run 并展示 mixed route，证明新增 GameData 通过物理 bundle 初始化；完整 Unity acceptance 自动化必须覆盖以下玩法链：

   ```text
   新 Run(Gold=100)
   → 首次 Combat 胜利并冻结奖励/attached loot
   → 冷启动恢复 Pending
   → 选择或 Skip，原子获得遗物与药水
   → Rest 治疗或升级
   → Chest Claim/Skip
   → Shop 多次购买并 Leave
   → Event A 或 B
   → 后续 Combat 的 BattleStart +1 Strength 与药水 Heal 10/consume
   → 回图或既有 Terminal(Defeat)
   ```

5. 四类 NodeVisit 必须在 Unity EditMode acceptance 中分别覆盖进入保存失败、动作保存失败、exact retry、Scene 重建、进程冷启动、重复/过期/伪造输入；候选、库存、价格和选项不得刷新。不得把 Packed 启动烟测误写成所有互斥分支的手工产品验收。
6. 旧 schema v4 的 MapReady、RewardPending、BossGateReached、Terminal(Defeat) 各至少一份 fixture 完成迁移；旧 `tinyspire.act1.g3.v1` recipe/fingerprint 保持，v1 继续拒绝。
7. 运行相邻 G1～G4 定向回归和完整 EditMode；验证新增随机域不漂移既有 map/reward/battle fixtures。
8. `git diff --check` 通过，并枚举所有表格、生成 C#、JSON、i18n、Addressables settings/group manifest、package manifest/lock 与 Unity `.meta` 变化；只把本轮证据写入 `../06_testing/`。

## 15. 工作区保护、总回滚与硬排除

- 保留用户已有 `TinySpire/ProjectSettings/ProjectSettings.asset` 修改与 `TinySpire/.codex/` 未跟踪内容；不覆盖、不清理、不纳入本轮交付。
- 不执行 `git add .`、`git reset --hard`、`git clean` 或其他广泛清理。Git 暂存、commit 与 push 始终以执行当时的用户授权为准，且必须分别报告本地修改、本地 commit 与远端 push，不得混称已交付。
- 不修改 Scene、Prefab、asmdef、ProjectSettings、HybridCLR、Bootstrap/DI 架构或启动流程。RunEntry 与 Battle HUD 继续沿用现有代码构建 seam；若功能确实被高影响文件阻塞，停在最近停止点并报告影响文件、风险与可回滚路径。
- 不新增独立 RelicStore/PotionStore/GoldStore、第二 Node 状态机、通用事件总线、通用 settlement DSL、事件脚本语言、公式框架或库存框架；持有物和 NodeVisit 都深化既有 Run aggregate。
- 不实现 G7、真实 Boss/Boss 阶段、精英、RunOutcome、云存档、多槽、战中存档、多人、联网、出售/回购/刷新、动态经济、目标型药水、大型遗物/药水/事件池、正式动画或架构重构。
- 新 mixed profile 可以继续以现有 `BossGateReached` 作为不可进入的路线终点，但本轮不得从 BossGate 创建 Battle、奖励或终局。
- 回滚按 S0、G5-B、G5-C、G5-D、G6-A、G6-B、G6-C、G6-D、G6-E 的窄路径分别进行；任何回滚都不能删除真实用户存档、重写旧 profile fingerprint 或清理无关 WIP。schema 已被 live save 使用后只允许前向修复，不做无证据的向下迁移。

## 16. 2026-08-27 执行结果与最终停止点

S0、G5-B～D 与 G6-A～E 已按本计划的依赖顺序完成实现：schema v5、唯一 `RunHoldings`、类型化 `PendingNodeVisit`、遗物/药水 Battle seam、首次普通奖励附加 loot、混合地图，以及 Rest、Chest、Shop、Event 的冻结输入与 save-before-publish 结算均已落地。没有进入 G7，也没有新增第二份 Run 状态机、通用事件 DSL、Scene、Prefab、asmdef、ProjectSettings、HybridCLR 或 DI 改造。

第 14 节聚合验收门已经按上述分层证据关闭。先前两次批处理因 Unity license/entitlement code 198 失败的历史阻塞已解除；Luban 与 `TinySpire/Build/Sync and Build All` 成功，Rider MCP 增量 build 成功且 project problems 为 0。完整 Unity EditMode 于 2026-08-27 21:43:42 得到 **1348/1348 passed、0 failed、0 skipped**，耗时 74.8235407s；首次全量运行暴露 G4 production acceptance 对 G6 非战斗路径的旧假设，补齐按必经 NodeVisit 结算进入下一战的验收接线后重跑全绿。

最新 BuildLayout `BuildError` 为空、12/12 bundles `BuildStatus=0`，Relic/Potion JSON 位于 `AssetBundleProvider` 物理 bundle。UnityMCP Packed Play active builder index 1 从 Bootstrap→RunEntry 创建 Hero 1001 schema v5 Run，实际 profile/路线为 `tinyspire.act1.g6.v1` / `Combat→Rest→Chest→Shop→Event→Combat→BossGate`，Console Error、InvalidKey 与 ConfigInitializationException 均为 0；这条烟测证明真实 bundle 启动与 MapReady，不宣称手工重走全部节点分支，分支与失败语义由 1348 项 Unity acceptance 覆盖。验收后已恢复 index 0，用户原 `run-save.json` 也按既有 SHA-256 精确恢复。本计划状态为 `verified`，G5/G6 均为 `completed / verified`；权威细节见 [G5/G6 验收记录](../06_testing/2026-08-27-g5-g6-run-holdings-noncombat-nodes.md)。
