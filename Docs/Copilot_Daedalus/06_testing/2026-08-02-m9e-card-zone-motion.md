---
title: M9E 出牌、弃牌、抽牌与重洗运动
page_type: testing
lifecycle: active
date: 2026-08-02
status: passed
scope: PlayCard Prelude、CardMoved、CardsReshuffled、transient card、入场快进、取消、Addressables、Bootstrap 与范围审计
plan: ../plans/2026-08-02-m9-sts-feedback-outcome-restart.md
status_source: ../SESSION_LOG.md
---

# M9E 出牌、弃牌、抽牌与重洗运动

## 当前结论

M9E 已通过。既有 `BattleCommandPresentationAdapter` 现在把 PlayCard Prelude、`CardMoved` 与 `CardsReshuffled` 都交给 M9A 的同一个 presentation runner；每条命令仍只有一个 completion、一个表现屏障和原 settlement `Order`。PlayCard 只从冻结结果中的唯一 Hand→Discard 与首个可见 Effect 派生一次 Prelude，随后 Energy、Effect 与 CardMoved 没有重排。

离手卡在权威 Layout 发布后立即退出可交互 `_cards`，以同一张非交互 transient visual 完成目标/弃牌轨迹；Draw→Hand 只移动当前权威 Hand 中已经建立的真实 View；重洗只创建一个非交互纯字符 `↻`。这些对象不保存 Hand、CardZones、Combatant、Intent、Turn 或 outcome，完成、立即完成、取消、owner/Scene 销毁后均从当前 Layout 收口。

真实交互证据使用用户授权的跨帧 `InputSystemUIInputModule → EventSystem raycast → BeginDrag / Drag / EndDrag` 输入链，不直接调用 listener 或 Container 入口。截图不承担时序结论；卡区运动、ghost 生命周期和 incoming fast-forward 由连续帧及只读 Queue/CardZones/View 快照证明。

## 测试先行与红绿证据

| 契约 | 红灯 | 最小实现 | 最终绿灯 |
|---|---|---|---|
| PlayCard Prelude 与 settlement 顺序 | 新测试先要求 Prelude 严格早于 Order 0，旧 adapter 没有 card motion cue | `BattleCardMotionTweenFactory` 只按 frozen command/record 类型产生 cue，runner 保留原步骤顺序 | `Play_PlayCardPreludeRunsBeforeOrderZeroAndCardMovedAtItsOwnOrder` 与 Adapter 顺序测试进入最终 88 项聚焦回归 |
| Hand→Discard transient | 旧 `RebuildCards` 会在 Layout 发布时直接销毁离手 View，无法表现且没有可验证清理租约 | Container 先关闭交互、pending、targeting 与 raycast，再把同一 View 移入 transient 字典；Prelude 与 CardMoved 复用并精确销毁一次 | detach、复用、目标/弃牌轨迹及 owner 销毁测试通过 |
| Draw→Hand 与 StartBattle readiness | 初始 Draw settlement 到达时真实 Hand View 尚未建好；首次生产 Bootstrap 出现 cue 取不到 View 的 fault | Adapter 懒解析 hierarchy Hand；runner 用同一 frozen playback 的只读 readiness gate 延迟 cue 构造，不创建第二 completion 或 Queue seam | `Play_StartGateDefersCueConstructionThenCompletesExactlyOnce`、`Dispose_StartGatePending_DropsCompletionWithoutLateCue` 及干净 Bootstrap 通过 |
| 重洗 transient | 新测试先要求单个可释放、不可交互的重洗 View，旧 pile HUD 只有计数 | Pile HUD 暴露当前锚点，按 cue 播放一个纯字符 `↻`；CanvasGroup 与文字 raycast 均关闭 | 单例、非交互、顺序、立即完成和中途 Dispose 测试通过 |
| incoming BeginDrag 精确快进 | 新测试先要求只完成命中的 active cue 一次，旧 View 没有 cue token/快进入口 | View 持有一次性请求；合法 BeginDrag 先回到最新权威 base pose并让 runner 的精确 cue token完成，然后继续既有拖拽 | 自动 token 测试与真实 EventSystem 输入均为目标 cue `0→1`，另一合法卡不受影响 |
| 取消与迟到回调 | 中途销毁分别会留下 reshuffle、incoming 或 ghost 的活动状态 | runner/lease 与 View owner 在 Dispose/OnDestroy 同帧 Kill 并丢弃 completion，以当前 Layout 恢复 | 三条具体中途销毁测试均通过，completion 不迟到、transient 为 0 |

## 冻结记录与严格顺序

- PlayCard：`CommandPrelude` 先使用唯一 Hand→Discard 卡牌身份与首个可见 Effect 目标；Prelude 后仍按原 Order 播放 EnergySpent、各 Effect 与 CardMoved。运动代码不读取卡名、模板 ID 或 EffectType，也不复制合法性、目标或公式。
- End Action：同一结果内多张 Hand→Discard 依照冻结 `CardMoved.Order` 逐张生成 cue，runner 未另建动画队列；每张离手卡只拥有自己的 transient lease。
- StartBattle/下一轮：DrawPile→Hand 读取 frozen record 的卡 ID，但只操作当前权威 Hand 中同 ID 的 View；真实 Hand 的实例、顺序和数量继续由当前 Layout 决定。
- 重洗：`CardsReshuffled` cue 严格先于随后更大 Order 的 Draw→Hand；`↻` 只表现冻结的新抽牌顺序发生过，不写入或替代该顺序。

Adapter 测试分别锁定 `Present_EndActionMultipleCards_PreservesSettlementMotionAndCleanupOrder`、`Present_ReshuffleThenDraw_PreservesFrozenSettlementOrderAndCurrentHand` 和非法 Order 在取得任何表现所有权前抛错。没有改变 settlement 类型、记录顺序、Queue drain、continuation FIFO 或 fault 契约。

## 权威手牌、transient 与非交互

- 离手 View 先从 `_cards` 移除，再关闭 `CanvasGroup.interactable/blocksRaycasts`、卡牌 Graphic raycast、命令 pending、focus、箭头与高亮；之后才作为 transient visual 播放。
- 真实 ghost 中心的 `EventSystem.RaycastAll` 无卡牌命中；CanvasGroup 为 `interactable=false / blocksRaycasts=false`。它不能 BeginDrag、Submit 或成为目标。
- EnemyAction 连续帧中权威 Hand、可交互 `_cards` 都是 0；只有 settlement 拥有的非交互 ghost 可以短暂存在。进入下一 PlayerAction 后，View IDs/order/count 与新权威 Hand 完全一致。
- 重洗 `↻` 的文字 `raycastTarget=false`，CanvasGroup 同样不交互；中心射线不会产生 authority sequence。完成后对象销毁且 pile HUD 继续只显示当前计数。

## 四张正式卡与多轮生产证据

Strike、Defend、Bash 均从 Bootstrap 的正式牌组经真实 CardContent raycast、跨帧拖拽和 Queue Submit 播放同一 Prelude→Effect→CardMoved 路径；权威伤害、Block、Vulnerable、Energy、Hand 与 Discard 继续由 M7/M8 规则写入。代表性 Defend 事实为 Hand `5→4`、Discard `5→6`、Block `0→5`、Energy `3→2`、authority sequence `14`，释放帧 `waiting=true`，完成后 Queue idle、ghost 清零。

默认 encounter 牌组没有 Strength。为覆盖第四张正式卡，Play Mode 内只读验证前使用与 M7E 相同性质的短生命周期运行期夹具：把一张当局卡实例临时指向现有正式模板 `3001`，加载其正式 Addressable 牌面并重建当前 View；未修改 DataTables、GameData、Prefab 或项目文件。随后真实事件链顶层命中 `CardContent`，Strength `0→3`、Energy `3→3`、Hand `5→4`、Discard `5→6`；释放帧 authority sequence `5`、`waiting=true`、transient `1`，最终 Queue idle/fault none、transient `0`。退出 Play Mode 后夹具随当局 Session 销毁。

真实 End Action 命中 `Button.targetGraphic` 的 Icon，并经 Button listener→coordinator→Queue 提交；多张离手 ghost 按 settlement 顺序出现和清理。敌人行动完成后下一轮抽牌进入当前 base pose；多轮直到 Discard→Draw reshuffle 时，单个 `↻` 严格先于后续 Draw→Hand，最终 Hand IDs/order/count 与权威 Layout 一致。

## incoming 卡与表现期间输入

Round 5 的冻结样本为 `phase=PlayerAction / authority idle / pending=0 / waiting=true / fault=none`，Hand 与 View 都为 `[10,4,2,7,3]`。目标 incoming 卡 A=`10` 被真实 CardContent raycast 命中：press 后 PointerModel 的 press/drag/current 都指向 CardContent，移动帧进入真实 BeginDrag；A 立刻回到最新 base pose，incoming 标志清除，请求被消费，runner 的 `_fastForwardedCues.Count` 精确 `0→1`，Hand、Energy、Queue 序号与 pending 不变，下一张 cue 继续播放。

同一运行段中，另一张合法卡 B=`9` 可先被实际 BeginDrag/Drag/EndDrag；A 仍保持 incoming，fast-forward `0→0`，权威事实不变。为稳定捕获 A 的移动 Graphic，证据 harness 曾暂时把 `Time.timeScale` 设为 0 只冻结非权威 hover Tween 几何；InputSystem/EventSystem 仍逐帧处理，动作完成后立即恢复为 1。该操作没有进入生产代码或保存事实。最终 `waiting=false / pending=0 / fault=none / views=authority / transients=0 / timeScale=1`。

这证明表现屏障期间没有使用 `IsWaitingForPresentation` 全局锁手牌；只有目标卡自身的 incoming cue 被局部快进一次，其他合法卡继续可用。

## 立即完成、取消与 Scene 生命周期

- `CardsReshuffled_RunnerDisposeMidFlight_CleansVisualWithoutLateCompletion`：中途 Dispose 后 `↻` 同帧销毁，旧 completion 不回调。
- `DrawToHand_RunnerDisposeMidFlight_RestoresLatestBasePoseWithoutLateCompletion`：只恢复当前 Layout 的最新 base pose，不保留 draw 起点或迟到 cue。
- `HandToDiscard_OwnerDestroyedMidFlight_CleansGhostWithoutLateCompletion`：owner 销毁后 ghost 清空，不把离手卡重新伪造成 Hand。
- runner 的自然、加速、立即完成、重复控制和 active Dispose 继续由 M9A 测试证明至多一次 completion；M9E cue lease 只接入同一边界。
- StartBattle gate 尚未构造 cue 时销毁，同样丢弃旧 completion；新 Scope 不会收到上一场的 Tween 或回调。

## 最终自动验证与静态构建

| 检查 | 结果 |
|---|---|
| M9E factory/pile/hand/adapter/runner/plan 与相关 View 聚焦 | **88/88 passed，0 failed，0 skipped**；任务 `cf327d4aeb0e4ff0b9614bc3d00aa236` |
| CardZones、Effect Queue、Hand/transition、Pile HUD、M7/M8 stage-record/Queue 相关回归 | **166/166 passed，0 failed，0 skipped**；任务 `01ae9015550d4e2b90be7bd991f14124` |
| `dotnet build TinySpire/TinySpire.sln --no-restore -m:1` | **0 error，12 warnings**；均为既有 Unity / R3 / UniTask 依赖程序集版本冲突 |
| Local Content | 最终执行 `TinySpire/Addressables/Build Local Content` 成功，耗时 **8.88s**；输出 `Library/com.unity.addressables/aa/Windows/settings.json` |
| Bootstrap / Console | `PlayerAction / round 1 / authority idle / waiting=false / pending=0 / fault=none`；Hand 与 View 都为 `[1,10,7,6,2]`，transient `0`；干净运行及退出 Play 后 Console Error / Warning **0/0** |

`git diff --check` 只报告 M9B～M9D 已记录的 Unity YAML 空 `m_Name: ` 尾空格：`ParticipantHudView.prefab` 13 处、`BattleHandUI.prefab` 1 处、`BattleTargetingArrow.prefab` 3 处；M9E 新增/修改的 C#、测试、Meta 与本验收页没有新增尾随空白。HEAD 仍为 M8 基线 `6545640963e3f184bcd7915706e87bea4a142afa`。

## 范围与工作区保护

M9E 生产改动只位于：

- `TinySpire/Assets/Scripts/UI/Battle/Feedback/BattleCardMotionTweenFactory.cs` 与 Meta；
- `TinySpire/Assets/Scripts/UI/Battle/Feedback/BattleCommandPresentationRunner.cs`；
- `TinySpire/Assets/Scripts/UI/Battle/BattleCommandPresentationAdapter.cs`、`BattleCardPileHudView.cs`、`BattleParticipantPresenter.cs`；
- `TinySpire/Assets/Scripts/UI/Battle/Hand/HandCardContainer.cs`、`HandCardVisual.cs`；
- 对应 `TinySpire/Assets/Editor/Tests/` M9E 测试与 Meta。

没有修改 Queue、Turn、settlement、Combatant、Intent、CardZones、Effect 公式/执行、目标规则、终局规则、`BattleLifetimeScope`、BattleScene、Prefab、DataTables、Localization、GameData、ProjectSettings、asmdef、HybridCLR、启动/DI/Run/网络/多人。`packages-lock.json` 的外部 hash 差异继续排除；受保护 Hermes/Candidates 改动未读取、引用、修改、回退、移动、清理或暂存。未 commit、未 push。

## 停止点判定与后续

四张正式卡、PlayCard Prelude、End Action 多牌、EnemyAction 无旧交互手牌、下一轮抽牌、重洗、ghost/`↻` 非交互、incoming 目标卡精确一次快进、其他合法卡继续可用、立即完成、取消和 owner/Scene 销毁均已有自动或生产证据。最新定向/相关回归、串行 build、Local Content、Bootstrap 与 Console 均通过。

M9E 停止点完成，`DEP-004` 改为 resolved。下一步只进入 M9F · 阶段横幅、胜负面板、重开与退出；M9F/M9G、M3E 与整个 M9 仍保持待完成。
