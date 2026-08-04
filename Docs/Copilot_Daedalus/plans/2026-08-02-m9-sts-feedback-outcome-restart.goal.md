---
title: TinySpire BattleScene M9 · 可复制 Goal
page_type: handoff
lifecycle: archived
created: 2026-08-02
updated: 2026-08-05
companion_plan: 2026-08-02-m9-sts-feedback-outcome-restart.md
status_source: ../SESSION_LOG.md
---

# TinySpire BattleScene M9 · 可复制 `/goal`

> 历史交接页：M9 已完成并归档，本页保留当时的完整 Goal 授权原文，不再作为新实施入口。最终验收见 `../06_testing/2026-08-02-m9g-full-validation-review.md`；它仍不构成 commit 或 push 授权。

```text
/goal

完成 TinySpire BattleScene M9 · STS 式反馈、胜负与重开。严格以 `Docs/Copilot_Daedalus/plans/2026-08-02-m9-sts-feedback-outcome-restart.md` 为唯一实施计划，遵守根 `AGENTS.md`、`Docs/Copilot_Daedalus/ARCHITECTURE_CONVENTIONS.md`、现有 `CODE_DECISIONS.md` 以及计划中的表现顺序、一次 completion、取消清理、资源范围、停止点与验证要求，按 M9A → M9B → M9C → M9D → M9E → M9F → M9G 串行执行；每个切片完成对应 `06_testing/` 验收页、计划状态与 `SESSION_LOG.md` 同步后再继续。

复用 M4～M8 的 `BattleCommandQueue.Submit`、只读 `Queue`/`Turn`、统一 coordinator、权威序号、玩家轮次栅栏、确定性意图、显式 Self/Enemy 目标、`BattleEffectExecutor`、共享公式、不可变 `BattleSettlementRecord`、continuation、表现屏障、fault 与 `BattleEnded`。Queue 继续唯一拥有 Queued、非重入 drain、continuation FIFO、一次性 token、屏障和 fault；M9 只深化既有 `IBattleCommandPresentation.Present(result, completion)` 与 concrete `BattleCommandPresentationAdapter`，不得修改 Queue/Turn/settlement 契约、增加第二 completion/事件总线/动画命令队列，或让 Tween/R3/UI 写 Health、Block、Status、Intent、CardZones、Turn、终局和 outcome。

M9A 建立不可变表现计划：每命令最多一个互斥的 StartBattle/PlayCard `CommandPrelude`，随后 settlement 派生步骤严格按 Order；同时完成零可见直通、加速/立即完成、至多一次 completion 与场景销毁清理。M9B 完成 Block、Strength、Vulnerable 常驻 HUD 并保留既有意图共享公式，不抢先隐藏 0 HP 世界 View/生命 HUD；M9C 完成伤害/格挡数字、状态/意图脉冲、实际生命损失抖动与 fatal 死亡过渡；M9D 完成 Disabled/VisualOnly/Playable 样式、Enemy 越线 focus anchor，并把 Runtime/Targeting 四张正式 PNG 接入箭头和合法/悬停高亮；M9E 以非交互 transient View 消费 `CardMoved`/`CardsReshuffled`，完成出牌、结束行动弃牌、抽牌与重洗动画且不伪造手牌；M9F 播放一次战斗开始覆盖层，只在 PhaseBefore != PhaseAfter 且进入 PlayerAction/EnemyAction 时播放回合横幅，在全部前序反馈后从现有终局规则派生胜负面板，加入七个正式本地化文案，重开同一 BattleScene/同一 Inspector seed，并把退出固定为退出应用；M9G 完成全量验证、文档和双轴复审。

表现期间继续允许既有合法玩家意图提交并由 Queue 排序，不得用 `IsWaitingForPresentation` 全局锁输入；只有离手 ghost、战斗开始覆盖层、终局战斗输入和场景切换按钮使用局部表现锁，覆盖层只阻断系统指针。失败/skip 不得伪造反馈；最终 HUD 只读当前事实，一次性数字、抖动、轨迹、死亡与横幅只读当前冻结结果。PlayCard 只能从唯一 Hand→Discard 与首个可见 Effect 派生一个先于 Order 0 的非权威 Prelude，随后 Energy/Effect/CardMoved 仍严格按 Order；合法 BeginDrag 命中入场卡时只快进该卡到当前权威 base pose并精确完成一次 cue，再正常拖拽，其他合法卡保持可用。胜负不保存镜像，死亡 View 不删除权威参与者，重开不引入 RunState 或新随机策略。当前只显示 Strength/Vulnerable，不因已有 Weak/Poison 图片实现新状态；卡牌运动不得按卡名、模板 ID 或 EffectType 复制规则/公式。

允许修改计划列明的 `TinySpire/Assets/Scripts/UI/Battle/**`、M9 Editor 测试、`BattleHandUI.prefab`、`BattleTargetingArrow.prefab`、`BattleTurnHud.prefab`、`ParticipantHudView.prefab`、一个必要飘字 Prefab，以及仅引用现有正式 Runtime Battle UI 资源；`BattleLifetimeScope` 预计不改，确需时最多一条必要的 hierarchy concrete 注册。M9F 只允许修改 `DataTables/Datas/i18n.xlsx` 及由既有工具生成的对应 Localization/Addressables 内容，并必须执行 Luban、Localization 同步与 `TinySpire/Build/Sync and Build All`。同处 `Assembly-CSharp` 的表现 adapter 只允许在 BattleEnded 步骤临时调用 internal `BattleTerminalRules`，立即映射文案；这不是 public/DI seam，也不保存 outcome，若需要公开 API 或跨程序集访问必须停止。不得修改 Battle domain 契约、其他 DataTables/生成战斗配置/GameData、`BattleScene.unity`、ProjectSettings、asmdef、HybridCLR、启动/DI 架构、Run/网络/多人/奖励/主菜单；不得实现 Weak、Poison、Dexterity、遗物、触发器、DSL、新 Effect、新目标、多/随机/链式目标、Exhaust 或命令中途选择。

开始前完整重读规则和计划，执行 `git status --short` 与 `git rev-parse HEAD`，记录实际起始 HEAD 与全部 tracked/untracked 改动。当前预期代码基线为 M8 提交 `6545640963e3f184bcd7915706e87bea4a142afa`；必须保护 `Docs/Hermes_Pegasus/art/asset-index.md`、`Docs/Hermes_Pegasus/art/assets/art-style/scenes/candidates/**`、`TinySpire/Assets/Arts/Runtime/UI/Battle/Candidates.meta` 与 `TinySpire/Assets/Arts/Runtime/UI/Battle/Candidates/**`，不修改、不引用、不暂存、不回退。新增 Meta/Prefab 优先经当前唯一 Unity MCP；不得启动第二 Editor、结束用户 Unity/Git 进程、删除未授权锁或清理 Library/Temp。

每个 Prefab/可寻址依赖切片完成后重建 Local Content。最终运行 M9 定向、M2～M8 回归、全量 EditMode、串行 solution build、最终 Addressables、Bootstrap、真实 Game View 战斗开始/回合/胜利/失败/连续重开/退出按钮/动画取消、多轮卡区运动、16:7/16:9/16:10/16:11/16:14 真实系统指针验收、Console 与 Standards/Spec 双轴复审；退出应用在 Editor 由薄 seam 自动测试加真实按钮点击证明接线，另把 Development Player 构建到仓库外临时目录并以真实按钮点击证明目标进程正常结束，不把 Editor no-op 冒充 OS 退出。动画顺序使用短录屏或连续帧加只读事实证据，截图/直接 listener 不得冒充物理时序或交互。若需要修改 Queue/settlement/公式/目标/终局、公开 terminal API、保存第二份权威事实、引入 MainMenu/Run/new seed、使用 Candidates/缺失美术、扩大到计划外配置/Scene/Prefab/DI，或 Unity MCP、Player 退出与其他真实证据不足，立即停止并请求确认。未经明确确认不 commit、不 push。
```
