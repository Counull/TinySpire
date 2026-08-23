---
title: G3 确定性尖塔式 Act 地图实施计划
page_type: plan
lifecycle: active
date: 2026-08-24
scope: G3 only
source: Docs/Hermes_Pegasus/design/decisions.md#决策-012g3-地图采用尖塔式分层路线
status_source: ../SESSION_LOG.md
implementation_status: verified
---

# G3 确定性尖塔式 Act 地图

## 1. 目标、决策源与授权

本计划按 `Docs/Hermes_Pegasus/design/decisions.md` 的决策 012～016 和用户本轮明确授权，实现一张可保存、可复现、可游玩的单 Act 地图闭环：

```text
Start → 明牌分层路线 → 普通战斗 → 胜利回地图 → Boss 门
```

用户同时授权添加当前 G3 所需的功能性测试数据。该授权不包含真实 Boss 战、Boss 阶段、奖励、Run 胜利、遗物实际效果/库存/次数/UI、商店、休息、事件、宝箱、精英、多人/FishNet、云/多槽或战中存档。`Docs/Hermes_Pegasus/design/decisions.md` 是已确认的玩法合同，本计划只落实代码 seam、串行停止点与验收，不修改该文件。

## 2. Seam audit 与所有权结论

现有 G2 seam 可以原位深化，不需要重写高影响 Scene、Prefab、程序集或 DI：

- Bootstrap root 继续跨 `RunEntryScene` / `BattleScene` 持有 `RunStateStore`、`RunFlowService` 和 save port；child Scene Scope 生命周期不变。
- `MapDefinition` 是创建 Run 时由 profile/version/seed 一次生成并冻结的不可变整图事实；它隐藏节点/边复制与 fingerprint 规范化细节。
- `RunStateStore` 是唯一 Map/Run 可变事实写入所有者；当前位置从实际路径派生，所选 Combat 节点只在胜利后完成并追加路径。
- `RunFlowService` 只读取配置、调用 Store、提交稳定 save document 并请求 SceneFlow；不保存第二份地图或进度。
- `RunEntryPresenter/View` 只将 Store 快照投影为节点、边、锁定/可选/完成状态，并提交带稳定 `NodeId` 的意图；hover 后继集合是临时展示，不写回 Store。
- Battle 仍只通过现有 setup source 消费冻结 Hero/HP/Deck/Encounter/seed，并由 child-scope `BattleResultRunBridge` exactly-once 回写当前 attempt。

## 3. G3-A · 固定 Profile、整图 Generator 与 Validator

按 RED → GREEN 交付纯 C# 地图核心：

1. `ActMapProfile` 以 profile ID 固定普通层数量和每层 Slot 数、Encounter 池、启用 Boss 池、本局 Boss 候选数与终点数；层数不是范围随机，也不使用自由布局。
2. map seed 从 Run random root 的独立 domain 派生，不推进或复用 Battle / Reward 随机流。
3. generator 一次创建不可进入的 Start、所有普通 Combat 节点、Boss 终点和只向下一层的边；稳定 `NodeId + Layer + Slot`、普通 `EncounterId`、Boss 候选子集与终点 `BossId` 全部冻结。同一 Boss 可拥有多个终点。
4. `MapDefinition` 防御性复制构造输入，并对 profile/version/seed/nodes/edges 做规范化 SHA-256 fingerprint；外部不能经原数组或公开集合修改内部节点/边。
5. validator 拒绝 profile/version 漂移、重复 ID/Layer-Slot/边、非稳定 NodeId、错误内容引用、非唯一 Start、缺失端点、非相邻边、Boss 出边、环、Start 不可达的普通节点、不能通向 Boss 的普通死路，以及没有普通可达终点的本局 Boss 候选。

停止点：generator determinism、不同 seed 受控变化、Profile 不可变、Boss 候选/重复终点、validator 正反例全部转绿。

## 4. G3-B · 纯可达性与 RunStateStore 权威迁移

1. 可达性由独立纯规则接收 `MapDefinition + 当前路径/节点 + movement mode`。普通模式只返回当前节点直接出边；预留 WingBoots 模式只返回紧邻下一层任意已生成节点，本轮没有遗物库存、次数、消耗或 UI。
2. 同一纯 module 计算候选节点的完整后继节点/边和可达 Boss 集合；MapDefinition 不保存派生可达集合，View 不自己遍历出另一套规则。
3. `RunStateStore.CommitNode` 在任何写入前用规则校验目标。Combat 节点进入 committed/in-battle transient，Boss 节点直接追加路径并形成稳定 `BossGateReached`。
4. 普通胜利验证当前 `RunBattleId` / node / attempt，写回结算生命、完成 committed 节点、追加路径并回 `MapReady`；旧、迟到、锁定、重复目标均零写入。
5. 普通失败不完成节点或追加失败节点到路径，直接形成 `Terminal(Defeat)`；删除 G1 `RunBattleSnapshot / RestartBattle` 的当前 Run 语义。

停止点：普通/WingBoots 可达性、完整 downstream、非法选择零写入、旧 BattleResult、胜利推进、BossGate、Defeat 无重试全部转绿。

## 5. G3-C · recipe-only 存档、原子终局与冷启动

1. 当前 schema v2 在既有 Run/Hero/HP/Deck/random root 事实之外，地图与进度只保存 map seed、generator version、profile/config ID、map fingerprint、实际 path、稳定 phase、可选 committed node 与 terminal reason；字段结构不得出现整图、节点/边副本、可选节点、可达 Boss、hover、动画、`BattleAttemptSequence` 或其他 UI/派生集合。运行时 attempt 由已完成 Combat path 与恢复 phase 推导。
2. restore 先取得精确 profile/version，以 map seed 重建整图，运行 validator 并精确比较 fingerprint；之后才校验并恢复 path、BossGate 或 Terminal。配置引用、profile、版本、fingerprint 或 path 形状不兼容均返回 typed failure。
3. schema v1 缺少 profile/version/map seed/fingerprint/path/Boss 身份，无法无歧义迁移；migrator 明确 fail-fast，不默认补值或静默重掷。
4. Victory / BossGate 只在稳定 Map 状态提交检查点。Defeat 先让 Store 进入 `Terminal(Defeat)`，再以同一终局 document 走原子单槽提交；Adapter 先耐久写入并回读校验 `terminal-intent recovery artifact`，再发布通用 temp/live。相同 document 的重试复用已有恢复物，不同 document 被拒绝，损坏或非终局恢复物 fail-closed，不能退回旧 live checkpoint。
5. 终局提交成功后 Continue 永久禁用；失败页确认离开才按 live → intent → temp 顺序删除，live 删除失败时必须保留恢复物。若退出、崩溃或冷启动，启动探测直接恢复失败页，不把终局档提供为 Continue。

停止点：schema round-trip、recipe 重建、无整图字段、profile/version/fingerprint/path/config 漂移、v1 fail-fast、终局 replace failure/temp recovery、冷启动失败页和确认删除全部转绿。

## 6. G3-D · RunEntry 明牌地图与实际闭环

1. 在既有动态 `RunEntryView` 内投影完整节点与边，不修改 Scene/Prefab。节点位置只由冻结 `Layer + Slot` 映射；只读 identity catalog 将内容 ID 投影为名称和程序化锚点，但不写回 Map/Save。5001 显示 `SLIME PATROL`、首敌本地化名与 Slime silhouette；5002 `SENTRY LINE` 只作为判别测试数据；Boss 9001/9002/9003 分别显示 `BOSS ALPHA/BETA/GAMMA` 和 Crown/Horns/Eye 锚点。同 Boss 多终点保持同一身份。
2. 只有纯规则返回的当前候选节点可交互。Start、已完成、当前、锁定与 BossGate 状态均由 RunState 投影，不在 View 维护第二进度。
3. 悬停当前可选节点时，高亮该节点、全部后继节点/边和仍可达 Boss；弱化会被放弃的路线/Boss。离开 hover 只恢复视觉，不发 Store 命令。
4. 点击 Combat 只经 Flow/Store 进入冻结 Encounter；胜利经现有 Result bridge 回图。点击 Boss 终点进入 `BossGateReached` 页面/投影，不启动 Boss Battle、奖励或 RunOutcome。
5. 失败页不提供 Restart；只有终局档已稳定后允许确认离开并删除。

停止点：Presenter/View 定向测试、实际 `新 Run → 多节点选择 → 普通战斗胜利回图 → Boss 门` 与 `普通战斗失败 → 冷启动失败页 → 确认删除` 两条链通过。

## 7. 完整验证门与证据纪律

在以下全部取得本轮真实证据前，G3 保持 `implementing` 或 `validating`，不得标记 verified：

1. Generator、validator、可达性、Store/Flow、save/terminal、Presenter/View 定向 RED→GREEN。
2. 生产与 Editor 静态编译 0 errors。
3. 完整 Unity EditMode 0 failed / 0 skipped，并记录任务 ID、数量与耗时。
4. `TinySpire/Build/Sync and Build All` 成功；Addressables Local Content 成功。若未修改 DataTables/Localization，明确记录未运行独立 Luban 的原因，不把旧构建证据冒充本轮结果。
5. 唯一现有 Unity Editor 的 Packed Play / Use Existing Build 或等价真实 bundle 链完成上述两条手测；不得启动第二个 Editor 或 batch 实例，也不得结束用户进程、删除锁或清理 Library/Temp。
6. 每个主链检查 Console，记录真实 Error 数和查询时点。

滚动证据统一写入 `Docs/Copilot_Daedalus/06_testing/2026-08-24-g3-deterministic-act-map.md`。

## 8. 影响路径、回滚与工作区保护

- 允许的产品影响限定在 `TinySpire/Assets/Scripts/Run/Map/`、现有 Run state/flow/persistence、RunEntry presentation/view、对应 Editor tests，以及本计划列出的 Daedalus 文档。
- 本轮没有新增业务配置表或可寻址素材域；首次完整 EditMode 暴露 i18n workbook 四个既有单元格漂移后，仅同步该四格合同，因此必须重新执行 `Sync and Build All`。Scene、Prefab、ProjectSettings、asmdef、HybridCLR 与 package 文件仍不在修改范围；若实际发现必须扩大到这些高影响范围，应停止并报告，而不是自行扩张。
- 回滚按 Map core、Run state/flow、persistence、RunEntry projection 和测试的窄路径分别撤销；不得使用 reset/clean，不触碰用户 staged/WIP。
- 本轮不 commit、不 push、不 broad stage；`Docs/Hermes_Pegasus/design/decisions.md` 保持只读。

## 9. 最终实施证据

截至 2026-08-24，本片为 `verified`：

- 静态 `dotnet build`：生产 **0 errors / 6 warnings**；Editor **0 errors / 12 warnings**。Mono 定向 runner：map+store **25/25**、save **21/21**、atomic **19/19**、flow **22/22**、presenter **15/15**；View 在 Unity 中 **13/13**。
- 首次完整 Unity 的两项 RED 分别来自 i18n workbook 四格漂移与测试构造参数未同步，均已修复。最终交互式完整 EditMode job `8e910a98b14f4fe4b4901ba78bf060dc` 为 **993 passed / 0 failed / 0 skipped，44.1991795s**。
- `TinySpire/Build/Sync and Build All` 成功；Addressables 子构建耗时 **22.798s**。BuildLayout `BuildError` 为空、共 12 个 bundle，Provider 均为 `AssetBundleProvider`。
- Packed Play 生产链实走 `新 Run → 两个普通节点胜利回图 → BossGateReached → 进程级冷启动恢复`，地图 fingerprint、路径、HP 与 Boss 身份保持一致；另一链实走 `普通战斗失败 → Terminal(Defeat) → 进程级冷启动失败页 → 确认删除`，没有同节点重试或 Continue。
- 所有产品验收检查点 Console Error=0。MCP 截图工具曾单独产生 5 条递归 PlayerLoop 错误；停止截图并清空后未再出现，最终完整测试与两条产品链均为 0。
- 测试档已删除，用户原 schema v1 `run-save.json` 已按原 SHA-256 `419058435D82A48EA08DBF3121F6127417EAC700D302388BFFFA4586DFEE54B9` 恢复；Addressables Play Mode 已恢复 Fast Mode。完整证据见对应 [验收记录](../06_testing/2026-08-24-g3-deterministic-act-map.md)。
