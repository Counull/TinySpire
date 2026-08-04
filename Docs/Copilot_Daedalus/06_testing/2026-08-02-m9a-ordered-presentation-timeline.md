---
title: M9A 有序表现时间线、一次 completion 与取消
page_type: testing
lifecycle: active
date: 2026-08-02
status: passed
scope: 表现计划、runner、adapter、M8 Queue 回归、静态构建与范围审计
plan: ../plans/2026-08-02-m9-sts-feedback-outcome-restart.md
status_source: ../SESSION_LOG.md
---

# M9A 有序表现时间线、一次 completion 与取消

## 当前结论

M9A 已通过。现有 `IBattleCommandPresentation.Present(result, completion)` 与 concrete `BattleCommandPresentationAdapter` 已深化为一个不可变命令级表现计划和一个串行 runner：每条命令至多一个互斥的 StartBattle / PlayCard `CommandPrelude`，随后逐条保留全部 settlement，并只按原始 `Order` 与同记录稳定子序播放可见步骤。Queue-facing seam、Queue / Turn / settlement 契约、continuation、屏障、fault、公式、目标和终局规则均未修改。

本切片建立的是后续 concrete View 的顺序、一次完成与取消基础，不把计时用空 Tween 冒充 M9B～M9F 的生产 HUD、飘字、卡牌运动、横幅或终局表现。

## 测试先行与红绿证据

| 契约 | 红灯测试或失败信号 | 红灯任务 | 最小实现 | 绿灯任务与结果 |
|---|---|---|---|---|
| 非可见 settlement 立即直通 | `NonVisibleSettlementResult_CompletesSynchronouslyWithoutFixedDelay` 首次得到 completion `0` 而非 `1` | `2298d7304528448ca34a10ff574f4bb4` | 先移除旧的固定结果等待特判 | `1c62d10a9f0441d58e4e9aa826bb7af0`，1/1 |
| Operation skip 不伪造等待 | `NonVisibleOperationSkippedResult_CompletesSynchronouslyWithoutFixedDelay` 首次红灯 | `687016c106344555bf349048210179b5` | 扩充零可见直通证据，随后收敛到统一 plan | `3fadca26ba91440bb477b0f9252ce1d5`，2/2 |
| StartBattle / Strike / Bash 的 Prelude 与 Order | 首个 planner 测试阶段因 `BattleCommandPresentationPlan`、Prelude 与 Step 类型尚不存在而编译红灯 | Unity 编译红灯 | 建立不可变 plan；StartBattle 与 PlayCard 只由命令类型、唯一离手记录和首个可见 Effect 派生 | StartBattle `47129271893f422b9db2dce913be432a`；StartBattle + Strike `c4be2d113c4747898ee19ca12e12c61e`；Bash suite `8641e53c906340b58db3c5361447cd9b` |
| 唯一 Hand→Discard | 多条离手记录未抛错 | `d9259f580dba473bbc352fd6bb1634db` | 第二条 Hand→Discard 同步拒绝，不静默选卡 | `90f124e165d84e47b5fb5b11adebbc0c`，6/6 |
| 14 类 concrete settlement 显式映射 | Damage、Block、Strength、Vulnerable、Card route / reshuffle、Intent、Phase / outcome 的缺失 enum 或运行时断言逐项红灯 | `e05d829168014b358207381561fe12ae` 等逐项红灯 | 每类明确派生零到多个步骤；未知 concrete 类型同步拒绝 | 最终 planner `c81b6e7b30cd4bf4893645516a55d00d`，16/16 |
| 未知类型与 BattleEnded 尾序 | 未知 concrete 被静默跳过；BattleEnded 后仍接受后续记录 | `1c8040afb0644349947aa236583e5ac9`、`50c1e937e8c04750bfea9e0e19635e20` | 显式穷尽当前 14 类；进入 BattleEnded 必须是末条记录 | `7304e2c7c6c549f4939461362ac97ed7`、`c81b6e7b30cd4bf4893645516a55d00d` |
| 串行可见时间线与加速 | runner 类型缺失；可见时间线抛 `NotSupportedException`；`SetSpeed` 缺失 | Unity 编译红灯、`d808a348884a4d0eb67b596c828f61fe` | 单一手动更新 DOTween `Sequence`，Prelude 后按扁平步骤追加 | `231d0ace8b294060817ba85cd031be9d`、`5aa674097f2e4273b1b38a7e7607f1b5` |
| 取消与 transient cleanup | 父 Sequence Kill 不会自动触发嵌套子 Tween 的清理，2 个断言失败 | `05d4c196dfb84a68b3072b730a8ef671` | concrete cue lease 绑定 Tween 与幂等 cleanup；正常、立即完成、Dispose、部分构建失败均由 runner 显式收口 | `75a705b560bd410587385454d626c791`，8/8；补强后最终 runner suite 全绿 |
| adapter 只消费完整 plan | 混合 Energy 记录仍占用固定表现等待 | `5b5af8c719d4449ba373834545ae4e1f` | adapter 只执行 `Plan.Create → Runner.Play`，删除 settlement subtype 分支 | `8f0d1ad0765445cf9d6b317de4ee672f`，11/11 |

补强审查后又新增首个可见 Effect target、三层只读集合、自然/立即完成 cleanup 和 adapter 加速转发五项证据；任务 `6b714139a5e1436bae4dd5e177131aa5` 为 **5/5 passed**。

## 不可变计划、Prelude 与 settlement Order

### 当前 14 类记录

| settlement | M9A 显式消费结果 |
|---|---|
| `BattleDamageAppliedSettlement` | 先实际格挡吸收数字；再实际生命损失数字与抖动；fatal 最后追加死亡过渡 |
| `BattleStatusAppliedSettlement` | 仅 Vulnerable 正增量派生图标脉冲 |
| `BattleBlockGainedSettlement` | 仅实际正增量派生格挡增加数字 |
| `BattleAttributeModifiedSettlement` | 仅 Strength 非零变化派生图标脉冲 |
| `BattleStatusReducedSettlement` | 仅 Vulnerable 实际衰减派生图标脉冲 |
| `BattleCardMovedSettlement` | 只为 DrawPile→Hand 与 Hand→DiscardPile 派生运动步骤；其他路线显式零可见 |
| `BattleCardsReshuffledSettlement` | 非空重洗顺序派生重洗步骤 |
| `BattleEnemyIntentAdvancedSettlement` | 派生意图脉冲 |
| `BattleEnergySpentSettlement` | 显式保留 entry，零可见 |
| `BattleOperationSkippedSettlement` | 显式保留 entry，零可见且不伪造反馈 |
| `BattleBlockClearedSettlement` | 显式保留 entry，零可见 |
| `BattleEnergyRefilledSettlement` | 显式保留 entry，零可见 |
| `BattleEnemyActionSkippedSettlement` | 显式保留 entry，零可见且不伪造反馈 |
| `BattlePhaseChangedSettlement` | 仅真实 phase 变化进入 PlayerAction / EnemyAction / BattleEnded 时派生对应步骤；同 phase 行动者交接零可见 |

### Prelude 与严格顺序

- StartBattle：唯一 StartBattle Prelude → `CardMoved(Order 0)` → `PlayerTurnBanner(Order 1)`。
- Strike：唯一 PlayCard Prelude 从 Hand→Discard 卡牌身份与首个可见 Damage target 派生；entry 仍为 `EnergySpent(0) → Damage(1) → Hand→Discard(2)`，可见扁平步骤不会把后置记录前移。
- Bash：entry 仍为 `EnergySpent(0) → Damage(1) → Vulnerable(2) → Hand→Discard(3)`；只有 Prelude 位于 Order 0 前，两个 Effect 与离手记录均未重排。
- Skip Effect 不能成为出牌目标；补强测试证明首个不可见 `OperationSkipped` 被忽略，并使用下一条首个可见 Effect 的冻结 target。
- 输入 settlement 必须从 0 连续且当前位置等于 `Order`；planner 不排序。多条 Hand→Discard、未知 concrete 类型或 BattleEnded 后仍有记录均同步拒绝。
- `SettlementEntries`、扁平 `SettlementSteps` 和每个 entry 的 `Steps` 都复制为只读集合；自动测试证明外部无法修改。

## completion、加速、立即完成与取消

- 没有 Prelude 且没有可见步骤的非空结果同步 completion，不创建 Tween，也不保留旧的 0.35 秒等待。
- 可见计划只由一个父 Sequence 串行拥有；正常 Tick、加速与立即完成沿同一个幂等门闩至多释放一次 completion。
- `Finish` 在调用 completion 前先清除旧 playback 所有权，因此 Queue completion 重入下一计划不会与旧计划重叠。
- 自然完成与立即完成均显式清理三条测试 cue lease 一次；重复立即完成、后续 Tick 和后续 Dispose 不会二次清理或二次 completion。
- owner / Scene Scope 销毁时 Kill 当前父 Sequence、逐条清理 cue lease 并丢弃 completion；后续 Tick 或立即完成不会迟到释放旧 Queue。
- factory 同步构建中抛错会 Kill 已构建部分、清理 lease、原样抛出且不调用 completion；Queue 的 post-write presentation fault 继续由既有 M8 seam 处理。
- concrete adapter 的 `SetPresentationSpeed` 已有转发自动证据；没有新增第二 completion、计时器、动画命令队列或玩家 Skip 按钮。

只读代码审查没有 P1 / P2。两项中低风险留给接入真实 View 的后续切片约束：factory 构建不得重入 runner；M9C / M9E concrete transient cleanup 必须幂等且不抛。当前 adapter 的 factory 不重入、cleanup 为空，不构成 M9A 生产触发路径，也不授权新增错误通道。

## M8 Queue 回归与静态构建

| 检查 | 最终结果 |
|---|---|
| Plan / Runner / Adapter + settlement contract + M8B / M8D | **83/83 passed，0 failed，0 skipped**；任务 `f3703ba76c4e4d8d9472f27215a32d81` |
| 完整 `BattleCommandQueueTests` / M8B / M8D + `BattleEffectCommandQueueTests` | **57/57 passed，0 failed，0 skipped**；任务 `c64fce57df5c4d55812e2a7c3efce75e` |
| M9A Plan / Runner / Adapter 独立聚焦 | **40/40 passed，0 failed，0 skipped**；任务 `3cf9c5dde5bc4574ba695e872890f7a0` |
| `BattleSettlementContractTests` 独立聚焦 | **21/21 passed，0 failed，0 skipped**；任务 `015b54672c4241a4924081f39108fe21` |
| `dotnet build TinySpire/TinySpire.sln --no-restore -m:1` | **0 error，12 warnings**；均为既有 Unity / R3 / UniTask 依赖程序集版本冲突 |
| `git diff --check` | 代码完成后与文档停止点后均通过 |
| Unity Console | 最终受控测试只产生 Test Runner 的 `TestResults.xml` 保存信息行；清除此信息行后 Error / Warning 查询为 **0/0**，此前的 UnityConnect token 网络异常未在最终受控回归中复现 |

回归覆盖既有 Queued / lifecycle、非重入 drain、accepted / continuation / presentation-submit FIFO、旧 completion 隔离、一次 token / 屏障、post-write presentation fault、敌人多行动者反馈屏障和 BattleEnded 尾部稳定拒绝。表现等待期间第二条合法 PlayCard 仍可提交并进入 Queue，未用 `IsWaitingForPresentation` 全局锁玩家输入。

## 范围与工作区保护

M9A authored 代码范围严格为：

- `TinySpire/Assets/Scripts/UI/Battle/BattleCommandPresentationAdapter.cs`
- `TinySpire/Assets/Scripts/UI/Battle/Feedback/BattleCommandPresentationPlan.cs` 及 Unity 生成 Meta
- `TinySpire/Assets/Scripts/UI/Battle/Feedback/BattleCommandPresentationRunner.cs` 及 Unity 生成 Meta
- `TinySpire/Assets/Editor/Tests/BattleCommandPresentationAdapterTests.cs`
- `TinySpire/Assets/Editor/Tests/BattleCommandPresentationPlanTests.cs` 及 Unity 生成 Meta
- `TinySpire/Assets/Editor/Tests/BattleCommandPresentationRunnerTests.cs` 及 Unity 生成 Meta

没有修改 Queue、Turn、Combatant、Intent、CardZones、settlement、Effect 公式、目标、终局规则、`BattleLifetimeScope`、Scene、Prefab、配置表、生成战斗配置、GameData、ProjectSettings、asmdef、HybridCLR、启动 / DI / Run / 网络 / 多人架构。启动基线中列明的四组受保护用户改动保持排除；未清理、回退、移动、用作资源、暂存或覆盖。未 commit、未 push。

## 不适用且未冒充通过的验证

- M9A 没有修改 Prefab、Sprite 或任何可寻址 Scene 依赖，因此没有运行 Local Content；这不是把 Addressables 写成已通过。
- 没有修改 DataTables / Localization，因此没有运行 Luban、Localization 同步或 `Sync and Build All`。
- 本切片只建立纯 plan / runner 与 adapter 门闩；Bootstrap、真实 Game View、真实系统指针、连续动画帧和多宽高比属于后续 concrete View 切片及 M9G，当前没有写成已通过。
- 没有提前实现 M9B 的常驻 HUD、M9C 的 concrete 反馈、M9D 的目标素材、M9E 的卡区运动或 M9F 的横幅 / 终局 / 重开 / 退出。

## 停止点判定与后续

M9A 的 14 类记录、同记录子序、零可见直通、Prelude 互斥与唯一性、严格 Order、同步 / 异步 completion、重复 / 重入、加速、立即完成、自然 / 取消清理、owner 销毁、表现同步异常，以及 M8 Queue lifecycle / FIFO / barrier / fault / terminal 均有自动证据。串行 build、Console 与范围审计通过；验收页、测试索引、计划状态、计划索引和 `SESSION_LOG.md` 已同步。

M9A 停止点完成。下一步只进入 M9B · Block、状态与既有意图 HUD；M9C～M9G 仍保持待实施。
