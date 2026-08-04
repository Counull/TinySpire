---
title: 06_testing · 测试记录
page_type: testing
lifecycle: active
updated: 2026-08-05
---

- [2026-07-30 BattleScene LifetimeScope](2026-07-30-battle-lifetime-scope.md)

# 06_testing · 测试记录

- 角色：NUnit 用例说明、验收范围、回归结果。
- 与实现计划（`../plans/`）对应，记录"验证了什么、结论如何"。
- 当前状态见状态源 `../SESSION_LOG.md`。

## 验证记录

- [2026-08-05 M9 验收后 BUG 分诊与结构审查关联](2026-08-05-m9-post-validation-bug-triage.md) — 两项 Hand motion 由精确红灯、426/426 EditMode 覆盖；生命 HUD 临时投影到角色头顶，并在五种真实 BattleScene 尺寸复测为 0 相交对
- [2026-08-02 M9G 全量验证、真实交互、Player 退出与双轴复审](2026-08-02-m9g-full-validation-review.md)
- [2026-08-02 M9F 阶段横幅、胜负面板、重开与退出](2026-08-02-m9f-turn-terminal-restart-exit.md)
- [2026-08-02 M9E 出牌、弃牌、抽牌与重洗运动](2026-08-02-m9e-card-zone-motion.md)
- [2026-08-02 M9D 不可用样式、目标聚焦与正式目标素材](2026-08-02-m9d-card-focus-targeting-feedback.md)
- [2026-08-02 M9C 结算反馈、受击与死亡过渡](2026-08-02-m9c-settlement-combat-feedback-death.md)
- [2026-08-02 M9B 参与者状态、Block 与既有意图 HUD](2026-08-02-m9b-combatant-status-hud.md)
- [2026-08-02 M9A 有序表现时间线、一次 completion 与取消](2026-08-02-m9a-ordered-presentation-timeline.md)
- [2026-08-02 M8E 全量验证、真实 Game View 与双轴复审](2026-08-02-m8e-full-validation-review.md)
- [2026-08-02 M8D 状态时机、死亡与完整战斗循环](2026-08-02-m8d-status-death-battle-loop.md)
- [2026-08-02 M8C 敌人意图与 Effect 联合事务](2026-08-02-m8c-enemy-effect-transaction.md)
- [2026-08-02 M8B 命令生命周期、continuation 与表现屏障](2026-08-02-m8b-command-lifecycle-presentation-barrier.md)
- [2026-08-02 M8A 命令、状态与终局契约](2026-08-02-m8a-command-status-terminal-contract.md)
- [2026-08-02 M7E 全量验证、真实 Game View 与双轴复审](2026-08-02-m7e-full-validation-review.md)
- [2026-08-02 M7D 出牌事务与卡区结算记录](2026-08-02-m7d-card-effect-transaction.md)
- [2026-08-02 M7C 有序 Effect 执行 module](2026-08-02-m7c-ordered-effect-executor.md)
- [2026-08-02 M7B 参与者权威状态与伤害操作](2026-08-02-m7b-combatant-effect-operations.md)
- [2026-08-02 M7A 结算记录与公式契约](2026-08-02-m7a-settlement-formula-contract.md)
- [2026-08-02 M6D 全量验证、双轴复审与文档收口](2026-08-02-m6d-full-validation-review.md)
- [2026-08-01 M6C Self / Enemy 目标选择 UI](2026-08-01-m6c-self-enemy-target-selection.md)
- [2026-08-01 M6B 队首目标重校验与权威写链](2026-08-01-m6b-queue-head-target-revalidation.md)
- [2026-08-01 M6A 目标契约与纯合法性 module](2026-08-01-m6a-card-play-rules.md)
- [2026-08-01 M5D 全量验证与复审](2026-08-01-m5d-full-validation-review.md)
- [2026-08-01 M5C 敌人意图 HUD](2026-08-01-m5c-enemy-intent-hud.md)
- [2026-08-01 M5B Session、权威命令队列与生产接线](2026-08-01-m5b-session-command-queue-wiring.md)
- [2026-08-01 M5A 敌人行为配置与确定性选择核心](2026-08-01-m5a-enemy-behavior-selection.md)
- [2026-08-01 M4E 全量验证与双轴复审](2026-08-01-m4e-full-validation-review.md)
- [2026-08-01 M4D 当前单玩家命令 UI 接线](2026-08-01-m4d-single-player-command-ui.md)
- [2026-08-01 M4C 队列化结束行动与敌人顺序交接](2026-08-01-m4c-end-action-enemy-handoff.md)
- [2026-08-01 M4B 队列化出牌、能量与执行期校验](2026-08-01-m4b-queued-card-play-energy.md)
- [2026-08-01 M4A 权威命令队列与回合事实骨架](2026-08-01-m4a-authoritative-command-queue.md)
- [2026-07-31 牌面短键与 Addressables 逻辑地址迁移](2026-07-31-card-illustration-logical-keys.md)
- [2026-07-31 DataTables 工作簿简易配色](2026-07-31-datatables-simple-colors.md)
- [2026-07-31 战斗 UI 首批美术与牌面配置链路接入](2026-07-31-battle-ui-art-integration.md)
- [2026-07-30 BattleScene M3A 参与者配置与 Prefab 工厂](2026-07-30-battlescene-participant-views.md)
- [2026-07-30 Addressables 迁移](2026-07-30-addressables-migration.md)
- [2026-07-30 卡牌本地化与动态文本](2026-07-30-card-localization-dynamic-text.md)
- [2026-07-30 卡牌区域与确定性洗牌](2026-07-30-card-zones-deterministic-random.md)
- [2026-07-30 战斗配置接入运行时](2026-07-30-battle-config-runtime-integration.md)
- [2026-07-30 战斗静态配置表](2026-07-30-battle-static-config-tables.md)
- [2026-07-30 BattleState 运行时参与者模型](2026-07-30-battle-runtime-state.md)
- [2026-07-30 最小状态机 Core](2026-07-30-state-machine-core.md)
- [2026-07-30 BattleScene 拖拽出牌（最小判定）](2026-07-30-battlescene-drag-to-play-minimal.md)
- [2026-07-29 BattleScene 手牌 UI（杀戮尖塔式）](2026-07-29-battlescene-hand-ui-sts-style.md)
- [2026-07-12 BattleScene 基础手牌 UI](2026-07-12-battlescene-card-ui.md)
- [2026-07-12 LoadingScene 最短展示时间](2026-07-12-loading-scene-minimum-duration.md)
