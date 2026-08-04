---
title: M9C 结算反馈、受击与死亡过渡
page_type: testing
lifecycle: active
date: 2026-08-02
status: passed
scope: 冻结 settlement 反馈、纯字符飘字、受击/状态/意图/死亡、启动 readiness、Prefab、Addressables、Bootstrap 与范围审计
plan: ../plans/2026-08-02-m9-sts-feedback-outcome-restart.md
status_source: ../SESSION_LOG.md
---

# M9C 结算反馈、受击与死亡过渡

## 当前结论

M9C 已通过。`BattleCommandPresentationAdapter` 继续只消费 M9A 的不可变计划；`BattleCombatFeedbackTweenFactory` 把冻结的 Damage、Block、Attribute、Status 与 Intent 步骤路由到 `BattleParticipantPresenter` 的唯一 `CombatantId → world View / ParticipantHudView` 映射。Block 吸收、实际生命损失、Block 增加、对应图标脉冲、实际生命损失抖动和 fatal 死亡过渡均按计划顺序播放，没有从最终 HUD 差值猜测一次性反馈。

经用户 2026-08-03 明确确认，飘字采用纯字符 `-N / +N` 与颜色区分，不接伤害数字底板；未读取或引用 Candidates。启动 readiness 只读当前 Session 与 Presenter 映射，在映射未齐时局部关闭 End Action 与 Hand 系统指针入口；它没有接入 Queue、`IsWaitingForPresentation`、第二 completion、事件总线或权威事实。

## 测试先行与红绿证据

| 契约 | 红灯 | 最小实现 | 最终绿灯 |
|---|---|---|---|
| 冻结伤害/格挡/状态/意图路由 | 首批 factory、HUD 与飘字测试在缺少 concrete cue factory、反馈锚点、飘字 View 和死亡过渡时失败 | 新增一个 concrete factory 与一个通用 transient 飘字 View；所有数值只取冻结步骤，精确路由到当前目标 | 进入最终 M9C 聚焦 **96/96 passed** |
| readiness 局部系统指针门禁 | concrete Turn HUD / Hand 测试先证明映射未齐时按钮和卡牌仍可接收系统指针 | Presenter 从当前 Session、world View 与 HUD 字典即时派生 readiness；Turn/Hand 只在现有入口二次检查 | 世界 View/HUD 缺一、对象销毁、加载失败、迟到完成与完整恢复均进入最终聚焦集 |
| 入场/回流卡同帧取消 | `HandleBeginDrag` concrete 测试发现 `SetBasePoseImmediately` 后仍残留一个 Hand tween，任务 `229b8251c1b44c69bdf301b53d940de5` 为 `Expected 0, But was 1` | `HandCardVisual` 对自己拥有的默认 AutoKill tween 使用 `Complete(false)` 同步回收，再按当前权威 base pose 开始合法拖拽 | 任务 `9cf4e090f5c749bfba596d6daddb06f1`，1/1 passed |
| owner 销毁不留父时间线 | 新增 `Dispose_ActiveTimeline_ReleasesParentSequenceInSameFrame`；任务 `85a12b64def442698a111a89e8879074` 为 `Expected 0, But was 1` | 每次 playback 给父 Sequence 独立私有 ID；自然结束、立即完成、构建异常与 Dispose 全部按该 ID 精确 Kill | 任务 `c1060cf979eb46d69521983c57a488ee`，1/1 passed；Runner 最终 12/12，测试后 `active=0 / playing=0` |
| 测试夹具资源所有权 | 统一回归后发现直接调用 cue 工厂的测试遗留 `active=26 / playing=16`，进入 Play 会制造假 Console 噪声 | 测试 orphan 使用夹具私有 ID；默认 AutoKill tween 用 `Complete(false)`；手动父时间线逐条唯一 ID，禁止生产 `KillAll` | 四个受影响测试类 **31/31 passed**，随后 `active=0 / playing=0`；最终统一回归同样为 0/0 |

## 冻结反馈与严格顺序

- `DamageApplied` 只使用同一冻结记录的 `blockBefore/After`、`healthBefore/After`：先 Block 吸收数字，再实际 HealthLoss 数字；只有 `healthBefore - healthAfter > 0` 才抖动，fatal 最后播放死亡过渡。
- 全格挡只显示实际吸收量，不显示生命损失或抖动；格挡溢出分别显示实际吸收量与实际生命损失；普通及 Vulnerable 伤害不在表现层重算公式。
- `BlockGained` 只显示 `blockAfter - blockBefore`；Strength、Vulnerable 施加/衰减与 Enemy Intent 前进只脉冲冻结记录指定参与者的既有图标根。
- 多参与者、多记录继续由 M9A 父 runner 严格串行；M9C factory 不消费 CardMoved、横幅或 BattleOutcome，也不重排 settlement `Order`。
- skipped、普通失败和不属于 M9C 的步骤不创建反馈；缺失精确映射保持现有同步 presentation fault，不伪造成 skip、成功或 completion。

## 常驻 HUD、fatal 与权威事实

- 常驻 Health、Block、Strength、Vulnerable 与 Intent 仍从当前 Combatant/Intent 即时投影；一次性数字、抖动、脉冲与死亡只读当前冻结结果。
- fatal 过渡完成前，0 HP 的 world View 与完整 HUD 保持可见；过渡完成后只隐藏对应 View/HUD。重新绑定到已经死亡的同一权威参与者时直接恢复隐藏终态。
- 死亡 View 不删除 `CombatantData`，不改变 Encounter 顺序、Intent Layout、CardZones、Turn 或 outcome；另一敌人的映射、View/HUD 与意图不受影响。
- M9C 结束时没有胜负面板。运行期对象查询中 outcome/terminal/victory/defeat 匹配为 0；可见终局和文案仍严格留给 M9F。

## readiness 与输入边界

- `BattleParticipantPresenter.IsPresentationReady` 只在当前 Session 的每名参与者都拥有仍存活的唯一 world View 与 HUD 映射时为 true；不存在计时器、缓存 outcome 或第二份参与者集合。
- `BattleTurnHudView` 与 `HandCardContainer` 只在系统指针入口读取 readiness。映射齐全后仍继续使用既有阶段、费用、目标、pending 与 fault 合法性；表现等待期间其他合法玩家命令仍由 Queue 排序。
- 直接 `BattleCommandQueue.Submit` 没有 readiness 分支。调用方若绕过局部门禁而 concrete 表现目标确实缺失，仍保留既有 post-write presentation fault 语义，不伪造零反馈直通。
- concrete End Action Button 验证 `false → true → false`，且 programmatic listener 不写 Queue/Turn；concrete Hand Card 验证 CanvasGroup/raycast `false → true`、合法 `HandleBeginDrag`，随后映射销毁会立即取消当前拖拽并保持 Queue/CardZones 零变化。
- 地址加载失败、部分构建、Presenter/Scene 销毁与迟到加载完成均走幂等清理；旧 Scope 不会迟到解锁、播放 Tween 或调用 completion。

## 纯字符 Prefab 与清理

- 新增 `Assets/Prefabs/UI/Battle/BattleFloatingNumberView.prefab`，只包含非交互 Text、Outline 与 CanvasGroup；三类反馈以纯字符和颜色区分，不含 Image/backplate，`raycastTarget=false`、`blocksRaycasts=false`。
- `ParticipantHudView.prefab` 只新增 `FeedbackAnchor` 与序列化引用；没有修改 BattleScene、角色 Prefab、Targeting Prefab 或 `BattleLifetimeScope`。
- 正常、加速、立即完成、构建异常、局部取消、owner/Scene 销毁均精确清理 transient 与父 Sequence；completion 最多一次，取消和销毁不伪装正常完成。
- 最终 Prefab 空值序列化规范化后，`ParticipantHudPrefabContractTests` 再次 **8/8 passed**（任务 `1c9ef09cf34049b681c0091fae3a9e14`），并通过 `TinySpire/Addressables/Build Local Content` 重建；`Library/com.unity.addressables/aa/Windows/settings.json` 的最终时间为 **2026-08-03 07:53:53**。

## 最终自动验证与静态构建

| 检查 | 结果 |
|---|---|
| M9C Plan / Runner / Adapter、反馈 factory、Presenter routing、HUD/Prefab/View 与 release resolver 聚焦 | **96/96 passed，0 failed，0 skipped**；任务 `1edb43696c294fd6aef3cddb7d9cd886` |
| M9A～M9C、Combatants/Effect/Status/Intent/M8D、targeting 与 Hand/card transition 统一相关回归 | **239/239 passed，0 failed，0 skipped**；任务 `aea498d7fb544681ba3c5a810ca85656` |
| M8B Queue 普通失败、presentation fault、一次屏障与旧 completion 隔离 | **11/11 passed，0 failed，0 skipped**；任务 `2ae8e6a13a3d4094ba9ee552a9ca65c2` |
| 最终 Participant HUD Prefab 合约 | **8/8 passed，0 failed，0 skipped**；任务 `1c9ef09cf34049b681c0091fae3a9e14` |
| 测试域 Tween 所有权 | 最终聚焦/统一回归后均为 `active=0 / playing=0` |
| `dotnet build TinySpire/TinySpire.sln --no-restore -m:1` | **0 error，12 warnings**；均为既有 Unity / R3 / UniTask 依赖程序集版本冲突 |
| Unity Console | 最终 Local Content 后 Bootstrap 实跑 Error / Warning 为 **0/0** |

## 连续时序与生产 Bootstrap 证据

系统指针验收采用用户明确授权的 InputSystem/EventSystem 跨帧注入；它不是 OS 物理鼠标，也没有用直接 listener 冒充。实际 `EventSystem` 命中 End Action Button 的 `targetGraphic` 中心 `(1838, 540)` 后，Queue 产生权威命令并进入真实反馈时间线。

- 同一 `HealthLossNumber_6` 对象的连续事实：frame 114962 `alpha=1.000, y=0.39`；frame 115200 `alpha=0.650, y=44.74`；frame 115315 `alpha=0.248, y=47.89`。三帧期间 Health 固定为 18，Queue 保持当前命令表现屏障，证明是同一冻结反馈而非最终 HUD 差值。
- 连续帧图像：`TinySpire/Temp/CodexEvidence/M9C/m9c-sequence2-00-start.png`、`m9c-sequence2-01-mid.png`、`m9c-sequence2-02-late.png`。截图只作画面佐证，顺序以同对象连续帧和只读事实为准。
- 受击抖动同一 world View 的最大临时偏移幅度为 `0.119795`；fatal settled 时 `health=0, alive=false, phase=BattleEnded, authority=none, pending=0, waiting=false, faulted=false`，对应 world View/HUD 均隐藏且 `activeNumbers=0`。证据为 `m9c-sequence3-03-shake.png` 与 `m9c-sequence3-04-fatal-settled.png`。
- 首帧 readiness recorder 观察到 frame 244 `ready=false / button=false`，frame 266 `ready=true / button=false`，frame 267 `ready=true / button=true`；三帧 Queue 均无 current、pending 或 fault，不使用固定延迟解锁。
- 最终 Bootstrap 从 `BootstrapScene → BattleScene` 后只读快照为 `ready=True, views=3, huds=3, endAction=True, phase=PlayerAction, authority=none, pending=0, waiting=False, faulted=False, activeTweens=0, playingTweens=0`；退出 Play 后恢复 `BootstrapScene`。

## 范围与工作区保护

M9C authored 生产范围只位于：

- `TinySpire/Assets/Scripts/UI/Battle/**` 内的既有 adapter、Presenter、Participant HUD、Turn HUD、Hand/Targeting readiness 接线及 `Feedback/**`；
- `TinySpire/Assets/Arts/Runtime/Prefabs/ParticipantHudView.prefab`；
- `TinySpire/Assets/Prefabs/UI/Battle/BattleFloatingNumberView.prefab/.meta`；
- 对应 M9 Editor 测试与 Meta。

没有修改 Queue、Turn、settlement、Combatant、Intent、CardZones、Effect 公式/执行、目标规则、终局规则、`BattleLifetimeScope`、BattleScene、其他 DataTables、Localization、生成战斗配置、GameData、ProjectSettings、asmdef、HybridCLR、启动 / DI / Run / 网络 / 多人架构。受保护 Hermes/Candidates 四组用户改动未读取为资源、未引用、未修改、未回退、未移动、未清理、未暂存。未 commit、未 push。

## 停止点判定与后续

全格挡、格挡溢出、普通/易伤伤害、Defend、Strength、Vulnerable、Intent、fatal、skipped、普通失败、fault、严格 Order、精确映射、readiness、一次 completion、立即完成、取消和销毁清理均有自动或连续时序证据；聚焦/相关回归、串行 build、Prefab、最终 Local Content、Bootstrap 与 Console 已完成。

M9C 停止点完成。下一步只进入 M9D · 不可用样式、目标聚焦与正式目标素材；M9E～M9G、胜负面板、本地化、重开、退出应用、五宽高比和全量最终复审仍保持待实施。
