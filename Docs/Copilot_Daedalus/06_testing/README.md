---
title: 06_testing · 测试记录
page_type: testing
lifecycle: active
updated: 2026-08-06
---

- [2026-07-30 BattleScene LifetimeScope](2026-07-30-battle-lifetime-scope.md)

# 06_testing · 测试记录

- 角色：NUnit 用例说明、验收范围、回归结果。
- 与实现计划（`../plans/`）对应，记录"验证了什么、结论如何"。
- 当前状态见状态源 `../SESSION_LOG.md`。

## 验证记录

- [2026-08-06 STS2 Ironclad I4 成功归宿与 Tremble](2026-08-06-sts2-ironclad-i4-success-destination.md) — Tremble 以 3 层易伤和 Exhaust 真实归宿翻为 Implemented；相关 61/61、完整 EditMode 482/482、Luban/Localization/Local Content 通过，不含 Exhaust 飞行动画
- [2026-08-06 STS2 Ironclad I3 85 张目录与占位素材](2026-08-06-sts2-ironclad-i3-card-catalog.md) — 冻结单人卡 85/85 录入，82 张 CatalogOnly 复用既有占位并走真实 AB；完整 EditMode 479/479、真实牌面加载 5/5 通过
- [2026-08-06 STS2 Ironclad I2 CatalogOnly 构建隔离](2026-08-06-sts2-ironclad-i2-build-isolation.md) — Deck/程序/牌面/记录身份在 Localization 与 Addressables 前 fail-fast；最终相关 102/102、Local Content 与真实逻辑地址 1/1 通过
- [2026-08-06 STS2 Ironclad I1 CatalogOnly 运行时隔离](2026-08-06-sts2-ironclad-i1-catalog-runtime-gate.md) — Queue typed failure 在费用、卡区与 Effect 写入前终止；精确 1/1、相关与同步构建后回归各 86/86，Luban 与 Local Content 成功
- [2026-08-05 DOTween Pro 仓库净化与免费版独立验证](2026-08-05-dotween-pro-repository-sanitization.md) — 当前树与 GitHub `main` 可达历史已移除 Pro；免费版无 Pro 编译、459/459 EditMode、真实 Bootstrap、精确非 Pro LFS 补传与远端回读审计均通过
- [2026-08-05 配置素材短键与真实 AssetBundle 加载](2026-08-05-config-asset-logical-keys.md) — Hero/Enemy 表迁移为短键，构建期漂移校验、逻辑地址组、Luban/本地内容构建、Packed Play Mode 物理 bundle 与真实 Game View 证据
- [2026-08-05 M10D 交付级验证与性能基线](2026-08-05-m10d-delivery-validation.md) — M10 定向 EditMode 25/25、默认 Game View/Console 和可重复微基线已取证；完整 451 项中的两项非 M10 UI/Targeting 异常保留为历史事实，不将其伪报为 M10 全量全绿
- [2026-08-05 M10C 确定性、帧率无关与生命周期回归](2026-08-05-m10c-determinism-lifecycle.md) — Submit/只读事实轨迹在 30/60/120 FPS、加速和立即完成下相同；取消、重启、Scope/Scene 生命周期定向回归 3/3、相关聚合 53/53 与真实 Bootstrap Play Mode 证据
- [2026-08-05 M10B Bootstrap 可见失败路由与默认内容黄金基线](2026-08-05-m10b-bootstrap-golden-baseline.md) — typed 配置失败停止路由、作者表/生成 JSON/Localization 三方黄金断言、运行时流程 key 门禁与正常 Bootstrap 实测；M10A+M10B 定向 EditMode 21/21 通过
- [2026-08-05 M10A 配置原子性与表清单 fail-fast](2026-08-05-m10a-config-fail-fast.md) — 配置 typed failure、原子发布、重试与四份清单构建期校验；定向 EditMode 9/9 通过
- [2026-08-05 M9 目标箭头与锁定框视觉反馈验收](2026-08-05-m9-targeting-visual-feedback.md) — 分段切线箭身、四角锁定框与相关 Prefab 契约；Unity 定向类集 26/26 通过
- [2026-08-05 M9 出牌不飞向怪物验收](2026-08-05-play-card-no-target-flight.md) — Prelude 仅持有 transient，卡牌只飞向弃牌堆；Unity 定向类集 26/26 通过
- [2026-08-05 M9 验收后 BUG 分诊与结构审查关联](2026-08-05-m9-post-validation-bug-triage.md) — 两项 Hand motion、生命 HUD 头顶投影及 `BUG-UI-002` 伤害飘字局部排序均有精确红绿证据；当前完整 EditMode 460/460，玩家/敌人真实 HUD 前景与 Console 已复核
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
