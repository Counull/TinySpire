---
title: Daedalus · 依赖项账本（Dependency Ledger）
page_type: registry
lifecycle: active
created: 2026-07-29
updated: 2026-08-02
status_source: SESSION_LOG.md
note: 全项目范围唯一的依赖项 ID 分配与状态账本；实现计划文档只引用 ID，不重复维护完整描述。
---

# Daedalus · 依赖项账本（Dependency Ledger）

> 每条"本轮先占位、留给未来解决"的实现细节，都在这里登记一个全局唯一 ID（`DEP-NNN`）。代码中用 `// TODO(DEP-NNN): <一句话>` 标记对应位置；plan 文档只引用 ID + 一句话摘要，不重复维护阻塞条件全文——这份文件是唯一事实源。

## 使用规则

1. **ID 分配**：新依赖项永远追加到表格末尾，用下一个未使用的编号，不重用、不跳号。**不要在单个 plan 文档里独立编号**，避免多个 plan 各自从 `DEP-001` 开始导致撞号。
2. **状态**：`open`（尚未解决）/ `resolved`（已解决，保留记录不删除）。
3. **解决时**：把状态改成 `resolved`，补充"解决记录"列（哪个 plan/commit 解决的、怎么解决的），不要删除整行。
4. **代码标记**：代码里对应位置写 `// TODO(DEP-NNN): <一句话说明>`，一句话应能让人不查文档也大致知道要做什么；详细阻塞条件查本文件。

## 依赖项列表

| ID | 内容 | 阻塞条件 | 涉及代码位置（预期/实际） | 来源 Plan | 状态 | 解决记录 |
|---|---|---|---|---|---|---|
| DEP-001 | 当前世界空间参与者的屏幕目标命中、合法高亮与目标提交 | M3/M5 已确定参与者为世界空间 `SpriteRenderer`、HUD 为 UGUI；M6C 计划复用 `BattleParticipantPresenter` 唯一 View 映射，将 `SpriteRenderer.bounds` 投影为屏幕矩形并完成真实 Game View 验证，不增加 Collider、Physics2D Raycaster 或第二套注册表 | `TinySpire/Assets/Scripts/UI/Battle/Hand/HandCardContainer.cs`、`TinySpire/Assets/Scripts/UI/Battle/BattleParticipantPresenter.cs`、`TinySpire/Assets/Scripts/UI/Battle/ParticipantHudView.cs` | `plans/2026-07-29-battlescene-drag-to-play-minimal.md`、`plans/2026-08-01-m6-card-play-legality-target-selection.md` | resolved | M6C：复用 Presenter 唯一 View 映射完成 `SpriteRenderer.bounds` 屏幕矩形命中、稳定候选顺序、合法/悬停高亮及 Self/Enemy 精确目标提交；定向 EditMode、Addressables、Bootstrap 与真实 Game View（含左右敌人、无效释放、费用不足视觉拖动和多分辨率）均通过，未增加 Collider、Physics2D Raycaster 或第二套注册表。 |
| DEP-002 | 费用/能量系统与检查逻辑 | 需先定义能量池数据结构；最终应由出牌命令在提交区域移动前统一校验 | `TinySpire/Assets/Scripts/UI/Battle/Hand/HandCardContainer.cs` | `plans/2026-07-29-battlescene-drag-to-play-minimal.md` | resolved | `plans/2026-07-31-m4-turn-scheduling-energy.md` M4D：UI 只提交 `PlayCardCommand`；队首按当前 `Card.Cost`、玩家能量和卡区事实校验，且仅在执行成功后扣能量并移动卡牌。 |
| DEP-003 | 拖过出牌线的最终视觉样式 | 需要策划/美术确认最终表现 | `TinySpire/Assets/Scripts/UI/Battle/Hand/HandCardVisual.cs` | `plans/2026-07-29-battlescene-drag-to-play-minimal.md` | open | M6C 人工审阅确认功能性冻结/箭头可用，但最终聚焦位置与 Tween 留给 M9；需求见 `10_communication/2026-08-02-battle-card-motion-feedback-brief.md`。Linear [LXX-6](https://linear.app/lxxr/issue/LXX-6) 已完成箭身、箭头及合法/悬停高亮四张 PNG 的美术交付，并在回复中验证尺寸、RGBA、透明中心及文件一致性；Issue 明确不实施交互或资源接线。Unity 后续已为工作区文件生成未跟踪 Meta，但 M9 尚未确认最终切片/缩放契约或接入生产 Prefab，因此本依赖继续保持 open。 |
| DEP-004 | 打出后卡牌的销毁前过渡动作（按卡牌效果类型区分） | M7 已提供有序 Effect 与 `CardMoved` 结算记录；仍需 M9 消费该记录并实现对应过渡 | `TinySpire/Assets/Scripts/UI/Battle/Hand/HandCardContainer.cs` | `plans/2026-07-29-battlescene-drag-to-play-minimal.md`、`plans/2026-08-02-m7-effect-executor.md` | open | M6C 人工审阅补充结束行动弃牌过渡需求；M9 只观察权威卡区变化，不保留假手牌。M7 已提供按权威顺序冻结的 Effect 与 `CardMoved` 结算记录，但不播放过渡；依赖仍需 M9 实际消费后才能 resolved。需求见 `10_communication/2026-08-02-battle-card-motion-feedback-brief.md`。 |
| DEP-005 | `BattleLifetimeScope` 已注册战斗会话，但回合调度器与其余战斗局内模块仍待确定后注册 | 需要先完成路线图 M3～M4 的视图与回合流程边界 | `TinySpire/Assets/Scripts/Battle/BattleLifetimeScope.cs` | `plans/2026-07-30-battle-config-runtime-integration.md` | resolved | `plans/2026-07-31-m4-turn-scheduling-energy.md` M4C：注册 `BattleCommandQueue`、表现 adapter 与启动/逐帧驱动；阶段模块由队列内部持有。 |
| DEP-006 | 初始手牌临时取初始卡组的前 N 张，尚未经过抽牌堆洗牌与抽取 | 需要先实现 `CardZoneState`、战斗专属确定性随机源与抽牌/重洗流程 | `TinySpire/Assets/Scripts/Battle/BattleSession.cs` | `plans/2026-07-30-battle-config-runtime-integration.md` | resolved | `plans/2026-07-30-card-zones-deterministic-random.md`：创建完整卡组、确定性洗牌后抽取初始手牌，并实现弃牌重洗。 |
| DEP-007 | BattleScene 的战斗种子当前由 Inspector 常量提供，尚未来自 Run 的根种子/存档 | 需要先实现 `RunState`、战斗创建标识与随机流派生/恢复规则 | `TinySpire/Assets/Scripts/Battle/BattleLifetimeScope.cs` | `plans/2026-07-30-card-zones-deterministic-random.md` | open | — |
| DEP-008 | 多人根模型当前只接入一个玩家与一套 `BattleCardZonesData` | 需要 Party/Run 装配能够创建多名玩家及各自独立牌组，再把 `CombatantId` 映射到对应卡区 | `TinySpire/Assets/Scripts/Battle/BattleSession.cs`、`TinySpire/Assets/Scripts/Battle/BattleLifetimeScope.cs`、`TinySpire/Assets/Scripts/Battle/Turn/` | `plans/2026-07-31-m4-turn-scheduling-energy.md` | open | — |
| DEP-009 | M5 已完成敌人行为组、权威当前意图、确定性选择、HUD 与完成后下一意图，但仍没有真实行为 Effect 执行 | M7 已建立共享 Effect/目标操作边界；仍需 M8 让敌人当前意图执行伤害、格挡、状态及死亡/中止规则 | `TinySpire/Assets/Scripts/Battle/BattleEnemyIntentsData.cs`、`TinySpire/Assets/Scripts/Battle/Commands/BattleCommandQueue.cs`、`TinySpire/Assets/Scripts/Battle/Effects/`、`TinySpire/Assets/Scripts/Battle/BattleEnemyActionExecutor.cs` | `plans/2026-07-31-m4-turn-scheduling-energy.md`、`plans/2026-08-01-m5-enemy-intents-deterministic-behavior.md`、`plans/2026-08-02-m7-effect-executor.md`、`plans/2026-08-02-m8-enemy-actions-status-timing-battle-loop.md` | resolved | M8：敌人当前意图通过 M7 同一 ordered `BattleEffectId` executor、显式目标与共享公式执行；联合事务覆盖 Block/Vulnerable、下一 Intent/history/random、死亡 source 跳过、玩家死亡中止剩余敌人与最后敌人死亡终局。自动与多轮真实 Game View 证据见 `06_testing/2026-08-02-m8d-status-death-battle-loop.md`、`06_testing/2026-08-02-m8e-full-validation-review.md`。 |
| DEP-010 | 命令执行中途需要所属玩家做局部选择时，尚无暂停/续接协议 | 需要目标选择与 Effect 系统定义输入 token、所有权、取消和超时语义，同时保证其他玩家仍可提交命令 | `TinySpire/Assets/Scripts/Battle/Commands/` | `plans/2026-07-31-m4-turn-scheduling-energy.md` | open | — |
| DEP-011 | M4 只实现单机本地权威序号，尚无联机 Host 确认、广播、重放和失同步恢复 | 需要 Lobby/Run 生命周期、玩家网络身份、可靠消息与确定性状态校验方案 | `TinySpire/Assets/Scripts/Battle/Commands/` | `plans/2026-07-31-m4-turn-scheduling-energy.md` | open | — |
| DEP-012 | 当前 `Card` 配置没有成功出牌后进入 Discard 或 Exhaust 的权威归宿字段 | 需要玩法确认消耗牌范围，并在确有首张消耗牌时扩展 Card 表、Luban 生成和 Addressables 内容；不得按模板 ID、卡名或 EffectType 硬编码 | `DataTables/Datas/battle.card.xlsx`、`TinySpire/Assets/Scripts/Battle/BattleCardZonesData.cs`、未来出牌事务 | `plans/2026-08-02-m7-effect-executor.md` | open | M7 MVP 的 Strength/Strike/Defend/Bash 全部进入弃牌堆；现有 `ExhaustFromHand` 不代表已有归宿规则。 |
| DEP-013 | Block 清理、Vulnerable 衰减及状态在回合开始/结束的触发时机尚未接入权威调度 | 需要 M8 把自动连跳阶段升级为可验证的状态时机，并与敌人行动、死亡中止和表现屏障一起处理 | `TinySpire/Assets/Scripts/Battle/CombatantData.cs`、`TinySpire/Assets/Scripts/Battle/Turn/BattleTurnController.cs`、`TinySpire/Assets/Scripts/Battle/Effects/BattleStatusTiming.cs` | `plans/2026-08-02-m7-effect-executor.md`、`plans/2026-08-02-m8-enemy-actions-status-timing-battle-loop.md` | resolved | M8：玩家 RoundStart 固定 Block → Energy → Draw，EndPlayerAction 固定 Discard → Vulnerable；敌人固定 Block → Effect → Vulnerable → Intent。0 值/死亡不伪造记录，表现 completion 只释放后继屏障。自动、多轮物理与暂停屏障证据见 M8D/M8E 验收页。 |
