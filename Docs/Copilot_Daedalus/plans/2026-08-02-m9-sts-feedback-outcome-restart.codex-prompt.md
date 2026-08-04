---
title: Codex 实施 Prompt · TinySpire BattleScene M9
page_type: handoff
lifecycle: archived
created: 2026-08-02
updated: 2026-08-05
companion_plan: 2026-08-02-m9-sts-feedback-outcome-restart.md
companion_goal: 2026-08-02-m9-sts-feedback-outcome-restart.goal.md
status_source: ../SESSION_LOG.md
---

# Codex 实施 Prompt · TinySpire BattleScene M9

> 历史交接页：M9 已完成并归档，本页保留当时的完整启动指令，不再作为新实施入口。最终验收见 `../06_testing/2026-08-02-m9g-full-validation-review.md`；唯一实施计划亦已归档。

```text
开始执行已创建的 TinySpire BattleScene M9 Goal。

唯一实施计划：
`Docs/Copilot_Daedalus/plans/2026-08-02-m9-sts-feedback-outcome-restart.md`

配套 Goal 原文：
`Docs/Copilot_Daedalus/plans/2026-08-02-m9-sts-feedback-outcome-restart.goal.md`

先不要改文件。完整重读根 `AGENTS.md`、唯一计划、`Docs/Copilot_Daedalus/ARCHITECTURE_CONVENTIONS.md`、`CODE_DECISIONS.md` 中 CD-027～CD-048、`DEPENDENCIES.md` 的 DEP-003/004/007/008/010/011/012、M6D/M7E/M8E 验收，以及 `Docs/Copilot_Daedalus/10_communication/2026-08-02-battle-card-motion-feedback-brief.md`。随后执行并回报：

1. `git rev-parse HEAD`
2. `git status --short`
3. 实际 tracked/untracked 基线、预期 M8 代码提交 `6545640963e3f184bcd7915706e87bea4a142afa` 是否一致
4. 当前唯一 Unity Editor/Play Mode/Console 状态
5. M9A 的首个红灯测试、预计精确文件范围与停止点

保护并排除这些用户改动：

- `Docs/Hermes_Pegasus/art/asset-index.md`
- `Docs/Hermes_Pegasus/art/assets/art-style/scenes/candidates/**`
- `TinySpire/Assets/Arts/Runtime/UI/Battle/Candidates.meta`
- `TinySpire/Assets/Arts/Runtime/UI/Battle/Candidates/**`

不要清理、回退、移动、引用或暂存它们。不要启动第二个 Unity Editor、结束用户 Unity/Git 进程、删除未授权锁，或清理 `Library` / `Temp`。

严格串行执行 M9A → M9B → M9C → M9D → M9E → M9F → M9G。每个切片都先测试红灯，再做最小实现，运行计划列明的定向/相关回归与串行 solution build；涉及 Prefab/可寻址 Scene 依赖时重建 Local Content；创建对应 `Docs/Copilot_Daedalus/06_testing/` 验收页并同步计划状态、索引和 `SESSION_LOG.md`，完成停止点后才继续。不要一次实现多个切片，也不要把计划中的最终验证提前写成已通过。

M9 只深化已有 `IBattleCommandPresentation.Present(result, completion)` 和 concrete `BattleCommandPresentationAdapter`。保持 `BattleCommandQueue.Submit`、只读 `Queue`/`Turn`、统一 coordinator、权威序号、Queued、非重入 drain、continuation FIFO、一次性 token、一次屏障、fault、玩家轮次栅栏、确定性意图、显式 Self/Enemy、共享 Effect 公式、不可变 settlement 与 `BattleEnded` 不变。每个命令最多有一个互斥的 StartBattle/PlayCard `CommandPrelude`；它不属于 settlement，完成后所有 settlement 派生步骤严格按 Order。禁止修改 Queue/Turn/settlement 契约、增加 settlement 事件总线/每类记录的 I* presenter/第二动画队列，或让动画回调、R3 subscriber、HUD、重开按钮写入任何权威战斗事实。

最终 HUD 从当前 Combatant/Intent/CardZones/Turn 即时派生；数字、抖动、轨迹、状态脉冲、死亡与横幅只消费当前冻结结果。表现期间仍允许既有合法玩家命令提交并由 Queue 排序，不得用 `IsWaitingForPresentation` 全局锁住输入；只有离手 ghost、战斗开始覆盖层、终局与场景按钮使用局部锁。PlayCard 只可用唯一 Hand→Discard 与首个可见 Effect 派生一个先于 Order 0 的 Prelude；记录本身不得重排。离手卡必须先退出可交互手牌集合，再作为非交互 transient visual 动画；合法 BeginDrag 命中入场卡时只快进该卡到当前权威 base pose并精确完成一次 cue，再开始正常拖拽，其他合法卡保持可用。完成/取消/场景销毁后清理，并以当前权威 Layout 为准。正常、加速、立即完成至多释放一次 completion；旧 Scope 销毁后不得有迟到 Tween/completion。

M9 的产品默认已经在计划中锁定，不要重新发明：StartBattle 先显示一次战斗开始覆盖层；回合横幅只在 PhaseBefore != PhaseAfter 且进入 PlayerAction/EnemyAction 时显示，EnemyAction→EnemyAction 的行动者交接不重播；重开同一 BattleScene、同一 Inspector encounter/seed；退出应用而不是返回不存在的 MainMenu；胜负只由现有终局规则派生；缺失 enemy banner/胜负装饰使用功能性 UGUI 和现有横幅 tint；不生成或接入 Candidates；七个正式文案使用 Unity Localization，并只在 M9F 修改 `DataTables/Datas/i18n.xlsx` 后运行 Luban、Localization 同步和 `TinySpire/Build/Sync and Build All`。

所有新增函数至少写中文注释。新增 Meta/Prefab 优先经当前 Unity MCP。同处 `Assembly-CSharp` 的表现 adapter 只允许在 BattleEnded 步骤临时调用 internal `BattleTerminalRules` 并立即映射文案；不得公开/注册该规则或保存 outcome，若必须跨程序集或新增 public API 立即停止。不得修改 `BattleScene.unity`、Battle domain 契约、其他 DataTables、生成战斗配置、GameData 战斗 JSON、ProjectSettings、asmdef、HybridCLR、启动/DI 架构、Run/网络/多人/奖励/主菜单；`BattleLifetimeScope` 预计无需修改，确需新 concrete hierarchy View 时最多一条必要注册并先按计划判断是否触发停止条件。

出现以下任一情况立即停止并报告预计文件、风险、回滚方式和所需确认：现有 settlement/只读事实不足；需要改 Queue/顺序/公式/目标/终局或公开 terminal API；需要保存第二份 Hand/CardZones/Combatant/Intent/outcome；重开需要 new seed/RunState；退出需要新 Scene/MainMenu；需要 Candidates/缺失正式美术；需要计划外配置、Scene、Prefab 或 DI；Luban/Localization/Addressables 产生计划外语义差异；Unity MCP、真实指针、连续时序、多宽高比、重开、仓库外 Development Player 退出或 Console 证据不足。

M9G 必须完成 M9 定向、M2～M8 回归、全量 EditMode、串行 solution build、最终 Addressables、Bootstrap、真实 Game View 战斗开始/回合、多轮卡牌/战斗时序、胜利、失败、连续重开、退出按钮、取消/立即完成、五种宽高比、Console、文档同步和 Standards/Spec 双轴复审。退出应用在 Editor 由薄 seam 自动测试加真实系统指针点击证明接线，另把 Development Player 构建到仓库外临时目录并真实点击退出、证明目标进程正常结束；不把 Editor no-op 冒充 OS 退出。时序用短录屏或连续帧加只读事实快照；单张截图不能冒充动画顺序，直接调用 listener 不能冒充真实系统指针。未经明确确认不 commit、不 push。
```
