---
title: TinySpire · Run MVP 路线图
owner: Daedalus
page_type: roadmap
lifecycle: active
created: 2026-08-14
updated: 2026-08-24
status_source: SESSION_LOG.md
predecessor: ROADMAP.md
note: 本文件承担 BattleScene 之后的 Run 阶段结果、候选切片、依赖与门禁；任何阶段或切片都不因出现在本文而获得实施授权。
---

# TinySpire · Run MVP 路线图

> **当前路线入口。** 后续任务中提到“Roadmap / 跑路线图”，默认指向本文；旧 `ROADMAP.md` 只用于追溯已冻结的 BattleScene MVP（M0～M10）。当前动态状态仍查 `SESSION_LOG.md`。

> BattleScene MVP 的冻结路线见 [ROADMAP.md](ROADMAP.md)。当前动态状态只查 [SESSION_LOG.md](SESSION_LOG.md)。**G1、G2、G3 均已由对应 verified 结果完成。** G4+ 仍未开始，也不因 G3 完成而自动获得 Grill、计划或实施授权。

## 1. 当前起点与终点

起点是 2026-08-14 的 BattleScene MVP 检查点：Git commit `e07e39a`，tag `milestone-battlescene-mvp-2026-08-14`，最新完整 Unity EditMode 记录为 **807/807 passed**。单场战斗、基础卡牌内容、权威命令队列、结算与功能性反馈已经形成可复用底座。

Run MVP 的终点不是“把所有 STS 内容做完”，而是至少用一个角色、一个 Act 和一个 Boss 贯通：

```text
主菜单 → 新 Run → 地图 → 多个节点 → Boss → Run 结算 → 主菜单
```

贯通过程中，牌组、生命、金币、地图位置与确定性随机事实必须跨场景连续；任何 UI 都只能派生和提交命令，不能成为第二份 Run 状态。

当前 BattleScene UI、视觉反馈与动画是功能性基线，仍属 provisional。除非某个 Run 切片因可读性或可操作性确实被阻塞，否则表现重做不抢占游戏本体的阶段顺序；它们按第 12 节的共用门禁作为独立债务管理。

## 2. Roadmap、切片与 Grill 的职责

本文件回答四个问题：每个阶段要交付什么玩家结果、建议拆成哪些最小切片、阶段之间依赖什么能力，以及怎样才算阶段完成。它不冻结具体类名、Scene/Prefab 结构、表字段、按钮文案或一次提交包含多少文件。

本文中的状态与权限分开记录：

- **Phase status**：阶段级进度，只使用 `not-started / active / completed / blocked`。
- **Slice status**：最小竖切进度，使用 `candidate / needs-grill / proposed-for-plan / planned / implementing / validating / verified / blocked`。
- **Authorization**：当前允许执行的动作；状态推进本身不产生 Grill、计划或实现权限。
- **Acceptance**：只能由本轮真实测试、构建和手测证据支持；Phase 用 `completed`，Slice 用 `verified`，两者不混用。

每个可执行切片必须经历以下门禁：

1. **提名**：从当前阶段挑一个最小可观察结果，只登记范围与依赖，状态为 `needs-grill`。
2. **局部 Grill**：只追问这个切片的玩法事实、生命周期、失败语义、数据所有权、UI 边界、验收和明确排除项；不顺带 Grill 整个 G1 或整个 Run。
3. **窄计划**：把已确认答案写入 `plans/`，列出影响路径、回滚方式和串行停止点；计划状态仍不等于实施授权。
4. **明确授权**：用户明确批准该计划后，才能进入 TDD / 实现 / 表格 / Scene / Prefab 等写入。
5. **验收与沉淀**：完成代码、配置、Unity 原生验证和 Wiki 闭环后，才把切片标记为 `verified`。

如果一个切片在设计或实现中拆出具有独立玩法选择、生命周期边界、高影响文件或失败语义的子切片，该子切片回到 `needs-grill`，不能继承父切片的授权。纯机械接线、同一已冻结契约下的测试补齐和生成物同步不重复 Grill。

## 3. 当前状态与阶段总览

| 阶段 | Phase status | 当前/首个切片 | 玩家可见的阶段结果 | 阶段完成门槛 | 硬能力入口 |
|---|---|---|---|---|---|
| G1 | `completed` | G1-A `verified` | 从主菜单创建 Run，进入首战，胜利回图，失败可恢复 snapshot 后新 seed 重开 | G1-A 的完整测试、构建与手测证据通过 | BattleScene MVP checkpoint |
| G2 | `completed` | G2-A `verified` | 关闭并重启游戏后，可从最近稳定地图检查点继续同一个 Run | 单槽存档可原子写入、坏档安全失败、恢复事实与后续随机一致 | G1 的 Run 生命周期、snapshot、setup/result seam |
| G3 | `completed` | G3 `verified` | 生成并游玩一张可保存、可复现、开局明牌的确定性 Act 地图，胜利推进至 Boss 门，普通战斗失败立即终结 Run | 合法路线可完整推进至 `BossGateReached`；recipe 重建、失败终局冷启动与完整 Unity 验收均通过 | G2 的持久化 Run facts 与随机边界；决策 012～016 |
| G4 | `not-started` | G4-A `candidate` | 胜利后选择/跳过冻结奖励，Run 牌组变化真实进入下一战 | 奖励不重随机、不重复领取，逐卡实例与升级事实可保存 | G1 结果 bridge、G2 schema；节点绑定时依赖 G3 |
| G5 | `not-started` | G5-A `candidate` | 获得并跨战使用至少一个遗物和一个药水 | 触发顺序稳定、消费 exactly-once、保存/重开语义明确 | G2 持久化、G4 奖励/实例 seam |
| G6 | `not-started` | G6-A `candidate` | 商店、事件、休息点、宝箱各有一个可完成样本 | 候选冻结、结算原子、重进/读档不可重复获利 | G2 经济事实、G3 节点契约、所需 G4/G5 seam |
| G7 | `not-started` | G7-A `candidate` | 从新 Run 贯通一个 Act、一个精英和一个 Boss，再进入 Run 终局 | 单 Act 全链可达且内容门禁拒绝坏引用，终局只结算一次 | G3 地图/Act 结构与 G4～G6 内容流程 |
| G8 | `not-started` | G8-A `candidate` | 设置、教程、可访问性、正式表现、统计和发布验证形成产品闭环 | 目标 Player build 完整 Run 通过，兼容/性能/输入/分辨率矩阵有证据 | G1～G7 的产品关键验收 |

当前事实：G1 没有“剩余范围”。G1-A 当时明确排除的内容属于后续阶段：存档/继续在 G2；多节点 Act 地图、明牌 Boss 终点与 `BossGateReached` 在 G3；奖励与卡牌实例在 G4；遗物/药水在 G5；非战斗节点在 G6；真实 Boss 战、Boss 阶段与最终 Run 结算在 G7；真实设置/教程/统计/产品表现收口在 G8。联网和多人不属于当前 G1～G8 Run MVP。

## 4. G1 · 最小 Run 生命周期（completed）

G1-A「基础入口 → 角色选择 → 临时单节点地图 → 首战 → 胜利回图 / 失败可 SL 重开」是 G1 的完成切片，而不是 G1 的一部分占位：

- Bootstrap 默认进入单一 `RunEntryScene`；主菜单、设置/占位页、双 Hero 单选、临时地图和失败页都在该 Scene 内切换。
- `RunStateStore` 唯一拥有跨场景事实与 snapshot；`RunFlowService` 只编排。Battle 经现有 setup seam 消费 Hero、当前生命、牌组模板与 seed；Battle 只经稳定 `BattleResult` bridge 写回。
- 1001 胜利后以 17/30 回图并完成节点；1002 失败后恢复 70/70 snapshot，重开签发新 attempt/new seed 且创建干净 BattleSession。
- `Sync and Build All`、Packed Play Mode 真实 bundle 链和完整 EditMode 873/873 已通过。详细证据见 [G1-A 验收记录](06_testing/2026-08-16-g1a-entry-first-battle-run-lifecycle.md)。

G1-A 的实现提交为 `fa14889`，已位于 `main` 与 `origin/main`。后续阶段不能把 G1 的实现授权自动继承过去。

## 5. G2 · Run Persistence、恢复与确定性

**阶段结果：** 玩家可在进程/Editor 重启后继续最近一份地图稳定态；战斗中间态不进入存档，恢复后后续随机结果与未中断路径一致。

[G2-A Grill 记录](../Hermes_Pegasus/design/2026-08-16-g2a-run-persistence-grill.md) 是本切片的冻结设计源。G2-A 已按 A1 → A2 → A3 串行实施并验证；以下 A1～A3 始终是同一个 G2-A 竖切的串行停止点，不是三个独立 Goal，也不存在 G2-B。

| 切片 | 具体做什么 | 主要交付物 | 通过标准 |
|---|---|---|---|
| G2-A1 · Save Document 契约 | 冻结 v1 只保存哪些 Run 事实、schema/version/migration 与坏档规则 | 显式 Save DTO、领域映射、校验器、可替换 save-store seam、内存 fake | S0/S1 round-trip 等价；BattleSession、ActiveBattle、手牌、敌人、队列和 Unity Object 无法进入存档；坏 JSON/版本/配置 ID 明确失败 |
| G2-A2 · 原子本地单槽 | 在 `persistentDataPath` 提供一个版本化 JSON 槽，临时写入并校验后再提交 | load/commit/delete Adapter、临时文件与错误结果、文件 IO 定向测试 | 写入失败不覆盖上一份有效存档；不可写、坏正式文件、残留临时文件均有可诊断结果，不静默删档 |
| G2-A3 · 检查点与继续游戏 | Hero 确认后保存 S0，节点完整结算后保存 S1；主菜单提供继续、放弃确认和保存失败重试 | RunFlow 编排、继续/放弃/错误 UI、冷启动恢复与生命周期测试 | 冷启动可恢复 S0/S1；战斗中断回最近稳定检查点；非稳定态不写盘；commit 失败阻断继续推进；G1 胜败/重开仍通过 |

G2-A 的最终验收记录见 [2026-08-16 G2-A Run Persistence 与继续游戏](06_testing/2026-08-16-g2a-run-persistence.md)：完整 EditMode 947/947、Luban 与本地 Addressables 构建、唯一 Editor 的 S0 / Continue / 战中不写盘 / S1 / 冷启动恢复 / 确认删除主链均已通过。

**G2 完成门槛：** 当前最小 Run 能跨进程恢复；存档损坏或不兼容时安全失败；恢复后的 Hero、HP、模板 ID、节点事实与随机事实不漂移；同一稳定检查点的下一次地图/战斗/奖励随机输入与未中断路径一致。

**明确不做：** 战斗中途保存、云存档、多槽、跨设备、账号/平台 SDK、反作弊、真实多节点地图、奖励和永久死亡。战斗中断后 attempt/seed 的精确恢复规则必须在 G2-A 计划前再次核对，不能由 Adapter 暗定。

## 6. G3 · 确定性尖塔式 Act 地图与统一节点流程

**阶段结果：** 用开局完整生成并冻结、可保存、可复现、可验证的分层 DAG 替换 G1 的单个布尔节点。玩家从不可进入的 Start 选择明牌普通战斗路线，胜利回图并最终抵达可保存的 Boss 门；普通战斗失败立即形成不可继续的 `Terminal(Defeat)`。

| 切片 | 具体做什么 | 主要交付物 | 通过标准 |
|---|---|---|---|
| G3-A · 纯地图模型、配置与生成器 | 用固定 `ActMapProfile` 层/槽配置和独立 map seed 一次生成整张分层 DAG；冻结普通节点 `EncounterId`、Boss 候选子集和终点 `BossId` | 稳定 `NodeId + Layer + Slot`、不可变 Map/Node/Edge、纯 C# generator 与 validator | 同 recipe 同图；无环、无重复 ID、边引用有效；所有普通节点从 Start 可达且能通向 Boss；每个候选 Boss 普通可达；地图 RNG 不影响 Battle/Reward RNG |
| G3-B · 节点权威与纯可达性 | `RunStateStore` 持有冻结 MapDefinition 和唯一可变进度；普通模式只允许直接出边，并预留“下一层任意已生成节点”的 WingBoots 纯规则 | 原子 RunState 迁移、Node/attempt 关联、普通/WingBoots reachability、完整后继与可达 Boss 查询 | 过期/锁定/重复输入零写入；旧 BattleResult 不能结算新节点；UI 与 `node.Outgoing` 不成为可选性事实源 |
| G3-C · 功能性明牌地图、Boss 门与失败终局 | 在现有 `RunEntryScene` 投影完整拓扑、Encounter/Boss 身份和 hover 后半程预览；普通战斗胜利回图，Boss 终点进入 `BossGateReached`；失败进入 `Terminal(Defeat)` | Map projection/presenter、点击命令、Encounter 输入、Battle 往返、Boss 门、失败页与确认删除 | 多条路线可实际入战；胜利推进；hover 高亮完整后继和可达 Boss；失败节点不完成、无同节点重试、Continue 禁用，冷启动恢复失败页 |
| G3-D · recipe-only 存档与完整回归 | 存 seed、generator version、profile/config ID、SHA-256 fingerprint、path/phase/终局必要事实；attempt 由 path+phase 推导，不存整图/UI/派生集合 | schema v2、terminal-intent recovery artifact、typed restore failure、round-trip、scene re-entry、Packed Play 证据 | 冷启动恢复同一图和位置；fingerprint/version/profile/path 漂移 fail-fast；终局不复活旧 Continue；无歧义迁移不了的 v1 明确失败且不静默重掷；Console Error 为 0 |

**G3 完成门槛：** 玩家能从新 Run 的明牌确定性地图选择多节点合法路线，经普通战斗胜利回图并抵达 `BossGateReached`；关闭再继续后通过 recipe 重建的地图拓扑、Encounter/Boss 身份、当前位置与路径完全一致。另一条真实链必须证明普通战斗失败原子保存为 `Terminal(Defeat)`、不可 Continue、冷启动仍为失败页且只有确认离开才删档。Generator/validator/可达性/存档/失败终局定向测试、完整 EditMode、`Sync and Build All`、Addressables/Packed Play 或等价 Unity 手测与 Console Error 0 均取得本轮真实证据后，G3 才可标记 completed / verified。

**G3 完成证据（2026-08-24）：** seam audit、五组纯 C#/Mono 定向与 View 13/13 均通过；最终交互式完整 EditMode job `8e910a98b14f4fe4b4901ba78bf060dc` 为 **993/993 passed**。`Sync and Build All` 与 Local Addressables 成功，BuildLayout 的 12 个 bundle 均使用 `AssetBundleProvider`。Packed Play 实走两个普通节点胜利后进入并冷启动恢复 `BossGateReached`；另一新 Run 实走普通战斗失败、原子 `Terminal(Defeat)`、进程级冷启动失败页和确认删除。各产品检查点 Console Error=0；详细证据见 [G3 验收记录](06_testing/2026-08-24-g3-deterministic-act-map.md)。

**明确不做：** 真实 Boss 战、Boss 阶段、奖励、Run 胜利、遗物实际效果/库存/次数/UI、正式地图美术、滚动大地图、精英、商店、事件、休息点和宝箱；也不做多人/FishNet、云/多槽或战中存档。Boss 身份和门属于 G3 的冻结 MapDefinition，但真实 Boss Encounter 与 RunOutcome 仍由 G7 接入。

## 7. G4 · 战斗奖励、Run 牌组与卡牌实例

**阶段结果：** 胜利后由 Run 冻结卡牌奖励，选择或跳过后原子修改逐卡实例牌组；新增/升级实例真实进入下一场 Battle。

| 切片 | 具体做什么 | 主要交付物 | 通过标准 |
|---|---|---|---|
| G4-A · RunDeck 实例 | 初始牌组模板只展开一次，每张卡有稳定实例 ID 和升级事实；Battle 接收不可变牌组快照 | RunCard instance/ordered deck、save migration、Battle setup 投影、legacy template fallback | 同模板副本身份独立；临时战斗卡和牌区不回写 Run；保存后实例身份稳定 |
| G4-B · 冻结奖励候选 | 胜利后用独立 reward seed 从 Hero 奖励池生成一次 Pending；失败无奖励 | RewardPending、奖励池配置/校验、随机域测试 | 刷新 UI、切语言、重建 Scene 或读档都不重随机；空池、坏引用和 `CatalogOnly` 内容被拒绝 |
| G4-C · 选择/跳过闭环 | 在 `RunEntryScene` 加最小奖励页，选择一张或跳过后完成节点并提交稳定存档 | reward presenter、原子选择/跳过命令、commit-failure 状态 | 选择精确新增一个实例，跳过不改牌组；重复/伪造输入零写入；新卡下一战可抽到；存档失败保持 Pending |
| G4-D · 升级运行时 | 为一小组明确卡牌补齐升级后的费用、文本、归宿和规则数值读取 | 升级 schema、formatter/合法性/执行投影、实例级测试 | 只升级指定副本；保存恢复后仍生效；基础/升级行为与文本一致；无定义卡不可升级 |

**G4 完成门槛：** `胜利 → 冻结奖励 → 选择/跳过 → 回图 → 下一战` 全链成立，重进/读档不会刷新或重复领取；RunDeck 和升级事实跨战、跨进程一致。

**明确不做：** 遗物、药水、大型奖励池、奖励动画、删牌/转化、通用任意卡升级。玩家主动升级的首个入口属于 G6 休息点；G4 只先交付实例与规则能力。

## 8. G5 · 遗物、药水与跨战时机

**阶段结果：** Run 拥有至少一个遗物和一个可消耗药水，它们经明确的 Battle 输入、结算和 snapshot 语义跨越战斗，UI 不拥有业务状态。

| 切片 | 具体做什么 | 主要交付物 | 通过标准 |
|---|---|---|---|
| G5-A · 持有物事实与时机词汇 | Grill 模板/实例、槽位/堆叠/唯一性和首个真实触发时机 | Relic/Potion inventory facts、原子获得/移除、save migration、稳定时机词汇 | round-trip 等价；非法/重复获得或移除零写入；时机和排序有纯领域测试 |
| G5-B · 一个无选择遗物 | 把冻结遗物输入带入 Battle，并用既有命令/结算时机执行一个简单效果 | setup 扩展、一个配置样本、Battle 执行与表现 | 同一时机精确触发一次；多来源排序确定；Scope 卸载无遗留订阅 |
| G5-C · 一个无目标药水 | Battle UI 只提交使用意图，Battle 记录已接受消费，唯一结果 bridge 回写实例 ID | potion command/HUD、类型化 BattleResult 字段、消费回写 | 重复点击/非法使用零写入；成功消费 exactly-once；失败 SL 是否恢复药水按已 Grill 规则执行 |
| G5-D · 获得与最小表现 | 把一种遗物/药水接入既有奖励来源，并在 Run 页面显示只读持有物 | 一个真实获得来源、TMP+i18n 面板、配置/资源门禁 | 获得、保存、读档、入战和消费闭环通过；场景重建不重复获得 |

**G5 完成门槛：** 一个遗物和一个药水均可真实获得、保存、恢复并进入战斗；遗物稳定触发，药水只消费一次，失败重开不继承未授权的 Battle 临时状态。

**明确不做：** 大型随机池、装备/制作、通用事件总线、持久充能遗物、选择目标药水、重构 Battle 队列或 DI。

## 9. G6 · 商店、事件、休息点与宝箱

**阶段结果：** 复用 G3 的节点生命周期和一套冻结/认领事务，分别交付四类非战斗节点的一个真实样本。

| 切片 | 具体做什么 | 主要交付物 | 通过标准 |
|---|---|---|---|
| G6-A · 非战斗访问契约 | 为节点冻结候选/价格/选项并记录未结算、已结算；不另造第二套节点状态机 | NodeVisit/Pending facts、一次性 settle/claim seam、save migration | 重进/读档候选不刷新；已结算不可再次认领；过期/伪造输入零写入 |
| G6-B · 休息点 | 经 Grill 后实现治疗与升级的最小选择，升级复用 G4 实例能力 | 一个页面、两条原子命令、一个真实节点样本 | 只能选择一次；治疗不超上限；只升级合法指定实例；结算后回图 |
| G6-C · 宝箱 | 冻结并认领一种既有 G4/G5 奖励 | 一个宝箱配置/页面、reward seam 复用 | 首次进入候选固定；认领 exactly-once；重进/读档不能再领 |
| G6-D · 商店 | 冻结一组最小库存和价格，只售既有实例类型 | 金币事实、库存/价格、原子 purchase、最小商店页 | 扣款与入库同事务；余额不足/重复购买零写入；重进不刷新库存 |
| G6-E · 事件 | 交付一个两选一事件，结果只调用既有 Run 命令 | 一个事件配置/页面、choice settlement | 选项冻结、只能选择一次、读档不变；首个样本不引入通用事件 DSL |

**G6 完成门槛：** 一条混合路线能分别进入并完成休息、宝箱、商店和事件；每类节点都独立通过保存/恢复、重进和 exactly-once 验收。

**明确不做：** 动态经济、出售/回购/刷新、事件脚本语言、大事件池、连锁事件、复杂动画和多人投票。

## 10. G7 · 单 Act、精英、Boss 与 Run 终局

**阶段结果：** 把前述能力组合成一个确定性的单 Act，至少包含一个精英和一个 Boss，Boss 后产生一次权威 RunOutcome 并返回主菜单。

| 切片 | 具体做什么 | 主要交付物 | 通过标准 |
|---|---|---|---|
| G7-A · Act 内容与路线门禁 | 在 G3 的 `ActMapProfile` / Boss 门之上引用普通、精英、真实 Boss 内容池和完成规则 | Act manifest、跨表 validator、最小内容矩阵 | 引用存在、池非空、Boss 可达、必要文本齐全；不另造第二套地图模型 |
| G7-B · 一个精英 | 用现有 Encounter/Battle setup 表达精英，奖励继续走 G4 | 精英 Encounter、节点规则、一个差异化样本 | 精英进出仍走唯一 setup/result seam；胜败、奖励和读档一致 |
| G7-C · 一个 Boss | Grill 并实现一种最小 Boss 阶段模型；阶段事实留在 Battle | Boss Encounter/phase、Battle 投影、定向测试 | 阶段切换只发生一次且可预测；Run 只记录节点/Act 进度；UI 不写阶段事实 |
| G7-D · Run 终局 | 在 G3 已有普通战斗 `Terminal(Defeat)` 上补 Boss 胜利/失败与主动放弃的类型化结果和最小结果页 | RunOutcome、终局迁移、save finalize/clear、返回主菜单 | 终局只结算一次；旧 save 不可继续；不得恢复已由决策 016 删除的普通战斗失败重试 |
| G7-E · 聚合内容门禁与完整 Run | 聚合 Act→Map→Node→Encounter/Reward/Event/Item 引用检查并跑全链 | 构建期 fixtures、Packed/Player 完整 Run 证据包 | 拒绝空池、坏引用、不可达 Boss、重复唯一奖励、缺失 i18n/素材键；主菜单到终局再回主菜单，Console Error 0 |

**G7 完成门槛：** 一个角色可从新 Run 经过地图、战斗与非战斗节点、精英和 Boss，完成单 Act 后进入唯一终局并回主菜单；相同 root seed/save 的路线和冻结候选一致。

**明确不做：** 多 Act、Ascension、每日挑战、多个真实 Boss Encounter / 多 Boss 战内容、通用 Boss DSL、全量内容目录、联网排行榜和多人；G3 已冻结的多候选 Boss 身份与多个 Boss 终点不属于此排除项。

## 11. G8 · 产品化与发布门禁

**阶段结果：** 在不改变 Run/Battle 权威边界的前提下，把完整单 Act 竖切收口成可发布、可复验的产品基线。

| 切片 | 具体做什么 | 主要交付物 | 通过标准 |
|---|---|---|---|
| G8-A · 应用设置 | 把语言、音量、显示等逐项 Grill 后持久化；设置不进入 RunState | 独立 App/Profile settings owner、版本与坏数据回退、真实设置页 | 重启恢复、坏数据安全回退、不污染 Run save；每个选项有实际效果 |
| G8-B · 输入/分辨率/可访问性 | 冻结键鼠/手柄、宽高比、文字缩放、高对比/减少动态效果支持矩阵 | 导航与布局适配、可访问性选项、矩阵测试 | 所有必经页面可在声明组合下完成导航，无截断、失焦或不可达按钮 |
| G8-C · 首轮教程 | 用 Profile 记录教程进度，只覆盖已稳定的完整 Run | 提示/阻挡层、跳过/重置、教程状态 | 教程不直接写 Battle/Run；重启可续，跳过后不再阻断正常输入 |
| G8-D · 表现与音频收口 | 按入口、地图/节点、奖励、Boss/结果、Battle 分批替换功能占位 | 正式 UI/音频/VFX、各素材域地址与构建门禁 | 每批独立验收；真实资源经 BuildLayout + Packed/Player 证明 bundle 加载 |
| G8-E · 统计与 Run 历史 | 从权威 RunOutcome 生成不可变 RunSummary，驱动统计页 | history store、统计 projection、去重写入 | 只有终局写一次；运行中 UI 不自行累计；重启后统计一致 |
| G8-F · 发布验证矩阵 | 验证存档迁移、崩溃中断点、语言/输入/分辨率、性能和目标平台构建 | 明确支持矩阵、Player build、完整 Run 回归与原始证据 | 目标平台完整单 Act 通过，Console Error 0，性能预算和兼容规则达到已 Grill 指标 |

**G8 完成门槛：** 目标 Player build 能在声明的输入、语言和分辨率组合下完整跑通单 Act；设置、教程和统计可跨重启恢复；最新 BuildLayout、性能、存档兼容和完整 Run 回归都有本轮证据。

**明确不做：** 云同步、成就、遥测、商业化、联网/多人、多平台同时首发、全语种、全量配音或大型过场；这些需要另建并 Grill 新 Roadmap。

## 12. 所有阶段共用的不可破坏门禁

- `RunStateStore` 继续是跨场景 Run 业务事实的唯一写入口；`RunFlowService` 只编排；View 只提交意图和渲染 projection。
- Battle 输入继续走 `IBattleSetupOptionsSource / BattleSetupOptions`；Battle 对 Run 的持久影响继续合并进唯一稳定 `BattleResult` bridge。
- Map、Battle、Reward、Event 等随机域必须显式分开；同一稳定输入可复现，增加某一域的随机调用不能漂移其他域。
- 每个切片先做纯领域 RED，再实现最小状态所有者，再接 Presenter/View；不得为了未来内容预建万能框架。
- 修改 Luban 表或 i18n 必须运行 `TinySpire/Build/Sync and Build All`；新增可寻址素材域必须精确同步 Group，并以最新 BuildLayout 和 Packed Play/Player 证明真实 bundle 加载。
- 每个切片至少需要定向测试、完整 EditMode、与风险相称的 Packed Play/Player 手测、Console Error 0，以及本轮原始证据；不得沿用旧数量冒充本轮验证。
- 出现新的玩法选择、生命周期边界、破坏性 schema migration 或高影响 Scene/DI 改动时，立即回到 `needs-grill`；前一切片的授权不能继承。

表现债务继续独立管理：局部可读性/可操作性阻塞当前切片时做最小修复；大范围视觉语言、HUD 重排、动效节奏和正式资源替换留给独立表现切片及 G8，不抢占 G2～G7 的业务事实闭环。

## 13. 下一步

G3「确定性尖塔式 Act 地图」已完成并 `verified`；最终完整 EditMode 993/993、`Sync and Build All`、Local Addressables、Packed Play 胜利到 BossGate 与失败终局两条生产链、进程级冷启动和 Console Error 0 均已取得本轮证据。下一候选是 G4-A，但仍为 `candidate`，必须先完成独立局部 Grill、窄计划和明确实施授权。

G4+、Platform Save Spike、平台 SDK、云存档、多槽、真实 Boss 战、奖励和遗物实际效果均未获授权。任何后续切片仍须重新经过 Grill、窄计划、明确授权和独立验收，不能继承 G3 的权限。
