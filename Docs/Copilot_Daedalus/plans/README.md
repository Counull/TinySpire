---
title: plans · 实现计划（03_design 角色）
page_type: plan
lifecycle: active
updated: 2026-08-14
m10_status: m10-complete-targeted-25-green-full-451-with-2-non-m10-failures
---

## 最新状态

- [Run MVP 路线图](../RUN_ROADMAP.md) — 当前阶段结构与逐切片门禁；动态状态查 `../SESSION_LOG.md`，本索引不预登记覆盖整个 G1 的实施计划
- [2026-08-06 STS2 v0.107.1 Ironclad 单人卡池接入](2026-08-06-sts2-v01071-ironclad-card-pool.md) — Juggernaut 基础态已经 settlement-derived trigger 与 Queue 表现屏障正式发布；Ironclad 15/70、全项目 98/70，定向 7/7、完整 EditMode 807/807
- [2026-08-12 Marine Game 机枪兵 V2 卡牌单场战斗接入](2026-08-12-machine-gunner-v2-card-runtime-plan.md) — Unstoppable 基础态已共用同一 settlement trigger 深模块正式发布；Marine 82/0（V1 64/0、V2 18/0）

# plans · 实现计划

- 角色：llm-workflow `03_design` 的本地化落点——各切片的实现计划与技术方案。
- 每份计划含：目标、影响的层（计算/状态/时序/UI）、Open Question、验收点。
- 计划是 proposal，经 Theseus 确认后方可执行；当前状态见 `../SESSION_LOG.md`。
- Roadmap 只负责阶段方向。G1 以及后续每个实际切片、必要时每个子切片，都必须先完成局部 Grill，再新增对应计划；本索引不会预先登记一个覆盖整个 G1 的大计划。

## 现有计划

- [2026-08-12 Marine Game 机枪兵 V2 卡牌单场战斗接入](2026-08-12-machine-gunner-v2-card-runtime-plan.md) — 当前 README web 的单场卡牌实施计划；Unstoppable 已验收，当前 82 Implemented / 0 CatalogOnly（V1 64/0、V2 18/0）
- [2026-08-06 STS2 v0.107.1 Ironclad 单人卡池接入](2026-08-06-sts2-v01071-ironclad-card-pool.md) — 以本机 public/main build 23811903 冻结 85 张单人卡；Juggernaut 已验收，当前 15 Implemented / 70 CatalogOnly
- [2026-08-05 M10 BattleScene MVP 对标、可靠性与内容扩展入口](2026-08-05-m10-battlescene-conformance.md) — 唯一实施计划已完成；M10 定向 25/25 通过，完整 EditMode 451 项中的两项非 M10 UI/Targeting 异常按交付时事实保留，不把未纳入本提交的后续 M9 测试校准计入 M10 通过口径
- [2026-08-05 M9 目标箭头与锁定框视觉反馈](2026-08-05-m9-targeting-visual-feedback.md) — 已实施并通过 Unity 定向验收；保留后续 UI 调参入口
- [2026-08-05 M9 出牌不飞向怪物](2026-08-05-play-card-no-target-flight.md)
- [2026-07-30 卡牌区域与确定性洗牌](2026-07-30-card-zones-deterministic-random.md)
- [2026-07-30 卡牌本地化文本与动态参数设计](2026-07-30-card-localized-text-design.md)
- [2026-07-30 战斗静态配置表](2026-07-30-battle-static-config-tables.md)
- [2026-07-30 BattleState 运行时参与者模型](2026-07-30-battle-runtime-state.md)
- [2026-07-12 BattleScene 基础手牌 UI](2026-07-12-battlescene-card-ui.md)

## 辅助审查（非实施计划）

- [2026-08-05 M9 代码结构与实现质量审查](2026-08-05-m9-code-structure-review.md) — 只读建议来源；当前 BUG 分诊与采纳边界见 `../06_testing/2026-08-05-m9-post-validation-bug-triage.md`

## 已替代 / 历史计划

- [2026-08-07 Marine Game 机枪兵卡牌单场战斗接入](2026-08-07-marine-game-card-only-integration.md) — 旧压缩包的 64 模板历史切片；已由 V2 计划取代
- [2026-08-06 机枪兵单场战斗内容接入](2026-08-06-machine-gunner-card-pool-integration.md) — 旧 JSON 的 MG0/MG1 历史计划；资源档案的已完成事实保留，卡牌内容及后续切片由 2026-08-07 计划取代

- [2026-07-30 BattleScene M3A 参与者视图与生命 HUD](2026-07-30-battlescene-participant-views.md) — M3A 历史方案已完成；其中 `view_prefab_address` / 完整角色 Prefab 地址已由 CD-055 的短键与逻辑地址标准取代
- [2026-07-30 YooAsset 到 Addressables 迁移](2026-07-30-addressables-migration.md) — 迁移已完成；其中“配置表 Unity 素材保留完整地址”和旧分步构建入口已由 CD-055 与根 `AGENTS.md` 取代，场景及 GameData 基础设施地址仍保留完整 `Assets/...`
- [2026-08-02 M9 STS 式反馈、胜负与重开](2026-08-02-m9-sts-feedback-outcome-restart.md) — M9A～M9G 已串行完成；最终自动验证、Bootstrap、真实 Game View、仓库外 Development Player 退出、范围审计与双轴复审见 `../06_testing/2026-08-02-m9g-full-validation-review.md`。配套 [Goal](2026-08-02-m9-sts-feedback-outcome-restart.goal.md) 与[启动 Prompt](2026-08-02-m9-sts-feedback-outcome-restart.codex-prompt.md)仅作历史交接记录。
- [2026-08-02 M8 敌人行动、状态时机与完整战斗循环](2026-08-02-m8-enemy-actions-status-timing-battle-loop.md) — M8A～M8E 已串行完成；最终自动验证、Bootstrap、真实 Game View 与双轴复审见 `../06_testing/2026-08-02-m8e-full-validation-review.md`。
- [2026-08-02 M7 Effect 执行器](2026-08-02-m7-effect-executor.md) — M7A～M7E 已串行完成；最终自动验证、Bootstrap、真实 Game View 与双轴复审见 `../06_testing/2026-08-02-m7e-full-validation-review.md`。
- [2026-08-01 M6 出牌命令、合法性与目标选择](2026-08-01-m6-card-play-legality-target-selection.md) — M6A～M6D 已串行完成，含目标失效零写入、真实 Game View、Addressables、Bootstrap 与双轴复审。
- [2026-08-01 M5 敌人意图与确定性行为选择](2026-08-01-m5-enemy-intents-deterministic-behavior.md) — M5A～M5D 已完成，含确定性复现、Game View 与双轴复审。
- [2026-07-31 M4 回合调度、权威命令队列与每玩家能量](2026-07-31-m4-turn-scheduling-energy.md) — M4A～M4E 已完成并通过全量验收。
- [2026-07-31 牌面短键与 Addressables 逻辑地址迁移](2026-07-31-card-illustration-logical-keys.md) — 已完成并通过逻辑地址加载验收。
- [2026-07-31 战斗 UI 首批美术与牌面配置链路接入](2026-07-31-battle-ui-art-integration.md) — 已完成并通过运行时验收。
- [2026-07-30 战斗配置接入运行时](2026-07-30-battle-config-runtime-integration.md) — 已由卡牌区域与确定性洗牌计划承接运行时牌区。
